using System.Collections.Generic;
using UnityEngine;

// Look of the small over-head enemy health bar. Lives in Resources/EnemyHealthBarStyle.asset
[CreateAssetMenu(menuName = "Rowdy Dandy/Enemy Health Bar Style")]
public class EnemyHealthBarStyle : ScriptableObject
{
    [Header("Sprites (all the same pixel size, drawn on top of each other)")]
    public Sprite trailSprite;   // delayed bar that catches up after a hit
    public Sprite fillSprite;    // current health
    public Sprite frameSprite;   // outline / tick marks drawn on top

    [Header("Size & Placement")]
    [Tooltip("1 = native sprite size (76px = ~1.2 units wide).")]
    public float scale = 0.5f;
    [Tooltip("Gap between the top of the enemy and the bar, in world units.")]
    public float heightOffset = 0.12f;
    public int sortingOrder = 9000; // under the damage numbers (10000)

    [Header("Visibility")]
    [Tooltip("Seconds the bar stays up after the last hit before fading out.")]
    public float showTime = 2.5f;
    public float fadeTime = 0.3f;
    [Tooltip("Keep the bar up for as long as the enemy is hurt instead of fading out.")]
    public bool stayWhileDamaged = false;

    [Header("Trail")]
    [Tooltip("Pause before the trail starts draining.")]
    public float trailDelay = 0.3f;
    [Tooltip("How much of the bar the trail drains per second.")]
    public float trailSpeed = 1.2f;

    [Header("Colour by health")]
    [Tooltip("The fill is recoloured from this gradient: right end = full health, left end = nearly dead. Off = the fill sprite's own colours.")]
    public bool colourByHealth = true;
    public Gradient healthColours = DefaultGradient();

