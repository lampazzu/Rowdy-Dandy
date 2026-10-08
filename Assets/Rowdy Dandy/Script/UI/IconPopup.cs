using UnityEngine;

// World-space "[icon] WORD" popup in the pixel font: pops in, floats up, fades. Used for weapon REPAIRED / BROKEN.
//   IconPopup.Show(position, weaponIcon, "REPAIRED", color);
public class IconPopup : MonoBehaviour
{
    private const float PixelsPerUnit = 64f;

    private static Material unlitMaterial;

    private SpriteRenderer iconRenderer, textRenderer;
    private Texture2D textTexture;
    private Sprite textSprite;
    private Color color;
    private float age;
    private float baseScale;
    private float Lifetime = 1.35f;

    public static IconPopup Show(Vector3 position, Sprite icon, string text, Color color, float size = 1f, float lifetime = 1.35f)
    {
        var go = new GameObject("IconPopup " + text);
        go.transform.position = new Vector3(position.x, position.y, -1f);
        var popup = go.AddComponent<IconPopup>();
        popup.Lifetime = Mathf.Max(0.3f, lifetime);
        popup.Build(icon, text, color, size);
        return popup;
    }

    private void Build(Sprite icon, string text, Color tint, float size)
    {
        color = tint;
        baseScale = 0.75f * size * GameSettings.MessageSize;

        textTexture = PixelFont.Render(text, PixelFont.Edge.Outline);
        textSprite = Sprite.Create(textTexture, new Rect(0, 0, textTexture.width, textTexture.height), new Vector2(0f, 0.5f), PixelsPerUnit);

        float iconWidth = 0f;
        if (icon != null)
        {
            iconRenderer = MakeRenderer("Icon", icon);
            // weapon icons are 16 px at their own PPU; show them ~12 font pixels tall
            float h = icon.bounds.size.y;
            float s = h > 0f ? (12f / PixelsPerUnit) / h : 1f;
            iconRenderer.transform.localScale = Vector3.one * s;
            iconWidth = icon.bounds.size.x * s + 3f / PixelsPerUnit;
        }
        textRenderer = MakeRenderer("Text", textSprite);

        // Center the pair: [icon] [text]
        float textWidth = textTexture.width / PixelsPerUnit;
        float total = iconWidth + textWidth;
        if (iconRenderer != null) iconRenderer.transform.localPosition = new Vector3(-total / 2f + (iconWidth - 3f / PixelsPerUnit) / 2f, 0f, 0f);
        textRenderer.transform.localPosition = new Vector3(-total / 2f + iconWidth, 0f, 0f);

        transform.localScale = Vector3.one * baseScale * 1.8f;
        Apply(1f);
    }

    private SpriteRenderer MakeRenderer(string name, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 200;
        if (unlitMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) unlitMaterial = new Material(shader) { name = "Icon Popup (Unlit)" };
        }
        if (unlitMaterial != null) sr.sharedMaterial = unlitMaterial;
        return sr;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        if (age >= Lifetime) { Destroy(gameObject); return; }

        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * baseScale, Time.unscaledDeltaTime * 14f);
        float rise = Mathf.Lerp(1.4f, 0.15f, age / Lifetime) * Mathf.Min(1f, 1.35f / Lifetime);
        transform.position += Vector3.up * rise * Time.unscaledDeltaTime;
        Apply(1f - Mathf.Clamp01((age - Lifetime * 0.6f) / (Lifetime * 0.4f)));
    }

    private void Apply(float alpha)
    {
        // a white flash on the first frames, then the tint
        Color c = Color.Lerp(Color.white, color, Mathf.Clamp01(age / 0.12f));
        c.a = alpha;
        if (textRenderer != null) textRenderer.color = c;
        if (iconRenderer != null) iconRenderer.color = new Color(1f, 1f, 1f, alpha);
    }

    private void OnDestroy()
    {
        if (textSprite != null) Destroy(textSprite);
        if (textTexture != null) Destroy(textTexture);
    }
}
