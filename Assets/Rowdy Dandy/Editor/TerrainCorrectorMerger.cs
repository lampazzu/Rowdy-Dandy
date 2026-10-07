using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Tools > Rowdy Dandy > Merge Terrain Correctors Into Tilemaps
// TerrainCorrector polygons/boxes are separate colliders laid over the tilemaps. Where their edges meet the tile
// surface there are tiny steps and hidden vertices that Rowdy's box snags on (and launches off at slope crests).
// This moves each corrector under the solid tilemap it touches and sets it to Merge into that map's
// CompositeCollider2D, so map + correctors become one seamless outline. The collision stays exactly where it was
// (the composite's offset is compensated). One-way platforms are left alone.
// One Undo step: Ctrl+Z puts it all back. Save the scene afterwards to keep it.
public static class TerrainCorrectorMerger
{
    private const string UndoName = "Merge Terrain Correctors";
    private const float MaxGap = 0.1f; // a corrector further than this from every map is skipped

    [MenuItem("Tools/Rowdy Dandy/Merge Terrain Correctors Into Tilemaps")]
    private static void Merge()
    {
        List<CompositeCollider2D> maps = Object.FindObjectsByType<CompositeCollider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(c => c.enabled && !c.usedByEffector && c.GetComponent<TilemapCollider2D>() != null)
            .ToList();

        if (maps.Count == 0)
        {
            Debug.LogWarning("TerrainCorrectorMerger: no solid tilemap with a CompositeCollider2D found in the open scene.");
            return;
        }

        List<GameObject> correctors = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("TerrainCorrector"))
            .Select(t => t.gameObject)
            .ToList();

        Physics2D.SyncTransforms();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);

        HashSet<CompositeCollider2D> touchedMaps = new HashSet<CompositeCollider2D>();
        int merged = 0;
        List<string> skipped = new List<string>();

        foreach (GameObject go in correctors)
        {
            List<Collider2D> colliders = go.GetComponents<Collider2D>()
                .Where(c => c.enabled && !c.isTrigger && c.compositeOperation == Collider2D.CompositeOperation.None)
                .ToList();
            if (colliders.Count == 0) continue; // nothing active to merge, or already merged

            // The map this corrector sits on = the closest one (negative distance = overlapping)
            CompositeCollider2D bestMap = null;
            float bestDistance = float.MaxValue;
            foreach (CompositeCollider2D map in maps)
            {
                foreach (Collider2D col in colliders)
                {
                    ColliderDistance2D d = Physics2D.Distance(col, map);
                    if (d.isValid && d.distance < bestDistance)
                    {
                        bestDistance = d.distance;
                        bestMap = map;
                    }
                }
            }

            if (bestMap == null || bestDistance > MaxGap)
            {
                skipped.Add(go.name);
                continue;
            }

            Undo.SetTransformParent(go.transform, bestMap.transform, true, UndoName);

            // The composite shifts everything merged into it by its offset; move the corrector the other way so it stays put
            Undo.RecordObject(go.transform, UndoName);
            go.transform.localPosition -= (Vector3)bestMap.offset;

            foreach (Collider2D col in colliders)
            {
                Undo.RecordObject(col, UndoName);
                col.compositeOperation = Collider2D.CompositeOperation.Merge;
            }

            touchedMaps.Add(bestMap);
            merged++;
        }

        foreach (CompositeCollider2D map in touchedMaps)
        {
            Undo.RecordObject(map, UndoName);
            map.GenerateGeometry();
        }

        EditorSceneManager.MarkAllScenesDirty();

        string mapNames = string.Join(", ", touchedMaps.Select(m => m.name));
        Debug.Log($"TerrainCorrectorMerger: merged {merged} correctors into {mapNames}. Save the scene to keep it (Ctrl+Z undoes).");
        if (skipped.Count > 0)
        {
            Debug.LogWarning($"TerrainCorrectorMerger: skipped (not touching any solid tilemap): {string.Join(", ", skipped)}");
        }
    }
}
