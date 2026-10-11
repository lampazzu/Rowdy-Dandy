using UnityEngine;

// A colosseum island that lives IN THE SCENE (made by Tools > Rowdy Dandy > Level Editing > Bake Colosseums Into Scene):
// edit it like the rest of the map - paint its Ground tilemap, move / add / delete decoration, platforms, lights.
// At Play, GloomArena uses it instead of building the island from code; only the logic is still added then:
// the colosseum itself (waves, gates), the gong, the portals and the sky.
//   Left X / Right X / Floor Y   - the arena floor between the two gates (the gates also push out to where the floor
//                                  really ends, like the Frontier's). Drawn as orange lines in the Scene view.
//   Gong Spot / Gate spots       - empty children: move them to move the gong and the portals.
//   Pool (Purple Reign)          - the child "Pool": its "Water Body" box is where sea creatures live and walkers drown.
public class GloomArenaBaked : MonoBehaviour
{
    [Tooltip("1 The Gloomwood, 2 The Purple Reign")]
    public int tier = 1;
    [Tooltip("Arena floor between the two gates (world x)")]
    public float leftX, rightX;
    [Tooltip("Arena floor height (world y)")]
    public float floorY;
    [Tooltip("Where the gong stands (its feet)")]
    public Transform gongSpot;
    [Tooltip("The portal back to the previous colosseum")]
    public Transform gateBackSpot;
    [Tooltip("The portal on to the next colosseum (Gloomwood only)")]
    public Transform gateOnSpot;

    public static GloomArenaBaked Find(int tier)
    {
        foreach (GloomArenaBaked b in FindObjectsByType<GloomArenaBaked>(FindObjectsSortMode.None))
            if (b.tier == tier && b.isActiveAndEnabled) return b;
        return null;
    }

    // The Purple Reign's pool, read from the "Pool/Water Body" box (so moving or resizing it in the scene just works)
    public bool Pool(out float x0, out float x1, out float surface)
    {
        x0 = x1 = surface = 0f;
        Transform body = transform.Find("Pool/Water Body");
        if (body == null || !body.TryGetComponent(out BoxCollider2D box)) return false;
        Bounds b = box.bounds;
        x0 = b.min.x; x1 = b.max.x; surface = b.max.y;
        return true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        Gizmos.DrawLine(new Vector3(leftX, floorY - 1f), new Vector3(leftX, floorY + 9f));
        Gizmos.DrawLine(new Vector3(rightX, floorY - 1f), new Vector3(rightX, floorY + 9f));
        Gizmos.DrawLine(new Vector3(leftX, floorY), new Vector3(rightX, floorY));
        Gizmos.color = new Color(0.9f, 0.5f, 1f, 0.9f);
        foreach (Transform t in new[] { gongSpot, gateBackSpot, gateOnSpot })
            if (t != null) Gizmos.DrawWireSphere(t.position + Vector3.up * 0.5f, 0.5f);
    }
}
