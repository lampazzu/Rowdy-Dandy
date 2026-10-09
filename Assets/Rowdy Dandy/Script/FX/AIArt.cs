using System.Collections.Generic;
using UnityEngine;

// AI PLACEHOLDER ART: every image Claude made for the game lives in Assets/Resources/AI Placeholders/, to be redrawn
// in your own style.
//   HUD/, Pickups/        - placeholder PNG files (used directly by the HUD / items: edit them in place)
//   Drawn In Code/        - sprites the game draws itself at runtime (boon icons, gong, gates, crowns, button icons,
//                           FX rings, panels...). Each one passes through AIArt.Use(name, sprite):
//                             - in the editor, the first time it's drawn it's saved there as <name>.png (so new
//                               placeholders keep showing up in the folder on their own)
//                             - if that PNG exists, the game uses IT instead of the drawn one: same pivot, same
//                               pixels-per-unit, same 9-slice border - so just paint over it. Any size works: it's
//                               scaled by the same pixels-per-unit, so double the canvas = double the size in game.
//                           Frame strips (UseFrames) are saved as one horizontal strip, frames side by side.
// Delete a PNG to get the drawn placeholder back (and it gets re-exported).
public static class AIArt
{
    public const string Folder = "AI Placeholders/Drawn In Code/";
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>();

    // file-safe: letters, digits, _ and - only
    private static string Clean(string name)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in name) sb.Append(char.IsLetterOrDigit(c) && c < 128 || c == '_' || c == '-' ? c : c == ' ' ? '_' : 'x');
        return sb.ToString();
    }

    public static Sprite Use(string name, Sprite generated)
    {
        if (string.IsNullOrEmpty(name) || generated == null) return generated;
        name = Clean(name);
        if (cache.TryGetValue(name, out Sprite s) && s != null) return s;
        Texture2D custom = Resources.Load<Texture2D>(Folder + name);
        if (custom != null)
        {
            custom.filterMode = FilterMode.Point;
            Vector2 pivot = new Vector2(generated.pivot.x / generated.rect.width, generated.pivot.y / generated.rect.height);
            // the 9-slice border scales with the picture (a 2x redraw keeps its frame 2x thick)
            Vector4 border = generated.border * (custom.width / Mathf.Max(1f, generated.rect.width));
            s = Sprite.Create(custom, new Rect(0, 0, custom.width, custom.height), pivot, generated.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            s.name = name;
        }
        else
        {
            Export(name, generated);
            s = generated;
        }
        cache[name] = s;
        return s;
    }

    public static Sprite[] UseFrames(string name, Sprite[] generated)
    {
        if (string.IsNullOrEmpty(name) || generated == null || generated.Length == 0 || generated[0] == null) return generated;
        name = Clean(name);
        if (frameCache.TryGetValue(name, out Sprite[] f) && f != null) return f;
        Texture2D custom = Resources.Load<Texture2D>(Folder + name);
        if (custom != null)
        {
            custom.filterMode = FilterMode.Point;
            int n = generated.Length, fw = custom.width / n;
            f = new Sprite[n];
            for (int i = 0; i < n; i++)
            {
                Sprite g = generated[i];
                Vector2 pivot = new Vector2(g.pivot.x / g.rect.width, g.pivot.y / g.rect.height);
                f[i] = Sprite.Create(custom, new Rect(i * fw, 0, fw, custom.height), pivot, g.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
        }
        else
        {
            ExportStrip(name, generated);
            f = generated;
        }
        frameCache[name] = f;
        return f;
    }

    // ---------------------------------------------------------------- export (editor only)
    private static void Export(string name, Sprite s)
    {
#if UNITY_EDITOR
        string path = Path(name);
        if (System.IO.File.Exists(path)) return;
        Color32[] px = Read(s, out int w, out int h);
        if (px == null) return;
        Write(path, px, w, h);
#endif
    }

    private static void ExportStrip(string name, Sprite[] frames)
    {
#if UNITY_EDITOR
        string path = Path(name);
        if (System.IO.File.Exists(path)) return;
        int fw = 0, fh = 0;
        var parts = new List<Color32[]>();
        foreach (Sprite s in frames)
        {
            Color32[] p = Read(s, out int w, out int h);
            if (p == null) return;
            fw = Mathf.Max(fw, w); fh = Mathf.Max(fh, h);
            parts.Add(p);
        }
        var all = new Color32[fw * frames.Length * fh];
        for (int i = 0; i < frames.Length; i++)
        {
            int w = (int)frames[i].rect.width, h = (int)frames[i].rect.height;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    all[y * fw * frames.Length + i * fw + x] = parts[i][y * w + x];
        }
        Write(path, all, fw * frames.Length, fh);
#endif
    }

#if UNITY_EDITOR
    private static string Path(string name) => "Assets/Resources/" + Folder + name + ".png";

    private static void Write(string path, Color32[] px, int w, int h)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        catch (System.Exception e) { Debug.LogWarning("AIArt: couldn't export " + path + ": " + e.Message); }
    }

    // The sprite's own pixels, even from a texture that isn't readable (copied through a render texture)
    private static Color32[] Read(Sprite s, out int w, out int h)
    {
        Rect r = s.rect;
        w = (int)r.width; h = (int)r.height;
        Texture2D tex = s.texture;
        try
        {
            Color32[] all;
            int tw = tex.width, th = tex.height;
            if (tex.isReadable) all = tex.GetPixels32();
            else
            {
                RenderTexture rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                RenderTexture prev = RenderTexture.active;
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(tw, th, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
                copy.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                all = copy.GetPixels32();
                Object.DestroyImmediate(copy);
            }
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = all[((int)r.y + y) * tw + (int)r.x + x];
            return px;
        }
        catch { return null; }
    }
#endif
}
