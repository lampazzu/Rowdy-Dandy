using UnityEngine;
using UnityEngine.UI;

// Juice for a filled HUD bar (health, EXP, durability). Put it on the fill Image.
// Your scripts keep setting fillAmount like before (HealthBar, PlayerStats, WeaponManager);
// this reads the new value and animates toward it:
//  - Losing: the fill snaps down, a "chunk" ghost lingers then drains, the bar flashes and punches (optional panel shake)
//  - Gaining: a bright preview ghost jumps ahead and the fill smoothly catches up
//  - Low value: the fill pulses
//  - Big drop on an EXP bar = level up: gold flash + big punch, then refills from empty
// Uses unscaled time so it still animates during hit stop / slow motion.
[RequireComponent(typeof(Image))]
public class HUDBarFeedback : MonoBehaviour
{
    [Header("Fill Animation")]
    [SerializeField] private float fillSpeed = 1.5f;          // fill units per second when gaining
    [SerializeField] private float ghostDelay = 0.35f;        // how long the lost chunk lingers
    [SerializeField] private float ghostDrainSpeed = 1.2f;
    [SerializeField] private Color lossGhostColor = new Color(1f, 0.3f, 0.45f, 1f);
    [SerializeField] private Color gainGhostColor = new Color(1f, 1f, 1f, 0.55f);

