using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// World effects for the boons added in the 8-patron update (Sea Abyss rework, new Narcissism / Lycanthropy / Mama Rot /
// Guild boons, the Crazy Chef, the Blacksmith and the Sun God). Same rules as BoonFX: built at runtime, art from BoonArt /
// ItemArt or small drawn pixel sprites (always at the game's 64 px per unit), damage credited to Rowdy with the boon's name.

// ================================================================================================ drawn sprites
public static class MoreSprites
{
    private static Sprite Draw(string key, string[] rows, Vector2 pivot)
    {
        return BoonFX.FromRows("More_" + key, rows, ch =>
        {
            switch (ch)
            {
                case 'k': return new Color32(27, 8, 32, 255);
                case 'w': return new Color32(255, 255, 255, 255);
                case 'p': return new Color32(255, 110, 200, 255);
                case 'P': return new Color32(200, 50, 140, 255);
                case 'g': return new Color32(255, 210, 76, 255);
                case 'G': return new Color32(130, 240, 90, 255);
                case 'D': return new Color32(60, 140, 50, 255);
                case 'v': return new Color32(180, 100, 255, 255);
                case 'V': return new Color32(110, 50, 170, 255);
                case 'r': return new Color32(230, 40, 60, 255);
                case 'o': return new Color32(255, 140, 50, 255);
                case 'y': return new Color32(255, 240, 120, 255);
                case 'n': return new Color32(150, 95, 55, 255);
                case 'N': return new Color32(95, 58, 35, 255);
                case 'b': return new Color32(235, 190, 120, 255);   // bread
                case 'B': return new Color32(190, 130, 70, 255);
                case 'l': return new Color32(110, 210, 80, 255);    // lettuce
                case 'c': return new Color32(255, 205, 60, 255);    // cheese
                case 'm': return new Color32(150, 60, 40, 255);     // meatball
                case 't': return new Color32(220, 50, 40, 255);     // tomato
                case 's': return new Color32(170, 180, 200, 255);   // steel
                case 'S': return new Color32(90, 98, 120, 255);
                case 'i': return new Color32(60, 66, 84, 255);      // iron
                case 'a': return new Color32(80, 230, 210, 255);    // aqua glow
                case 'A': return new Color32(20, 60, 130, 255);     // abyss blue
                case 'u': return new Color32(70, 40, 120, 255);     // tentacle
                case 'U': return new Color32(130, 90, 190, 255);
                case 'e': return new Color32(250, 245, 230, 255);   // egg shell
                case 'h': return new Color32(160, 140, 120, 255);   // hairball
                case 'H': return new Color32(110, 95, 80, 255);
            }
            return new Color32(0, 0, 0, 0);
        }, pivot);
    }

    public static Sprite BigHeart => Draw("heart", new[] {
        ".kkk.kkk.",
        "kpppkpwpk",
        "kpppppwpk",
        "kpppppppk",
        ".kpppppk.",
        "..kpppk..",
        "...kpk...",
        "....k...." }, new Vector2(0.5f, 0.5f));

    public static Sprite Yolk => Draw("yolk", new[] {
        ".eee...",
        "eeyyee.",
        "eyywye.",
        "eeyyeee",
        ".eeee.." }, new Vector2(0.5f, 0.5f));

    public static Sprite Seed => Draw("seed", new[] {
        ".kk.",
        "kGvk",
        "kvVk",
        "kVVk",
        ".kk." }, new Vector2(0.5f, 0.5f));

    public static Sprite Pod => Draw("pod", new[] {
        "..kk...",
        ".kGGk..",
        "kGvGGk.",
        "kGGGvGk",
        "kDGGGDk",
        ".kDDDk.",
        "..kkk.." }, new Vector2(0.5f, 0.5f));

    public static Sprite Egg => Draw("egg", new[] {
        ".kk.",
        "keek",
        "kewk",
        "keek",
        "keek",
        ".kk." }, new Vector2(0.5f, 0.5f));

    public static Sprite Hairball => Draw("hairball", new[] {
        ".kkkk.",
        "khHhhk",
        "kHhhHk",
        "khhHhk",
        "kHhhhk",
        ".kkkk." }, new Vector2(0.5f, 0.5f));

    public static Sprite Anvil => Draw("anvil", new[] {
        "kkkkkkkkkkkkkkkkkkk.",
        "kssssssssssssssssssk",
        "kiiiiiiiiiiiiiiiiiik",
        ".kkkkiiiiiiiiiiiik..",
        ".....kiiiiiiiik.....",
        "......kiiiiiik......",
        "......kiiiiiik......",
        ".....kiiiiiiiik.....",
        "....kiiiiiiiiiik....",
        "...kSSSSSSSSSSSSk...",
        "...kkkkkkkkkkkkkk..." }, new Vector2(0.5f, 0f));

    public static Sprite Crown => Draw("crown", new[] {
        "k..k..k..",
        "kgkkgkkgk",
        "kgggggggk",
        "kgrgggrgk",
        "kkkkkkkkk" }, new Vector2(0.5f, 0f));

    public static Sprite Reticle => Draw("reticle", new[] {
        "...krk...",
        "..kr.rk..",
        ".k.....k.",
        "kr..r..rk",
        "r..rrr..r",
        "kr..r..rk",
        ".k.....k.",
        "..kr.rk..",
        "...krk..." }, new Vector2(0.5f, 0.5f));

    public static Sprite SunMote => Draw("sunmote", new[] {
        "...y...",
        ".y.g.y.",
        "..ggg..",
        "ygggggy",
        "..ggg..",
        ".y.g.y.",
        "...y..." }, new Vector2(0.5f, 0.5f));

    public static Sprite Lure => Draw("lure", new[] {
        ".kkk.",
        "kaawk",
        "kaaak",
        "kaaak",
        ".kkk." }, new Vector2(0.5f, 0.5f));

    public static Sprite Flower => Draw("flower", new[] {
        "..kkk.kkk..",
        ".kpppkpppk.",
        "kpppPkPpppk",
        "kpPkkykkPpk",
        ".kkkyyykkk.",
        "kpppkykpppk",
        "kpppPkPpppk",
        ".kpppkpppk.",
        "..kkkDkkk..",
        "....kDk....",
        "...kGDk.kk.",
        "..kGGDkkGk.",
        "...kkDkGk..",
        "....kDkk...",
        "....kDk....",
        "....kkk...." }, new Vector2(0.5f, 0f));

    public static Sprite Root(int variant) => variant % 2 == 0
        ? Draw("root0", new[] {
            "....k....",
            "...knk...",
            "...knk.k.",
            "..knNkkn.",
            "..knNnNk.",
            ".kNnNnk..",
            ".kNnNk...",
            "kNnNnNk..",
            "kNNNNNNk." }, new Vector2(0.5f, 0f))
        : Draw("root1", new[] {
            ".k.....",
            "knk..k.",
            "knNkknk",
            ".kNnNk.",
            "..kNnk.",
            ".kNnNk.",
            "kNNnNNk" }, new Vector2(0.5f, 0f));

    // Leviathan: a tentacle with suckers, curled at the tip (14 x 44)
    public static Sprite Tentacle => Draw("tentacle", new[] {
        "....kkkk......",
        "...kuuuuk.....",
        "..kuk..kuk....",
        "..kuk...kuk...",
        "...k....kuk...",
        "........kuk...",
        ".......kuuk...",
        ".......kuUk...",
        "......kuuUk...",
        "......kuUUk...",
        ".....kuuUUk...",
        ".....kuUwUk...",
        ".....kuuUUk...",
        "....kuuUUUk...",
        "....kuuUwUk...",
        "....kuuUUUUk..",
        "....kuuUUUUk..",
        "...kuuuUwUUk..",
        "...kuuuUUUUk..",
        "...kuuuUUUUk..",
        "...kuuuUUwUUk.",
        "...kuuuUUUUUk.",
        "..kuuuuUUUUUk.",
        "..kuuuuUUwUUk.",
        "..kuuuuUUUUUk.",
        "..kuuuuUUUUUUk",
        "..kuuuuUUwUUUk",
        ".kuuuuuUUUUUUk",
        ".kuuuuuUUUUUUk",
        ".kuuuuuUUwUUUk",
        ".kuuuuuUUUUUUk",
        ".kuuuuuuUUUUUk",
        "kuuuuuuUUwUUUk",
        "kuuuuuuUUUUUUk",
        "kuuuuuuUUUUUUk",
        "kuuuuuuuUUUUUk",
        "kuuuuuuuUUUUUk",
        "kuuuuuuuuUUUUk",
        "kuuuuuuuuUUUUk",
        "kuuuuuuuuuUUUk",
        "kuuuuuuuuuuUUk",
        "kAuuuuuuuuuuUk",
        "kAAuuuuuuuuuuk",
        "kkkkkkkkkkkkkk" }, new Vector2(0.5f, 0f));

    // Sandwich Time: a fat side-view sandwich (28 x 22)
    public static Sprite Sandwich => Draw("sandwich", new[] {
        "......kkkkkkkkkkkkkkk.......",
        "....kkbbbbbbbbbbbbbbbkk.....",
        "...kbbbwbbbbbbbbwbbbbbbk....",
        "..kbbbbbbbbbwbbbbbbbbbbbk...",
        "..kBbbbbbbbbbbbbbbbbbbbBk...",
        ".kkBBBBBBBBBBBBBBBBBBBBBkk..",
        "kllllklllllkllllllklllllllk.",
        ".kccccccccccccccccccccccck..",
        "..kcckccccccccckcccccccck...",
        ".kttttktttttttttttkttttttk..",
        "ktttttttttttttttttttttttttk.",
        ".kmmmmmmkkmmmmmmkkmmmmmmk...",
        "kmmnmmmmmkmmnmmmmkmmmnmmmk..",
        "kmmmmmnmmkmmmmmnmkmmmmmmmk..",
        ".kmmmmmmk.kmmmmmk.kmmmmmk...",
        "kllllllllllllllllllllllllk..",
        ".kbbbbbbbbbbbbbbbbbbbbbbbk..",
        ".kbbbbbbbbbbbbbbbbbbbbbbbk..",
        ".kBBBBBBBBBBBBBBBBBBBBBBBk..",
        "..kkkkkkkkkkkkkkkkkkkkkkk...",
        "............................",
        "............................" }, new Vector2(0.5f, 0f));

    // A pink carpet segment (32 x 5): gold trim on top, pink body with a pattern, dark lip
    public static Sprite Carpet => Draw("carpet", new[] {
        "gggggggggggggggggggggggggggggggg",
        "ppPppppppPppppppppPppppppppPpppp",
        "pppppPppppppppPpppppppPppppppppP",
        "PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP",
        "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" }, new Vector2(0.5f, 0f));

    // Soft column of light, white, fading at the sides (for Solar Flare / Spotlight)
    public static Sprite Shaft
    {
        get
        {
            const int w = 9, h = 32;
            float[] side = { 0.1f, 0.35f, 0.7f, 0.92f, 1f, 0.92f, 0.7f, 0.35f, 0.1f };
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var row = new char[w];
                for (int x = 0; x < w; x++) row[x] = (char)('0' + Mathf.RoundToInt(9f * side[x]));
                rows[y] = new string(row);
            }
            return BoonFX.FromRows("More_Shaft", rows, ch => new Color32(255, 255, 255, (byte)Mathf.RoundToInt((ch - '0') / 9f * 255f)), new Vector2(0.5f, 0f));
        }
    }

    // A spotlight cone hanging from above: narrow at the lamp, wide and soft at the floor
    public static Sprite Cone
    {
        get
        {
            const int h = 48, w = 31;
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                float down = y / (float)(h - 1);
                float half = 1f + down * (w / 2f - 1f);
                var row = new char[w];
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(x - (w - 1) / 2f) / half;
                    float edge = d >= 1f ? 0f : 1f - d * d;
                    float a = edge * Mathf.Lerp(0.9f, 0.45f, down);
                    row[x] = (char)('0' + Mathf.Clamp(Mathf.RoundToInt(9f * a), 0, 9));
                }
                rows[y] = new string(row);
            }
            return BoonFX.FromRows("More_Cone", rows, ch => new Color32(255, 255, 255, (byte)Mathf.RoundToInt((ch - '0') / 9f * 255f)), new Vector2(0.5f, 1f));
        }
    }

    // Flat ellipse of light lying on the floor (Sunspot), 48 x 8
    public static Sprite Pool
    {
        get
        {
            const int w = 48, h = 8;
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var row = new char[w];
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - (w - 1) / 2f) / (w / 2f), dy = (y - (h - 1) / 2f) / (h / 2f);
                    float d = dx * dx + dy * dy;
                    float a = d >= 1f ? 0f : (d > 0.75f ? 1f : 0.55f);
                    row[x] = (char)('0' + Mathf.RoundToInt(9f * a));
                }
                rows[y] = new string(row);
            }
            return BoonFX.FromRows("More_Pool", rows, ch => new Color32(255, 255, 255, (byte)Mathf.RoundToInt((ch - '0') / 9f * 255f)), new Vector2(0.5f, 0.5f));
        }
    }
}

// ================================================================================================ shared pieces
// A thrown thing on an arc: hits the first enemy it touches or the ground, then calls back
public class Lob : MonoBehaviour
{
    public System.Action<Vector3, EnemyHealth> onImpact;
    public Vector2 velocity;
    public float gravity = 14f, spin, radius = 0.3f, life = 3f, trailTimer;
    public Color trail = Color.clear;
    public EnemyHealth homingTarget;
    private float age;

    public static Lob Throw(Sprite sprite, Vector3 from, Vector2 velocity, float spin, System.Action<Vector3, EnemyHealth> onImpact, int order = BoonFX.Order + 3)
    {
        SpriteRenderer sr = BoonFX.MakeRenderer("Lob", sprite, from, order, null, ItemArt.Lit);
        var l = sr.gameObject.AddComponent<Lob>();
        l.velocity = velocity; l.spin = spin; l.onImpact = onImpact;
        return l;
    }

