using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Level editing helpers (Tools > Rowdy Dandy > Level Editing):
//   Organize Scenery Folders      - sorts Assets/Scenery into category folders (01 Terrain, 02 Rocks, 03 Vegetation...).
//                                   Moves go through the AssetDatabase, so every scene / prefab reference stays intact.
//                                   Safe to run again after adding new art: only files not in place yet move.
//   Make Decoration Clusters      - copies little groups of decoration from the open scene (things placed together:
//                                   palms + grass + rocks...) into prefabs in Assets/Scenery/00 Clusters/Small|Medium|Large.
//                                   The scene is not changed. Running it again replaces the old clusters.
//   Level Kit                     - a window with big thumbnails of every Scenery sprite / cluster by category. Click one
//                                   and click in the Scene view to place it (pixel-snapped, optional random flip),
//                                   or drag it in. Esc / right click stops placing.
//   Snap Selection To Pixels      - Ctrl+Alt+P: puts the selected objects on the 1/64 pixel grid.
public static class LevelEditingTools
{
    public const string Root = "Assets/Scenery";
    public const string ClusterRoot = "Assets/Scenery/00 Clusters";

    // ================================================================ organize the folders
    private static readonly (string pattern, string dest)[] PathRules =
    {
        (@"^RDR Assets/Tilemap/", "01 Terrain/Tilesets/Sand Tiles"),
        (@"^Old TileSet/Beach\.prefab$", "01 Terrain/Palettes"),
        (@"^Old TileSet/RD_SkyBox", "05 Sky And Backgrounds/Old Sky Boxes"),
        (@"^Old TileSet/", "01 Terrain/Old TileSet"),
        (@"^Asset Gimmicks/(Lake|LakeWave|RD_Lake|OceanWav|RD_OceanShore|New Material)", "07 Water/Lake And Shore"),
        (@"^Asset Gimmicks/(RD_Sun|SunShining|RDR_SkyBox)", "05 Sky And Backgrounds/Sun And Moon"),
        (@"^Assets/Interactable Assets/", "08 Interactable Plants"),
        (@"^Assets/Materials/", "07 Water/Lake And Shore"),
        (@"^RDR Assets/NPC MiniScene/Materials/", "04 Props/NPC Mini Scene/Materials"),
        (@"^RDR Assets/NPC MiniScene/", "04 Props/NPC Mini Scene"),
        (@"^RDR Assets/Water/", "07 Water/Animated Water"),
        (@"^Parallax/.*\.cs$", "10 Scripts"),
        (@"^Parallax/(RDR_Moon|Moon\.anim|RDR_SunV10|Sun\.anim)", "05 Sky And Backgrounds/Sun And Moon"),
        (@"^Parallax/(.*Fog|GreatFog)", "06 Atmosphere/Fog"),
        (@"^Parallax/", "05 Sky And Backgrounds/Parallax"),
        (@"^Map 3\.0/", "05 Sky And Backgrounds/Map Backgrounds"),
        (@"^Gloomy Forest/RockPack", "02 Rocks/Gloomy Forest"),
        (@"^Gloomy Forest/(Tree|HorrificTree|RDR_GloomyTrees)", "03 Vegetation/Gloomy Forest/Trees"),
        (@"^Gloomy Forest/(Bush|GrassPatch)", "03 Vegetation/Gloomy Forest/Bushes And Grass"),
        (@"^Gloomy Forest/Flower", "03 Vegetation/Gloomy Forest/Flowers"),
        (@"^Gloomy Forest/Crystal", "03 Vegetation/Gloomy Forest/Crystals"),
        (@"^Gloomy Forest/", "03 Vegetation/Gloomy Forest"),
        (@"\.cs$", "10 Scripts"),
        (@"\.physicsMaterial2D$", "09 Physics Materials"),
    };

    private static readonly (string pattern, string dest)[] NameRules =
    {
        (@"^RDR_TileSetSand", "01 Terrain/Tilesets"),
        (@"^(RDR_BigAssTile|RDR_TestNewTerrain|Sprite test|Map TEst|floresta elfica)", "01 Terrain/Big Pieces"),
        (@"^RDR_NewSlope", "01 Terrain/Slopes"),
        (@"^RDR_WoodenBridge", "01 Terrain/Bridges"),
        (@"^(RDR_MegaCliff|RDR_Cliffs|RDR_LightCLiff)", "01 Terrain/Cliffs"),
        (@"^(RDR_Earth|RDR_BackSand)", "01 Terrain/Earth Walls And Pillars"),
        (@"^RDR_SandPile", "01 Terrain/Sand Piles"),
        (@"^RDR_PointyRocks", "02 Rocks/Beach"),
        (@"^(Palmeira|RDR_Palmeira|PalmTree|RDR_BigPalmTree)", "03 Vegetation/Beach/Palm Trees"),
        (@"^(RDR_Vine|RDR_CipoVegetation|CipoVegetation)", "03 Vegetation/Beach/Vines"),
        (@"^(RDR_Vegetation|RDR_GrassPatch|RDR_OrangeGrassPatch|RDR_Flowers)", "03 Vegetation/Beach/Grass And Flowers"),
        (@"^(RDR_Checkpoint|checkpointanim)", "04 Props/Checkpoint"),
        (@"^(RDR_WolfSkull|RDR_Shadow)", "04 Props"),
        (@"^(RDR_SkyBox|RDR_NewSky|NotMyBeach)", "05 Sky And Backgrounds/Sky Boxes"),
        (@"^(RD_Sun|SunShiningV2)", "05 Sky And Backgrounds/Sun And Moon"),
        (@"^(RDR_Heatwave|RDR_Mist)", "06 Atmosphere/Heat And Mist"),
        (@"^(RDR_Fireflies|FireFlies)", "06 Atmosphere/Fireflies"),
        (@"^(RD_Lake|RD_OceanShore)", "07 Water/Lake And Shore"),
        (@"^RDR_BigAssWaterViva", "07 Water"),
    };

    private static readonly Regex Organized = new Regex(@"^\d\d ");

