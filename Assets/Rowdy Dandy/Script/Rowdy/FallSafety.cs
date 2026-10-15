using UnityEngine;

// Safety net for collision bugs that throw Rowdy out of the level (2026-10-13 playtest: falling through walls, pushed
// into the beach ground). It doesn't fix the spots - it stops them from ruining a run and logs where they are:
//   - remembers where he stood on solid ground (a few samples, so the spot it goes back to is ~1 s old, not the glitch)
//   - he falls below the lowest terrain in the scene, or falls with nothing at all under him (no ground, no water)
//     for a moment, or is stuck inside solid ground = put back on that spot
//   - every rescue logs "[FallSafety] ..." with the position, so the real collider can be fixed
// Added by Health.Awake on Rowdy.
[DisallowMultipleComponent]
public class FallSafety : MonoBehaviour
{
    private const float SampleEvery = 0.25f, NoGroundRescue = 0.7f, StuckRescue = 0.3f, RayDepth = 60f;

    private Rigidbody2D rb;
    private PlayerMovement move;
    private Health health;
    private Collider2D body;
    private readonly Vector2[] safe = new Vector2[5];
    private int safeCount, safeNext;
    private Vector2 lastPos;
    private float killY = float.NegativeInfinity, groundedFor, sampleAt, noGroundFor, stuckFor, checkAt, cooldownUntil;
    private static readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    private static ContactFilter2D AllHits => new ContactFilter2D { useTriggers = false, useLayerMask = false };

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        move = GetComponent<PlayerMovement>();
        health = GetComponent<Health>();
        body = GetComponent<Collider2D>();
        // the lowest solid terrain in the scene: anything well below that is out of the map
        float low = float.PositiveInfinity;
        foreach (Collider2D c in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            if (c.CompareTag("Ground") && SolidGround.IsGround(c)) low = Mathf.Min(low, c.bounds.min.y);
        if (!float.IsPositiveInfinity(low)) killY = low - 4f;
        lastPos = rb != null ? rb.position : (Vector2)transform.position;
        Remember(lastPos);
    }

    private void Remember(Vector2 p)
    {
        safe[safeNext] = p;
        safeNext = (safeNext + 1) % safe.Length;
        safeCount = Mathf.Min(safeCount + 1, safe.Length);
    }

    // the oldest sample: about a second before now
    private Vector2 SafeSpot => safeCount < safe.Length ? safe[0] : safe[safeNext];

    private void FixedUpdate()
    {
        if (rb == null || move == null || !move.enabled || (health != null && health.IsDead) || PauseMenu.IsPaused) return;
        if (Werewolf.Active || StephmossForm.Active) return; // their own movement; nothing to compare against
        float dt = Time.fixedDeltaTime;
        Vector2 p = rb.position;
        // a teleport (checkpoint reload, colosseum gates, portals): the old safe spots belong to another place
        if ((p - lastPos).sqrMagnitude > 15f * 15f) { safeCount = 0; safeNext = 0; Remember(p); cooldownUntil = Time.time + 1f; }
        lastPos = p;

        // standing still-ish on real ground (not mid-glitch): a safe spot
        bool grounded = move.IsGrounded && Mathf.Abs(rb.linearVelocity.y) < 0.5f;
        groundedFor = grounded ? groundedFor + dt : 0f;
        if (groundedFor > 0.3f && Time.time >= sampleAt && !InsideGround(p))
        {
            sampleAt = Time.time + SampleEvery;
            Remember(p);
        }
        if (Time.time < cooldownUntil) return;

        // (runtime-built areas can sit lower than the scene's terrain: below killY only counts with nothing under him)
        if (p.y < killY && Time.time >= checkAt && NothingBelow(p)) { Rescue("fell below the map"); return; }

        // falling with nothing under him at all
        if (!grounded && rb.linearVelocity.y < -1f)
        {
            if (Time.time >= checkAt)
            {
                checkAt = Time.time + 0.1f;
                if (NothingBelow(p)) noGroundFor += 0.1f; else noGroundFor = 0f;
            }
            if (noGroundFor >= NoGroundRescue) { Rescue("fell with no ground under him"); return; }
        }
        else noGroundFor = 0f;

        // pushed inside solid ground
        stuckFor = InsideGround(p) ? stuckFor + dt : 0f;
        if (stuckFor >= StuckRescue) Rescue("stuck inside the ground");
    }

    private bool InsideGround(Vector2 p)
    {
        Vector2 c = body != null ? (Vector2)body.bounds.center : p + Vector2.up * 0.5f;
        return SolidGround.Blocked(c, new Vector2(0.12f, 0.12f));
    }

    // no collider of any kind under him (ground, one-way platform or water)
    private bool NothingBelow(Vector2 p)
    {
        int n = Physics2D.Raycast(p, Vector2.down, AllHits, hits, RayDepth);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = hits[i].collider;
            if (c == null || c.isTrigger || c.attachedRigidbody == rb) continue;
            if (SolidGround.IsGround(c) || c.CompareTag("Water") || c.gameObject.layer == 6) return false;
        }
        return true;
    }

    private void Rescue(string why)
    {
        Vector2 from = rb.position, to = SafeSpot;
        Debug.LogWarning("[FallSafety] Rowdy " + why + " at (" + from.x.ToString("0.00") + ", " + from.y.ToString("0.00") + ") - back to (" + to.x.ToString("0.00") + ", " + to.y.ToString("0.00") + ")");
        rb.position = to + Vector2.up * 0.05f;
        transform.position = rb.position;
        rb.linearVelocity = Vector2.zero;
        noGroundFor = stuckFor = 0f;
        cooldownUntil = Time.time + 1f;
        PulseRing.Spawn((Vector3)to + Vector3.up * 0.5f, new Color(1f, 0.9f, 0.6f, 0.9f), 1.2f, 0.35f);
    }
}
