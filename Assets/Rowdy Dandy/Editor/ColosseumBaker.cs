using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Tools > Rowdy Dandy > Level Editing > Bake Colosseums Into Scene
// Puts THE GLOOMWOOD and THE PURPLE REIGN into the open scene as real objects (the same islands the game used to
// build at Play: ground tilemap, walls, decoration, platforms, lights, the pool), so they can be edited by hand like
// the rest of the map. At Play the game then uses them as they are and only adds the logic (colosseum, gong, portals,
// sky). An island that's already in the scene is never touched: delete its root to bake it again.
// Art cut from sheets at runtime / drawn in code is saved to Assets/Scenery/Colosseums/<island> Art.asset so the
// scene keeps it; runtime materials are swapped for URP's Sprite-Lit-Default / Sprite-Unlit-Default.
public static class ColosseumBaker
{
    private const string Folder = "Assets/Scenery/Colosseums";
    private const string LitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
    private const string UnlitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    [MenuItem("Tools/Rowdy Dandy/Level Editing/Bake Colosseums Into Scene")]
    private static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Bake Colosseums", "Leave Play mode first.", "OK"); return; }
        FrontierArena frontier = null;
        foreach (FrontierArena a in Object.FindObjectsByType<FrontierArena>(FindObjectsSortMode.None)) if (!a.gloom) { frontier = a; break; }
        if (frontier == null) { EditorUtility.DisplayDialog("Bake Colosseums", "No Frontier colosseum (FrontierArena) in the open scene.", "OK"); return; }

        // the Frontier's floor as the game sees it at Play (fitted gates, floor found by ray)
        Physics2D.SyncTransforms();
        float floor = frontier.floorY;
        if (SolidGround.Ray(new Vector2((frontier.leftX + frontier.rightX) / 2f, floor + 4f), Vector2.down, 10f, out RaycastHit2D hit)) floor = hit.point.y;
        Vector2 bounds = frontier.FittedBounds();
        Vector3? gong = frontier.gong != null ? frontier.gong.position : (Vector3?)null;

        var made = new List<string>();
        var skipped = new List<string>();
        foreach (int tier in new[] { 1, 2 })
        {
            string title = tier == 2 ? "The Purple Reign" : "The Gloomwood";
            if (Existing(tier) != null) { skipped.Add(title); continue; }
            GameObject root = GloomArena.BakeIsland(tier, bounds.x, bounds.y, floor, gong);
            if (root == null) continue;
            KeepArt(root, title);
            Undo.RegisterCreatedObjectUndo(root, "Bake " + title);
            made.Add(title);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(frontier.gameObject.scene);

        string msg = (made.Count > 0 ? "Baked into the scene: " + string.Join(", ", made) + ".\nSave the scene to keep them." : "Nothing baked.")
                     + (skipped.Count > 0 ? "\n\nAlready in the scene (left alone): " + string.Join(", ", skipped) + ". Delete its root to bake it again." : "");
        Debug.Log("<color=cyan>[Level Editing]</color> " + msg.Replace("\n", " "));
        EditorUtility.DisplayDialog("Bake Colosseums", msg, "OK");
        if (made.Count > 0)
        {
            GloomArenaBaked first = Existing(made[0] == "The Purple Reign" ? 2 : 1);
            if (first != null) { Selection.activeGameObject = first.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
        }
    }

    private static GloomArenaBaked Existing(int tier)
    {
        foreach (GloomArenaBaked b in Object.FindObjectsByType<GloomArenaBaked>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.tier == tier) return b;
        return null;
    }

    // ---------------------------------------------------------------- make everything survive a scene save
    internal static void KeepArt(GameObject root, string title)
    {
        Material lit = AssetDatabase.LoadAssetAtPath<Material>(LitPath), unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitPath);
        if (!AssetDatabase.IsValidFolder("Assets/Scenery")) AssetDatabase.CreateFolder("Assets", "Scenery");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Scenery", "Colosseums");
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + title + " Art.asset");
        GloomArenaBakedArt container = null;
        var saved = new Dictionary<Object, Object>();

        Object Keep(Object o)
        {
            if (o == null || EditorUtility.IsPersistent(o)) return o;
            if (saved.TryGetValue(o, out Object done)) return done;
            if (container == null)
            {
                container = ScriptableObject.CreateInstance<GloomArenaBakedArt>();
                AssetDatabase.CreateAsset(container, path);
            }
            if (o is Sprite s && s.texture != null && !EditorUtility.IsPersistent(s.texture))
                Keep(s.texture); // the texture first, so the sprite points at the saved one
            if (string.IsNullOrEmpty(o.name)) o.name = o.GetType().Name;
            AssetDatabase.AddObjectToAsset(o, container);
            saved[o] = o;
            return o;
        }

        Material Mat(Material m)
        {
            if (m == null || EditorUtility.IsPersistent(m)) return m;
            string shader = m.shader != null ? m.shader.name : "";
            if (shader.Contains("Sprite-Lit") && lit != null) return lit;
            if (shader.Contains("Sprite-Unlit") && unlit != null) return unlit;
            return (Material)Keep(m);
        }

        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite != null && !EditorUtility.IsPersistent(sr.sprite)) Keep(sr.sprite);
            sr.sharedMaterial = Mat(sr.sharedMaterial);
        }
        foreach (TilemapRenderer tr in root.GetComponentsInChildren<TilemapRenderer>(true)) tr.sharedMaterial = Mat(tr.sharedMaterial);
        if (container != null) EditorUtility.SetDirty(container);
    }
}