    private static string DestinationFor(string rel)
    {
        if (Organized.IsMatch(rel)) return null; // already in a category folder
        foreach (var r in PathRules) if (Regex.IsMatch(rel, r.pattern)) return r.dest;
        string name = Path.GetFileName(rel);
        foreach (var r in NameRules) if (Regex.IsMatch(name, r.pattern)) return r.dest;
        return "11 Unsorted";
    }

    [MenuItem("Tools/Rowdy Dandy/Level Editing/Organize Scenery Folders")]
    private static void OrganizeFolders()
    {
        var moves = new List<(string from, string to)>();
        foreach (string guid in AssetDatabase.FindAssets("", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path)) continue;
            string rel = path.Substring(Root.Length + 1);
            string dest = DestinationFor(rel);
            if (dest == null) continue;
            string to = Root + "/" + dest + "/" + Path.GetFileName(path);
            if (to != path && !moves.Any(m => m.from == path)) moves.Add((path, to));
        }
        if (moves.Count == 0) { EditorUtility.DisplayDialog("Organize Scenery Folders", "Everything is already in its folder.", "OK"); return; }
        string summary = string.Join("\n", moves.GroupBy(m => Path.GetDirectoryName(m.to).Replace('\\', '/').Substring(Root.Length + 1)).OrderBy(g => g.Key).Select(g => g.Count() + "  " + g.Key));
        if (!EditorUtility.DisplayDialog("Organize Scenery Folders", moves.Count + " files will move (references stay intact):\n\n" + summary, "Move them", "Cancel")) return;

