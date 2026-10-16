using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Rowdy Dandy > Level Editing > Bake Tutorial Level Into Scene
// Puts DANDY SCHOOL (the tutorial level, TutorialLevel) into the open scene as real objects - ground tilemap, walls,
// platforms, the pool, props, the stations with their signs - so it can be decorated and changed by hand like the rest
// of the map (put your art under "Decor (yours)", move stations, edit their hints / cards in the Inspector).
// At Play the game then uses it as it is and only adds the portals. A tutorial level already in the scene is never
// touched: delete its root to bake it again.
public static class TutorialBaker
{
    [MenuItem("Tools/Rowdy Dandy/Level Editing/Bake Tutorial Level Into Scene")]
    private static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Bake Tutorial Level", "Leave Play mode first.", "OK"); return; }
        TutorialLevel existing = Object.FindFirstObjectByType<TutorialLevel>(FindObjectsInactive.Include);
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Bake Tutorial Level", "There's already a tutorial level in the scene (" + existing.name + "). Delete its root to bake it again.", "OK");
            Selection.activeGameObject = existing.gameObject;
            return;
        }
        float floor = -3.4f;
        foreach (FrontierArena a in Object.FindObjectsByType<FrontierArena>(FindObjectsSortMode.None)) if (!a.gloom) { floor = a.floorY; break; }
        Physics2D.SyncTransforms();
        GameObject root = TutorialBuilder.Build(new Vector3(TutorialLevel.DefaultX, floor, 0f));
        if (root == null) return;
        ColosseumBaker.KeepArt(root, "Tutorial Level");
        Undo.RegisterCreatedObjectUndo(root, "Bake Tutorial Level");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("<color=cyan>[Level Editing]</color> Tutorial level baked into the scene at x " + TutorialLevel.DefaultX + ". Save the scene to keep it.");
        EditorUtility.DisplayDialog("Bake Tutorial Level", "DANDY SCHOOL is in the scene now (far left, x " + TutorialLevel.DefaultX + ").\nSave the scene to keep it.", "OK");
    }

    // Jump the Scene view to it (baked or not, this frames where it is / would be)
    [MenuItem("Tools/Rowdy Dandy/Level Editing/Frame Tutorial Level")]
    private static void Frame()
    {
        TutorialLevel t = Object.FindFirstObjectByType<TutorialLevel>(FindObjectsInactive.Include);
        if (t != null) { Selection.activeGameObject = t.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); return; }
        SceneView v = SceneView.lastActiveSceneView;
        if (v != null) v.LookAt(new Vector3(TutorialLevel.DefaultX + TutorialLevel.Length / 2f, -3.4f, 0f), v.rotation, 30f);
    }
}
