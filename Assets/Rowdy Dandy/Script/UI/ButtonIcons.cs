using System.Collections.Generic;
using UnityEngine;

// Pixel-art button icons, drawn at runtime in the HUD's style (11 art pixels tall):
//   Xbox      green A / red B / blue X / yellow Y, LB RB bumpers, LT RT triggers, View / Menu
//   PlayStation  dark buttons with the blue cross, red circle, pink square, green triangle, L1 R1 L2 R2, Share / Options
//   Switch    dark buttons with A B X Y, L R ZL ZR, - +
//   Keyboard  keycaps (E, Q, TAB, SPACE...), the arrow keys, the mouse (left click), and a stick / d-pad for "move" / "down"
//
// Each icon id gets one private-use character, so icons can sit inside any text: PixelFont draws them inline
// (GameInput.Format turns "{JUMP}" into the right one). For Images / SpriteRenderers use Get(id).
// Ids: "XB:A", "PS:CROSS", "SW:ZL", "KB:SPACE", "STICK", "ARROWS", "DPAD:DOWN" (GameInput.IconId builds them).
public static class ButtonIcons
{
    public const int Height = 11;
    public const int UIScale = 3; // UI pixels per art pixel for HUD icons

    private static readonly Color32 Outline = new Color32(0x14, 0x06, 0x1A, 0xFF);
    private static readonly Color32 PadBody = new Color32(58, 52, 70, 255);
    private static readonly Color32 PadShade = new Color32(36, 32, 46, 255);
    private static readonly Color32 PadLight = new Color32(104, 96, 122, 255);
    private static readonly Color32 White = new Color32(245, 242, 250, 255);
    private static readonly Color32 KeyFace = new Color32(225, 220, 235, 255);
    private static readonly Color32 KeySide = new Color32(140, 130, 160, 255);

    // ---------------------------------------------------------------- id <-> character

    private static readonly Dictionary<string, char> charOf = new Dictionary<string, char>();
    private static readonly Dictionary<char, string> idOf = new Dictionary<char, string>();
    private static char next = '';

    public static char Char(string id)
    {
        id = (id ?? "").ToUpperInvariant();
        if (!charOf.TryGetValue(id, out char c))
        {
            c = next++;
            charOf[id] = c;
            idOf[c] = id;
        }
        return c;
    }

    public static string Text(string id) => Char(id).ToString();
    public static bool IsIcon(char c) => idOf.ContainsKey(c);

    // ---------------------------------------------------------------- pixels / sprites

