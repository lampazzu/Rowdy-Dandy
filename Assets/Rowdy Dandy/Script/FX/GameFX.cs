using System.Collections.Generic;
using UnityEngine;

// Small shared effect pieces used by the mega list 4 features (checkpoints, jellies, blood, heal, cats, statuses):
//   FXSound.Play("Stun")                          - one-shot from Resources/Sounds/FX (or Sounds/), pitchable
//   SheetFX.Play(tex, frames, pos, fps, ...)      - plays a horizontal sprite sheet once (or looped) and cleans up
//   SpriteOutline.Add(renderer, color)            - flat-colour 1 px outline that follows a sprite (Rowdy outline, overheal)
//   PulseRing.Spawn(pos, color, radius, time)     - expanding pixel ring (shockwaves)
//   Blood.Spill(pos, dirX, amount)                - droplets that fall, splat and leave pools on the ground
// "Is there floor here?" for the level as it's built: solid ground is on several layers (Ground, the one-way Tilemap,
// terrain correctors...), so instead of a layer mask: any non-trigger collider that isn't a character, water,
// a pickup or a dynamic body.
public static class SolidGround
{
    public static bool IsGround(Collider2D c)
    {
        if (c == null || c.isTrigger) return false;
        if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) return false;
        if (c.CompareTag("Player") || c.CompareTag("Enemy") || c.CompareTag("Water")) return false;
        int layer = c.gameObject.layer;
        if (layer == 6 || layer == LayerMask.NameToLayer("Water") || layer == LayerMask.NameToLayer("Enemy") || layer == LayerMask.NameToLayer("IgnorePlayer")) return false;
        return true;
    }

    public static bool Ray(Vector2 from, Vector2 direction, float distance, out RaycastHit2D hit)
    {
        foreach (RaycastHit2D h in Physics2D.RaycastAll(from, direction, distance))
        {
            if (!IsGround(h.collider)) continue;
            if (h.distance <= 0.0001f && h.collider.usedByEffector) continue; // inside a one-way platform
            hit = h;
            return true;
        }
        hit = default;
        return false;
    }

    public static bool Line(Vector2 a, Vector2 b, out RaycastHit2D hit)
    {
        Vector2 d = b - a;
        return Ray(a, d.normalized, d.magnitude, out hit);
    }

    // Standing on something? (feet = bottom of the collider)
    public static bool Under(Collider2D body, float reach = 0.35f)
    {
        if (body == null) return false;
        Bounds b = body.bounds;
        return Ray(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, reach, out _);
    }

    public static bool Blocked(Vector2 center, Vector2 size)
    {
        foreach (Collider2D c in Physics2D.OverlapBoxAll(center, size, 0f))
            if (IsGround(c) && !c.usedByEffector) return true;
        return false;
    }
}

public static class FXSound
{
    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private static AudioSource[] sources;
    private static int next;

    public static void Play(string name, float volume = 1f, float pitch = 1f) => Play(Clip(name), volume, pitch);

    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (!AudioGuard.Safe(clip, ref volume, ref pitch)) return;
        if (sources == null || sources[0] == null)
        {
            var go = new GameObject("FXSound (auto)");
            Object.DontDestroyOnLoad(go);
            sources = new AudioSource[8];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = go.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
        }
        AudioSource s = sources[next];
        next = (next + 1) % sources.Length;
        s.pitch = pitch;
        s.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }

    public static AudioClip Clip(string name)
    {
        if (clips.TryGetValue(name, out AudioClip c)) return c;
        c = Resources.Load<AudioClip>("Sounds/FX/" + name);
        if (c == null) c = Resources.Load<AudioClip>("Sounds/" + name);
        clips[name] = c;
        return c;
    }
}

// Plays the frames of a horizontal sheet (cut with ItemArt.Frames). Follows a transform if given.
public class SheetFX : MonoBehaviour
{
    private Sprite[] frames;
    private SpriteRenderer sr;
    private Transform follow;
    private Vector3 followOffset;
    private float fps, age;
    private bool loop, unscaled;
    private Color color = Color.white;
    private float fadeOutAt = -1f;

    public SpriteRenderer Renderer => sr;

