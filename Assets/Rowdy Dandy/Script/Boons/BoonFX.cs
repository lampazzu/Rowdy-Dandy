using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The boons' world effects (all built at runtime, art from BoonArt / ItemArt + small generated pixel sprites):
// hair crescents, decoys, waves, foam rings, spore clouds, vines, lightning, disco floors, the mirror ball,
// ghost cats, dancing zombies, spark rings... Damage from here is credited to Rowdy (kill feed / stats) with the
// boon's name.
public static class BoonFX
{
    public const int Order = 96;

    // ---------------------------------------------------------------- shared helpers
    public static readonly Color Pink = new Color(1f, 0.37f, 0.78f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
    public static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
    public static readonly Color Foam = new Color(0.88f, 1f, 1f);
    public static readonly Color Blood = new Color(0.92f, 0.16f, 0.24f);
    public static readonly Color Toxic = new Color(0.55f, 1f, 0.25f);
    public static readonly Color Violet = new Color(0.72f, 0.38f, 1f);
    public static readonly Color Disco = new Color(1f, 0.9f, 0.22f);
    public static readonly Color Lavender = new Color(0.8f, 0.62f, 1f);

    public static Color Rainbow(float t, float offset = 0f) => Color.HSVToRGB(Mathf.Repeat(t * 0.9f + offset, 1f), 0.65f, 1f);

    public static List<EnemyHealth> EnemiesIn(Vector2 center, float radius, EnemyHealth except = null)
    {
        var list = new List<EnemyHealth>();
        foreach (Collider2D c in Physics2D.OverlapCircleAll(center, radius, LayerMask.GetMask("Enemy")))
        {
            EnemyHealth e = c.GetComponentInParent<EnemyHealth>();
            if (e == null || e == except || e.enemydead || e.IsObject || !e.CompareTag("Enemy") || list.Contains(e)) continue;
            list.Add(e);
        }
        return list;
    }

    public static List<EnemyHealth> EnemiesInBox(Vector2 center, Vector2 size)
    {
        var list = new List<EnemyHealth>();
        foreach (Collider2D c in Physics2D.OverlapBoxAll(center, size, 0f, LayerMask.GetMask("Enemy")))
        {
            EnemyHealth e = c.GetComponentInParent<EnemyHealth>();
            if (e == null || e.enemydead || e.IsObject || !e.CompareTag("Enemy") || list.Contains(e)) continue;
            list.Add(e);
        }
        return list;
    }

    public static EnemyHealth Nearest(Vector2 from, float radius, ICollection<EnemyHealth> skip = null)
    {
        EnemyHealth best = null;
        float bestD = float.MaxValue;
        foreach (EnemyHealth e in EnemiesIn(from, radius))
        {
            if (skip != null && skip.Contains(e)) continue;
            float d = Vector2.Distance(from, Center(e));
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    public static Vector3 Center(EnemyHealth e) => EnemyFairness.BodyCenter(e);

    // Boon damage, credited to Rowdy with the boon's name (or a cat)
    public static void Hit(EnemyHealth e, float damage, string with, PetFollower cat = null, bool quiet = false)
    {
        if (e == null || e.enemydead || damage <= 0f) return;
        KillCredit credit = cat != null ? KillCredit.Cat(cat) : KillCredit.Rowdy();
        if (cat == null) credit.with = with;
        EnemyHealth.CreditNextHit(credit);
        RunStats.BoonDamage += damage;
        e.TakeDamageEnemy(Mathf.Round(damage), false, quiet);
    }

    public static void Push(EnemyHealth e, Vector2 velocity)
    {
        if (e != null && e.TryGetComponent(out Rigidbody2D rb) && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = velocity;
    }

    public static SheetFX Sheet(Texture2D sheet, int frames, Vector3 at, float fps, float scale = 1f, Color? tint = null, Transform follow = null, bool loop = false, int order = Order, Vector2? pivot = null)
        => SheetFX.Play(sheet, frames, at, fps, 64f, order, follow, loop, tint, scale, pivot);

    public static void Popup(Vector3 at, string text, Color color, float size = 0.9f, float life = 1.1f) => IconPopup.Show(at, null, text, color, size, life);

    public static SpriteRenderer MakeRenderer(string name, Sprite sprite, Vector3 at, int order = Order, Transform parent = null, Material material = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = order;
        Material m = material != null ? material : CatFX.Unlit;
        if (m != null) sr.sharedMaterial = m;
        return sr;
    }

    // ---------------------------------------------------------------- generated sprites
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    public static Sprite FromRows(string key, string[] rows, System.Func<char, Color32> palette, Vector2 pivot, float ppu = 64f)
    {
        if (sprites.TryGetValue(key, out Sprite s) && s != null) return s;
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = key };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[(h - 1 - y) * w + x] = rows[y][x] == '.' ? new Color32(0, 0, 0, 0) : palette(rows[y][x]);
        tex.SetPixels32(px);
        tex.Apply(false, true);
        s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu);
        sprites[key] = s;
        return s;
    }

    public static Sprite Pixel => FromRows("BoonPixel", new[] { "#" }, c => new Color32(255, 255, 255, 255), new Vector2(0.5f, 0.5f));

    public static Sprite Sparkle => FromRows("BoonSparkle", new[] { "..#..", "..#..", "##W##", "..#..", "..#.." },
        c => c == 'W' ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 200), new Vector2(0.5f, 0.5f));

    public static Sprite Heart => FromRows("BoonHeart", new[] { ".##.##.", "#######", "#######", ".#####.", "..###..", "...#..." },
        c => new Color32(255, 255, 255, 255), new Vector2(0.5f, 0.5f));

    public static Sprite Zzz => FromRows("BoonZ", new[] { "#####", "...#.", "..#..", ".#...", "#####" }, c => new Color32(255, 255, 255, 255), new Vector2(0.5f, 0.5f));

    // Pink hair crescent (opens to the back), 14 x 22 px
    public static Sprite Crescent
    {
        get
        {
            const string key = "BoonCrescent";
            if (sprites.TryGetValue(key, out Sprite s) && s != null) return s;
            const int w = 14, h = 22;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = key };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dy = y - (h - 1) * 0.5f;
                    float outer = Mathf.Sqrt((x - 2f) * (x - 2f) + dy * dy);
                    float inner = Mathf.Sqrt((x + 1.5f) * (x + 1.5f) + dy * dy);
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (outer <= 11f && inner > 10f)
                    {
                        bool rim = outer > 9.8f;
                        bool core = outer > 8.6f && outer <= 9.8f;
                        c = rim ? new Color32(255, 210, 76, 255) : core ? new Color32(255, 240, 250, 255) : new Color32(255, 94, 200, 255);
                    }
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f);
            sprites[key] = s;
            return s;
        }
    }

    // Mirror ball, 4 frames (facets twinkle)
    public static Sprite DiscoBall(int frame)
    {
        string key = "BoonDiscoBall" + frame;
        if (sprites.TryGetValue(key, out Sprite s) && s != null) return s;
        const int n = 16;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = key };
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                Color32 col = new Color32(0, 0, 0, 0);
                if (d <= 7.6f) col = new Color32(27, 8, 32, 255);
                if (d <= 6.6f)
                {
                    bool facetLine = (x + frame) % 3 == 0 || y % 3 == 0;
                    int twinkle = (x * 7 + y * 13 + frame * 5) % 11;
                    if (facetLine) col = new Color32(110, 100, 140, 255);
                    else if (twinkle == 0) col = new Color32(255, 255, 255, 255);
                    else if (twinkle < 3) col = new Color32(255, 230, 120, 255);
                    else col = y > 8 ? new Color32(205, 200, 230, 255) : new Color32(160, 150, 200, 255);
                }
                px[y * n + x] = col;
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        sprites[key] = s;
        return s;
    }

    // ---------------------------------------------------------------- sparkle burst (UI-free, world pixels)
    public static void Sparkles(Vector3 at, Color color, int count, float radius = 0.5f, float life = 0.6f)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 p = at + (Vector3)(Random.insideUnitCircle * radius);
            SpriteRenderer sr = MakeRenderer("Sparkle", Sparkle, p, Order + 2);
            sr.color = Color.Lerp(color, Color.white, Random.value * 0.5f);
            sr.gameObject.AddComponent<Twinkle>().Begin(life * Random.Range(0.6f, 1.2f), Random.Range(0.6f, 1.4f));
        }
    }

    // ---------------------------------------------------------------- lightning
    public static void Lightning(Vector3 a, Vector3 b, Color color, float life = 0.2f)
    {
        var go = new GameObject("Boon Lightning");
        go.AddComponent<LightningArc>().Begin(a, b, color, life);
    }

    // ---------------------------------------------------------------- statuses on enemies (StatusEffects)
    public static void Charm(EnemyHealth e, float seconds) { if (e != null && !e.enemydead) StatusEffects.Of(e).Charm(seconds); }
    public static void Root(EnemyHealth e, float seconds) { if (e != null && !e.enemydead) StatusEffects.Of(e).Root(seconds); }
    public static void Fear(EnemyHealth e, float seconds) { if (e != null && !e.enemydead) StatusEffects.Of(e).Fear(seconds); }
    public static void Stun(EnemyHealth e, float seconds) { if (e != null && !e.enemydead) StatusEffects.Of(e).Stun(seconds); }
    public static void Slow(EnemyHealth e, float seconds, float factor) { if (e != null && !e.enemydead) StatusEffects.Of(e).Slow(seconds, factor); }
    public static void Poison(EnemyHealth e, float seconds, float dps) { if (e != null && !e.enemydead) StatusEffects.Of(e).Poison(seconds, dps, null); }
}

