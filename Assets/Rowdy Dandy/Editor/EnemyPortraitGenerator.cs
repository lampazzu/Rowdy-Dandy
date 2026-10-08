using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Builds the enemy head portraits used by the kill feed and Rowdy Notes: Assets/Resources/EnemyPortraits/<id>.png.
// For each enemy it takes the first frame of its Idle clip (else Walk / Move / Fly...), keeps the biggest solid
// shape (drops detached FX, arrows...), finds the head and crops a square around it - every portrait is framed the
// same way so they all read at the same size, like the cat faces.
//   Upright enemies (gnolls, riders...)  -> head = top of the body
//   Beasts (wolves, shark, birds...)     -> head = the leading end of the upper body
// It also writes <id>_anim.png (every frame of that clip, cropped with the same head box, side by side) and
// <id>_anim.txt (each frame's duration in seconds) - Rowdy Notes plays those as animated portraits.
// Cats get one too (cat_Wig, cat_Samurai: the whole cat). Runs by itself once when portraits are missing; re-run with Tools > Rowdy Dandy > Generate Enemy Portraits after
// tweaking the table below. Check Generated/EnemyPortraits_Preview.png (frame + chosen box, then the crop).
[InitializeOnLoad]
public static class EnemyPortraitGenerator
{
    private const string OutputFolder = "Assets/Resources/EnemyPortraits";
    private const string PreviewPath = "Assets/Rowdy Dandy/HUD and UI/Generated/EnemyPortraits_Preview.png";
    private const string Prefabs = "Assets/Resources/Enemies Prefab/";

    private enum Mode { Top, Front, Bottom, LeftEnd, Whole }

    private class Source
    {
        public string id, path;     // path: an enemy prefab, or an .anim clip
        public Mode mode;
        public float size;          // crop size as a fraction of the body height
        public float nudgeX, nudgeY; // shift the box, fraction of the crop size (+x = towards the face side / right)
        public RectInt? manual;     // exact box in the frame's pixels (x, y from bottom-left, w = h), overrides detection
        public int forceSide;       // Front mode: -1 / +1 = the face is on the left / right (0 = guess)
        public Color tint = Color.white; // multiplied into the result (the Moonbound Elder is a violet Big Wolf)
        public Source(string id, string path, Mode mode, float size, float nudgeX = 0f, float nudgeY = 0f)
        { this.id = id; this.path = path; this.mode = mode; this.size = size; this.nudgeX = nudgeX; this.nudgeY = nudgeY; }
    }