        var log = new List<string>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var m in moves)
            {
                EnsureFolder(Path.GetDirectoryName(m.to).Replace('\\', '/'));
                string target = AssetDatabase.GenerateUniqueAssetPath(m.to);
                string error = AssetDatabase.MoveAsset(m.from, target);
                log.Add(string.IsNullOrEmpty(error) ? m.from + " -> " + target : "FAILED " + m.from + ": " + error);
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        DeleteEmptyFolders(Root);
        AssetDatabase.Refresh();
        File.WriteAllLines("Temp/scenery_moves.txt", log);
        int failed = log.Count(l => l.StartsWith("FAILED"));
        Debug.Log("<color=cyan>[Level Editing]</color> Scenery organized: " + (log.Count - failed) + " moved, " + failed + " failed (list in Temp/scenery_moves.txt).");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void DeleteEmptyFolders(string folder)
    {
        foreach (string sub in AssetDatabase.GetSubFolders(folder)) DeleteEmptyFolders(sub);
        if (folder == Root) return;
        bool empty = AssetDatabase.GetSubFolders(folder).Length == 0 && Directory.GetFiles(folder).All(f => f.EndsWith(".meta"));
        if (empty) AssetDatabase.DeleteAsset(folder);
    }

    // ================================================================ decoration clusters from the scene
    private static readonly HashSet<string> AllowedScripts = new HashSet<string>
    {
        "InteractiveVegetation", "OffsetAnimation", "RandomZRotation", "ZRandomizer", "FireflyHover", "ImageAnimation",
        "AnimatorHelper", "Light2D", "SortingGroup",
    };

    private static bool IsSceneryArt(Sprite s)
    {
        if (s == null) return false;
        string path = AssetDatabase.GetAssetPath(s);
        if (!path.StartsWith(Root + "/")) return false;
        return path.IndexOf("Sky", System.StringComparison.OrdinalIgnoreCase) < 0 && path.IndexOf("Parallax", System.StringComparison.OrdinalIgnoreCase) < 0
            && path.IndexOf("Map Backgrounds", System.StringComparison.OrdinalIgnoreCase) < 0 && path.IndexOf("Map 3.0", System.StringComparison.OrdinalIgnoreCase) < 0;
    }

    // Only art: renderers, animators, lights, colliders and the harmless decoration scripts
    private static bool PureDecoration(GameObject go)
    {
        foreach (Component c in go.GetComponentsInChildren<Component>(true))
        {
            if (c == null) return false; // missing script
            if (c is Transform || c is SpriteRenderer || c is Animator || c is Collider2D || c is ParticleSystem || c is ParticleSystemRenderer) continue;
            if (c is UnityEngine.Tilemaps.Tilemap || c is UnityEngine.Tilemaps.TilemapRenderer || c is Rigidbody2D) return false;
            if (AllowedScripts.Contains(c.GetType().Name)) continue;
            return false;
        }
        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr.sprite != null && !IsSceneryArt(sr.sprite)) return false;
        return true;
    }

    private static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // The piece to copy: the highest parent that is still one small piece of pure decoration
    private static GameObject UnitOf(GameObject go)
    {
        GameObject unit = go;
        Transform p = go.transform.parent;
        while (p != null && p.childCount <= 12 && PureDecoration(p.gameObject) && BoundsOf(p.gameObject).size.x < 10f)
        {
            unit = p.gameObject;
            p = p.parent;
        }
        return unit;
    }

    private static string ZoneName(Vector3 p)
    {
        if (p.x >= GloomArenaMinX) return "Colosseums";
        if (p.x >= 256f) return "Frontier";
        if (p.x >= 205f) return "Pelich Island";
        if (p.x >= 5f && p.x < 60f && p.y >= 7f) return "Gloomy Forest";
        return "Beach";
    }
    private const float GloomArenaMinX = 600f;

    private static string Tidy(string n)
    {
        n = Regex.Replace(n, @"^(RDR_|RD_)", "");
        n = Regex.Replace(n, @"[_ ]?\(?\d+\)?$", "");
        n = Regex.Replace(n, @"[_ ]?(Animated|V\d)$", "");
        return n.Replace("_", " ").Trim();
    }

    // ---------------------------------------------------------------- clusters v2: compact islands + plant clumps
    // ISLANDS: an anchor = one separate patch of painted tiles (a little floating island in a tilemap) or a mid-size
    // sprite with a solid collider (a platform piece). Everything standing on it or hanging under it (plants, vines,
    // statues, jellies...) joins it. Prefab instances (statues, the blue plants) are copied AS prefabs, so they keep
    // their scripts. Tiles are copied into the cluster's own little tilemap, colliders included.
    // PLANT CLUMPS: vegetation / rocks whose sprites overlap each other, at least 3, no wider than 6 units.
    private class Anchor
    {
        public Bounds area;
        public Tilemap tilemap;              // tile island: which tilemap and which cells
        public List<Vector3Int> cells;
        public GameObject platform;          // sprite platform
        public readonly List<GameObject> members = new List<GameObject>();
    }

    private static bool Blocked(GameObject go)
    {
        // the player, enemies that walk around, cats, UI, parallax
        return go.GetComponentInParent<Canvas>() != null || go.GetComponentInParent<PlayerMovement>() != null || go.GetComponentInParent<Health>() != null
            || go.GetComponentInParent<EnemyMovement>() != null || go.GetComponentInParent<PetFollower>() != null
            || go.GetComponentInParent<Parallax>() != null || go.GetComponentInParent<DynamicWater2D>() != null;
    }

    // The thing to copy for a renderer: its outermost prefab instance, else its decoration unit, else itself (if simple)
    private static GameObject MemberOf(GameObject go)
    {
        GameObject inst = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        if (inst != null) return BoundsOf(inst).size.x <= 8f ? inst : null; // a statue, a plant...: as its prefab (a huge prefab = not a piece)
        if (PureDecoration(go)) return UnitOf(go);
        foreach (Component c in go.GetComponents<Component>())
            if (!(c is Transform || c is SpriteRenderer || c is Animator || c is Collider2D)) return null;
        return go;
    }

    private static bool LowOrder(GameObject go)
    {
        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>()) if (sr.sortingOrder > -50) return false;
        return true; // backdrops / far layers
    }

    private static List<Anchor> TileIslands()
    {
        var list = new List<Anchor>();
        foreach (Tilemap tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.name.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0 || tm.GetComponentInParent<Canvas>() != null) continue;
            tm.CompressBounds();
            var filled = new HashSet<Vector3Int>();
            foreach (Vector3Int c in tm.cellBounds.allPositionsWithin) if (tm.HasTile(c)) filled.Add(c);
            var seen = new HashSet<Vector3Int>();
            foreach (Vector3Int start in filled)
            {
                if (seen.Contains(start)) continue;
                var island = new List<Vector3Int>();
                var stack = new Stack<Vector3Int>();
                stack.Push(start); seen.Add(start);
                while (stack.Count > 0 && island.Count < 4000)
                {
                    Vector3Int c = stack.Pop();
                    island.Add(c);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var n = new Vector3Int(c.x + dx, c.y + dy, 0);
                            if (filled.Contains(n) && seen.Add(n)) stack.Push(n);
                        }
                }
                int minX = island.Min(c => c.x), maxX = island.Max(c => c.x), minY = island.Min(c => c.y), maxY = island.Max(c => c.y);
                Vector3 a = tm.CellToWorld(new Vector3Int(minX, minY, 0)), b = tm.CellToWorld(new Vector3Int(maxX + 1, maxY + 1, 0));
                var area = new Bounds((a + b) / 2f, new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 1f));
                if (island.Count < 4 || area.size.x > 12f || area.size.x < 0.75f || area.size.y > 8f) continue; // a speck, or the mainland
                list.Add(new Anchor { area = area, tilemap = tm, cells = island });
            }
        }
        return list;
    }

    private static List<Anchor> SpritePlatforms()
    {
        var list = new List<Anchor>();
        foreach (Collider2D col in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (col.isTrigger || col is TilemapCollider2D || col is CompositeCollider2D || Blocked(col.gameObject)) continue;
            SpriteRenderer sr = col.GetComponent<SpriteRenderer>() ?? col.GetComponentInParent<SpriteRenderer>();
            if (sr == null || sr.sprite == null || sr.sortingOrder <= -50) continue;
            Bounds b = sr.bounds;
            if (b.size.x < 1.5f || b.size.x > 12f || b.size.y > 8f) continue;
            if (col.GetComponentInParent<EnemyHealth>() != null) continue; // statues are members, not floors
            GameObject piece = MemberOf(sr.gameObject);
            if (piece == null) continue;
            list.Add(new Anchor { area = b, platform = piece });
        }
        return list;
    }

    [MenuItem("Tools/Rowdy Dandy/Level Editing/Make Decoration Clusters From Scene")]
    public static void MakeClusters()
    {
        var anchors = TileIslands();
        anchors.AddRange(SpritePlatforms());
        // a platform sprite lying on a tile island belongs to it
        anchors = anchors.OrderByDescending(x => x.tilemap != null).ThenByDescending(x => x.area.size.x).ToList();
        var kept = new List<Anchor>();
        foreach (Anchor an in anchors)
        {
            Anchor host = kept.FirstOrDefault(k => k.area.Intersects(an.area) && Overlap(k.area, an.area) > 0.5f);
            if (host == null) { kept.Add(an); continue; }
            if (an.platform != null && !host.members.Contains(an.platform)) host.members.Add(an.platform);
            host.area.Encapsulate(an.area);
        }

        // everything resting on / hanging from an island
        var taken = new HashSet<GameObject>();
        foreach (Anchor an in kept) foreach (GameObject m in an.members) taken.Add(m);
        foreach (Anchor an in kept) if (an.platform != null) taken.Add(an.platform);
        var renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Anchor an in kept)
        {
            Bounds zone = an.area;
            Bounds reach = new Bounds(zone.center + new Vector3(0f, -0.25f, 0f), zone.size + new Vector3(0.5f, 6.5f, 0f)); // 3 up, 3.5 down
            foreach (SpriteRenderer sr in renderers)
            {
                if (sr.sprite == null || sr.sortingOrder <= -50 || Blocked(sr.gameObject)) continue;
                Bounds b = sr.bounds;
                if (b.size.x > Mathf.Max(3f, zone.size.x * 1.3f) || b.size.y > 7f) continue;
                if (b.center.x < zone.min.x - 0.2f || b.center.x > zone.max.x + 0.2f) continue; // on the island, not beside it
                if (!reach.Intersects(new Bounds(new Vector3(b.center.x, b.center.y, 0f), new Vector3(b.size.x, b.size.y, 1f)))) continue;
                // standing on it: its bottom near the island top; hanging: its top near the island bottom; or inside it
                bool onTop = b.min.y >= zone.max.y - 0.6f && b.min.y <= zone.max.y + 0.5f;
                bool hanging = b.max.y <= zone.min.y + 0.8f && b.max.y >= zone.min.y - 0.5f;
                bool inside = zone.Contains(new Vector3(b.center.x, b.center.y, zone.center.z));
                if (!onTop && !hanging && !inside) continue;
                GameObject m = MemberOf(sr.gameObject);
                if (m == null || taken.Contains(m) || LowOrder(m)) continue;
                an.members.Add(m);
                taken.Add(m);
            }
        }
        kept.RemoveAll(an => an.members.Count == 0 && an.tilemap == null); // a bare platform sprite isn't a cluster

        // plant clumps from what's left
        var plants = new List<GameObject>();
        foreach (SpriteRenderer sr in renderers)
        {
            if (!IsSceneryArt(sr.sprite) || sr.sortingOrder <= -50 || Blocked(sr.gameObject) || !PureDecoration(sr.gameObject)) continue;
            string cat = LevelKitLibrary.Classify(AssetDatabase.GetAssetPath(sr.sprite), sr.name);
            if (cat != "Vegetation" && cat != "Rocks") continue;
            GameObject u = UnitOf(sr.gameObject);
            if (!taken.Contains(u) && !plants.Contains(u) && BoundsOf(u).size.x <= 4f) plants.Add(u);
        }
        var clumps = new List<List<GameObject>>();
        {
            int n = plants.Count;
            var parent = Enumerable.Range(0, n).ToArray();
            int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
            var bs = plants.Select(BoundsOf).Select(b => new Bounds(new Vector3(b.center.x, b.center.y, 0f), new Vector3(b.size.x * 0.9f, b.size.y * 0.9f, 1f))).ToArray();
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (bs[i].Intersects(bs[j])) parent[Find(i)] = Find(j);
            foreach (var g in Enumerable.Range(0, n).GroupBy(Find))
            {
                var members = g.Select(i => plants[i]).ToList();
                if (members.Count < 3) continue;
                Bounds all = BoundsOf(members[0]);
                foreach (GameObject m in members) all.Encapsulate(BoundsOf(m));
                if (all.size.x <= 6f && all.size.y <= 5f) clumps.Add(members);
            }
        }

        int total = kept.Count + clumps.Count;
        if (total == 0) { EditorUtility.DisplayDialog("Decoration Clusters", "No islands or plant clumps found in the open scene.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Decoration Clusters", kept.Count + " islands and " + clumps.Count + " plant clumps found.\n\nSaved as prefabs in " + ClusterRoot
            + " (Islands / Plant Clumps). Old clusters there are replaced. The scene is not changed.", "Make them", "Cancel")) return;

        if (AssetDatabase.IsValidFolder(ClusterRoot)) AssetDatabase.DeleteAsset(ClusterRoot);
        foreach (string f in new[] { "Islands/Small", "Islands/Medium", "Islands/Large", "Plant Clumps" }) EnsureFolder(ClusterRoot + "/" + f);
        var used = new HashSet<string>();
        int made = 0;
        try
        {
            for (int i = 0; i < kept.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Decoration Clusters", "Island " + (i + 1) + " / " + kept.Count, (float)i / total);
                Anchor an = kept[i];
                Bounds all = an.area;
                foreach (GameObject m in an.members) all.Encapsulate(BoundsOf(m));
                string size = an.area.size.x <= 3f ? "Small" : an.area.size.x <= 6f ? "Medium" : "Large";
                string what = string.Join(", ", an.members.Where(m => m != an.platform).Select(m => Tidy(m.name)).GroupBy(s => s).OrderByDescending(g => g.Count()).Take(3).Select(g => g.Key));
                string name = UniqueName(used, ZoneName(an.area.center) + " Island" + (what.Length > 0 ? " - " + what : ""));
                var root = new GameObject(name);
                root.transform.position = Snap(new Vector3(an.area.center.x, an.area.min.y, 0f)); // pivot: under the island
                if (an.tilemap != null) CopyTiles(an, root.transform);
                if (an.platform != null) CopyMember(an.platform, root.transform);
                foreach (GameObject m in an.members) if (m != an.platform) CopyMember(m, root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, ClusterRoot + "/Islands/" + size + "/" + name + ".prefab");
                Object.DestroyImmediate(root);
                made++;
            }
            for (int i = 0; i < clumps.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Decoration Clusters", "Clump " + (i + 1) + " / " + clumps.Count, (float)(kept.Count + i) / total);
                var members = clumps[i];
                Bounds all = BoundsOf(members[0]);
                foreach (GameObject m in members) all.Encapsulate(BoundsOf(m));
                string what = string.Join(", ", members.Select(m => Tidy(m.name)).GroupBy(s => s).OrderByDescending(g => g.Count()).Take(3).Select(g => g.Key));
                string name = UniqueName(used, ZoneName(all.center) + " Clump - " + what);
                var root = new GameObject(name);
                root.transform.position = Snap(new Vector3(all.center.x, all.min.y, 0f)); // pivot: on the floor
                foreach (GameObject m in members) CopyMember(m, root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, ClusterRoot + "/Plant Clumps/" + name + ".prefab");
                Object.DestroyImmediate(root);
                made++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.Refresh();
        Debug.Log("<color=cyan>[Level Editing]</color> " + made + " clusters saved in " + ClusterRoot + ".");
    }

    private static float Overlap(Bounds a, Bounds b)
    {
        float w = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x), h = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
        if (w <= 0f || h <= 0f) return 0f;
        float smaller = Mathf.Min(a.size.x * a.size.y, b.size.x * b.size.y);
        return smaller > 0f ? w * h / smaller : 0f;
    }

    private static string UniqueName(HashSet<string> used, string n)
    {
        foreach (char ch in Path.GetInvalidFileNameChars()) n = n.Replace(ch, ' ');
        string name = n;
        for (int k = 2; used.Contains(name); k++) name = n + " " + k;
        used.Add(name);
        return name;
    }

    // A copy that keeps prefab links (statues, plants keep their scripts and stay updated from their prefab)
    private static void CopyMember(GameObject src, Transform root)
    {
        GameObject copy;
        GameObject asset = PrefabUtility.IsPartOfPrefabInstance(src) && PrefabUtility.GetOutermostPrefabInstanceRoot(src) == src
            ? PrefabUtility.GetCorrespondingObjectFromSource(src) : null;
        if (asset != null)
        {
            copy = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PropertyModification[] mods = PrefabUtility.GetPropertyModifications(src);
            if (mods != null) PrefabUtility.SetPropertyModifications(copy, mods);
        }
        else copy = Object.Instantiate(src);
        copy.name = src.name;
        copy.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        copy.transform.localScale = src.transform.lossyScale;
        copy.transform.SetParent(root, true);
    }

    // The island's own little tilemap: same tiles, cell size, look and colliders as the one it came from
    private static void CopyTiles(Anchor an, Transform root)
    {
        Tilemap src = an.tilemap;
        Grid srcGrid = src.layoutGrid;
        var gridGo = new GameObject("Tiles");
        gridGo.transform.SetParent(root, false);
        gridGo.transform.position = srcGrid != null ? srcGrid.transform.position : Vector3.zero;
        var grid = gridGo.AddComponent<Grid>();
        if (srcGrid != null) { grid.cellSize = srcGrid.cellSize; grid.cellGap = srcGrid.cellGap; grid.cellLayout = srcGrid.cellLayout; grid.cellSwizzle = srcGrid.cellSwizzle; }
        var tmGo = new GameObject(src.name);
        tmGo.transform.SetParent(gridGo.transform, false);
        tmGo.transform.localPosition = src.transform.localPosition;
        tmGo.layer = src.gameObject.layer;
        tmGo.tag = src.gameObject.tag;
        var tm = tmGo.AddComponent<Tilemap>();
        tm.tileAnchor = src.tileAnchor;
        tm.color = src.color;
        TilemapRenderer sr = src.GetComponent<TilemapRenderer>(), r = tmGo.AddComponent<TilemapRenderer>();
        if (sr != null) { r.sharedMaterial = sr.sharedMaterial; r.sortingLayerID = sr.sortingLayerID; r.sortingOrder = sr.sortingOrder; r.mode = sr.mode; }
        foreach (Vector3Int c in an.cells)
        {
            tm.SetTile(c, src.GetTile(c));
            tm.SetTransformMatrix(c, src.GetTransformMatrix(c));
            tm.SetColor(c, src.GetColor(c));
        }
        tm.CompressBounds();
        // colliders like the original (one-way platforms keep their effector)
        if (src.GetComponent<TilemapCollider2D>() is TilemapCollider2D stc)
        {
            var tc = tmGo.AddComponent<TilemapCollider2D>();
            tc.isTrigger = stc.isTrigger;
            tc.sharedMaterial = stc.sharedMaterial;
            CompositeCollider2D scomp = src.GetComponent<CompositeCollider2D>();
            PlatformEffector2D seff = src.GetComponent<PlatformEffector2D>();
            if (scomp != null)
            {
                var body = tmGo.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                var comp = tmGo.AddComponent<CompositeCollider2D>();
                comp.geometryType = scomp.geometryType;
                comp.usedByEffector = scomp.usedByEffector;
                tc.compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            if (seff != null)
            {
                var eff = tmGo.AddComponent<PlatformEffector2D>();
                eff.useOneWay = seff.useOneWay;
                eff.surfaceArc = seff.surfaceArc;
                eff.useSideFriction = seff.useSideFriction;
                eff.useSideBounce = seff.useSideBounce;
                eff.colliderMask = seff.colliderMask;
                eff.useColliderMask = seff.useColliderMask;
                if (scomp == null) tc.usedByEffector = true;
            }
        }
    }

    // ================================================================ snap to pixels
    [MenuItem("Tools/Rowdy Dandy/Level Editing/Snap Selection To Pixels %&p")]
    private static void SnapSelection()
    {
        Transform[] ts = Selection.transforms;
        Undo.RecordObjects(ts, "Snap To Pixels");
        foreach (Transform t in ts) t.position = Snap(t.position);
    }

    public static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * 64f) / 64f, Mathf.Round(p.y * 64f) / 64f, p.z);

    [MenuItem("Tools/Rowdy Dandy/Level Editing/Level Kit")]
    private static void OpenKit() => LevelKitWindow.Open();

    [MenuItem("Tools/Rowdy Dandy/Level Editing/Rebuild Level Kit Pieces")]
    private static void RebuildPieces() => LevelKitLibrary.Build(true);

    // shared with the Level Kit
    public static bool IsPureDecoration(GameObject go) => PureDecoration(go);
    public static GameObject DecorationUnitOf(GameObject go) => UnitOf(go);
    public static bool IsDecorationArt(Sprite s) => IsSceneryArt(s);
    public static string TidyName(string n) => Tidy(n);
    public static void MakeFolder(string path) => EnsureFolder(path);
}