// Sparkle that pops, twinkles and fades
public class Twinkle : MonoBehaviour
{
    private float life, age, size;
    private SpriteRenderer sr;
    private Color color;

    public void Begin(float lifetime, float scale)
    {
        life = lifetime;
        size = scale;
        sr = GetComponent<SpriteRenderer>();
        color = sr.color;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        float k = age / life;
        float s = size * (k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f * 0.6f);
        transform.localScale = Vector3.one * Mathf.Max(0.05f, s);
        transform.position += Vector3.up * 0.25f * Time.deltaTime;
        sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k) * (Mathf.Repeat(age * 14f, 1f) < 0.8f ? 1f : 0.4f));
    }
}

// Jagged electric arc between two points (two line renderers: glow + white core), re-jitters a few times
public class LightningArc : MonoBehaviour
{
    private LineRenderer glow, core;
    private Vector3 a, b;
    private float life, age, jitterTimer;
    private Color color;
    private static Material material;

    public void Begin(Vector3 from, Vector3 to, Color tint, float lifetime)
    {
        a = from; b = to; color = tint; life = lifetime;
        if (material == null && CatFX.Unlit != null) material = new Material(CatFX.Unlit) { mainTexture = Texture2D.whiteTexture };
        glow = Make(0.065f, 98);
        core = Make(0.025f, 99);
        Jitter();
    }

    private LineRenderer Make(float width, int order)
    {
        var go = new GameObject("Arc");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 8;
        lr.widthMultiplier = width;
        lr.numCapVertices = 0;
        lr.sortingLayerName = "Default";
        lr.sortingOrder = order;
        if (material != null) lr.sharedMaterial = material;
        return lr;
    }

    private void Jitter()
    {
        Vector3 d = b - a;
        Vector3 n = new Vector3(-d.y, d.x, 0f).normalized;
        for (int i = 0; i < 8; i++)
        {
            float t = i / 7f;
            float off = i == 0 || i == 7 ? 0f : Random.Range(-0.22f, 0.22f) * Mathf.Min(1f, d.magnitude * 0.4f);
            Vector3 p = a + d * t + n * off;
            glow.SetPosition(i, p);
            core.SetPosition(i, p);
        }
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        jitterTimer -= Time.unscaledDeltaTime;
        if (jitterTimer <= 0f) { jitterTimer = 0.04f; Jitter(); }
        float k = 1f - age / life;
        glow.startColor = glow.endColor = new Color(color.r, color.g, color.b, 0.55f * k);
        core.startColor = core.endColor = new Color(1f, 1f, 1f, k);
    }
}

