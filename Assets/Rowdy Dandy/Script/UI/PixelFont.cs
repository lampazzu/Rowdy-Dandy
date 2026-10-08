using System.Collections.Generic;
using UnityEngine;

// The game's 5x7 pixel font (uppercase only, lowercase is converted), rasterized into small point-filtered textures.
// Used by PixelText (menus / UI) and FloatingDamageText (damage numbers, CRITICAL!, COUNTER! ...).
// Ink is white so the caller can tint it with an Image / SpriteRenderer color; the edge pixels stay dark under any tint.
public static class PixelFont
{
    public enum Edge { DropShadow, Outline }

    public const int GlyphW = 5, GlyphH = 7, Spacing = 1, LineSpacing = 2;

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
        // PlayStation face buttons (GameInput.Cross / Circle / Square / Triangle)
        ['✕'] = new[] { ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "....." },
        ['○'] = new[] { ".....", ".###.", "#...#", "#...#", "#...#", ".###.", "....." },
        ['□'] = new[] { ".....", "#####", "#...#", "#...#", "#...#", "#####", "....." },
        ['△'] = new[] { ".....", "..#..", ".#.#.", ".#.#.", "#...#", "#####", "....." },
    };

    private static readonly Color32 Ink = new Color32(255, 255, 255, 255);
    private static readonly Color32 ShadowColor = new Color32(20, 6, 26, 220);
    private static readonly Color32 OutlineColor = new Color32(20, 6, 26, 255);

    // The 5x7 glyph for a character ('#' = ink), '?' if the font doesn't have it
    public static string[] Glyph(char c) => Font.TryGetValue(char.ToUpperInvariant(c), out string[] g) ? g : Font['?'];

    public static bool HasIcons(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s) if (ButtonIcons.IsIcon(c)) return true;
        return false;
    }

    // How many letters wide a text is (button icons count as several), for word wrapping
    public static int CharLength(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int px = 0;
        foreach (char c in s) px += Advance(c) + Spacing;
        return Mathf.CeilToInt(px / (float)(GlyphW + Spacing));
    }

    private static int Advance(char c) => ButtonIcons.IsIcon(c) ? ButtonIcons.Width(c) : GlyphW;

    // Draws text into `texture` (created or resized as needed). Lines split on '\n' are centered.
    // Button icons (ButtonIcons characters) are drawn inline in their own colours, centered on the line, which grows
    // to fit them. `ink` colours the letters (white by default, so a UI tint can colour them instead).
    // Returns the texture; its size is the text's size in pixels.
    public static Texture2D Render(string content, Edge edge, Texture2D texture = null, Color32? ink = null)
    {
        string[] lines = (content ?? "").ToUpperInvariant().Replace("\r", "").Split('\n');
        Color32 inkColor = ink ?? Ink;

        int pad = edge == Edge.Outline ? 1 : 0;  // outline grows the text 1px on every side
        int extra = edge == Edge.Outline ? 2 : 1; // drop shadow adds 1px right/bottom
        var lineW = new int[lines.Length];
        var lineH = new int[lines.Length];
        int maxW = GlyphW, totalH = 0;
        for (int l = 0; l < lines.Length; l++)
        {
            int w = 0, h = GlyphH;
            for (int i = 0; i < lines[l].Length; i++)
            {
                char c = lines[l][i];
                w += Advance(c) + (i > 0 ? Spacing : 0);
                if (ButtonIcons.IsIcon(c)) h = Mathf.Max(h, ButtonIcons.Height);
            }
            lineW[l] = w;
            lineH[l] = h;
            maxW = Mathf.Max(maxW, w);
            totalH += h + (l > 0 ? LineSpacing : 0);
        }
        int width = maxW + extra;
        int height = totalH + extra;

        if (texture == null || texture.width != width || texture.height != height)
        {
            if (texture != null) Object.Destroy(texture);
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PixelText" };
        }

        // Ink mask + icon pixels first (y = 0 is the top row), then edges around the ink
        var inked = new bool[width, height];
        var icons = new Color32[width, height];
        int y0 = pad;
        for (int l = 0; l < lines.Length; l++)
        {
            string line = lines[l];
            int x = pad + (maxW - lineW[l]) / 2;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ButtonIcons.Pixels(ch, out Color32[] px, out int iw, out int ih))
                {
                    int iy = y0 + (lineH[l] - ih) / 2;
                    for (int gy = 0; gy < ih; gy++)
                        for (int gx = 0; gx < iw; gx++)
                        {
                            Color32 p = px[gy * iw + gx];
                            if (p.a > 0 && x + gx < width && iy + gy < height) icons[x + gx, iy + gy] = p;
                        }
                    x += iw + Spacing;
                    continue;
                }
                string[] glyph = Glyph(ch);
                int gyStart = y0 + (lineH[l] - GlyphH) / 2;
                for (int gy = 0; gy < GlyphH; gy++)
                    for (int gx = 0; gx < GlyphW; gx++)
                        if (glyph[gy][gx] == '#') inked[x + gx, gyStart + gy] = true;
                x += GlyphW + Spacing;
            }
            y0 += lineH[l] + LineSpacing;
        }

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color32 c;
                if (icons[x, y].a > 0) c = icons[x, y];
                else if (inked[x, y]) c = inkColor;
                else if (edge == Edge.DropShadow) c = x > 0 && y > 0 && inked[x - 1, y - 1] ? ShadowColor : default;
                else c = TouchesInk(inked, x, y, width, height) ? OutlineColor : default;
                pixels[(height - 1 - y) * width + x] = c; // texture rows start at the bottom
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }
    private static bool TouchesInk(bool[,] ink, int x, int y, int width, int height)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < width && ny < height && ink[nx, ny]) return true;
            }
        }
        return false;
    }
}