    public static SheetFX Play(Texture2D sheet, int frameCount, Vector3 position, float fps, float pixelsPerUnit = 64f, int order = 80,
                               Transform follow = null, bool loop = false, Color? tint = null, float scale = 1f, Vector2? pivot = null, bool unscaled = false)
    {
        if (sheet == null || frameCount <= 0) return null;
        Sprite[] frames = ItemArt.Frames(sheet, frameCount, 1, pivot ?? new Vector2(0.5f, 0.5f), pixelsPerUnit);
        var go = new GameObject("FX " + sheet.name);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * Mathf.Min(1f, scale); // art never shown above its own pixel size
        var fx = go.AddComponent<SheetFX>();
        fx.frames = frames;
        fx.fps = fps;
        fx.loop = loop;
        fx.unscaled = unscaled;
        fx.follow = follow;
        if (follow != null) fx.followOffset = position - follow.position;
        fx.sr = go.AddComponent<SpriteRenderer>();
        fx.sr.sprite = frames[0];
        fx.sr.sortingLayerName = "Default";
        fx.sr.sortingOrder = order;
        if (CatFX.Unlit != null) fx.sr.sharedMaterial = CatFX.Unlit;
        fx.color = tint ?? Color.white;
        fx.sr.color = fx.color;
        return fx;
    }

    public void SetSorting(int layerId, int order) { sr.sortingLayerID = layerId; sr.sortingOrder = order; }

    // Looping effects: fade away over 'seconds' and go
    public void Stop(float seconds = 0.15f)
    {
        if (this == null) return;
        loop = false;
        fadeOutAt = age;
        fadeTime = Mathf.Max(0.01f, seconds);
    }
    private float fadeTime = 0.15f;

    private void LateUpdate()
    {
        age += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        if (follow != null) transform.position = follow.position + followOffset;
        else if (followOffset != Vector3.zero) { Destroy(gameObject); return; } // what it followed is gone

        int f = (int)(age * fps);
        if (f >= frames.Length)
        {
            if (!loop && fadeOutAt < 0f) { Destroy(gameObject); return; }
            f %= frames.Length;
        }
        sr.sprite = frames[f];

        if (fadeOutAt >= 0f)
        {
            float k = 1f - (age - fadeOutAt) / fadeTime;
            if (k <= 0f) { Destroy(gameObject); return; }
            sr.color = new Color(color.r, color.g, color.b, color.a * k);
        }
    }
}

// Flat-colour outline: four 1 px offset copies of the sprite drawn just behind it. Alpha 0 = hidden.
public class SpriteOutline : MonoBehaviour
{
    private static readonly Vector2[] Offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

    private SpriteRenderer source;
    private SpriteRenderer[] copies;
    public Color color = new Color(0.3f, 0.65f, 1f, 1f);
    public int pixels = 1;
    public int orderOffset = -1;

    public static SpriteOutline Add(SpriteRenderer source, Color color, int pixels = 1, int orderOffset = -1)
    {
        var holder = new GameObject("Outline");
        holder.transform.SetParent(source.transform, false);
        var o = holder.AddComponent<SpriteOutline>();
        o.source = source;
        o.color = color;
        o.pixels = pixels;
        o.orderOffset = orderOffset;
        o.Build();
        return o;
    }

    private void Build()
    {
        copies = new SpriteRenderer[Offsets.Length];
        for (int i = 0; i < Offsets.Length; i++)
        {
            var go = new GameObject("Edge");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (CatFX.Silhouette != null) sr.sharedMaterial = CatFX.Silhouette;
            copies[i] = sr;
        }
    }

    private void LateUpdate()
    {
        if (source == null) { Destroy(gameObject); return; }
        bool show = color.a > 0.01f && source.enabled && source.gameObject.activeInHierarchy && source.sprite != null;
        float px = pixels / (source.sprite != null ? source.sprite.pixelsPerUnit : 64f);
        for (int i = 0; i < copies.Length; i++)
        {
            SpriteRenderer c = copies[i];
            c.enabled = show;
            if (!show) continue;
            c.sprite = source.sprite;
            c.flipX = source.flipX;
            c.flipY = source.flipY;
            c.sortingLayerID = source.sortingLayerID;
            c.sortingOrder = source.sortingOrder + orderOffset;
            // offsets in world pixels, whatever the parent's (possibly flipped) scale
            Vector3 s = source.transform.lossyScale;
            c.transform.localPosition = new Vector3(Offsets[i].x * px / Mathf.Max(0.0001f, Mathf.Abs(s.x)) * Mathf.Sign(s.x),
                                                    Offsets[i].y * px / Mathf.Max(0.0001f, Mathf.Abs(s.y)), 0f);
            c.color = color;
        }
    }
}

