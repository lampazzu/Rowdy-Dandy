using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// perf: Unity sends OnTrigger / OnCollision messages for every contact to BOTH colliders' objects, every physics step,
// even when nothing on that object listens. In a big fight ~40% of them went to objects with no handler script at all
// (the tilemaps, terrain correctors, patrol point boxes, water triggers...). When a level loads, every collider whose
// object (and body object) has no OnTrigger*/OnCollision* method gets callbackLayers = 0: Unity then skips ITS side of
// those messages. The other side still gets them exactly as before (tested: Rowdy's ground collisions keep coming),
// and the contacts / physics / queries themselves don't change at all.
// Only colliders still on the default "Everything" are touched; objects spawned later are left as they are.
public static class CallbackTrimmer
{
    private static readonly string[] Handlers = { "OnTriggerEnter2D", "OnTriggerStay2D", "OnTriggerExit2D", "OnCollisionEnter2D", "OnCollisionStay2D", "OnCollisionExit2D" };
    private static readonly Dictionary<System.Type, bool> handlerTypes = new Dictionary<System.Type, bool>();
    private static readonly List<MonoBehaviour> behaviours = new List<MonoBehaviour>();

    public static int Trimmed { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        int n = 0;
        foreach (Collider2D c in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c == null || c.gameObject.scene != scene || c.callbackLayers.value != -1) continue;
            if (HasHandler(c.gameObject)) continue;
            Rigidbody2D body = c.attachedRigidbody;
            if (body != null && body.gameObject != c.gameObject && HasHandler(body.gameObject)) continue;
            c.callbackLayers = 0;
            n++;
        }
        Trimmed = n;
    }

    // Any MonoBehaviour on the object (ours, MoreMountains, TMP...) with one of the six physics messages
    private static bool HasHandler(GameObject go)
    {
        go.GetComponents(behaviours);
        try
        {
            foreach (MonoBehaviour m in behaviours)
            {
                if (m == null) return true; // a missing script: don't guess, leave it alone
                System.Type t = m.GetType();
                if (!handlerTypes.TryGetValue(t, out bool has))
                {
                    has = false;
                    for (System.Type k = t; k != null && k != typeof(MonoBehaviour) && !has; k = k.BaseType)
                        foreach (string name in Handlers)
                            if (k.GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                                  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly) != null) { has = true; break; }
                    handlerTypes[t] = has;
                }
                if (has) return true;
            }
            return false;
        }
        finally { behaviours.Clear(); }
    }
}
