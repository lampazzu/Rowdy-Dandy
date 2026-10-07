using System.Collections.Generic;
using UnityEngine;

// Pink pixel-art tag plates like the HUD's HP / XP / DUR labels (8 art pixels tall, outline + highlight + shadow,
// white 3x5 letters), made at runtime for any word: button prompts "L1", "Q", "L2", "SELECT", "TAB"...
// Shown at 4 UI pixels per art pixel, the same as the HUD (HUDOrganizer S = 4).
public static class HudTag
{
    public const int ArtScale = 4;
    public const int Height = 8;

    private static readonly Color32 Outline = new Color32(0x1B, 0x08, 0x20, 0xFF);
    private static readonly Color32 Highlight = new Color32(0xFF, 0x39, 0xC0, 0xFF);
    private static readonly Color32 Plate = new Color32(0xB6, 0x0A, 0x7F, 0xFF);
    private static readonly Color32 Shadow = new Color32(0x6E, 0x03, 0x4C, 0xFF);
    private static readonly Color32 Letter = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

    // 3x5 glyphs, top row first (same style as PixelLevelLabel / the generated labels)
    private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['A'] = new[] { ".1.", "1.1", "111", "1.1", "1.1" },
        ['B'] = new[] { "11.", "1.1", "11.", "1.1", "11." },
        ['C'] = new[] { ".11", "1..", "1..", "1..", ".11" },
        ['D'] = new[] { "11.", "1.1", "1.1", "1.1", "11." },
        ['E'] = new[] { "111", "1..", "11.", "1..", "111" },
        ['F'] = new[] { "111", "1..", "11.", "1..", "1.." },
        ['G'] = new[] { ".11", "1..", "1.1", "1.1", ".11" },
        ['H'] = new[] { "1.1", "1.1", "111", "1.1", "1.1" },
        ['I'] = new[] { "111", ".1.", ".1.", ".1.", "111" },
        ['J'] = new[] { "..1", "..1", "..1", "1.1", ".1." },
        ['K'] = new[] { "1.1", "1.1", "11.", "1.1", "1.1" },
        ['L'] = new[] { "1..", "1..", "1..", "1..", "111" },
        ['M'] = new[] { "1.1", "111", "111", "1.1", "1.1" },
        ['N'] = new[] { "11.", "1.1", "1.1", "1.1", "1.1" },
        ['O'] = new[] { ".1.", "1.1", "1.1", "1.1", ".1." },
        ['P'] = new[] { "11.", "1.1", "11.", "1..", "1.." },
        ['Q'] = new[] { ".1.", "1.1", "1.1", "11.", ".11" },
        ['R'] = new[] { "11.", "1.1", "11.", "1.1", "1.1" },
        ['S'] = new[] { ".11", "1..", ".1.", "..1", "11." },
        ['T'] = new[] { "111", ".1.", ".1.", ".1.", ".1." },
        ['U'] = new[] { "1.1", "1.1", "1.1", "1.1", "111" },
        ['V'] = new[] { "1.1", "1.1", "1.1", "1.1", ".1." },
        ['W'] = new[] { "1.1", "1.1", "111", "111", "1.1" },
        ['X'] = new[] { "1.1", "1.1", ".1.", "1.1", "1.1" },
        ['Y'] = new[] { "1.1", "1.1", ".1.", ".1.", ".1." },
        ['Z'] = new[] { "111", "..1", ".1.", "1..", "111" },
        ['0'] = new[] { "111", "1.1", "1.1", "1.1", "111" },
        ['1'] = new[] { ".1.", "11.", ".1.", ".1.", "111" },
        ['2'] = new[] { "11.", "..1", ".1.", "1..", "111" },
        ['3'] = new[] { "11.", "..1", ".1.", "..1", "11." },
        ['4'] = new[] { "1.1", "1.1", "111", "..1", "..1" },
        ['5'] = new[] { "111", "1..", "11.", "..1", "11." },
        ['6'] = new[] { ".11", "1..", "111", "1.1", "111" },
        ['7'] = new[] { "111", "..1", ".1.", ".1.", ".1." },
        ['8'] = new[] { "111", "1.1", "111", "1.1", "111" },
        ['9'] = new[] { "111", "1.1", "111", "..1", "11." },
        [' '] = new[] { "...", "...", "...", "...", "..." },
    };

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // Size of the tag in UI pixels (at ArtScale)
    public static Vector2 UISize(Sprite tag) => new Vector2(tag.rect.width * ArtScale, tag.rect.height * ArtScale);

    public static Sprite Make(string word)
    {
        word = (word ?? "").ToUpperInvariant();
        if (cache.TryGetValue(word, out Sprite cached) && cached != null) return cached;

        var glyphs = new List<string[]>();
        foreach (char c in word) glyphs.Add(Glyphs.TryGetValue(c, out string[] g) ? g : Glyphs[' ']);
        int textWidth = Mathf.Max(1, glyphs.Count * 4 - 1);
        int width = Mathf.Max(9, textWidth + 6);

        var tex = new Texture2D(width, Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "HudTag_" + word
        };
        var px = new Color32[width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < width; x++)
            {
                bool edge = x == 0 || y == 0 || x == width - 1 || y == Height - 1;
                bool corner = (x == 0 || x == width - 1) && (y == 0 || y == Height - 1);
                Set(px, width, x, y, corner ? new Color32(0, 0, 0, 0) : edge ? Outline : y == 1 ? Highlight : y == Height - 2 ? Shadow : Plate);
            }

        int startX = (width - textWidth) / 2;
        for (int i = 0; i < glyphs.Count; i++)
            for (int gy = 0; gy < 5; gy++)
                for (int gx = 0; gx < 3; gx++)
                    if (glyphs[i][gy][gx] == '1') Set(px, width, startX + i * 4 + gx, 1 + gy, Letter);

        tex.SetPixels32(px);
        tex.Apply(false, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, Height), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = tex.name;
        cache[word] = sprite;
        return sprite;
    }

    private static void Set(Color32[] px, int width, int x, int yFromTop, Color32 c)
    {
        if (x < 0 || x >= width || yFromTop < 0 || yFromTop >= Height) return;
        px[(Height - 1 - yFromTop) * width + x] = c;
    }
}
