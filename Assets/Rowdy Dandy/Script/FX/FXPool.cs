using System.Collections.Generic;
using UnityEngine;

// One manager for the small throwaway effects that used to be a GameObject + Update each (the frame rate killer
// when a fight got busy): FX pixels (FXParticle.Burst), blood drops (Blood.Spill) and afterimages (CatFX.Afterimage).
// Renderers are recycled, everything moves in one loop, and each kind has a cap (the oldest go first).
public class FXPool : MonoBehaviour
{
    private const int MaxPixels = 700, MaxDrops = 220, MaxGhosts = 90;

    private struct Pixel
    {
        public SpriteRenderer sr;
        public Transform tf;
        public Vector2 pos, vel;
        public float gravity, life, age;
        public Color color;
    }

    private struct Drop
    {
        public SpriteRenderer sr;
        public Transform tf;
        public Vector2 pos, vel;
        public float age;
    }

    private struct Ghost
    {
        public SpriteRenderer sr;
        public float life, age, alpha;
    }

    private static FXPool instance;
    private readonly List<Pixel> pixels = new List<Pixel>(MaxPixels);
    private readonly List<Drop> drops = new List<Drop>(MaxDrops);
    private readonly List<Ghost> ghosts = new List<Ghost>(MaxGhosts);
    private readonly Stack<SpriteRenderer> free = new Stack<SpriteRenderer>();

    private static FXPool Get
    {
        get
        {
            if (instance != null) return instance;
            var go = new GameObject("FX Pool (auto)");
            instance = go.AddComponent<FXPool>(); // lives in the scene: a reload clears everything with it
            return instance;
        }
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private SpriteRenderer Take(string name, Sprite sprite, int order, Material material)
    {
        SpriteRenderer sr = null;
        while (free.Count > 0 && sr == null) sr = free.Pop();
        if (sr == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
        }
        sr.gameObject.SetActive(true);
        sr.sprite = sprite;
        sr.sortingOrder = order;
        sr.flipX = sr.flipY = false;
        if (material != null) sr.sharedMaterial = material;
        sr.transform.localScale = Vector3.one;
        sr.transform.rotation = Quaternion.identity;
        return sr;
    }

    private void Give(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.gameObject.SetActive(false);
        free.Push(sr);
    }

    // ---------------------------------------------------------------- FX pixels
    public static void Pixels(Vector3 at, Color color, int count, float speedMin, float speedMax, float gravity, float life, bool upwards)
    {
        if (count <= 0) return;
        FXPool p = Get;
        for (int i = 0; i < count; i++)
        {
            if (p.pixels.Count >= MaxPixels) { p.Give(p.pixels[0].sr); p.pixels.RemoveAt(0); }
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            if (upwards) dir = new Vector2(dir.x, Mathf.Abs(dir.y) * 0.8f + 0.2f).normalized;
            SpriteRenderer sr = p.Take("FX Pixel", Blood.DropSprite, 96, CatFX.Unlit);
            if (Random.value < 0.35f) sr.transform.localScale = Vector3.one * 1.5f;
            var px = new Pixel
            {
                sr = sr, tf = sr.transform, pos = at,
                vel = dir * Random.Range(speedMin, speedMax),
                gravity = gravity,
                life = life * Random.Range(0.7f, 1.2f),
                color = Color.Lerp(color, Color.white, Random.Range(0f, 0.3f)),
            };
            sr.color = px.color;
            px.tf.position = at;
            p.pixels.Add(px);
        }
    }

    // ---------------------------------------------------------------- blood drops (fall, splat, leave a pool)
    public static void BloodDrop(Vector3 at, Vector2 velocity)
    {
        FXPool p = Get;
        if (p.drops.Count >= MaxDrops) { p.Give(p.drops[0].sr); p.drops.RemoveAt(0); }
        SpriteRenderer sr = p.Take("Blood Drop", Blood.DropSprite, 74, ItemArt.Lit);
        sr.color = Random.value < 0.5f ? Blood.Bright : Blood.Dark;
        if (Random.value < 0.4f) sr.transform.localScale = Vector3.one * 1.5f;
        sr.transform.position = at;
        p.drops.Add(new Drop { sr = sr, tf = sr.transform, pos = at, vel = velocity });
    }

    // ---------------------------------------------------------------- afterimages (flat colour copy that fades)
    public static void Afterimage(SpriteRenderer source, Color color, float life)
    {
        FXPool p = Get;
        if (p.ghosts.Count >= MaxGhosts) { p.Give(p.ghosts[0].sr); p.ghosts.RemoveAt(0); }
        SpriteRenderer sr = p.Take("Afterimage", source.sprite, source.sortingOrder - 1, CatFX.Silhouette);
        sr.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        sr.transform.localScale = source.transform.lossyScale;
        sr.flipX = source.flipX;
        sr.sortingLayerID = source.sortingLayerID;
        sr.color = color;
        p.ghosts.Add(new Ghost { sr = sr, life = Mathf.Max(0.01f, life), alpha = color.a });
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Pixel px = pixels[i];
            px.age += dt;
            if (px.age >= px.life || px.sr == null) { Give(px.sr); pixels.RemoveAt(i); continue; }
            px.vel.y -= px.gravity * dt;
            px.vel *= 1f - 1.5f * dt;
            px.pos += px.vel * dt;
            px.tf.position = px.pos;
            px.sr.color = new Color(px.color.r, px.color.g, px.color.b, px.color.a * (1f - px.age / px.life));
            pixels[i] = px;
        }

        for (int i = drops.Count - 1; i >= 0; i--)
        {
            Drop d = drops[i];
            d.age += dt;
            if (d.age > 2.5f || d.sr == null) { Give(d.sr); drops.RemoveAt(i); continue; }
            d.vel.y -= 14f * dt;
            Vector2 step = d.vel * dt;
            if (d.vel.y < 0f && SolidGround.Ray(d.pos, step.normalized, step.magnitude + 0.01f, out RaycastHit2D hit) && hit.normal.y > 0.5f)
            {
                BloodPool.Spawn(hit.point, hit.collider, d.sr.color, hit.normal);
                Give(d.sr);
                drops.RemoveAt(i);
                continue;
            }
            d.pos += step;
            d.tf.position = d.pos;
            drops[i] = d;
        }

        float udt = Time.unscaledDeltaTime;
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            Ghost g = ghosts[i];
            g.age += udt;
            if (g.age >= g.life || g.sr == null) { Give(g.sr); ghosts.RemoveAt(i); continue; }
            Color c = g.sr.color;
            c.a = g.alpha * (1f - g.age / g.life);
            g.sr.color = c;
            ghosts[i] = g;
        }
    }
}
