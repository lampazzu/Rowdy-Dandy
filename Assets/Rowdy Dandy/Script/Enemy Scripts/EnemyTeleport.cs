using System.Collections;
using UnityEngine;

// GNOLL ARCHERS & BOMBERS TELEPORT. Every so often (cooldown) a shooter that's badly placed - Rowdy right in its face,
// Rowdy out of its line / on another floor, or Rowdy too far away - comes apart pixel by pixel (Shaders/Deconstruct:
// a glitchy sweep, the breaking pixels lit up), a portal sigil opens where it's going (fair warning), and it builds
// itself back up there, facing Rowdy. Untouchable while it's gone; it waits a moment before shooting again.
// Spots: solid floor near Rowdy's floor, on screen, not in water, not inside a wall, inside the colosseum during a
// trial. Added by EnemyHealth.Start to every Gnoll Archer / Gnoll Bomber.
public class EnemyTeleport : MonoBehaviour
{
    private static readonly Color Edge = new Color(1f, 0.45f, 1f);
    private static readonly Color Glow = new Color(0.85f, 0.4f, 1f, 0.9f);

    private EnemyHealth health;
    private MeleeEnemy melee;
    private EnemyMovement mover;
    private Rigidbody2D rb;
    private SpriteRenderer body;
    private Animator anim;
    private Collider2D solid;
    private bool bomber, busy;
    private float readyAt, lostSight;
    private Material originalMat;
    private MaterialPropertyBlock savedBlock;

    public bool Busy => busy;

    public static void TryAttach(EnemyHealth e)
    {
        if (e == null || e.IsObject || e.GetComponent<EnemyTeleport>() != null) return;
        EnemyCatalog.Entry kind = EnemyCatalog.Identify(e);
        if (kind == null || (kind.id != "gnollarcher" && kind.id != "gnollbomber")) return;
        e.gameObject.AddComponent<EnemyTeleport>().bomber = kind.id == "gnollbomber";
    }

    private void Start()
    {
        health = GetComponent<EnemyHealth>();
        melee = GetComponent<MeleeEnemy>();
        mover = GetComponent<EnemyMovement>();
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        foreach (Collider2D c in GetComponents<Collider2D>()) if (c.enabled && !c.isTrigger) { solid = c; break; }
        readyAt = Time.time + Random.Range(2.5f, 4f); // never right after spawning
        if (Material == null || body == null || rb == null) enabled = false;
    }

    private float Cooldown => (bomber ? Random.Range(7f, 9f) : Random.Range(6f, 8.5f)) * (FrontierArena.ArenaAt(transform.position) != null ? 0.8f : 1f);

    // ---------------------------------------------------------------- when
    private void Update()
    {
        if (busy || health == null || health.enemydead || BoonRunner.Rowdy == null || PauseMenu.IsPaused) return;
        Vector3 me = solid != null ? solid.bounds.center : transform.position;
        Vector3 rowdy = BoonRunner.RowdyCenter;
        Vector2 d = rowdy - me;
        bool engaged = d.magnitude < 18f;
        lostSight = engaged && melee != null && !melee.SeesPlayer ? lostSight + Time.deltaTime : 0f;
        if (Time.time < readyAt || !engaged || !InIdleClip() || StatusEffects.IsStunned(gameObject)) return;

        bool tooClose = Mathf.Abs(d.x) < 2.8f && Mathf.Abs(d.y) < 2.2f;
        bool otherFloor = Mathf.Abs(d.y) > 2.6f;
        bool tooFar = Mathf.Abs(d.x) > (bomber ? 9f : 12f);
        bool blind = lostSight > 2.5f;
        if (!tooClose && !otherFloor && !tooFar && !blind) return;

        if (FindSpot(rowdy, me, tooClose, out Vector3 spot)) StartCoroutine(Teleport(spot));
        else readyAt = Time.time + 1f; // nowhere good right now: look again in a moment
    }

    private bool InIdleClip()
    {
        if (anim == null || !anim.isActiveAndEnabled) return true;
        AnimatorClipInfo[] info = anim.GetCurrentAnimatorClipInfo(0);
        if (info.Length == 0 || info[0].clip == null) return true;
        return info[0].clip.name.ToLowerInvariant().Contains("idle");
    }

