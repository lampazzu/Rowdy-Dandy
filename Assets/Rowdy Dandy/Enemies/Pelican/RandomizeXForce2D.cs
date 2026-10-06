using UnityEngine;

public class RandomizeXForce2D : MonoBehaviour
{
    public float minXForce = -30f;  // Minimum X force
    public float maxXForce = -10f;   // Maximum X force

    private void Start()
    {
        ConstantForce2D force = GetComponent<ConstantForce2D>();
        if (force != null)
        {
            force.force = new Vector2(Random.Range(minXForce, maxXForce), force.force.y);
        }
        else
        {
            Debug.LogWarning("No ConstantForce2D component found on " + gameObject.name);
        }
    }
}