// ================================================================================================ Pompadour
// Hair Flip: a pink crescent that flies forward and slices everything it passes
public class HairCrescent : MonoBehaviour
{
    private float dir, damage, age, trailTimer;
    private readonly HashSet<EnemyHealth> hit = new HashSet<EnemyHealth>();
    private SpriteRenderer sr;

    public static void Fire(Vector3 at, float direction, float damage, float scale = 1f)
    {
        SpriteRenderer sr = BoonFX.MakeRenderer("Hair Crescent", BoonFX.Crescent, at, BoonFX.Order + 4);
        sr.transform.localScale = new Vector3(direction * 1.6f * scale, 1.6f * scale, 1f);
        var c = sr.gameObject.AddComponent<HairCrescent>();
        c.dir = direction; c.damage = damage; c.sr = sr;
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.hairFlip : null, 0.5f, Random.Range(1.3f, 1.5f));
    }

    private void Update()
    {
        age += Time.deltaTime;
        float speed = Mathf.Lerp(13f, 6f, age / 0.55f);
        transform.position += new Vector3(dir * speed * Time.deltaTime, 0f, 0f);
        float s = 1f + Mathf.Sin(age * 30f) * 0.06f;
        transform.localScale = new Vector3(dir * 1.6f * s, 1.6f * s, 1f);
        sr.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((age - 0.4f) / 0.15f));

        trailTimer -= Time.deltaTime;
        if (trailTimer <= 0f)
        {
            trailTimer = 0.025f;
            CatFX.Afterimage(sr, new Color(1f, 0.45f, 0.8f, 0.45f), 0.16f);
            if (Random.value < 0.5f) BoonFX.Sparkles(transform.position + new Vector3(-dir * 0.1f, Random.Range(-0.3f, 0.3f), 0f), BoonFX.Gold, 1, 0.05f, 0.35f);
        }

        foreach (EnemyHealth e in BoonFX.EnemiesIn(transform.position, 0.6f))
        {
            if (!hit.Add(e)) continue;
            BoonFX.Hit(e, damage, "Hair Flip");
            BoonFX.Sparkles(BoonFX.Center(e), BoonFX.Pink, 6, 0.3f, 0.5f);
            PulseRing.Spawn(BoonFX.Center(e), new Color(1f, 0.5f, 0.85f, 0.9f), 0.6f, 0.2f);
            TimeSlowController.HitStop(0.03f, 0.1f);
        }
        if (age >= 0.55f) Destroy(gameObject);
    }
}

// Too Pretty To Hit: a shimmering copy of Rowdy, striking a pose. Charms everyone near, then shatters.
public class Decoy : MonoBehaviour
{
    private SpriteRenderer sr;
    private SpriteOutline outline;
    private float life, age, sparkleTimer;
    private bool tsunamiCharm;

    public static void Spawn(SpriteRenderer rowdy, float charmSeconds)
    {
        if (rowdy == null || rowdy.sprite == null) return;
        SpriteRenderer sr = BoonFX.MakeRenderer("Decoy", rowdy.sprite, rowdy.transform.position, rowdy.sortingOrder - 2, null, ItemArt.Lit);
        sr.sortingLayerID = rowdy.sortingLayerID;
        sr.transform.localScale = rowdy.transform.lossyScale;
        sr.flipX = rowdy.flipX;
        sr.color = new Color(1f, 0.75f, 0.92f, 0.85f);
        var d = sr.gameObject.AddComponent<Decoy>();
        d.sr = sr;
        d.life = charmSeconds + 0.4f;
        d.outline = SpriteOutline.Add(sr, BoonFX.Pink, 1, -1);

        Vector3 c = sr.bounds.center;
        BoonFX.Popup(c + Vector3.up * 0.7f, "OVER HERE, BABY", BoonFX.Pink, 0.75f, 1.2f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.charmSfx : null, 0.7f, 1.1f);
        PulseRing.Spawn(c, new Color(1f, 0.5f, 0.85f, 0.9f), 4.5f, 0.45f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(c, 4.5f)) BoonFX.Charm(e, charmSeconds);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(age * 10f);
        outline.color = Color.Lerp(BoonFX.Pink, BoonFX.Gold, pulse);
        sr.color = new Color(1f, 0.75f + 0.2f * pulse, 0.92f, 0.85f);
        sparkleTimer -= Time.deltaTime;
        if (sparkleTimer <= 0f) { sparkleTimer = 0.12f; BoonFX.Sparkles(sr.bounds.center, Color.Lerp(BoonFX.Pink, Color.white, 0.4f), 1, 0.45f, 0.5f); }
        if (age >= life) Shatter();
    }

    private void Shatter()
    {
        Vector3 c = sr.bounds.center;
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.glassBreak : null, 0.55f, Random.Range(1.1f, 1.3f));
        FXParticle.Burst(c, new Color(1f, 0.6f, 0.9f), 22, 2f, 5f, 9f, 0.6f);
        FXParticle.Burst(c, Color.white, 10, 1.5f, 4f, 9f, 0.5f);
        PulseRing.Spawn(c, new Color(1f, 0.85f, 0.95f, 1f), 2f, 0.3f);
        ScreenShake.Impulse(0.25f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(c, 2f)) BoonFX.Hit(e, 20f, "Too Pretty To Hit");
        Destroy(gameObject);
    }
}

// ================================================================================================ Riptide
// Wipeout: a wave rolling forward along the ground, carrying enemies and slamming them into walls
public class RiptideWave : MonoBehaviour
{
    private float dir, damage, age, travel, speed = 12f;
    private bool pink, toxic;
    private float size = 1f;
    private readonly Dictionary<EnemyHealth, float> carried = new Dictionary<EnemyHealth, float>();
    private SheetFX visual, visual2;
    private float foamTimer;
    private bool crashed;

