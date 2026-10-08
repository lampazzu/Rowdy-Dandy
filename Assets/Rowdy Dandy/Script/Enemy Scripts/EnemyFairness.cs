using UnityEngine;

// Rules and signals that keep enemies readable / fair:
//  - Melee enemies don't start an attack from off screen, nor in their first moments after spawning (MeleeEnemy).
//  - Archers / bombers may shoot from anywhere, but first draw a red aim trace showing where the shot comes from.
//  - "!" alert: pops once over an enemy's head when it notices Rowdy (starts chasing / sees him), see EnemyAlert.
public static class EnemyFairness
{
    public const float SpawnGrace = 0.8f;

    private static Camera cam;

    // Is this point inside the camera view? inset = fraction of the screen kept as a border (0.05 = 5% each side)
    public static bool OnScreen(Vector3 point, float inset = 0.02f)
    {
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam == null) return true; // no camera to judge with: don't block anything
        Vector3 v = cam.WorldToViewportPoint(point);
        return v.z > 0f && v.x > inset && v.x < 1f - inset && v.y > inset && v.y < 1f - inset;
    }

    public static Vector3 BodyCenter(Component enemy)
    {
        foreach (Collider2D c in enemy.GetComponents<Collider2D>())
            if (c.enabled && !c.isTrigger) return c.bounds.center;
        return enemy.transform.position;
    }

    public static float HeadHeight(Component enemy)
    {
        foreach (Collider2D c in enemy.GetComponents<Collider2D>())
            if (c.enabled && !c.isTrigger) return c.bounds.max.y - enemy.transform.position.y;
        return 0.6f;
    }

    public static bool IsRanged(EnemyHealth health)
    {
        EnemyCatalog.Entry e = health != null ? EnemyCatalog.Identify(health) : null;
        return e != null && (e.id == "gnollarcher" || e.id == "gnollbomber");
    }

    // Melee only: may it start an attack right now?
    public static bool CanStartMeleeAttack(Component enemy, float aliveSince)
    {
        if (Time.time - aliveSince < SpawnGrace) return false;
        return OnScreen(BodyCenter(enemy), 0.01f);
    }

    // Red aim trace in front of an archer / bomber for `duration` seconds before it shoots
    public static void AimTrace(Component enemy, float duration)
    {
        if (!GameSettings.RangedAimLines) return; // Accessibility > Ranged Aim Lines (the shot still waits the same time)
        var go = new GameObject("Aim Trace");
        go.AddComponent<EnemyAimTrace>().Begin(enemy, duration);
    }
}

// Where a shooter's shots actually fly, learned from the real projectiles (ArrowSpawner attaches a ShotRecorder to
// every arrow / bomb it spawns). Points are relative to the shooter's body center, x pointing the way it faces.
// Until a kind of shooter has fired once, bombers get a simulated lob and archers a straight line.
public static class ShotPaths
{
    private static readonly System.Collections.Generic.Dictionary<string, Vector2[]> paths = new System.Collections.Generic.Dictionary<string, Vector2[]>();

    public static string KeyOf(Component shooter)
    {
        EnemyHealth h = shooter != null ? shooter.GetComponentInParent<EnemyHealth>() : null;
        EnemyCatalog.Entry e = h != null ? EnemyCatalog.Identify(h) : null;
        return e != null ? e.id : null;
    }

    public static void Store(string key, Vector2[] path)
    {
        if (string.IsNullOrEmpty(key) || path == null || path.Length < 3) return;
        paths[key] = path;
    }

    public static bool TryGet(string key, out Vector2[] path)
    {
        path = null;
        return !string.IsNullOrEmpty(key) && paths.TryGetValue(key, out path);
    }

    // Gnoll bomb before any real one was seen: the RDR_Bomb prefab's physics (mass 0.5, gravity x25, drag 50,
    // constant force 41.9 forward) plus FrechaAnim's upward force (250 -> 0 over 0.75 s)
    public static Vector2[] SimulatedBomb()
    {
        var list = new System.Collections.Generic.List<Vector2>();
        Vector2 pos = new Vector2(0.25f, 0.05f), vel = Vector2.zero;
        const float dt = 0.02f, mass = 0.5f, drag = 50f;
        float gravity = Physics2D.gravity.y * 25f;
        for (float t = 0f; t < 2.5f; t += dt)
        {
            float up = Mathf.Lerp(250f, 0f, t / 0.75f);
            vel += dt * new Vector2(41.9f / mass, gravity + up / mass);
            vel *= 1f / (1f + dt * drag);
            pos += vel * dt;
            list.Add(pos);
        }
        return list.ToArray();
    }
}

