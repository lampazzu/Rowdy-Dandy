using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Editor helpers for building The Frontier (the island past Pelich):
//   Tools > Rowdy Dandy > Frontier > Survey          writes Temp/frontier_survey.txt (grid, tilemaps, key objects)
//   Tools > Rowdy Dandy > Frontier > Render Region   renders a world rectangle to Temp/frontier_render.png
// A one-shot command can also be dropped in Temp/rd_cmd.txt: it runs once after the next compile and the file is deleted.
//   survey | render x0 y0 x1 y1 [file] | list x0 y0 x1 y1 | build | play | prefsrestore <reg export file>
[InitializeOnLoad]
public static class FrontierTools
{
    private const string CmdFile = "Temp/rd_cmd.txt";

    static FrontierTools()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(CmdFile)) return;
            string cmd = File.ReadAllText(CmdFile).Trim();
            File.Delete(CmdFile);
            Run(cmd);
        };
    }

    public static void Run(string cmd)
    {
        string[] p = cmd.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (p.Length == 0) return;
        try
        {
            switch (p[0])
            {
                case "survey": Survey(); break;
                case "render": Render(float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3]), float.Parse(p[4]), p.Length > 5 ? p[5] : "frontier_render.png"); break;
                case "build": FrontierBuilder.Build(); break;
                case "play": EditorApplication.isPlaying = true; break;
                case "prefsrestore": RestorePrefs(cmd.Substring(13).Trim()); break;
                case "list": List(float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3]), float.Parse(p[4])); break;
            }
        }
        catch (System.Exception e) { File.WriteAllText("Temp/rd_cmd_error.txt", e.ToString()); }
    }

    [MenuItem("Tools/Rowdy Dandy/Frontier/Survey")]
    public static void Survey()
    {
        var sb = new StringBuilder();
        foreach (Grid g in Object.FindObjectsByType<Grid>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.AppendLine($"GRID {g.name} cell {g.cellSize} pos {g.transform.position} scale {g.transform.lossyScale}");
        foreach (Tilemap tm in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            tm.CompressBounds();
            BoundsInt b = tm.cellBounds;
            Vector3 min = tm.CellToWorld(b.min), max = tm.CellToWorld(b.max);
            var r = tm.GetComponent<TilemapRenderer>();
            sb.AppendLine($"TILEMAP {Path(tm.transform)} layer {LayerMask.LayerToName(tm.gameObject.layer)} tag {tm.tag} cells {b.min}..{b.max} world ({min.x:F1},{min.y:F1})..({max.x:F1},{max.y:F1}) tiles {tm.GetUsedTilesCount()} sort {(r != null ? r.sortingLayerName + "/" + r.sortingOrder : "-")} col {(tm.GetComponent<Collider2D>() != null)}");
        }
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = t.name;
            if (n.Contains("Pelich") || n.Contains("Respawn") || n.Contains("Checkpoint") || n.Contains("Confin") || n.Contains("Bound") || n.Contains("Water")
                || n.Contains("WaveRandomizer") || n.Contains("Rowdy Dandy") || n.Contains("Camera") || n.Contains("CM ") || n.Contains("Spawner"))
                if (t.parent == null || t.parent.parent == null || n.Contains("Respawn") || n.Contains("Checkpoint"))
                    sb.AppendLine($"OBJ {Path(t)} at {t.position} active {t.gameObject.activeInHierarchy}");
        }
        foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (c is PolygonCollider2D pc && (c.name.Contains("Confin") || c.name.Contains("Bound") || c.name.Contains("Camera")))
                sb.AppendLine($"CONFINER {Path(c.transform)} bounds {c.bounds.min}..{c.bounds.max}");
        // everything right of x 200 at the top level, with renderer bounds
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) continue;
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            sb.AppendLine($"ROOT {root.name} renderers {rs.Length} bounds ({b.min.x:F0},{b.min.y:F0})..({b.max.x:F0},{b.max.y:F0})");
        }
        File.WriteAllText("Temp/frontier_survey.txt", sb.ToString());
        Debug.Log("Frontier survey written to Temp/frontier_survey.txt");
    }

    // Every renderer whose bounds centre is inside the rect: path, components, bounds, sorting
    public static void List(float x0, float y0, float x1, float y1)
    {
        var sb = new StringBuilder();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Vector3 c = r.bounds.center;
            if (c.x < x0 || c.x > x1 || c.y < y0 || c.y > y1) continue;
            string comps = string.Join(",", r.GetComponents<Component>().Select(k => k == null ? "null" : k.GetType().Name).Where(n => n != "Transform"));
            string pf = PrefabUtility.IsPartOfPrefabInstance(r.gameObject) ? " PREFAB:" + System.IO.Path.GetFileNameWithoutExtension(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject)) : "";
            string sort = r.sortingLayerName + "/" + r.sortingOrder;
            sb.AppendLine(string.Format(inv, "{0} | {1} | c({2:F1},{3:F1}) s({4:F1},{5:F1}) | {6} | act {7}{8}", Path(r.transform), comps, c.x, c.y, r.bounds.size.x, r.bounds.size.y, sort, r.gameObject.activeInHierarchy, pf));
        }
        File.WriteAllText("Temp/frontier_list.txt", sb.ToString());
    }

    // Puts the PlayerPrefs back from a 'reg export' of the project's key (the editor caches prefs, so this goes through Unity)
    public static void RestorePrefs(string regFile)
    {
        string text = File.ReadAllText(regFile, Encoding.Unicode).Replace("\\\r\n  ", "");
        PlayerPrefs.DeleteAll();
        int n = 0;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (!line.StartsWith("\"")) continue;
            int q = line.IndexOf("\"=");
            string name = line.Substring(1, q - 1);
            string value = line.Substring(q + 2);
            int h = name.LastIndexOf("_h");
            if (h > 0) name = name.Substring(0, h);
            if (value.StartsWith("dword:")) PlayerPrefs.SetInt(name, unchecked((int)System.Convert.ToUInt32(value.Substring(6), 16)));
            else if (value.StartsWith("hex(4):"))
            {
                byte[] bytes = value.Substring(7).Split(',').Select(s => System.Convert.ToByte(s.Trim(), 16)).ToArray();
                PlayerPrefs.SetFloat(name, (float)System.BitConverter.ToDouble(bytes, 0));
            }
            else if (value.StartsWith("hex:"))
            {
                byte[] bytes = value.Substring(4).Split(',').Where(s => s.Trim().Length > 0).Select(s => System.Convert.ToByte(s.Trim(), 16)).ToArray();
                int len = bytes.Length > 0 && bytes[bytes.Length - 1] == 0 ? bytes.Length - 1 : bytes.Length;
                PlayerPrefs.SetString(name, Encoding.UTF8.GetString(bytes, 0, len));
            }
            else continue;
            n++;
        }
        PlayerPrefs.Save();
        File.WriteAllText("Temp/prefs_restored.txt", n + " prefs restored");
    }

    private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

    [MenuItem("Tools/Rowdy Dandy/Frontier/Render Region")]
    private static void RenderMenu() => Render(230f, -12f, 330f, 20f, "frontier_render.png");

    // Renders the world rectangle (x0,y0)-(x1,y1) at 16 px per unit
    public static void Render(float x0, float y0, float x1, float y1, string file)
    {
        const float ppu = 16f;
        int w = Mathf.CeilToInt((x1 - x0) * ppu), h = Mathf.CeilToInt((y1 - y0) * ppu);
        var go = new GameObject("Frontier Render Cam") { hideFlags = HideFlags.HideAndDontSave };
        Camera main = Camera.main;
        var cam = go.AddComponent<Camera>();
        if (main != null) cam.CopyFrom(main);
        cam.orthographic = true;
        cam.orthographicSize = (y1 - y0) / 2f;
        cam.aspect = (x1 - x0) / (y1 - y0);
        cam.transform.position = new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, -50f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        cam.cullingMask = ~0;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1000f;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("Temp/" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
        Debug.Log("Rendered " + file + " " + w + "x" + h);
    }
}