// The Level Kit's pieces: ONE prefab per thing, in Assets/Scenery/00 Kit Pieces/<category>:
//   - everything the open scene already uses as decoration, copied the way it's used (the animated palm tree with its
//     Animator, the flower with its order in layer...) - one per distinct look
//   - every other Scenery image once: animated sheets as one animated piece (their clip's controller), static sheets
//     as one piece (the first sprite), never each frame. Tile sets, skies and parallax layers are left out.
//   - Interactables: the plants, checkpoints, jellies and breakable statues from Resources
// Rebuilt with the window's Rebuild button (or Tools > Rowdy Dandy > Level Editing > Rebuild Level Kit Pieces).
public static class LevelKitLibrary
{
    public const string PiecesRoot = "Assets/Scenery/00 Kit Pieces";
    public static readonly string[] Categories = { "Vegetation", "Rocks", "Terrain", "Interactables", "Misc", "Clusters" };

    private static readonly string[] InteractablePrefabs =
    {
        "Assets/Resources/Interactables/BluePlant .prefab", "Assets/Resources/Interactables/OrangePlant.prefab",
        "Assets/Resources/Interactables/PurplePlant.prefab", "Assets/Resources/Interactables/Spawner.prefab",
        "Assets/Resources/Interactables/Jelly.prefab", "Assets/Resources/Interactables/BigJelly.prefab",
        "Assets/Resources/Enemies Prefab/Breakables/RDR_WolfStatues_A.prefab", "Assets/Resources/Enemies Prefab/Breakables/RDR_WolfStatues_B.prefab",
        "Assets/Resources/Enemies Prefab/Breakables/RDR_WolfStatues_C.prefab",
    };
    private static readonly string[] MiscPrefabs =
    {
        "Assets/Rowdy Dandy/Water Shader.prefab", "Assets/Resources/Interactables/Fireflies.prefab",
    };

