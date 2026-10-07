using UnityEngine;

// Gives each instance its own tiny Z offset so sprites sharing the same
// Sorting Layer + Order in Layer never tie (ties make them swap front/back
// every frame, which looks like a twitch).
// Note: this moves the whole object. Child sprites of the same object that
// share an Order in Layer still need a Sorting Group or distinct orders.
public class ZRandomizer : MonoBehaviour
{
    [Tooltip("Z distance between two instances. Keep tiny, it only needs to break ties.")]
    [SerializeField] private float step = 0.001f;

    [Tooltip("Number of distinct offsets before wrapping. Max offset = step * slots.")]
    [SerializeField] private int slots = 500;

    private static int nextSlot;

    private float baseZ;
    private bool hasBaseZ;

    void OnEnable()
    {
        // Remember the authored Z once, so pooled objects don't drift each time they re-enable
        if (!hasBaseZ)
        {
            baseZ = transform.position.z;
            hasBaseZ = true;
        }

        nextSlot = (nextSlot + 1) % Mathf.Max(1, slots);

        Vector3 newPosition = transform.position;
        newPosition.z = baseZ - nextSlot * step;
        transform.position = newPosition;
    }
}
