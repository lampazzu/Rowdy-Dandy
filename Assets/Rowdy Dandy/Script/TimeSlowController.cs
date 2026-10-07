using UnityEngine;

public class TimeSlowController : MonoBehaviour
{
    [Range(0f, 1f)]
    public float timeSlowValue = 1f; // 1 = Normal Speed, 0.5 = Half Speed, 0 = Frozen

    private void Update()
    {
        // The pause menu owns time while it's open (otherwise this would un-pause the game every frame)
        if (PauseMenu.IsPaused) return;

        Time.timeScale = timeSlowValue;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // Adjust physics update rate
    }
}