    public static string Classify(string path, string name)
    {
        string s = (path + "/" + name).ToLowerInvariant();
        if (s.Contains("interactable") || s.Contains("checkpoint")) return "Interactables";
        if (s.Contains("rock") || s.Contains("pointy")) return "Rocks";
        if (s.Contains("cliff") || s.Contains("earth") || s.Contains("pillar") || s.Contains("wall") || s.Contains("slope") || s.Contains("bridge")
            || s.Contains("sandpile") || s.Contains("backsand") || s.Contains("terrain") || s.Contains("big pieces")) return "Terrain";
        if (s.Contains("palm") || s.Contains("vine") || s.Contains("cipo") || s.Contains("grass") || s.Contains("flower") || s.Contains("bush")
            || s.Contains("tree") || s.Contains("crystal") || s.Contains("vegetation") || s.Contains("stump") || s.Contains("plant")) return "Vegetation";
        return "Misc";
    }

    // left out of the kit: they're painted with the Tile Palette, or are the sky / parallax / backgrounds
    private static bool Excluded(string path)
    {
        string s = path.ToLowerInvariant();
        return s.Contains("tileset") || s.Contains("tile set") || s.Contains("tilemap") || s.Contains("sand tiles") || s.Contains("bigasstile")
            || s.Contains("sky") || s.Contains("parallax") || s.Contains("map backgrounds") || s.Contains("map 3.0") || s.Contains("sun") || s.Contains("moon")
            || s.Contains("fog") || s.Contains("old tileset") || s.Contains("/00 ") || s.Contains("palettes") || s.Contains("big pieces")
            || s.Contains("sprite test") || s.Contains("map test") || s.Contains("floresta") || s.Contains("testnewterrain")
            || s.EndsWith(".aseprite") || s.EndsWith(".psd")                                  // the .png next to them is the one used
            || s.Contains("interactable") || s.Contains("checkpoint");                       // the real prefabs (with their scripts) are listed instead
    }

