using UnityEngine;

public class ZRandomizer : MonoBehaviour
{
    [SerializeField] private float minZ = -2f;
    [SerializeField] private float maxZ = 2f;

    void Start()
    {
        // Apply a random Z offset when the object spawns
        Vector3 newPosition = transform.position;
        newPosition.z += Random.Range(minZ, maxZ);
        transform.position = newPosition;
    }
}