    [Header("Flash & Punch")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private float punchScale = 0.08f;
    [SerializeField] private float punchDuration = 0.2f;
    [Tooltip("What gets punched. Leave empty to punch the bar's parent (the whole bar).")]
    [SerializeField] private RectTransform punchTarget;

    [Header("Shake On Loss")]
    [SerializeField] private UIShaker shakeOnLoss;
    [SerializeField] private float shakeStrength = 1f;

    [Header("Low Warning")]
    [Range(0f, 1f)] [SerializeField] private float lowThreshold = 0f;   // 0 = off
    [SerializeField] private Color lowColor = new Color(1f, 0.2f, 0.3f, 1f);
    [SerializeField] private float lowPulseSpeed = 6f;

    [Header("Level Up (EXP bars)")]
    [SerializeField] private bool treatBigDropAsLevelUp = false;
    [SerializeField] private Color levelUpFlashColor = new Color(1f, 0.79f, 0.24f, 1f);

    private Image fill;
    private Image ghost;
    private Color baseColor;
    private Vector3 punchBaseScale = Vector3.one;

    private float target;
    private float display;
    private float ghostValue;
    private float lastWritten = -1f;
    private float silentUntil;     // game scripts set their first values right after enabling; don't react to those
    private float ghostHoldTimer;
    private bool ghostIsLoss;

    private float flashTimer;
    private Color currentFlashColor;
    private float punchTimer;
    private float currentPunch;

    private void Awake()
    {
        fill = GetComponent<Image>();
        baseColor = fill.color;
        if (punchTarget == null && transform.parent != null) punchTarget = transform.parent as RectTransform;
        if (punchTarget != null) punchBaseScale = punchTarget.localScale;
        CreateGhost();
    }

    private void OnEnable()
    {
        // Re-sync silently (e.g. durability bar shown again after a weapon switch)
        target = display = ghostValue = fill != null ? fill.fillAmount : 0f;
        lastWritten = display;
        silentUntil = Time.unscaledTime + 0.5f;
        if (ghost != null) ghost.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (ghost != null) ghost.gameObject.SetActive(false);
        if (fill != null) fill.color = baseColor;
        if (punchTarget != null) punchTarget.localScale = punchBaseScale;
    }

    private void OnDestroy()
    {
        if (ghost != null) Destroy(ghost.gameObject);
    }

    // A copy of the fill drawn right behind it, used for the lingering chunk / gain preview
    private void CreateGhost()
    {
        RectTransform fillRect = fill.rectTransform;
        var ghostObject = new GameObject(name + " Ghost", typeof(RectTransform), typeof(Image));
        ghostObject.layer = gameObject.layer;

        RectTransform ghostRect = ghostObject.GetComponent<RectTransform>();
        ghostRect.SetParent(fillRect.parent, false);
        ghostRect.SetSiblingIndex(fillRect.GetSiblingIndex()); // directly behind the fill
        ghostRect.anchorMin = fillRect.anchorMin;
        ghostRect.anchorMax = fillRect.anchorMax;
        ghostRect.pivot = fillRect.pivot;
        ghostRect.anchoredPosition = fillRect.anchoredPosition;
        ghostRect.sizeDelta = fillRect.sizeDelta;
        ghostRect.localScale = fillRect.localScale;

        ghost = ghostObject.GetComponent<Image>();
        ghost.sprite = fill.sprite;
        ghost.type = Image.Type.Filled;
        ghost.fillMethod = fill.fillMethod;
        ghost.fillOrigin = fill.fillOrigin;
        ghost.preserveAspect = fill.preserveAspect;
        ghost.raycastTarget = false;
        ghost.fillAmount = fill.fillAmount;
        ghost.color = new Color(0f, 0f, 0f, 0f);
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;

        // Did a game script write a new value since our last write?
        float current = fill.fillAmount;
        if (!Mathf.Approximately(current, lastWritten))
        {
            if (Time.unscaledTime < silentUntil)
            {
                target = display = ghostValue = Mathf.Clamp01(current);
            }
            else
            {
                OnTargetChanged(Mathf.Clamp01(current));
            }
        }

        // Fill: snaps down on loss, eases up on gain
        display = display < target ? Mathf.MoveTowards(display, target, fillSpeed * dt) : target;

        // Ghost: holds, then drains toward the fill
        if (ghostHoldTimer > 0f)
        {
            ghostHoldTimer -= dt;
        }
        else if (ghostIsLoss)
        {
            ghostValue = Mathf.MoveTowards(ghostValue, display, ghostDrainSpeed * dt);
        }
        else
        {
            ghostValue = Mathf.Max(display, target);
        }

        fill.fillAmount = display;
        lastWritten = display;

        if (ghost != null)
        {
            ghost.fillAmount = ghostValue;
            bool showGhost = ghostValue > display + 0.001f;
            Color ghostColor = ghostIsLoss ? lossGhostColor : gainGhostColor;
            ghost.color = showGhost ? ghostColor : new Color(ghostColor.r, ghostColor.g, ghostColor.b, 0f);
        }

        UpdateColor(dt);
        UpdatePunch(dt);
    }

    private void OnTargetChanged(float newTarget)
    {
        float previous = target;
        target = newTarget;
        float delta = newTarget - previous;

        if (treatBigDropAsLevelUp && delta < -0.3f)
        {
            // Wrapped around: celebrate, then fill up from empty
            display = 0f;
            ghostValue = newTarget;
            ghostIsLoss = false;
            ghostHoldTimer = 0f;
            Flash(levelUpFlashColor, flashDuration * 3f);
            Punch(punchScale * 3f);
            return;
        }

        if (delta < 0f)
        {
            // Keep the lost chunk visible for a moment
            ghostValue = Mathf.Max(ghostValue, display);
            ghostIsLoss = true;
            ghostHoldTimer = ghostDelay;
            display = newTarget;
            Flash(flashColor, flashDuration);
            Punch(punchScale);
            if (shakeOnLoss != null) shakeOnLoss.Shake(shakeStrength * Mathf.Clamp(-delta * 5f, 0.3f, 1.5f));
        }
        else if (delta > 0f)
        {
            ghostIsLoss = false;
            ghostHoldTimer = 0f;
            ghostValue = newTarget;
            Flash(flashColor, flashDuration * 0.6f);
            Punch(punchScale * 0.5f);
        }
    }

    private void Flash(Color color, float duration)
    {
        currentFlashColor = color;
        flashTimer = Mathf.Max(flashTimer, duration);
    }

    private void Punch(float amount)
    {
        currentPunch = Mathf.Max(currentPunch * (punchTimer / Mathf.Max(0.0001f, punchDuration)), amount);
        punchTimer = punchDuration;
    }

    private void UpdateColor(float dt)
    {
        Color color = baseColor;

        if (lowThreshold > 0f && display <= lowThreshold && display > 0f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * lowPulseSpeed);
            color = Color.Lerp(baseColor, lowColor, pulse * 0.7f);
        }

        if (flashTimer > 0f)
        {
            flashTimer -= dt;
            color = Color.Lerp(color, currentFlashColor, Mathf.Clamp01(flashTimer / Mathf.Max(0.0001f, flashDuration)));
        }

        fill.color = color;
    }

    private void UpdatePunch(float dt)
    {
        if (punchTarget == null) return;

        if (punchTimer > 0f)
        {
            punchTimer -= dt;
            float t = Mathf.Clamp01(punchTimer / Mathf.Max(0.0001f, punchDuration));
            // quick overshoot that settles back to 1
            float scale = 1f + currentPunch * t * t;
            punchTarget.localScale = punchBaseScale * scale;
        }
        else
        {
            punchTarget.localScale = punchBaseScale;
            currentPunch = 0f;
        }
    }
}