    // ---------------------------------------------------------------- where
    private bool FindSpot(Vector3 rowdy, Vector3 me, bool escaping, out Vector3 spot)
    {
        spot = Vector3.zero;
        float rowdyFloor = SolidGround.Ray(rowdy, Vector2.down, 12f, out RaycastHit2D under) ? under.point.y : rowdy.y - 0.7f;
        FrontierArena arena = FrontierArena.ArenaAt(rowdy);
        Camera cam = Camera.main;
        float camL = float.MinValue, camR = float.MaxValue;
        if (cam != null && cam.orthographic)
        {
            float half = cam.orthographicSize * cam.aspect;
            camL = cam.transform.position.x - half + 0.8f;
            camR = cam.transform.position.x + half - 0.8f;
        }
        Vector2 size = solid != null ? (Vector2)solid.bounds.size : new Vector2(0.6f, 1f);
        float[] dists = bomber ? new[] { 5.5f, 4.5f, 6.5f, 3.8f } : new[] { 7f, 6f, 8.5f, 5f };
        // escaping keeps its side of Rowdy first; otherwise either side, at random
        float first = escaping ? Mathf.Sign(me.x - rowdy.x + 0.01f) : (Random.value < 0.5f ? -1f : 1f);
        float bestScore = float.MaxValue;
        bool found = false;
        foreach (float side in new[] { first, -first })
            foreach (float dist in dists)
            {
                float x = rowdy.x + side * dist;
                if (arena != null) x = Mathf.Clamp(x, arena.leftX + 0.8f, arena.rightX - 0.8f);
                if (Mathf.Abs(x - rowdy.x) < 3f) continue;
                bool onScreen = x >= camL && x <= camR;
                if (!onScreen && arena == null) continue;
                Vector2 from = new Vector2(x, rowdyFloor + 3.5f);
                if (!SolidGround.Ray(from, Vector2.down, 8f, out RaycastHit2D hit)) continue;
                if (Mathf.Abs(hit.point.y - rowdyFloor) > 3f) continue;
                if (SolidGround.Blocked(hit.point + Vector2.up * (size.y * 0.5f + 0.1f), size * 0.8f)) continue;
                if (InWater(from, hit.distance)) continue;
                if (arena == null && SolidGround.Line(new Vector2(rowdy.x, rowdyFloor + 1f), hit.point + Vector2.up * 1f, out _)) continue; // not behind a wall
                float score = Mathf.Abs(hit.point.y - rowdyFloor) * 2f + (onScreen ? 0f : 6f) + (side == first ? 0f : 1.5f) + Mathf.Abs(dist - dists[0]) * 0.3f;
                if (score < bestScore) { bestScore = score; spot = hit.point; found = true; }
            }
        return found;
    }

    private static bool InWater(Vector2 from, float groundDistance)
    {
        int mask = 1 << 6;
        int water = LayerMask.NameToLayer("Water");
        if (water >= 0) mask |= 1 << water;
        RaycastHit2D w = Physics2D.Raycast(from, Vector2.down, groundDistance + 0.05f, mask);
        return w.collider != null && w.distance < groundDistance - 0.05f;
    }

    // ---------------------------------------------------------------- how
    private static Material material;
    private static bool tried;
    private static MaterialPropertyBlock block;
    private static readonly int ProgressId = Shader.PropertyToID("_Progress"), EdgeId = Shader.PropertyToID("_Edge"),
        SweepId = Shader.PropertyToID("_Sweep"), RectId = Shader.PropertyToID("_Rect"), SeedId = Shader.PropertyToID("_Seed");

    private static Material Material
    {
        get
        {
            if (!tried)
            {
                tried = true;
                Shader s = Resources.Load<Shader>("Shaders/Deconstruct");
                if (s == null) s = Shader.Find("Rowdy Dandy/Deconstruct");
                if (s != null && s.isSupported) material = new Material(s) { name = "Deconstruct" };
            }
            return material;
        }
    }