    public static void Spawn(Vector3 feet, float direction, float damage, bool pink, bool toxic)
    {
        var go = new GameObject("Riptide Wave");
        go.transform.position = feet;
        var w = go.AddComponent<RiptideWave>();
        w.dir = direction; w.damage = damage; w.pink = pink; w.toxic = toxic;
        w.size = pink ? 2f : 1f;
        if (pink) w.damage *= 2f;
        w.travel = pink ? 0.75f : 0.55f;
        BoonArt art = BoonArt.Get;
        // the art is cyan: a flat-colour copy behind it turns the wave pink (Beach Bod) or toxic green (Red Tide)
        Color body = pink ? new Color(1f, 0.45f, 0.82f, 0.7f) : toxic ? new Color(0.5f, 1f, 0.3f, 0.6f) : new Color(0.85f, 1f, 1f, 0.35f);
        if (art != null)
        {
            w.visual2 = BoonFX.Sheet(art.waterSonic, 9, feet + new Vector3(-direction * 0.1f, 0.45f, 0f) * w.size, 22f, 1.4f * w.size, body, go.transform, true, BoonFX.Order - 1);
            if (w.visual2 != null)
            {
                w.visual2.transform.localScale = new Vector3(direction * 1.4f * w.size, 1.4f * w.size, 1f);
                if (CatFX.Silhouette != null) w.visual2.Renderer.sharedMaterial = CatFX.Silhouette;
            }
            w.visual = BoonFX.Sheet(art.waterSonic, 9, feet + Vector3.up * 0.45f * w.size, 22f, 1.25f * w.size, Color.white, go.transform, true);
            if (w.visual != null) w.visual.transform.localScale = new Vector3(direction * 1.25f * w.size, 1.25f * w.size, 1f);
            Play(art.tornado, 0.45f, pink ? 0.85f : 1.1f);
            Play(art.waveCrash, 0.5f, 1f);
        }
        if (pink) BoonFX.Popup(feet + Vector3.up * 2.2f, "BEACH BOD!", BoonFX.Pink, 1f, 1.2f);
        ScreenShake.Impulse(0.3f * w.size);
    }

    private static void Play(AudioClip c, float v, float p) => BoonArt.Play(c, v, p);

    private void Update()
    {
        age += Time.deltaTime;
        float step = dir * speed * Time.deltaTime;
        Vector3 front = transform.position + new Vector3(dir * 0.6f * size, 0.5f * size, 0f);

        // a wall ahead: crash
        if (!crashed && SolidGround.Blocked(front + new Vector3(step, 0.15f, 0f), new Vector2(0.2f, 0.4f * size)))
        {
            Crash(true);
            return;
        }
        transform.position += new Vector3(step, 0f, 0f);

        // pick up everyone in front, carry them
        foreach (EnemyHealth e in BoonFX.EnemiesInBox(transform.position + new Vector3(dir * 0.3f * size, 0.55f * size, 0f), new Vector2(1.2f * size, 1.2f * size)))
        {
            if (carried.ContainsKey(e)) continue;
            carried[e] = 0f;
            BoonFX.Stun(e, travel + 0.6f);
            FXParticle.Burst(BoonFX.Center(e), pink ? BoonFX.Pink : BoonFX.Foam, 8, 1f, 3f, 6f, 0.4f);
            if (pink) BoonFX.Charm(e, 2f);
        }
        foreach (var pair in carried)
        {
            EnemyHealth e = pair.Key;
            if (e == null || e.enemydead) continue;
            if (e.TryGetComponent(out Rigidbody2D rb) && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                float targetX = transform.position.x + dir * 0.55f * size;
                Vector2 p = rb.position;
                if (dir * (targetX - p.x) > 0f && !SolidGround.Blocked(new Vector2(targetX, p.y + 0.3f), new Vector2(0.15f, 0.3f))) p.x = targetX;
                rb.position = p;
                rb.linearVelocity = new Vector2(0f, Mathf.Max(rb.linearVelocity.y, 0f));
            }
        }

        foamTimer -= Time.deltaTime;
        if (foamTimer <= 0f)
        {
            foamTimer = 0.03f;
            Color c = pink ? Color.Lerp(BoonFX.Pink, Color.white, Random.value * 0.5f) : toxic ? BoonFX.Toxic : BoonFX.Foam;
            FXParticle.Burst(transform.position + new Vector3(dir * 0.4f * size, Random.Range(0f, 0.9f) * size, 0f), c, 2, 1f, 3f, 8f, 0.45f, true);
            if (pink && Random.value < 0.3f) BoonFX.Sparkles(transform.position + Vector3.up * size, BoonFX.Gold, 1, 0.6f, 0.5f);
        }

        if (age >= travel) Crash(false);
    }

    private void Crash(bool wall)
    {
        if (crashed) return;
        crashed = true;
        BoonArt art = BoonArt.Get;
        if (art != null) BoonArt.Play(art.waterBoom, wall ? 0.6f : 0.35f, wall ? 0.9f : 1.2f);
        Vector3 at = transform.position + new Vector3(dir * 0.5f * size, 0.5f * size, 0f);
        FXParticle.Burst(at, pink ? BoonFX.Pink : toxic ? BoonFX.Toxic : BoonFX.Foam, wall ? 30 : 16, 2f, 6f, 10f, 0.7f, true);
        PulseRing.Spawn(at, pink ? new Color(1f, 0.6f, 0.9f, 0.9f) : new Color(0.7f, 1f, 1f, 0.9f), 1.3f * size, 0.3f);
        if (wall) { ScreenShake.Impulse(0.45f); TimeSlowController.HitStop(0.05f, 0.08f); BoonFX.Popup(at + Vector3.up * 0.6f, "WIPEOUT!", BoonFX.Cyan, 0.9f, 1f); }

        foreach (var pair in carried)
        {
            EnemyHealth e = pair.Key;
            if (e == null || e.enemydead) continue;
            BoonFX.Hit(e, damage * (wall ? 1.6f : 1f), pink ? "Beach Bod" : "Wipeout");
            if (toxic) BoonFX.Poison(e, 4f, 8f);
            if (!wall) BoonFX.Push(e, new Vector2(dir * 4f, 3f));
        }
        if (visual != null) visual.Stop(0.15f);
        if (visual2 != null) visual2.Stop(0.15f);
        Destroy(gameObject, 0.2f);
        enabled = false;
    }
}

