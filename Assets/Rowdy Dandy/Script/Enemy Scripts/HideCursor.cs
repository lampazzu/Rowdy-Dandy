using UnityEngine;

public class HideCursor : MonoBehaviour
{
    void Start()
    {
        // Hide the cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked; // Optional: Locks the cursor at the center
    }
}