    public static List<Object> Items(string category)
    {
        var list = new List<Object>();
        string folder = category == "Clusters" ? LevelEditingTools.ClusterRoot : PiecesRoot + "/" + category;
        if (AssetDatabase.IsValidFolder(folder))
            foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
                if (go != null) list.Add(go);
            }
        foreach (string p in category == "Interactables" ? InteractablePrefabs : category == "Misc" ? MiscPrefabs : new string[0])
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null) list.Add(go);
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public static bool Built => AssetDatabase.IsValidFolder(PiecesRoot);

    public static void Build(bool ask)
    {
        if (ask && !EditorUtility.DisplayDialog("Level Kit", "Rebuild the kit pieces (Assets/Scenery/00 Kit Pieces) from the open scene and the Scenery art?\nThe scene is not changed.", "Rebuild", "Cancel")) return;
        if (AssetDatabase.IsValidFolder(PiecesRoot)) AssetDatabase.DeleteAsset(PiecesRoot);
        foreach (string c in Categories) if (c != "Clusters") LevelEditingTools.MakeFolder(PiecesRoot + "/" + c);

        var done = new HashSet<string>();          // texture paths already in the kit
        var names = new HashSet<string>();
        int made = 0;
        try
        {
            // 1. what the scene already uses, as it's used (one per sprite sheet + animator controller)
            var seen = new HashSet<string>();
            foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!LevelEditingTools.IsDecorationArt(sr.sprite) || sr.GetComponentInParent<Canvas>() != null || sr.bounds.size.x > 18f) continue;
                string tex = AssetDatabase.GetAssetPath(sr.sprite);
                if (Excluded(tex) || !LevelEditingTools.IsPureDecoration(sr.gameObject)) continue;
                GameObject unit = LevelEditingTools.DecorationUnitOf(sr.gameObject);
                Animator an = unit.GetComponentInChildren<Animator>();
                string key = tex + "|" + (an != null && an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "-") + "|" + unit.GetComponentsInChildren<SpriteRenderer>().Length;
                if (!seen.Add(key)) continue;
                string name = Unique(names, LevelEditingTools.TidyName(unit.name));
                string cat = Classify(tex, unit.name);
                GameObject copy = Object.Instantiate(unit);
                copy.name = name;
                copy.transform.position = Vector3.zero;
                copy.transform.rotation = Quaternion.identity;
                Vector3 ls = unit.transform.lossyScale;
                copy.transform.localScale = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), 1f);
                PrefabUtility.SaveAsPrefabAsset(copy, PiecesRoot + "/" + cat + "/" + name + ".prefab");
                Object.DestroyImmediate(copy);
                done.Add(tex);
                made++;
            }

            // 2. every other image once (animated sheets as one animated piece)
            var clipsByTexture = ClipsByTexture();
            Material lit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            foreach (string g in AssetDatabase.FindAssets("t:Texture2D", new[] { LevelEditingTools.Root }))
            {
                string tex = AssetDatabase.GUIDToAssetPath(g);
                if (done.Contains(tex) || Excluded(tex)) continue;
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(tex).OfType<Sprite>().OrderBy(s => s.name, new NaturalOrder()).ToArray();
                if (sprites.Length == 0) continue;
                string baseName = LevelEditingTools.TidyName(Path.GetFileNameWithoutExtension(tex));
                var go = new GameObject(Unique(names, baseName));
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = sprites[0];
                if (lit != null) r.sharedMaterial = lit;
                if (clipsByTexture.TryGetValue(g, out RuntimeAnimatorController ctrl) && ctrl != null)
                    go.AddComponent<Animator>().runtimeAnimatorController = ctrl;
                PrefabUtility.SaveAsPrefabAsset(go, PiecesRoot + "/" + Classify(tex, go.name) + "/" + go.name + ".prefab");
                Object.DestroyImmediate(go);
                made++;
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
        AssetDatabase.Refresh();
        Debug.Log("<color=cyan>[Level Editing]</color> Level Kit: " + made + " pieces in " + PiecesRoot + ".");
    }

    private static string Unique(HashSet<string> used, string n)
    {
        foreach (char ch in Path.GetInvalidFileNameChars()) n = n.Replace(ch, ' ');
        if (string.IsNullOrEmpty(n)) n = "Piece";
        string name = n;
        for (int k = 2; used.Contains(name); k++) name = n + " " + k;
        used.Add(name);
        return name;
    }

    // texture guid -> an animator controller that plays a clip made of its sprites
    private static Dictionary<string, RuntimeAnimatorController> ClipsByTexture()
    {
        var clipTex = new Dictionary<string, string>(); // clip path -> texture guid
        foreach (string g in AssetDatabase.FindAssets("t:AnimationClip", new[] { LevelEditingTools.Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            Match m = Regex.Match(File.ReadAllText(path), @"value: \{fileID: -?\d+, guid: (\w+), type: 3\}");
            if (m.Success) clipTex[path] = m.Groups[1].Value;
        }
        var result = new Dictionary<string, RuntimeAnimatorController>();
        foreach (string g in AssetDatabase.FindAssets("t:AnimatorController", new[] { LevelEditingTools.Root }))
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GUIDToAssetPath(g));
            if (ctrl == null) continue;
            foreach (AnimationClip clip in ctrl.animationClips)
            {
                if (clip == null || !clipTex.TryGetValue(AssetDatabase.GetAssetPath(clip), out string tex)) continue;
                if (!result.ContainsKey(tex)) result[tex] = ctrl;
            }
        }
        return result;
    }

    private class NaturalOrder : IComparer<string>
    {
        public int Compare(string a, string b) => EditorUtility.NaturalCompare(a, b);
    }
}