    private static readonly Source[] Sources =
    {
        // Crabby: the blue shell is on top, its orange head/face is the bottom part
        new Source("crabby", Prefabs + "Crabby.prefab", Mode.Bottom, 0.75f),
        // Gnolls: a bigger box with more room above, so the red eye, teeth and beige muzzle all fit
        new Source("gnollwarrior", Prefabs + "Enemy_GnollWarrior.prefab", Mode.Top, 0.44f, 0f, 0.08f),
        new Source("gnollarcher", Prefabs + "Enemy_GnollArcher.prefab", Mode.Top, 0.44f, 0f, 0.08f),
        new Source("gnollbomber", Prefabs + "Enemy_GnollBomber.prefab", Mode.Top, 0.42f, 0.2f, 0.08f),
        new Source("horserider", Prefabs + "Enemy_HorseRider.prefab", Mode.Top, 0.3f),
        // Wolves: the head is the top-left part; centering on the body only showed the right shoulder
        new Source("transformwolf", Prefabs + "Enemy_TransformWolf.prefab", Mode.Top, 0.36f, -0.55f, 0.08f),
        new Source("sharkwolf", Prefabs + "SharkWolf.prefab", Mode.Front, 0.55f),
        new Source("statue", Prefabs + "Breakables/RDR_WolfStatues_A.prefab", Mode.Top, 0.45f),
        new Source("watervivarider", Prefabs + "Enemy_WaterVivaRider.prefab", Mode.Top, 0.2f),
        new Source("wereknight", Prefabs + "Enemy_WereKnight.prefab", Mode.Top, 0.34f),
        new Source("werefast", Prefabs + "Enemy_Werefast.prefab", Mode.Front, 0.45f),
        new Source("bigwolf", Prefabs + "Enemy_BigWerewolf.prefab", Mode.Top, 0.4f, -0.5f, 0.08f),
        new Source("megacreature", Prefabs + "Enemy_MegaCreature.prefab", Mode.Front, 0.45f),
        new Source("pelican", Prefabs + "Neutral_Pelican.prefab", Mode.Front, 1.5f, -0.1f) { forceSide = -1 },
        // Manta: start from its left end (the head), leave the tail out
        new Source("mantaray", Prefabs + "Neutral_MantaRay.prefab", Mode.LeftEnd, 1.5f),
        // Pelich: wide enough for the whole yellow beak (it points left) up to the back of the head
        new Source("pelich", "Assets/Rowdy Dandy/Enemies/Pelich Anus/PelichIdle.anim", Mode.Top, 0.62f, -0.24f, 0.1f),
        // Cats (Rowdy Notes > Cats): the whole cat
        new Source("cat_Wig", "Assets/Resources/Interactables/Wig.prefab", Mode.Whole, 1f),
        new Source("cat_Samurai", "Assets/Resources/Interactables/SamuraiCat.prefab", Mode.Whole, 1f),
    };

    private static readonly string[] ClipPreference = { "idle", "walk", "moving", "move", "fly", "swim", "run" };
    private static readonly string[] ClipAvoid = { "death", "dead", "dying", "hurt", "hit", "attack", "jump" };

    static EnemyPortraitGenerator()
    {
        EditorApplication.delayCall += AutoGenerate;
    }

    // Bump after editing the table above and the portraits rebuild on the next compile
    private const int TableVersion = 7;
    private const string VersionKey = "RowdyDandy.EnemyPortraits.Version";

