using UnityEngine;

public class DontDestroyOnLoad : MonoBehaviour
{
    private void Awake()
    {
        // Check if an object of the same type already exists
        foreach (var obj in FindObjectsOfType<DontDestroyOnLoad>())
        {
            if (obj != this && obj.name == gameObject.name)
            {
                Destroy(gameObject);
                return;
            }
        }

        DontDestroyOnLoad(gameObject);
    }
}