// ================================================================================================ Mama Rot
// Spore Step: a lingering mushroom cloud that poisons (Rave Mold: also zaps)
public class SporeCloud : MonoBehaviour
{
    private float dps, age, life = 3f, tick, puffTimer, zapTimer;
    private bool rave;
    private const float Radius = 1.4f;
    private SheetFX visual;

    public static void Spawn(Vector3 at, float dps, bool rave)
    {
        var go = new GameObject("Spore Cloud");
        go.transform.position = at;
        var c = go.AddComponent<SporeCloud>();
        c.dps = dps; c.rave = rave;
        BoonArt art = BoonArt.Get;
        if (art != null)
        {
            c.visual = BoonFX.Sheet(art.ail, 10, at + Vector3.up * 0.35f, 12f, 0.75f, new Color(0.55f, 1f, 0.35f, 0.85f), go.transform, true);
            BoonArt.Play(art.sporePop, 0.35f, Random.Range(1.3f, 1.6f));
        }
        FXParticle.Burst(at, BoonFX.Toxic, 14, 0.6f, 2.2f, -0.5f, 0.9f, true);
        FXParticle.Burst(at, BoonFX.Violet, 6, 0.4f, 1.5f, -0.5f, 0.9f, true);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { if (visual != null) visual.Stop(0.3f); Destroy(gameObject, 0.3f); enabled = false; return; }

        puffTimer -= Time.deltaTime;
        if (puffTimer <= 0f)
        {
            puffTimer = 0.1f;
            Vector3 p = transform.position + new Vector3(Random.Range(-Radius, Radius) * 0.7f, Random.Range(0f, 0.8f), 0f);
            FXParticle.Burst(p, Random.value < 0.75f ? BoonFX.Toxic : BoonFX.Violet, 1, 0.1f, 0.4f, -0.8f, 0.8f);
        }

        tick -= Time.deltaTime;
        if (tick <= 0f)
        {
            tick = 0.5f;
            foreach (EnemyHealth e in BoonFX.EnemiesIn(transform.position + Vector3.up * 0.4f, Radius)) BoonFX.Poison(e, 1.2f, dps);
        }

        if (rave)
        {
            zapTimer -= Time.deltaTime;
            if (zapTimer <= 0f)
            {
                zapTimer = 0.6f;
                bool any = false;
                foreach (EnemyHealth e in BoonFX.EnemiesIn(transform.position + Vector3.up * 0.4f, Radius))
                {
                    BoonFX.Lightning(transform.position + Vector3.up * 0.5f, BoonFX.Center(e), BoonFX.Rainbow(Time.time), 0.15f);
                    BoonFX.Hit(e, 6f, "Rave Mold", null, true);
                    any = true;
                }
                PulseRing.Spawn(transform.position + Vector3.up * 0.4f, new Color(BoonFX.Disco.r, BoonFX.Disco.g, BoonFX.Disco.b, 0.6f), Radius, 0.25f);
                if (any) BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.zap : null, 0.2f, Random.Range(1.4f, 1.7f));
            }
        }
    }
}

// Overgrowth: vines burst out of the ground and root everyone around
public class VineBurst : MonoBehaviour
{
    private readonly List<(Transform t, float delay, float height)> vines = new List<(Transform, float, float)>();
    private float age;

    public static void Spawn(Vector3 at, float rootSeconds)
    {
        BoonArt art = BoonArt.Get;
        Vector3 ground = at;
        if (SolidGround.Ray(at + Vector3.up * 0.3f, Vector2.down, 4f, out RaycastHit2D hit)) ground = hit.point;
        var go = new GameObject("Vines");
        go.transform.position = ground;
        var v = go.AddComponent<VineBurst>();
        Sprite vine = art != null && art.earthPillar != null ? ItemArt.Frames(art.earthPillar, 1, 1, new Vector2(0.5f, 0f), 64f)[0] : null;
        int count = 7;
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) * 0.5f) * 0.32f + Random.Range(-0.08f, 0.08f);
            SpriteRenderer sr = BoonFX.MakeRenderer("Earth Pillar", vine != null ? vine : BoonFX.Pixel, ground + new Vector3(x, -0.05f, 0f), 70 + i % 3, go.transform, ItemArt.Lit);
            sr.color = Color.Lerp(new Color(0.85f, 0.95f, 0.75f), Color.white, Random.value); // the pillar art, a touch of rot green
            sr.flipX = Random.value < 0.5f;
            float height = Mathf.Lerp(0.5f, 1f, 1f - Mathf.Abs(i - (count - 1) * 0.5f) / count) * Random.Range(0.8f, 1.1f);
            sr.transform.localScale = new Vector3(1f, 0f, 1f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, x * -12f);
            v.vines.Add((sr.transform, i * 0.025f, height));
        }
        if (art != null) BoonArt.Play(art.vines, 0.55f, Random.Range(0.9f, 1.1f));
        GroundShock.Spawn(ground, 3f, new Color(0.6f, 1f, 0.35f), new Color(0.35f, 0.55f, 0.2f), 0.4f);
        FXParticle.Burst(ground, new Color(0.4f, 0.65f, 0.25f), 18, 1.5f, 4f, 9f, 0.6f, true);
        ScreenShake.Impulse(0.3f);
        int rooted = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(ground + Vector3.up * 0.5f, 3f)) { BoonFX.Root(e, rootSeconds); rooted++; }
        if (rooted > 0) BoonFX.Popup(ground + Vector3.up * 1.4f, rooted > 1 ? "ROOTED X" + rooted : "ROOTED!", BoonFX.Toxic, 0.85f, 1f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        bool alive = false;
        foreach (var (t, delay, height) in vines)
        {
            if (t == null) continue;
            alive = true;
            float k = Mathf.Clamp01((age - delay) / 0.14f);
            float grow = k < 1f ? 1f - (1f - k) * (1f - k) : 1f;
            float sink = Mathf.Clamp01((age - 1.8f) / 0.5f);
            float wobble = 1f + Mathf.Sin((age - delay) * 30f) * 0.08f * (1f - Mathf.Clamp01(age / 0.5f));
            t.localScale = new Vector3(1f, height * grow * (1f - sink) * wobble, 1f);
        }
        if (!alive || age > 2.4f) Destroy(gameObject);
    }
}

