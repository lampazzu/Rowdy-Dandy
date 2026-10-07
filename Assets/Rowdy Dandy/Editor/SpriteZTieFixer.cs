using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Rowdy Dandy > Fix Sprite Z Ties (Selection / Scene)
// Sprites with the same Sorting Layer + Order in Layer + Z swap front/back every frame (the "twitch").
// This nudges only those tied sprites by a tiny Z step so each has a stable place.
// Sorting Layer and Order in Layer still win over Z, so your layer order is untouched,
// and sprites you already separated with Z keep their front/back order.
// One Undo step: Ctrl+Z puts it all back. Save the scene afterwards to keep it.
public static class SpriteZTieFixer
{
    private const float Step = 0.001f;
    private const string UndoName = "Fix Sprite Z Ties";

    [MenuItem("Tools/Rowdy Dandy/Fix Sprite Z Ties (Selection)")]
    private static void FixSelection()
    {
        var renderers = Selection.gameObjects
            .SelectMany(go => go.GetComponentsInChildren<SpriteRenderer>(true))
            .Distinct()
            .ToList();

        if (renderers.Count == 0)
        {
            Debug.LogWarning("SpriteZTieFixer: select the decoration objects (or their parent) first.");
            return;
        }

        Fix(renderers);
    }

    [MenuItem("Tools/Rowdy Dandy/Fix Sprite Z Ties (Whole Scene)")]
    private static void FixScene()
    {
        var renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .ToList();

        Fix(renderers);
    }

    private static void Fix(List<SpriteRenderer> renderers)
    {
        // Prefab assets / runtime-spawned objects are not touched, only scene objects
        renderers = renderers.Where(r => r.gameObject.scene.IsValid()).ToList();

        // Sprites inside a Sorting Group only sort against siblings of that group
        var groups = renderers.GroupBy(r => (
            group: r.GetComponentInParent<SortingGroup>(true),
            layer: r.sortingLayerID,
            order: r.sortingOrder));

        var targetZ = new Dictionary<Transform, float>();

        foreach (var group in groups)
        {
            // Back to front (bigger Z = farther from the camera); ties broken by hierarchy order so it's stable
            var sorted = group
                .OrderByDescending(r => r.transform.position.z)
                .ThenBy(r => HierarchyKey(r.transform))
                .ToList();

            float previousZ = float.PositiveInfinity;

            foreach (var r in sorted)
            {
                float z = Mathf.Min(r.transform.position.z, previousZ - Step);
                previousZ = z;

                if (!Mathf.Approximately(z, r.transform.position.z))
                    targetZ[r.transform] = z;
            }
        }

        // Parents first, so moving a parent doesn't undo a child's new Z
        var ordered = targetZ.Keys.OrderBy(Depth).ToList();

        Undo.RecordObjects(ordered.ToArray(), UndoName);

        foreach (var t in ordered)
        {
            Vector3 p = t.position;
            p.z = targetZ[t];
            t.position = p;
        }

        foreach (var scene in ordered.Select(t => t.gameObject.scene).Distinct())
            EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"SpriteZTieFixer: checked {renderers.Count} sprites, nudged {ordered.Count} tied ones.");
    }

    private static int Depth(Transform t)
    {
        int depth = 0;
        for (; t.parent != null; t = t.parent)
            depth++;
        return depth;
    }

    // Sortable path of sibling indices, e.g. "0003/0012/0001"
    private static string HierarchyKey(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent)
            parts.Add(t.GetSiblingIndex().ToString("D5"));
        parts.Reverse();
        return string.Join("/", parts);
    }
}
