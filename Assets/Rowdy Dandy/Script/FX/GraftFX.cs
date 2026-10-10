using System.Collections.Generic;
using UnityEngine;

// Animations cut from the user's Graft (Boss Rush Jam) GIFs: Resources/Graft/<name>.png is the frames side by side at
// their true pixel size (the GIF converter found each GIF's own pixel scale and trimmed the empty canvas), and
// <name>_meta.txt says how many frames, their size, the GIF's frame delays and where the old canvas sat.
//   GraftFX.Play("StephHit", pos)                      - plays once at its own pixel size (never scaled up)
//   GraftFX.Frames("BRJ_Protobug_Idle", anchor)        - the frames, pivoted on a point of the GIF's canvas
// Stephmoss (the poison patron's transformation) uses the BRJ_Bug / Protobug sheets; the rest are effects.
public static class GraftFX
{
    public class Sheet
    {
        public Texture2D texture;
        public int frames, w, h, canvasW, canvasH;
        public float centerX, centerY, fps = 12f;
        public float MinX => canvasW / 2f - centerX;   // where the trimmed frame sat in the GIF's canvas
        public float MinY => canvasH / 2f - centerY;
    }

    private static readonly Dictionary<string, Sheet> sheets = new Dictionary<string, Sheet>();

    public static Sheet Get(string name)
    {
        if (sheets.TryGetValue(name, out Sheet s)) return s;
        s = null;
        Texture2D tex = Resources.Load<Texture2D>("Graft/" + name);
        TextAsset meta = Resources.Load<TextAsset>("Graft/" + name + "_meta");
        if (tex != null)
        {
            s = new Sheet { texture = tex, frames = 1, w = tex.width, h = tex.height };
            if (meta != null)
            {
                foreach (string line in meta.text.Split('\n'))
                {
                    string[] kv = line.Trim().Split('=');
                    if (kv.Length != 2) continue;
                    float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
                    switch (kv[0])
                    {
                        case "frames": s.frames = Mathf.Max(1, (int)v); break;
                        case "w": s.w = (int)v; break;
                        case "h": s.h = (int)v; break;
                        case "canvasW": s.canvasW = (int)v; break;
                        case "canvasH": s.canvasH = (int)v; break;
                        case "centerX": s.centerX = v; break;
                        case "centerY": s.centerY = v; break;
                        case "delays":
                            float total = 0f; int n = 0;
                            foreach (string d in kv[1].Split(','))
                                if (float.TryParse(d, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ms)) { total += Mathf.Max(20f, ms); n++; }
                            if (n > 0) s.fps = 1000f / (total / n);
                            break;
                    }
                }
            }
            if (s.canvasW == 0) { s.canvasW = s.w; s.canvasH = s.h; s.centerX = s.w / 2f; s.centerY = s.h / 2f; }
        }
        sheets[name] = s;
        return s;
    }

    public static bool Has(string name) => Get(name) != null;

    // Frames pivoted on the middle of the drawn pixels
    public static Sprite[] Frames(string name) => Frames(name, null);

    // Frames pivoted on a point of the GIF's canvas (in canvas pixels, from its top-left): sheets cut from the same
    // canvas (a boss's idle / attack / death) then all line up on that point
    public static Sprite[] Frames(string name, Vector2? canvasAnchor)
    {
        Sheet s = Get(name);
        if (s == null) return null;
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        if (canvasAnchor.HasValue)
            pivot = new Vector2((canvasAnchor.Value.x - s.MinX) / s.w, 1f - (canvasAnchor.Value.y - s.MinY) / s.h);
        return ItemArt.Frames(s.texture, s.frames, 1, pivot, 64f);
    }

    public static float Fps(string name) { Sheet s = Get(name); return s != null ? s.fps : 12f; }

    // Plays a sheet once (or looped) at its true pixel size. fpsScale speeds it up / slows it down.
    public static SheetFX Play(string name, Vector3 at, Color? tint = null, int order = 95, Transform follow = null, bool loop = false,
                               bool flipX = false, float fpsScale = 1f, Vector2? pivot = null, float scale = 1f)
    {
        Sheet s = Get(name);
        if (s == null) return null;
        SheetFX fx = SheetFX.Play(s.texture, s.frames, at, s.fps * fpsScale, 64f, order, follow, loop, tint, scale, pivot);
        if (fx != null && flipX) fx.transform.localScale = new Vector3(-fx.transform.localScale.x, fx.transform.localScale.y, 1f);
        return fx;
    }
}
