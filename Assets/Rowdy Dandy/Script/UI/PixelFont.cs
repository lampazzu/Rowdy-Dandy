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
    };

    private static readonly Color32 Ink = new Color32(255, 255, 255, 255);
    private static readonly Color32 ShadowColor = new Color32(20, 6, 26, 220);
    private static readonly Color32 OutlineColor = new Color32(20, 6, 26, 255);

    // Draws text into `texture` (created or resized as needed). Lines split on '\n' are centered.
    // Returns the texture; its size is the text's size in pixels.
    public static Texture2D Render(string content, Edge edge, Texture2D texture = null)
    {
        string[] lines = (content ?? "").ToUpperInvariant().Replace("\r", "").Split('\n');

        int pad = edge == Edge.Outline ? 1 : 0;  // outline grows the text 1px on every side
        int extra = edge == Edge.Outline ? 2 : 1; // drop shadow adds 1px right/bottom
        int maxChars = 1;
        foreach (string line in lines) maxChars = Mathf.Max(maxChars, line.Length);
        int width = maxChars * (GlyphW + Spacing) - Spacing + extra;
        int height = lines.Length * (GlyphH + LineSpacing) - LineSpacing + extra;

        if (texture == null || texture.width != width || texture.height != height)
        {
            if (texture != null) Object.Destroy(texture);
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PixelText" };
        }

        // Ink mask first (y = 0 is the top row), then edges around it
        var ink = new bool[width, height];
        for (int l = 0; l < lines.Length; l++)
        {
            string line = lines[l];
            int lineWidth = line.Length * (GlyphW + Spacing) - Spacing;
            int x0 = pad + (maxChars * (GlyphW + Spacing) - Spacing - lineWidth) / 2;
            int y0 = pad + l * (GlyphH + LineSpacing);
            for (int i = 0; i < line.Length; i++)
            {
                if (!Font.TryGetValue(line[i], out string[] glyph)) glyph = Font['?'];
                for (int gy = 0; gy < GlyphH; gy++)
                    for (int gx = 0; gx < GlyphW; gx++)
                        if (glyph[gy][gx] == '#') ink[x0 + i * (GlyphW + Spacing) + gx, y0 + gy] = true;
            }
        }

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color32 c;
                if (ink[x, y]) c = Ink;
                else if (edge == Edge.DropShadow) c = x > 0 && y > 0 && ink[x - 1, y - 1] ? ShadowColor : default;
                else c = TouchesInk(ink, x, y, width, height) ? OutlineColor : default;
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
