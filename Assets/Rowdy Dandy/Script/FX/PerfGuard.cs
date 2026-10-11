using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// PERFORMANCE
//  - Every Animator in the project was "Always Animate": ~270 pure decoration animators (vegetation, fireflies, palm
//    trees, the lake) kept animating across the whole level. At load, decoration animators (nothing on them but
//    renderers / lights / particles - no colliders, no gameplay scripts) switch to Cull Completely: they stop while
//    off screen and pick up again when seen. Anything with gameplay on it (plants, jellies, enemies, checkpoints,
//    the day/night clock) is left exactly as it was.
//  - F3: a small readout (fps, frame time worst of the last second, enemies, animators running, lights in view,
//    FX pool) to see what's going on when the game gets heavy.
// Sleeping far-away enemies pause their Animator too (EnemyMovement.UpdateSleep).
public class PerfGuard : MonoBehaviour
{
    private static PerfGuard instance;
    public static int DecorCulled { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("PerfGuard (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PerfGuard>();
        SceneManager.sceneLoaded += (s, m) => CullDecor();
        CullDecor();
    }

    // ---------------------------------------------------------------- decoration animators
    private static readonly List<Component> comps = new List<Component>();

    private static void CullDecor()
    {
        int n = 0;
        foreach (Animator a in FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (a == null || a.cullingMode != AnimatorCullingMode.AlwaysAnimate) continue;
            if (a.transform is RectTransform || a.GetComponentInParent<Canvas>(true) != null) continue; // UI
            if (a.GetComponentInParent<EnemyHealth>(true) != null || a.GetComponentInParent<Rigidbody2D>(true) != null) continue;
            if (!IsDecor(a.transform)) continue;
            if (a.GetComponentInChildren<Renderer>(true) == null) continue; // nothing to see = would never animate
            a.cullingMode = AnimatorCullingMode.CullCompletely;
            n++;
        }
        DecorCulled = n;
    }

    // only renderers, lights, particles and harmless visual helpers in this object and below
    private static bool IsDecor(Transform root)
    {
        comps.Clear();
        root.GetComponentsInChildren(true, comps);
        foreach (Component c in comps)
        {
            if (c == null) continue;
            if (c is Transform || c is Renderer || c is Animator || c is ParticleSystem) continue;
            string type = c.GetType().Name;
            if (type == "Light2D" || type == "SortingGroup" || type == "FireflyHover" || type == "SpriteDepthEffect" || type == "AudioSource") continue;
            return false; // colliders, rigidbodies, gameplay scripts: leave it alone
        }
        return true;
    }

    // ---------------------------------------------------------------- F3 readout
    private bool show;
    private float worst, worstShown, fps, nextSample;
    private int frames, enemies, animators, lights;
    private GUIStyle style;

    private void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f3Key.wasPressedThisFrame) show = !show;
        if (!show) return;
        float dt = Time.unscaledDeltaTime;
        worst = Mathf.Max(worst, dt);
        frames++;
        if (Time.unscaledTime < nextSample) return;
        fps = frames / Mathf.Max(0.001f, Time.unscaledTime - (nextSample - 1f));
        worstShown = worst;
        worst = 0f;
        frames = 0;
        nextSample = Time.unscaledTime + 1f;
        Sample();
    }

    private void Sample()
    {
        enemies = 0;
        foreach (EnemyHealth e in EnemyHealth.All) if (e != null && e.gameObject.activeInHierarchy && !e.enemydead && !e.IsObject) enemies++;
        animators = 0;
        foreach (Animator a in FindObjectsByType<Animator>(FindObjectsSortMode.None))
            if (a.isActiveAndEnabled && (a.cullingMode == AnimatorCullingMode.AlwaysAnimate || a.GetComponentInChildren<Renderer>() is Renderer r && r.isVisible)) animators++;
        lights = 0;
        Camera cam = Camera.main;
        if (cam != null)
        {
            float h = cam.orthographicSize, w = h * cam.aspect;
            Rect view = new Rect(cam.transform.position.x - w - 6f, cam.transform.position.y - h - 6f, w * 2f + 12f, h * 2f + 12f);
            foreach (UnityEngine.Rendering.Universal.Light2D l in FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None))
                if (l.isActiveAndEnabled && (l.lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global || view.Contains(l.transform.position))) lights++;
        }
    }

    private void OnGUI()
    {
        if (!show) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
        string col = fps >= 55f ? "#9f9" : fps >= 35f ? "#ff9" : "#f88";
        string text = "<color=" + col + ">" + fps.ToString("0") + " FPS</color>   worst frame " + (worstShown * 1000f).ToString("0") + " ms\n"
                      + "enemies " + enemies + "   animators running " + animators + " (decor culled " + DecorCulled + ")\n"
                      + "lights near view " + lights + "   timescale " + Time.timeScale.ToString("0.00");
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(8, 8, 470, 70), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(16, 12, 460, 70), text, style);
    }
}