// Side-on shockwave along the ground (stomps): a thin bright line racing out left and right from the impact,
// with dust crests at its two fronts kicking pixels up. Reads right on thin 2D platforms (no ellipses).
public class GroundShock : MonoBehaviour
{
    private static Sprite pixel;
    private SpriteRenderer line, crestL, crestR;
    private float age, life, reach;
    private Color color, dust;
    private float debrisTimer;

    public static GroundShock Spawn(Vector3 groundPoint, float reach, Color color, Color dust, float life = 0.45f)
    {
        var go = new GameObject("Ground Shock");
        go.transform.position = new Vector3(Mathf.Round(groundPoint.x * 64f) / 64f, Mathf.Round(groundPoint.y * 64f) / 64f, 0f);
        var g = go.AddComponent<GroundShock>();
        g.reach = reach; g.life = life; g.color = color; g.dust = dust;
        g.line = g.Make(9);
        g.crestL = g.Make(10);
        g.crestR = g.Make(10);
        g.Update();
        return g;
    }

    private SpriteRenderer Make(int order)
    {
        var go = new GameObject("Piece");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Pixel;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 90 + order;
        if (CatFX.Unlit != null) sr.sharedMaterial = CatFX.Unlit;
        return sr;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / life);
        if (t >= 1f) { Destroy(gameObject); return; }
        float ease = 1f - (1f - t) * (1f - t) * (1f - t);
        float front = Mathf.Round(reach * ease * 64f) / 64f;
        float fade = 1f - t;

        // the line: 2 px tall, thinning to 1 px, brightest near the fronts
        line.transform.localScale = new Vector3(Mathf.Max(1f, front * 2f * 64f), t < 0.5f ? 2f : 1f, 1f);
        line.transform.localPosition = new Vector3(0f, 1f / 64f, 0f);
        line.color = new Color(color.r, color.g, color.b, color.a * fade * 0.8f);

        // crests: little 3x5 px dust walls at both fronts
        foreach (var (crest, side) in new[] { (crestL, -1f), (crestR, 1f) })
        {
            crest.transform.localScale = new Vector3(3f, Mathf.Round(Mathf.Lerp(6f, 2f, t)), 1f);
            crest.transform.localPosition = new Vector3(side * front, Mathf.Lerp(6f, 2f, t) * 0.5f / 64f, 0f);
            crest.color = new Color(1f, 1f, 1f, fade);
        }

        debrisTimer -= Time.unscaledDeltaTime;
        if (debrisTimer <= 0f && t < 0.8f)
        {
            debrisTimer = 0.035f;
            Vector3 p = transform.position;
            FXParticle.Burst(p + new Vector3(-front, 0.02f, 0f), dust, 2, 0.8f, 2.2f, 10f, 0.4f, true);
            FXParticle.Burst(p + new Vector3(front, 0.02f, 0f), dust, 2, 0.8f, 2.2f, 10f, 0.4f, true);
        }
    }

    // 1 px at the game's 64 px per unit
    private static Sprite Pixel
    {
        get
        {
            if (pixel != null) return pixel;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "ShockPixel" };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 64f);
            return pixel;
        }
    }
}

// Expanding thin ring (pixel circle), fades as it grows
public class PulseRing : MonoBehaviour
{
    private static Sprite ring;
    private SpriteRenderer sr;
    private float age, life, radius;
    private Color color;
    private bool flat, shaped;

