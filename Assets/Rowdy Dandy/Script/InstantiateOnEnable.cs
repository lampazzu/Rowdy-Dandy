using UnityEngine;

public class InstantiateOnEnable : MonoBehaviour
{
    [Header("Prefab Settings")]
    [SerializeField] private GameObject prefabToInstantiate; // The prefab to instantiate
    [SerializeField] private Vector3 instantiatePosition = Vector3.zero; // Position to instantiate the prefab
    [SerializeField] private Quaternion instantiateRotation = Quaternion.identity; // Rotation to instantiate the prefab

    // This method is called when the object is enabled
    private void OnEnable()
    {
        if (prefabToInstantiate != null)
        {
            // Instantiate the prefab at the specified position and rotation
            Instantiate(prefabToInstantiate, instantiatePosition, instantiateRotation);
        }
        else
        {
            Debug.LogWarning("Prefab is not assigned!", this);
        }
    }
}