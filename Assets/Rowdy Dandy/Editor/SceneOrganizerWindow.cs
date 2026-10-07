using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Rowdy Dandy > Scene Organizer
//  Hierarchy:  sorts the loose top-level objects of the open scene into category folders. Preview first, then Apply (one Ctrl+Z).
//              Only top-level objects move, as whole units, into folders at the origin, so nothing changes position, sorting,
//              animation paths or references. Objects that must stay at the top level (Rowdy, pets with HitStop,
//              DontDestroyOnLoad objects, cameras, UI) are left alone.
//  Vegetation: gives grass / bushes / flowers / vines / trees the wind + interaction shader and the InteractiveVegetation component.
public class SceneOrganizerWindow : EditorWindow
{
    // ---------------------------------------------------------------- hierarchy rules (first match wins)
    private class Rule
    {
        public readonly string Folder;
        public readonly Regex Pattern;
        public Rule(string folder, string pattern) { Folder = folder; Pattern = new Regex(pattern, RegexOptions.IgnoreCase); }
    }

    private static readonly Rule[] Rules =
    {
        new Rule("[Gameplay]/Checkpoints", @"^RDR_Checkpoint"),
        new Rule("[Gameplay]/Hazards", @"KILLZONE|^Spike"),
        new Rule("[Gameplay]/Bounce Jellies", @"^(Big)?Jelly$"),
        new Rule("[Gameplay]/Spawners & Patrol", @"^Wave$|^WaveRandomizer$|^Spawner$|^PatrolPoint"),
        new Rule("[Gameplay]/Pickups", @"Heart$"),

        new Rule("[Characters]/Enemies", @"^Enemies$|^Enemy_|Plant$|^SharkWolf|^Crabby$"),
        new Rule("[Characters]/NPCs", @"_NPC|CookingPelican"),
        new Rule("[Characters]/Wildlife", @"MantaRay"),

        new Rule("[Environment]/Terrain", @"MegaCliff|LightCLiff|EarthWall|EarthFatPillar|EarthPile|BackSand|TestMapBackGround|TestMapFrontGround|WatervivaSource"),
        new Rule("[Environment]/Water", @"^Water Shader$|PoolAsset|BigAssWaterViva"),
        new Rule("[Environment]/Rocks", @"PointyRocks|^RockPack"),
        new Rule("[Environment]/Stumps & Logs", @"^TreeStump|SittingTrunk"),
        new Rule("[Environment]/Trees", @"^Tree[A-Z]$|HorrificTree|BigPalmTree|Palmeira"),
        new Rule("[Environment]/Grass", @"GrassPatch|OrangeGrass|^RDR_Vegetation"),
        new Rule("[Environment]/Bushes", @"^Bush"),
        new Rule("[Environment]/Vines", @"Vine"),
        new Rule("[Environment]/Flowers", @"Flower"),
        new Rule("[Environment]/Crystals", @"^Crystal"),
        new Rule("[Environment]/Structures", @"Cabin|WolfStatue|^Statues$|^Scene Props$"),
        new Rule("[Environment]", @"^Vegetation$"),

        new Rule("[Atmosphere]/Heatwave", @"Heatwave"),
        new Rule("[Atmosphere]/Mist & Fog", @"Mist|FogLax"),
        new Rule("[Atmosphere]/Fireflies", @"Firefl"),
        new Rule("[Atmosphere]/Dust", @"Dust"),
        new Rule("[Atmosphere]/Skies", @"^Skies$"),
        new Rule("[Atmosphere]/Lights", @"^Lights$|^Light 2D"),
    };

    private static readonly string[] Categories = { "[Gameplay]", "[Characters]", "[Environment]", "[Atmosphere]" };

    private class HierarchyPlan
    {
        public readonly SortedDictionary<string, List<GameObject>> Moves = new SortedDictionary<string, List<GameObject>>();
        public readonly List<KeyValuePair<GameObject, string>> Kept = new List<KeyValuePair<GameObject, string>>();
        public readonly List<GameObject> Unsorted = new List<GameObject>();
        public int MoveCount => Moves.Values.Sum(l => l.Count);
    }