    public static PulseRing Spawn(Vector3 at, Color color, float radius, float life = 0.4f, int order = 90, bool flat = false)
    {
        // drawn per real pixel (PixelShape): a big ring keeps a thin crisp line instead of a 64 px ring blown up
        SpriteRenderer shape = PixelShape.Make("Pulse Ring", PixelShape.Kind.Ring, at, order, null, radius > 2.5f ? 3f : 2f);
        GameObject go = shape != null ? shape.gameObject : new GameObject("Pulse Ring");
        go.transform.position = at;
        var p = go.AddComponent<PulseRing>();
        p.shaped = shape != null;
        if (shape != null) p.sr = shape;
        else
        {
            p.sr = go.AddComponent<SpriteRenderer>();
            p.sr.sprite = Ring;
            p.sr.sortingLayerName = "Default";
            p.sr.sortingOrder = order;
            if (CatFX.Unlit != null) p.sr.sharedMaterial = CatFX.Unlit;
        }
        p.color = color;
        p.life = life;
        p.radius = radius;
        p.flat = flat;
        p.Update();
        return p;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / life);
        if (t >= 1f) { Destroy(gameObject); return; }
        float ease = 1f - (1f - t) * (1f - t);
        float size = Mathf.Lerp(0.15f, radius * 2f, ease) / 1f; // sprite is 1 unit wide
        if (shaped) PixelShape.Size(sr, size, flat ? size * 0.3f : size);
        else transform.localScale = new Vector3(size, flat ? size * 0.3f : size, 1f);
        sr.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
    }

    private static Sprite Ring
    {
        get
        {
            if (ring != null) return ring;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PulseRing" };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    byte a = (byte)(d > c - 3f && d <= c ? 255 : d > c - 5f && d <= c - 3f ? 110 : 0);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            ring = AIArt.Use("FX_PulseRing", Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n));
            return ring;
        }
    }
}

// Blood: a spatter frame-anim at the hit, droplets flying out with gravity, and pools where they land.
public static class Blood
{
    public static readonly Color Dark = new Color32(0x6E, 0x05, 0x12, 0xFF);
    public static readonly Color Bright = new Color32(0xC8, 0x12, 0x24, 0xFF);
    private const int MaxPools = 70;
    private static readonly Queue<BloodPool> pools = new Queue<BloodPool>();
    private static Sprite drop;

    public static void Spill(Vector3 at, float dirX, int amount, bool spatter = true)
    {
        if (!GameSettings.BloodOn) return; // Accessibility > Blood
        ItemArt art = ItemArt.Get;
        if (spatter && art != null && art.blood != null && art.blood.Length > 0)
        {
            Texture2D sheet = art.blood[Random.Range(0, art.blood.Length)];
            SheetFX fx = SheetFX.Play(sheet, 4, at, 18f, 64f, 75);
            if (fx != null) fx.transform.localScale = new Vector3(dirX < 0f ? -1f : 1f, 1f, 1f);
        }
        for (int i = 0; i < amount; i++)
        {
            var go = new GameObject("Blood Drop");
            go.transform.position = at + (Vector3)Random.insideUnitCircle * 0.08f;
            var d = go.AddComponent<BloodDrop>();
            float side = Mathf.Abs(dirX) > 0.01f ? Mathf.Sign(dirX) : (Random.value < 0.5f ? -1f : 1f);
            d.Begin(new Vector2(side * Random.Range(0.4f, 2.6f) + Random.Range(-0.6f, 0.6f), Random.Range(0.6f, 3.2f)));
        }
    }

    public static void AddPool(BloodPool pool)
    {
        pools.Enqueue(pool);
        while (pools.Count > MaxPools)
        {
            BloodPool old = pools.Dequeue();
            if (old != null) old.FadeSoon();
        }
    }

    public static Sprite DropSprite
    {
        get
        {
            if (drop != null) return drop;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "BloodDrop" };
            tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
            tex.Apply(false, true);
            drop = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 64f);
            return drop;
        }
    }
}

public class BloodDrop : MonoBehaviour
{
    private Vector2 velocity;
    private float age;
    private SpriteRenderer sr;

    public void Begin(Vector2 v)
    {
        velocity = v;
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = Blood.DropSprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 74;
        sr.color = Random.value < 0.5f ? Blood.Bright : Blood.Dark;
        if (ItemArt.Lit != null) sr.sharedMaterial = ItemArt.Lit;
        if (Random.value < 0.4f) transform.localScale = Vector3.one * 1.5f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age > 2.5f) { Destroy(gameObject); return; }
        velocity.y -= 14f * dt;
        Vector2 from = transform.position;
        Vector2 step = velocity * dt;
        if (velocity.y < 0f && SolidGround.Ray(from, step.normalized, step.magnitude + 0.01f, out RaycastHit2D hit) && hit.normal.y > 0.5f)
        {
            BloodPool.Spawn(hit.point, hit.collider, sr.color, hit.normal);
            Destroy(gameObject);
            return;
        }
        transform.position = from + step;
    }
}

