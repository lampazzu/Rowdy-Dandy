using UnityEngine;

// Shakes a UI panel (e.g. the player HUD when Rowdy gets hit). Call Shake(strength).
// Uses unscaled time so it still works during hit stop / slow motion.
[RequireComponent(typeof(RectTransform))]
public class UIShaker : MonoBehaviour
{
    [SerializeField] private float maxOffset = 10f;     // pixels at full strength
    [SerializeField] private float maxRotation = 1.5f;  // degrees at full strength
    [SerializeField] private float recoverySpeed = 2.5f;
    [SerializeField] private float frequency = 30f;

    private RectTransform rect;
    private Vector2 basePosition;
    private Quaternion baseRotation;
    private float trauma;
    private float seed;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        basePosition = rect.anchoredPosition;
        baseRotation = rect.localRotation;
        seed = Random.value * 100f;
    }

    private void OnDisable()
    {
        trauma = 0f;
        if (rect != null)
        {
            rect.anchoredPosition = basePosition;
            rect.localRotation = baseRotation;
        }
    }

    public void Shake(float strength)
    {
        trauma = Mathf.Clamp01(trauma + strength * 0.5f * GameSettings.ScreenShake);
    }

    private void LateUpdate()
    {
        if (trauma <= 0f) return;

        trauma = Mathf.Max(0f, trauma - recoverySpeed * Time.unscaledDeltaTime);
        float amount = trauma * trauma; // feels punchier than linear
        float t = Time.unscaledTime * frequency;

        float x = (Mathf.PerlinNoise(seed, t) * 2f - 1f) * maxOffset * amount;
        float y = (Mathf.PerlinNoise(seed + 1f, t) * 2f - 1f) * maxOffset * amount;
        float angle = (Mathf.PerlinNoise(seed + 2f, t) * 2f - 1f) * maxRotation * amount;

        rect.anchoredPosition = basePosition + new Vector2(Mathf.Round(x), Mathf.Round(y)); // whole pixels keep the pixel art crisp
        rect.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);

        if (trauma <= 0f)
        {
            rect.anchoredPosition = basePosition;
            rect.localRotation = baseRotation;
        }
    }
}
