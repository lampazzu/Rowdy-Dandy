using System.Collections.Generic;
using UnityEngine;

// "Something invisible keeps dragging me": a body Rowdy can't see (a faded-out corpse whose death clip switched its AI
// back on, a flyer with no corpse handling, anything with all its renderers hidden) touching him gets its collisions
// with him switched off, and is written to the log (Player.log / Editor.log: "Invisible push:") so the real culprit
// can be fixed at the source. Terrain, triggers, effectors (jellies, one-way floors) and anything visible are left
// alone. Created automatically.
public class InvisiblePushGuard : MonoBehaviour
{
    private static InvisiblePushGuard instance;
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[24];
    private readonly HashSet<Collider2D> handled = new HashSet<Collider2D>();
    private readonly List<Renderer> renderers = new List<Renderer>();
    private Collider2D[] rowdyColliders;
    private Rigidbody2D rowdyBody;
    private Transform rowdy;
    private float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("InvisiblePushGuard (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<InvisiblePushGuard>();
    }

    private void FixedUpdate()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.1f;
        Transform r = BoonRunner.Rowdy;
        if (r == null) return;
        if (r != rowdy)
        {
            rowdy = r;
            rowdyBody = r.GetComponent<Rigidbody2D>();
            rowdyColliders = r.GetComponentsInChildren<Collider2D>(true);
            handled.Clear();
        }
        if (rowdyBody == null) return;
        int n = rowdyBody.GetContacts(contacts);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = contacts[i].collider == null || IsRowdys(contacts[i].collider) ? contacts[i].otherCollider : contacts[i].collider;
            if (c == null || IsRowdys(c) || handled.Contains(c) || c.isTrigger || c.usedByEffector) continue;
            Rigidbody2D body = c.attachedRigidbody;
            EnemyHealth owner = c.GetComponentInParent<EnemyHealth>();
            bool moving = body != null && body.bodyType != RigidbodyType2D.Static;
            if (owner == null && !moving) continue; // terrain, gates, walls
            if (Visible(owner != null ? owner.transform : c.transform)) continue;

            handled.Add(c);
            foreach (Collider2D mine in rowdyColliders) if (mine != null) Physics2D.IgnoreCollision(c, mine, true);
            Debug.LogWarning("Invisible push: " + Path(c.transform) + " layer " + LayerMask.LayerToName(c.gameObject.layer)
                             + " body " + (body != null ? body.bodyType + " v" + body.linearVelocity.ToString("0.0") : "none")
                             + (owner != null ? " enemy dead=" + owner.enemydead : "") + " at " + c.bounds.center.ToString("0.0"));
        }
    }

    private bool IsRowdys(Collider2D c)
    {
        if (c == null || rowdyColliders == null) return false;
        foreach (Collider2D mine in rowdyColliders) if (mine == c) return true;
        return false;
    }

    private bool Visible(Transform t)
    {
        renderers.Clear();
        t.GetComponentsInChildren(false, renderers);
        foreach (Renderer rd in renderers)
        {
            if (!rd.enabled || rd is ParticleSystemRenderer) continue;
            if (rd is SpriteRenderer sr && (sr.sprite == null || sr.color.a < 0.1f)) continue;
            return true;
        }
        return false;
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        for (Transform u = t.parent; u != null; u = u.parent) p = u.name + "/" + p;
        return p;
    }
}