    private static void AutoGenerate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool stale = EditorPrefs.GetInt(VersionKey, 0) != TableVersion;
        foreach (Source s in Sources)
            if (!File.Exists(Path.Combine(OutputFolder, s.id + ".png"))) stale = true;
        if (!stale) return;
        Generate();
        EditorPrefs.SetInt(VersionKey, TableVersion);
    }

    [MenuItem("Tools/Rowdy Dandy/Generate Enemy Portraits")]
    public static void Generate()
    {
        Directory.CreateDirectory(OutputFolder);
        var previews = new List<Texture2D>();
        var written = new List<string>();

        foreach (Source source in Sources)
        {
            try
            {
                tintNow = source.tint;
                Sprite sprite = FindSprite(source.path, out string from, out AnimationClip clip, out EditorCurveBinding? binding);
                if (sprite == null) { Debug.LogWarning($"Enemy portraits: no sprite found for {source.id} ({source.path})"); continue; }

                Color32[] frame = ReadSprite(sprite, out int w, out int h);
                if (frame == null) { Debug.LogWarning($"Enemy portraits: couldn't read {sprite.name} for {source.id}"); continue; }

                RectInt box = source.manual ?? FindHead(frame, w, h, source);
                Color32[] crop = Crop(frame, w, h, box);
                string file = Path.Combine(OutputFolder, source.id + ".png");
                var tex = new Texture2D(box.width, box.height, TextureFormat.RGBA32, false);
                tex.SetPixels32(crop);
                tex.Apply();
                File.WriteAllBytes(file, tex.EncodeToPNG());
                written.Add(file.Replace('\\', '/'));
                previews.Add(MakePreview(frame, w, h, box, tex));
                Object.DestroyImmediate(tex);
                if (clip != null && binding != null) WriteAnimation(source.id, clip, binding.Value, sprite, box, written);
                Debug.Log($"Enemy portraits: {source.id} <- {sprite.name} ({from}), box {box.x},{box.y} {box.width}px");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Enemy portraits: {source.id} failed: {e.Message}");
            }
        }

        AssetDatabase.Refresh();
        foreach (string file in written) ConfigureImporter(file);
        SavePreviewSheet(previews);
        Debug.Log($"Enemy portraits: wrote {written.Count} portraits to {OutputFolder}. Preview: {PreviewPath}");
    }

    // ---------------------------------------------------------------- finding the frame
    private static Sprite FindSprite(string path, out string from, out AnimationClip usedClip, out EditorCurveBinding? usedBinding)
    {
        from = "";
        usedClip = null;
        usedBinding = null;
        if (path.EndsWith(".anim"))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            from = clip != null ? clip.name : "";
            if (clip == null) return null;
            usedBinding = SpriteBinding(clip, null);
            usedClip = usedBinding != null ? clip : null;
            return FirstSprite(clip, null);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return null;
        EnemyHealth health = prefab.GetComponentInChildren<EnemyHealth>(true);
        Transform body = health != null ? health.transform : prefab.transform;
        SpriteRenderer renderer = body.GetComponent<SpriteRenderer>() ?? body.GetComponentInChildren<SpriteRenderer>(true);
        Animator animator = body.GetComponentInParent<Animator>(true) ?? body.GetComponentInChildren<Animator>(true);

        if (animator != null && animator.runtimeAnimatorController != null && renderer != null)
        {
            string rendererPath = AnimationUtility.CalculateTransformPath(renderer.transform, animator.transform);
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            foreach (string wanted in ClipPreference)
            {
                foreach (AnimationClip clip in clips)
                {
                    if (clip == null) continue;
                    string n = clip.name.ToLowerInvariant();
                    if (!n.Contains(wanted) || Avoid(n)) continue;
                    Sprite s = FirstSprite(clip, rendererPath);
                    if (s != null) { from = clip.name; usedClip = clip; usedBinding = SpriteBinding(clip, rendererPath); return s; }
                }
            }
        }
        from = "default sprite";
        return renderer != null ? renderer.sprite : null;
    }

    private static bool Avoid(string clipName)
    {
        foreach (string a in ClipAvoid) if (clipName.Contains(a)) return true;
        return false;
    }

    private static EditorCurveBinding? SpriteBinding(AnimationClip clip, string rendererPath)
    {
        EditorCurveBinding? fallback = null;
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;
            if (rendererPath == null || binding.path == rendererPath) return binding;
            if (fallback == null) fallback = binding;
        }
        return fallback;
    }

    // Every frame of the clip cropped with the head box (moved with each frame's pivot), as one strip + durations
    private static void WriteAnimation(string id, AnimationClip clip, EditorCurveBinding binding, Sprite first, RectInt box, List<string> written)
    {
        const int MaxFrames = 24;
        ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        var frames = new List<(Sprite sprite, float time)>();
        foreach (ObjectReferenceKeyframe key in keys)
        {
            if (!(key.value is Sprite s)) continue;
            if (frames.Count > 0 && frames[frames.Count - 1].sprite == s) continue;
            frames.Add((s, key.time));
            if (frames.Count >= MaxFrames) break;
        }
        string strip = Path.Combine(OutputFolder, id + "_anim.png");
        string timing = Path.Combine(OutputFolder, id + "_anim.txt");
        if (frames.Count < 2)
        {
            if (File.Exists(strip)) AssetDatabase.DeleteAsset(strip.Replace('\\', '/'));
            if (File.Exists(timing)) AssetDatabase.DeleteAsset(timing.Replace('\\', '/'));
            return;
        }

        int size = box.width;
        var sheet = new Texture2D(size * frames.Count, size, TextureFormat.RGBA32, false);
        var all = new Color32[size * frames.Count * size];
        var durations = new List<string>();
        float fallbackStep = clip.frameRate > 0f ? 1f / clip.frameRate : 0.1f;
        for (int i = 0; i < frames.Count; i++)
        {
            Color32[] px = ReadSprite(frames[i].sprite, out int w, out int h);
            if (px != null)
            {
                Vector2 shift = frames[i].sprite.pivot - first.pivot;
                var b = new RectInt(box.x + Mathf.RoundToInt(shift.x), box.y + Mathf.RoundToInt(shift.y), size, size);
                Color32[] crop = Crop(px, w, h, b);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        all[y * size * frames.Count + i * size + x] = crop[y * size + x];
            }
            float next = i + 1 < frames.Count ? frames[i + 1].time : clip.length;
            float d = next - frames[i].time;
            if (d <= 0.001f) d = fallbackStep;
            durations.Add(d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
        }
        sheet.SetPixels32(all);
        sheet.Apply();
        File.WriteAllBytes(strip, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        File.WriteAllText(timing, string.Join(",", durations));
        written.Add(strip.Replace('\\', '/'));
    }

    private static Sprite FirstSprite(AnimationClip clip, string rendererPath)
    {
        EditorCurveBinding? fallback = null;
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;
            if (rendererPath == null || binding.path == rendererPath) { fallback = binding; break; }
            if (fallback == null) fallback = binding;
        }
        if (fallback == null) return null;
        ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, fallback.Value);
        foreach (ObjectReferenceKeyframe key in keys) if (key.value is Sprite s) return s;
        return null;
    }

    // Pixels of the sprite's rect, read straight from the source PNG (works whatever the import settings are)
    private static Color32[] ReadSprite(Sprite sprite, out int w, out int h)
    {
        w = h = 0;
        string texturePath = AssetDatabase.GetAssetPath(sprite.texture);
        if (string.IsNullOrEmpty(texturePath) || !File.Exists(texturePath)) return null;
        var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!raw.LoadImage(File.ReadAllBytes(texturePath))) { Object.DestroyImmediate(raw); return null; }

        // The imported texture may be downsized (max size); then the rect is in its pixels, not the PNG's
        Rect r = sprite.rect;
        bool rectFitsImported = r.xMax <= sprite.texture.width + 0.5f && r.yMax <= sprite.texture.height + 0.5f;
        float scale = rectFitsImported ? (float)raw.width / sprite.texture.width : 1f;
        int x0 = Mathf.RoundToInt(r.x * scale), y0 = Mathf.RoundToInt(r.y * scale);
        w = Mathf.Clamp(Mathf.RoundToInt(r.width * scale), 1, raw.width - x0);
        h = Mathf.Clamp(Mathf.RoundToInt(r.height * scale), 1, raw.height - y0);
        Color32[] all = raw.GetPixels32();
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = all[(y0 + y) * raw.width + x0 + x];
        Object.DestroyImmediate(raw);
        return px;
    }

    // ---------------------------------------------------------------- head detection
    private static RectInt FindHead(Color32[] px, int w, int h, Source source)
    {
        if (source.mode == Mode.Whole)
        {
            // Every opaque pixel (cats: the whole cat, sword and all), in a square with a pixel of air
            int x0 = w, x1 = -1, y0 = h, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a >= 60) { x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            if (x1 < 0) return new RectInt(0, 0, Mathf.Min(w, h), Mathf.Min(w, h));
            int side = Mathf.Max(x1 - x0, y1 - y0) + 3;
            return new RectInt((x0 + x1 + 1) / 2 - side / 2, (y0 + y1 + 1) / 2 - side / 2, side, side);
        }
        bool[] body = LargestShape(px, w, h);
        int minX = w, maxX = -1, minY = h, maxY = -1;
        var rowCount = new int[h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (body[y * w + x])
                {
                    rowCount[y]++;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        if (maxX < 0) return new RectInt(0, 0, Mathf.Min(w, h), Mathf.Min(w, h));

        int bodyH = maxY - minY + 1;
        int size = Mathf.Clamp(Mathf.RoundToInt(source.size * bodyH), 10, Mathf.Max(10, Mathf.Max(w, h)));
        float cx, cy;

        if (source.mode == Mode.Bottom || source.mode == Mode.LeftEnd)
        {
            // Bottom: centred on the body's lowest band. LeftEnd: starts at the left tip, centred on that slice's height.
            float sx = 0f, sy = 0f; int n = 0;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (!body[y * w + x]) continue;
                    bool inBand = source.mode == Mode.Bottom ? y <= minY + size * 0.75f : x <= minX + size;
                    if (inBand) { sx += x; sy += y; n++; }
                }
            if (source.mode == Mode.Bottom)
            {
                cx = n > 0 ? sx / n : (minX + maxX) / 2f;
                cy = minY - 1 + size / 2f;
            }
            else
            {
                cx = minX - 1 + size / 2f;
                cy = n > 0 ? sy / n : (minY + maxY) / 2f;
            }
            cx += source.nudgeX * size;
        }
        else if (source.mode == Mode.Top)
        {
            // Highest row that's actually body (skips a stray hair of weapon / ear tip)
            int widest = 0;
            foreach (int c in rowCount) widest = Mathf.Max(widest, c);
            int top = maxY;
            int threshold = Mathf.Max(2, Mathf.RoundToInt(widest * 0.2f));
            while (top > minY && rowCount[top] < threshold) top--;

            // Middle of the body in the band just under the top = where the head is
            int bandBottom = top - Mathf.RoundToInt(size * 0.75f);
            float sum = 0f; int n = 0;
            for (int y = Mathf.Max(minY, bandBottom); y <= top; y++)
                for (int x = minX; x <= maxX; x++)
                    if (body[y * w + x]) { sum += x; n++; }
            cx = n > 0 ? sum / n : (minX + maxX) / 2f;
            cy = top + 2 - size / 2f; // a couple of pixels of air above the head
            cx += source.nudgeX * size;
        }
        else
        {
            // Which end leads: the side with more mass up high (the raised head) wins
            int upperFrom = minY + Mathf.RoundToInt(bodyH * 0.4f);
            int third = Mathf.Max(1, (maxX - minX + 1) / 3);
            int left = 0, right = 0;
            for (int y = upperFrom; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (body[y * w + x]) { if (x < minX + third) left++; else if (x > maxX - third) right++; }
            int side = source.forceSide != 0 ? source.forceSide : right >= left ? 1 : -1;

            int extreme = side > 0 ? maxX : minX;
            float sx = 0f, sy = 0f; int n = 0;
            for (int y = upperFrom; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (body[y * w + x] && Mathf.Abs(x - extreme) <= size * 0.8f) { sx += x; sy += y; n++; }
            cx = n > 0 ? sx / n : extreme - side * size / 2f;
            cy = n > 0 ? sy / n : maxY - size / 2f;
            cx += side * (size * 0.12f + source.nudgeX * size); // lean towards the snout
        }
        cy += source.nudgeY * size;

        return new RectInt(Mathf.RoundToInt(cx - size / 2f), Mathf.RoundToInt(cy - size / 2f), size, size);
    }

    // The biggest 4-connected blob of opaque pixels
    private static bool[] LargestShape(Color32[] px, int w, int h)
    {
        var label = new int[w * h];
        var best = new bool[w * h];
        int bestCount = 0, next = 0;
        var stack = new Stack<int>();
        var members = new List<int>();
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a < 60 || label[i] != 0) continue;
            next++;
            members.Clear();
            stack.Push(i);
            label[i] = next;
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                members.Add(p);
                int x = p % w, y = p / w;
                TryPush(x - 1, y); TryPush(x + 1, y); TryPush(x, y - 1); TryPush(x, y + 1);
            }
            if (members.Count > bestCount)
            {
                bestCount = members.Count;
                System.Array.Clear(best, 0, best.Length);
                foreach (int m in members) best[m] = true;
            }
        }
        return best;

        void TryPush(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int q = y * w + x;
            if (label[q] != 0 || px[q].a < 60) return;
            label[q] = next;
            stack.Push(q);
        }
    }

    private static Color tintNow = Color.white;

    private static Color32[] Crop(Color32[] px, int w, int h, RectInt box)
    {
        Color32[] r = CropRaw(px, w, h, box);
        if (tintNow != Color.white)
            for (int i = 0; i < r.Length; i++) r[i] = (Color)r[i] * tintNow;
        return r;
    }

    private static Color32[] CropRaw(Color32[] px, int w, int h, RectInt box)
    {
        var result = new Color32[box.width * box.height];
        for (int y = 0; y < box.height; y++)
            for (int x = 0; x < box.width; x++)
            {
                int sx = box.x + x, sy = box.y + y;
                result[y * box.width + x] = sx >= 0 && sy >= 0 && sx < w && sy < h ? px[sy * w + sx] : new Color32(0, 0, 0, 0);
            }
        return result;
    }

    // ---------------------------------------------------------------- import + preview
    private static void ConfigureImporter(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 64;
        importer.maxTextureSize = 8192; // animation strips get wide
        importer.SaveAndReimport();
    }

    // Frame (x2) with the chosen box in pink, then the portrait (x4)
    private static Texture2D MakePreview(Color32[] frame, int w, int h, RectInt box, Texture2D crop)
    {
        const int a = 2, b = 4, pad = 6;
        int width = w * a + pad + crop.width * b, height = Mathf.Max(h * a, crop.height * b);
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var px = new Color32[width * height];
        Color32 bg = new Color32(0x2A, 0x0A, 0x2E, 0xFF), pink = new Color32(0xFF, 0x39, 0xC0, 0xFF);
        for (int i = 0; i < px.Length; i++) px[i] = bg;
        for (int y = 0; y < h * a; y++)
            for (int x = 0; x < w * a; x++)
            {
                Color32 c = frame[(y / a) * w + x / a];
                if (c.a > 60) px[y * width + x] = c;
                int fx = x / a, fy = y / a;
                bool edge = (fx == box.x || fx == box.xMax - 1) && fy >= box.y && fy < box.yMax ||
                            (fy == box.y || fy == box.yMax - 1) && fx >= box.x && fx < box.xMax;
                if (edge) px[y * width + x] = pink;
            }
        Color32[] cp = crop.GetPixels32();
        for (int y = 0; y < crop.height * b; y++)
            for (int x = 0; x < crop.width * b; x++)
            {
                Color32 c = cp[(y / b) * crop.width + x / b];
                if (c.a > 60) px[y * width + w * a + pad + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static void SavePreviewSheet(List<Texture2D> previews)
    {
        if (previews.Count == 0) return;
        const int gap = 8;
        int width = 0, height = gap;
        foreach (Texture2D p in previews) { width = Mathf.Max(width, p.width); height += p.height + gap; }
        width += gap * 2;
        var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var px = new Color32[width * height];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0x14, 0x06, 0x1A, 0xFF);
        int yTop = height - gap;
        foreach (Texture2D p in previews)
        {
            Color32[] src = p.GetPixels32();
            int y0 = yTop - p.height;
            for (int y = 0; y < p.height; y++)
                for (int x = 0; x < p.width; x++)
                    px[(y0 + y) * width + gap + x] = src[y * p.width + x];
            yTop = y0 - gap;
            Object.DestroyImmediate(p);
        }
        sheet.SetPixels32(px);
        sheet.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
        File.WriteAllBytes(PreviewPath, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        AssetDatabase.ImportAsset(PreviewPath);
    }
}
