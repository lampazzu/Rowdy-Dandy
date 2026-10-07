using TMPro;
using UnityEngine;

// Pops a HUD text (scale punch + color flash) whenever its text changes, e.g. level or stat values.
// Your scripts keep setting .text as before; this just reacts to the change.
[RequireComponent(typeof(TMP_Text))]
public class HUDTextFeedback : MonoBehaviour
{
    [SerializeField] private float popScale = 0.3f;
    [SerializeField] private float popDuration = 0.3f;
    [SerializeField] private Color flashColor = new Color(1f, 0.79f, 0.24f, 1f);
    [SerializeField] private float flashDuration = 0.45f;

    private TMP_Text label;
    private string lastText;
    private Vector3 baseScale;
    private Color baseColor;
    private float timer;
    private float silentUntil;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
        baseScale = label.rectTransform.localScale;
        baseColor = label.color;
    }

    private void OnEnable()
    {
        // Don't pop for whatever was set while hidden (e.g. stats panel opened with C)
        lastText = label != null ? label.text : null;
        silentUntil = Time.unscaledTime + 0.5f;
    }

    private void OnDisable()
    {
        timer = 0f;
        if (label != null)
        {
            label.rectTransform.localScale = baseScale;
            label.color = baseColor;
        }
    }

    private void LateUpdate()
    {
        string current = label.text;
        if (current != lastText)
        {
            bool isStartupValue = lastText == null || Time.unscaledTime < silentUntil;
            lastText = current;
            if (!isStartupValue) timer = Mathf.Max(popDuration, flashDuration);
        }

        if (timer <= 0f) return;

        timer -= Time.unscaledDeltaTime;
        float elapsed = Mathf.Max(popDuration, flashDuration) - timer;

        float popT = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, popDuration));
        float scale = 1f + popScale * Mathf.Sin(popT * Mathf.PI) * (1f - popT * 0.5f);
        label.rectTransform.localScale = baseScale * scale;

        float flashT = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, flashDuration));
        label.color = Color.Lerp(flashColor, baseColor, flashT);

        if (timer <= 0f)
        {
            label.rectTransform.localScale = baseScale;
            label.color = baseColor;
        }
    }
}
