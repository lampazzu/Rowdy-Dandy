using UnityEngine;

using UnityEngine.Events;
using System.Collections;
using UnityEngine.Rendering.Universal;

public class Jellyfish : MonoBehaviour
{
    public PhysicsMaterial2D bouncyMaterial;
    public float bounceForce = 10f;
    public float glowDuration = 2f;
    [SerializeField] public UnityEvent onJelly;

    [SerializeField] private bool isSmallJelly = false;
    [SerializeField] private float hoverSpeed = 2f;
    [SerializeField] private float hoverAmount = 0.5f;

    [Header("Squash Settings")]
#pragma warning disable 0414
    [SerializeField] private float squashAmount = 0.7f; // (unused: the wobble has a fixed strength)
#pragma warning restore 0414
    [SerializeField] private float squashTime = 0.2f; // How fast it squashes

    private Light2D jellyfishLight;
    private Animator jellyfishAnimator;
    private Coroutine glowCoroutine;
    private Vector3 startPos;
    private Vector3 originalScale;
    private float hoverOffset; // Randomized offset for unique hovering
    private bool hasSplishTrigger; // Not every jelly's Animator has the "Splish" trigger

    private void Start()
    {
        jellyfishLight = GetComponent<Light2D>();
        jellyfishAnimator = GetComponent<Animator>();
        hasSplishTrigger = AnimatorHasParameter(jellyfishAnimator, "Splish");
        startPos = transform.position;
        originalScale = transform.localScale; // Store original scale
        hoverOffset = Random.Range(0f, Mathf.PI * 2); // Random offset to desync the hovering
    }

    private void Update()
    {
        if (isSmallJelly)
        {
            HoverMotion();
        }
    }

    private void HoverMotion()
    {
        float hoverY = Mathf.Sin((Time.time + hoverOffset) * hoverSpeed) * hoverAmount;
        transform.position = startPos + new Vector3(0, hoverY, 0);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player") && IsPlayerAbove(collision))
        {
            Rigidbody2D playerRb = collision.collider.GetComponent<Rigidbody2D>();

            if (playerRb != null)
            {
                GetComponent<Collider2D>().sharedMaterial = bouncyMaterial;
                playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, bounceForce);

                onJelly.Invoke();

                if (jellyfishLight != null)
                {
                    jellyfishLight.intensity = 1f;
                    if (glowCoroutine != null)
                    {
                        StopCoroutine(glowCoroutine);
                    }
                    glowCoroutine = StartCoroutine(ResetLightIntensity(glowDuration));
                }

                if (hasSplishTrigger)
                {
                    jellyfishAnimator.SetTrigger("Splish");
                }

                // Start Squash Effect (springy wobble) + splash, rising boing combo
                if (wobble != null) StopCoroutine(wobble);
                wobble = StartCoroutine(SquashEffect());
                BounceJuice(collision);
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            GetComponent<Collider2D>().sharedMaterial = null;

            if (hasSplishTrigger)
            {
                jellyfishAnimator.ResetTrigger("Splish");
            }
        }
    }

    private static bool AnimatorHasParameter(Animator animator, string parameterName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName) return true;
        }
        return false;
    }

    private IEnumerator ResetLightIntensity(float duration)
    {
        float startIntensity = jellyfishLight.intensity;
        float timeElapsed = 0f;

        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;
            jellyfishLight.intensity = Mathf.Lerp(startIntensity, 0.3f, timeElapsed / duration);
            yield return null;
        }

        jellyfishLight.intensity = 0f;
    }

    private Coroutine wobble;

    // Big squash on the hit, then a damped spring back (stretches tall, squashes again, settles)
    private IEnumerator SquashEffect()
    {
        float duration = Mathf.Max(0.35f, squashTime * 3f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float spring = Mathf.Cos(t * Mathf.PI * 3.2f) * Mathf.Exp(-t * 4f); // 1 -> -0.4 -> ... -> 0
            float squash = 0.35f * spring; // fixed strength (the Inspector's Squash Amount is 30 on many jellies and was never used)
            transform.localScale = new Vector3(originalScale.x * (1f + squash), originalScale.y * (1f - squash * 0.9f), originalScale.z);
            yield return null;
        }
        transform.localScale = originalScale;
        wobble = null;
    }

    // Consecutive bounces (within a second and a half) climb a scale: boing, boing, BOING
    private static int combo;
    private static float lastBounce = -10f;
    private static readonly int[] Steps = { 0, 2, 4, 5, 7, 9, 11, 12 };

    private void BounceJuice(Collision2D collision)
    {
        combo = Time.time - lastBounce < 1.5f ? combo + 1 : 0;
        lastBounce = Time.time;
        RunStats.JellyBounces++;

        Vector3 top = GetComponent<Collider2D>().bounds.center;
        top.y = GetComponent<Collider2D>().bounds.max.y;
        Color jelly = jellyfishLight != null ? jellyfishLight.color : new Color(0.6f, 0.9f, 1f);

        PulseRing.Spawn(top, new Color(jelly.r, jelly.g, jelly.b, 0.9f), 0.9f + combo * 0.1f, 0.3f, 90, true);
        PulseRing.Spawn(top, new Color(1f, 1f, 1f, 0.6f), 0.5f, 0.2f, 91, true);
        FXParticle.Burst(top, Color.Lerp(jelly, Color.white, 0.4f), 10 + combo * 2, 1.5f, 4f, 9f, 0.6f, true);
        FXSound.Play("Jelly", 0.5f, Mathf.Pow(2f, Steps[Mathf.Min(combo, Steps.Length - 1)] / 12f));
        TimeSlowController.HitStop(0.03f, 0.2f);
        ScreenShake.Impulse(0.12f + combo * 0.03f);
        GamepadRumble.Pulse(0.15f, 0.35f, 0.08f);
    }

    void JellyTime()
    {
        jellyfishAnimator.SetTrigger("Jelly");
    }

    private bool IsPlayerAbove(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f) // Ensures player is coming from above
            {
                return true;
            }
        }
        return false;
    }
}
