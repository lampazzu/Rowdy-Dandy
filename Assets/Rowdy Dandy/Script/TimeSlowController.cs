using UnityEngine;

public class TimeSlowController : MonoBehaviour
{
    [Range(0f, 1f)]
    public float timeSlowValue = 1f; // 1 = Normal Speed, 0.5 = Half Speed, 0 = Frozen

    // Short global hit-stops from code (Nick's cuts, Waterviva's counter kill...): this controller sets timeScale
    // every frame, so it has to be the one applying them.
    private static float hitStopUntil = -1f;
    private static float hitStopScale = 1f;

    public static void HitStop(float realSeconds, float scale = 0.05f)
    {
        if (PauseMenu.IsPaused) return;
        float until = Time.unscaledTime + realSeconds;
        if (until > hitStopUntil) hitStopUntil = until;
        hitStopScale = Mathf.Clamp01(scale);
        Time.timeScale = hitStopScale;
    }

    public static bool InHitStop => Time.unscaledTime < hitStopUntil;

    private void Update()
    {
        // The pause menu owns time while it's open (otherwise this would un-pause the game every frame)
        if (PauseMenu.IsPaused) return;

        Time.timeScale = InHitStop ? Mathf.Min(hitStopScale, timeSlowValue) : timeSlowValue;
        Time.fixedDeltaTime = 0.02f * Time.timeScale; // Adjust physics update rate
    }
}
