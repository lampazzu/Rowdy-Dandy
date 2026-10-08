using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// The orange "sun beam" freeform lights scattered under the scene's "Lights" object follow the night cycle:
// warm and as placed during the day, then a faint, cold moonlight while the moon is up.
// How far into the night we are is read from the moon sprite the NightCycleAnim clip raises
// (RDR_Moon_0 local Y: -2.6 = hidden, 1.26 = fully up). Attached automatically on scene load.
public class SunBeamMoonlight : MonoBehaviour
{
    [Tooltip("Beam colour at full night.")]
    [SerializeField] private Color moonColor = new Color(0.55f, 0.72f, 1f, 1f);
    [Tooltip("x the beam's placed intensity at full night (subtle).")]
    [SerializeField] private float moonIntensity = 0.45f;
    [SerializeField] private float moonHiddenY = -2.6f, moonUpY = 1.26f;

    private struct Beam { public Light2D light; public Color dayColor; public float dayIntensity; }
    private readonly List<Beam> beams = new List<Beam>();
    private Transform moon;
    private float lastNight = -1f;

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
        if (FindFirstObjectByType<SunBeamMoonlight>() != null) return;
        new GameObject("SunBeamMoonlight (auto)").AddComponent<SunBeamMoonlight>();
    }

    // The sun beams: freeform lights in that orange (1, ~0.5, ~0.26)
    private static bool IsSunBeam(Light2D l)
    {
        if (l.lightType != Light2D.LightType.Freeform) return false;
        Color c = l.color;
        return c.r > 0.95f && c.g > 0.4f && c.g < 0.6f && c.b > 0.15f && c.b < 0.35f;
    }

    private void Start()
    {
        foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.transform.parent == null || l.transform.parent.name != "Lights" || !IsSunBeam(l)) continue;
            beams.Add(new Beam { light = l, dayColor = l.color, dayIntensity = l.intensity });
        }
        GameObject moonObject = GameObject.Find("RDR_Moon_0");
        if (moonObject != null) moon = moonObject.transform;
        if (beams.Count == 0) enabled = false;
    }

    private void LateUpdate()
    {
        float night = moon != null ? Mathf.Clamp01(Mathf.InverseLerp(moonHiddenY, moonUpY, moon.localPosition.y)) : 0f;
        night = night * night * (3f - 2f * night); // smooth in / out
        if (Mathf.Abs(night - lastNight) < 0.002f) return;
        lastNight = night;

        foreach (Beam b in beams)
        {
            if (b.light == null) continue;
            b.light.color = Color.Lerp(b.dayColor, moonColor, night);
            b.light.intensity = b.dayIntensity * Mathf.Lerp(1f, moonIntensity, night);
        }
    }
}
