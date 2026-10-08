using UnityEngine;
using UnityEngine.SceneManagement;

public class TimeSlowController : MonoBehaviour
{
    [Range(0f, 1f)]
    public float timeSlowValue = 1f; // 1 = Normal Speed, 0.5 = Half Speed, 0 = Frozen

    // Short global hit-stops from code (Nick's cuts, Waterviva's counter kill...): this controller sets timeScale
    // every frame, so it has to be the one applying them.
    private static float hitStopUntil = -1f;
    private static float hitStopScale = 1f;

    // Longer slow motion from code (Rowdy's death, grabbing a flying rat). Eases back to normal over its last 30%.
    private static float slowStart = -1f, slowUntil = -1f;
    private static float slowScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        hitStopUntil = slowUntil = -1f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // A death slow-mo must not carry over into the reloaded level
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        hitStopUntil = slowUntil = -1f;
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
    }

    public static void HitStop(float realSeconds, float scale = 0.05f)
    {
        if (PauseMenu.IsPaused) return;
        float until = Time.unscaledTime + realSeconds;
        if (until > hitStopUntil) hitStopUntil = until;
        hitStopScale = Mathf.Clamp01(scale);
        Time.timeScale = hitStopScale;
    }

    public static void SlowMotion(float realSeconds, float scale)
    {
        if (PauseMenu.IsPaused) return;
        slowStart = Time.unscaledTime;
        slowUntil = Time.unscaledTime + realSeconds;
        slowScale = Mathf.Clamp01(scale);
    }

    public static bool InHitStop => Time.unscaledTime < hitStopUntil;

    private static float SlowNow
    {
        get
        {
            float now = Time.unscaledTime;
            if (now >= slowUntil) return 1f;
            float length = Mathf.Max(0.01f, slowUntil - slowStart);
            float k = (slowUntil - now) / length; // 1 -> 0
            return k > 0.3f ? slowScale : Mathf.Lerp(1f, slowScale, k / 0.3f);
        }
    }

    private void Update()
    {
        // The pause menu owns time while it's open (otherwise this would un-pause the game every frame)
        if (PauseMenu.IsPaused) return;

        float scale = Mathf.Min(timeSlowValue, SlowNow);
        Time.timeScale = InHitStop ? Mathf.Min(hitStopScale, scale) : scale;
        Time.fixedDeltaTime = 0.02f * Mathf.Max(0.05f, Time.timeScale); // Adjust physics update rate
    }
}
