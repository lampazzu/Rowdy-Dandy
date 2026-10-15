using UnityEngine;

// Manta Rays in a colosseum (FrontierArena adds this to every arena manta): instead of patrolling back and forth
// they hunt Rowdy - steering toward him at a height he can reach, swooping past and turning back (their velocity
// turns smoothly, so they overshoot and loop). They stay perfectly level: no tilting / rolling, only a left / right
// flip. The patrol AI is switched off while this runs; dead mantas are left to their death clip.
public class ArenaMantaChase : MonoBehaviour
{
    [HideInInspector] public float floorY;
    [HideInInspector] public float minHeight = 0.6f, maxHeight = 2.2f; // over the arena floor (Rowdy reaches ~1.3 up)

    private const float Speed = 2.8f, Steer = 4.5f;

    private Rigidbody2D rb;
    private EnemyMovement ai;
    private EnemyHealth health;
    private Vector2 vel;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        ai = GetComponent<EnemyMovement>();
        health = GetComponentInParent<EnemyHealth>();
        if (health == null) health = GetComponentInChildren<EnemyHealth>();
        if (ai != null) ai.enabled = false;
        if (rb != null) { rb.gravityScale = 0f; rb.freezeRotation = true; }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        if (health != null && health.enemydead) { enabled = false; return; }
        if (StatusEffects.IsStunned(gameObject)) { vel = Vector2.zero; rb.linearVelocity = Vector2.zero; return; }
        if (BoonRunner.Rowdy == null) return;

        Vector2 me = rb.position;
        Vector2 target = BoonRunner.RowdyCenter;
        target.y = Mathf.Clamp(target.y, floorY + minHeight, floorY + maxHeight);
        Vector2 dir = target - me;
        Vector2 want = dir.sqrMagnitude > 0.0001f ? dir.normalized * Speed : Vector2.zero;
        vel = Vector2.MoveTowards(vel, want, Steer * Time.fixedDeltaTime); // a smooth turn: it overshoots and loops back
        rb.linearVelocity = vel;
    }

    private void LateUpdate()
    {
        transform.rotation = Quaternion.identity; // level, whatever a clip or the old AI did
        if (Mathf.Abs(vel.x) > 0.3f && ai != null) ai.FaceTowards(transform.position.x + Mathf.Sign(vel.x));
    }
}