// Rides on a projectile and writes down its flight (see ShotPaths)
public class ShotRecorder : MonoBehaviour
{
    private string key;
    private Vector3 origin;
    private float dir;
    private readonly System.Collections.Generic.List<Vector2> points = new System.Collections.Generic.List<Vector2>();
    private float age;

    public static void Attach(GameObject projectile, Component shooter)
    {
        string key = ShotPaths.KeyOf(shooter);
        if (key == null) return;
        EnemyHealth h = shooter.GetComponentInParent<EnemyHealth>();
        var r = projectile.AddComponent<ShotRecorder>();
        r.key = key;
        r.origin = EnemyFairness.BodyCenter(h);
        r.dir = h.transform.lossyScale.x >= 0f ? -1f : 1f;
        r.points.Add(Local(r, projectile.transform.position));
    }

    private static Vector2 Local(ShotRecorder r, Vector3 world) => new Vector2((world.x - r.origin.x) * r.dir, world.y - r.origin.y);

    private void FixedUpdate()
    {
        age += Time.fixedDeltaTime;
        if (age > 3f || points.Count > 200) { Save(); enabled = false; return; }
        points.Add(Local(this, transform.position));
    }

    private void OnDestroy() => Save();

    private bool saved;
    private void Save()
    {
        if (saved) return;
        saved = true;
        ShotPaths.Store(key, points.ToArray());
    }
}

// Red aim trace from an archer / bomber along the path its shot will take (straight for arrows, an arc for bombs),
// cut where it meets the ground, with a target mark where it lands. Grows out from the shooter, flickers faster
// right before the shot. Optional: Accessibility > Ranged Aim Lines (off by default).
public class EnemyAimTrace : MonoBehaviour
{
    private const float MaxLength = 16f;
    private const float DotSpacing = 0.11f;
    private static Sprite dotSprite, markSprite;
    private static readonly Color Red = new Color(1f, 0.2f, 0.25f, 1f);

    private Component enemy;
    private Vector2[] path;            // local (forward = +x) points, already spaced DotSpacing apart
    private SpriteRenderer[] dots;
    private SpriteRenderer mark;
    private float duration, age;
    private int layer, order;

    public void Begin(Component target, float time)
    {
        enemy = target;
        duration = Mathf.Max(0.05f, time);
        SpriteRenderer body = target.GetComponent<SpriteRenderer>();
        layer = body != null ? body.sortingLayerID : 0;
        order = body != null ? body.sortingOrder : 0;

        string key = ShotPaths.KeyOf(target);
        if (!ShotPaths.TryGet(key, out Vector2[] raw))
            raw = key == "gnollbomber" ? ShotPaths.SimulatedBomb() : new[] { new Vector2(0.25f, 0.05f), new Vector2(0.25f + MaxLength, 0.05f) };
        path = Resample(raw);
        Build();
        LateUpdate();
    }

