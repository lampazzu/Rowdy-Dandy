using UnityEngine;

public class DontDestroyOnLoad : MonoBehaviour
{
    private void Awake()
    {
        // Check if an object of the same type already exists
        foreach (var obj in FindObjectsByType<DontDestroyOnLoad>(FindObjectsSortMode.None))
        {
            if (obj != this && obj.name == gameObject.name)
            {
                Destroy(gameObject);
                return;
            }
        }

        // DontDestroyOnLoad only works on root objects (Unity ignores it with a warning otherwise)
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
