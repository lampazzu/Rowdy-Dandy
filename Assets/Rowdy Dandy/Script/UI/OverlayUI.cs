using UnityEngine;
using UnityEngine.UI;

// Shared screen-space canvas for the runtime-built overlays (kill feed, FPS counter, run stats, level up text),
// plus the small UI helpers they use. Same look as the pause menu: 1920x1080 reference, pixel text, 9-slice panel.
public static class OverlayUI
{
    private static Canvas canvas;
    private static Sprite panelSprite;
    private static Sprite whiteSprite;

    public static float SlicedMultiplier { get; private set; } = 0.390625f;

    // Above the HUD, below the pause menu (500)
    public static RectTransform Root
    {
        get
        {
            if (canvas == null)
            {
                var go = new GameObject("Overlay Canvas (auto)", typeof(Canvas), typeof(CanvasScaler));
                Object.DontDestroyOnLoad(go);
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 400;
                canvas.pixelPerfect = true;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 1f;
                SlicedMultiplier = scaler.referencePixelsPerUnit / (64f * 4f);
            }
            return (RectTransform)canvas.transform;
        }
    }

    public static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image MakeImage(string name, Transform parent, Color color, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    public static Image MakePanel(string name, Transform parent)
    {
        Image panel = MakeImage(name, parent, Color.white, PanelSprite);
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = SlicedMultiplier;
        return panel;
    }

    // Anchor + pivot at the same corner, position in UI pixels from it
    public static void Place(RectTransform rect, Vector2 corner, Vector2 position, Vector2? size = null)
    {
        rect.anchorMin = rect.anchorMax = corner;
        rect.pivot = corner;
        rect.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
        if (size.HasValue) rect.sizeDelta = size.Value;
    }

    public static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            return whiteSprite;
        }
    }

    // Same pixel frame as the HUD / pause menu (12x12, 4px border)
    public static Sprite PanelSprite
    {
        get
        {
            if (panelSprite != null) return panelSprite;
            var tex = new Texture2D(12, 12, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "OverlayPanel" };
            Color32 outline = new Color32(0x1B, 0x08, 0x20, 0xFF), highlight = new Color32(0xFF, 0x39, 0xC0, 0xFF), border = new Color32(0xB6, 0x0A, 0x7F, 0xFF), fill = new Color32(0x2A, 0x0A, 0x2E, 0xE6), clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[144];
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    int top = 11 - y;
                    int d = Mathf.Min(Mathf.Min(x, 11 - x), Mathf.Min(top, 11 - top));
                    bool corner = (x == 0 || x == 11) && (top == 0 || top == 11);
                    pixels[y * 12 + x] = corner ? clear : d == 0 ? outline : d == 1 ? ((top <= 1 || x <= 1) ? highlight : border) : d == 2 ? outline : fill;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            panelSprite = Sprite.Create(tex, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
            return panelSprite;
        }
    }

    // Small point-filtered sprite from rows of chars ('.' = clear, other chars looked up in the palette)
    public static Sprite PixelSprite(string[] rows, System.Func<char, Color32> palette, string name)
    {
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[(h - 1 - y) * w + x] = rows[y][x] == '.' ? new Color32(0, 0, 0, 0) : palette(rows[y][x]);
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
    }
}