    private IEnumerator Teleport(Vector3 spot)
    {
        busy = true;
        readyAt = Time.time + Cooldown;
        lostSight = 0f;
        float footOffset = solid != null ? transform.position.y - solid.bounds.min.y : 0f;
        Vector3 startCenter = solid != null ? solid.bounds.center : transform.position;

        // no shot while it's gone (an aim already showing is dropped)
        bool meleeWas = melee != null && melee.enabled;
        if (melee != null) { melee.StopAllCoroutines(); melee.enabled = false; }
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false; // untouchable: no collisions, no hits

        // the rest of its renderers (elite outline, poison overlay...) hide while it's apart
        SpriteRenderer[] extras = GetComponentsInChildren<SpriteRenderer>();
        bool[] extrasWere = new bool[extras.Length];
        for (int i = 0; i < extras.Length; i++) { extrasWere[i] = extras[i].enabled; if (extras[i] != body) extras[i].enabled = false; }

        Material original = originalMat = body.sharedMaterial;
        var saved = savedBlock = new MaterialPropertyBlock();
        body.GetPropertyBlock(saved);
        body.sharedMaterial = Material;
        float seed = Random.value * 100f;

        // where it's going: the sigil opens first
        int order = body.sortingOrder;
        SheetFX portal = GraftFX.Play("BRJ_Portal_Create", spot + Vector3.up * 0.75f, new Color(1f, 0.75f, 1f, 0.95f), order - 1);
        FXSound.Play("Teleport", 0.45f, Random.Range(1.05f, 1.2f));

        // DECONSTRUCT: top first, pixels breaking off and drifting up
        yield return Sweep(0f, 1f, 0.38f, 1f, seed, true);
        GraftFX.Play("BRJ_WeirdTeleport", startCenter, new Color(1f, 0.8f, 1f, 0.9f), order + 1);
        FXParticle.Burst(startCenter, Glow, 10, 1f, 3f, -2f, 0.5f);

        // across
        Vector3 to = new Vector3(spot.x, spot.y + footOffset + 0.02f, transform.position.z);
        transform.position = to;
        rb.position = to;
        if (mover != null && BoonRunner.Rowdy != null) mover.FaceTowards(BoonRunner.RowdyCenter.x);
        yield return new WaitForSeconds(0.12f);

        // REBUILD: from the feet up, pixels gathering in
        Vector3 endCenter = solid != null ? solid.bounds.center : to;
        PulseRing.Spawn(endCenter, Glow, 1.1f, 0.3f);
        yield return Sweep(1f, 0f, 0.38f, 1f, seed + 13f, false);

        if (this == null || body == null) yield break;
        body.sharedMaterial = original;
        body.SetPropertyBlock(saved);
        for (int i = 0; i < extras.Length; i++) if (extras[i] != null && extras[i] != body) extras[i].enabled = extrasWere[i];
        if (portal != null) portal.Stop(0.25f);
        rb.simulated = true;
        rb.linearVelocity = Vector2.zero;
        if (melee != null && meleeWas) melee.enabled = true; // OnEnable = a short grace before the next shot
        FXParticle.Burst(endCenter, Glow, 8, 0.5f, 2f, 1f, 0.4f, true);
        busy = false;
    }

    private IEnumerator Sweep(float from, float to, float time, float sweep, float seed, bool breaking)
    {
        if (block == null) block = new MaterialPropertyBlock();
        float puffAt = 0f;
        for (float t = 0f; t <= 1f; t += Time.deltaTime / time)
        {
            if (this == null || body == null) yield break;
            float p = Mathf.Lerp(from, to, t);
            Sprite s = body.sprite;
            Vector4 rect = new Vector4(0f, 0f, 1f, 1f);
            if (s != null && s.texture != null)
            {
                Rect r = s.textureRect;
                float w = s.texture.width, h = s.texture.height;
                rect = new Vector4(r.xMin / w, r.yMin / h, r.xMax / w, r.yMax / h);
            }
            body.GetPropertyBlock(block);
            block.SetFloat(ProgressId, p);
            block.SetColor(EdgeId, Edge);
            block.SetFloat(SweepId, sweep);
            block.SetVector(RectId, rect);
            block.SetFloat(SeedId, seed);
            body.SetPropertyBlock(block);

            // loose pixels: flying off upward while it breaks, falling into place while it rebuilds
            puffAt -= Time.deltaTime;
            if (puffAt <= 0f)
            {
                puffAt = 0.03f;
                Bounds b = body.bounds;
                float y = breaking ? Mathf.Lerp(b.max.y, b.min.y, p) : Mathf.Lerp(b.min.y, b.max.y, 1f - p);
                Vector3 at = new Vector3(Random.Range(b.min.x, b.max.x), y, 0f);
                Color c = Random.value < 0.5f ? Edge : new Color(0.35f, 0.15f, 0.45f);
                if (breaking) FXParticle.Burst(at, c, 2, 0.4f, 1.6f, -3f, 0.45f);
                else FXParticle.Burst(at + Vector3.up * 0.5f, c, 1, 0.2f, 0.6f, 4f, 0.25f);
            }
            yield return null;
        }
        if (body != null)
        {
            body.GetPropertyBlock(block);
            block.SetFloat(ProgressId, to);
            body.SetPropertyBlock(block);
        }
    }

    private void OnDisable()
    {
        // pulled away mid-teleport (death clip, pooled, scene change): never leave it half-built or untouchable
        if (!busy) return;
        busy = false;
        if (body != null && originalMat != null) { body.sharedMaterial = originalMat; body.SetPropertyBlock(savedBlock); }
        if (rb != null) rb.simulated = true;
        if (melee != null) melee.enabled = true;
    }
}
