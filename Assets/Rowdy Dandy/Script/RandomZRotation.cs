using UnityEngine;

public class RandomZRotation : MonoBehaviour
{
    private static int flipState = 1;
    private Transform parent;
    private float followDuration = 3f; // Time (in seconds) for how long to follow the parent
    private float followTimer = 0f;

    void Start()
    {
        parent = transform.parent;

        // Set a random Z rotation
        float randomZ = Random.Range(-28f, 10f);
        transform.rotation = Quaternion.Euler(0, 0, randomZ);

        // Alternate the X scale
        Vector3 scale = transform.localScale;
        flipState *= -1; // Alternate between 1 and -1
        scale.x = flipState;
        transform.localScale = scale;

        // Reset follow timer
        followTimer = followDuration;
    }

    void Update()
    {
        if (parent != null && followTimer > 0)
        {
            // Keep following the parent for the specified duration
            transform.position = parent.position;
            followTimer -= Time.deltaTime; // Reduce follow time each frame
        }
        else
        {
            // Optionally, stop following the parent after the timer runs out
            // transform.position = originalPosition; // Uncomment if you want to reset the position
        }
    }
}
