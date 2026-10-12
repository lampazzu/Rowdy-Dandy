using UnityEngine;

// perf: the Physics2D "...All" queries without a new array per call. Same hits in the same order as
// Physics2D.RaycastAll / OverlapCircleAll / OverlapBoxAll (default layers, triggers as Physics2D.queriesHitTriggers),
// written into a shared buffer that grows if it ever fills up. Read the results right away: the next query reuses it.
public static class PhysicsQuery
{
    public static RaycastHit2D[] Hits { get; private set; } = new RaycastHit2D[64];
    public static Collider2D[] Colliders { get; private set; } = new Collider2D[128];

    private static ContactFilter2D Filter(int layerMask)
    {
        var f = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
        f.SetLayerMask(layerMask);
        return f;
    }

    // Physics2D.RaycastAll: nearest first
    public static int RaycastAll(Vector2 from, Vector2 direction, float distance, int layerMask = Physics2D.DefaultRaycastLayers)
    {
        ContactFilter2D f = Filter(layerMask);
        int n;
        while ((n = Physics2D.Raycast(from, direction, f, Hits, distance)) == Hits.Length) Hits = new RaycastHit2D[Hits.Length * 2];
        for (int i = 1; i < n; i++)
        {
            RaycastHit2D h = Hits[i];
            int j = i - 1;
            while (j >= 0 && Hits[j].distance > h.distance) { Hits[j + 1] = Hits[j]; j--; }
            Hits[j + 1] = h;
        }
        return n;
    }

    public static int OverlapCircleAll(Vector2 center, float radius, int layerMask = Physics2D.DefaultRaycastLayers)
    {
        ContactFilter2D f = Filter(layerMask);
        int n;
        while ((n = Physics2D.OverlapCircle(center, radius, f, Colliders)) == Colliders.Length) Colliders = new Collider2D[Colliders.Length * 2];
        return n;
    }

    public static int OverlapBoxAll(Vector2 center, Vector2 size, float angle, int layerMask = Physics2D.DefaultRaycastLayers)
    {
        ContactFilter2D f = Filter(layerMask);
        int n;
        while ((n = Physics2D.OverlapBox(center, size, angle, f, Colliders)) == Colliders.Length) Colliders = new Collider2D[Colliders.Length * 2];
        return n;
    }

    // A transform-moved object whose colliders have no Rigidbody2D: give it a STATIC one. Colliders without a body sit
    // on the world's shared static body in world space, so Box2D rebuilds their shapes on every move (dozens of EXP
    // gems bobbing = hundreds of rebuilds a frame). With their own static body a move just moves that body. Still
    // static: exactly the same contacts / triggers / queries as before (a kinematic body would add new trigger contacts).
    public static Rigidbody2D AddMoverBody(GameObject go)
    {
        if (go.GetComponentInParent<Rigidbody2D>() != null) return null;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        return rb;
    }

    // LayerMask.GetMask("Enemy") without the params array and string lookup every call
    private static int enemyMask = -1;
    public static int EnemyMask => enemyMask != -1 ? enemyMask : (enemyMask = LayerMask.GetMask("Enemy"));
}
