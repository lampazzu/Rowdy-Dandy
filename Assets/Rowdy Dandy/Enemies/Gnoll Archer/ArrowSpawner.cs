using UnityEngine;

public class ArrowSpawner : MonoBehaviour
{
    public GameObject arrowPrefab; // The prefab to spawn
    public Transform spawnPoint; // The point where the arrow will be spawned
    public Transform parentTransform; // The parent object to check the x scale from
    public bool isBloody; // Determines if the arrow should be bloody

    private void OnEnable()
    {
        if (isBloody)
        {
            // Auto-assign spawnPoint and parentTransform if isBloody is true
            spawnPoint = transform;
            parentTransform = transform.parent;
        }

        // Spawn the arrow when the script is enabled
        SpawnArrow();
    }

    private void SpawnArrow()
    {
        if (arrowPrefab != null && spawnPoint != null)
        {
            // Instantiate the arrow at the spawn point
            GameObject arrow = Instantiate(arrowPrefab, spawnPoint.position, spawnPoint.rotation);
            ConstantForce2D arrowForce = arrow.GetComponent<ConstantForce2D>();

            if (arrowForce == null)
            {
                Debug.LogError("Arrow Prefab does not have a ConstantForce2D component.");
                return;
            }

            // Check the x scale of the parent and adjust the arrow's x scale and force accordingly
            if (parentTransform != null)
            {
                float parentScaleX = parentTransform.localScale.x;
                Vector3 arrowScale = arrow.transform.localScale;
                Vector2 arrowForceValue = arrowForce.force;

                if (parentScaleX > 0)
                {
                    arrowScale.x = -Mathf.Abs(arrowScale.x); // Ensure the arrow is flipped
                    arrowForceValue.x = -Mathf.Abs(arrowForceValue.x); // Apply negative force for left direction
                }
                else
                {
                    arrowScale.x = Mathf.Abs(arrowScale.x); // Ensure the arrow is not flipped
                    arrowForceValue.x = Mathf.Abs(arrowForceValue.x); // Apply positive force for right direction
                }

                arrow.transform.localScale = arrowScale;
                arrowForce.force = arrowForceValue;
            }
            else
            {
                Debug.LogError("Parent Transform not assigned.");
            }
        }
        else
        {
            Debug.LogError("Arrow Prefab or Spawn Point not assigned.");
        }
    }
}
