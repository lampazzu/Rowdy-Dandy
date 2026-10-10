using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The user's own flowers and mushrooms (Scenery/Assets/RDR_Flowers.png, 3 frames of 32 x 32 - BoonArt.flora) for the
// Stephmoss boons: Spore Step pops mushrooms out of the ground, Bloom grows a flower.
//   - each frame is trimmed to its drawn pixels, feet at the bottom, true 64 px per unit (never scaled up)
//   - mushrooms come in three looks: the art's own colours, a toxic green and a violet hue shift (stronger colour,
//     a bit brighter; the dark outline pixels stay as drawn)
//   - Pop() makes one stand out: a 1 px glowing outline in its colour and a small soft light
public static class FloraArt
{
    public const int PurpleFlower = 0, Mushrooms = 1, BlueFlower = 2;
    public enum Look { Natural, Toxic, Violet }

    public static readonly Color ToxicGlow = new Color(0.72f, 1f, 0.3f);
    public static readonly Color VioletGlow = new Color(0.8f, 0.5f, 1f);

    private static Color32[] pixels;
    private static int texW, texH;
    private static bool loaded;
    private static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();

    // A frame of the sheet in one of the looks (null if the art is missing: callers keep their old pixel sprite)
    public static Sprite Get(int frame, Look look = Look.Natural)
    {
        int key = frame * 10 + (int)look;
        if (cache.TryGetValue(key, out Sprite s)) return s;
        s = Build(frame, look);
        cache[key] = s;
        return s;
    }

    public static Sprite RandomMushroom(out Color glow)
    {
        float r = Random.value;
        Look look = r < 0.34f ? Look.Natural : r < 0.72f ? Look.Toxic : Look.Violet;
        glow = look == Look.Violet ? VioletGlow : ToxicGlow;
        return Get(Mushrooms, look);
    }

    public static Sprite RandomFlower(out Color glow)
    {
        bool blue = Random.value < 0.5f;
        glow = blue ? new Color(0.45f, 0.85f, 1f) : new Color(1f, 0.5f, 0.95f);
        return Get(blue ? BlueFlower : PurpleFlower);
    }

    // Glowing outline + a little light, both following the renderer
    public static void Pop(SpriteRenderer sr, Color glow, float light = 0.6f, float radius = 0.7f)
    {
        if (sr == null) return;
        SpriteOutline.Add(sr, new Color(glow.r, glow.g, glow.b, 0.9f), 1, -1);
        if (light <= 0f) return;
        var l = new GameObject("Glow").AddComponent<Light2D>();
        l.transform.SetParent(sr.transform, false);
        l.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        l.lightType = Light2D.LightType.Point;
        l.color = glow;
        l.intensity = light;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerRadius = 0.02f;
    }

    // ---------------------------------------------------------------- building the sprites
    private static bool Load()
    {
        if (loaded) return pixels != null;
        loaded = true;
        BoonArt art = BoonArt.Get;
        Texture2D tex = art != null ? art.flora : null;
        if (tex == null) return false;
        texW = tex.width; texH = tex.height;
        try
        {
            if (tex.isReadable) pixels = tex.GetPixels32();
            else
            {
                // not readable: read it back through a render texture
                RenderTexture rt = RenderTexture.GetTemporary(texW, texH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                RenderTexture prev = RenderTexture.active;
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, texW, texH), 0, 0);
                copy.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                pixels = copy.GetPixels32();
                Object.Destroy(copy);
            }
        }
        catch { pixels = null; }
        return pixels != null;
    }

    private static Sprite Build(int frame, Look look)
    {
        if (!Load()) return null;
        int size = texH; // square frames side by side
        int x0 = frame * size;
        if (x0 + size > texW) return null;

        // trim to the drawn pixels
        int minX = size, minY = size, maxX = -1, maxY = -1;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (pixels[y * texW + x0 + x].a > 0) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
        if (maxX < 0) return null;
        int w = maxX - minX + 1, h = maxY - minY + 1;

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = Recolor(pixels[(minY + y) * texW + x0 + minX + x], look);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Flora" + frame + look };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 64f);
    }

    private static Color32 Recolor(Color32 c, Look look)
    {
        if (c.a == 0 || look == Look.Natural) return c;
        Color.RGBToHSV(c, out float hh, out float s, out float v);
        if (v < 0.18f) return c; // the dark outline stays as drawn
        float hue = look == Look.Toxic ? 0.24f : 0.78f;
        // keep the art's light and shade, push the colour
        Color o = Color.HSVToRGB(Mathf.Repeat(hue + (hh - 0.08f) * 0.3f, 1f), Mathf.Clamp01(s * 1.25f + 0.15f), Mathf.Clamp01(v * 1.15f + 0.05f));
        o.a = c.a / 255f;
        return o;
    }
}
