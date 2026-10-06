using UnityEngine;

public class TimelineEnabler : MonoBehaviour
{
    private bool shouldDeactivate = true;

    void OnEnable()
    {
        shouldDeactivate = true; // Reset the flag when the GameObject is enabled
    }

    void OnDisable()
    {
        if (shouldDeactivate)
        {
            // Deactivate the GameObject when it's disabled
            gameObject.SetActive(false);
        }
    }

    public void DeactivateObject()
    {
        // Set the flag to false when the method is called
        shouldDeactivate = false;
    }
}