    // velocity that lands on 'to' after 'time' seconds
    public static Vector2 Aim(Vector3 from, Vector3 to, float time, float gravity = 14f)
    {
        Vector2 d = to - from;
        return new Vector2(d.x / time, (d.y + 0.5f * gravity * time * time) / time);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        Vector3 p = transform.position;
        velocity.y -= gravity * dt;
        Vector3 next = p + (Vector3)(velocity * dt);
        transform.position = next;
        transform.rotation = Quaternion.Euler(0f, 0f, transform.eulerAngles.z + spin * dt);

        if (trail.a > 0f)
        {
            trailTimer -= dt;
            if (trailTimer <= 0f) { trailTimer = 0.04f; FXParticle.Burst(p, trail, 1, 0.1f, 0.4f, 0f, 0.3f); }
        }

        foreach (EnemyHealth e in BoonFX.EnemiesIn(next, radius))
        {
            Impact(next, e);
            return;
        }
        if (velocity.y < 0f && SolidGround.Ray(p, (next - p).normalized, (next - p).magnitude + 0.05f, out RaycastHit2D hit))
        {
            Impact(hit.point, null);
            return;
        }
        if (age > life) Impact(next, null);
    }

    private void Impact(Vector3 at, EnemyHealth e)
    {
        onImpact?.Invoke(at, e);
        Destroy(gameObject);
    }
}

// A piece of a sprite that flies off, spins and fades (Earth Erupt's pillars shattering, tentacles bursting)
public class Shard : MonoBehaviour
{
    private Vector2 velocity;
    private float spin, age, life;
    private SpriteRenderer sr;
    private Color color;
    private static readonly Dictionary<string, Sprite> pieces = new Dictionary<string, Sprite>();

    // Breaks a sprite renderer into cols x rows chunks that keep its scale / rotation and fly away from 'from'
    public static void Break(SpriteRenderer source, int cols, int rows, Vector3 from, float force = 3.5f)
    {
        if (source == null || source.sprite == null) return;
        Sprite s = source.sprite;
        Rect r = s.rect;
        float ppu = s.pixelsPerUnit;
        int cw = Mathf.Max(1, Mathf.CeilToInt(r.width / cols)), ch = Mathf.Max(1, Mathf.CeilToInt(r.height / rows));
        Transform t = source.transform;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                float x0 = r.x + x * cw, y0 = r.y + y * ch;
                float w = Mathf.Min(cw, r.xMax - x0), h = Mathf.Min(ch, r.yMax - y0);
                if (w <= 0f || h <= 0f) continue;
                string key = s.texture.GetInstanceID() + "_" + x0 + "_" + y0 + "_" + w + "_" + h;
                if (!pieces.TryGetValue(key, out Sprite piece) || piece == null)
                {
                    piece = Sprite.Create(s.texture, new Rect(x0, y0, w, h), new Vector2(0.5f, 0.5f), ppu);
                    pieces[key] = piece;
                }
                // centre of this chunk in the source's local space (sprite pivot is in pixels from the rect corner)
                Vector2 local = new Vector2(x0 - r.x + w * 0.5f - s.pivot.x, y0 - r.y + h * 0.5f - s.pivot.y) / ppu;
                if (source.flipX) local.x = -local.x;
                Vector3 world = t.TransformPoint(local);
                var go = new GameObject("Shard");
                go.transform.position = world;
                go.transform.rotation = t.rotation;
                go.transform.localScale = t.lossyScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = piece;
                sr.flipX = source.flipX;
                sr.sortingLayerID = source.sortingLayerID;
                sr.sortingOrder = source.sortingOrder;
                sr.sharedMaterial = source.sharedMaterial;
                sr.color = source.color;
                var sh = go.AddComponent<Shard>();
                sh.sr = sr;
                sh.color = source.color;
                Vector2 away = ((Vector2)(world - from)).normalized;
                if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle.normalized;
                sh.velocity = away * Random.Range(force * 0.5f, force) + Vector2.up * Random.Range(1.5f, 4f);
                sh.spin = Random.Range(-540f, 540f);
                sh.life = Random.Range(0.45f, 0.75f);
            }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= life) { Destroy(gameObject); return; }
        velocity.y -= 16f * dt;
        transform.position += (Vector3)(velocity * dt);
        transform.rotation = Quaternion.Euler(0f, 0f, transform.eulerAngles.z + spin * dt);
        float k = age / life;
        sr.color = new Color(color.r, color.g, color.b, color.a * (k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f));
    }
}

// Little motes that rush inward to a point (Crushing Depths, Rip Current)
public class InwardMote : MonoBehaviour
{
    private Vector3 target;
    private float age, life, swirl;
    private SpriteRenderer sr;
    private Color color;

    public static void Ring(Vector3 center, float radius, int count, Color color, float life, float swirl = 0f)
    {
        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            Vector3 p = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.6f, 0f) * radius * Random.Range(0.85f, 1.1f);
            SpriteRenderer sr = BoonFX.MakeRenderer("Mote", BoonFX.Pixel, p, BoonFX.Order + 2);
            sr.transform.localScale = Vector3.one * Random.Range(2f, 3f);
            var m = sr.gameObject.AddComponent<InwardMote>();
            m.target = center; m.life = life * Random.Range(0.85f, 1.15f); m.sr = sr; m.color = color; m.swirl = swirl;
            sr.color = color;
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / life);
        Vector3 to = target - transform.position;
        Vector3 side = new Vector3(-to.y, to.x, 0f) * swirl;
        transform.position += (to * (1f + 6f * k) + side) * Time.deltaTime * 3f;
        sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
        if (k >= 1f) Destroy(gameObject);
    }
}

// Damage over time on an enemy: bleeding (Open Wounds, Pounce) and burning (Sunburn, Forged In Sunlight)
public class BoonDot : MonoBehaviour
{
    private EnemyHealth health;
    private float bleedUntil, bleedDps, burnUntil, burnDps, tick, fxTimer;

    private static BoonDot Of(EnemyHealth e)
    {
        BoonDot d = e.GetComponent<BoonDot>();
        if (d == null) { d = e.gameObject.AddComponent<BoonDot>(); d.health = e; d.tick = 0.5f; }
        return d;
    }

    public static void Bleed(EnemyHealth e, float dps, float seconds)
    {
        if (e == null || e.enemydead || e.IsObject) return;
        BoonDot d = Of(e);
        if (Time.time >= d.bleedUntil)
        {
            Blood.Spill(BoonFX.Center(e), Random.value < 0.5f ? -1f : 1f, 3);
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.squish : null, 0.3f, Random.Range(0.9f, 1.1f));
        }
        d.bleedUntil = Mathf.Max(d.bleedUntil, Time.time + seconds);
        d.bleedDps = Mathf.Max(Time.time < d.bleedUntil - seconds ? d.bleedDps : 0f, dps);
    }

    public static void Burn(EnemyHealth e, float dps, float seconds)
    {
        if (e == null || e.enemydead || e.IsObject) return;
        BoonDot d = Of(e);
        if (Time.time >= d.burnUntil)
        {
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sizzle : null, 0.3f, Random.Range(1.1f, 1.4f));
            FXParticle.Burst(BoonFX.Center(e), BoonFX.Ember, 16, 1.5f, 4f, -3f, 0.6f);
            FXParticle.Burst(BoonFX.Center(e), BoonFX.Sunny, 8, 1f, 3f, -2f, 0.4f);
            PulseRing.Spawn(BoonFX.Center(e), new Color(1f, 0.55f, 0.15f, 0.9f), 0.9f, 0.3f);
        }
        d.burnUntil = Mathf.Max(d.burnUntil, Time.time + seconds);
        d.burnDps = Mathf.Max(d.burnDps, dps);
    }

    private void Update()
    {
        if (health == null || health.enemydead) { Destroy(this); return; }
        float now = Time.time;
        bool bleeding = now < bleedUntil, burning = now < burnUntil;
        UpdateFlames(burning);
        if (!bleeding && !burning) { burnDps = bleedDps = 0f; return; }

        fxTimer -= Time.deltaTime;
        if (fxTimer <= 0f)
        {
            fxTimer = 0.07f;
            Vector3 c = BoonFX.Center(health);
            if (burning)
            {
                FXParticle.Burst(c + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.3f, 0.1f), 0f), Random.value < 0.5f ? BoonFX.Ember : BoonFX.Sunny, 1, 0.2f, 0.6f, -4f, 0.4f);
            }
            if (bleeding && Random.value < 0.5f)
                FXParticle.Burst(c + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0f), BoonFX.Blood, 1, 0.1f, 0.4f, 6f, 0.5f);
        }

        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 0.5f;
        if (burning) BoonFX.Hit(health, burnDps * 0.5f, "Sunburn", null, true);
        if (bleeding && health != null && !health.enemydead) BoonFX.Hit(health, bleedDps * 0.5f, "Bleeding", null, true);
    }

    // Burning (Sunburn / Forged In Sunlight): real flames licking up the enemy (TBZG_VFX_Charge, the fire sheet,
    // looping at its feet) and a flickering orange glow on its sprite, so a burning enemy reads at a glance.
    private SheetFX flames;
    private SpriteRenderer bodySr;
    private Color bodyBase;

    private void UpdateFlames(bool burning)
    {
        if (burning && flames == null)
        {
            BoonArt art = BoonArt.Get;
            if (art != null && art.charge != null)
            {
                bodySr = health.GetComponent<SpriteRenderer>();
                Bounds b = bodySr != null ? bodySr.bounds : new Bounds(BoonFX.Center(health), Vector3.one * 0.6f);
                float scale = Mathf.Clamp(b.size.x / 1.6f, 0.35f, 0.7f);
                flames = SheetFX.Play(art.charge, 10, new Vector3(b.center.x, b.min.y - 0.1f, 0f), 20f, 64f,
                    bodySr != null ? bodySr.sortingOrder + 3 : 80, health.transform, true, new Color(1f, 0.9f, 0.6f, 1f), scale, new Vector2(0.5f, 0f));
                if (bodySr != null) bodyBase = bodySr.color;
            }
        }
        else if (!burning && flames != null)
        {
            flames.Stop(0.25f);
            flames = null;
            if (bodySr != null) bodySr.color = bodyBase;
        }
        if (burning && bodySr != null && !StatusEffects.IsStunned(health.gameObject))
            bodySr.color = Color.Lerp(bodyBase, new Color(1f, 0.55f, 0.25f, bodyBase.a), 0.25f + 0.2f * Mathf.Sin(Time.time * 22f));
    }

    private void OnDestroy()
    {
        if (flames != null) flames.Stop(0.2f);
        if (bodySr != null) bodySr.color = bodyBase;
    }
}

// Hair Spray: sticky gloss on an enemy (glints, and Rowdy's hits do more)
public class Glossed : MonoBehaviour
{
    private float until, glint;
    private static float sprayedAt = -10f;
    private EnemyHealth health;

    public static bool On(EnemyHealth e) => e != null && e.TryGetComponent(out Glossed g) && Time.time < g.until;

    public static void Apply(EnemyHealth e, float seconds)
    {
        if (e == null) return;
        Glossed g = e.GetComponent<Glossed>();
        if (g == null) { g = e.gameObject.AddComponent<Glossed>(); g.health = e; }
        if (Time.time >= g.until && Time.time >= sprayedAt + 0.15f)
        {
            sprayedAt = Time.time; // pssst
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sizzle : null, 0.18f, Random.Range(2.2f, 2.5f));
        }
        g.until = Time.time + seconds;
    }

    private void Update()
    {
        if (health == null || health.enemydead || Time.time >= until) return;
        glint -= Time.deltaTime;
        if (glint > 0f) return;
        glint = 0.25f;
        BoonFX.Sparkles(BoonFX.Center(health) + (Vector3)(Random.insideUnitCircle * 0.35f), Random.value < 0.5f ? BoonFX.Pink : Color.white, 1, 0.05f, 0.4f);
    }
}

// Fading line between two points (fishing line, skewer thrust)
public static class Lines
{
    private static Material material;

    public static void Flash(Vector3 a, Vector3 b, Color start, Color end, float width, float life, int order = BoonFX.Order + 4)
    {
        if (material == null && CatFX.Unlit != null) material = new Material(CatFX.Unlit) { mainTexture = Texture2D.whiteTexture };
        var go = new GameObject("Boon Line");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.widthMultiplier = width;
        lr.numCapVertices = 0;
        lr.sortingLayerName = "Default";
        lr.sortingOrder = order;
        if (material != null) lr.sharedMaterial = material;
        lr.startColor = start;
        lr.endColor = end;
        go.AddComponent<LineFade>().life = life;
    }
}

// Screen-wide flash (Blinding Dash)
public class ScreenFlash : MonoBehaviour
{
    private Image image;
    private float age, life;
    private Color color;

    public static void Play(Color color, float life)
    {
        Image img = OverlayUI.MakeImage("Boon Flash", OverlayUI.Root, color, OverlayUI.WhiteSprite);
        img.raycastTarget = false;
        RectTransform r = img.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        r.SetAsFirstSibling(); // under the HUD
        var f = img.gameObject.AddComponent<ScreenFlash>();
        f.image = img; f.life = life; f.color = color;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float k = 1f - age / life;
        if (k <= 0f) { Destroy(gameObject); return; }
        image.color = new Color(color.r, color.g, color.b, color.a * k * k);
    }
}

// ================================================================================================ Sea Abyss
// Wipeout: while Rowdy surfs he IS the wave. Enemies in front get swept up and carried along, then slammed when the
// ride ends (more into a wall). Beach Bod: the pink wave (Misc/Test/testWave), at its own pixel size.
public class TideRider : MonoBehaviour
{
    private static TideRider active;
    public static bool Riding => active != null;
    public static bool RidingPink => active != null && active.pink;

    private PlayerMovement move;
    private SpriteRenderer rowdyBody;
    private float damage, age, foamTimer;
    private bool pink, toxic, sawSurf, crashed;
    private readonly List<EnemyHealth> carried = new List<EnemyHealth>();
    private SheetFX water;
    private SpriteRenderer pinkWave;
    private Light2D pinkGlow;
    private float trailTimer;
    private const float MaxRide = 2.4f, MinRide = 0.35f;