    // ---------------------------------------------------------------- vegetation profiles
    private class Profile
    {
        public readonly string Name;
        public readonly Regex Pattern;
        public readonly InteractiveVegetation.Mode Mode;
        public readonly float Wind;
        public readonly float Push;
        public readonly bool Rustle;
        public Profile(string name, string pattern, InteractiveVegetation.Mode mode, float wind, float push, bool rustle)
        { Name = name; Pattern = new Regex(pattern, RegexOptions.IgnoreCase); Mode = mode; Wind = wind; Push = push; Rustle = rustle; }
    }

    private static readonly Profile[] Profiles =
    {
        new Profile("Hanging vines", @"^RDR_Vine", InteractiveVegetation.Mode.Hanging, 1.2f, 0.3f, false),
        new Profile("Grass", @"^RDR_Vegetation_(6|7|8|9|10)$|GrassPatch|OrangeGrass", InteractiveVegetation.Mode.Grass, 1f, 0.35f, true),
        new Profile("Blossom branches", @"^RDR_Vegetation_(11|12)$", InteractiveVegetation.Mode.Grass, 0.7f, 0.25f, true),
        new Profile("Flowers", @"^FlowerRed|^RDR_Flowers_(0|2)$", InteractiveVegetation.Mode.Grass, 0.9f, 0.3f, true),
        new Profile("Bushes", @"^Bush", InteractiveVegetation.Mode.Grass, 0.6f, 0.22f, true),
        new Profile("Crystal plants", @"^CrystalFlower|^CrystalVegetation|^CrystalVine", InteractiveVegetation.Mode.Grass, 0.35f, 0.15f, true),
        new Profile("Trees", @"^RDR_Vegetation_[0-5]$|^Tree[A-Z]$|^HorrificTree", InteractiveVegetation.Mode.Tree, 0.22f, 0.2f, true),
        // Added 2026-10-07 so the rest of the map moves like the Gloomy Forest (check them in Preview first)
        new Profile("Palm trees", @"^RDR_BigPalmTree", InteractiveVegetation.Mode.Tree, 0.3f, 0.2f, true),
        new Profile("Flowers (set 1)", @"^RDR_Flowers_1$", InteractiveVegetation.Mode.Grass, 0.9f, 0.3f, true),
        new Profile("Crystal patches", @"^CrystalPatch", InteractiveVegetation.Mode.Grass, 0.3f, 0.12f, true),    };

    private const string DefaultSpriteShader = "Universal Render Pipeline/2D/Sprite-Lit-Default";
    private const string VegetationShader = "Rowdy Dandy/2D/Sprite-Lit-Vegetation";
    private const string VegetationMaterialPath = "Assets/Rowdy Dandy/Shaders/M_Vegetation.mat";

    private class VegetationPlan
    {
        public readonly List<KeyValuePair<SpriteRenderer, Profile>> Entries = new List<KeyValuePair<SpriteRenderer, Profile>>();
        public int SkippedMaterial;
        public int SkippedGameplay;
    }

    // ---------------------------------------------------------------- state
    private int tab;
    private HierarchyPlan hierarchyPlan;
    private VegetationPlan vegetationPlan;
    private Vector2 scroll;
    private readonly Dictionary<string, bool> foldouts = new Dictionary<string, bool>();
    private readonly Dictionary<Sprite, SpriteInfo> spriteCache = new Dictionary<Sprite, SpriteInfo>();

    [MenuItem("Tools/Rowdy Dandy/Scene Organizer")]
    private static void Open() => GetWindow<SceneOrganizerWindow>("Scene Organizer");

    private void OnHierarchyChange() { hierarchyPlan = null; vegetationPlan = null; Repaint(); }

    private void OnGUI()
    {
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Exit Play mode to organize the scene.", MessageType.Info);
            return;
        }