    // Even dot spacing along the polyline, up to MaxLength, stopping where it hits solid ground
    private Vector2[] Resample(Vector2[] raw)
    {
        float dir = enemy.transform.lossyScale.x >= 0f ? -1f : 1f;
        Vector3 origin = EnemyFairness.BodyCenter(enemy);
        // Long straight pieces split up, so the ground check below can skip just the bit at the shooter's feet
        var dense = new System.Collections.Generic.List<Vector2> { raw[0] };
        for (int i = 1; i < raw.Length; i++)
        {
            int pieces = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(raw[i - 1], raw[i]) / 0.5f));
            for (int s = 1; s <= pieces; s++) dense.Add(Vector2.Lerp(raw[i - 1], raw[i], s / (float)pieces));
        }
        raw = dense.ToArray();

        var result = new System.Collections.Generic.List<Vector2>();
        float carry = 0f, travelled = 0f;
        result.Add(raw[0]);
        for (int i = 1; i < raw.Length && travelled < MaxLength; i++)
        {
            Vector2 a = raw[i - 1], b = raw[i];
            float len = Vector2.Distance(a, b);
            if (len < 0.0001f) continue;

            // World-space segment: stop at the ground (bombs land, arrows hit walls)
            Vector2 wa = new Vector2(origin.x + a.x * dir, origin.y + a.y), wb = new Vector2(origin.x + b.x * dir, origin.y + b.y);
            RaycastHit2D hit = default;
            // one-way platforms only stop things coming down onto them
            if (travelled > 0.4f && SolidGround.Line(wa, wb, out RaycastHit2D h) && (!h.collider.usedByEffector || wb.y < wa.y)) hit = h;
            float usable = hit.collider != null ? len * hit.fraction : len;

            float d = DotSpacing - carry;
            while (d <= usable && travelled < MaxLength)
            {
                result.Add(Vector2.Lerp(a, b, d / len));
                travelled += DotSpacing;
                d += DotSpacing;
            }
            carry = usable - (d - DotSpacing);
            if (hit.collider != null)
            {
                result.Add(Vector2.Lerp(a, b, usable / len));
                landed = true;
                break;
            }
        }
        return result.ToArray();
    }

    private bool landed;

    private void Build()
    {
        dots = new SpriteRenderer[path.Length];
        for (int i = 0; i < path.Length; i++)
        {
            var go = new GameObject("Dot");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DotSprite;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order - 1;
            if (CatFX.Unlit != null) sr.sharedMaterial = CatFX.Unlit;
            dots[i] = sr;
        }
        if (landed || path.Length > 0)
        {
            var m = new GameObject("Target");
            m.transform.SetParent(transform, false);
            mark = m.AddComponent<SpriteRenderer>();
            mark.sprite = MarkSprite;
            mark.sortingLayerID = layer;
            mark.sortingOrder = order + 1;
            if (CatFX.Unlit != null) mark.sharedMaterial = CatFX.Unlit;
            mark.enabled = landed;
        }
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        if (enemy == null || age > duration) { Destroy(gameObject); return; }
        EnemyHealth h = enemy.GetComponent<EnemyHealth>();
        if (h != null && h.enemydead) { Destroy(gameObject); return; }

        float dir = enemy.transform.lossyScale.x >= 0f ? -1f : 1f;
        Vector3 origin = EnemyFairness.BodyCenter(enemy);
        transform.position = origin;
        transform.localScale = new Vector3(dir, 1f, 1f);

        float k = Mathf.Clamp01(age / duration);
        float flicker = Mathf.Repeat(age * Mathf.Lerp(6f, 22f, k), 1f) < 0.65f ? 1f : 0.45f;
        int shown = Mathf.CeilToInt(path.Length * Mathf.Clamp01(age / 0.15f)); // shoots out from the shooter
        float march = Mathf.Repeat(age * 10f, 1f); // dots crawl along the path towards the target
        for (int i = 0; i < dots.Length; i++)
        {
            bool on = i < shown;
            dots[i].enabled = on;
            if (!on) continue;
            Vector2 p = path[i];
            if (i + 1 < path.Length) p = Vector2.Lerp(path[i], path[i + 1], march * 0.5f);
            dots[i].transform.localPosition = new Vector3(Snap(p.x), Snap(p.y), 0f);
            float fadeOut = 1f - 0.5f * i / Mathf.Max(1, path.Length); // fainter far away
            dots[i].color = new Color(Red.r, Red.g, Red.b, (0.45f + 0.5f * k) * flicker * fadeOut);
        }

        if (mark != null && landed)
        {
            mark.enabled = shown >= path.Length;
            Vector2 end = path[path.Length - 1];
            mark.transform.localPosition = new Vector3(Snap(end.x), Snap(end.y + 0.03f), 0f);
            float pulse = 1f + 0.25f * Mathf.Sin(age * Mathf.Lerp(10f, 30f, k));
            mark.transform.localScale = new Vector3(pulse, pulse, 1f);
            mark.color = new Color(Red.r, Red.g, Red.b, (0.6f + 0.4f * k) * flicker);
        }
    }

    private static float Snap(float v) => Mathf.Round(v * 64f) / 64f;

    // 2x2 px dot
    private static Sprite DotSprite
    {
        get
        {
            if (dotSprite != null) return dotSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "EnemyAimDot" };
            tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
            tex.Apply(false, true);
            dotSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 64f);
            return dotSprite;
        }
    }

    // Flat target ring where the shot lands (9 x 4 px)
    private static Sprite MarkSprite
    {
        get
        {
            if (markSprite != null) return markSprite;
            Sprite raw = OverlayUI.PixelSprite(new[]
            {
                "..WWWWW..",
                "WW.....WW",
                "WW.....WW",
                "..WWWWW..",
            }, ch => new Color32(255, 255, 255, 255), "EnemyAimMark");
            markSprite = Sprite.Create(raw.texture, raw.rect, new Vector2(0.5f, 0.5f), 64f);
            return markSprite;
        }
    }
}