    public static void Begin(BoonRunner runner, float damage, bool pink, bool toxic)
    {
        if (runner == null) return;
        if (active != null) active.Crash(false);
        var go = new GameObject("Tide Rider");
        var t = go.AddComponent<TideRider>();
        active = t;
        t.move = BoonRunner.Movement;
        t.rowdyBody = runner.GetComponent<SpriteRenderer>();
        t.damage = damage * (pink ? 1.5f : 1f);
        t.pink = pink;
        t.toxic = toxic;
        Vector3 feet = BoonRunner.RowdyFeet;
        go.transform.position = feet;
        int layer = t.rowdyBody != null ? t.rowdyBody.sortingLayerID : 0;
        int order = t.rowdyBody != null ? t.rowdyBody.sortingOrder : BoonFX.Order;

        BoonArt art = BoonArt.Get;
        if (pink && BeachBodWave.Frames != null)
        {
            // in FRONT of Rowdy, animated (BeachBodWave), unlit so it glows, with a pink light around it
            t.pinkWave = BoonFX.MakeRenderer("Pink Wave", BeachBodWave.Frames[0], feet, order + 2, null, CatFX.Unlit != null ? CatFX.Unlit : ItemArt.Lit);
            t.pinkWave.sortingLayerID = layer;
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(t.pinkWave.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            t.pinkGlow = lightGo.AddComponent<Light2D>();
            t.pinkGlow.lightType = Light2D.LightType.Point;
            t.pinkGlow.color = new Color(1f, 0.45f, 0.85f);
            t.pinkGlow.pointLightOuterRadius = 2.6f;
            t.pinkGlow.pointLightInnerRadius = 0.4f;
            t.pinkGlow.intensity = 1.1f;
        }
        else if (art != null)
        {
            Color tint = toxic ? new Color(0.6f, 1f, 0.45f) : Color.white;
            t.water = BoonFX.Sheet(art.waterSonic, 9, feet + Vector3.up * 0.5f, 22f, 1f, tint, null, true, order + 1);
            if (t.water != null) t.water.SetSorting(layer, order + 1);
        }
        if (art != null)
        {
            BoonArt.Play(art.tornado, 0.4f, pink ? 0.85f : 1.1f);
            BoonArt.Play(art.waveCrash, 0.45f, 1f);
        }
        FXParticle.Burst(feet + Vector3.up * 0.3f, pink ? BoonFX.Pink : BoonFX.Foam, 14, 1f, 4f, 8f, 0.5f, true);
        ScreenShake.Impulse(0.25f);
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
        if (water != null) water.Stop(0.15f);
        if (pinkWave != null) Destroy(pinkWave.gameObject);
    }

    private void LateUpdate()
    {
        if (crashed) return;
        Transform rowdy = BoonRunner.Rowdy;
        if (rowdy == null || move == null) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        age += dt;
        bool surfing = move.IsSurfing;
        if (surfing) sawSurf = true;
        float facing = BoonRunner.RowdyFacing;
        Vector3 feet = BoonRunner.RowdyFeet;
        transform.position = feet;

        // the wave hugs Rowdy: water curling over his front, or the pink wave under and behind him
        if (water != null)
        {
            water.transform.position = feet + new Vector3(facing * 0.3f, 0.5f, 0f);
            water.transform.localScale = new Vector3(facing, 1f, 1f);
        }
        if (pinkWave != null)
        {
            float bob = Mathf.Round(Mathf.Sin(age * 12f) * 2f) / 64f; // whole pixels
            pinkWave.transform.position = feet + new Vector3(-facing * 0.25f, -0.18f + bob, 0f);
            pinkWave.flipX = facing < 0f;
            Sprite[] frames = BeachBodWave.Frames;
            if (frames != null) pinkWave.sprite = frames[(int)(age * 16f) % frames.Length];
            if (pinkGlow != null) pinkGlow.intensity = 0.9f + 0.4f * Mathf.Sin(age * 9f);
            // pink afterimages streaming off the back, hearts and glitter over the crest
            trailTimer -= dt;
            if (trailTimer <= 0f)
            {
                trailTimer = 0.05f;
                CatFX.Afterimage(pinkWave, new Color(1f, 0.5f, 0.85f, 0.45f), 0.25f);
                Vector3 crest = pinkWave.bounds.center + new Vector3(facing * Random.Range(0f, pinkWave.bounds.extents.x), pinkWave.bounds.extents.y * 0.6f, 0f);
                BoonFX.Sparkles(crest, Random.value < 0.5f ? Color.white : BoonFX.Gold, 1, 0.3f, 0.45f);
                FXParticle.Burst(crest, Color.Lerp(BoonFX.Pink, Color.white, Random.value * 0.6f), 2, 1.5f, 4f, 6f, 0.5f);
            }
        }

        Vector3 front = feet + new Vector3(facing * 0.7f, 0.5f, 0f);
        bool over = age >= MaxRide || (age >= MinRide && !surfing && (sawSurf || age >= 0.6f));
        if (carried.Count > 0 && SolidGround.Blocked(front + new Vector3(facing * 0.3f, 0.1f, 0f), new Vector2(0.2f, 0.4f))) { Crash(true); return; }
        if (over) { Crash(false); return; }

        // sweep up everything in front
        foreach (EnemyHealth e in BoonFX.EnemiesInBox(feet + new Vector3(facing * 0.55f, 0.55f, 0f), new Vector2(1.3f, 1.2f)))
        {
            if (carried.Contains(e)) continue;
            carried.Add(e);
            BoonFX.Stun(e, MaxRide + 0.6f);
            FXParticle.Burst(BoonFX.Center(e), pink ? BoonFX.Pink : BoonFX.Foam, 8, 1f, 3f, 6f, 0.4f);
            if (pink) BoonFX.Charm(e, 2.5f);
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.splash : null, 0.3f, Random.Range(1.2f, 1.5f));
        }
        // and carry them along on the crest
        for (int i = 0; i < carried.Count; i++)
        {
            EnemyHealth e = carried[i];
            if (e == null || e.enemydead || !e.TryGetComponent(out Rigidbody2D rb)) continue;
            float targetX = feet.x + facing * (0.75f + 0.18f * Mathf.Min(i, 3));
            Vector2 p = rb.position;
            if (facing * (targetX - p.x) > 0f && !SolidGround.Blocked(new Vector2(targetX, p.y + 0.3f), new Vector2(0.15f, 0.3f))) p.x = targetX;
            if (rb.bodyType == RigidbodyType2D.Dynamic)
            {
                rb.position = p;
                rb.linearVelocity = new Vector2(0f, Mathf.Max(rb.linearVelocity.y, 0f));
            }
            else rb.MovePosition(p);
        }

        foamTimer -= dt;
        if (foamTimer <= 0f)
        {
            foamTimer = 0.035f;
            Color c = pink ? Color.Lerp(BoonFX.Pink, Color.white, Random.value * 0.5f) : toxic ? BoonFX.Toxic : BoonFX.Foam;
            FXParticle.Burst(feet + new Vector3(facing * 0.5f, Random.Range(0.05f, 0.9f), 0f), c, 2, 1f, 3f, 8f, 0.45f, true);
            if (pink && Random.value < 0.3f) BoonFX.Sparkles(feet + Vector3.up * 0.9f, BoonFX.Gold, 1, 0.6f, 0.5f);
        }
    }

    private void Crash(bool wall)
    {
        if (crashed) return;
        crashed = true;
        float facing = BoonRunner.RowdyFacing;
        Vector3 at = BoonRunner.RowdyFeet + new Vector3(facing * 0.8f, 0.5f, 0f);
        BoonArt art = BoonArt.Get;
        if (art != null) BoonArt.Play(art.waterBoom, wall ? 0.6f : 0.35f, wall ? 0.9f : 1.2f);
        FXParticle.Burst(at, pink ? BoonFX.Pink : toxic ? BoonFX.Toxic : BoonFX.Foam, wall ? 30 : 16, 2f, 6f, 10f, 0.7f, true);
        PulseRing.Spawn(at, pink ? new Color(1f, 0.6f, 0.9f, 0.9f) : new Color(0.7f, 1f, 1f, 0.9f), 1.3f, 0.3f);
        if (carried.Count > 0)
        {
            ScreenShake.Impulse(wall ? 0.5f : 0.3f);
            TimeSlowController.HitStop(wall ? 0.06f : 0.03f, 0.08f);
        }
        foreach (EnemyHealth e in carried)
        {
            if (e == null || e.enemydead) continue;
            BoonFX.Hit(e, damage * (wall ? Balance.WipeoutWallBonus : 1f), pink ? "Beach Bod" : "Wipeout");
            if (e.TryGetComponent(out StatusEffects s)) s.EndStun();
            if (toxic) BoonFX.Poison(e, 4f, 8f);
            Vector2 fling = Balance.WipeoutFling;
            if (!wall) BoonFX.Push(e, new Vector2(facing * fling.x, fling.y));
        }
        if (water != null) water.Stop(0.15f);
        if (pinkWave != null) Shard.Break(pinkWave, 4, 3, pinkWave.bounds.center, 2.5f);
        Destroy(gameObject);
    }
}

// Beach Bod's wave, animated: 16 frames made at runtime from the pink wave art (Misc/Test/testWave.png) at its own
// pixel size. Every column rolls up and down (a travelling swell), the top edge is a white foam line, a glossy
// shine band sweeps across, the pinks shimmer through magenta, and glitter pixels twinkle on it.
public static class BeachBodWave
{
    private const int Count = 16, Pad = 3;
    private static Sprite[] frames;
    private static bool tried;

    public static Sprite[] Frames
    {
        get
        {
            if (frames != null || tried) return frames;
            tried = true;
            Sprite src = BoonArt.PinkWave;
            if (src == null) return null;
            Color32[] px = ReadPixels(src.texture);
            if (px == null) return frames = new[] { src };
            int w = src.texture.width, h = src.texture.height, oh = h + Pad * 2;
            frames = new Sprite[Count];
            var outPx = new Color32[w * oh];
            for (int f = 0; f < Count; f++)
            {
                float phase = f / (float)Count * Mathf.PI * 2f;
                System.Array.Clear(outPx, 0, outPx.Length);
                for (int x = 0; x < w; x++)
                {
                    int dy = Mathf.RoundToInt(Mathf.Sin(phase + x * 0.16f) * 2f);
                    int top = -1;
                    for (int y = h - 1; y >= 0; y--) if (px[y * w + x].a > 20) { top = y; break; }
                    for (int y = 0; y < h; y++)
                    {
                        Color32 c = px[y * w + x];
                        if (c.a <= 20) continue;
                        Color col = c;
                        // shimmer: hue drifts between pink and magenta along the wave
                        Color.RGBToHSV(col, out float hh, out float s, out float v);
                        hh = Mathf.Repeat(hh + 0.035f * Mathf.Sin(phase * 2f + x * 0.07f), 1f);
                        v = Mathf.Clamp01(v * (1.05f + 0.1f * Mathf.Sin(phase + x * 0.1f)));
                        col = Color.HSVToRGB(hh, s, v);
                        // foam on the crest
                        if (top >= 0 && y >= top - 1) col = Color.Lerp(col, Color.white, y == top ? 0.85f : 0.45f);
                        // the gloss band, sweeping across
                        float band = Mathf.Repeat(f / (float)Count * 1.6f, 1.6f) * (w + h) - (w + h) * 0.3f;
                        float d = Mathf.Abs(x + (h - y) - band);
                        if (d < 4f) col = Color.Lerp(col, Color.white, d < 2f ? 0.55f : 0.25f);
                        // glitter
                        int hash = (x * 73856093) ^ (y * 19349663) ^ (f * 83492791);
                        if ((hash & 0x3FF) < 6) col = Color.white;
                        col.a = c.a / 255f;
                        int oy = y + Pad + dy;
                        if (oy >= 0 && oy < oh) outPx[oy * w + x] = col;
                    }
                }
                var tex = new Texture2D(w, oh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "BeachBod_" + f };
                tex.SetPixels32(outPx);
                tex.Apply(false, true);
                Vector2 pivot = src.pivot / new Vector2(w, h); // pivot in 0..1 of the source
                frames[f] = Sprite.Create(tex, new Rect(0, 0, w, oh), new Vector2(pivot.x, (pivot.y * h + Pad) / oh), src.pixelsPerUnit);
            }
            return frames;
        }
    }

    // Works on non-readable textures too (copied through a render texture)
    private static Color32[] ReadPixels(Texture2D tex)
    {
        try
        {
            if (tex.isReadable) return tex.GetPixels32();
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] result = copy.GetPixels32();
            Object.Destroy(copy);
            return result;
        }
        catch { return null; }
    }
}

