using UnityEngine;
using UnityEngine.UI;

// Crisp pixel-font text for UI (PixelFont's 5x7 glyphs + a 1px drop shadow), drawn into a small point-filtered texture.
// Matches the HUD's pixel labels but larger, so whole menu lines stay readable. Uppercase only (lowercase is converted).
// Usage: PixelText.Create(parent, "SETTINGS", scale: 6, color, pivotX: 0.5f)  then  .SetText("...")
// Button icons inside the text (GameInput.Format / ButtonIcons) keep their own colours: the tint is baked into the
// letters instead of the RawImage, so only the alpha is applied on top.
[RequireComponent(typeof(RawImage))]
public class PixelText : MonoBehaviour
{
    private RawImage image;
    private Texture2D texture;
    private string text;
    private int scale = 3;
    private Color tint = Color.white;
    private bool hasIcons;

    public static PixelText Create(Transform parent, string content, int scale, Color color, float pivotX = 0f)
    {
        var go = new GameObject("Pixel Text", typeof(RectTransform), typeof(RawImage), typeof(PixelText));
        go.transform.SetParent(parent, false);
        var pixelText = go.GetComponent<PixelText>();
        pixelText.image = go.GetComponent<RawImage>();
        pixelText.image.raycastTarget = false;
        pixelText.tint = color;
        pixelText.image.color = color;
        pixelText.scale = Mathf.Max(1, scale);
        ((RectTransform)go.transform).pivot = new Vector2(pivotX, 0.5f);
        pixelText.SetText(content);
        return pixelText;
    }

    public Color Color
    {
        get => tint;
        set
        {
            bool rebake = hasIcons && (value.r != tint.r || value.g != tint.g || value.b != tint.b);
            tint = value;
            if (rebake) Bake();
            else ApplyTint();
        }
    }

    private void ApplyTint() => image.color = hasIcons ? new Color(1f, 1f, 1f, tint.a) : tint;

    private void Bake()
    {
        Color32? ink = null;
        if (hasIcons) ink = (Color32)new Color(tint.r, tint.g, tint.b, 1f);
        texture = PixelFont.Render(text, PixelFont.Edge.DropShadow, texture, ink);
        image.texture = texture;
        Rect.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
        ApplyTint();
    }

    public RectTransform Rect => (RectTransform)transform;

    public void SetScale(int newScale)
    {
        scale = Mathf.Max(1, newScale);
        if (texture != null) Rect.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
    }

    public void SetText(string content)
    {
        content = (content ?? "").ToUpperInvariant();
        if (content == text && texture != null) return;
        text = content;
        hasIcons = PixelFont.HasIcons(text);
        Bake();
    }

    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }
}
