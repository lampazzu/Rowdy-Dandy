using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The boons' world effects (all built at runtime, art from BoonArt / ItemArt + small generated pixel sprites):
// hair crescents, decoys, foam rings, spore clouds, lightning, ghost cats... (the newer patrons' effects are in
// BoonFXMore.cs). Damage from here is credited to Rowdy (kill feed / stats) with the
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
    public static readonly Color Disco = new Color(1f, 0.9f, 0.22f);  // electric yellow (lightning)
    public static readonly Color Deep = new Color(0.1f, 0.25f, 0.55f);  // the abyss: dark water
    public static readonly Color Sunny = new Color(1f, 0.85f, 0.35f);
    public static readonly Color Ember = new Color(1f, 0.5f, 0.15f);
    public static readonly Color Steel = new Color(0.82f, 0.88f, 1f);
    public static readonly Color Sauce = new Color(0.85f, 0.15f, 0.08f);
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
        int h = rows.Length, w = 0;
        foreach (string r in rows) w = Mathf.Max(w, r.Length); // ragged drawings are padded
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = key };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[(h - 1 - y) * w + x] = x >= rows[y].Length || rows[y][x] == '.' ? new Color32(0, 0, 0, 0) : palette(rows[y][x]);
        tex.SetPixels32(px);
        tex.Apply(false, true);
        s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu);
        // your redraw, if there is one (AI Placeholders). Boon icons are named per boon by BoonIcons instead.
        if (key != "BoonPixel" && !key.StartsWith("BoonIcon_")) s = AIArt.Use(key, s);
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
            // drawn at the size it flies at (it used to be 14 x 22 scaled 1.6x: fat, uneven pixels)
            const string key = "BoonCrescentHD";
            if (sprites.TryGetValue(key, out Sprite s) && s != null) return s;
            const int w = 22, h = 35;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = key };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dy = y - (h - 1) * 0.5f;
                    float outer = Mathf.Sqrt((x - 3.2f) * (x - 3.2f) + dy * dy);
                    float inner = Mathf.Sqrt((x + 2.4f) * (x + 2.4f) + dy * dy);
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (outer <= 17.6f && inner > 16f)
                    {
                        bool rim = outer > 15.9f;
                        bool core = outer > 14f && outer <= 15.9f;
                        c = rim ? new Color32(255, 210, 76, 255) : core ? new Color32(255, 240, 250, 255) : new Color32(255, 94, 200, 255);
                    }
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = AIArt.Use(key, Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f));
            sprites[key] = s;
            return s;
        }
    }

    // Split Edge: a silver crescent with a hot ember rim, drawn at its in-game size (16 x 34 px)
    public static Sprite SteelCrescent
    {
        get
        {
            const string key = "BoonSteelCrescent";
            if (sprites.TryGetValue(key, out Sprite s) && s != null) return s;
            const int w = 16, h = 34;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = key };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dy = y - (h - 1) * 0.5f;
                    float outer = Mathf.Sqrt((x - 1f) * (x - 1f) * 2.2f + dy * dy);
                    float inner = Mathf.Sqrt((x + 3.5f) * (x + 3.5f) * 2.2f + dy * dy);
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (outer <= 17f && inner > 16.2f)
                    {
                        bool rim = outer > 15.6f;
                        bool core = outer > 13.8f && outer <= 15.6f;
                        c = rim ? new Color32(255, 140, 50, 255) : core ? new Color32(255, 255, 255, 255) : new Color32(178, 196, 230, 255);
                    }
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = AIArt.Use(key, Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f));
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
        s = AIArt.Use(key, Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f));
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

// ================================================================================================ Narcissism
// Hair Flip: a pink crescent that flies forward and slices everything it passes
public class HairCrescent : MonoBehaviour
{
    private float dir, damage, age, trailTimer;
    private readonly HashSet<EnemyHealth> hit = new HashSet<EnemyHealth>();
    private SpriteRenderer sr;
    private bool steel; // Split Edge (the Blacksmith): a silver wave at the art's own pixel size

