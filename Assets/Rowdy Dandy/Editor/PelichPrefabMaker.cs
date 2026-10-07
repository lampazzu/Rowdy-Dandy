using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Rowdy Dandy > Make Pelich Prefab: saves the open scene's "PelichAnus" (with a PelichBoss component) as
// Assets/Resources/Enemies Prefab/Enemy_Pelich.prefab and links the scene object to it. Save the scene afterwards.
// Undo-able up to the prefab file itself (delete the file to start over).
public static class PelichPrefabMaker
{
    private const string PrefabPath = "Assets/Resources/Enemies Prefab/Enemy_Pelich.prefab";

    [MenuItem("Tools/Rowdy Dandy/Make Pelich Prefab")]
    private static void Make()
    {
        GameObject pelich = GameObject.Find("PelichAnus");
        if (pelich == null)
        {
            EditorUtility.DisplayDialog("Make Pelich Prefab", "No active object named \"PelichAnus\" in the open scene.", "OK");
            return;
        }
        if (PrefabUtility.IsPartOfPrefabInstance(pelich))
        {
            EditorUtility.DisplayDialog("Make Pelich Prefab", "PelichAnus is already a prefab instance:\n" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(pelich), "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("Make Pelich Prefab", PrefabPath + " already exists. Replace it?", "Replace", "Cancel"))
            return;

        if (pelich.GetComponent<PelichBoss>() == null) Undo.AddComponent<PelichBoss>(pelich);
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(pelich, PrefabPath, InteractionMode.UserAction);
        EditorSceneManager.MarkSceneDirty(pelich.scene);
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("Pelich prefab saved to " + PrefabPath + ". Save the scene to keep the link.");
    }
}
