using UnityEngine;

// Frames-per-second readout in the bottom-left corner (Settings > Show FPS). Created automatically.
public class FpsCounter : MonoBehaviour
{
    private static FpsCounter instance;

    private PixelText label;
    private int frames;
    private float elapsed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("FpsCounter (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FpsCounter>();
    }

    private void Awake()
    {
        label = PixelText.Create(OverlayUI.Root, "-- FPS", 2, new Color(0.75f, 1f, 0.75f, 0.9f), 0f);
        OverlayUI.Place(label.Rect, new Vector2(0f, 0f), new Vector2(14f, 12f));
        label.gameObject.SetActive(GameSettings.ShowFps);
    }

    private void OnEnable() { GameSettings.Changed += OnSettingsChanged; }
    private void OnDisable() { GameSettings.Changed -= OnSettingsChanged; }

    private void OnSettingsChanged()
    {
        if (label != null) label.gameObject.SetActive(GameSettings.ShowFps);
    }

    private void Update()
    {
        if (!GameSettings.ShowFps) return;

        frames++;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < 0.5f) return;

        int fps = Mathf.RoundToInt(frames / elapsed);
        label.SetText(fps + " FPS");
        label.Color = fps >= 55 ? new Color(0.75f, 1f, 0.75f, 0.9f) : fps >= 30 ? new Color(1f, 0.9f, 0.5f, 0.9f) : new Color(1f, 0.5f, 0.5f, 0.9f);
        frames = 0;
        elapsed = 0f;
    }
}