// A flat puddle on the ground, tilted to the floor's slope: spreads out, stays a while, fades.
// Neighbouring drops merge into the same puddle.
public class BloodPool : MonoBehaviour
{
    private static readonly List<BloodPool> live = new List<BloodPool>();
    private static Sprite[] puddles;

    private SpriteRenderer sr;
    private float age, width = 0.1f, targetWidth, life = 14f;
    private Color color;
    private Vector2 normal = Vector2.up;

    public static void Spawn(Vector2 at, Collider2D ground, Color color, Vector2 normal = default)
    {
        if (normal.sqrMagnitude < 0.01f) normal = Vector2.up;
        foreach (BloodPool p in live)
        {
            if (p == null) continue;
            // Same puddle: close along the floor, barely off it, and the same slope
            Vector2 offset = at - (Vector2)p.transform.position;
            Vector2 along = new Vector2(normal.y, -normal.x);
            if (Mathf.Abs(Vector2.Dot(offset, normal)) < 0.05f && Mathf.Abs(Vector2.Dot(offset, along)) < 0.12f && Vector2.Dot(p.normal, normal) > 0.98f)
            {
                p.Grow();
                return;
            }
        }
        var go = new GameObject("Blood Pool");
        Vector2 lift = normal * (0.5f / 64f);
        go.transform.position = new Vector3(Mathf.Round(at.x * 64f) / 64f + lift.x, Mathf.Round(at.y * 64f) / 64f + lift.y, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-normal.x, normal.y) * Mathf.Rad2Deg); // lies along the slope
        if (ground != null) go.transform.SetParent(ground.transform, true); // rides moving platforms
        var pool = go.AddComponent<BloodPool>();
        pool.normal = normal;
        pool.color = Color.Lerp(color, Blood.Dark, 0.4f);
        pool.sr = go.AddComponent<SpriteRenderer>();
        pool.sr.sprite = Puddle(Random.Range(0, 3));
        pool.sr.sortingLayerName = "Default";
        pool.sr.sortingOrder = 6;
        if (ItemArt.Lit != null) pool.sr.sharedMaterial = ItemArt.Lit;
        pool.targetWidth = Random.Range(0.6f, 1f);
        live.Add(pool);
        Blood.AddPool(pool);
    }

    private void Grow()
    {
        targetWidth = Mathf.Min(targetWidth + 0.35f, 2.6f);
        age = Mathf.Min(age, 2f);
    }

    public void FadeSoon() => age = Mathf.Max(age, life - 1f);

    private void OnDestroy() => live.Remove(this);

    private void Update()
    {
        age += Time.deltaTime;
        if (age > life) { Destroy(gameObject); return; }
        width = Mathf.MoveTowards(width, targetWidth, Time.deltaTime * 3f);
        Vector3 parent = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(width / Mathf.Max(0.0001f, Mathf.Abs(parent.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(parent.y)), 1f);
        float fade = age > life - 3f ? (life - age) / 3f : 1f;
        sr.color = new Color(color.r, color.g, color.b, 0.92f * fade);
    }

    // 16 x 3 px puddles (scaled sideways by width)
    private static Sprite Puddle(int i)
    {
        if (puddles == null)
        {
            string[][] shapes =
            {
                new[] { "....OOOOOOOO....", "..OOOHHOOOOOOO..", "OOOOOOOOOOOOOOOO" },
                new[] { "......OOOOO.....", ".OOOOOOOHOOOOO..", "OOOOOOOOOOOOOOO." },
                new[] { "..OOOO...OOO....", ".OOOOOOOOOOOOOO.", "OOOOOOOOOOOOOOOO" },
            };
            puddles = new Sprite[shapes.Length];
            for (int s = 0; s < shapes.Length; s++)
            {
                Sprite raw = OverlayUI.PixelSprite(shapes[s], ch => ch == 'H' ? new Color32(255, 140, 150, 255) : new Color32(255, 255, 255, 255), "BloodPuddle" + s);
                puddles[s] = Sprite.Create(raw.texture, raw.rect, new Vector2(0.5f, 0.15f), 16f * 4f / 1f);
            }
        }
        return puddles[i];
    }
}