// ================================================================================================ DJ Fever
// Night Fever: a strip of flashing dance floor. Enemies standing on it get stunned (once each).
public class DiscoFloor : MonoBehaviour
{
    private readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();
    private readonly HashSet<EnemyHealth> done = new HashSet<EnemyHealth>();
    private float age, life = 4f, stun, width;
    private const float TileW = 0.5f;

    public static void Spawn(Vector3 feet, float direction, float stunSeconds)
    {
        Vector3 ground = feet;
        if (SolidGround.Ray(feet + Vector3.up * 0.3f, Vector2.down, 2f, out RaycastHit2D hit)) ground = hit.point;
        const int count = 10;
        float width = count * TileW;
        var go = new GameObject("Disco Floor");
        go.transform.position = ground + new Vector3(direction * (width * 0.5f - 0.6f), 0f, 0f); // starts just behind Rowdy
        var f = go.AddComponent<DiscoFloor>();
        f.stun = stunSeconds;
        f.width = width;
        for (int i = 0; i < count; i++)
        {
            // revealed in the dash direction
            float x = direction * (-width * 0.5f + TileW * (i + 0.5f));
            Vector3 at = go.transform.position + new Vector3(x, 2f / 64f, 0f);
            // only where there's floor (no tiles hanging in the air past a ledge)
            if (!SolidGround.Ray(at + Vector3.up * 0.3f, Vector2.down, 0.6f, out RaycastHit2D floor)) continue;
            at.y = floor.point.y + 2f / 64f;
            SpriteRenderer sr = BoonFX.MakeRenderer("Tile", BoonFX.Pixel, at, 8, go.transform);
            sr.transform.localScale = new Vector3(TileW * 64f - 2f, 4f, 1f);
            sr.color = new Color(1f, 1f, 1f, 0f);
            f.tiles.Add(sr);
        }
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.discoFloor : null, 0.4f, 1.25f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life || tiles.Count == 0) { Destroy(gameObject); return; }
        float fadeIn = Mathf.Clamp01(age / 0.15f), fadeOut = Mathf.Clamp01((life - age) / 0.4f);
        int beat = (int)(age * 6f);
        for (int i = 0; i < tiles.Count; i++)
        {
            bool reveal = age > i * 0.025f;
            Color c = BoonFX.Rainbow(((i + beat) % 5) / 5f);
            float flash = (i + beat) % 2 == 0 ? 1f : 0.55f;
            tiles[i].color = new Color(c.r * flash, c.g * flash, c.b * flash, reveal ? 0.9f * fadeIn * fadeOut : 0f);
        }
        if (Time.frameCount % 6 == 0)
        {
            int i = Random.Range(0, tiles.Count);
            BoonFX.Sparkles(tiles[i].transform.position + Vector3.up * 0.1f, BoonFX.Rainbow(Time.time, i * 0.1f), 1, 0.1f, 0.4f);
        }
        foreach (EnemyHealth e in BoonFX.EnemiesInBox(transform.position + Vector3.up * 0.35f, new Vector2(width, 0.7f)))
        {
            if (!done.Add(e)) continue;
            BoonFX.Stun(e, stun);
            BoonFX.Sparkles(BoonFX.Center(e), BoonFX.Rainbow(Time.time), 5, 0.3f, 0.6f);
            BoonFX.Popup(BoonFX.Center(e) + Vector3.up * 0.6f, "GET DOWN!", BoonFX.Disco, 0.6f, 0.8f);
        }
    }
}

// Mirror Ball: drops over Rowdy, spins, fires rainbow beams at enemies for a few seconds
public class MirrorBall : MonoBehaviour
{
    private Transform rowdy;
    private SpriteRenderer ball;
    private readonly List<SpriteRenderer> spots = new List<SpriteRenderer>();
    private float damage, age, life = 6f, fireTimer;
    private static MirrorBall active;
    private static Material lineMaterial;