public static class AbyssFX
{
    // Crushing Depths: the dark rushes in, then everything near the target is crushed
    public static void Crush(Vector3 at, float damage)
    {
        InwardMote.Ring(at, 1.5f, 16, new Color(0.25f, 0.45f, 0.9f, 1f), 0.16f);
        BoonRunner.Delay(0.13f, () =>
        {
            foreach (EnemyHealth e in BoonFX.EnemiesIn(at, 1.5f)) BoonFX.Hit(e, damage, "Crushing Depths");
            PulseRing.Spawn(at, new Color(0.3f, 0.55f, 1f, 1f), 1.5f, 0.25f);
            PulseRing.Spawn(at, new Color(0.05f, 0.1f, 0.3f, 0.9f), 0.8f, 0.2f);
            FXParticle.Burst(at, BoonFX.Deep, 14, 1.5f, 4f, 4f, 0.45f);
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.waterBoom : null, 0.45f, 0.65f);
            TimeSlowController.HitStop(0.05f, 0.06f);
            ScreenShake.Impulse(0.3f);
        });
    }

    // Rip Current: everything within 3 is dragged toward Rowdy and slowed
    public static void RipCurrent(Vector3 feet, Transform rowdy, float slow, bool toxic)
    {
        InwardMote.Ring(feet + Vector3.up * 0.4f, 2.8f, 18, toxic ? BoonFX.Toxic : new Color(0.45f, 0.75f, 1f, 1f), 0.35f, 0.8f);
        PulseRing.Spawn(feet + Vector3.up * 0.05f, new Color(0.3f, 0.6f, 1f, 0.7f), 3f, 0.3f, 90, true);
        int pulled = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(feet + Vector3.up * 0.4f, 3f))
        {
            float d = Mathf.Abs(BoonFX.Center(e).x - feet.x);
            UndertowPull.Begin(e, rowdy, Mathf.Clamp(d - 0.9f, 0.3f, 2f), 0.28f);
            BoonFX.Slow(e, 2f, slow);
            if (toxic) BoonFX.Poison(e, 3f, 8f);
            pulled++;
        }
        if (pulled > 0) BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.boto : null, 0.35f, 0.8f);
    }

    // Leviathan: four tentacles burst out of the ground around Rowdy
    public static void Tentacles(Vector3 feet, float damage)
    {
        float[] xs = { -2.1f, -1.05f, 1.05f, 2.1f };
        BoonArt art = BoonArt.Get;
        if (art != null) { BoonArt.Play(art.waterBoom, 0.6f, 0.55f); BoonArt.Play(art.vines, 0.4f, 0.6f); }
        ScreenShake.Impulse(0.6f);
        GamepadRumble.Pulse(0.5f, 0.7f, 0.35f);
        for (int i = 0; i < xs.Length; i++)
        {
            Vector3 at = feet + new Vector3(xs[i], 0.4f, 0f);
            if (SolidGround.Ray(at + Vector3.up * 0.6f, Vector2.down, 2f, out RaycastHit2D hit)) at = hit.point;
            else at.y = feet.y;
            Tentacle.Rise(at, damage, xs[i] < 0f, i * 0.06f);
        }
    }
}

public class Tentacle : MonoBehaviour
{
    private SpriteRenderer sr;
    private float age, delay, damage;
    private bool hit;
    private const float Life = 1.1f;

    public static void Rise(Vector3 ground, float damage, bool flip, float delay)
    {
        SpriteRenderer sr = BoonFX.MakeRenderer("Tentacle", MoreSprites.Tentacle, ground + Vector3.down * 0.05f, 72, null, ItemArt.Lit);
        sr.flipX = flip;
        sr.transform.localScale = new Vector3(1f, 0f, 1f);
        var t = sr.gameObject.AddComponent<Tentacle>();
        t.sr = sr; t.damage = damage; t.delay = delay;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float a = age - delay;
        if (a < 0f) return;
        float k = Mathf.Clamp01(a / 0.12f);
        float grow = k < 1f ? 1f - (1f - k) * (1f - k) : 1f;
        float sway = Mathf.Sin(a * 9f) * 4f * (1f - Mathf.Clamp01(a / Life));
        transform.localScale = new Vector3(1f, grow, 1f);
        transform.rotation = Quaternion.Euler(0f, 0f, sway);
        if (!hit && k >= 1f)
        {
            hit = true;
            Vector3 c = transform.position + Vector3.up * 0.35f;
            GroundShock.Spawn(transform.position, 1f, new Color(0.55f, 0.45f, 1f), BoonFX.Deep, 0.3f);
            FXParticle.Burst(transform.position, BoonFX.Deep, 10, 1.5f, 4f, 9f, 0.5f, true);
            foreach (EnemyHealth e in BoonFX.EnemiesInBox(c, new Vector2(0.9f, 1.2f)))
            {
                BoonFX.Hit(e, damage, "Leviathan");
                BoonFX.Push(e, new Vector2(Mathf.Sign(BoonFX.Center(e).x - BoonRunner.RowdyCenter.x) * 3f, 6f));
            }
        }
        if (a >= Life)
        {
            Shard.Break(sr, 2, 5, transform.position + Vector3.up * 0.3f, 2f);
            FXParticle.Burst(transform.position + Vector3.up * 0.4f, new Color(0.45f, 0.6f, 1f), 10, 1f, 3f, 8f, 0.4f);
            Destroy(gameObject);
        }
    }
}

// Ink Cloud: a squirt of black ink at the dash start; enemies inside panic. The ink is Rowdy's own surf spray
// (the "SurfWater" particles on his prefab) cloned and dyed black: a big burst back and up, then it keeps
// bubbling while the cloud lasts.
public class InkCloud : MonoBehaviour
{
    private float age, fear;
    private readonly HashSet<EnemyHealth> done = new HashSet<EnemyHealth>();
    private const float Life = 2.2f, Radius = 1.6f;
    private ParticleSystem[] sprays;
    private static Material inkMaterial;

    private static readonly Color InkDark = new Color(0.03f, 0.02f, 0.06f, 1f), InkSheen = new Color(0.2f, 0.12f, 0.32f, 0.9f);

    public static void Spawn(Vector3 at, float facing, float fearSeconds)
    {
        var go = new GameObject("Ink Cloud");
        go.transform.position = at;
        var c = go.AddComponent<InkCloud>();
        c.fear = fearSeconds;
        c.sprays = new[] { c.Spray(at, facing, 115f, 1f), c.Spray(at, facing, 65f, 0.7f), c.Spray(at + Vector3.down * 0.3f, -facing, 150f, 0.5f) };
        if (c.sprays[0] == null) // no surf spray to copy: plain ink blobs
            FXParticle.Burst(at, InkDark, 26, 1f, 4f, 2f, 0.9f);
        FXParticle.Burst(at, InkSheen, 10, 1f, 3f, 3f, 0.6f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sporePop : null, 0.45f, 0.6f);
        FXSound.Play("Jelly", 0.35f, 0.6f); // a wet squelch
    }

    // One spray of ink, like Rowdy's surf spray (a cone of drops that arcs and falls) but black. 'angle' = degrees
    // from the facing direction (0 = forward, 90 = up). Built fresh: Rowdy's own SurfWater particles don't draw
    // when cloned (built-in material + a size-over-life curve made for his speed).
    private ParticleSystem Spray(Vector3 at, float facing, float angle, float strength)
    {
        var go = new GameObject("Ink Spray");
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        float dir = (facing >= 0f ? angle : 180f - angle) * Mathf.Deg2Rad;
        go.transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(dir), Mathf.Sin(dir), 0f), Vector3.back); // the cone shoots along local +z
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(InkDark, InkSheen);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f * strength, 7.5f * strength);
        // drops at the game's own pixel size (whole pixels: 2 px, the odd 3 px one) - the old 4-12 px squares read as blocks
        main.startSize = (strength >= 1f ? 3f : 2f) / 64f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
        main.gravityModifier = 0.9f;
        main.maxParticles = 600;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.12f;
        var emission = ps.emission;
        emission.rateOverTime = 700f * strength;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var size = ps.sizeOverLifetime;
        size.enabled = false; // shrinking drops ended up between pixel sizes
        var psr = go.GetComponent<ParticleSystemRenderer>();
        if (inkMaterial == null && CatFX.Unlit != null) inkMaterial = new Material(CatFX.Unlit) { name = "Ink", mainTexture = Texture2D.whiteTexture };
        if (inkMaterial != null) psr.sharedMaterial = inkMaterial;
        psr.sortingLayerName = "Default";
        psr.sortingOrder = BoonFX.Order + 2;
        ps.Play();
        return ps;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Life + 1f) { Destroy(gameObject); return; }
        // the squirt: a hard burst first, then it bubbles, then it stops and the last drops fall
        if (sprays != null)
            foreach (ParticleSystem ps in sprays)
            {
                if (ps == null) continue;
                var emission = ps.emission;
                emission.rateOverTimeMultiplier = age < 0.25f ? 700f : age < Life ? 70f : 0f;
            }
        if (age >= Life) return;
        if (Random.value < 0.4f) FXParticle.Burst(transform.position + (Vector3)(Random.insideUnitCircle * Radius * 0.8f), InkDark, 1, 0.1f, 0.4f, -0.5f, 0.7f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(transform.position, Radius))
        {
            if (!done.Add(e)) continue;
            BoonFX.Fear(e, fear);
            BoonFX.Popup(BoonFX.Center(e) + Vector3.up * 0.6f, "!?", new Color(0.6f, 0.5f, 1f), 0.6f, 0.7f);
        }
    }
}

// Angler Lure: a glowing bulb dangling over Rowdy's head that zaps the nearest enemy every few seconds
public class AnglerLure : MonoBehaviour
{
    private static AnglerLure instance;
    private SpriteRenderer bulb;
    private LineRenderer line;
    private Light2D glow;
    private float zapTimer = 1f, age;

    public static void Keep(bool on, Transform rowdy)
    {
        if (on && instance == null && rowdy != null)
        {
            var go = new GameObject("Angler Lure");
            instance = go.AddComponent<AnglerLure>();
            instance.Build();
        }
        else if (!on && instance != null) { Destroy(instance.gameObject); instance = null; }
    }

    private void Build()
    {
        bulb = BoonFX.MakeRenderer("Bulb", MoreSprites.Lure, transform.position, BoonFX.Order + 1, transform);
        var lineGo = new GameObject("Line");
        lineGo.transform.SetParent(transform, false);
        line = lineGo.AddComponent<LineRenderer>();
        line.positionCount = 6;
        line.widthMultiplier = 1f / 64f;
        line.sortingLayerName = "Default";
        line.sortingOrder = BoonFX.Order;
        if (CatFX.Unlit != null) line.sharedMaterial = new Material(CatFX.Unlit) { mainTexture = Texture2D.whiteTexture };
        line.startColor = line.endColor = new Color(0.15f, 0.2f, 0.35f, 0.9f);
        var lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(bulb.transform, false);
        glow = lightGo.AddComponent<Light2D>();
        glow.lightType = Light2D.LightType.Point;
        glow.color = new Color(0.4f, 1f, 0.85f);
        glow.intensity = 0.7f;
        glow.pointLightOuterRadius = 1.4f;
        glow.pointLightInnerRadius = 0.1f;
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void LateUpdate()
    {
        if (BoonRunner.Rowdy == null) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        float f = BoonRunner.RowdyFacing;
        Vector3 root = BoonRunner.RowdyHead + new Vector3(-f * 0.05f, 0.05f, 0f);
        Vector3 tip = root + new Vector3(f * 0.45f, 0.42f + Mathf.Round(Mathf.Sin(age * 3f) * 3f) / 64f, 0f);
        bulb.transform.position = tip;
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            Vector3 p = Vector3.Lerp(root, tip, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.12f;
            line.SetPosition(i, p);
        }
        glow.intensity = 0.55f + 0.2f * Mathf.Sin(age * 5f);
        if (PauseMenu.IsPaused) return;

        zapTimer -= Time.deltaTime;
        if (zapTimer > 0f) return;
        EnemyHealth target = BoonFX.Nearest(tip, 6f);
        if (target == null) { zapTimer = 0.3f; return; }
        zapTimer = 2.5f;
        Vector3 c = BoonFX.Center(target);
        BoonFX.Lightning(tip, c, new Color(0.4f, 1f, 0.85f), 0.2f);
        BoonFX.Hit(target, Boons.V("angler", 0), "Angler Lure");
        PulseRing.Spawn(tip, new Color(0.5f, 1f, 0.9f, 0.9f), 0.5f, 0.2f);
        FXParticle.Burst(c, new Color(0.4f, 1f, 0.85f), 6, 1f, 3f, 2f, 0.4f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.zap : null, 0.22f, Random.Range(1.6f, 1.9f));
    }
}

// ================================================================================================ Narcissism
// Runway Walk: a pink carpet rolls out under the dash; enemies on it are charmed once and take a little damage
public class RunwayCarpet : MonoBehaviour
{
    private readonly List<SpriteRenderer> segments = new List<SpriteRenderer>();
    private readonly HashSet<EnemyHealth> done = new HashSet<EnemyHealth>();
    private float age, charm, width;
    private const float Life = 3f, SegW = 0.5f;

    public static void Spawn(Vector3 feet, float facing, float charmSeconds)
    {
        Vector3 ground = feet;
        if (SolidGround.Ray(feet + Vector3.up * 0.3f, Vector2.down, 2f, out RaycastHit2D hit)) ground = hit.point;
        const int count = 9;
        var go = new GameObject("Runway");
        go.transform.position = ground + new Vector3(facing * (count * SegW * 0.5f - 0.4f), 0f, 0f);
        var c = go.AddComponent<RunwayCarpet>();
        c.charm = charmSeconds;
        c.width = count * SegW;
        Sprite carpet = MoreSprites.Carpet;
        for (int i = 0; i < count; i++)
        {
            Vector3 at = ground + new Vector3(facing * (-0.4f + SegW * (i + 0.5f)), 0f, 0f);
            if (!SolidGround.Ray(at + Vector3.up * 0.3f, Vector2.down, 0.6f, out RaycastHit2D floor)) continue; // no carpet in the air
            at.y = floor.point.y - 1f / 64f;
            SpriteRenderer sr = BoonFX.MakeRenderer("Carpet", carpet, at, BoonFX.Order - 2, go.transform, ItemArt.Lit);
            sr.color = new Color(1f, 1f, 1f, 0f);
            c.segments.Add(sr);
        }
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.pose : null, 0.35f, 1.3f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.charmSfx : null, 0.25f, 1.2f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Life || segments.Count == 0) { Destroy(gameObject); return; }
        float fade = Mathf.Clamp01((Life - age) / 0.4f);
        for (int i = 0; i < segments.Count; i++)
        {
            float reveal = Mathf.Clamp01((age - i * 0.03f) / 0.06f); // rolls out one segment after another
            segments[i].color = new Color(1f, 1f, 1f, reveal * fade);
            if (reveal > 0f && reveal < 1f) FXParticle.Burst(segments[i].transform.position + Vector3.up * 0.08f, BoonFX.Gold, 1, 0.5f, 1.5f, 4f, 0.3f, true);
        }
        if (Random.value < 0.25f && segments.Count > 0)
            BoonFX.Sparkles(segments[Random.Range(0, segments.Count)].transform.position + Vector3.up * Random.Range(0.1f, 0.6f), Random.value < 0.5f ? BoonFX.Pink : BoonFX.Gold, 1, 0.1f, 0.4f);
        foreach (EnemyHealth e in BoonFX.EnemiesInBox(transform.position + Vector3.up * 0.4f, new Vector2(width, 0.8f)))
        {
            if (!done.Add(e)) continue;
            BoonFX.Charm(e, charm);
            BoonFX.Hit(e, 12f, "Runway Walk");
            BoonFX.Popup(BoonFX.Center(e) + Vector3.up * 0.6f, "STUNNING!", BoonFX.Pink, 0.6f, 0.8f);
        }
    }
}

