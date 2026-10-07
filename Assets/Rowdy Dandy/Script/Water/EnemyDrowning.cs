using UnityEngine;

// Drownable enemies: hitting the water brakes the fall hard, then they sink slowly while struggling,
// and drown quickly. Added automatically by EnemyMovement to enemies with "Is Drownable" ticked
// (add it by hand to tweak one enemy). Splashes and bubbles come from WaterSplashBody.
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyDrowning : MonoBehaviour
{
    [Tooltip("Seconds from touching the water to dying.")]
    [SerializeField] private float drownTime = 0.9f;

    [Header("Hitting The Water")]
    [Tooltip("How much of the falling speed survives the impact.")]
    [Range(0f, 1f)] [SerializeField] private float entryFallSpeedKept = 0.25f;
    [Range(0f, 1f)] [SerializeField] private float entrySideSpeedKept = 0.3f;

    [Header("Sinking")]
    [Tooltip("Gravity multiplier while under water.")]
    [Range(0f, 1f)] [SerializeField] private float underwaterGravity = 0.2f;
    [SerializeField] private float underwaterDrag = 4f;
    [SerializeField] private float maxSinkSpeed = 1.4f;

    [Header("Struggling")]
    [SerializeField] private float struggleInterval = 0.25f;
    [SerializeField] private float struggleKick = 0.8f;

    private Rigidbody2D rb;
    private EnemyHealth health;
    private EnemyMovement movement;
    private MeleeEnemy melee;
    private Collider2D body;

    private bool drowning;
    private float drownStart;
    private float lastInWaterTime;
    private float nextStruggle;
    private float savedGravity, savedDrag;
    private bool movementWasOn, meleeWasOn;

    public bool IsDrowning => drowning;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>();
        melee = GetComponent<MeleeEnemy>();

        foreach (Collider2D col in GetComponents<Collider2D>())
        {
            if (!col.isTrigger) { body = col; break; }
        }
    }

    private void OnDisable()
    {
        if (drowning) StopDrowning();
    }

    private void FixedUpdate()
    {
        bool dead = health != null && health.enemydead;
        bool inWater = IsInWater();
        if (inWater) lastInWaterTime = Time.time;

        if (!drowning)
        {
            if (inWater && !dead) StartDrowning();
            return;
        }

        // Killed by something else, or pulled out (revived / respawned) -> back to normal
        if (dead || Time.time - lastInWaterTime > 0.5f)
        {
            StopDrowning();
            return;
        }

        Vector2 v = rb.linearVelocity;
        v.y = Mathf.Max(v.y, -maxSinkSpeed);

        // Flailing: little kicks that slow the sinking without saving them
        if (Time.time >= nextStruggle)
        {
            nextStruggle = Time.time + struggleInterval * Random.Range(0.7f, 1.3f);
            v.x = Random.Range(-1f, 1f) * struggleKick;
            v.y = Mathf.Max(v.y, 0f) + struggleKick * Random.Range(0.3f, 0.8f);
        }
        rb.linearVelocity = v;

        if (Time.time - drownStart >= drownTime)
        {
            Drown();
        }
    }

    // A point a bit above the feet, so the brake happens as soon as the body is in, not when half sunk
    public bool IsInWater()
    {
        Bounds b = body != null && body.enabled ? body.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
        Vector2 point = new Vector2(b.center.x, b.min.y + b.size.y * 0.25f);
        return IsWaterAt(point, WaterMask());
    }

    public static bool IsWaterAt(Vector2 point, int mask)
    {
        if (mask != 0 && Physics2D.OverlapPoint(point, mask) != null) return true;

        DynamicWater2D water = DynamicWater2D.FindAt(point, 0f);
        return water != null && point.y < water.SurfaceY(point.x);
    }

    private int WaterMask()
    {
        int mask = movement != null ? movement.DrowningWaterMask : 0;
        WaterSplashFX fx = WaterSplashFX.Existing;
        if (fx != null) mask |= fx.WaterMask;
        return mask;
    }

    private void StartDrowning()
    {
        drowning = true;
        drownStart = Time.time;
        nextStruggle = Time.time + struggleInterval;

        savedGravity = rb.gravityScale;
        savedDrag = rb.linearDamping;
        rb.gravityScale = savedGravity * underwaterGravity;
        rb.linearDamping = underwaterDrag;

        Vector2 v = rb.linearVelocity;
        rb.linearVelocity = new Vector2(v.x * entrySideSpeedKept, v.y < 0f ? v.y * entryFallSpeedKept : v.y);

        // The AI stops walking / attacking: it's busy drowning
        movementWasOn = movement != null && movement.enabled;
        meleeWasOn = melee != null && melee.enabled;
        if (movementWasOn) movement.enabled = false;
        if (meleeWasOn) melee.enabled = false;
    }

    private void StopDrowning()
    {
        drowning = false;
        rb.gravityScale = savedGravity;
        rb.linearDamping = savedDrag;
        if (movementWasOn && movement != null) movement.enabled = true;
        if (meleeWasOn && melee != null) melee.enabled = true;
        movementWasOn = meleeWasOn = false;
    }

    private void Drown()
    {
        // Restore first so EnemyCorpse records the AI as running and can bring it back on revive
        StopDrowning();

        if (health != null && !health.enemydead && health.currentenemyHealth > 0f)
        {
            EnemyHealth.CreditNextHit(KillCredit.Drowning());
            health.TakeDamageEnemy(health.currentenemyHealth);
        }
    }
}