        tab = GUILayout.Toolbar(tab, new[] { "Hierarchy", "Interactive Vegetation" });
        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (tab == 0) DrawHierarchyTab(); else DrawVegetationTab();
        EditorGUILayout.EndScrollView();
    }

    // =========================================================================
    // HIERARCHY
    // =========================================================================

    private void DrawHierarchyTab()
    {
        EditorGUILayout.HelpBox(
            "Sorts the loose top-level objects into [Gameplay] / [Characters] / [Environment] / [Atmosphere] folders.\n" +
            "Only whole top-level objects move, into folders at the origin, so positions, sorting, animations and references don't change.\n" +
            "One Undo step. Save the scene afterwards to keep it.", MessageType.None);

        if (GUILayout.Button("Preview", GUILayout.Height(26))) hierarchyPlan = BuildHierarchyPlan();
        if (hierarchyPlan == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"{hierarchyPlan.MoveCount} objects will move into {hierarchyPlan.Moves.Count} folders", EditorStyles.boldLabel);
        foreach (var pair in hierarchyPlan.Moves)
        {
            DrawObjectList(pair.Key + $"  ({pair.Value.Count})", pair.Value);
        }

        EditorGUILayout.Space();
        DrawObjectList($"Stay at the top level, on purpose ({hierarchyPlan.Kept.Count})", hierarchyPlan.Kept.Select(k => k.Key).ToList(),
            hierarchyPlan.Kept.ToDictionary(k => k.Key, k => k.Value));
        DrawObjectList($"Not recognized, left where they are ({hierarchyPlan.Unsorted.Count})", hierarchyPlan.Unsorted);

        EditorGUILayout.Space();
        GUI.enabled = hierarchyPlan.MoveCount > 0;
        if (GUILayout.Button($"Apply: organize {hierarchyPlan.MoveCount} objects", GUILayout.Height(32)))
        {
            ApplyHierarchy(hierarchyPlan);
            hierarchyPlan = null;
        }
        GUI.enabled = true;
    }

    private void DrawObjectList(string title, List<GameObject> objects, Dictionary<GameObject, string> notes = null)
    {
        foldouts.TryGetValue(title, out bool open);
        open = EditorGUILayout.Foldout(open, title, true);
        foldouts[title] = open;
        if (!open) return;

        EditorGUI.indentLevel++;
        foreach (GameObject go in objects.Take(300))
        {
            if (go == null) continue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(go.name, GUILayout.MinWidth(160));
            if (notes != null && notes.TryGetValue(go, out string note)) EditorGUILayout.LabelField(note, EditorStyles.miniLabel);
            if (GUILayout.Button("Select", GUILayout.Width(50))) { Selection.activeGameObject = go; EditorGUIUtility.PingObject(go); }
            EditorGUILayout.EndHorizontal();
        }
        if (objects.Count > 300) EditorGUILayout.LabelField($"... and {objects.Count - 300} more");
        EditorGUI.indentLevel--;
    }

    private static HierarchyPlan BuildHierarchyPlan()
    {
        var plan = new HierarchyPlan();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (Categories.Contains(root.name)) continue; // our own folders

            string keepReason = KeepAtRootReason(root);
            if (keepReason != null)
            {
                plan.Kept.Add(new KeyValuePair<GameObject, string>(root, keepReason));
                continue;
            }

            string baseName = BaseName(root.name);
            Rule rule = Rules.FirstOrDefault(r => r.Pattern.IsMatch(baseName));
            if (rule == null)
            {
                plan.Unsorted.Add(root);
                continue;
            }

            if (!plan.Moves.TryGetValue(rule.Folder, out List<GameObject> list))
            {
                list = new List<GameObject>();
                plan.Moves[rule.Folder] = list;
            }
            list.Add(root);
        }
        return plan;
    }

    // Things whose behavior depends on being a top-level object
    private static string KeepAtRootReason(GameObject root)
    {
        if (root.GetComponentInChildren<PlayerMovement>(true) != null) return "the player";
        if (root.GetComponentInChildren<HitStop>(true) != null) return "HitStop uses transform.root";
        if (root.GetComponentInChildren<DontDestroyOnLoad>(true) != null) return "DontDestroyOnLoad only works at the top level";
        if (root.GetComponentInChildren<SoundManager>(true) != null) return "SoundManager persists between scenes";
        if (root.GetComponentInChildren<Camera>(true) != null) return "contains a camera";
        if (root.GetComponent<Canvas>() != null) return "UI canvas";
        if (root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null) return "UI event system";
        return null;
    }

    private static string BaseName(string name)
    {
        string trimmed = name.Trim().Trim('\'').Trim();
        return Regex.Replace(trimmed, @"\s*\(\d+\)$", "").Trim();
    }

    private static void ApplyHierarchy(HierarchyPlan plan)
    {
        Scene scene = SceneManager.GetActiveScene();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Organize Scene Hierarchy");

        foreach (var pair in plan.Moves)
        {
            Transform folder = GetOrCreateFolder(scene, pair.Key);
            var sorted = pair.Value.Where(g => g != null).OrderBy(g => g.name, Comparer<string>.Create(EditorUtility.NaturalCompare)).ToList();
            foreach (GameObject go in sorted)
            {
                Undo.SetTransformParent(go.transform, folder, "Organize Scene Hierarchy");
                go.transform.SetAsLastSibling();
            }
        }

        // Category folders after the systems that stay on top
        List<GameObject> roots = scene.GetRootGameObjects().ToList();
        int index = roots.Count - 1;
        for (int i = Categories.Length - 1; i >= 0; i--)
        {
            GameObject category = roots.FirstOrDefault(r => r.name == Categories[i]);
            if (category == null) continue;
            Undo.SetSiblingIndex(category.transform, index--, "Organize Scene Hierarchy");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Scene Organizer: moved {plan.MoveCount} objects into {plan.Moves.Count} folders. Ctrl+Z to undo; save the scene to keep it.");
    }

    // Folders are plain empty objects at the origin, so children keep their exact world positions
    private static Transform GetOrCreateFolder(Scene scene, string path)
    {
        Transform current = null;
        foreach (string part in path.Split('/'))
        {
            Transform next = current == null
                ? scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == part)
                : current.Find(part);

            if (next == null)
            {
                var go = new GameObject(part);
                Undo.RegisterCreatedObjectUndo(go, "Organize Scene Hierarchy");
                if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
                if (current != null) go.transform.SetParent(current, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                next = go.transform;
            }
            current = next;
        }
        return current;
    }

    // =========================================================================
    // VEGETATION
    // =========================================================================

    private void DrawVegetationTab()
    {
        EditorGUILayout.HelpBox(
            "Grass, bushes and flowers bend away from Rowdy / enemies walking through and spring back, throwing a few leaves in their own colors.\n" +
            "Vines sway from the top; trees get a breeze and shake leaves loose when bumped or slashed. Lighting stays the same (lit sprite shader with 2D lights).\n" +
            "Only sprites using the default Sprite-Lit material are changed. Running Apply again updates plants set up earlier.\nOne Undo step; Remove puts the original materials back.", MessageType.None);

        if (GUILayout.Button("Preview", GUILayout.Height(26))) vegetationPlan = BuildVegetationPlan();
        if (vegetationPlan != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{vegetationPlan.Entries.Count} sprites will get the vegetation setup", EditorStyles.boldLabel);
            foreach (var group in vegetationPlan.Entries.GroupBy(e => e.Value.Name))
            {
                DrawObjectList($"{group.Key}  ({group.Count()})", group.Select(e => e.Key.gameObject).ToList());
            }
            if (vegetationPlan.SkippedMaterial > 0) EditorGUILayout.LabelField($"Skipped {vegetationPlan.SkippedMaterial} with a custom material.", EditorStyles.miniLabel);
            if (vegetationPlan.SkippedGameplay > 0) EditorGUILayout.LabelField($"Skipped {vegetationPlan.SkippedGameplay} that are animated or part of gameplay objects.", EditorStyles.miniLabel);

            EditorGUILayout.Space();
            GUI.enabled = vegetationPlan.Entries.Count > 0;
            if (GUILayout.Button($"Apply to {vegetationPlan.Entries.Count} sprites", GUILayout.Height(32)))
            {
                ApplyVegetation(vegetationPlan);
                vegetationPlan = null;
            }
            GUI.enabled = true;
        }

        EditorGUILayout.Space(20);
        if (GUILayout.Button("Remove interactive vegetation from this scene"))
        {
            RemoveVegetation();
            vegetationPlan = null;
        }
    }

    private VegetationPlan BuildVegetationPlan()
    {
        var plan = new VegetationPlan();
        Scene scene = SceneManager.GetActiveScene();
        foreach (SpriteRenderer sr in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true)))
        {
            if (sr.sprite == null) continue;

            Profile profile = Profiles.FirstOrDefault(p => p.Pattern.IsMatch(sr.sprite.name))
                              ?? Profiles.FirstOrDefault(p => p.Pattern.IsMatch(BaseName(sr.gameObject.name)));
            if (profile == null) continue;

            Material mat = sr.sharedMaterial;
            bool isDefault = mat != null && mat.shader != null && mat.shader.name == DefaultSpriteShader;
            bool alreadyVegetation = mat != null && mat.shader != null && mat.shader.name == VegetationShader;
            if (!isDefault && !alreadyVegetation) { plan.SkippedMaterial++; continue; }

            // Leave animated / physical / gameplay sprites alone
            if (sr.GetComponent<Animator>() != null || sr.GetComponent<Rigidbody2D>() != null || sr.GetComponent<Collider2D>() != null
                || sr.GetComponentInParent<EnemyMovement>(true) != null || sr.GetComponentInParent<EnemyHealth>(true) != null
                || sr.GetComponentInParent<PlayerMovement>(true) != null)
            {
                plan.SkippedGameplay++;
                continue;
            }

            plan.Entries.Add(new KeyValuePair<SpriteRenderer, Profile>(sr, profile));
        }
        return plan;
    }

    private void ApplyVegetation(VegetationPlan plan)
    {
        Material vegetationMaterial = GetOrCreateVegetationMaterial();
        if (vegetationMaterial == null) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Interactive Vegetation");

        int done = 0;
        foreach (var entry in plan.Entries)
        {
            SpriteRenderer sr = entry.Key;
            Profile profile = entry.Value;
            if (sr == null) continue;

            Material original = sr.sharedMaterial;
            Undo.RecordObject(sr, "Setup Interactive Vegetation");
            sr.sharedMaterial = vegetationMaterial;

            InteractiveVegetation plant = sr.GetComponent<InteractiveVegetation>();
            if (plant == null) plant = Undo.AddComponent<InteractiveVegetation>(sr.gameObject);
            Undo.RecordObject(plant, "Setup Interactive Vegetation");
            SpriteInfo info = Analyze(sr.sprite);
            plant.Configure(profile.Mode, profile.Wind, profile.Push, info.ColorA, info.ColorB, profile.Rustle, info.VisibleMin, info.VisibleMax);
            if (original != null && original != vegetationMaterial) plant.OriginalMaterial = original;
            EditorUtility.SetDirty(plant);
            done++;
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"Interactive vegetation set up on {done} sprites. Ctrl+Z to undo; save the scene to keep it.");
    }

    private static void RemoveVegetation()
    {
        Scene scene = SceneManager.GetActiveScene();
        var plants = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<InteractiveVegetation>(true)).ToList();
        if (plants.Count == 0) { Debug.Log("No interactive vegetation in this scene."); return; }

        Material fallback = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove Interactive Vegetation");
        foreach (InteractiveVegetation plant in plants)
        {
            SpriteRenderer sr = plant.GetComponent<SpriteRenderer>();
            Material restore = plant.OriginalMaterial != null ? plant.OriginalMaterial : fallback;
            if (sr != null && restore != null)
            {
                Undo.RecordObject(sr, "Remove Interactive Vegetation");
                sr.sharedMaterial = restore;
            }
            Undo.DestroyObjectImmediate(plant);
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"Removed interactive vegetation from {plants.Count} sprites.");
    }

    private static Material GetOrCreateVegetationMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(VegetationMaterialPath);
        if (mat != null) return mat;

        Shader shader = Shader.Find(VegetationShader);
        if (shader == null)
        {
            EditorUtility.DisplayDialog("Interactive Vegetation", $"Shader '{VegetationShader}' wasn't found. Check the Console for shader errors.", "OK");
            return null;
        }
        mat = new Material(shader) { name = "M_Vegetation" };
        AssetDatabase.CreateAsset(mat, VegetationMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private class SpriteInfo
    {
        public Color ColorA = new Color(0.9f, 0.35f, 0.55f);
        public Color ColorB = new Color(0.55f, 0.15f, 0.35f);
        public Vector2 VisibleMin, VisibleMax; // sprite local space; zero = unknown
    }

    // Reads the sprite's pixels: the box around its visible pixels (so empty space never reacts),
    // and two leaf colors from the upper part of what's visible (ignoring dark outlines)
    private SpriteInfo Analyze(Sprite sprite)
    {
        if (spriteCache.TryGetValue(sprite, out SpriteInfo cached)) return cached;

        var info = new SpriteInfo();
        string path = AssetDatabase.GetAssetPath(sprite.texture);
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            var tex = new Texture2D(2, 2);
            if (tex.LoadImage(File.ReadAllBytes(path)))
            {
                // The file can be bigger than the imported texture (max size setting)
                float scale = sprite.texture.width > 0 ? tex.width / (float)sprite.texture.width : 1f;
                Rect r = sprite.rect;
                int x = Mathf.Clamp(Mathf.RoundToInt(r.x * scale), 0, tex.width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(r.y * scale), 0, tex.height - 1);
                int w = Mathf.Clamp(Mathf.RoundToInt(r.width * scale), 1, tex.width - x);
                int h = Mathf.Clamp(Mathf.RoundToInt(r.height * scale), 1, tex.height - y);
                Color[] pixels = tex.GetPixels(x, y, w, h);

                int minX = w, minY = h, maxX = -1, maxY = -1;
                for (int py = 0; py < h; py++)
                {
                    for (int px = 0; px < w; px++)
                    {
                        if (pixels[py * w + px].a <= 0.1f) continue;
                        if (px < minX) minX = px;
                        if (px > maxX) maxX = px;
                        if (py < minY) minY = py;
                        if (py > maxY) maxY = py;
                    }
                }

                if (maxX >= minX && maxY >= minY)
                {
                    float pixelsPerUnit = sprite.pixelsPerUnit * scale;
                    Vector2 pivot = sprite.pivot * scale;
                    info.VisibleMin = (new Vector2(minX, minY) - pivot) / pixelsPerUnit;
                    info.VisibleMax = (new Vector2(maxX + 1, maxY + 1) - pivot) / pixelsPerUnit;

                    // Leaf colors from the top 60% of the visible part
                    int foliageFromY = minY + (maxY - minY) * 2 / 5;
                    var usable = new List<Color>();
                    for (int py = foliageFromY; py <= maxY; py++)
                    {
                        for (int px = minX; px <= maxX; px++)
                        {
                            Color c = pixels[py * w + px];
                            if (c.a > 0.5f && Luminance(c) > 0.12f) usable.Add(c);
                        }
                    }
                    usable.Sort((a, b) => Luminance(a).CompareTo(Luminance(b)));
                    if (usable.Count > 8)
                    {
                        info.ColorA = usable[(int)(usable.Count * 0.75f)];
                        info.ColorB = usable[(int)(usable.Count * 0.4f)];
                    }
                }
            }
            DestroyImmediate(tex);
        }
        info.ColorA.a = info.ColorB.a = 1f;
        spriteCache[sprite] = info;
        return info;
    }

    private static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
}