// Blow A Kiss: a heart that flies to an enemy
public class KissHeart : MonoBehaviour
{
    private EnemyHealth target;
    private float damage, age;
    private Vector3 velocity;
    private SpriteRenderer sr;

    public static void Fire(Vector3 from, EnemyHealth target, float damage)
    {
        SpriteRenderer sr = BoonFX.MakeRenderer("Kiss", MoreSprites.BigHeart, from, BoonFX.Order + 5);
        var k = sr.gameObject.AddComponent<KissHeart>();
        k.target = target; k.damage = damage; k.sr = sr;
        k.velocity = new Vector3(Random.Range(-1f, 1f), 3.5f, 0f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.charmSfx : null, 0.3f, 1.5f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (target == null || target.enemydead || age > 2f)
        {
            FXParticle.Burst(transform.position, BoonFX.Pink, 6, 0.5f, 2f, 1f, 0.4f);
            Destroy(gameObject);
            return;
        }
        Vector3 to = BoonFX.Center(target) - transform.position;
        float speed = Mathf.Lerp(3f, 11f, Mathf.Clamp01(age / 0.4f));
        velocity = Vector3.Lerp(velocity, to.normalized * speed, Time.deltaTime * 8f);
        transform.position += velocity * Time.deltaTime;
        if (Time.frameCount % 3 == 0) CatFX.Afterimage(sr, new Color(1f, 0.5f, 0.85f, 0.4f), 0.15f);
        if (to.magnitude > 0.3f) return;

        BoonFX.Hit(target, damage, "Blow A Kiss");
        BoonFX.Charm(target, 1f);
        BoonFX.Sparkles(BoonFX.Center(target), BoonFX.Pink, 6, 0.3f, 0.5f);
        PulseRing.Spawn(BoonFX.Center(target), new Color(1f, 0.55f, 0.85f, 1f), 0.6f, 0.2f);
        Destroy(gameObject);
    }
}

// Spotlight: a warm cone of light from above follows Rowdy for a few seconds
public class SpotlightFX : MonoBehaviour
{
    private SpriteRenderer cone;
    private Transform rowdy;
    private float age, life;

    public static void Begin(Transform rowdy, float seconds)
    {
        var go = new GameObject("Spotlight");
        var s = go.AddComponent<SpotlightFX>();
        s.rowdy = rowdy; s.life = seconds;
        s.cone = PixelShape.Make("Cone", PixelShape.Kind.Cone, rowdy.position, BoonFX.Order - 3, go.transform);
        if (s.cone != null) PixelShape.Size(s.cone, 31f * 2.2f / 64f, 48f * 4.2f / 64f); // same size, real pixels
        else
        {
            s.cone = BoonFX.MakeRenderer("Cone", MoreSprites.Cone, rowdy.position, BoonFX.Order - 3, go.transform);
            s.cone.transform.localScale = new Vector3(2.2f, 4.2f, 1f);
        }
        s.cone.color = new Color(1f, 0.95f, 0.75f, 0f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.pose : null, 0.4f, 1f);
        BoonFX.Sparkles(BoonRunner.RowdyHead, BoonFX.Gold, 6, 0.4f, 0.6f);
    }

    private void LateUpdate()
    {
        if (rowdy == null) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        float a = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((life - age) / 0.35f);
        cone.transform.position = BoonRunner.RowdyFeet + Vector3.up * 3.1f;
        cone.color = new Color(1f, 0.95f, 0.75f, 0.42f * a * (0.9f + 0.1f * Mathf.Sin(age * 20f)));
        if (Random.value < 0.3f)
            FXParticle.Burst(BoonRunner.RowdyFeet + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.2f, 2.4f), 0f), new Color(1f, 0.95f, 0.75f), 1, 0.05f, 0.2f, 0.3f, 0.8f);
    }
}

public static class NarcFX
{
    // Jealousy: every charmed enemy slaps the nearest other enemy
    public static void JealousSlaps(Vector3 center, float damage)
    {
        foreach (EnemyHealth e in BoonFX.EnemiesIn(center, 9f))
        {
            if (!e.TryGetComponent(out StatusEffects s) || !s.IsCharmed) continue;
            Vector3 from = BoonFX.Center(e);
            EnemyHealth victim = BoonFX.Nearest(from, 2.6f, new[] { e });
            if (victim == null) continue;
            Vector3 at = BoonFX.Center(victim);
            BoonFX.Hit(victim, damage, "Jealousy");
            PulseRing.Spawn(at, new Color(1f, 0.6f, 0.85f, 0.9f), 0.45f, 0.15f);
            FXParticle.Burst(at, BoonFX.Pink, 5, 1f, 2.5f, 3f, 0.35f);
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.squish : null, 0.18f, Random.Range(1.5f, 1.8f));
        }
    }
}

// ================================================================================================ Lycanthropy
public static class LycFX
{
    public static void Pounce(EnemyHealth e, float damage, bool silver)
    {
        Vector3 c = BoonFX.Center(e);
        BoonArt art = BoonArt.Get;
        if (art != null)
        {
            SheetFX fx = BoonFX.Sheet(art.clawSlash, 6, c, 28f, 1f, silver ? new Color(0.85f, 0.92f, 1f) : new Color(1f, 0.55f, 0.6f));
            if (fx != null) fx.transform.localScale = new Vector3(Random.value < 0.5f ? -1f : 1f, 1f, 1f);
        }
        BoonFX.Hit(e, damage, "Pounce");
        BoonDot.Bleed(e, Mathf.Max(3f, damage * 0.25f), 3f);
        FXParticle.Burst(c, BoonFX.Blood, 8, 1.5f, 4f, 6f, 0.4f);
    }
}

// Hunters Mark: the toughest enemy on screen wears a red reticle
public class HuntersMark : MonoBehaviour
{
    public static EnemyHealth Target { get; private set; }
    private static HuntersMark marker;
    private SpriteRenderer reticle;
    private SpriteOutline outline;
    private EnemyHealth outlined;
    private readonly Dictionary<EnemyHealth, SpriteOutline> outlines = new Dictionary<EnemyHealth, SpriteOutline>();
    private float pickTimer, age;

    public static void Clear() => Target = null;

    public static void Keep(bool on, Vector3 center)
    {
        if (!on)
        {
            Target = null;
            if (marker != null) { Destroy(marker.gameObject); marker = null; }
            return;
        }
        if (marker == null)
        {
            var go = new GameObject("Hunters Mark");
            marker = go.AddComponent<HuntersMark>();
            marker.reticle = BoonFX.MakeRenderer("Reticle", MoreSprites.Reticle, center, BoonFX.Order + 6, go.transform);
        }
        marker.pickTimer -= Time.deltaTime;
        if (marker.pickTimer <= 0f || Target == null || Target.enemydead)
        {
            marker.pickTimer = 0.5f;
            EnemyHealth best = null;
            foreach (EnemyHealth e in BoonFX.EnemiesIn(center, 9f))
            {
                if (!EnemyFairness.OnScreen(BoonFX.Center(e), 0.02f)) continue;
                if (best == null || e.startingenemyHealth > best.startingenemyHealth + 0.5f ||
                    (Mathf.Abs(e.startingenemyHealth - best.startingenemyHealth) <= 0.5f && e.currentenemyHealth > best.currentenemyHealth)) best = e;
            }
            if (best != Target && Target != null && best != null) BoonFX.Popup(BoonFX.Center(best) + Vector3.up * 0.9f, "PREY", BoonFX.Blood, 0.55f, 0.7f);
            Target = best;
        }
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        bool show = Target != null && !Target.enemydead;
        reticle.enabled = show;
        if (outlined != Target)
        {
            if (outline != null) outline.color = Color.clear;
            outline = null;
            outlined = Target;
            if (show && Target.TryGetComponent(out SpriteRenderer body))
            {
                // our own outline per enemy (never another system's, e.g. the elite glow)
                if (!outlines.TryGetValue(Target, out outline) || outline == null) { outline = SpriteOutline.Add(body, Color.clear, 1, -1); outlines[Target] = outline; }
            }
        }
        if (!show) return;
        Vector3 head = Target.transform.position + Vector3.up * (EnemyFairness.HeadHeight(Target) + 0.25f);
        reticle.transform.position = head + Vector3.up * Mathf.Round(Mathf.Sin(age * 4f) * 2f) / 64f;
        reticle.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(age * 8f));
        if (outline != null) outline.color = new Color(1f, 0.15f, 0.25f, 0.55f + 0.3f * Mathf.Sin(age * 6f));
    }

    private void OnDestroy() { if (outline != null) outline.color = Color.clear; }
}

// ================================================================================================ Mama Rot
// Earth Erupt: pillars of earth burst out of the ground and root everyone around, then SHATTER.
// A kill in the air drops a glowing seed: the earth erupts where it lands.
public class EarthErupt : MonoBehaviour
{
    private readonly List<(SpriteRenderer sr, float delay, float height)> pillars = new List<(SpriteRenderer, float, float)>();
    private float age;
    private Vector3 ground;
    private const float BreakAt = 1.5f;

    public static void Spawn(EnemyHealth dead, Vector3 center, float rootSeconds)
    {
        if (SolidGround.Ray(center + Vector3.up * 0.2f, Vector2.down, 1.4f, out RaycastHit2D hit)) Erupt(hit.point, rootSeconds);
        else EruptSeed.Drop(center, rootSeconds); // airborne: the seed falls first
    }

    public static void Erupt(Vector3 groundPoint, float rootSeconds)
    {
        BoonArt art = BoonArt.Get;
        var go = new GameObject("Earth Erupt");
        go.transform.position = groundPoint;
        var v = go.AddComponent<EarthErupt>();
        v.ground = groundPoint;
        Sprite pillar = art != null && art.earthPillar != null ? ItemArt.Frames(art.earthPillar, 1, 1, new Vector2(0.5f, 0f), 64f)[0] : null;
        const int count = 7;
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) * 0.5f) * 0.32f + Random.Range(-0.08f, 0.08f);
            Vector3 at = groundPoint + new Vector3(x, -0.05f, 0f);
            if (SolidGround.Ray(at + Vector3.up * 0.5f, Vector2.down, 1f, out RaycastHit2D floor)) at.y = floor.point.y - 0.05f; // follow slopes, skip gaps
            else continue;
            SpriteRenderer sr = BoonFX.MakeRenderer("Earth Pillar", pillar != null ? pillar : BoonFX.Pixel, at, 70 + i % 3, go.transform, ItemArt.Lit);
            sr.color = Color.Lerp(new Color(0.85f, 0.95f, 0.75f), Color.white, Random.value);
            sr.flipX = Random.value < 0.5f;
            float height = Mathf.Lerp(0.5f, 1f, 1f - Mathf.Abs(i - (count - 1) * 0.5f) / count) * Random.Range(0.8f, 1.1f);
            sr.transform.localScale = new Vector3(1f, 0f, 1f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, x * -12f);
            v.pillars.Add((sr, i * 0.025f, height));
        }
        if (art != null) BoonArt.Play(art.vines, 0.55f, Random.Range(0.9f, 1.1f));
        GroundShock.Spawn(groundPoint, 3f, new Color(0.6f, 1f, 0.35f), new Color(0.35f, 0.55f, 0.2f), 0.4f);
        FXParticle.Burst(groundPoint, new Color(0.4f, 0.65f, 0.25f), 18, 1.5f, 4f, 9f, 0.6f, true);
        ScreenShake.Impulse(0.3f);
        int rooted = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(groundPoint + Vector3.up * 0.5f, 3f)) { BoonFX.Root(e, rootSeconds); rooted++; }
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age < BreakAt)
        {
            foreach (var (sr, delay, height) in pillars)
            {
                if (sr == null) continue;
                float k = Mathf.Clamp01((age - delay) / 0.14f);
                float grow = k < 1f ? 1f - (1f - k) * (1f - k) : 1f;
                float wobble = 1f + Mathf.Sin((age - delay) * 30f) * 0.08f * (1f - Mathf.Clamp01(age / 0.5f));
                sr.transform.localScale = new Vector3(1f, height * grow * wobble, 1f);
            }
            return;
        }

        // crack: a little shake first, then every pillar bursts into chunks of rock
        foreach (var (sr, _, _) in pillars)
        {
            if (sr == null) continue;
            Shard.Break(sr, 2, 5, ground + Vector3.down * 0.4f, 3.2f);
            FXParticle.Burst(sr.bounds.center, new Color(0.55f, 0.45f, 0.35f), 4, 1f, 3f, 9f, 0.4f);
        }
        FXParticle.Burst(ground + Vector3.up * 0.1f, new Color(0.45f, 0.38f, 0.3f), 14, 1f, 3f, 6f, 0.5f, true);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.rockBreak : null, 0.5f, Random.Range(0.85f, 1f));
        Destroy(gameObject);
    }
}

// The seed an airborne kill drops: glows, falls, and the earth erupts where it lands
public class EruptSeed : MonoBehaviour
{
    private float vy, age, roots;
    private SpriteRenderer sr;