    private class Canvas
    {
        public readonly int w, h;
        public readonly Color32[] px; // row 0 = top
        public Canvas(int w, int h) { this.w = w; this.h = h; px = new Color32[w * h]; }
        public void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c; }
        public void Fill(int x0, int y0, int x1, int y1, Color32 c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, c); }
    }

    private static readonly Dictionary<string, Canvas> canvases = new Dictionary<string, Canvas>();
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    // Top row first. False if c isn't an icon.
    public static bool Pixels(char c, out Color32[] px, out int w, out int h)
    {
        px = null; w = h = 0;
        if (!idOf.TryGetValue(c, out string id)) return false;
        Canvas canvas = CanvasFor(id);
        px = canvas.px; w = canvas.w; h = canvas.h;
        return true;
    }

    public static int Width(char c) => idOf.TryGetValue(c, out string id) ? CanvasFor(id).w : 0;

    public static Sprite Get(string id) => Get(id, new Vector2(0.5f, 0.5f));

    public static Sprite Get(string id, Vector2 pivot)
    {
        id = (id ?? "").ToUpperInvariant();
        string key = id + "|" + pivot.x + "," + pivot.y;
        if (sprites.TryGetValue(key, out Sprite s) && s != null) return s;
        Canvas canvas = CanvasFor(id);
        var tex = new Texture2D(canvas.w, canvas.h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "ButtonIcon_" + id
        };
        var flipped = new Color32[canvas.px.Length];
        for (int y = 0; y < canvas.h; y++)
            for (int x = 0; x < canvas.w; x++)
                flipped[(canvas.h - 1 - y) * canvas.w + x] = canvas.px[y * canvas.w + x];
        tex.SetPixels32(flipped);
        tex.Apply(false, true);
        s = Sprite.Create(tex, new Rect(0, 0, canvas.w, canvas.h), pivot, 64f, 0, SpriteMeshType.FullRect);
        s.name = tex.name;
        s = AIArt.Use("ButtonIcon_" + id + (pivot == new Vector2(0.5f, 0.5f) ? "" : "_pivot" + Mathf.RoundToInt(pivot.x * 10) + Mathf.RoundToInt(pivot.y * 10)), s);
        sprites[key] = s;
        return s;
    }

    // Size of a HUD icon in UI pixels
    public static Vector2 UISize(Sprite icon, float scale = UIScale) => new Vector2(icon.rect.width * scale, icon.rect.height * scale);

    private static Canvas CanvasFor(string id)
    {
        if (canvases.TryGetValue(id, out Canvas c)) return c;
        c = Build(id);
        canvases[id] = c;
        return c;
    }

    // ---------------------------------------------------------------- the icons

    private static Canvas Build(string id)
    {
        int colon = id.IndexOf(':');
        string family = colon >= 0 ? id.Substring(0, colon) : id;
        string name = colon >= 0 ? id.Substring(colon + 1) : "";

        switch (family)
        {
            case "KB": return Keycap(name);
            case "ARROWS": return ArrowKeys();
            case "STICK": return Stick();
            case "DPAD": return DpadDown();
            case "MOUSE": return MouseLeft();
            case "XB":
                switch (name)
                {
                    case "A": return Face(new Color32(92, 206, 92, 255), new Color32(46, 140, 52, 255), "A", White);
                    case "B": return Face(new Color32(236, 74, 74, 255), new Color32(160, 36, 44, 255), "B", White);
                    case "X": return Face(new Color32(74, 150, 240, 255), new Color32(38, 92, 172, 255), "X", White);
                    case "Y": return Face(new Color32(245, 206, 56, 255), new Color32(184, 136, 18, 255), "Y", Outline);
                    case "LB": case "RB": return Bumper(name);
                    case "LT": case "RT": return Trigger(name);
                    case "VIEW": return RoundPicto(false);
                    case "MENU": return RoundPicto(true);
                }
                break;
            case "PS":
                switch (name)
                {
                    case "CROSS": return Face(PadBody, PadShade, GameInput.Cross.ToString(), new Color32(124, 178, 240, 255));
                    case "CIRCLE": return Face(PadBody, PadShade, GameInput.Circle.ToString(), new Color32(255, 106, 106, 255));
                    case "SQUARE": return Face(PadBody, PadShade, GameInput.Square.ToString(), new Color32(240, 150, 216, 255));
                    case "TRIANGLE": return Face(PadBody, PadShade, GameInput.Triangle.ToString(), new Color32(64, 226, 160, 255));
                    case "L1": case "R1": return Bumper(name);
                    case "L2": case "R2": return Trigger(name);
                    default: return Pill(name); // SHARE / CREATE / OPTIONS
                }
            case "SW":
                switch (name)
                {
                    case "L": case "R": return Bumper(name);
                    case "ZL": case "ZR": return Trigger(name);
                    default: return Face(PadBody, PadShade, name, White); // A B X Y - +
                }
        }
        return Keycap(name.Length > 0 ? name : id);
    }

    // Round face button, 11x11, with a 5x7 letter / symbol
    private static Canvas Face(Color32 body, Color32 shade, string glyph, Color32 ink)
    {
        var c = new Canvas(11, 11);
        Color32 light = Color32.Lerp(body, White, 0.35f);
        for (int y = 0; y < 11; y++)
            for (int x = 0; x < 11; x++)
            {
                float dx = x - 5f, dy = y - 5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= 5.45f) c.Set(x, y, Outline);
                if (d <= 4.45f) c.Set(x, y, y >= 8 ? shade : (y <= 2 && x <= 5) ? light : body);
            }
        Text5(c, glyph, 3, 2, ink);
        return c;
    }

    // Shoulder button: wide and short, slanted top corners
    private static Canvas Bumper(string label)
    {
        int w = Text5Width(label) + 6;
        var c = new Canvas(w, 11);
        c.Fill(0, 2, w - 1, 10, Outline);
        c.Fill(1, 3, w - 2, 9, PadBody);
        c.Fill(1, 3, w - 2, 3, PadLight);
        c.Fill(1, 9, w - 2, 9, PadShade);
        Clear(c, 0, 2); Clear(c, 1, 2); Clear(c, 0, 3); Clear(c, w - 1, 2); Clear(c, w - 2, 2); Clear(c, w - 1, 3);
        c.Set(1, 3, Outline); c.Set(w - 2, 3, Outline);
        c.Set(0, 10, default); c.Set(w - 1, 10, default);
        Text5(c, label, 3, 3, White);
        return c;
    }

    // Trigger: full height, rounded top
    private static Canvas Trigger(string label)
    {
        int w = Text5Width(label) + 6;
        var c = new Canvas(w, 11);
        c.Fill(0, 0, w - 1, 10, Outline);
        c.Fill(1, 1, w - 2, 9, PadBody);
        c.Fill(1, 1, w - 2, 2, PadLight);
        for (int i = 0; i < 2; i++) { Clear(c, i, 0); Clear(c, w - 1 - i, 0); }
        Clear(c, 0, 1); Clear(c, w - 1, 1);
        c.Set(1, 1, Outline); c.Set(w - 2, 1, Outline); c.Set(2, 1, Outline); c.Set(w - 3, 1, Outline); c.Set(1, 2, Outline); c.Set(w - 2, 2, Outline);
        c.Set(0, 10, default); c.Set(w - 1, 10, default);
        Text5(c, label, 3, 3, White);
        return c;
    }

    // Small pill with tiny text (PlayStation Share / Create / Options)
    private static Canvas Pill(string label)
    {
        int w = Text3Width(label) + 6;
        var c = new Canvas(w, 11);
        c.Fill(0, 2, w - 1, 10, Outline);
        c.Fill(1, 3, w - 2, 9, PadBody);
        c.Fill(1, 3, w - 2, 3, PadLight);
        Clear(c, 0, 2); Clear(c, 0, 10); Clear(c, w - 1, 2); Clear(c, w - 1, 10);
        Text3(c, label, 3, 4, White);
        return c;
    }

    // Xbox View (two squares) / Menu (three lines) on a round dark button
    private static Canvas RoundPicto(bool menu)
    {
        Canvas c = Face(PadBody, PadShade, " ", White);
        if (menu)
        {
            for (int x = 3; x <= 7; x++) { c.Set(x, 3, White); c.Set(x, 5, White); c.Set(x, 7, White); }
        }
        else
        {
            for (int i = 0; i <= 3; i++)
            {
                c.Set(2 + i, 2, White); c.Set(2 + i, 5, White); c.Set(2, 2 + i, White); c.Set(5, 2 + i, White);
                c.Set(5 + i, 5, White); c.Set(5 + i, 8, White); c.Set(5, 5 + i, White); c.Set(8, 5 + i, White);
            }
        }
        return c;
    }

    // Keycap: one letter in the big font, longer names (TAB, SPACE, SHIFT...) in the small one
    private static Canvas Keycap(string label)
    {
        bool single = label.Length == 1;
        int w = single ? 11 : Mathf.Max(13, Text3Width(label) + 6);
        if (label == "SPACE") w = Mathf.Max(w, 29);
        var c = new Canvas(w, 11);
        c.Fill(0, 0, w - 1, 10, Outline);
        c.Fill(1, 1, w - 2, 7, KeyFace);
        c.Fill(1, 8, w - 2, 9, KeySide);
        Clear(c, 0, 0); Clear(c, w - 1, 0); Clear(c, 0, 10); Clear(c, w - 1, 10);
        if (single) Text5(c, label, 3, 1, Outline);
        else Text3(c, label, (w - Text3Width(label)) / 2, 2, Outline);
        return c;
    }

    // The four arrow keys
    private static Canvas ArrowKeys()
    {
        var c = new Canvas(19, 11);
        void Key(int x0, int y0) // 7 wide, 6 tall
        {
            c.Fill(x0, y0, x0 + 6, y0 + 5, Outline);
            c.Fill(x0 + 1, y0 + 1, x0 + 5, y0 + 3, KeyFace);
            c.Fill(x0 + 1, y0 + 4, x0 + 5, y0 + 4, KeySide);
        }
        Key(6, 0); Key(0, 5); Key(6, 5); Key(12, 5);
        Clear(c, 6, 0); Clear(c, 12, 0); Clear(c, 0, 5); Clear(c, 18, 5); Clear(c, 0, 10); Clear(c, 18, 10);
        // arrows
        c.Set(9, 1, Outline); c.Set(8, 2, Outline); c.Set(9, 2, Outline); c.Set(10, 2, Outline);
        c.Set(2, 7, Outline); c.Set(3, 6, Outline); c.Set(3, 7, Outline); c.Set(3, 8, Outline);
        c.Set(8, 6, Outline); c.Set(9, 6, Outline); c.Set(10, 6, Outline); c.Set(9, 7, Outline);
        c.Set(16, 7, Outline); c.Set(15, 6, Outline); c.Set(15, 7, Outline); c.Set(15, 8, Outline);
        return c;
    }

    // Analog stick seen from above
    private static Canvas Stick()
    {
        var c = new Canvas(11, 11);
        for (int y = 0; y < 11; y++)
            for (int x = 0; x < 11; x++)
            {
                float dx = x - 5f, dy = y - 5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= 5.45f) c.Set(x, y, Outline);
                if (d <= 4.45f) c.Set(x, y, PadShade);
                float cy = y - 4.5f, cd = Mathf.Sqrt(dx * dx + cy * cy);
                if (cd <= 3.3f) c.Set(x, y, Outline);
                if (cd <= 2.4f) c.Set(x, y, y <= 3 ? Color32.Lerp(PadLight, White, 0.4f) : PadLight);
            }
        return c;
    }

    // Mouse with the left button lit
    private static Canvas MouseLeft()
    {
        var c = new Canvas(9, 11);
        c.Fill(0, 1, 8, 9, Outline);
        c.Fill(1, 0, 7, 10, Outline);
        c.Fill(1, 2, 7, 8, KeyFace);
        c.Fill(2, 1, 6, 9, KeyFace);
        c.Fill(2, 8, 6, 9, KeySide);
        c.Fill(1, 2, 7, 8, KeyFace);
        c.Fill(1, 7, 7, 8, KeySide);
        c.Fill(2, 1, 3, 4, new Color32(255, 92, 190, 255)); // left button
        c.Set(1, 2, new Color32(255, 92, 190, 255)); c.Set(1, 3, new Color32(255, 92, 190, 255)); c.Set(1, 4, new Color32(255, 92, 190, 255));
        c.Fill(4, 1, 4, 4, Outline);  // split
        c.Fill(1, 5, 7, 5, Outline);
        return c;
    }

    // D-pad with the down arm lit
    private static Canvas DpadDown()
    {
        var c = new Canvas(11, 11);
        c.Fill(3, 0, 7, 10, Outline);
        c.Fill(0, 3, 10, 7, Outline);
        c.Fill(4, 1, 6, 9, PadBody);
        c.Fill(1, 4, 9, 6, PadBody);
        c.Fill(4, 7, 6, 9, White);
        return c;
    }

    // ---------------------------------------------------------------- text helpers

    private static void Clear(Canvas c, int x, int y) => c.Set(x, y, default);

    private static int Text5Width(string s) => Mathf.Max(1, s.Length * (PixelFont.GlyphW + 1) - 1);

    private static void Text5(Canvas c, string s, int x, int y, Color32 ink)
    {
        if (s.Length == 1) x = (c.w - PixelFont.GlyphW) / 2;
        foreach (char ch in s)
        {
            string[] g = PixelFont.Glyph(ch);
            for (int gy = 0; gy < PixelFont.GlyphH; gy++)
                for (int gx = 0; gx < PixelFont.GlyphW; gx++)
                    if (g[gy][gx] == '#') c.Set(x + gx, y + gy, ink);
            x += PixelFont.GlyphW + 1;
        }
    }

    private static int Text3Width(string s)
    {
        int w = -1;
        foreach (char ch in s) w += HudTag.SmallGlyph(ch)[0].Length + 1;
        return Mathf.Max(1, w);
    }

    private static void Text3(Canvas c, string s, int x, int y, Color32 ink)
    {
        foreach (char ch in s)
        {
            string[] g = HudTag.SmallGlyph(ch);
            for (int gy = 0; gy < 5; gy++)
                for (int gx = 0; gx < g[gy].Length; gx++)
                    if (g[gy][gx] == '1') c.Set(x + gx, y + gy, ink);
            x += g[0].Length + 1;
        }
    }
}