    public static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.18f, 0.2f), 0f), new GradientColorKey(new Color(0.85f, 0.3f, 0.95f), 0.45f), new GradientColorKey(new Color(0.3f, 0.6f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}

// Small over-head health bar, created at runtime by EnemyHealth.
// Hidden at full health, pops up when the enemy is hit, fades out after a moment.
// Lives on its own (not parented) so enemy flips/scale keyframes don't mess with it.
public class EnemyHealthBar : MonoBehaviour
{
    private static EnemyHealthBarStyle style;
    private static bool styleMissingWarned;
    private static Material unlitSpriteMaterial;
    // Sprites cropped pixel by pixel (index = visible width in px), so the bar drains without stretching the art
    private static readonly Dictionary<Sprite, Sprite[]> croppedSprites = new Dictionary<Sprite, Sprite[]>();
    // Greyscale copy of the fill (so it can be tinted any colour: blue at full health -> red when nearly dead)
    private static readonly Dictionary<Sprite, Sprite> greySprites = new Dictionary<Sprite, Sprite>();
    private Sprite fillSource;

    private EnemyHealth target;
    private Collider2D targetCollider;
    private SpriteRenderer targetSprite;

    private SpriteRenderer trailRenderer;
    private SpriteRenderer fillRenderer;
    private SpriteRenderer frameRenderer;

    private float lastHealth;
    private float trailRatio = 1f;
    private float trailHold;
    private float visibleTimer;
    private float alpha;
    private float headroom = -1f;

    public static void Attach(EnemyHealth enemy)
    {
        if (style == null)
        {
            style = Resources.Load<EnemyHealthBarStyle>("EnemyHealthBarStyle");
            if (style == null)
            {
                if (!styleMissingWarned) Debug.LogWarning("EnemyHealthBar: Resources/EnemyHealthBarStyle.asset not found, enemy health bars disabled.");
                styleMissingWarned = true;
                return;
            }
        }
        if (style.fillSprite == null) return;

        GameObject bar = new GameObject("HealthBar (" + enemy.name + ")");
        bar.AddComponent<EnemyHealthBar>().Init(enemy);
    }

    private void Init(EnemyHealth enemy)
    {
        target = enemy;
        // Body collider = an enabled solid one (some enemies also carry disabled/trigger extras)
        foreach (Collider2D col in enemy.GetComponents<Collider2D>())
        {
            if (col.enabled && !col.isTrigger) { targetCollider = col; break; }
            if (targetCollider == null) targetCollider = col;
        }
        targetSprite = enemy.GetComponent<SpriteRenderer>();
        lastHealth = enemy.currentenemyHealth;

        transform.localScale = Vector3.one * style.scale;

        trailRenderer = CreateLayer("Trail", style.trailSprite, 0);
        fillSource = style.colourByHealth ? Grey(style.fillSprite) : style.fillSprite;
        fillRenderer = CreateLayer("Fill", fillSource, 1);
        frameRenderer = CreateLayer("Frame", style.frameSprite, 2);

        // Left-pivot sprites start at x = 0, shift everything so the bar is centered
        float width = style.fillSprite.rect.width / style.fillSprite.pixelsPerUnit;
        foreach (Transform child in transform)
            child.localPosition = new Vector3(-width * 0.5f, 0f, 0f);

        ApplyAlpha(0f);
    }

    private SpriteRenderer CreateLayer(string layerName, Sprite sprite, int orderOffset)
    {
        if (sprite == null) return null;

        if (unlitSpriteMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) unlitSpriteMaterial = new Material(shader) { name = "Enemy Health Bar (Unlit)" };
        }

        GameObject layer = new GameObject(layerName);
        layer.transform.SetParent(transform, false);

        SpriteRenderer sr = layer.AddComponent<SpriteRenderer>();
        sr.sprite = GetCropped(sprite, Mathf.RoundToInt(sprite.rect.width));
        sr.sortingOrder = style.sortingOrder + orderOffset;
        if (unlitSpriteMaterial != null) sr.sharedMaterial = unlitSpriteMaterial; // readable in dark areas, ignores 2D lights
        return sr;
    }

    private static Sprite GetCropped(Sprite source, int pixels)
    {
        int width = Mathf.RoundToInt(source.rect.width);
        pixels = Mathf.Clamp(pixels, 0, width);

        if (!croppedSprites.TryGetValue(source, out Sprite[] crops))
        {
            crops = new Sprite[width + 1];
            croppedSprites[source] = crops;
        }

        if (pixels == 0) return null;

        if (crops[pixels] == null)
        {
            Rect r = source.rect;
            crops[pixels] = Sprite.Create(source.texture, new Rect(r.x, r.y, pixels, r.height),
                new Vector2(0f, 0.5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            crops[pixels].name = source.name + "_" + pixels;
        }
        return crops[pixels];
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        float maxHealth = Mathf.Max(0.0001f, target.startingenemyHealth);
        float health = Mathf.Max(0f, target.currentenemyHealth);
        float ratio = Mathf.Clamp01(health / maxHealth);
        bool alive = target.isActiveAndEnabled && !target.enemydead && health > 0f;

        if (health < lastHealth - 0.001f)
        {
            visibleTimer = style.showTime;
            trailHold = style.trailDelay;
        }
        else if (health > lastHealth)
        {
            trailRatio = ratio; // healed / revived: no trail
        }
        lastHealth = health;

        // Trail waits a moment, then drains down to the real value
        if (trailHold > 0f) trailHold -= Time.deltaTime;
        else trailRatio = Mathf.MoveTowards(trailRatio, ratio, style.trailSpeed * Time.deltaTime);
        trailRatio = Mathf.Max(trailRatio, ratio);

        visibleTimer -= Time.deltaTime;
        bool show = alive && ratio < 1f && (visibleTimer > 0f || style.stayWhileDamaged);
        float fadeStep = style.fadeTime > 0f ? Time.deltaTime / style.fadeTime : 1f;
        alpha = show ? 1f : Mathf.MoveTowards(alpha, 0f, fadeStep);
        if (!alive) alpha = 0f;

        ApplyAlpha(alpha);
        if (alpha <= 0f) return;

        SetFill(fillRenderer, fillSource, ratio);
        if (style.colourByHealth && fillRenderer != null)
        {
            Color c = (style.healthColours ?? EnemyHealthBarStyle.DefaultGradient()).Evaluate(ratio);
            c.a = alpha;
            fillRenderer.color = c;
        }
        SetFill(trailRenderer, style.trailSprite, trailRatio);
        FollowTarget();
    }

    private static void SetFill(SpriteRenderer sr, Sprite source, float ratio)
    {
        if (sr == null || source == null) return;
        int pixels = Mathf.CeilToInt(ratio * source.rect.width - 0.001f); // any damage left shows at least 1px
        sr.sprite = GetCropped(source, pixels);
    }

    // Luminance only, stretched so the brightest pixel is white (keeps the art's shading, loses its hue)
    private static Sprite Grey(Sprite source)
    {
        if (source == null) return null;
        if (greySprites.TryGetValue(source, out Sprite cached)) return cached;
        Sprite result = source;
        try
        {
            Rect r = source.rect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            RenderTexture rt = RenderTexture.GetTemporary(source.texture.width, source.texture.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active; // (before Blit, which makes rt the active one)
            Graphics.Blit(source.texture, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = source.name + "_Grey" };
            tex.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color32[] px = tex.GetPixels32();
            float max = 0.01f;
            foreach (Color32 p in px) if (p.a > 0) max = Mathf.Max(max, Lum(p));
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(Lum(px[i]) / max * 255f), 0, 255);
                px[i] = new Color32(v, v, v, px[i].a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            result = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            result.name = source.name + "_Grey";
        }
        catch (System.Exception) { result = source; }
        greySprites[source] = result;
        return result;
    }

    private static float Lum(Color32 p) => (0.3f * p.r + 0.59f * p.g + 0.11f * p.b) / 255f;

    private void FollowTarget()
    {
        Bounds bounds;
        if (targetCollider != null && targetCollider.enabled) bounds = targetCollider.bounds;
        else if (targetSprite != null) bounds = targetSprite.bounds;
        else bounds = new Bounds(target.transform.position, Vector3.zero);

        // Measured once, so the bar doesn't bob with every animation frame.
        // Uses the body collider: enemy sprites sit on big padded canvases, so their bounds float way above the head
        if (headroom < 0f)
            headroom = Mathf.Max(0f, bounds.max.y - target.transform.position.y);

        transform.position = new Vector3(bounds.center.x, target.transform.position.y + headroom + style.heightOffset, target.transform.position.z);
    }

    private void ApplyAlpha(float a)
    {
        SetAlpha(trailRenderer, a);
        SetAlpha(fillRenderer, a);
        SetAlpha(frameRenderer, a);
    }

    private static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
        sr.enabled = a > 0f;
    }
}