    public static void Drop(Transform rowdy, float damage)
    {
        if (active != null) { active.age = 0f; return; }
        var go = new GameObject("Mirror Ball");
        go.transform.position = rowdy.position + Vector3.up * 6f;
        var m = go.AddComponent<MirrorBall>();
        active = m;
        m.rowdy = rowdy; m.damage = damage;
        m.ball = BoonFX.MakeRenderer("Ball", BoonFX.DiscoBall(0), go.transform.position, 140, go.transform);
        m.ball.transform.localScale = Vector3.one * 2f;
        for (int i = 0; i < 4; i++)
        {
            SpriteRenderer s = BoonFX.MakeRenderer("Spot", BoonFX.Pixel, go.transform.position, 7, go.transform);
            s.color = new Color(1f, 1f, 1f, 0f);
            m.spots.Add(s);
        }
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.mirrorBall : null, 0.7f, 1f);
        BoonFX.Popup(rowdy.position + Vector3.up * 1.8f, "MIRROR BALL!", BoonFX.Disco, 1.1f, 1.4f);
    }

    private void OnDestroy() { if (active == this) active = null; }

    private void Update()
    {
        if (rowdy == null) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        float drop = Mathf.Clamp01(age / 0.45f);
        float leave = Mathf.Clamp01((age - life) / 0.4f);
        Vector3 target = rowdy.position + Vector3.up * Mathf.Lerp(6f, 2.5f, 1f - (1f - drop) * (1f - drop)) + Vector3.up * leave * 5f;
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 14f);
        ball.sprite = BoonFX.DiscoBall((int)(age * 12f) % 4);
        ball.transform.localPosition = new Vector3(0f, Mathf.Sin(age * 3f) * 0.05f, 0f);
        if (leave >= 1f) { Destroy(gameObject); return; }

        // sweeping spotlights: thin coloured beams down to the floor
        for (int i = 0; i < spots.Count; i++)
        {
            float a = Mathf.Sin(age * (1.4f + i * 0.3f) + i * 1.7f) * 40f;
            SpriteRenderer s = spots[i];
            s.transform.localPosition = Vector3.zero;
            s.transform.rotation = Quaternion.Euler(0f, 0f, 180f + a);
            s.transform.localScale = new Vector3(3f, 3.2f * 64f, 1f);
            s.transform.position = transform.position + s.transform.up * 1.6f;
            Color c = BoonFX.Rainbow(age * 0.5f, i * 0.25f);
            s.color = new Color(c.r, c.g, c.b, 0.12f * drop * (1f - leave));
        }

        if (drop < 1f || age > life) return;
        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f) return;
        fireTimer = 0.3f;
        List<EnemyHealth> targets = BoonFX.EnemiesIn(rowdy.position, 7f);
        if (targets.Count == 0) return;
        EnemyHealth e = targets[Random.Range(0, targets.Count)];
        Beam(transform.position, BoonFX.Center(e), BoonFX.Rainbow(Time.time * 2f));
        BoonFX.Hit(e, damage, "Mirror Ball");
        BoonFX.Sparkles(BoonFX.Center(e), BoonFX.Rainbow(Time.time), 4, 0.25f, 0.5f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.beam : null, 0.2f, Random.Range(1.5f, 1.9f));
    }

    private static void Beam(Vector3 a, Vector3 b, Color c)
    {
        if (lineMaterial == null && CatFX.Unlit != null) lineMaterial = new Material(CatFX.Unlit) { mainTexture = Texture2D.whiteTexture };
        var go = new GameObject("Disco Beam");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.widthMultiplier = 0.045f;
        lr.sortingLayerName = "Default";
        lr.sortingOrder = 139;
        if (lineMaterial != null) lr.sharedMaterial = lineMaterial;
        lr.startColor = Color.white;
        lr.endColor = c;
        go.AddComponent<LineFade>().life = 0.14f;
    }
}

public class LineFade : MonoBehaviour
{
    public float life = 0.15f;
    private float age;
    private LineRenderer lr;
    private Color a, b;
    private void Start() { lr = GetComponent<LineRenderer>(); a = lr.startColor; b = lr.endColor; }
    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        float k = 1f - age / life;
        lr.startColor = new Color(a.r, a.g, a.b, a.a * k);
        lr.endColor = new Color(b.r, b.g, b.b, b.a * k);
        lr.widthMultiplier *= 0.97f;
    }
}

// ================================================================================================ Madame Meow
// Feline Fury: a ghost cat pounces from above onto an enemy
public class GhostCat : MonoBehaviour
{
    private EnemyHealth target;
    private Vector3 start;
    private float damage, age;
    private SpriteRenderer sr;
    private const float Flight = 0.22f;

    public static void Pounce(Vector3 from, EnemyHealth target, float damage, Sprite catSprite, Color tint)
    {
        if (target == null) return;
        SpriteRenderer sr = BoonFX.MakeRenderer("Ghost Cat", catSprite != null ? catSprite : BoonFX.Heart, from, BoonFX.Order + 6, null, CatFX.Silhouette);
        sr.color = new Color(tint.r, tint.g, tint.b, 0.85f);
        sr.flipX = BoonFX.Center(target).x < from.x;
        var g = sr.gameObject.AddComponent<GhostCat>();
        g.target = target; g.damage = damage; g.start = from; g.sr = sr;
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.catPounce : null, 0.6f * GameSettings.CatVoiceVolume, Random.Range(1.05f, 1.25f));
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (target == null) { Destroy(gameObject); return; }
        Vector3 end = BoonFX.Center(target);
        float k = Mathf.Clamp01(age / Flight);
        Vector3 p = Vector3.Lerp(start, end, k * k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.6f;
        transform.position = p;
        transform.rotation = Quaternion.Euler(0f, 0f, (sr.flipX ? 1f : -1f) * Mathf.Lerp(-30f, 25f, k));
        if (Time.frameCount % 2 == 0) CatFX.Afterimage(sr, new Color(sr.color.r, sr.color.g, sr.color.b, 0.4f), 0.18f);
        if (k < 1f) return;

        BoonArt art = BoonArt.Get;
        if (art != null) BoonFX.Sheet(art.clawSlash, 6, end, 24f, 1.2f, new Color(0.9f, 0.75f, 1f));
        BoonFX.Hit(target, damage, "Feline Fury");
        BoonFX.Popup(end + Vector3.up * 0.6f, "MRRAOW!", BoonFX.Lavender, 0.7f, 0.8f);
        FXParticle.Burst(end, BoonFX.Lavender, 12, 1.5f, 4f, 4f, 0.5f);
        TimeSlowController.HitStop(0.05f, 0.08f);
        ScreenShake.Impulse(0.2f);
        Destroy(gameObject);
    }
}

// ================================================================================================ Thriller
// A killed enemy gets back up as a dancing zombie and fights for Rowdy for a while
public class DanceZombie : MonoBehaviour
{
    private SpriteRenderer sr;
    private SpriteOutline outline;
    private float age, life = 8f, hitTimer, danceTimer, rise = 0.6f;
    private Vector3 baseScale;
    private float groundY;
    private EnemyHealth target;