// "!" over an enemy's head the moment it notices Rowdy: hops up out of the head, squashes on landing, then fades. Once per aggro - it shows again only after the enemy has lost him for a while.
// Added by EnemyMovement.Start; aggro = EnemyMovement.isChasing, or MeleeEnemy seeing him (archers don't move).
public class EnemyAlert : MonoBehaviour
{
    private const float ForgetAfter = 4f;     // seconds without aggro before it can alert again
    private const float Life = 0.9f;
    private static Sprite bang;

    private EnemyMovement movement;
    private MeleeEnemy melee;
    private EnemyHealth health;
    private bool aggro, pendingShow;
    private float lostAt = -100f;

    private SpriteRenderer icon;
    private float shownAt = -10f;

    public static void Attach(GameObject enemy)
    {
        if (enemy.GetComponent<EnemyAlert>() == null) enemy.AddComponent<EnemyAlert>();
    }

    private void Start()
    {
        movement = GetComponent<EnemyMovement>();
        melee = GetComponent<MeleeEnemy>();
        health = GetComponent<EnemyHealth>();
        if (health != null && health.IsObject) enabled = false;
    }

    private void LateUpdate()
    {
        if (health != null && health.enemydead) { Hide(); return; }

        bool now = (movement != null && movement.isChasing) || (melee != null && melee.SeesPlayer);
        if (now && !aggro && Time.time - lostAt > ForgetAfter) pendingShow = true;
        if (!now && aggro) lostAt = Time.time;
        aggro = now;
        if (!now || !GameSettings.EnemyAlerts) pendingShow = false; // Accessibility > Enemy Alerts

        // Only worth showing where you can see it
        if (pendingShow && EnemyFairness.OnScreen(EnemyFairness.BodyCenter(this), 0.02f))
        {
            pendingShow = false;
            shownAt = Time.time;
            if (icon == null) Build();
        }
        Animate();
    }

    private void Build()
    {
        var go = new GameObject("Alert !");
        icon = go.AddComponent<SpriteRenderer>();
        icon.sprite = Bang;
        SpriteRenderer body = GetComponent<SpriteRenderer>();
        icon.sortingLayerID = body != null ? body.sortingLayerID : 0;
        icon.sortingOrder = (body != null ? body.sortingOrder : 0) + 40;
        if (CatFX.Unlit != null) icon.sharedMaterial = CatFX.Unlit;
    }

    private void Animate()
    {
        if (icon == null) return;
        float t = Time.time - shownAt;
        if (t > Life) { icon.enabled = false; return; }
        icon.enabled = true;

        float head = EnemyFairness.HeadHeight(this);
        // hop: up fast, settle down; squash on the landing
        float hop = t < 0.12f ? Mathf.Lerp(0f, 0.22f, t / 0.12f) : t < 0.26f ? Mathf.Lerp(0.22f, 0.1f, (t - 0.12f) / 0.14f) : 0.1f;
        float squash = t > 0.24f && t < 0.34f ? 1f - Mathf.Sin((t - 0.24f) / 0.1f * Mathf.PI) * 0.25f : 1f;
        float pop = t < 0.08f ? Mathf.Lerp(0.4f, 1.15f, t / 0.08f) : 1f;
        icon.transform.position = new Vector3(transform.position.x, transform.position.y + head + 0.08f + hop, transform.position.z);
        icon.transform.localScale = new Vector3(pop / squash, pop * squash, 1f);

        // white flash in, warm yellow body, fade out at the end
        float fade = t > Life - 0.25f ? 1f - (t - (Life - 0.25f)) / 0.25f : 1f;
        Color c = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.3f), Mathf.Clamp01((t - 0.05f) / 0.15f));
        c.a = fade;
        icon.color = c;
    }

    private void Hide() { if (icon != null) icon.enabled = false; }

    private void OnDestroy() { if (icon != null) Destroy(icon.gameObject); }

    // 5 x 12: chunky "!" with a dark outline and a highlight column
    private static Sprite Bang
    {
        get
        {
            if (bang != null) return bang;
            Sprite raw = OverlayUI.PixelSprite(new[]
            {
                ".OOO.",
                "OHWWO",
                "OHWWO",
                "OHWWO",
                "OHWWO",
                ".OWO.",
                ".OWO.",
                "..O..",
                ".OOO.",
                "OHWWO",
                "OWWWO",
                ".OOO.",
            }, ch => ch == 'H' ? new Color32(255, 255, 255, 255) : ch == 'W' ? new Color32(235, 225, 215, 255) : new Color32(0x1B, 0x08, 0x20, 0xFF), "EnemyAlertBang");
            bang = Sprite.Create(raw.texture, raw.rect, new Vector2(0.5f, 0f), 64f);
            return bang;
        }
    }
}