    public static void Drop(Vector3 from, float rootSeconds)
    {
        SpriteRenderer sr = BoonFX.MakeRenderer("Erupt Seed", MoreSprites.Seed, from, BoonFX.Order + 3);
        var s = sr.gameObject.AddComponent<EruptSeed>();
        s.sr = sr; s.roots = rootSeconds; s.vy = 2.5f;
        // a burst of rot in the air where it died
        FXParticle.Burst(from, BoonFX.Toxic, 12, 1f, 3f, 2f, 0.5f);
        FXParticle.Burst(from, BoonFX.Violet, 6, 0.8f, 2f, 2f, 0.5f);
        PulseRing.Spawn(from, new Color(0.6f, 1f, 0.35f, 0.9f), 0.8f, 0.25f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sporePop : null, 0.3f, 1.5f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        vy -= 16f * dt;
        Vector3 p = transform.position;
        Vector3 next = p + Vector3.up * vy * dt;
        if (Time.frameCount % 2 == 0) FXParticle.Burst(p, BoonFX.Toxic, 1, 0.05f, 0.2f, 0f, 0.3f);
        sr.color = Color.Lerp(Color.white, new Color(0.7f, 1f, 0.5f), 0.5f + 0.5f * Mathf.Sin(age * 25f));
        if (vy < 0f && SolidGround.Ray(p, Vector2.down, p.y - next.y + 0.05f, out RaycastHit2D hit))
        {
            EarthErupt.Erupt(hit.point, roots);
            Destroy(gameObject);
            return;
        }
        transform.position = next;
        if (age > 2.5f) // fell into nothing
        {
            FXParticle.Burst(p, BoonFX.Toxic, 10, 1f, 3f, 2f, 0.5f);
            Destroy(gameObject);
        }
    }
}

public static class RotFX
{
    // Plague: the dead enemy's poison jumps on
    public static void Plague(Vector3 at, EnemyHealth dead, int jumps)
    {
        var skip = new List<EnemyHealth> { dead };
        float dps = Mathf.Max(8f, Boons.Has("rottenedge") ? Boons.V("rottenedge", 0) : 0f);
        for (int i = 0; i < jumps; i++)
        {
            EnemyHealth next = BoonFX.Nearest(at, 4f, skip);
            if (next == null) break;
            skip.Add(next);
            BoonFX.Lightning(at, BoonFX.Center(next), BoonFX.Toxic, 0.25f);
            BoonFX.Poison(next, 4f, dps);
        }
        if (skip.Count > 1)
        {
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sporePop : null, 0.35f, Random.Range(1.6f, 1.8f));
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.zap : null, 0.15f, 0.7f);
        }
    }

    // Thorn Skin: whoever hit Rowdy gets pricked and poisoned
    public static void Thorns(EnemyHealth attacker, float damage)
    {
        Vector3 c = BoonRunner.RowdyCenter;
        PulseRing.Spawn(c, new Color(0.55f, 1f, 0.3f, 0.9f), 0.9f, 0.2f);
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            FXParticle.Burst(c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.3f, i % 2 == 0 ? BoonFX.Toxic : new Color(0.35f, 0.55f, 0.2f), 1, 3f, 4f, 2f, 0.3f);
        }
        BoonFX.Lightning(c, BoonFX.Center(attacker), new Color(0.5f, 0.9f, 0.3f), 0.15f);
        BoonFX.Hit(attacker, damage, "Thorn Skin");
        BoonFX.Poison(attacker, 4f, 6f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.vines : null, 0.35f, Random.Range(1.4f, 1.6f));
    }
}

// Root Snare: roots wait in the ground where the dash started; the first few enemies to step in are held
public class RootSnare : MonoBehaviour
{
    private readonly List<SpriteRenderer> roots = new List<SpriteRenderer>();
    private readonly HashSet<EnemyHealth> done = new HashSet<EnemyHealth>();
    private float age, hold;
    private int catches;
    private const float Life = 6f, Width = 1.8f;

    public static void Spawn(Vector3 feet, float rootSeconds)
    {
        Vector3 ground = feet;
        if (SolidGround.Ray(feet + Vector3.up * 0.3f, Vector2.down, 2f, out RaycastHit2D hit)) ground = hit.point;
        var go = new GameObject("Root Snare");
        go.transform.position = ground;
        var s = go.AddComponent<RootSnare>();
        s.hold = rootSeconds;
        for (int i = 0; i < 6; i++)
        {
            Vector3 at = ground + new Vector3(-Width / 2f + Width * (i + 0.5f) / 6f + Random.Range(-0.05f, 0.05f), 0f, 0f);
            if (!SolidGround.Ray(at + Vector3.up * 0.4f, Vector2.down, 0.8f, out RaycastHit2D floor)) continue;
            at.y = floor.point.y - 1f / 64f;
            SpriteRenderer sr = BoonFX.MakeRenderer("Root", MoreSprites.Root(i), at, 71, go.transform, ItemArt.Lit);
            sr.flipX = Random.value < 0.5f;
            sr.transform.localScale = new Vector3(1f, 0f, 1f);
            s.roots.Add(sr);
        }
        FXParticle.Burst(ground, new Color(0.45f, 0.35f, 0.25f), 10, 1f, 2.5f, 8f, 0.4f, true);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.vines : null, 0.3f, 1.3f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] == null) continue;
            float k = Mathf.Clamp01((age - i * 0.03f) / 0.12f);
            float sway = 1f + Mathf.Sin(age * 6f + i) * 0.05f;
            roots[i].transform.localScale = new Vector3(1f, k * sway, 1f);
        }
        if (age >= Life || catches >= 3) { Crumble(); return; }
        foreach (EnemyHealth e in BoonFX.EnemiesInBox(transform.position + Vector3.up * 0.4f, new Vector2(Width, 0.8f)))
        {
            if (!done.Add(e)) continue;
            catches++;
            BoonFX.Root(e, hold);
            BoonFX.Poison(e, 3f, 6f);
            BoonFX.Popup(BoonFX.Center(e) + Vector3.up * 0.6f, "SNARED!", BoonFX.Toxic, 0.6f, 0.7f);
            FXParticle.Burst(BoonFX.Center(e), BoonFX.Toxic, 8, 1f, 2.5f, 3f, 0.4f);
        }
    }

    private void Crumble()
    {
        foreach (SpriteRenderer r in roots) if (r != null) Shard.Break(r, 2, 2, r.transform.position, 2f);
        Destroy(gameObject);
    }
}

// Bloom: a flower that heals Rowdy when he touches it
public class BloomFlower : MonoBehaviour
{
    private SpriteRenderer sr;
    private float heal, age;
    private Color glow;
    private const float Life = 20f;

    public static void Spawn(Vector3 at, float heal)
    {
        Vector3 ground = at;
        if (SolidGround.Ray(at + Vector3.up * 0.2f, Vector2.down, 6f, out RaycastHit2D hit)) ground = hit.point;
        // the user's flowers (FloraArt: purple or blue from RDR_Flowers) with a glow in their colour
        Sprite mine = FloraArt.RandomFlower(out Color glow);
        SpriteRenderer sr = BoonFX.MakeRenderer("Bloom", mine != null ? mine : MoreSprites.Flower, ground, 72, null, ItemArt.Lit);
        sr.transform.localScale = new Vector3(1f, 0f, 1f);
        if (mine != null) FloraArt.Pop(sr, glow, 0.9f, 1.2f);
        var b = sr.gameObject.AddComponent<BloomFlower>();
        b.sr = sr; b.heal = heal; b.glow = mine != null ? glow : BoonFX.Pink;
        FXParticle.Burst(ground + Vector3.up * 0.2f, BoonFX.Pink, 10, 1f, 2.5f, 2f, 0.5f, true);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sporePop : null, 0.35f, 1.8f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / 0.2f);
        float grow = k < 1f ? Mathf.Sin(k * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(k * Mathf.PI)) : 1f;
        transform.localScale = new Vector3(1f, grow, 1f);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 2.5f) * 4f);
        float fade = age > Life - 2f ? (Mathf.Repeat(age * 6f, 1f) < 0.5f ? 0.35f : 1f) : 1f;
        // a slow heartbeat of light in its own colour, so it reads as "touch me" from across the screen
        float beat = 0.5f + 0.5f * Mathf.Sin(age * 4f);
        sr.color = new Color(Mathf.Lerp(1f, glow.r, 0.25f * beat) + 0.1f * beat, Mathf.Lerp(1f, glow.g, 0.25f * beat) + 0.1f * beat, Mathf.Lerp(1f, glow.b, 0.25f * beat) + 0.1f * beat, fade);
        if (Random.value < 0.08f) BoonFX.Sparkles(transform.position + new Vector3(Random.Range(-0.15f, 0.15f), 0.2f, 0f), glow, 1, 0.1f, 0.5f);
        if (age >= Life) { Shard.Break(sr, 2, 3, transform.position, 1.5f); Destroy(gameObject); return; }

        Transform rowdy = BoonRunner.Rowdy;
        if (rowdy == null || Vector2.Distance(BoonRunner.RowdyCenter, transform.position + Vector3.up * 0.12f) > 0.75f) return;
        if (rowdy.TryGetComponent(out Health h)) h.AddHealth(heal, false);
        FXParticle.Burst(transform.position + Vector3.up * 0.15f, BoonFX.Pink, 16, 1f, 3.5f, 1f, 0.6f);
        FXParticle.Burst(transform.position + Vector3.up * 0.15f, BoonFX.Toxic, 8, 1f, 2.5f, 1f, 0.6f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.heal : null, 0.4f, 1.3f);
        Destroy(gameObject);
    }
}

// ================================================================================================ Stinky Bois Guild
public static class GuildFX
{
    private static readonly Color Stink = new Color(0.62f, 0.75f, 0.3f, 0.85f);

    public static void StinkLine(Vector3 at)
    {
        FXParticle.Burst(at + new Vector3(Random.Range(-0.15f, 0.15f), 0.15f, 0f), Stink, 1, 0.1f, 0.3f, -1.2f, 0.9f);
    }

    public static void StenchTick(PetFollower cat, float damage)
    {
        foreach (EnemyHealth e in BoonFX.EnemiesIn(cat.transform.position, 1.5f))
        {
            BoonFX.Hit(e, damage * Boons.CatDamageFor(cat), "Stench", cat, true);
            BoonFX.Slow(e, 1f, 0.25f);
            if (Random.value < 0.3f) FXParticle.Burst(BoonFX.Center(e), Stink, 3, 0.3f, 1f, -1f, 0.6f);
        }
    }
}

// Top Cat: the party leader wears a tiny crown
public class CatCrown : MonoBehaviour
{
    private static CatCrown instance;
    private PetFollower cat;
    private SpriteRenderer crown;
    private float age;

    public static void Keep(PetFollower leader)
    {
        if (leader == null || !leader.IsCollected)
        {
            if (instance != null) { Destroy(instance.gameObject); instance = null; }
            return;
        }
        if (instance == null)
        {
            var go = new GameObject("Top Cat Crown");
            instance = go.AddComponent<CatCrown>();
            instance.crown = BoonFX.MakeRenderer("Crown", MoreSprites.Crown, leader.transform.position, BoonFX.Order + 4, go.transform);
        }
        if (instance.cat != leader)
        {
            instance.cat = leader;
            BoonFX.Sparkles(leader.transform.position + Vector3.up * 0.4f, BoonFX.Gold, 6, 0.3f, 0.6f);
        }
    }

    private void LateUpdate()
    {
        if (cat == null) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        Bounds b = cat.TryGetComponent(out SpriteRenderer sr) ? sr.bounds : new Bounds(cat.transform.position, Vector3.one * 0.4f);
        crown.transform.position = new Vector3(b.center.x, b.max.y - 2f / 64f + Mathf.Round(Mathf.Sin(age * 5f) * 1f) / 64f, 0f);
        crown.enabled = sr == null || sr.enabled;
        if (Random.value < 0.03f) BoonFX.Sparkles(crown.transform.position + Vector3.up * 0.05f, BoonFX.Gold, 1, 0.1f, 0.4f);
    }
}

// Stray Tax: a fish treat pops out of the enemy; grab it to heal and feed the cats
public class FishTreat : MonoBehaviour
{
    private static Sprite[] frames;
    private SpriteRenderer sr;
    private Vector2 velocity;
    private float age;
    private bool landed;
    private const float Life = 12f;

    public static void Drop(Vector3 at)
    {
        if (frames == null)
        {
            Texture2D tex = Resources.Load<Texture2D>("AI Placeholders/Pickups/CatTreat_Fish");
            if (tex != null) { tex.filterMode = FilterMode.Point; frames = ItemArt.Frames(tex, 2, 1, new Vector2(0.5f, 0f), 64f); }
        }
        SpriteRenderer sr = BoonFX.MakeRenderer("Fish Treat", frames != null && frames.Length > 0 ? frames[0] : BoonFX.Heart, at, 75, null, ItemArt.Lit);
        var f = sr.gameObject.AddComponent<FishTreat>();
        f.sr = sr;
        f.velocity = new Vector2(Random.Range(-1.5f, 1.5f), 4.5f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (frames != null && frames.Length > 1) sr.sprite = frames[(int)(age * 6f) % frames.Length];
        if (!landed)
        {
            velocity.y -= 14f * dt;
            Vector3 p = transform.position;
            Vector3 next = p + (Vector3)(velocity * dt);
            if (velocity.y < 0f && SolidGround.Ray(p + Vector3.up * 0.05f, Vector2.down, p.y - next.y + 0.1f, out RaycastHit2D hit))
            {
                next = hit.point;
                if (velocity.y < -3f) { velocity = new Vector2(velocity.x * 0.4f, -velocity.y * 0.35f); BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.plop : null, 0.15f, 1.6f); }
                else { landed = true; velocity = Vector2.zero; }
            }
            transform.position = next;
            if (age > 4f) landed = true;
        }
        sr.color = age > Life - 2.5f && Mathf.Repeat(age * 8f, 1f) < 0.5f ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
        if (Random.value < 0.04f) BoonFX.Sparkles(transform.position + Vector3.up * 0.15f, BoonFX.Lavender, 1, 0.15f, 0.4f);
        if (age >= Life) { Destroy(gameObject); return; }

        if (age < 0.3f || BoonRunner.Rowdy == null || Vector2.Distance(BoonRunner.RowdyCenter, transform.position + Vector3.up * 0.15f) > 0.7f) return;
        if (BoonRunner.Rowdy.TryGetComponent(out Health h)) h.AddHealth(8f, false);
        foreach (PetFollower p in PetFollower.Pets)
        {
            if (p == null || !p.IsCollected) continue;
            if (Boons.Has("catfood")) p.WakeUp(); else p.Treat();
            BoonFX.Sparkles(p.transform.position, BoonFX.Lavender, 2, 0.2f, 0.4f);
        }
        FXParticle.Burst(transform.position + Vector3.up * 0.15f, BoonFX.Lavender, 10, 1f, 3f, 2f, 0.5f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.chomp : null, 0.4f, 1.3f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.guild : null, 0.4f * GameSettings.CatVoiceVolume, 1.3f);
        Destroy(gameObject);
    }
}