    public static void Raise(EnemyHealth dead)
    {
        if (dead == null) return;
        SpriteRenderer body = dead.GetComponent<SpriteRenderer>();
        if (body == null || body.sprite == null) return;
        Vector3 at = dead.transform.position;
        float ground = at.y;
        Collider2D col = dead.GetComponent<Collider2D>();
        if (col != null) ground = col.bounds.min.y;
        SpriteRenderer sr = BoonFX.MakeRenderer("Dance Zombie", body.sprite, at, body.sortingOrder, null, ItemArt.Lit);
        sr.sortingLayerID = body.sortingLayerID;
        sr.flipX = body.flipX;
        sr.transform.localScale = dead.transform.lossyScale;
        sr.color = new Color(0.55f, 1f, 0.65f, 0f);
        var z = sr.gameObject.AddComponent<DanceZombie>();
        z.sr = sr;
        z.baseScale = sr.transform.localScale;
        z.groundY = ground;
        z.outline = SpriteOutline.Add(sr, BoonFX.Violet, 1, -1);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.zombieRise : null, 0.45f, Random.Range(0.9f, 1.1f));
        FXParticle.Burst(new Vector3(at.x, ground, 0f), new Color(0.35f, 0.25f, 0.2f), 12, 1f, 3f, 8f, 0.5f, true);
        BoonFX.Popup(at + Vector3.up * 1f, "IT IS THRILLER!", BoonFX.Violet, 0.8f, 1.2f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Collapse(); return; }
        float r = Mathf.Clamp01(age / rise);
        sr.color = new Color(0.55f, 1f, 0.65f, r);
        outline.color = Color.Lerp(BoonFX.Violet, BoonFX.Rainbow(age * 0.6f), 0.5f) * new Color(1f, 1f, 1f, r);

        // dance: hop + squash + flip on the beat
        danceTimer += Time.deltaTime;
        float beat = Mathf.Repeat(danceTimer * 2.2f, 1f);
        float squash = 1f + Mathf.Sin(beat * Mathf.PI * 2f) * 0.08f;
        if (beat < Time.deltaTime * 2.2f) sr.flipX = !sr.flipX;
        transform.localScale = new Vector3(baseScale.x / squash, baseScale.y * squash * Mathf.Lerp(0.2f, 1f, r), 1f);

        if (r < 1f) return;

        if (target == null || target.enemydead) target = BoonFX.Nearest(transform.position, 8f);
        if (target != null)
        {
            Vector3 to = BoonFX.Center(target);
            float dx = to.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.5f) transform.position += new Vector3(Mathf.Sign(dx) * 2.4f * Time.deltaTime, 0f, 0f);
            hitTimer -= Time.deltaTime;
            if (Mathf.Abs(dx) < 0.9f && hitTimer <= 0f)
            {
                hitTimer = 0.5f;
                BoonFX.Hit(target, 8f, "Thriller");
                FXParticle.Burst(to, BoonFX.Violet, 6, 1f, 3f, 4f, 0.4f);
            }
        }
        if (Time.frameCount % 8 == 0) BoonFX.Sparkles(sr.bounds.center + Vector3.up * 0.3f, BoonFX.Rainbow(age), 1, 0.4f, 0.5f);
    }

    private void Collapse()
    {
        Vector3 c = sr.bounds.center;
        FXParticle.Burst(c, BoonFX.Violet, 14, 1f, 3.5f, 6f, 0.6f);
        for (int i = 0; i < 12; i++) FXParticle.Burst(c, BoonFX.Rainbow(i / 12f), 1, 2f, 4f, 7f, 0.8f, true);
        Destroy(gameObject);
    }
}

// ================================================================================================ Funky Feet / Hang Ten
public static class Rings
{
    public static void SparkRing(Vector3 at, float damage)
    {
        PulseRing.Spawn(at, new Color(1f, 0.95f, 0.4f, 1f), 1.8f, 0.3f);
        PulseRing.Spawn(at, new Color(0.7f, 0.5f, 1f, 0.8f), 1.3f, 0.25f);
        for (int i = 0; i < 10; i++) FXParticle.Burst(at, BoonFX.Rainbow(i / 10f), 1, 3f, 5f, 0f, 0.35f);
        bool any = false;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(at, 1.8f))
        {
            BoonFX.Lightning(at, BoonFX.Center(e), BoonFX.Disco, 0.15f);
            BoonFX.Hit(e, damage, "Funky Feet");
            any = true;
        }
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.zap : null, any ? 0.35f : 0.15f, Random.Range(1.6f, 1.9f));
    }

    public static void FoamRing(Vector3 feet, float damage, bool toxic)
    {
        BoonArt art = BoonArt.Get;
        Color foam = toxic ? BoonFX.Toxic : BoonFX.Foam;
        GroundShock.Spawn(feet, 2.4f, foam, toxic ? new Color(0.4f, 0.8f, 0.3f) : new Color(0.5f, 0.85f, 1f), 0.35f);
        if (art != null)
        {
            BoonFX.Sheet(art.waterSonic, 9, feet + new Vector3(-0.7f, 0.35f, 0f), 24f, 0.9f, toxic ? new Color(0.6f, 1f, 0.45f) : Color.white);
            SheetFX right = BoonFX.Sheet(art.waterSonic, 9, feet + new Vector3(0.7f, 0.35f, 0f), 24f, 0.9f, toxic ? new Color(0.6f, 1f, 0.45f) : Color.white);
            if (right != null) right.transform.localScale = new Vector3(-0.9f, 0.9f, 1f);
            BoonArt.Play(art.splash, 0.6f, Random.Range(1.2f, 1.4f));
        }
        FXParticle.Burst(feet, foam, 20, 2f, 5f, 10f, 0.6f, true);
        ScreenShake.Impulse(0.35f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(feet + Vector3.up * 0.4f, 2.4f))
        {
            BoonFX.Hit(e, damage, "Hang Ten");
            float side = Mathf.Sign(BoonFX.Center(e).x - feet.x);
            BoonFX.Push(e, new Vector2(side * 5f, 4f));
            if (toxic) BoonFX.Poison(e, 4f, 8f);
        }
    }
}
