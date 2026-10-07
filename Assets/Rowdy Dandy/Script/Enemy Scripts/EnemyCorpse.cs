using System.Collections.Generic;
using UnityEngine;

// Keeps dead enemies calm, grounded and out of Rowdy's way. Added automatically by EnemyMovement
// (only to walking enemies: not flyers, plants or anything with effectors); EnemyHealth calls OnKilled.
//
// The death clips keep doing their thing (sprites, colors, lights, triggers...). On death this:
//  - stops the AI (a dead enemy kept chasing / leaping, which launched bodies)
//  - takes the body out of the physics simulation (kinematic), so nothing can push, fling or bounce it
//  - lowers it to the floor below at a capped speed, then tilts it to match the slope
//  - lets Rowdy (and other enemies) pass through it right away
// Drownable enemies still sink through water. Everything is undone if the enemy is revived.
// Runs after other scripts so nothing (AI re-enabled by a death clip, etc.) can move the body after it settles
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyCorpse : MonoBehaviour
{
    [SerializeField] private bool alignToGround = true;
    [SerializeField] private float maxAlignAngle = 40f;
    [SerializeField] private float alignSpeed = 300f;  // degrees per second
    [SerializeField] private float fallGravity = 25f;
    [SerializeField] private float maxFallSpeed = 9f;
    [Tooltip("Stop falling after this long without finding ground (e.g. sank out of view).")]
    [SerializeField] private float maxFallTime = 4f;
    [Tooltip("Max sinking speed for drownable bodies under water.")]
    [SerializeField] private float waterSinkSpeed = 0.9f;

    // Solid colliders of dead bodies, so enemies that spawn later walk through them too
    private static readonly List<Collider2D> DeadSolids = new List<Collider2D>();

    private EnemyHealth health;
    private EnemyMovement movement;
    private EnemyDrowning drowning;
    private Rigidbody2D rb;
    private Collider2D[] ownColliders;
    private Collider2D[] solidColliders;
    private Collider2D feet;
    private readonly List<Collider2D> ignored = new List<Collider2D>();
    private readonly List<MonoBehaviour> stoppedAI = new List<MonoBehaviour>();

    private RigidbodyType2D originalBodyType;
    private bool isDead;
    private bool landed;
    private float feetOffset;   // transform.y - bottom of the body, measured at death
    private float fallSpeed;
    private float fallTime;
    private float targetAngle;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>();
        rb = GetComponent<Rigidbody2D>();
        ownColliders = GetComponentsInChildren<Collider2D>(true);

        var solid = new List<Collider2D>();
        foreach (Collider2D col in ownColliders)
        {
            if (col != null && !col.isTrigger && !col.usedByEffector) solid.Add(col);
        }
        solidColliders = solid.ToArray();

        // Walk through bodies that are already lying around
        foreach (Collider2D dead in DeadSolids) SetIgnore(solidColliders, dead, true);
    }

    private void OnDestroy()
    {
        foreach (Collider2D col in solidColliders) DeadSolids.Remove(col);
    }

    public void OnKilled()
    {
        if (isDead || rb == null) return;
        isDead = true;
        landed = false;
        drowning = GetComponent<EnemyDrowning>(); // added at runtime by EnemyMovement, possibly after this Awake
        fallSpeed = 0f;
        fallTime = 0f;
        targetAngle = 0f;

        // 1. The AI stops
        stoppedAI.Clear();
        foreach (MonoBehaviour ai in new MonoBehaviour[] { movement, GetComponent<MeleeEnemy>() })
        {
            if (ai != null && ai.enabled) { ai.enabled = false; stoppedAI.Add(ai); }
        }

        // 2. Measure where its feet are, before anything rotates
        feet = PickFeetCollider();
        if (feet != null)
        {
            feetOffset = transform.position.y - feet.bounds.min.y;
        }
        else
        {
            // No solid collider: use the bottom of the sprite
            SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
            feetOffset = sprite != null ? transform.position.y - sprite.bounds.min.y : 0f;
        }

        // 3. Out of the physics simulation: no more pushes, launches or bounces
        originalBodyType = rb.bodyType;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // 4. Rowdy, his attacks and other enemies pass through the body
        ignored.Clear();
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) ignored.AddRange(player.transform.root.GetComponentsInChildren<Collider2D>(true));
        foreach (EnemyHealth other in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (other != health) ignored.AddRange(other.GetComponentsInChildren<Collider2D>(true));
        }
        foreach (Collider2D col in ignored) SetIgnore(solidColliders, col, true);
        DeadSolids.AddRange(solidColliders);
    }

    private void FixedUpdate()
    {
        if (!isDead) return;

        if (!health.enemydead)
        {
            Revive();
            return;
        }

        // Some death clips keep EnemyMovement running (they key its enabled flag / moveSpeed), and a kinematic
        // body keeps any velocity it's given forever. The body only moves where this script puts it.
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        float dt = Time.fixedDeltaTime;
        Vector2 position = rb.position;
        float feetX = feet != null ? feet.bounds.center.x : position.x;
        float feetY = position.y - feetOffset;

        if (!landed && fallTime < maxFallTime)
        {
            fallTime += dt;
            float maxSpeed = drowning != null && drowning.IsInWater() ? waterSinkSpeed : maxFallSpeed;
            fallSpeed = Mathf.Min(fallSpeed + fallGravity * dt, maxSpeed);
            float step = fallSpeed * dt;

            // Look from a little above the feet down past this step's movement
            if (FindGround(new Vector2(feetX, feetY + 0.4f), 0.4f + step + 0.02f, out RaycastHit2D hit))
            {
                position.y = hit.point.y + feetOffset;
                landed = true;
                fallSpeed = 0f;
                targetAngle = SlopeAngle(feetX, hit);
            }
            else
            {
                position.y -= step;
            }
            rb.position = position;
        }

        if (alignToGround)
        {
            rb.rotation = Mathf.MoveTowardsAngle(rb.rotation, landed ? targetAngle : 0f, alignSpeed * dt);
        }
    }

    // Average slope under the body, from rays at both ends of its feet
    private float SlopeAngle(float feetX, RaycastHit2D centerHit)
    {
        float halfWidth = feet != null ? feet.bounds.extents.x : 0.2f;
        float y = centerHit.point.y + 0.4f;
        if (FindGround(new Vector2(feetX - halfWidth, y), 1f, out RaycastHit2D left) &&
            FindGround(new Vector2(feetX + halfWidth, y), 1f, out RaycastHit2D right))
        {
            Vector2 across = right.point - left.point;
            float angle = Mathf.Atan2(across.y, across.x) * Mathf.Rad2Deg;
            if (Mathf.Abs(angle) <= maxAlignAngle) return angle;
        }

        float normalAngle = Mathf.Atan2(centerHit.normal.y, centerHit.normal.x) * Mathf.Rad2Deg - 90f;
        return Mathf.Abs(normalAngle) <= maxAlignAngle ? normalAngle : 0f;
    }

    // First solid piece of the level below a point (not characters, not water a drownable enemy sinks through)
    private bool FindGround(Vector2 origin, float distance, out RaycastHit2D result)
    {
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(origin, Vector2.down, distance))
        {
            Collider2D col = hit.collider;
            if (col == null || col.isTrigger) continue;
            if (col.transform.IsChildOf(transform)) continue;
            if (col.usedByEffector && col.GetComponent<PlatformEffector2D>() == null) continue;
            if (col.attachedRigidbody != null && col.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue; // characters, jellies, pickups
            if (col.CompareTag("Player") || col.CompareTag("Enemy")) continue;
            if (movement != null && movement.PassesThroughLayer(col.gameObject.layer)) continue;

            result = hit;
            return true;
        }
        result = default;
        return false;
    }

    // The collider that represents the body's feet: a solid one on the root, else the lowest solid one
    private Collider2D PickFeetCollider()
    {
        Collider2D best = null;
        foreach (Collider2D col in solidColliders)
        {
            if (col == null || !col.enabled) continue;
            if (col.gameObject == gameObject) return col;
            if (best == null || col.bounds.min.y < best.bounds.min.y) best = col;
        }
        return best;
    }

    private void Revive()
    {
        isDead = false;
        foreach (Collider2D col in ignored) SetIgnore(solidColliders, col, false);
        ignored.Clear();
        foreach (Collider2D col in solidColliders) DeadSolids.Remove(col);

        rb.bodyType = originalBodyType;
        rb.rotation = health.initialrotationenemy.eulerAngles.z;

        foreach (MonoBehaviour ai in stoppedAI) if (ai != null) ai.enabled = true;
        stoppedAI.Clear();
    }

    private static void SetIgnore(Collider2D[] colliders, Collider2D other, bool ignore)
    {
        if (colliders == null || other == null) return;
        foreach (Collider2D col in colliders)
        {
            if (col != null && col != other) Physics2D.IgnoreCollision(col, other, ignore);
        }
    }
}
