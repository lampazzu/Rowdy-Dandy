using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Builds THE FRONTIER into the open scene (Tools > Rowdy Dandy > Frontier > Build The Frontier). Safe to run again:
// it removes its own previous build first ("The Frontier" root + the tiles it painted).
//   1. The Volt Rat enemy: slices PIV_Flying_Rat into sprites, makes its clips + animator + prefab
//      (Rowdy Dandy/Enemies/VoltRat/*, Resources/Enemies Prefab/Enemy_VoltRat.prefab). Edit the clips freely.
//   2. The sea past Pelich (Watermap tiles, 253 -> 266).
//   3. The island: a copy of Pelich's arena ground - the collision tiles (BeachMap) and every decoration sprite
//      under [Environment] / [Atmosphere] - moved 44 units right (x 266 -> 297). Decorate it over as you like.
//   4. A checkpoint where you land, and the colosseum: FrontierArena (gates, gong, rounds) on the arena floor.
public static class FrontierBuilder
{
    private const float Shift = 44f;                      // island = Pelich's arena moved this far right
    private const float SourceX0 = 221f, SourceX1 = 254f; // the part of the map that gets copied
    private const float SourceY0 = -16f, SourceY1 = 12f;
    private const float SeaX0 = 252f, SeaX1 = 268f;
    private const string RootName = "The Frontier";
    private const string RatDir = "Assets/Rowdy Dandy/Enemies/VoltRat";
    private const string RatSheet = "Assets/Rowdy Dandy/New Stuff you can use/PIV_Flying_Rat.png";
    private const string RatPrefab = "Assets/Resources/Enemies Prefab/Enemy_VoltRat.prefab";

    [MenuItem("Tools/Rowdy Dandy/Frontier/Build The Frontier")]
    public static void Build()
    {
        BuildVoltRat();
        BuildIsland();
    }

    [MenuItem("Tools/Rowdy Dandy/Frontier/Build Volt Rat")]
    public static void BuildVoltRat()
    {
        if (!AssetDatabase.IsValidFolder(RatDir)) AssetDatabase.CreateFolder("Assets/Rowdy Dandy/Enemies", "VoltRat");

        // 1. slice the sheet (10 x 2) into named sprites
        var importer = (TextureImporter)AssetImporter.GetAtPath(RatSheet);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RatSheet);
        if (importer.spriteImportMode != SpriteImportMode.Multiple || importer.spritesheet == null || importer.spritesheet.Length != 20)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            float w = tex.width / 10f, h = tex.height / 2f;
            var metas = new List<SpriteMetaData>();
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 10; c++)
                {
                    float x0 = Mathf.Round(c * w), x1 = Mathf.Round((c + 1) * w);
                    float y0 = Mathf.Round(tex.height - (r + 1) * h), y1 = Mathf.Round(tex.height - r * h);
                    metas.Add(new SpriteMetaData { name = "VoltRat_" + (r * 10 + c), rect = new Rect(x0, y0, x1 - x0, y1 - y0), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 0.3f) });
                }
#pragma warning disable 0618
            importer.spritesheet = metas.ToArray();
