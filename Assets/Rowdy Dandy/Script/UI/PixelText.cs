using UnityEngine;
using UnityEngine.UI;

// Crisp pixel-font text for UI (PixelFont's 5x7 glyphs + a 1px drop shadow), drawn into a small point-filtered texture.
// Matches the HUD's pixel labels but larger, so whole menu lines stay readable. Uppercase only (lowercase is converted).
// Usage: PixelText.Create(parent, "SETTINGS", scale: 6, color, pivotX: 0.5f)  then  .SetText("...")
[RequireComponent(typeof(RawImage))]
public class PixelText : MonoBehaviour
{
    private RawImage image;
    private Texture2D texture;
    private string text;
    private int scale = 3;

    public static PixelText Create(Transform parent, string content, int scale, Color color, float pivotX = 0f)
    {
        var go = new GameObject("Pixel Text", typeof(RectTransform), typeof(RawImage), typeof(PixelText));
        go.transform.SetParent(parent, false);
        var pixelText = go.GetComponent<PixelText>();
        pixelText.image = go.GetComponent<RawImage>();
        pixelText.image.raycastTarget = false;
        pixelText.image.color = color;
        pixelText.scale = Mathf.Max(1, scale);
        ((RectTransform)go.transform).pivot = new Vector2(pivotX, 0.5f);
        pixelText.SetText(content);
        return pixelText;
    }

    public Color Color
    {
        get => image.color;
        set => image.color = value;
    }

    public RectTransform Rect => (RectTransform)transform;

    public void SetText(string content)
    {
        content = (content ?? "").ToUpperInvariant();
        if (content == text && texture != null) return;
        text = content;

        texture = PixelFont.Render(text, PixelFont.Edge.DropShadow, texture);

        image.texture = texture;
        Rect.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
    }

    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }
}
