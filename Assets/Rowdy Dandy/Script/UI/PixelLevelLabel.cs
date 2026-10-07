using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Draws the player level as a pixel-art label plate (same style as the HP / XP / DUR labels), e.g. "LV3".
// PlayerStats keeps writing its level text as before; this reads the number from it, so that text can stay hidden.
// Pops when the level changes.
[RequireComponent(typeof(Image))]
public class PixelLevelLabel : MonoBehaviour
{
    [Tooltip("The text PlayerStats writes the level into (\"Level: 3\"). It can be disabled.")]
    [SerializeField] private TMP_Text source;
    [SerializeField] private float popScale = 0.5f;
    [SerializeField] private float popDuration = 0.35f;

    private const int Width = 15;   // same size as the other label plates
    private const int Height = 8;
    private const float PixelsPerUnit = 64f;

    // 3x5 pixel glyphs, top row first (same font as the generated labels)
    private static readonly string[][] Glyphs =
    {
        new[] { "111", "1.1", "1.1", "1.1", "111" }, // 0
        new[] { ".1.", "11.", ".1.", ".1.", "111" }, // 1
        new[] { "11.", "..1", ".1.", "1..", "111" }, // 2
        new[] { "11.", "..1", ".1.", "..1", "11." }, // 3
        new[] { "1.1", "1.1", "111", "..1", "..1" }, // 4
        new[] { "111", "1..", "11.", "..1", "11." }, // 5
        new[] { ".11", "1..", "111", "1.1", "111" }, // 6
        new[] { "111", "..1", ".1.", ".1.", ".1." }, // 7
        new[] { "111", "1.1", "111", "1.1", "111" }, // 8
        new[] { "111", "1.1", "111", "..1", "11." }, // 9
    };
    private static readonly string[] GlyphL = { "1..", "1..", "1..", "1..", "111" };
    private static readonly string[] GlyphV = { "1.1", "1.1", "1.1", "1.1", ".1." };

    private static readonly Color32 Outline = new Color32(0x1B, 0x08, 0x20, 0xFF);
    private static readonly Color32 Highlight = new Color32(0xFF, 0x39, 0xC0, 0xFF);
    private static readonly Color32 Plate = new Color32(0xB6, 0x0A, 0x7F, 0xFF);
    private static readonly Color32 Shadow = new Color32(0x6E, 0x03, 0x4C, 0xFF);
    private static readonly Color32 Letter = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    private static readonly Regex Number = new Regex(@"\d+");

    private Image image;
    private RectTransform rect;
    private Vector3 baseScale;
    private Texture2D texture;
    private Sprite sprite;
    private int shownLevel = -1;
    private float popTimer;

    private void Awake()
    {
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        baseScale = rect.localScale;
    }

    private void OnDestroy()
    {
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }

    private void LateUpdate()
    {
        if (source != null)
        {
            Match match = Number.Match(source.text ?? "");
            if (match.Success && int.TryParse(match.Value, out int level) && level != shownLevel)
            {
                bool isFirst = shownLevel < 0;
                shownLevel = level;
                Render(level);
                if (!isFirst) popTimer = popDuration;
            }
        }

        if (popTimer > 0f)
        {
            popTimer -= Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(popTimer / Mathf.Max(0.0001f, popDuration));
            rect.localScale = baseScale * (1f + popScale * Mathf.Sin(t * Mathf.PI) * (1f - t * 0.5f));
            if (popTimer <= 0f) rect.localScale = baseScale;
        }
    }

    private void Render(int level)
    {
        // "LV3" fits the plate; two digits become "L12", three just "123"
        var glyphs = new System.Collections.Generic.List<string[]>();
        string digits = level.ToString();
        if (digits.Length == 1) { glyphs.Add(GlyphL); glyphs.Add(GlyphV); }
        else if (digits.Length == 2) { glyphs.Add(GlyphL); }
        foreach (char c in digits) glyphs.Add(Glyphs[c - '0']);
        while (glyphs.Count * 4 - 1 > Width - 2) glyphs.RemoveAt(0);

        if (texture == null)
        {
            texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "LevelLabel"
            };
        }

        var pixels = new Color32[Width * Height];
        for (int y = 0; y < Height; y++) // y = 0 is the top row here
        {
            for (int x = 0; x < Width; x++)
            {
                bool edge = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                bool corner = (x == 0 || x == Width - 1) && (y == 0 || y == Height - 1);
                Color32 c = corner ? new Color32(0, 0, 0, 0)
                    : edge ? Outline
                    : y == 1 ? Highlight
                    : y == Height - 2 ? Shadow
                    : Plate;
                SetPixel(pixels, x, y, c);
            }
        }

        int textWidth = glyphs.Count * 4 - 1;
        int startX = (Width - textWidth) / 2;
        for (int i = 0; i < glyphs.Count; i++)
        {
            for (int gy = 0; gy < 5; gy++)
            {
                for (int gx = 0; gx < 3; gx++)
                {
                    if (glyphs[i][gy][gx] == '1') SetPixel(pixels, startX + i * 4 + gx, 1 + gy, Letter);
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false);

        if (sprite == null)
        {
            sprite = Sprite.Create(texture, new Rect(0, 0, Width, Height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = "LevelLabel";
        }
        image.sprite = sprite;
    }

    // Texture rows start at the bottom, the glyph tables at the top
    private static void SetPixel(Color32[] pixels, int x, int yFromTop, Color32 color)
    {
        if (x < 0 || x >= Width || yFromTop < 0 || yFromTop >= Height) return;
        pixels[(Height - 1 - yFromTop) * Width + x] = color;
    }
}
