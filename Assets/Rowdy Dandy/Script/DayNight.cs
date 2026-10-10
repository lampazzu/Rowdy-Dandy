using UnityEngine;
using UnityEngine.SceneManagement;

// Drives the scene's "Night Cycle" animator (NightCycleAnim, 49.27 s clip at state speed 0.14):
//  - day lasts longer than night (the animator speed changes by phase: dayMinutes / nightMinutes, dawn + dusk count as day)
//  - the time of day survives deaths / reloads within a play session; resting at a checkpoint resets it to morning
//  - IsNight / NightAmount for the rest of the game (WaveEnemySpawner: fewer enemies by day, elites at night)
// Clip timeline (clip seconds): 0-5.4 full day, 5.4-12.5 dusk, 12.5-14.7 moon rises, 14.7-33.4 moon up,
// 34.3-38.3 dawn, 38.3-49.27 morning -> day. Attached automatically on scene load.
public class DayNight : MonoBehaviour
{
    public static float DayMinutes = 3.5f;   // real minutes from dawn to the end of dusk
    public static float NightMinutes = 2f;   // real minutes of night

    private const float ClipLength = 49.266666f;
    private const float StateSpeed = 0.14f;
    private const float NightStart = 12.5f, NightFull = 14.7f, NightFullEnd = 33.4f, NightEnd = 34.3f;
    public const float MorningTime = 40.4f; // just after sunrise

    private static float clipTime = -1f;     // -1 = fresh session: keep the scene's own start (clip time 0)
    private static bool restPending;

    private Animator animator;

    // 0 = day, 1 = deep night (smooth ramps at dusk / dawn)
    public static float NightAmount { get; private set; }
    public static bool IsNight => NightAmount > 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { clipTime = -1f; restPending = false; NightAmount = 0f; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        Attach();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

    private static void Attach()
    {
        if (FindFirstObjectByType<DayNight>() != null) return;
        new GameObject("DayNight (auto)").AddComponent<DayNight>();
    }

    // Checkpoint rest: the next load starts in the morning
    public static void ResetToMorningOnNextLoad() => restPending = true;

    // Right now, no reload (Come To The Light)
    private static DayNight current;
    public static void ResetToMorningNow() { if (current != null && current.animator != null) current.JumpTo(MorningTime); }

    private void Start()
    {
        current = this;
        foreach (Animator a in FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            if (a.runtimeAnimatorController != null && a.runtimeAnimatorController.name == "Night Cycle") { animator = a; break; }
        }
        if (animator == null) { enabled = false; return; }

        if (restPending) { clipTime = MorningTime; restPending = false; }
        if (clipTime >= 0f) JumpTo(clipTime);
    }

    // Jumping the clip to a time: applied (and checked) for up to 2 real seconds, since the animator only reports the
    // new time after it has evaluated once - and the first load of a session can stall (sky drop, tutorial pause),
    // which used to run out the old 5 tries and leave a rest at night
    private float jumpTo = -1f, jumpUntil = -1f;

    private void JumpTo(float t)
    {
        jumpTo = t;
        jumpUntil = Time.unscaledTime + 2f;
        clipTime = t;
        NightAmount = t >= NightStart && t < 38.3f ? 1f : 0f;
        animator.Play("NightCycleAnim", 0, t / ClipLength);
        animator.Update(0f);
    }

    // The Sun God's COME TO THE LIGHT: the night never comes (dusk skips straight to the morning)
    public static bool NightBanned => Boons.Has("cometothelight");

    private void Update()
    {
        if (animator == null) return;
        if (jumpUntil > 0f)
        {
            float now = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f) * ClipLength;
            if (Mathf.Abs(now - jumpTo) < 1f) jumpUntil = -1f;
            else if (Time.unscaledTime < jumpUntil) { animator.Play("NightCycleAnim", 0, jumpTo / ClipLength); animator.Update(0f); return; }
            else jumpUntil = -1f;
        }
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        float t = Mathf.Repeat(state.normalizedTime, 1f) * ClipLength;
        if (NightBanned && t >= 5.4f && t < 38.3f) { JumpTo(38.3f); return; }
        clipTime = t;

        bool night = t >= NightStart && t < NightEnd;
        float clipSeconds = night ? NightEnd - NightStart : ClipLength - (NightEnd - NightStart);
        float realSeconds = Mathf.Max(10f, (night ? NightMinutes : DayMinutes) * 60f);
        animator.speed = clipSeconds / StateSpeed / realSeconds;

        if (t < NightStart || t >= 38.3f) NightAmount = 0f;
        else if (t < NightFull) NightAmount = Mathf.SmoothStep(0f, 1f, (t - NightStart) / (NightFull - NightStart));
        else if (t < NightFullEnd) NightAmount = 1f;
        else NightAmount = Mathf.SmoothStep(1f, 0f, (t - NightFullEnd) / (38.3f - NightFullEnd));

        if (NightAmount > 0.9f) Tutorials.Show(Tutorials.Topic.Night); // first nightfall
    }

    // The night was too dark to read: the clip's Global Light2D colour gets lifted toward a cool moonlight at night
    // (after the animator wrote it this frame, so it never builds up). The blue vignette lives in Global Volume Fake Profile.
    public const float NightLift = 0.3f;
    private static readonly Color Moonlight = new Color(0.62f, 0.66f, 0.85f);
    private UnityEngine.Rendering.Universal.Light2D globalLight;
    private bool lightSearched;

    private void LateUpdate()
    {
        if (animator == null || NightAmount <= 0f || jumpUntil > 0f) return;
        if (!lightSearched)
        {
            lightSearched = true;
            foreach (var l in FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None))
                if (l.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global && l.name == "Global Light2D") { globalLight = l; break; }
        }
        if (globalLight == null) return;
        Color c = globalLight.color;
        Color lifted = new Color(Mathf.Max(c.r, Moonlight.r), Mathf.Max(c.g, Moonlight.g), Mathf.Max(c.b, Moonlight.b), c.a);
        globalLight.color = Color.Lerp(c, lifted, NightLift * NightAmount);
    }
}
