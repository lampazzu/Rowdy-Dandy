using System.Collections;
using UnityEngine;

// Runtime visuals for the cats (no prefab setup):
//  - Nick (samurai): blue ninja afterimages + a thin glowing trail while he dashes, a bright slash across every
//    enemy he cuts, a white-blue flash on himself, and a short hit-stop so each cut reads.
//  - Lost cats (not collected yet): LostCatGlow, a pulsing outline so they're easy to spot.
public static class CatFX
{
    public static readonly Color NinjaBlue = new Color(0.35f, 0.75f, 1f, 1f);
    public static readonly Color SlashCore = new Color(0.9f, 0.98f, 1f, 1f);

    private static Material silhouette, unlit;
    private static Sprite slashSprite;

    // Draws a sprite as a flat colour (vertex colour + the sprite's alpha) - the classic "GUI/Text Shader" trick
    public static Material Silhouette
    {
        get
        {
            if (silhouette == null)
            {
                Shader shader = Shader.Find("GUI/Text Shader");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null) silhouette = new Material(shader) { name = "Cat Silhouette" };
            }
            return silhouette;
        }
    }

    public static Material Unlit
    {
        get
        {
            if (unlit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) unlit = new Material(shader) { name = "Cat FX (Unlit)" };
            }
            return unlit;
        }
    }

    // ---------------------------------------------------------------- afterimages
    public static void Afterimage(SpriteRenderer source, Color color, float life = 0.28f)
    {
        if (source == null || source.sprite == null) return;
        var go = new GameObject("Ninja Afterimage");
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = source.transform.lossyScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder - 1;
        if (Silhouette != null) sr.sharedMaterial = Silhouette;
        sr.color = color;
        go.AddComponent<FadeAndDie>().Begin(life, color.a, 1f);
    }

    // Thin additive-looking trail behind the dashing cat (removed with StopTrail)
    public static TrailRenderer StartTrail(Transform cat, SpriteRenderer reference)
    {
        var go = new GameObject("Ninja Trail");
        go.transform.SetParent(cat, false);
        go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.22f;
        trail.minVertexDistance = 0.04f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.16f), new Keyframe(1f, 0f));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(SlashCore, 0f), new GradientColorKey(NinjaBlue, 0.35f), new GradientColorKey(NinjaBlue, 1f) },
                  new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        if (Unlit != null) trail.sharedMaterial = new Material(Unlit) { mainTexture = Texture2D.whiteTexture };
        if (reference != null) { trail.sortingLayerID = reference.sortingLayerID; trail.sortingOrder = reference.sortingOrder - 2; }
        trail.numCapVertices = 2;
        return trail;
    }

    public static void StopTrail(TrailRenderer trail)
    {
        if (trail == null) return;
        trail.transform.SetParent(null, true);
        trail.emitting = false;
        Object.Destroy(trail.gameObject, trail.time + 0.05f);
    }

    // ---------------------------------------------------------------- the cut
    // A bright slash line across the target, a ring of sparks, and Nick flashes
    public static void Slash(Vector3 at, SpriteRenderer cat, int sortingLayer, int sortingOrder)
    {
        float angle = Random.Range(-35f, 35f) + (Random.value < 0.5f ? 0f : 180f);
        for (int layer = 0; layer < 2; layer++)
        {
            var go = new GameObject("Samurai Slash");
            go.transform.position = at + new Vector3(0f, 0.15f, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SlashSprite;
            sr.sortingLayerID = sortingLayer;
            sr.sortingOrder = sortingOrder + 20 + layer;
            if (Unlit != null) sr.sharedMaterial = Unlit;
            Color c = layer == 0 ? NinjaBlue : SlashCore;
            sr.color = c;
            // the outer blue one is thicker
            go.transform.localScale = layer == 0 ? new Vector3(1.15f, 2.6f, 1f) : new Vector3(1.05f, 1f, 1f);
            go.AddComponent<FadeAndDie>().Begin(0.2f, 1f, 1f, stretch: true);
        }

        if (cat != null) Afterimage(cat, new Color(SlashCore.r, SlashCore.g, SlashCore.b, 0.9f), 0.12f); // flash on himself
    }

    // 40 x 3 px line, brightest in the middle
    private static Sprite SlashSprite
    {
        get
        {
            if (slashSprite != null) return slashSprite;
            const int w = 40, h = 3;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "SamuraiSlash" };
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float edge = 1f - Mathf.Abs(x / (float)(w - 1) * 2f - 1f); // 1 in the middle
                for (int y = 0; y < h; y++)
                {
                    bool core = y == 1;
                    float a = core ? Mathf.Clamp01(edge * 2.2f) : Mathf.Clamp01(edge * 1.4f - 0.3f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Round(a * 4f) / 4f * 255));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            slashSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            return slashSprite;
        }
    }

    // Freeze Nick and the enemy for a beat (their animators), plus a tiny global hit-stop
    public static IEnumerator CutFreeze(Animator cat, Animator enemy, float seconds)
    {
        TimeSlowController.HitStop(seconds * 0.6f, 0.05f);
        float catSpeed = cat != null ? cat.speed : 1f, enemySpeed = enemy != null ? enemy.speed : 1f;
        if (cat != null) cat.speed = 0f;
        if (enemy != null) enemy.speed = 0f;
        yield return new WaitForSecondsRealtime(seconds);
        if (cat != null) cat.speed = catSpeed;
        if (enemy != null) enemy.speed = enemySpeed;
    }

    // ---------------------------------------------------------------- helpers
    public class FadeAndDie : MonoBehaviour
    {
        private float life, age, startAlpha, startScale;
        private bool stretch;
        private SpriteRenderer sr;
        private Vector3 baseScale;

        public void Begin(float lifetime, float alpha, float scale, bool stretch = false)
        {
            life = Mathf.Max(0.01f, lifetime);
            startAlpha = alpha;
            startScale = scale;
            this.stretch = stretch;
            sr = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(age / life);
            if (sr != null) { Color c = sr.color; c.a = startAlpha * (1f - k); sr.color = c; }
            if (stretch) transform.localScale = new Vector3(baseScale.x * Mathf.Lerp(0.6f, 1.25f, Mathf.Sqrt(k)), baseScale.y * (1f - k * 0.7f), 1f);
            if (age >= life) Destroy(gameObject);
        }
    }
}

