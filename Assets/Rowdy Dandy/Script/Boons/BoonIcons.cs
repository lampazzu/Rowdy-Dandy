using System.Collections.Generic;
using UnityEngine;

// Icons for the boons: cut out of the effect sheets in "New Stuff you can use" (cropped to their drawn pixels), or
// small pixel drawings below, coloured with the patron's colours. Also the round medallion they sit on and the
// patrons' emblems (a PNG at Resources/BoonPortraits/<Patron>.png replaces an emblem, e.g. Narcissism.png).
public static class BoonIcons
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Get(BoonDef d)
    {
        if (d == null) return null;
        if (cache.TryGetValue(d.id, out Sprite s) && s != null) return s;
        PatronInfo p = BoonCatalog.Of(d.patron);
        Color b = d.IsDuo ? BoonCatalog.Of(d.partner.Value).color : p.accent;
        s = Make(d.icon, p.color, b);
        cache[d.id] = s;
        return s;
    }

    public static Sprite Emblem(Patron patron)
    {
        string key = "emblem_" + patron;
        if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
        Texture2D portrait = Resources.Load<Texture2D>("BoonPortraits/" + patron);
        if (portrait != null)
        {
            portrait.filterMode = FilterMode.Point;
            s = Sprite.Create(portrait, new Rect(0, 0, portrait.width, portrait.height), new Vector2(0.5f, 0.5f), 64f);
        }
        else
        {
            PatronInfo p = BoonCatalog.Of(patron);
            s = Make(p.emblem, p.color, p.accent);
        }
        cache[key] = s;
        return s;
    }

    public static bool HasPortrait(Patron patron) => Resources.Load<Texture2D>("BoonPortraits/" + patron) != null;

    private static Sprite Make(string spec, Color a, Color b)
    {
        if (string.IsNullOrEmpty(spec)) return null;
        string[] parts = spec.Split(':');
        switch (parts[0])
        {
            case "draw":
                return Draw(parts[1], a, b);
            case "food":
                return FoodIcon(parts[1]) ?? Draw("sandwich", a, b);
            case "sheet":
            case "item":
                Texture2D tex = parts[0] == "sheet" ? SheetTexture(parts[1]) : ItemTexture(parts[1]);
                int frames = parts.Length > 2 ? int.Parse(parts[2]) : 1;
                int index = parts.Length > 3 ? int.Parse(parts[3]) : 0;
                return Crop(tex, frames, index) ?? Draw("wave", a, b);
        }
        return null;
    }

    // The Crazy Chef's food art (the .aseprite sprites), by file name
    private static Sprite FoodIcon(string name)
    {
        switch (name)
        {
            case "ovo": return BoonArt.FoodSprite(BoonArt.Food.Egg);
            case "pao": return BoonArt.FoodSprite(BoonArt.Food.Bread);
            case "tomate": return BoonArt.FoodSprite(BoonArt.Food.Tomato);
            case "guejo": return BoonArt.FoodSprite(BoonArt.Food.Cheese);
            case "armondega": return BoonArt.FoodSprite(BoonArt.Food.Meatball);
            case "arface": return BoonArt.FoodSprite(BoonArt.Food.Lettuce);
        }
        return null;
    }

    private static Texture2D SheetTexture(string field)
    {
        BoonArt art = BoonArt.Get;
        if (art == null) return null;
        switch (field)
        {
            case "clawSlash": return art.clawSlash;
            case "waterSonic": return art.waterSonic;
            case "charm": return art.charm;
            case "fear": return art.fear;
            case "slow": return art.slow;
            case "charge": return art.charge;
            case "magicalHit": return art.magicalHit;
            case "physicalHit": return art.physicalHit;
            case "manaRecovery": return art.manaRecovery;
            case "ail": return art.ail;
            case "earthPillar": return art.earthPillar;
            case "matinta": return art.matinta;
        }
        return null;
    }

    private static Texture2D ItemTexture(string field)
    {
        ItemArt art = ItemArt.Get;
        if (art == null) return null;
        switch (field)
        {
            case "werewolf": return art.werewolf;
            case "vfxPoison": return art.vfxPoison;
            case "vfxHeal": return art.vfxHeal;
            case "vfxDecay": return art.vfxDecay;
            case "vfxBlock": return art.vfxBlock;
        }
        return null;
    }

    // One frame of a horizontal sheet, trimmed to its visible pixels (needs Read/Write, set by BoonArtSetup)
    private static Sprite Crop(Texture2D tex, int frames, int index)
    {
        if (tex == null) return null;
        int fw = tex.width / Mathf.Max(1, frames);
        var rect = new RectInt(Mathf.Clamp(index, 0, frames - 1) * fw, 0, fw, tex.height);
        if (tex.isReadable)
        {
            try
            {
                Color32[] px = tex.GetPixels32();
                int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
                for (int y = rect.yMin; y < rect.yMax; y++)
                    for (int x = rect.xMin; x < rect.xMax; x++)
                        if (px[y * tex.width + x].a > 40)
                        {
                            if (x < minX) minX = x; if (x > maxX) maxX = x;
                            if (y < minY) minY = y; if (y > maxY) maxY = y;
                        }
                if (maxX >= 0)
                {
                    // square-ish box so the icon stays centred
                    int w = maxX - minX + 1, h = maxY - minY + 1, size = Mathf.Max(w, h);
                    int cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
                    int x0 = Mathf.Clamp(cx - size / 2, rect.xMin, rect.xMax - 1), y0 = Mathf.Clamp(cy - size / 2, 0, tex.height - 1);
                    int x1 = Mathf.Clamp(x0 + size, x0 + 1, rect.xMax), y1 = Mathf.Clamp(y0 + size, y0 + 1, tex.height);
                    rect = new RectInt(x0, y0, x1 - x0, y1 - y0);
                }
            }
            catch (System.Exception) { }
        }
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(rect.x, rect.y, rect.width, rect.height), new Vector2(0.5f, 0.5f), 64f);
    }

    // ---------------------------------------------------------------- the round plate icons sit on
    private static Sprite medallion;
    public static Sprite Medallion
    {
        get
        {
            if (medallion != null) return medallion;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "BoonMedallion" };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    Color32 col = new Color32(0, 0, 0, 0);
                    if (d <= 15.5f) col = new Color32(27, 8, 32, 255);
                    if (d <= 14.5f) col = (y > 16 && x < 16) ? new Color32(255, 255, 255, 255) : new Color32(215, 215, 215, 255);
                    if (d <= 12.5f) col = new Color32(27, 8, 32, 255);
                    if (d <= 11.5f) col = new Color32(70, 62, 80, 255);
                    px[y * n + x] = col;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            medallion = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
            return medallion;
        }
    }

    // ---------------------------------------------------------------- pixel drawings (16 x 16)
    // a / A = patron colour (light / dark), b / B = accent, k outline, w white, r red, g gold, c cyan, G green,
    // p pink, s silver, n brown, z dark grey, y yellow, v violet, P dark pink, o orange, d deep blue
    private static Sprite Draw(string name, Color a, Color b)
    {
        string[] rows = Drawings(name);
        Color32 Pal(char ch)
        {
            switch (ch)
            {
                case 'a': return a;
                case 'A': return a * 0.62f + new Color(0, 0, 0, 0.38f);
                case 'b': return b;
                case 'B': return b * 0.62f + new Color(0, 0, 0, 0.38f);
                case 'k': return new Color32(27, 8, 32, 255);
                case 'w': return new Color32(250, 246, 255, 255);
                case 'r': return new Color32(230, 40, 60, 255);
                case 'g': return new Color32(255, 210, 76, 255);
                case 'c': return new Color32(120, 230, 255, 255);
                case 'G': return new Color32(130, 240, 90, 255);
                case 'p': return new Color32(255, 110, 200, 255);
                case 's': return new Color32(200, 200, 220, 255);
                case 'n': return new Color32(130, 82, 50, 255);
                case 'z': return new Color32(70, 62, 80, 255);
                case 'y': return new Color32(255, 240, 90, 255);
                case 'v': return new Color32(180, 100, 255, 255);
                case 'P': return new Color32(170, 40, 120, 255);
                case 'o': return new Color32(255, 140, 50, 255);
                case 'd': return new Color32(25, 55, 120, 255);
            }
            return new Color32(255, 255, 255, 255);
        }
        // pad / trim to 16 x 16 so a slip in a drawing can't break anything
        var fixedRows = new string[16];
        for (int i = 0; i < 16; i++)
        {
            string r = i < rows.Length ? rows[i] : "";
            fixedRows[i] = r.Length >= 16 ? r.Substring(0, 16) : r.PadRight(16, '.');
        }
        return BoonFX.FromRows("BoonIcon_" + name + "_" + ColorUtility.ToHtmlStringRGB(a), fixedRows, Pal, new Vector2(0.5f, 0.5f), 64f);
    }

    private static string[] Drawings(string name)
    {
        switch (name)
        {
            case "hairflip": return new[] {
                "................",
                ".....kkkkkk.....",
                "...kkaaaaaakk...",
                "..kaawwaaaaaak..",
                ".kawwaaaaaabbak.",
                ".kawaaakkkkabak.",
                "kaaaakk....kbak.",
                "kaaak.......kbk.",
                "kaak.........k..",
                "kaak............",
                ".kaak...........",
                ".kaaak..........",
                "..kaak..........",
                "...kk...........",
                "................",
                "................" };
            case "wave": return new[] {
                "................",
                "......kkkk......",
                "....kkwwwwk.....",
                "...kwwaaaawk....",
                "..kwaaakkaawk...",
                "..kaak....kak...",
                ".kaak......k....",
                ".kaak...........",
                "kaaak.......kk..",
                "kaaaak....kkaak.",
                "kAaaaakkkkaaaak.",
                "kAAaaaaaaaaaaAk.",
                ".kAAAaaaaaaAAk..",
                "..kkAAAAAAAAk...",
                "....kkkkkkkk....",
                "................" };
            case "mirror": return new[] {
                ".....kkkkkk.....",
                "....kggggggk....",
                "...kgcwwcccgk...",
                "..kgcwwcccccgk..",
                "..kgwwcccccagk..",
                "..kgcccccccagk..",
                "..kgccccccaagk..",
                "...kgcccaaagk...",
                "....kggggggk....",
                ".....kkggkk.....",
                "......kggk......",
                "......kggk......",
                "......kbbk......",
                "......kbbk......",
                "......kggk......",
                ".......kk......." };
            case "crown": return new[] {
                "................",
                "................",
                ".k.....k.....k..",
                "kwk...kwk...kwk.",
                "kgk...kgk...kgk.",
                "kggk.kgggk.kggk.",
                "kgggkgggggkgggk.",
                "kggggggaggggggk.",
                "kgggggaaagggggk.",
                "kggaggggggggagk.",
                "kgaaaggggggaaak.",
                "kggggggggggggggk",
                "kgwgwgwgwgwgwggk",
                "kkkkkkkkkkkkkkk.",
                "................",
                "................" };
            case "spiral": return new[] {
                "................",
                "....kkkkkkk.....",
                "...kaaaaaaak....",
                "..kaakkkkkaak...",
                ".kaak.....kaak..",
                ".kak..kkk..kak..",
                "kaak.kaaak.kak..",
                "kak.kak.kak.kak.",
                "kak.kak.kak.kak.",
                "kak..kkkak..kak.",
                "kaak....kak.kak.",
                ".kaakkkkaak.kak.",
                "..kaaaaaak..kak.",
                "...kkkkkk..kaak.",
                "..........kwak..",
                "...........kk..." };
            case "board": return new[] {
                "................",
                "............kk..",
                "...........kwak.",
                "..........kwaak.",
                ".........kwaak..",
                "........kwaak...",
                ".......kwbbk....",
                "......kwaak.....",
                ".....kwaak......",
                "....kwaak.......",
                "...kwbbk........",
                "..kwaak.........",
                ".kwaak..........",
                ".kaak...........",
                "..kk............",
                "................" };
            case "moonrage": return new[] {
                "................",
                ".....kkkkk......",
                "...kkaaaak......",
                "..kaaaaak.......",
                ".kaaaaak........",
                ".kaaaak.........",
                "kaaaaak.........",
                "kaaaak......k...",
                "kaaaak.....krk..",
                "kaaaaak.....k...",
                ".kaaaaak........",
                ".kaaaaaakk...kk.",
                "..kaaaaaaakkkak.",
                "...kkAaaaaaaak..",
                ".....kkkkkkkk...",
                "................" };
            case "moonwolf": return new[] {
                ".....kkkkkk.....",
                "...kkwwwwwwkk...",
                "..kwwwwwwwswwk..",
                ".kwwswwwwwwwwwk.",
                ".kwwwwwwwwwkkwk.",
                "kwwwwwwwwwkzzkwk",
                "kwwwwwwwkkzzzkwk",
                "kwwwwwwkzzzzkwwk",
                "kwwkkkkzzrzzkwwk",
                "kwkzzzzzzzzkwwwk",
                "kwkzzzzzzzkwwwwk",
                ".kkzzzzzzzkwwwk.",
                ".kzzzzzzzzkwwwk.",
                "..kzzzzzzzkwwk..",
                "...kkzzzzzkkk...",
                ".....kkkkkk....." };
            case "fang": return new[] {
                "................",
                ".kkkkkkkkkkkkk..",
                "kssssssssssssk..",
                "kwwssssssssswk..",
                ".kwwk.....kwwk..",
                ".kwwk.....kwwk..",
                "..kwk......kwk..",
                "..kwk......kwk..",
                "...k...kk...k...",
                ".......krk......",
                "......krrk......",
                ".....krrrrk.....",
                ".....krrwrk.....",
                ".....krrrrk.....",
                "......kkkk......",
                "................" };
            case "paw": return new[] {
                "................",
                "..kkk.....kkk...",
                ".kaaak...kaaak..",
                ".kaaak...kaaak..",
                "..kkk.kkk.kkk...",
                ".....kaaak......",
                "kkk..kaaak..kkk.",
                "kaak..kkk..kaak.",
                "kaak.......kaak.",
                ".kk..kkkkk..kk..",
                "....kaaaaak.....",
                "...kaaaaaaak....",
                "...kaaaaaaak....",
                "...kAaaaaaAk....",
                "....kkkkkkk.....",
                "................" };
            case "sporestep": return new[] {
                "................",
                ".....kkkkkk.....",
                "...kkaaaaaakk...",
                "..kaawaaaawaak..",
                ".kaawwaabaaaaak.",
                ".kaaaaaabbaaaak.",
                "kaabbaaaaaaawaak",
                "kAaabaaaaaaaaaAk",
                ".kkkkkkkkkkkkkk.",
                "......kwwk......",
                "......kwwk......",
                ".....kwwwwk.....",
                ".....kwwwwk.....",
                "....kkkkkkkk....",
                "................",
                "................" };
            case "leaf": return new[] {
                "................",
                "...........kkk..",
                "........kkkGGk..",
                "......kkGGGGGk..",
                ".....kGGGwGGGk..",
                "....kGGGwGGGk...",
                "...kGGGwGGGGk...",
                "...kGGwGGGGk....",
                "..kGGwGGGGk.....",
                "..kGwGGGkk......",
                "..kwGkkk........",
                ".kwkk...........",
                ".kk.......v.....",
                "k........vvv....",
                "..........v.....",
                "................" };
            case "ninelives": return new[] {
                "....kkkkkkk.....",
                "...kgggggggk....",
                "....kkkkkkk.....",
                "..k.........k...",
                ".kak.......kak..",
                ".kaak.....kaak..",
                ".kaaakkkkkaaak..",
                ".kaaaaaaaaaaak..",
                ".kawkaaaaakwak..",
                ".kakkaaaaakkak..",
                ".kaaaaapaaaaak..",
                "..kaaawwwaaak...",
                "...kkaaaaakk....",
                ".....kkkkk......",
                "................",
                "................" };
            case "fur": return new[] {
                "................",
                "....k...k...k...",
                "...kak.kak.kak..",
                "...kak.kak.kak..",
                "..kaak.kaakkaak.",
                "..kaakkaaakaaak.",
                ".kaaaakaaaaaaak.",
                ".kaawaaaaawaaak.",
                ".kaaaaaaaaaaaak.",
                ".kaaaabaaaaabak.",
                ".kabbbbbbbbbbbk.",
                ".kbbbbbbbbbbbbk.",
                "..kBBBBBBBBBBk..",
                "...kkkkkkkkkk...",
                "................",
                "................" };
            case "catface": return new[] {
                "................",
                "..k.........k...",
                ".kak.......kak..",
                ".kaak.....kaak..",
                ".kaaakkkkkaaak..",
                ".kaaaaaaaaaaak..",
                ".kaaaaaaaaaaak..",
                ".kawwkaaakwwak..",
                ".kawkkaaakkwak..",
                ".kaaaaapaaaaak..",
                "kkaaaakwkaaaakk.",
                ".kaaaaaaaaaaak..",
                "..kaaaaaaaaak...",
                "...kkkkkkkkk....",
                "................",
                "................" };
            case "bell": return new[] {
                "................",
                ".......kk.......",
                "......kggk......",
                ".....kggggk.....",
                "....kgwggggk....",
                "....kgwggggk....",
                "...kggggggggk...",
                "...kggggggggk...",
                "..kgggggggggAk..",
                "..kgggggggggAk..",
                ".kkkkkkkkkkkkkk.",
                ".kbbbbbbbbbbbbk.",
                ".kkkkkkkkkkkkkk.",
                "......kzzk......",
                ".......kk.......",
                "................" };
            case "brokensword": return new[] {
                "................",
                "............kk..",
                "...........kwsk.",
                "..........kwsk..",
                ".........kwsk...",
                "........kwsk....",
                "................",
                "......kwsk......",
                ".....kwsk...y...",
                "..k.kwsk...yyy..",
                "..kkwsk.....y...",
                "...kgk..........",
                "..kgkgk.........",
                ".kgk.kk.........",
                ".kk.............",
                "................" };
            case "carpet": return new[] {
                "................",
                "................",
                "..kkkk..........",
                ".kaaaak.........",
                "kaabbaak........",
                "kabkkbak........",
                "kabkkbakkkkkkkkk",
                "kaabbaakaaaaaaak",
                ".kaaaakaggaggaak",
                "..kkkk.kaaaaaaak",
                ".......kAAAAAAAk",
                ".......kkkkkkkkk",
                "................",
                "..g...........g.",
                ".ggg.........ggg",
                "..g...........g." };
            case "kiss": return new[] {
                "................",
                "..........kk.kk.",
                ".........kppkppk",
                ".........kpppppk",
                "..........kpppk.",
                "...........kpk..",
                "............k...",
                "..kkkk..kkkk....",
                ".kaaaakkaaaak...",
                "kaawaaaaaaawak..",
                "kaaaaakkaaaaak..",
                "kkkkkkkkkkkkkk..",
                "kAaaaaaaaaaaAk..",
                ".kAaaaaaaaaAk...",
                "..kkAAAAAAkk....",
                "....kkkkkk......" };
            case "spotlight": return new[] {
                "....kkkkkk......",
                "...kzzzzzzk.....",
                "..kzzzzzzzzk....",
                "..kkkkkkkkkk....",
                "...kyyyyyyk.....",
                "..kyyyyyyyyk....",
                ".kyyyyyyyyyyk...",
                ".kyyyyyyyyyyyk..",
                "kyyyyyyyyyyyyyk.",
                "kyyyyyyyyyyyyyyk",
                "................",
                "....kkkkkk......",
                "...kaawaaak.....",
                "...kaaaaaak.....",
                "....kkkkkk......",
                "................" };
            case "brokenheart": return new[] {
                "................",
                ".kkkk....kkkk...",
                "kaaaak..kaaaak..",
                "kawaaakkaaaaak..",
                "kaaaaak.kaaaak..",
                "kaaaak..kaaaak..",
                ".kaaak.kaaaak...",
                "..kaaakkaaak....",
                "...kaak.kak.....",
                "....kak.kk......",
                ".....kk.........",
                "................",
                "...b.....b......",
                "..bbb...bbb.....",
                "...b.....b......",
                "................" };
            case "diamond": return new[] {
                "................",
                "....kkkkkkkk....",
                "...kwwaaaabbk...",
                "..kwaaaaaaabbk..",
                ".kkkkkkkkkkkkkk.",
                ".kawaaaaaaaabbk.",
                "..kawaaaaaabk...",
                "...kaaaaaaak....",
                "....kaaaaak.....",
                ".....kaaak......",
                "......kak.......",
                ".......k........",
                "................",
                "..w..........w..",
                ".www........www.",
                "..w..........w.." };
            case "spraycan": return new[] {
                "................",
                "......kk....w...",
                ".....kzzk..w.w..",
                ".....kzzk.w...w.",
                "....kkkkkk......",
                "...kaaaaaak.w...",
                "...kawaaaak.....",
                "...kawaaaak.....",
                "...kaaaaaak.....",
                "...kbbbbbbk.....",
                "...kbbbbbbk.....",
                "...kaaaaaak.....",
                "...kaaaaaak.....",
                "...kAAAAAAk.....",
                "....kkkkkk......",
                "................" };
            case "pressure": return new[] {
                "................",
                ".......kk.......",
                "......kaak......",
                ".......aa.......",
                "....kkkkkkkk....",
                "..k.kddddddk.k..",
                ".kak.kdccdk.kak.",
                "kaaa.kdccdk.aaak",
                ".kak.kdccdk.kak.",
                "..k.kddddddk.k..",
                "....kkkkkkkk....",
                ".......aa.......",
                "......kaak......",
                ".......kk.......",
                "................",
                "................" };
            case "lure": return new[] {
                "............kk..",
                "..........kkck..",
                ".........kccwck.",
                "........k.kccck.",
                ".......k...kkk..",
                "......k.........",
                ".kkkkk..........",
                "kdddddkk........",
                "kdwddddddk......",
                "kddddddddk......",
                "kkddkdkdddk.....",
                ".kwkwkwkdddk....",
                ".kddddddddk.....",
                "..kkkkkkkk......",
                "................",
                "................" };
            case "ink": return new[] {
                "................",
                "....kkkk........",
                "...kzzzzk.kkk...",
                "..kzzzzzzkzzzk..",
                ".kzzwzzzzzzzzzk.",
                ".kzzzzzzzzzzzzk.",
                "kzzzzzzzzzzzzzk.",
                "kzzzzzzzzzwzzzzk",
                ".kzzzzzzzzzzzzk.",
                "..kkzzzzzzzzkk..",
                "....kkzzzkkk....",
                ".....kzk.kzk....",
                "......k...k.....",
                "....kzk...kzk...",
                "....kk.....kk...",
                "................" };
            case "whirl": return new[] {
                "................",
                "....kkkkkkk.....",
                "..kkaaaaaaakk...",
                ".kaakkkkkkkaak..",
                "kaak.kaaak..kak.",
                "kak.kakkkak..kak",
                "kak.kak..kak.kak",
                "kak..kkk.kak.kak",
                "kaak....kak..kak",
                ".kaakkkkak..kak.",
                "..kaaaaak..kaak.",
                "...kkkkk.kkaak..",
                "........kaak....",
                "......kkaak.....",
                ".....kaakk......",
                "......kk........" };
            case "tentacle": return new[] {
                "................",
                "..........kkk...",
                ".........kaaak..",
                ".........kak.k..",
                "..........kk....",
                "..........kak...",
                ".........kaak...",
                ".........kawk...",
                "........kaaak...",
                "........kawak...",
                ".......kaaaak...",
                ".......kawaak...",
                "......kaaaaak...",
                ".....kaaawaak...",
                "....kAaaaaaak...",
                "...kkkkkkkkkk..." };
            case "mark": return new[] {
                "................",
                ".......kk.......",
                "......krrk......",
                "....kkkrrkkk....",
                "...krr.kk.rrk...",
                "..kr...rr...rk..",
                "..kr..krrk..rk..",
                "kkrrkrrwrrkrrkk.",
                "kkrrkrrrrrkrrkk.",
                "..kr..krrk..rk..",
                "..kr...rr...rk..",
                "...krr.kk.rrk...",
                "....kkkrrkkk....",
                "......krrk......",
                ".......kk.......",
                "................" };
            case "drop": return new[] {
                ".......k........",
                "......krk.......",
                "......krk.......",
                ".....krrrk......",
                ".....krrrk......",
                "....krrrrrk.....",
                "...krrwrrrrk....",
                "...krwrrrrrk....",
                "..krrrrrrrrrk...",
                "..krrrrrrrrrk...",
                "..krrrrrrrrrk...",
                "...krrrrrrrk....",
                "....kkrrrkk.....",
                "......kkk.......",
                "..r.......r.....",
                ".rrr.....rrr...." };
            case "moonbite": return new[] {
                "................",
                ".....kkkkkk.....",
                "...kkbbbbbbkk...",
                "..kbbwbbbbbbbk..",
                ".kbbwbbbbbbbbbk.",
                ".kbbbbbbbbbk.k..",
                "kbbbbbbbbbk.....",
                "kbbbbbbbbk......",
                "kbbbbbbbbk......",
                "kbbbbbbbbbk.....",
                ".kbbbbbbbbbk.k..",
                ".kbbbbbbbbbbbbk.",
                "..kbbbbbbbbbbk..",
                "...kkbbbbbbkk...",
                ".....kkkkkk.....",
                "................" };
            case "fester": return new[] {
                "................",
                "......kk........",
                ".....kaak..kk...",
                "......kk..kaak..",
                "..kk.......kk...",
                ".kaak..kkk......",
                "..kk..kaaak.....",
                "......kawak.....",
                "..kkkkkkkkkkkk..",
                ".kaaaaaaaaaaaak.",
                "kaabaaaaabaaaaak",
                "kaaaaabaaaaabaak",
                "kAaaaaaaaaaaaaAk",
                ".kAAAAAAAAAAAAk.",
                "..kkkkkkkkkkkk..",
                "................" };
            case "plague": return new[] {
                "................",
                "......kkkk......",
                ".....kaaaak.....",
                ".....kawaak.....",
                ".....kaaaak.....",
                "......kkkk......",
                ".....k....k.....",
                "....k......k....",
                "..kkkk....kkkk..",
                ".kaaaak..kaaaak.",
                ".kawaak..kawaak.",
                ".kaaaak..kaaaak.",
                "..kkkk....kkkk..",
                "................",
                "................",
                "................" };
            case "thorn": return new[] {
                "................",
                "....k.....k.....",
                "....kk...kk.....",
                ".....kk.kk......",
                "k.....kGk.....k.",
                "kk....kGk....kk.",
                ".kkk..kGk..kkk..",
                "...kkkGGGkkk....",
                "......GGG.......",
                "...kkkGGGkkk....",
                ".kkk..kGk..kkk..",
                "kk....kGk....kk.",
                "k.....kGk.....k.",
                ".....kk.kk......",
                "....kk...kk.....",
                "....k.....k....." };
            case "roots": return new[] {
                "................",
                "................",
                "...k.......k....",
                "..knk.....knk...",
                "..knk..k..knk...",
                "...knk.knkknk...",
                "...knkknknnk....",
                "....knnnnnk.....",
                "....knnnnk......",
                "kkkkknnnnkkkkkkk",
                "zzzzknnnnkzzzzzz",
                "zzzknkzzknkzzzzz",
                "zzknkzzzzknkzzzz",
                "zknkzzzzzzknkzzz",
                "zkkzzzzzzzzkkzzz",
                "zzzzzzzzzzzzzzzz" };
            case "flower": return new[] {
                "................",
                "....kkk.kkk.....",
                "...kpppkpppk....",
                "..kppwpkpppk....",
                "..kpppkykpppk...",
                "...kkkyyykkk....",
                "..kpppkykpppk...",
                "..kppppkppppk...",
                "...kpppkpppk....",
                "....kkkGkkk.....",
                "......kGk.kk....",
                "...kk.kGkkGk....",
                "...kGkkGkGk.....",
                "....kGkGkk......",
                "......kGk.......",
                "......kkk......." };
            case "pod": return new[] {
                "................",
                "......kk........",
                ".....kGGk.......",
                "....kGvvGk......",
                "...kGvGGvGk.....",
                "...kGGGGvGk.....",
                "..kGvGGGGGGk....",
                "..kGGGGvGGGk....",
                "..kGGvGGGGvk....",
                "...kGGGGGGk.....",
                "...kGGGvGGk.....",
                "....kGGGGk......",
                ".....kkkk.......",
                "..G.......v.....",
                ".GGG.....vvv....",
                "..G.......v....." };
            case "fish": return new[] {
                "................",
                "................",
                "................",
                "..........k.....",
                "....kkkkkkak....",
                "..kkaaaaaakak..k",
                ".kawkaaaaaaakkak",
                "kaakkaaaaaaaaaak",
                "kaaaaaaaaaaaaaak",
                ".kaaaaaaaaakkkak",
                "..kkaaaaaakk..kk",
                "....kkkkkk......",
                "................",
                "................",
                "................",
                "................" };
            case "hairball": return new[] {
                "................",
                "................",
                ".....kkkkk......",
                "...kknnznnkk....",
                "..kznnnnznnnk...",
                "..knnzznnnnzk...",
                ".knnnnnnznnnnk..",
                ".kznnznnnnznnk..",
                ".knnnnnznnnnzk..",
                "..knznnnnzznk...",
                "..knnnznnnnnk...",
                "...kknnnznkk.k..",
                ".....kkkkk..kzk.",
                "..........kzk...",
                "...........k....",
                "................" };
            case "ambush": return new[] {
                "................",
                "..k...k.........",
                ".kak.kak........",
                ".kaakaak........",
                ".kaaaaaakk......",
                ".kawaawaaak.....",
                ".kaaaaaaaaakk...",
                "..kapaaaaaaaak..",
                "...kkaaaaaaaaak.",
                ".....kaaaaaaaak.",
                "......kakkkak.k.",
                ".....kwk..kwk...",
                ".....kk....kk...",
                "..s.s...........",
                "...s.s..........",
                "....s.s........." };
            case "stench": return new[] {
                "..G...G....G....",
                ".G...G....G.....",
                "..G...G....G....",
                ".G...G....G.....",
                "................",
                "..k.........k...",
                ".kak.......kak..",
                ".kaak.....kaak..",
                ".kaaakkkkkaaak..",
                ".kaaaaaaaaaaak..",
                ".kakkkaaakkkak..",
                ".kaaaaapaaaaak..",
                "..kaaakkkaaak...",
                "...kkaaaaakk....",
                ".....kkkkk......",
                "................" };
            case "topcat": return new[] {
                "...k..k..k......",
                "...kgkkgkkgk....",
                "...kgggggggk....",
                "...kkkkkkkkk....",
                "..k.........k...",
                ".kak.......kak..",
                ".kaak.....kaak..",
                ".kaaakkkkkaaak..",
                ".kaaaaaaaaaaak..",
                ".kawwkaaakwwak..",
                ".kawkkaaakkwak..",
                ".kaaaaapaaaaak..",
                "..kaaaaaaaaak...",
                "...kkkkkkkkk....",
                "................",
                "................" };
            case "sandwich": return new[] {
                "................",
                "....kkkkkkkk....",
                "..kkbbbbbbbbkk..",
                ".kbbwbbbbbbbbbk.",
                ".kBBBBBBBBBBBBk.",
                "kGGkGGGkGGGkGGGk",
                ".kyyyyyyyyyyyyk.",
                "krrrrkrrrrrkrrrk",
                ".knnnnnnnnnnnnk.",
                "knnnznnnnznnnnnk",
                ".kGGGGGGGGGGGGk.",
                ".kbbbbbbbbbbbbk.",
                ".kBBBBBBBBBBBBk.",
                "..kkkkkkkkkkkk..",
                "................",
                "................" };
            case "sauce": return new[] {
                "......kk........",
                ".....kwwk.......",
                ".....kaak.......",
                ".....kaak.......",
                "....kkkkkk......",
                "...krrrrrrk.....",
                "..krrwrrrrrk....",
                "..krwrrrrrrk....",
                "..kryyyyyyrk....",
                "..kryrryryrk....",
                "..kryyyyyyrk....",
                "..krrrrrrrrk....",
                "..krrrrrrrrk....",
                "...kkkkkkkk..r..",
                "............rrr.",
                ".............r.." };
            case "whetstone": return new[] {
                "..........kk....",
                ".........kwsk...",
                "........kwsk....",
                ".......kwsk.....",
                "......kwsk..y...",
                ".....kwsk..yyy..",
                "....kwsk....y...",
                "...kgk..........",
                "..kgk...........",
                ".kkk............",
                "..kkkkkkkkkkkk..",
                ".knnnnnnnnnnnnk.",
                "kbbbbbbbbbbbbbbk",
                "kBBBBBBBBBBBBBBk",
                ".kkkkkkkkkkkkkk.",
                "................" };
            case "ingot": return new[] {
                "................",
                "................",
                "................",
                "....kkkkkkkk....",
                "...kwsssssssk...",
                "..kwssssssssak..",
                ".kkkkkkkkkkkkak.",
                ".kssssssssssskk.",
                ".kssssssssssssk.",
                ".kzzzzzzzzzzzzk.",
                "..kkkkkkkkkkkk..",
                "................",
                "...b...b...b....",
                "..bbb.bbb.bbb...",
                "...b...b...b....",
                "................" };
            case "hook": return new[] {
                ".........kk.....",
                "........kwk.....",
                "........kwk.....",
                "........kwk.....",
                "........kwk.....",
                "........kwk.....",
                "..kk....kwk.....",
                ".kwk....kwk.....",
                ".kwk....kwk.....",
                ".kwkk..kwwk.....",
                "..kwwkkwwk......",
                "...kwwwwk.......",
                "....kkkk........",
                "................",
                "..c...c...c.....",
                "...c.c.c.c......" };
            case "steelwave": return new[] {
                "................",
                ".......kkkk.....",
                ".........kwk....",
                "..........kwk...",
                "..........ksk...",
                "...........ksk..",
                "...........kswk.",
                "...........kswk.",
                "...........kswk.",
                "...........ksk..",
                "..........ksk...",
                "..........kwk...",
                ".........kwk....",
                ".......kkkk.....",
                "..b.b...........",
                ".b.b............" };
            case "spear": return new[] {
                "................",
                "................",
                "..............k.",
                ".............kwk",
                "............kwsk",
                "...........kwsk.",
                "..........kwsk..",
                ".........kgk....",
                "........knk.....",
                ".......knk......",
                "......knk.......",
                ".....knk........",
                "....knk.........",
                "...knk..........",
                "..knk...........",
                "..kk............" };
            case "cleaver": return new[] {
                "................",
                "..kkkkkkkkk.....",
                ".kwssssssssk....",
                ".kwssssssssk....",
                ".kssssssskssk...",
                ".kssssssskssk...",
                ".ksssssssssssk..",
                ".kzzzzzzzzzzzk..",
                "..kkkkkkkkkkkkk.",
                "...........knnk.",
                "............knnk",
                ".............kkk",
                "................",
                "...r.r..........",
                "....r.r.........",
                "................" };
            case "anvil": return new[] {
                "................",
                "................",
                "................",
                "kkkkkkkkkkkkkkk.",
                "kwsssssssssssk..",
                "kzzzzzzzzzzzzzk.",
                ".kkkkzzzzzzzk...",
                ".....kzzzzzk....",
                "......kzzzk.....",
                ".....kzzzzzk....",
                "....kzzzzzzzk...",
                "...kssssssssk...",
                "...kkkkkkkkkk...",
                "................",
                "..b.....b.......",
                ".bbb...bbb......" };
            case "sparks": return new[] {
                "......y.........",
                "....y.y.y.......",
                ".....yyy....b...",
                "...yyygyyy.bbb..",
                ".....yyy....b...",
                "....y.y.y.......",
                "......y.....y...",
                "...b.......yyy..",
                "..bbb.......y...",
                "...b............",
                "......kkkkkk....",
                ".....kzzzzzzk...",
                "....kzzzzzzzzk..",
                "...kkkkkkkkkkkk.",
                "....kzk....kzk..",
                "....kk......kk.." };
            case "sunbeam": return new[] {
                ".....yyyyyy.....",
                "....yyywwyyy....",
                ".....yywwyy.....",
                "......ywwy......",
                "......ywwy......",
                "......ywwy......",
                "......ywwy......",
                "......ywwy......",
                "......ywwy......",
                "......ywwy......",
                ".....yywwyy.....",
                "....yyywwyyy....",
                "..kkkkkkkkkkkk..",
                ".kgggggggggggggk",
                "kgggggggggggggggk",
                "................" };
            case "flame": return new[] {
                "......k.........",
                ".....kak........",
                ".....kaak.......",
                "....kaaak..k....",
                "...kaayaak.kk...",
                "...kayyyaakak...",
                "..kaayyyyaaak...",
                "..kayywyyyaak...",
                ".kaayywwyyaaak..",
                ".kayyywwyyyaak..",
                ".kayyyyyyyyyak..",
                ".kaayyyyyyyaak..",
                "..kaaayyyyaak...",
                "...kkaaaaakk....",
                ".....kkkkk......",
                "................" };
            case "sun": return new[] {
                ".......y........",
                "...y...y...y....",
                "....y.....y.....",
                "......kkkk......",
                ".....kaaaak.....",
                "y...kaawaaak...y",
                ".yy.kawaaaak.yy.",
                "....kaaaaaak....",
                ".yy.kaaaaaak.yy.",
                "y...kaaaaaak...y",
                ".....kaaaak.....",
                "......kkkk......",
                "....y.....y.....",
                "...y...y...y....",
                ".......y........",
                "................" };
            case "halo": return new[] {
                "......kkkk......",
                ".....kyyyyk.....",
                ".....kygyyk.....",
                "......kkkk......",
                "...bbbbbbbbbb...",
                "..b..........b..",
                "kkkk........kkkk",
                "kyyk........kyyk",
                "kygk........kygk",
                "kkkk........kkkk",
                "..b..........b..",
                "...bbbbbbbbbb...",
                "................",
                "................",
                "................",
                "................" };
            case "flash": return new[] {
                ".......w........",
                ".......w........",
                "..w....w....w...",
                "...w...w...w....",
                "....w.www.w.....",
                ".....wwwww......",
                "wwwwwwwywwwwwww.",
                ".....wwwww......",
                "....w.www.w.....",
                "...w...w...w....",
                "..w....w....w...",
                ".......w........",
                ".......w........",
                "................",
                "................",
                "................" };
            case "sunspot": return new[] {
                "................",
                "......y.........",
                "...y..y..y......",
                "....y.y.y.......",
                "................",
                "......y.........",
                "................",
                "................",
                "...kkkkkkkkkk...",
                ".kkyyyyyyyyyykk.",
                "kyyyywwyyyyyyyyk",
                "kyyyyyyyyyywwyyk",
                ".kkyyyyyyyyyykk.",
                "...kkkkkkkkkk...",
                "................",
                "................" };
            case "pinkwave": return new[] {
                "................",
                "........kkkk....",
                "......kkppppk...",
                ".....kppwwwppk..",
                "....kppwkkkwpk..",
                "...kppwk...kpk..",
                "..kpppk.....k...",
                "..kppk..........",
                ".kpppk......kk..",
                ".kppppk...kkppk.",
                "kpppppkkkkpppppk",
                "kPppppppppppppPk",
                ".kPPpppppppppPk.",
                "..kkPPPPPPPPkk..",
                "....kkkkkkkk....",
                "................" };
            case "eclipse": return new[] {
                "...y...y...y....",
                "....y..y..y.....",
                "......kkkk......",
                "y...kkzzzzkk...y",
                ".y.kzzzzzzzzk.y.",
                "..kzzzzzzzzzzk..",
                "..kzzzzzzzzzzk..",
                "yykzzzzzzzzzzkyy",
                "..kzzzzzzzzzzk..",
                "..kzzzzzzzzzzk..",
                ".y.kzzzzzzzzk.y.",
                "y...kkzzzzkk...y",
                "......kkkk......",
                "....y..y..y.....",
                "...y...y...y....",
                "................" };
            case "hotblade": return new[] {
                "..............kk",
                ".............kwk",
                "............kwyk",
                "...........kwyk.",
                "..........kwyk..",
                ".........kwyk...",
                "........kwyk....",
                ".......kwyk.....",
                "......kwyk......",
                "..k..kwyk.......",
                "..kkkwyk........",
                "...kgkk.........",
                "..kgkgk.........",
                ".kgk.kk.........",
                ".kk.............",
                "................" };
        }
        return new[] { "......kkkk......", "....kkaaaakk....", "...kaaaaaaaak...", "..kaaaaaaaaaak..", "..kaaaaaaaaaak..", "...kaaaaaaaak...", "....kkaaaakk....", "......kkkk......" };
    }
}