// The Level Kit: browse Assets/Scenery by category with big thumbnails, click to pick, click in the Scene to place
public class LevelKitWindow : EditorWindow
{
    private string[] categories = new string[0];
    private int category;
    private string search = "";
    private float thumb = 80f;
    private Vector2 scroll;
    private readonly List<Object> items = new List<Object>();
    private Object armed;
    private bool snap = true, randomFlip, parentUnderRoot = true;
    private int order = 0;
    private bool orderFromPrefab = true;
    private const string RootName = "Level Kit Decor";

    public static void Open()
    {
        var w = GetWindow<LevelKitWindow>("Level Kit");
        w.minSize = new Vector2(320f, 300f);
        w.Refresh();
    }

    private void OnEnable() { SceneView.duringSceneGui += OnScene; Refresh(); }
    private void OnDisable() { SceneView.duringSceneGui -= OnScene; }
    private void OnProjectChange() => Refresh();

    private void Refresh()
    {
        categories = LevelKitLibrary.Categories;
        category = Mathf.Clamp(category, 0, categories.Length - 1);
        LoadItems();
    }

    private void LoadItems()
    {
        items.Clear();
        items.AddRange(LevelKitLibrary.Items(categories[category]));
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        if (!LevelKitLibrary.Built)
        {
            EditorGUILayout.HelpBox("The kit pieces aren't made yet. Open the main scene and press Build: every decoration your scene uses (as it's used, animations included) plus every other Scenery image, ONE piece each.", MessageType.Info);
            if (GUILayout.Button("Build Kit Pieces", GUILayout.Height(28f))) { LevelKitLibrary.Build(false); LoadItems(); }
            return;
        }
        EditorGUI.BeginChangeCheck();
        category = GUILayout.Toolbar(category, categories, GUILayout.Height(24f));
        if (EditorGUI.EndChangeCheck()) { LoadItems(); scroll = Vector2.zero; }
        if (GUILayout.Button(new GUIContent("Rebuild pieces", "Again from the open scene + Scenery art (after adding new art or decorating more)"), EditorStyles.miniButton)) { LevelKitLibrary.Build(true); LoadItems(); }
        search = EditorGUILayout.TextField("Search", search);
        thumb = EditorGUILayout.Slider("Thumbnail size", thumb, 48f, 160f);
        EditorGUILayout.BeginHorizontal();
        snap = GUILayout.Toggle(snap, "Pixel snap", "Button");
        randomFlip = GUILayout.Toggle(randomFlip, "Random flip", "Button");
        parentUnderRoot = GUILayout.Toggle(parentUnderRoot, "Into '" + RootName + "'", "Button");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        orderFromPrefab = GUILayout.Toggle(orderFromPrefab, "Keep prefab order", "Button", GUILayout.Width(130f));
        order = EditorGUILayout.IntField(new GUIContent("Sprite order (0 = auto)", "0: the order the scene already uses for sprites from the same folder"), order);
        EditorGUILayout.EndHorizontal();
        if (armed != null)
            EditorGUILayout.HelpBox("Placing: " + armed.name + "\nClick in the Scene view to place. Esc or right click to stop.", MessageType.None);
        else
            EditorGUILayout.HelpBox("Click a thumbnail, then click in the Scene view to place it. Or drag a thumbnail into the Scene.", MessageType.None);

        if (categories[category] == "Clusters" && items.Count == 0)
        {
            EditorGUILayout.HelpBox("No clusters yet. With the main scene open, this copies groups of decoration placed together on your map (palms + grass + rocks...) into prefabs, small to big. The scene is not changed.", MessageType.Info);
            if (GUILayout.Button("Make Clusters From The Open Scene", GUILayout.Height(28f))) { LevelEditingTools.MakeClusters(); LoadItems(); }
            return;
        }
        var shown = items.Where(o => string.IsNullOrEmpty(search) || o.name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        int cols = Mathf.Max(1, Mathf.FloorToInt((position.width - 20f) / (thumb + 6f)));
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < shown.Count; i += cols)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + cols, shown.Count); j++) DrawItem(shown[j]);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        if (AssetPreview.IsLoadingAssetPreviews()) Repaint();
    }

    private void DrawItem(Object o)
    {
        Rect r = GUILayoutUtility.GetRect(thumb, thumb + 14f, GUILayout.Width(thumb), GUILayout.Height(thumb + 14f));
        Rect img = new Rect(r.x + 2f, r.y + 2f, thumb - 4f, thumb - 4f);
        bool on = o == armed;
        EditorGUI.DrawRect(r, on ? new Color(0.25f, 0.5f, 0.9f, 0.6f) : new Color(0f, 0f, 0f, 0.15f));
        Sprite s = o as Sprite;
        if (s == null && o is GameObject g)
        {
            // a one-sprite piece: draw its sprite (crisp); bigger groups use Unity's preview
            SpriteRenderer[] rs = g.GetComponentsInChildren<SpriteRenderer>(true);
            if (rs.Length == 1) s = rs[0].sprite;
        }
        if (s != null && s.texture != null)
        {
            // the sprite itself, its own rect, kept crisp and in proportion
            Rect tr = s.textureRect;
            Rect uv = new Rect(tr.x / s.texture.width, tr.y / s.texture.height, tr.width / s.texture.width, tr.height / s.texture.height);
            float k = Mathf.Min(img.width / tr.width, img.height / tr.height);
            Rect fit = new Rect(img.center.x - tr.width * k / 2f, img.center.y - tr.height * k / 2f, tr.width * k, tr.height * k);
            GUI.DrawTextureWithTexCoords(fit, s.texture, uv, true);
        }
        else
        {
            Texture2D prev = AssetPreview.GetAssetPreview(o) ?? AssetPreview.GetMiniThumbnail(o);
            if (prev != null) GUI.DrawTexture(img, prev, ScaleMode.ScaleToFit);
        }
        GUI.Label(new Rect(r.x, r.yMax - 14f, r.width, 14f), o.name, EditorStyles.miniLabel);

        Event e = Event.current;
        if (!r.Contains(e.mousePosition)) return;
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            armed = on ? null : o;
            if (armed != null) EditorGUIUtility.PingObject(armed);
            e.Use();
            Repaint();
            SceneView.RepaintAll();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { o };
            DragAndDrop.StartDrag(o.name);
            e.Use();
        }
    }

    private void OnScene(SceneView view)
    {
        if (armed == null) return;
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);
        Vector3 world = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
        world.z = 0f;
        if (snap) world = LevelEditingTools.Snap(world);

        Handles.color = new Color(0.4f, 0.75f, 1f, 1f);
        Handles.DrawWireDisc(world, Vector3.forward, HandleUtility.GetHandleSize(world) * 0.08f);
        Handles.Label(world + Vector3.up * HandleUtility.GetHandleSize(world) * 0.2f, armed.name);
        if (e.type == EventType.MouseMove) view.Repaint();

        if ((e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) || (e.type == EventType.MouseDown && e.button == 1))
        {
            armed = null;
            e.Use();
            Repaint();
            return;
        }
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Place(world);
            e.Use();
        }
    }

    // Order 0 = auto: the order the scene already uses most for sprites from the same folder (so a Gloomy Forest
    // flower lands where the other Gloomy Forest flowers are, not behind the ground or a fog layer)
    private static int AutoOrder(Sprite s)
    {
        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(s));
        var counts = new Dictionary<int, int>();
        foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (sr.sprite == null || Path.GetDirectoryName(AssetDatabase.GetAssetPath(sr.sprite)) != folder) continue;
            counts.TryGetValue(sr.sortingOrder, out int c);
            counts[sr.sortingOrder] = c + 1;
        }
        return counts.Count > 0 ? counts.OrderByDescending(kv => kv.Value).First().Key : 0;
    }

    private static Material spriteLit;
    private static Material SpriteLitDefault
    {
        get
        {
            if (spriteLit != null) return spriteLit;
            spriteLit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            if (spriteLit == null)
                foreach (string g in AssetDatabase.FindAssets("Sprite-Lit-Default t:Material"))
                {
                    spriteLit = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                    if (spriteLit != null) break;
                }
            return spriteLit;
        }
    }

    // pieces placed before the fix (pink squares): Tools > Rowdy Dandy > Level Editing > Fix Pink Sprites
    [MenuItem("Tools/Rowdy Dandy/Level Editing/Fix Pink Sprites (Built-in Material)")]
    private static void FixPink()
    {
        Material lit = SpriteLitDefault;
        if (lit == null) { Debug.LogWarning("Sprite-Lit-Default not found"); return; }
        int n = 0;
        foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Material m = sr.sharedMaterial;
            if (m != null && m.shader != null && (m.shader.name == "Sprites/Default" || !m.shader.isSupported))
            {
                Undo.RecordObject(sr, "Fix Pink Sprites");
                sr.sharedMaterial = lit;
                n++;
            }
        }
        Debug.Log("<color=cyan>[Level Editing]</color> " + n + " sprites switched to Sprite-Lit-Default.");
    }

    private void Place(Vector3 at)
    {
        GameObject go;
        if (armed is Sprite s)
        {
            go = new GameObject(s.name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order != 0 ? order : AutoOrder(s);
            // a SpriteRenderer made from code gets the built-in Sprites-Default material, which URP 2D can't draw
            // (a pink square): give it URP's Sprite-Lit-Default, like Unity's own drag and drop does
            Material lit = SpriteLitDefault;
            if (lit != null) sr.sharedMaterial = lit;
        }
        else if (armed is GameObject prefab)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.GetActiveScene());
            if (!orderFromPrefab) foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>()) sr.sortingOrder += order;
        }
        else return;
        go.transform.position = at;
        if (randomFlip && Random.value < 0.5f)
        {
            Vector3 sc = go.transform.localScale;
            go.transform.localScale = new Vector3(-sc.x, sc.y, sc.z);
        }
        if (parentUnderRoot)
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Level Kit Root");
            }
            go.transform.SetParent(root.transform, true);
        }
        Undo.RegisterCreatedObjectUndo(go, "Place " + go.name);
        Selection.activeGameObject = go;
    }
}