// Pulsing glow outline on a cat that hasn't been found yet (4 flat-colour copies 1px out, behind it).
// Added by PetFollower to every cat; hides itself as soon as the cat is collected.
public class LostCatGlow : MonoBehaviour
{
    private static readonly Color GlowColor = new Color(1f, 0.85f, 0.35f, 1f);
    private const float PixelsPerUnit = 64f;

    private PetFollower pet;
    private SpriteRenderer source;
    private SpriteRenderer[] outline;
    private SpriteRenderer halo;
    private float phase;

    private void Start()
    {
        pet = GetComponent<PetFollower>();
        source = GetComponent<SpriteRenderer>();
        if (source == null) { enabled = false; return; }
        phase = Random.Range(0f, 6f);

        Vector2[] offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        outline = new SpriteRenderer[offsets.Length];
        float px = 1f / (source.sprite != null ? source.sprite.pixelsPerUnit : PixelsPerUnit);
        for (int i = 0; i < offsets.Length; i++)
        {
            var go = new GameObject("Lost Glow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (Vector3)(offsets[i] * px);
            var sr = go.AddComponent<SpriteRenderer>();
            if (CatFX.Silhouette != null) sr.sharedMaterial = CatFX.Silhouette;
            outline[i] = sr;
        }

        // A soft second ring, 2px out, fainter
        var haloGo = new GameObject("Lost Halo");
        haloGo.transform.SetParent(transform, false);
        haloGo.transform.localScale = Vector3.one * 1.12f;
        halo = haloGo.AddComponent<SpriteRenderer>();
        if (CatFX.Silhouette != null) halo.sharedMaterial = CatFX.Silhouette;
    }

    private void LateUpdate()
    {
        if (outline == null) return;
        bool show = pet == null || !pet.IsCollected;
        float pulse = 0.55f + 0.45f * Mathf.Sin((Time.time + phase) * 3.2f);

        foreach (SpriteRenderer sr in outline) Copy(sr, show, new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0.95f * pulse), -1);
        Copy(halo, show, new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0.25f * pulse), -2);
        if (!show) enabled = false;
    }

    private void Copy(SpriteRenderer sr, bool show, Color color, int orderOffset)
    {
        sr.enabled = show && source.enabled;
        if (!sr.enabled) return;
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.flipY = source.flipY;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder + orderOffset;
        sr.color = color;
    }
}
