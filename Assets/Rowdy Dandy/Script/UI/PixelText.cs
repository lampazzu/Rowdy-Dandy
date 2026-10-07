using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Crisp pixel-font text for UI (5x7 glyphs + a 1px drop shadow), drawn into a small point-filtered texture.
// Matches the HUD's pixel labels but larger, so whole menu lines stay readable. Uppercase only (lowercase is converted).
// Usage: PixelText.Create(parent, "SETTINGS", scale: 6, color, pivotX: 0.5f)  then  .SetText("...")
[RequireComponent(typeof(RawImage))]
public class PixelText : MonoBehaviour
{
    private const int GlyphW = 5, GlyphH = 7, Spacing = 1;

    private static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
    {
        ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
        ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
        ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
        ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
        ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
        ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" },
        ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['I'] = new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
        ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##.." },
        ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
        ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
        ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
        ['N'] = new[] { "#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#" },
        ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
        ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
        ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
        ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
        ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
        ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
        ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#." },
        ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
        ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
        ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
        ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
        ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
        ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
        ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
        ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
        ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
        ['6'] = new[] { ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###." },
        ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
        ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
        ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###." },
        [' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." },
        ['.'] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "..#.." },
        [','] = new[] { ".....", ".....", ".....", ".....", ".....", "..#..", ".#..." },
        [':'] = new[] { ".....", "..#..", "..#..", ".....", "..#..", "..#..", "....." },
        ['!'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." },
        ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
        ['%'] = new[] { "##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##" },
        ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
        ['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
        ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
        ['<'] = new[] { "...#.", "..#..", ".#...", "#....", ".#...", "..#..", "...#." },
        ['>'] = new[] { ".#...", "..#..", "...#.", "....#", "...#.", "..#..", ".#..." },
        ['('] = new[] { "...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#." },
        [')'] = new[] { ".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#..." },
        ['\''] = new[] { "..#..", "..#..", ".....", ".....", ".....", ".....", "....." },
    };

    private static readonly Color32 Ink = new Color32(255, 255, 255, 255);
    private static readonly Color32 Shadow = new Color32(20, 6, 26, 220);

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

        int count = Mathf.Max(1, text.Length);
        int width = count * (GlyphW + Spacing) - Spacing + 1;  // +1 for the drop shadow
        int height = GlyphH + 1;

        if (texture == null || texture.width != width)
        {
            if (texture != null) Destroy(texture);
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PixelText" };
        }

        var pixels = new Color32[width * height];
        for (int pass = 0; pass < 2; pass++) // shadow first, then ink on top
        {
            int offset = pass == 0 ? 1 : 0;
            Color32 color = pass == 0 ? Shadow : Ink;
            for (int i = 0; i < text.Length; i++)
            {
                if (!Font.TryGetValue(text[i], out string[] glyph)) glyph = Font['?'];
                int gx0 = i * (GlyphW + Spacing) + offset;
                for (int gy = 0; gy < GlyphH; gy++)
                {
                    for (int gx = 0; gx < GlyphW; gx++)
                    {
                        if (glyph[gy][gx] != '#') continue;
                        int x = gx0 + gx;
                        int y = height - 1 - (gy + offset); // texture rows start at the bottom
                        pixels[y * width + x] = color;
                    }
                }
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false);

        image.texture = texture;
        Rect.sizeDelta = new Vector2(width * scale, height * scale);
    }

    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }
}
