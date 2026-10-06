using UnityEngine;
using TMPro;

public class FloatingDamageText : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private TMP_Text damageText;

    [Header("Damage Thresholds")]
    [SerializeField] private float ultraDamageThreshold = 200f;
    [SerializeField] private float extremeDamageThreshold = 100f;
    [SerializeField] private float highDamageThreshold = 50f;
    [SerializeField] private float lowDamageThreshold = 15f;

    [Header("Color Options")]
    [SerializeField] private Color ultraDamageColor = new Color(1f, 0.1f, 0.1f, 1f);     // Fiery Red / Crimson
    [SerializeField] private Color extremeDamageColor = new Color(1f, 0.5f, 0f, 1f);    // Deep Orange
    [SerializeField] private Color highDamageColor = new Color(1f, 0.85f, 0f, 1f);      // Golden Yellow
    [SerializeField] private Color mediumDamageColor = new Color(0.7f, 0.2f, 1f, 1f);    // Electric Purple
    [SerializeField] private Color lowDamageColor = new Color(0.85f, 0.85f, 0.85f, 1f);  // Soft White / Grey
    [SerializeField] private Color criticalColor = new Color(1f, 0f, 0.35f, 1f);         // Vibrant Neon Pink / Red
    [SerializeField] private Color counterColor = new Color(0f, 0.9f, 1f, 1f);           // Bright Cyan / Blue

    [Header("Juicy Pop & Scale")]
    [SerializeField] private float baseScale = 1.0f;
    [SerializeField] private float ultraDamageScaleMultiplier = 1.8f;
    [SerializeField] private float extremeDamageScaleMultiplier = 1.6f;
    [SerializeField] private float highDamageScaleMultiplier = 1.4f;
    [SerializeField] private float critScaleMultiplier = 1.7f;
    [SerializeField] private float popMultiplier = 1.8f;      // How big it swells on frame 1
    [SerializeField] private float popSpeed = 12f;             // Speed of scale spring back

    [Header("Movement & Physics")]
    [SerializeField] private float floatSpeed = 2.5f;
    [SerializeField] private float horizontalArcSpread = 1.5f; // Random horizontal drift
    [SerializeField] private float gravity = 1.2f;
    [SerializeField] private float fadeDuration = 0.75f;
    [SerializeField] private Vector3 randomOffset = new Vector3(0.2f, 0.2f, 0f);

    private Vector3 initialVelocity;
    private Vector3 targetScale;
    private Color textColor;
    private float timer = 0f;

    // Standard Setup for Numeric Damage
    public void Setup(float damageAmount)
    {
        Setup(damageAmount, false);
    }

    // Overloaded Setup for Numeric Damage + Critical Hits
    public void Setup(float damageAmount, bool isCrit)
    {
        if (isCrit)
        {
            SetupCritical(damageAmount);
            return;
        }

        if (damageText == null) damageText = GetComponent<TMP_Text>();

        string formattedDamage = damageAmount % 1 == 0 ? damageAmount.ToString("F0") : damageAmount.ToString("F1");
        damageText.text = formattedDamage;

        float calculatedBaseScale = baseScale;

        if (damageAmount >= ultraDamageThreshold)
        {
            textColor = ultraDamageColor;
            calculatedBaseScale *= ultraDamageScaleMultiplier;
        }
        else if (damageAmount >= extremeDamageThreshold)
        {
            textColor = extremeDamageColor;
            calculatedBaseScale *= extremeDamageScaleMultiplier;
        }
        else if (damageAmount >= highDamageThreshold)
        {
            textColor = highDamageColor;
            calculatedBaseScale *= highDamageScaleMultiplier;
        }
        else if (damageAmount <= lowDamageThreshold)
        {
            textColor = lowDamageColor;
            calculatedBaseScale *= 0.85f;
        }
        else
        {
            textColor = mediumDamageColor;
        }

        ApplySetup(calculatedBaseScale);
    }

    // Dedicated Setup for Critical Hits (Works like SetupCustomText)
    public void SetupCritical(float damageAmount)
    {
        if (damageText == null) damageText = GetComponent<TMP_Text>();

        string formattedDamage = damageAmount % 1 == 0 ? damageAmount.ToString("F0") : damageAmount.ToString("F1");

        // Displays "CRITICAL!" followed by damage
        damageText.text = $"CRITICAL!\n{formattedDamage}";
        textColor = criticalColor;

        ApplySetup(baseScale * critScaleMultiplier);
    }

    // Overloaded Setup for Custom Words (e.g. "COUNTER!", "PARRY!")
    public void SetupCustomText(string text, Color? overrideColor = null, float scaleMultiplier = 1.5f)
    {
        if (damageText == null) damageText = GetComponent<TMP_Text>();

        damageText.text = text;
        textColor = overrideColor ?? counterColor;

        ApplySetup(baseScale * scaleMultiplier);
    }

    private void ApplySetup(float finalScale)
    {
        damageText.color = textColor;

        // Scale setup
        targetScale = Vector3.one * finalScale;
        transform.localScale = targetScale * popMultiplier;

        // Center Position Offset
        transform.position = new Vector3(
            transform.position.x + Random.Range(-randomOffset.x, randomOffset.x),
            transform.position.y + Random.Range(-randomOffset.y, randomOffset.y),
            -1f
        );

        // Movement setup
        float randomDir = Random.Range(-0.5f, 0.5f);
        initialVelocity = new Vector3(randomDir * horizontalArcSpread, floatSpeed, 0f);
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * popSpeed);

        initialVelocity.y -= gravity * Time.deltaTime;
        transform.position += initialVelocity * Time.deltaTime;

        timer += Time.deltaTime;
        if (timer >= fadeDuration)
        {
            Destroy(gameObject);
        }
        else
        {
            textColor.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            damageText.color = textColor;
        }
    }
}