#pragma warning restore 0618
            importer.SaveAndReimport();
        }
        var sprites = AssetDatabase.LoadAllAssetsAtPath(RatSheet).OfType<Sprite>().ToDictionary(s => s.name);
        Sprite F(int i) => sprites.TryGetValue("VoltRat_" + i, out Sprite s) ? s : null;

        // 2. clips
        AnimationClip fly = Clip("Fly", 12f, true, F(0), F(1), F(2), F(1));
        AnimationClip charge = Clip("Charge", 18f, true, F(3), F(4), F(5), F(4));
        AnimationClip dash = Clip("Dash", 16f, true, F(6), F(7));
        AnimationClip recover = Clip("Recover", 10f, false, F(7), F(6), F(0), F(1));
        AnimationClip hit = Clip("GetHit", 14f, false, F(4), F(0), F(4), F(0));
        AnimationClip death = Clip("Death", 14f, false, F(10), F(11), F(12), F(13), F(14), F(15), F(16), F(17), F(18), F(19));

        // 3. animator: Fly (also 'Walk', which EnemyHealth falls back to after a hit), Charge, Dash, Recover, GetHit, Death
        string ctrlPath = RatDir + "/VoltRat.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        ctrl.AddParameter("destroyed", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("back to idle", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState sFly = sm.AddState("Fly"); sFly.motion = fly;
        AnimatorState sWalk = sm.AddState("Walk"); sWalk.motion = fly;
        AnimatorState sCharge = sm.AddState("Charge"); sCharge.motion = charge;
        AnimatorState sDash = sm.AddState("Dash"); sDash.motion = dash;
        AnimatorState sRecover = sm.AddState("Recover"); sRecover.motion = recover;
        AnimatorState sHit = sm.AddState("GetHit"); sHit.motion = hit;
        AnimatorState sDeath = sm.AddState("Death"); sDeath.motion = death;
        sm.defaultState = sFly;
        AnimatorStateTransition toDeath = sm.AddAnyStateTransition(sDeath);
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "destroyed");
        toDeath.duration = 0f; toDeath.canTransitionToSelf = false;
        AnimatorStateTransition hitBack = sHit.AddTransition(sFly);
        hitBack.hasExitTime = true; hitBack.exitTime = 1f; hitBack.duration = 0f;
        AnimatorStateTransition recoverBack = sRecover.AddTransition(sFly);
        recoverBack.hasExitTime = true; recoverBack.exitTime = 1f; recoverBack.duration = 0f;
        AnimatorStateTransition idle = sm.AddAnyStateTransition(sFly);
        idle.AddCondition(AnimatorConditionMode.If, 0f, "back to idle");
        idle.duration = 0f;

        // 4. prefab
        GameObject reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Enemies Prefab/Enemy_Werefast.prefab");
        SpriteRenderer refSr = reference != null ? reference.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.GetComponent<EnemyHealth>() != null) : null;
        var go = new GameObject("Enemy_VoltRat");
        go.tag = "Enemy";
        go.layer = LayerMask.NameToLayer("Enemy");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = F(0);
        sr.color = new Color(1f, 0.82f, 0.95f); // a touch of violet: not the friendly legendary rat
        if (refSr != null) { sr.sharedMaterial = refSr.sharedMaterial; sr.sortingLayerID = refSr.sortingLayerID; sr.sortingOrder = refSr.sortingOrder + 1; }
        var anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.42f, 0.26f);
        col.offset = new Vector2(0f, 0.08f);
        var health = go.AddComponent<EnemyHealth>();
        var so = new SerializedObject(health);
        so.FindProperty("startingenemyHealth").floatValue = 30f;
        so.FindProperty("expGemPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Systems/EXPgem.prefab");
        so.FindProperty("expGemAmount").intValue = 2;
        so.FindProperty("dropChancePercent").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        var ai = go.AddComponent<VoltRat>();
        var aiSo = new SerializedObject(ai);
        aiSo.FindProperty("crackle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Rowdy Dandy/Sound Effects/electric-impact-37128.mp3");
        aiSo.ApplyModifiedPropertiesWithoutUndo();
        // the zap: only on while dashing
        var hb = new GameObject("Hitbox");
        hb.transform.SetParent(go.transform, false);
        hb.layer = go.layer;
        hb.tag = "Untagged";
        var hbCol = hb.AddComponent<BoxCollider2D>();
        hbCol.isTrigger = true;
        hbCol.size = new Vector2(0.5f, 0.32f);
        hbCol.offset = new Vector2(0f, 0.08f);
        var spike = hb.AddComponent<Spike>();
        var spikeSo = new SerializedObject(spike);
        spikeSo.FindProperty("damage").floatValue = 12f;
        spikeSo.FindProperty("telegraphContact").boolValue = false;
        spikeSo.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(go, RatPrefab);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Debug.Log("Volt Rat ready: " + RatPrefab);
    }

    private static AnimationClip Clip(string name, float fps, bool loop, params Sprite[] frames)
    {
        string path = RatDir + "/VoltRat_" + name + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] }; // hold the last frame its full length
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    // ---------------------------------------------------------------- the island
    public static void BuildIsland()
    {
        var scene = EditorSceneManager.GetActiveScene();
        GameObject old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old != null) Object.DestroyImmediate(old);

        Tilemap beach = FindTilemap("BeachMap"), water = FindTilemap("Watermap");
        if (beach == null || water == null) { Debug.LogError("Frontier: BeachMap / Watermap not found"); return; }

        // 2. collision tiles: clear the target, copy Pelich's arena block across
        int shiftCells = Mathf.RoundToInt(Shift / beach.layoutGrid.cellSize.x);
        Vector3Int a = beach.WorldToCell(new Vector3(SourceX0, SourceY0, 0f)), b = beach.WorldToCell(new Vector3(SourceX1, SourceY1, 0f));
        Undo.RegisterCompleteObjectUndo(beach, "Frontier tiles");
        for (int x = a.x; x <= b.x; x++)
            for (int y = a.y; y <= b.y; y++)
                beach.SetTile(new Vector3Int(x + shiftCells, y, 0), beach.GetTile(new Vector3Int(x, y, 0)));
        beach.RefreshAllTiles();

        // the sea between Pelich and the island: copy the last water column across
        Undo.RegisterCompleteObjectUndo(water, "Frontier sea");
        water.CompressBounds();
        BoundsInt wb = water.cellBounds;
        int sourceCol = wb.xMax - 2;
        Vector3Int s0 = water.WorldToCell(new Vector3(SeaX0, 0f, 0f)), s1 = water.WorldToCell(new Vector3(SeaX1, 0f, 0f));
        for (int x = s0.x; x <= s1.x; x++)
            for (int y = wb.yMin; y < wb.yMax; y++)
            {
                TileBase t = water.GetTile(new Vector3Int(sourceCol, y, 0));
                if (t != null) water.SetTile(new Vector3Int(x, y, 0), t);
            }
        water.RefreshAllTiles();
        foreach (var c in new Component[] { beach, water })
        {
            var tc = c.GetComponent<TilemapCollider2D>(); if (tc != null) tc.ProcessTilemapChanges();
            var comp = c.GetComponent<CompositeCollider2D>(); if (comp != null) comp.GenerateGeometry();
        }

        // 3. decorations
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Frontier");
        var decor = new GameObject("Decor (copied from Pelich's arena)");
        decor.transform.SetParent(root.transform, false);
        int copied = 0;
        foreach (GameObject top in scene.GetRootGameObjects())
        {
            if (top.name != "[Environment]" && top.name != "[Atmosphere]") continue;
            foreach (SpriteRenderer r in top.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Vector3 c = r.bounds.center;
                if (c.x < SourceX0 || c.x > SourceX1 || c.y < SourceY0 || c.y > SourceY1) continue;
                GameObject src = r.gameObject;
                if (src.GetComponentInParent<EnemyHealth>(true) != null || src.GetComponent<Jellyfish>() != null) continue;
                if (src.transform.parent != null && src.transform.parent.GetComponent<SpriteRenderer>() != null) continue; // copied with its parent
                GameObject copy = Object.Instantiate(src, decor.transform);
                copy.name = src.name;
                copy.transform.position = src.transform.position + new Vector3(Shift, 0f, 0f);
                copy.transform.rotation = src.transform.rotation;
                copy.transform.localScale = src.transform.lossyScale;
                copied++;
            }
        }

        // the sea needs its surface too: a copy of the water body closest to Pelich, stretched over the new sea
        DynamicWater2D nearest = null;
        foreach (DynamicWater2D w in Object.FindObjectsByType<DynamicWater2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (w.transform.IsChildOf(root.transform)) continue;
            if (nearest == null || Mathf.Abs(w.transform.position.x - SeaX0) < Mathf.Abs(nearest.transform.position.x - SeaX0)) nearest = w;
        }
        if (nearest != null)
        {
            GameObject sea = Object.Instantiate(nearest.gameObject, root.transform);
            sea.name = "Frontier Sea";
            sea.transform.localScale = nearest.transform.lossyScale;
            var box = sea.GetComponent<BoxCollider2D>();
            var srcBox = nearest.GetComponent<BoxCollider2D>();
            float sx = Mathf.Abs(sea.transform.lossyScale.x);
            float worldTop = srcBox.bounds.max.y;
            box.size = new Vector2((SeaX1 - SeaX0) / Mathf.Max(0.0001f, sx), srcBox.size.y);
            box.offset = new Vector2(0f, srcBox.offset.y);
            sea.transform.position = new Vector3((SeaX0 + SeaX1) / 2f, nearest.transform.position.y, nearest.transform.position.z);
        }

        // 4. checkpoint + arena
        float floor = -3.4f;
        GameObject spawner = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Interactables/Spawner.prefab");
        if (spawner != null)
        {
            var cp = (GameObject)PrefabUtility.InstantiatePrefab(spawner, root.transform);
            cp.name = "Frontier Checkpoint";
            cp.transform.position = new Vector3(231.5f + Shift, -3.4f, 0f); // where Pelich's arena has its spawner, before the gate
        }
        var arenaGo = new GameObject("Colosseum (FrontierArena)");
        arenaGo.transform.SetParent(root.transform, false);
        var arena = arenaGo.AddComponent<FrontierArena>();
        arena.leftX = 233f + Shift;
        arena.rightX = 251f + Shift;
        arena.floorY = floor;
        arena.islandStartX = 256f;
        var gong = new GameObject("War Gong");
        gong.transform.SetParent(arenaGo.transform, false);
        gong.transform.position = new Vector3(arena.leftX + 1.6f, floor, 0f);
        var gsr = gong.AddComponent<SpriteRenderer>();
        gsr.sprite = null; // drawn at runtime (FrontierArena.GongSprite)
        arena.gong = gong.transform;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("The Frontier built: " + copied + " decorations copied, island at x " + (SourceX0 + Shift) + "-" + (SourceX1 + Shift));
        File.WriteAllText("Temp/frontier_build.txt", "decor " + copied + "\nshiftCells " + shiftCells + "\ntiles " + a + " .. " + b + "\nsea " + s0 + " .. " + s1 + " from col " + sourceCol);
    }

    private static Tilemap FindTilemap(string name) =>
        Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == name);
}