// ================================================================================================ Crazy Chef
public static class Lobbed
{
    public static void SporePod(Vector3 from, float facing, float dps)
    {
        Lob l = Lob.Throw(MoreSprites.Pod, from, new Vector2(facing * 4.5f, 4f), facing * -360f, (at, e) =>
        {
            Vector3 ground = at;
            if (SolidGround.Ray(at + Vector3.up * 0.3f, Vector2.down, 3f, out RaycastHit2D hit)) ground = hit.point;
            SporeCloud.Spawn(ground, dps, false);
        });
        l.trail = BoonFX.Toxic;
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.hairFlip : null, 0.22f, 1.3f);
    }

    public static void Meatball(Vector3 from, float facing, float damage, bool fermented)
    {
        Sprite s = BoonArt.FoodSprite(BoonArt.Food.Meatball);
        // the food art is tiny now (6-10 px, was drawn ~40-60 px on a 64 canvas): hit radius / blast sized to match
        Lob l = Lob.Throw(s != null ? s : MoreSprites.Pod, from, new Vector2(facing * 5f, 4.5f), facing * -420f, (at, e) =>
        {
            ChefFX.SauceBoom(at, damage, 1.15f, fermented, "Meatball Mortar");
        });
        l.radius = 0.22f;
        l.trail = new Color(0.75f, 0.2f, 0.1f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.hairFlip : null, 0.25f, 1.1f);
    }

    public static void Egg(Vector3 from, float facing, int index, float damage, bool fermented)
    {
        Vector2 v = new Vector2(facing * (5.5f + index * 1.2f), 2f + index * 1.4f);
        Lob.Throw(MoreSprites.Egg, from, v, facing * -720f, (at, e) =>
        {
            ChefFX.EggSplat(at, e, damage, fermented);
        }).radius = 0.25f;
    }

    public static void Hairball(Vector3 from, EnemyHealth target, float damage)
    {
        Vector3 to = BoonFX.Center(target);
        Lob l = Lob.Throw(MoreSprites.Hairball, from, Lob.Aim(from, to, 0.45f), 300f, (at, e) =>
        {
            FXParticle.Burst(at, new Color(0.62f, 0.55f, 0.47f), 10, 1f, 3f, 5f, 0.4f);
            if (e == null) return;
            BoonFX.Hit(e, damage, "Hairball");
            BoonFX.Slow(e, 2f, 0.4f);
        });
        l.radius = 0.3f;
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.guild : null, 0.3f * GameSettings.CatVoiceVolume, 1.6f);
    }

    public static void FoodFight(Vector3 from, EnemyHealth target, float damage, PetFollower cat)
    {
        BoonArt.Food[] pool = { BoonArt.Food.Bread, BoonArt.Food.Tomato, BoonArt.Food.Cheese, BoonArt.Food.Lettuce, BoonArt.Food.Meatball, BoonArt.Food.Egg };
        Sprite s = BoonArt.FoodSprite(pool[Random.Range(0, pool.Length)]);
        Vector3 to = BoonFX.Center(target);
        Lob l = Lob.Throw(s != null ? s : MoreSprites.Hairball, from, Lob.Aim(from, to, 0.4f), Random.Range(-500f, 500f), (at, e) =>
        {
            FXParticle.Burst(at, new Color(1f, 0.75f, 0.4f), 8, 1f, 3f, 5f, 0.4f);
            if (e != null) BoonFX.Hit(e, damage, "Food Fight", cat);
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.squish : null, 0.2f, Random.Range(1.2f, 1.5f));
        });
        l.radius = 0.22f; // small food (it's aimed at the target, so it still lands)
    }
}

public static class ChefFX
{
    public static void SauceBoom(Vector3 at, float damage, float radius, bool fermented, string with)
    {
        FXParticle.Burst(at, BoonFX.Sauce, 18, 1.5f, 4.5f, 8f, 0.55f, true);
        FXParticle.Burst(at, new Color(1f, 0.5f, 0.2f), 8, 1f, 3f, 6f, 0.45f);
        PulseRing.Spawn(at, new Color(1f, 0.35f, 0.2f, 0.9f), radius, 0.25f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.squish : null, 0.4f, 0.8f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.plop : null, 0.3f, 0.9f);
        ScreenShake.Impulse(0.2f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(at, radius))
        {
            BoonFX.Hit(e, damage, with);
            if (fermented) BoonFX.Poison(e, 4f, 8f);
        }
    }

    public static void EggSplat(Vector3 at, EnemyHealth e, float damage, bool fermented)
    {
        FXParticle.Burst(at, new Color(1f, 0.85f, 0.2f), 8, 1f, 3f, 6f, 0.4f);
        FXParticle.Burst(at, new Color(0.98f, 0.96f, 0.9f), 6, 1f, 2.5f, 6f, 0.4f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.squish : null, 0.25f, Random.Range(1.5f, 1.8f));
        Sprite fried = BoonArt.FoodSprite(BoonArt.Food.Egg);
        Sprite yolk = MoreSprites.Yolk;
        if (e != null)
        {
            BoonFX.Hit(e, damage, "Egg Toss");
            BoonFX.Stun(e, 0.8f);
            if (fermented) BoonFX.Poison(e, 4f, 8f);
            BoonFX.Popup(BoonFX.Center(e) + Vector3.up * 0.6f, "YOLKED!", new Color(1f, 0.85f, 0.25f), 0.55f, 0.7f);
            StuckFood.Stick(yolk, e, 0.8f); // a little yolk on the face (the big fried egg lands on the floor instead)
        }
        else if (fried != null) StuckFood.Lie(fried, at, 1.2f);
    }

    public static void TomatoSplat(Vector3 feet, float damage, bool fermented)
    {
        Sprite tomato = BoonArt.FoodSprite(BoonArt.Food.Tomato);
        for (int i = 0; i < 3; i++)
        {
            float side = i - 1;
            if (tomato == null) break;
            Lob.Throw(tomato, feet + Vector3.up * 0.3f, new Vector2(side * 3f, 3.5f + Mathf.Abs(side)), side * -400f, (at, e) =>
            {
                FXParticle.Burst(at, new Color(0.9f, 0.15f, 0.1f), 10, 1f, 3f, 7f, 0.45f, true);
                BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.squish : null, 0.2f, Random.Range(1.2f, 1.5f));
            }).radius = 0.01f; // they just splat; the damage happens on landing
        }
        GroundShock.Spawn(feet, 2f, new Color(1f, 0.3f, 0.2f), new Color(0.6f, 0.1f, 0.08f), 0.35f);
        FXParticle.Burst(feet + Vector3.up * 0.1f, new Color(0.9f, 0.15f, 0.1f), 18, 1.5f, 4f, 9f, 0.5f, true);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.squish : null, 0.45f, 0.75f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(feet + Vector3.up * 0.4f, 2f))
        {
            BoonFX.Hit(e, damage, "Tomato Splat");
            BoonFX.Slow(e, 2f, 0.4f);
            if (fermented) BoonFX.Poison(e, 4f, 8f);
        }
    }

    public static void CheeseSpray(Vector3 at, float facing)
    {
        FXParticle.Burst(at, new Color(1f, 0.82f, 0.25f), 12, 2f, 5f, 8f, 0.5f);
        FXParticle.Burst(at, new Color(1f, 0.95f, 0.55f), 6, 1f, 3f, 8f, 0.4f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.squish : null, 0.2f, Random.Range(1.6f, 1.9f));
    }

    public static void SauceSplash(Vector3 center, float damage)
    {
        PulseRing.Spawn(center, new Color(1f, 0.3f, 0.15f, 0.95f), 1.8f, 0.3f);
        FXParticle.Burst(center, BoonFX.Sauce, 20, 2f, 5f, 8f, 0.55f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sizzle : null, 0.3f, 1.2f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(center, 1.8f)) BoonFX.Hit(e, damage, "Secret Sauce");
    }
}

// A food sprite stuck on an enemy's face for a moment, or lying flat on the floor
public class StuckFood : MonoBehaviour
{
    private Transform follow;
    private Vector3 offset;
    private float age, life;
    private SpriteRenderer sr;

    public static void Stick(Sprite s, EnemyHealth e, float seconds)
    {
        Vector3 c = BoonFX.Center(e);
        SpriteRenderer sr = BoonFX.MakeRenderer("Stuck Food", s, c, BoonFX.Order + 2, null, ItemArt.Lit);
        var f = sr.gameObject.AddComponent<StuckFood>();
        f.sr = sr; f.follow = e.transform; f.offset = c - e.transform.position; f.life = seconds;
    }

    public static void Lie(Sprite s, Vector3 at, float seconds)
    {
        SpriteRenderer sr = BoonFX.MakeRenderer("Food Splat", s, at + Vector3.up * 0.05f, 60, null, ItemArt.Lit);
        var f = sr.gameObject.AddComponent<StuckFood>();
        f.sr = sr; f.life = seconds;
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        if (follow != null) transform.position = follow.position + offset;
        float k = age / life;
        sr.color = new Color(1f, 1f, 1f, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
    }
}

// Sandwich Time: a fat sandwich falls from the sky; walk into it to eat it
public class GiantSandwich : MonoBehaviour
{
    private SpriteRenderer sr;
    private float vy, age, groundY;
    private bool landed;
    private const float Life = 20f;

    public static void Drop(Vector3 near)
    {
        Vector3 ground = near;
        if (SolidGround.Ray(near + Vector3.up * 0.5f, Vector2.down, 6f, out RaycastHit2D hit)) ground = hit.point;
        else if (SolidGround.Ray(BoonRunner.RowdyCenter, Vector2.down, 6f, out RaycastHit2D under)) ground = under.point;
        SpriteRenderer sr = BoonFX.MakeRenderer("Giant Sandwich", MoreSprites.Sandwich, ground + Vector3.up * 6f, 76, null, ItemArt.Lit);
        var s = sr.gameObject.AddComponent<GiantSandwich>();
        s.sr = sr; s.groundY = ground.y;
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.chef : null, 0.4f, 1.1f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (!landed)
        {
            vy -= 22f * dt;
            Vector3 p = transform.position + Vector3.up * vy * dt;
            if (p.y <= groundY)
            {
                p.y = groundY;
                landed = true;
                GroundShock.Spawn(p, 1.4f, new Color(1f, 0.8f, 0.45f), new Color(0.6f, 0.45f, 0.3f), 0.35f);
                FXParticle.Burst(p + Vector3.up * 0.1f, new Color(0.9f, 0.75f, 0.5f), 14, 1f, 3f, 8f, 0.5f, true);
                BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.plop : null, 0.5f, 0.7f);
                ScreenShake.Impulse(0.25f);
            }
            transform.position = p;
            return;
        }
        // squash on the landing, then a gentle breathing (whole pixels)
        float land = Mathf.Clamp01((age - 0f) * 6f);
        transform.localScale = Vector3.one;
        if (Random.value < 0.05f) BoonFX.Sparkles(transform.position + new Vector3(Random.Range(-0.2f, 0.2f), 0.4f, 0f), new Color(1f, 0.8f, 0.4f), 1, 0.1f, 0.5f);
        sr.color = age > Life - 3f && Mathf.Repeat(age * 6f, 1f) < 0.5f ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        if (age >= Life) { Shard.Break(sr, 3, 3, transform.position, 2f); Destroy(gameObject); return; }

        Transform rowdy = BoonRunner.Rowdy;
        if (rowdy == null || land < 1f || Vector2.Distance(BoonRunner.RowdyCenter, transform.position + Vector3.up * 0.18f) > 0.8f) return;
        if (rowdy.TryGetComponent(out Health h)) h.AddHealth(25f, false);
        BoonRunner.Feed(8f);
        if (Boons.Has("catfood"))
            foreach (PetFollower p in PetFollower.Pets) if (p != null && p.IsCollected) p.WakeUp();
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.chomp : null, 0.6f, 1f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sparkle : null, 0.4f, 1.2f);
        // the ingredients fly out
        BoonArt.Food[] parts = { BoonArt.Food.Bread, BoonArt.Food.Lettuce, BoonArt.Food.Tomato, BoonArt.Food.Cheese, BoonArt.Food.Meatball };
        foreach (BoonArt.Food f in parts)
        {
            Sprite s = BoonArt.FoodSprite(f);
            if (s == null) continue;
            Lob l = Lob.Throw(s, transform.position + Vector3.up * 0.2f, new Vector2(Random.Range(-3f, 3f), Random.Range(4f, 6f)), Random.Range(-600f, 600f), null, BoonFX.Order + 4);
            l.radius = 0f; l.life = 0.7f;
        }
        PulseRing.Spawn(transform.position + Vector3.up * 0.2f, new Color(1f, 0.7f, 0.3f, 1f), 1.2f, 0.3f);
        Destroy(gameObject);
    }
}

