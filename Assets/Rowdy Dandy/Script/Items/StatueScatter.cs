using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// A few extra wolf statues (Resources/Enemies Prefab/Breakables/RDR_WolfStatues_A/B/C) at random spots on solid
// ground every time the level loads, so every run is a little different. Same spot rules as the ores: on top of
// Ground, room above, not near water / checkpoints / Pelich's arena / each other.
public static class StatueScatter
{
    public const int PerLevel = 6;
    private static readonly string[] Prefabs =
    {
        "Enemies Prefab/Breakables/RDR_WolfStatues_A",
        "Enemies Prefab/Breakables/RDR_WolfStatues_B",
        "Enemies Prefab/Breakables/RDR_WolfStatues_C",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        Scatter();
        SceneManager.sceneLoaded += (s, m) => Scatter();
    }

    private static void Scatter()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        bool any = false;
        Bounds level = default;
        foreach (CompositeCollider2D c in Object.FindObjectsByType<CompositeCollider2D>(FindObjectsSortMode.None))
        {
            if (c.gameObject.layer != groundLayer && !c.CompareTag("Ground")) continue;
            if (!any) { level = c.bounds; any = true; } else level.Encapsulate(c.bounds);
        }
        if (!any) return;

        float? pelichX = null;
        foreach (EnemyHealth e in Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            EnemyCatalog.Entry entry = EnemyCatalog.Identify(e);
            if (entry != null && entry.id == "pelich") { pelichX = e.transform.position.x; break; }
        }

        GameObject rowdy = GameObject.FindGameObjectWithTag("Player");
        var placed = new List<Vector2>();
        int attempts = 0;
        while (placed.Count < PerLevel && attempts++ < PerLevel * 60)
        {
            float x = Random.Range(level.min.x + 6f, level.max.x - 6f);
            if (!FindSpot(x, level, groundLayer, out Vector2 spot)) continue;
            if (RespawnTrigger.IsNear(spot, 3f)) continue;
            if (pelichX.HasValue && Mathf.Abs(spot.x - pelichX.Value) < 18f) continue;
            if (spot.x < 12f) continue; // keep the beach start clear
            if (rowdy != null && Vector2.Distance(rowdy.transform.position, spot) < 10f) continue;
            bool crowded = false;
            foreach (Vector2 p in placed) if (Vector2.Distance(p, spot) < 14f) { crowded = true; break; }
            if (crowded) continue;

            if (Place(spot) != null) placed.Add(spot);
        }
    }

    // Dev tools: one right here (on the ground below the point)
    public static void SpawnNear(Vector3 around)
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        Place(SolidGround.Ray(around + Vector3.up * 0.5f, Vector2.down, 8f, out RaycastHit2D hit) ? hit.point : (Vector2)around);
    }

    private static bool FindSpot(float x, Bounds level, int groundLayer, out Vector2 spot)
    {
        spot = default;
        var surfaces = new List<RaycastHit2D>();
        foreach (RaycastHit2D h in Physics2D.RaycastAll(new Vector2(x, level.max.y + 2f), Vector2.down, level.size.y + 4f))
        {
            if (h.collider == null || h.collider.isTrigger || h.normal.y < 0.9f) continue;
            if (h.collider.attachedRigidbody != null && h.collider.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
            surfaces.Add(h);
        }
        surfaces.RemoveAll(s => s.collider.CompareTag("Water") || s.collider.gameObject.layer == 6);
        if (surfaces.Count == 0) return false;
        RaycastHit2D pick = surfaces[Random.Range(0, surfaces.Count)];
        if (pick.collider.CompareTag("Water") || pick.collider.gameObject.layer == 6) return false;
        if (!SolidGround.IsGround(pick.collider)) return false;
        // flat for a statue's width, room above
        foreach (float dx in new[] { -0.5f, 0.5f })
        {
            if (!SolidGround.Ray(pick.point + new Vector2(dx, 0.3f), Vector2.down, 0.6f, out RaycastHit2D side) || Mathf.Abs(side.point.y - pick.point.y) > 0.15f) return false;
        }
        if (SolidGround.Blocked(pick.point + new Vector2(0f, 1.1f), new Vector2(1.2f, 1.8f))) return false;
        spot = pick.point;
        return true;
    }

    private static GameObject Place(Vector2 spot)
    {
        GameObject prefab = Resources.Load<GameObject>(Prefabs[Random.Range(0, Prefabs.Length)]);
        if (prefab == null) return null;
        GameObject statue = Object.Instantiate(prefab, new Vector3(spot.x, spot.y, 0f), Quaternion.identity);
        statue.name = prefab.name + " (random)";
        if (Random.value < 0.5f)
        {
            Vector3 s = statue.transform.localScale;
            statue.transform.localScale = new Vector3(-s.x, s.y, s.z);
        }

        // stand it on the ground: lowest point of its solid collider (or sprite) onto the spot
        float bottom = float.MaxValue;
        foreach (Collider2D c in statue.GetComponentsInChildren<Collider2D>())
            if (!c.isTrigger) bottom = Mathf.Min(bottom, c.bounds.min.y);
        if (bottom == float.MaxValue)
            foreach (SpriteRenderer r in statue.GetComponentsInChildren<SpriteRenderer>())
                bottom = Mathf.Min(bottom, r.bounds.min.y);
        if (bottom != float.MaxValue) statue.transform.position += Vector3.up * (spot.y - bottom);
        return statue;
    }
}