    public static void Fire(Vector3 at, float direction, float damage, float scale = 1f)
    {
        SpriteRenderer sr = BoonFX.MakeRenderer("Hair Crescent", BoonFX.Crescent, at, BoonFX.Order + 4);
        sr.transform.localScale = new Vector3(direction * Mathf.Min(1f, scale), Mathf.Min(1f, scale), 1f); // never above its own pixel size
        var c = sr.gameObject.AddComponent<HairCrescent>();
        c.dir = direction; c.damage = damage; c.sr = sr;
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.hairFlip : null, 0.5f, Random.Range(1.3f, 1.5f));
    }

    public static void FireSteel(Vector3 at, float direction, float damage)
    {
        SpriteRenderer sr = BoonFX.MakeRenderer("Steel Wave", BoonFX.SteelCrescent, at, BoonFX.Order + 4);
        sr.transform.localScale = new Vector3(direction, 1f, 1f);
        var c = sr.gameObject.AddComponent<HairCrescent>();
        c.dir = direction; c.damage = damage; c.sr = sr; c.steel = true;
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.hairFlip : null, 0.35f, Random.Range(1.7f, 1.9f));
    }

    private void Update()
    {
        age += Time.deltaTime;
        float speed = Mathf.Lerp(13f, 6f, age / 0.55f);
        transform.position += new Vector3(dir * speed * Time.deltaTime, 0f, 0f);
        float shimmer = steel ? 1f : 0.88f + 0.12f * Mathf.Sin(age * 30f); // shimmers instead of wobbling its scale
        sr.color = new Color(1f, 1f, 1f, shimmer * (1f - Mathf.Clamp01((age - 0.4f) / 0.15f)));

        trailTimer -= Time.deltaTime;
        if (trailTimer <= 0f)
        {
            trailTimer = 0.025f;
            CatFX.Afterimage(sr, steel ? new Color(0.75f, 0.85f, 1f, 0.45f) : new Color(1f, 0.45f, 0.8f, 0.45f), 0.16f);
            if (Random.value < 0.5f) BoonFX.Sparkles(transform.position + new Vector3(-dir * 0.1f, Random.Range(-0.3f, 0.3f), 0f), steel ? BoonFX.Ember : BoonFX.Gold, 1, 0.05f, 0.35f);
        }

        foreach (EnemyHealth e in BoonFX.EnemiesIn(transform.position, 0.6f))
        {
            if (!hit.Add(e)) continue;
            BoonFX.Hit(e, damage, steel ? "Split Edge" : "Hair Flip");
            BoonFX.Sparkles(BoonFX.Center(e), steel ? BoonFX.Steel : BoonFX.Pink, 6, 0.3f, 0.5f);
            PulseRing.Spawn(BoonFX.Center(e), steel ? new Color(0.8f, 0.9f, 1f, 0.9f) : new Color(1f, 0.5f, 0.85f, 0.9f), 0.6f, 0.2f);
            if (steel) BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.clang : null, 0.25f, Random.Range(1.4f, 1.7f));
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

// ================================================================================================ Sea Abyss
// Undertow: drags a hit enemy toward Rowdy for a moment. The enemies' GetHit clips keyframe moveSpeed (their own
// knockback), and EnemyMovement writes the velocity from it every frame, so a one-off velocity kick was always undone.
// This runs after everything else for a few physics steps and owns the horizontal speed while it does.
[DefaultExecutionOrder(1000)]
public class UndertowPull : MonoBehaviour
{
    private Rigidbody2D rb;
    private Transform toward;
    private float speed, until, stopDistance;

    public static void Begin(EnemyHealth e, Transform toward, float distance, float seconds)
    {
        if (e == null || e.enemydead || toward == null || !e.TryGetComponent(out Rigidbody2D body)) return;
        UndertowPull p = e.GetComponent<UndertowPull>();
        if (p == null) p = e.gameObject.AddComponent<UndertowPull>();
        p.rb = body;
        p.toward = toward;
        p.speed = distance / Mathf.Max(0.05f, seconds);
        p.until = Time.time + seconds;
        p.stopDistance = 0.85f; // never pulled into Rowdy
        p.enabled = true;
    }

    private void FixedUpdate()
    {
        if (rb == null || toward == null || Time.time > until || (TryGetComponent(out EnemyHealth h) && h.enemydead)) { enabled = false; return; }
        float dx = toward.position.x - rb.position.x;
        if (Mathf.Abs(dx) <= stopDistance) { Stop(); return; }
        float dir = Mathf.Sign(dx);
        float step = speed * Time.fixedDeltaTime;
        if (SolidGround.Blocked(rb.position + new Vector2(dir * (0.35f + step), 0.3f), new Vector2(0.15f, 0.3f))) { Stop(); return; }
        if (rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
        else rb.MovePosition(rb.position + new Vector2(dir * step, 0f));
    }

    private void Stop()
    {
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
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

    private readonly List<(Transform t, float delay, float size)> shrooms = new List<(Transform, float, float)>();
    private float lingerTimer;

    // The poison puff (TBZG_VFX_Poison, 10 x 214x172) plays once, then smaller puffs keep the cloud alive,
    // and a few little mushrooms pop out of the ground for as long as it lasts
    private static Texture2D Puff => ItemArt.Get != null ? ItemArt.Get.vfxPoison : null;

    public static void Spawn(Vector3 at, float dps, bool rave)
    {
        Vector3 ground = at;
        if (SolidGround.Ray(at + Vector3.up * 0.3f, Vector2.down, 1.5f, out RaycastHit2D hit)) ground = hit.point;
        var go = new GameObject("Spore Cloud");
        go.transform.position = ground;
        var c = go.AddComponent<SporeCloud>();
        c.dps = dps; c.rave = rave;
        c.visual = BoonFX.Sheet(Puff, 10, ground + Vector3.up * 0.05f, 16f, 0.8f, new Color(0.85f, 1f, 0.8f, 0.95f), null, false, BoonFX.Order, new Vector2(0.5f, 0.12f));

        // the user's mushrooms (FloraArt, RDR_Flowers_1) at their true size: natural / toxic / violet, glowing
        int count = Random.Range(2, 4);
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(-Radius, Radius) * 0.7f;
            if (!SolidGround.Ray(ground + new Vector3(x, 0.4f, 0f), Vector2.down, 0.9f, out RaycastHit2D floor)) continue; // no shrooms on thin air
            Sprite mine = FloraArt.RandomMushroom(out Color glow);
            SpriteRenderer sr = BoonFX.MakeRenderer("Shroom", mine != null ? mine : Mushroom(Random.value < 0.6f), new Vector3(ground.x + x, floor.point.y, 0f), 9 + i % 2, null, ItemArt.Lit);
            sr.flipX = Random.value < 0.5f;
            sr.transform.localScale = new Vector3(1f, 0f, 1f);
            if (mine != null) FloraArt.Pop(sr, glow, i == 0 ? 0.7f : 0f, 1.1f); // one light per cloud is plenty
            c.shrooms.Add((sr.transform, i * 0.05f + Random.Range(0f, 0.08f), mine != null ? 1f : Random.Range(1.4f, 2.1f)));
        }

        BoonArt art = BoonArt.Get;
        if (art != null) BoonArt.Play(art.sporePop, 0.35f, Random.Range(1.3f, 1.6f));
        FXParticle.Burst(ground + Vector3.up * 0.2f, BoonFX.Toxic, 10, 0.6f, 2f, -0.5f, 0.9f, true);
        FXParticle.Burst(ground + Vector3.up * 0.2f, BoonFX.Violet, 5, 0.4f, 1.5f, -0.5f, 0.9f, true);
    }

    // Little pixel mushroom: violet or rot-green cap with spots, pale stem (Mama Rot's colours)
    private static Sprite Mushroom(bool violet) => BoonFX.FromRows(violet ? "SporeShroomV" : "SporeShroomG", new[]
    {
        "..CCC..",
        ".CWCCC.",
        "CCCCWCC",
        "CDDDDDC",
        "..SSS..",
        "..SSs..",
        "..SSs..",
    }, ch =>
    {
        switch (ch)
        {
            case 'C': return violet ? new Color32(184, 97, 255, 255) : new Color32(140, 255, 64, 255);
            case 'D': return violet ? new Color32(110, 50, 170, 255) : new Color32(70, 150, 40, 255);
            case 'W': return new Color32(255, 250, 235, 255);
            case 'S': return new Color32(238, 230, 205, 255);
            case 's': return new Color32(190, 180, 150, 255);
            default: return new Color32(0, 0, 0, 0);
        }
    }, new Vector2(0.5f, 0f));

    private void Update()
    {
        age += Time.deltaTime;
        UpdateShrooms();
        if (age >= life + 0.35f) { Destroy(gameObject); return; }
        if (age >= life) return; // shrooms sinking back in

        // the cloud keeps breathing: smaller puffs drifting inside it
        lingerTimer -= Time.deltaTime;
        if (age > 0.45f && lingerTimer <= 0f && age < life - 0.4f)
        {
            lingerTimer = 0.55f;
            Vector3 p = transform.position + new Vector3(Random.Range(-Radius, Radius) * 0.6f, 0.05f, 0f);
            SheetFX small = BoonFX.Sheet(Puff, 10, p, 14f, Random.Range(0.4f, 0.55f), new Color(0.8f, 1f, 0.75f, 0.6f), null, false, BoonFX.Order - 1, new Vector2(0.5f, 0.12f));
            if (small != null) small.transform.localScale = new Vector3((Random.value < 0.5f ? -1f : 1f) * small.transform.localScale.x, small.transform.localScale.y, 1f);
        }

        puffTimer -= Time.deltaTime;
        if (puffTimer <= 0f)
        {
            puffTimer = 0.12f;
            Vector3 p = transform.position + new Vector3(Random.Range(-Radius, Radius) * 0.7f, Random.Range(0.1f, 0.8f), 0f);
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

    // pop up with a little overshoot, wobble while the cloud lives, sink back when it ends
    private void UpdateShrooms()
    {
        foreach (var (t, delay, size) in shrooms)
        {
            if (t == null) continue;
            float k = Mathf.Clamp01((age - delay) / 0.16f);
            float grow = k < 1f ? Mathf.Sin(k * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(k * Mathf.PI)) : 1f;
            float sink = Mathf.Clamp01((age - life) / 0.3f);
            float breathe = 1f + 0.06f * Mathf.Sin(age * 7f + delay * 20f);
            t.localScale = new Vector3(size * (2f - breathe), size * grow * breathe * (1f - sink), 1f);
        }
    }

    private void OnDestroy()
    {
        foreach (var (t, _, _) in shrooms) if (t != null) Destroy(t.gameObject);
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

// ================================================================================================ Stinky Bois Guild
// Feline Fury: a ghost cat pounces from above onto an enemy
public class GhostCat : MonoBehaviour
{
    private EnemyHealth target;
    private Vector3 start;
    private float damage, age;
    private SpriteRenderer sr;
    private string with = "Feline Fury";
    private const float Flight = 0.22f;

    // ghost = a lavender silhouette (Feline Fury); otherwise the cat's own art with a lavender outline (Alley Ambush)
    public static void Pounce(Vector3 from, EnemyHealth target, float damage, Sprite catSprite, Color tint, string with = "Feline Fury", bool ghost = true)
    {
        if (target == null) return;
        SpriteRenderer sr = BoonFX.MakeRenderer("Ghost Cat", catSprite != null ? catSprite : BoonFX.Heart, from, BoonFX.Order + 6, null, ghost ? CatFX.Silhouette : null);
        sr.color = ghost ? new Color(tint.r, tint.g, tint.b, 0.85f) : Color.white;
        if (!ghost) SpriteOutline.Add(sr, tint, 1, -1);
        sr.flipX = BoonFX.Center(target).x < from.x;
        var g = sr.gameObject.AddComponent<GhostCat>();
        g.target = target; g.damage = damage; g.start = from; g.sr = sr; g.with = with;
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
        if (art != null) BoonFX.Sheet(art.clawSlash, 6, end, 24f, 1f, new Color(0.9f, 0.75f, 1f));
        BoonFX.Hit(target, damage, with);
        FXParticle.Burst(end, BoonFX.Lavender, 12, 1.5f, 4f, 4f, 0.5f);
        TimeSlowController.HitStop(0.05f, 0.08f);
        ScreenShake.Impulse(0.2f);
        Destroy(gameObject);
    }
}

// ================================================================================================ Depth Charge (hangten)
public static class Rings
{
    public static void FoamRing(Vector3 feet, float damage, bool toxic)
    {
        BoonArt art = BoonArt.Get;
        Color foam = toxic ? BoonFX.Toxic : new Color(0.45f, 0.7f, 1f); // black water from the deep
        GroundShock.Spawn(feet, 2.4f, foam, toxic ? new Color(0.4f, 0.8f, 0.3f) : BoonFX.Deep, 0.35f);
        if (art != null)
        {
            BoonFX.Sheet(art.waterSonic, 9, feet + new Vector3(-0.7f, 0.35f, 0f), 24f, 1f, toxic ? new Color(0.6f, 1f, 0.45f) : new Color(0.4f, 0.6f, 1f));
            SheetFX right = BoonFX.Sheet(art.waterSonic, 9, feet + new Vector3(0.7f, 0.35f, 0f), 24f, 1f, toxic ? new Color(0.6f, 1f, 0.45f) : new Color(0.4f, 0.6f, 1f));
            if (right != null) right.transform.localScale = new Vector3(-1f, 1f, 1f);
            BoonArt.Play(art.waterBoom, 0.6f, Random.Range(0.85f, 1f));   // the Iara water burst
            BoonArt.Play(art.waveCrash, 0.35f, Random.Range(1.1f, 1.25f));
        }
        FXParticle.Burst(feet, foam, 20, 2f, 5f, 10f, 0.6f, true);
        ScreenShake.Impulse(0.35f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(feet + Vector3.up * 0.4f, 2.4f))
        {
            BoonFX.Hit(e, damage, "Depth Charge");
            float side = Mathf.Sign(BoonFX.Center(e).x - feet.x);
            BoonFX.Push(e, new Vector2(side * 5f, 4f));
            if (toxic) BoonFX.Poison(e, 4f, 8f);
        }
    }
}