// ================================================================================================ The Blacksmith
public static class SmithFX
{
    public static void SparkShower(Vector3 at, EnemyHealth except, float damage, float facing)
    {
        FXParticle.Burst(at, BoonFX.Ember, 10, 2f, 5f, 10f, 0.4f);
        FXParticle.Burst(at, BoonFX.Sunny, 6, 2f, 4f, 10f, 0.3f);
        int hits = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(at, 1.5f, except))
        {
            if (hits++ >= 3) break;
            BoonFX.Lightning(at, BoonFX.Center(e), BoonFX.Ember, 0.1f);
            BoonFX.Hit(e, damage, "Spark Shower", null, true);
        }
        if (Random.value < 0.6f) BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.clang : null, 0.15f, Random.Range(1.9f, 2.2f)); // tink
    }

    public static void Skewer(EnemyHealth first, float facing, float damage)
    {
        if (first == null) return;
        Vector3 a = BoonFX.Center(first);
        Vector3 b = a + new Vector3(facing * 2.6f, 0f, 0f);
        Lines.Flash(a - new Vector3(facing * 0.6f, 0f, 0f), b, new Color(1f, 1f, 1f, 0.9f), new Color(0.8f, 0.9f, 1f, 0f), 3f / 64f, 0.12f);
        int n = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesInBox(a + new Vector3(facing * 1.35f, 0f, 0f), new Vector2(2.6f, 0.6f)))
        {
            if (e == first || n >= 3) continue;
            n++;
            BoonFX.Hit(e, damage, "Skewer");
            FXParticle.Burst(BoonFX.Center(e), BoonFX.Steel, 6, 1f, 3f, 4f, 0.3f);
        }
        if (n > 0) BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.clang : null, 0.3f, Random.Range(1.6f, 1.9f));
    }

    public static void FishingLine(Vector3 from, Vector3 to)
    {
        Lines.Flash(from, to, new Color(0.9f, 0.95f, 1f, 0.8f), new Color(0.9f, 0.95f, 1f, 0.3f), 1f / 64f, 0.15f);
    }

    public static void AnvilDrop(Vector3 at, float damage)
    {
        Vector3 ground = at;
        if (SolidGround.Ray(at + Vector3.up * 1.5f, Vector2.down, 4f, out RaycastHit2D hit)) ground = hit.point;
        else return; // no floor in front: no anvil
        FallingAnvil.Drop(ground, damage);
    }
}

public class FallingAnvil : MonoBehaviour
{
    private SpriteRenderer sr;
    private float vy, groundY, damage, age, sat = -1f;

    public static void Drop(Vector3 ground, float damage)
    {
        SpriteRenderer sr = BoonFX.MakeRenderer("Anvil", MoreSprites.Anvil, ground + Vector3.up * 6f, 77, null, ItemArt.Lit);
        var a = sr.gameObject.AddComponent<FallingAnvil>();
        a.sr = sr; a.groundY = ground.y; a.damage = damage; a.vy = -6f;
        // a shadow warning on the floor
        PulseRing.Spawn(ground + Vector3.up * 0.03f, new Color(0.2f, 0.2f, 0.25f, 0.7f), 0.6f, 0.3f, 60, true);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.whoosh : null, 0.3f, 1.4f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (sat < 0f)
        {
            vy -= 40f * dt;
            Vector3 p = transform.position + Vector3.up * vy * dt;
            if (p.y <= groundY) { p.y = groundY; transform.position = p; Slam(); return; }
            transform.position = p;
            if (Time.frameCount % 2 == 0) CatFX.Afterimage(sr, new Color(0.7f, 0.75f, 0.9f, 0.4f), 0.12f);
            return;
        }
        // sits a moment, glows, then breaks apart
        sr.color = Color.Lerp(new Color(1f, 0.7f, 0.45f), Color.white, Mathf.Clamp01((age - sat) / 0.4f));
        if (age - sat > 0.7f)
        {
            Shard.Break(sr, 4, 2, transform.position + Vector3.down * 0.2f, 2.5f);
            FXParticle.Burst(transform.position + Vector3.up * 0.1f, BoonFX.Ember, 8, 1f, 3f, 8f, 0.4f);
            Destroy(gameObject);
        }
    }

    private void Slam()
    {
        sat = age;
        Vector3 at = transform.position;
        BoonArt art = BoonArt.Get;
        if (art != null)
        {
            BoonArt.Play(art.clang, 0.7f, 0.7f);
            BoonArt.Play(art.bigBoom, 0.35f, 1.3f);
            if (art.groundPound != null) BoonFX.Sheet(art.groundPound, 12, at, 24f, 1f, new Color(1f, 0.75f, 0.5f), null, false, BoonFX.Order + 1, new Vector2(0.5f, 0.2f));
        }
        GroundShock.Spawn(at, 1.6f, BoonFX.Ember, new Color(0.4f, 0.35f, 0.3f), 0.35f);
        FXParticle.Burst(at + Vector3.up * 0.1f, BoonFX.Ember, 16, 2f, 5f, 10f, 0.45f, true);
        ScreenShake.Impulse(0.45f);
        GamepadRumble.Pulse(0.4f, 0.7f, 0.15f);
        TimeSlowController.HitStop(0.05f, 0.06f);
        int n = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(at + Vector3.up * 0.4f, 1.1f))
        {
            BoonFX.Hit(e, damage, "Anvil Drop");
            BoonFX.Stun(e, 1f);
            n++;
        }
    }
}

// ================================================================================================ The Sun God
public static class SunFX
{
    public static void BlindingFlash(Vector3 center, float seconds)
    {
        ScreenFlash.Play(new Color(1f, 0.98f, 0.85f, 0.55f), 0.35f);
        PulseRing.Spawn(center, new Color(1f, 0.95f, 0.7f, 1f), 3.5f, 0.35f);
        PulseRing.Spawn(center, new Color(1f, 1f, 1f, 0.9f), 2f, 0.25f);
        FXParticle.Burst(center, BoonFX.Sunny, 20, 2f, 6f, 0f, 0.45f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sunBeam : null, 0.35f, 1.6f);
        int n = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(center, 3.5f))
        {
            BoonFX.Stun(e, seconds);
            n++;
        }
    }

    // Solar Flare: a pillar of sunlight slams down on the target
    public static void SunPillar(Vector3 at, float damage)
    {
        Vector3 ground = at + Vector3.down * 0.5f;
        if (SolidGround.Ray(at + Vector3.up * 0.2f, Vector2.down, 3f, out RaycastHit2D hit)) ground = hit.point;
        SunBeam.Spawn(ground, damage);
    }

    public static void Sunspot(Vector3 feet, float dps) => SunPool.Spawn(feet, dps);
}

public class SunBeam : MonoBehaviour
{
    private SpriteRenderer beam, core;
    private float age, damage;
    private bool struck, shaped;
    private const float Life = 0.5f, Height = 7f;

    public static void Spawn(Vector3 ground, float damage)
    {
        var go = new GameObject("Sun Beam");
        go.transform.position = ground;
        var b = go.AddComponent<SunBeam>();
        b.damage = damage;
        // per real pixel (PixelShape) when possible: the old 9 px shaft stretched 7 units tall smeared into blocks
        b.beam = PixelShape.Make("Beam", PixelShape.Kind.Shaft, ground, BoonFX.Order + 2, go.transform);
        b.core = PixelShape.Make("Core", PixelShape.Kind.Shaft, ground, BoonFX.Order + 3, go.transform);
        b.shaped = b.beam != null && b.core != null;
        if (!b.shaped)
        {
            if (b.beam != null) Destroy(b.beam.gameObject);
            if (b.core != null) Destroy(b.core.gameObject);
            b.beam = BoonFX.MakeRenderer("Beam", MoreSprites.Shaft, ground, BoonFX.Order + 2, go.transform);
            b.core = BoonFX.MakeRenderer("Core", MoreSprites.Shaft, ground, BoonFX.Order + 3, go.transform);
        }
        b.beam.color = new Color(1f, 0.8f, 0.3f, 0f);
        b.core.color = new Color(1f, 1f, 0.95f, 0f);
        BoonArt art = BoonArt.Get;
        if (art != null && art.magicCircle != null)
        {
            SheetFX sigil = BoonFX.Sheet(art.magicCircle, 17, ground + Vector3.up * 0.05f, 30f, 0.45f, new Color(1f, 0.85f, 0.4f, 0.9f), null, false, BoonFX.Order - 1);
            if (sigil != null) sigil.transform.localScale = new Vector3(0.6f, 0.2f, 1f);
        }
        BoonArt.Play(art != null ? art.sunBeam : null, 0.45f, 1.15f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float k = age / Life;
        if (k >= 1f) { Destroy(gameObject); return; }
        float width = k < 0.25f ? Mathf.Lerp(0.2f, 1.4f, k / 0.25f) : Mathf.Lerp(1.4f, 0.1f, (k - 0.25f) / 0.75f);
        float alpha = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
        if (shaped)
        {
            PixelShape.Size(beam, width * 5f * 9f / 64f, Height);
            PixelShape.Size(core, width * 2f * 9f / 64f, Height);
        }
        else
        {
            beam.transform.localScale = new Vector3(width * 5f, Height * 64f / 32f, 1f);
            core.transform.localScale = new Vector3(width * 2f, Height * 64f / 32f, 1f);
        }
        beam.color = new Color(1f, 0.8f, 0.3f, 0.75f * alpha);
        core.color = new Color(1f, 1f, 0.95f, alpha);
        if (Random.value < 0.6f) FXParticle.Burst(transform.position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0f, 2.5f), 0f), BoonFX.Sunny, 1, 0.2f, 0.6f, -3f, 0.5f);
        if (struck || k < 0.15f) return;
        struck = true;
        GroundShock.Spawn(transform.position, 1.4f, BoonFX.Sunny, new Color(0.8f, 0.6f, 0.3f), 0.35f);
        FXParticle.Burst(transform.position + Vector3.up * 0.1f, BoonFX.Sunny, 16, 2f, 5f, 4f, 0.5f, true);
        ScreenShake.Impulse(0.3f);
        foreach (EnemyHealth e in BoonFX.EnemiesInBox(transform.position + Vector3.up * 1.5f, new Vector2(1.1f, 3f)))
        {
            BoonFX.Hit(e, damage, "Solar Flare");
            BoonDot.Burn(e, damage * 0.15f, 2f);
        }
    }
}

public class SunPool : MonoBehaviour
{
    private SpriteRenderer pool;
    private float age, dps, tick;
    private const float Life = 3f, Width = 2.4f;

    public static void Spawn(Vector3 feet, float dps)
    {
        Vector3 ground = feet;
        if (SolidGround.Ray(feet + Vector3.up * 0.3f, Vector2.down, 2f, out RaycastHit2D hit)) ground = hit.point;
        var go = new GameObject("Sunspot");
        go.transform.position = ground;
        var s = go.AddComponent<SunPool>();
        s.dps = dps;
        s.pool = PixelShape.Make("Pool", PixelShape.Kind.Pool, ground + Vector3.up * 2f / 64f, BoonFX.Order - 2, go.transform);
        if (s.pool != null) PixelShape.Size(s.pool, Width, 8f / 64f); // 8 px tall at its real pixel size
        else
        {
            s.pool = BoonFX.MakeRenderer("Pool", MoreSprites.Pool, ground + Vector3.up * 2f / 64f, BoonFX.Order - 2, go.transform);
            s.pool.transform.localScale = new Vector3(Width * 64f / 48f, 1f, 1f);
        }
        s.pool.color = new Color(1f, 0.85f, 0.35f, 0f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sunBeam : null, 0.25f, 1.8f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Life) { Destroy(gameObject); return; }
        float a = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((Life - age) / 0.4f);
        pool.color = new Color(1f, 0.85f, 0.35f, 0.8f * a * (0.85f + 0.15f * Mathf.Sin(age * 14f)));
        if (Random.value < 0.5f) FXParticle.Burst(transform.position + new Vector3(Random.Range(-Width / 2f, Width / 2f), 0.05f, 0f), BoonFX.Sunny, 1, 0.1f, 0.3f, -2f, 0.7f);
        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 0.5f;
        foreach (EnemyHealth e in BoonFX.EnemiesInBox(transform.position + Vector3.up * 0.4f, new Vector2(Width, 0.8f))) BoonDot.Burn(e, dps, 1.2f);
        Vector3 r = BoonRunner.RowdyFeet;
        if (BoonRunner.Rowdy != null && Mathf.Abs(r.x - transform.position.x) < Width / 2f && Mathf.Abs(r.y - transform.position.y) < 0.5f && BoonRunner.Rowdy.TryGetComponent(out Health h))
        {
            h.AddHealth(1f, false);
            FXParticle.Burst(BoonRunner.RowdyCenter, BoonFX.Sunny, 2, 0.2f, 0.6f, -2f, 0.5f);
        }
    }
}

// Halo: three little suns circling Rowdy
public class SunHalo : MonoBehaviour
{
    private static SunHalo instance;
    private readonly SpriteRenderer[] suns = new SpriteRenderer[3];
    private readonly Dictionary<EnemyHealth, float> lastHit = new Dictionary<EnemyHealth, float>();
    private float angle;

    public static void Keep(bool on, Transform rowdy)
    {
        if (on && instance == null && rowdy != null)
        {
            var go = new GameObject("Sun Halo");
            instance = go.AddComponent<SunHalo>();
            for (int i = 0; i < 3; i++) instance.suns[i] = BoonFX.MakeRenderer("Sun", MoreSprites.SunMote, rowdy.position, BoonFX.Order + 3, go.transform);
        }
        else if (!on && instance != null) { Destroy(instance.gameObject); instance = null; }
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void LateUpdate()
    {
        if (BoonRunner.Rowdy == null) { Destroy(gameObject); return; }
        if (!PauseMenu.IsPaused) angle += Time.deltaTime * 2.6f;
        Vector3 c = BoonRunner.RowdyCenter;
        for (int i = 0; i < 3; i++)
        {
            float a = angle + i * Mathf.PI * 2f / 3f;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * 0.85f, Mathf.Sin(a) * 0.55f, 0f);
            p = new Vector3(Mathf.Round(p.x * 64f) / 64f, Mathf.Round(p.y * 64f) / 64f, 0f); // whole pixels
            suns[i].transform.position = p;
            suns[i].sortingOrder = Mathf.Sin(a) > 0f ? BoonFX.Order - 6 : BoonFX.Order + 3; // behind him on the far side
            if (PauseMenu.IsPaused) continue;
            if (Time.frameCount % 3 == i) FXParticle.Burst(p, BoonFX.Sunny, 1, 0.05f, 0.15f, 0f, 0.25f);
            foreach (EnemyHealth e in BoonFX.EnemiesIn(p, 0.3f))
            {
                if (lastHit.TryGetValue(e, out float t) && Time.time - t < 0.5f) continue;
                lastHit[e] = Time.time;
                BoonFX.Hit(e, Boons.V("halo", 0), "Halo");
                FXParticle.Burst(p, BoonFX.Sunny, 5, 1f, 2.5f, 2f, 0.3f);
            }
        }
        if (lastHit.Count > 60) lastHit.Clear();
    }
}
