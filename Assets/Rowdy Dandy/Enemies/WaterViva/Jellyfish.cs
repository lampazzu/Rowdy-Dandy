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
    [SerializeField] private float squashAmount = 0.7f; // How much it squashes
    [SerializeField] private float squashTime = 0.2f; // How fast it squashes

    private Light2D jellyfishLight;
    private Animator jellyfishAnimator;
    private Coroutine glowCoroutine;
    private Vector3 startPos;
    private Vector3 originalScale;
    private float hoverOffset; // Randomized offset for unique hovering

    private void Start()
    {
        jellyfishLight = GetComponent<Light2D>();
        jellyfishAnimator = GetComponent<Animator>();
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

                if (jellyfishAnimator != null)
                {
                    jellyfishAnimator.SetTrigger("Splish");
                }

                // Start Squash Effect
                StartCoroutine(SquashEffect());
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            GetComponent<Collider2D>().sharedMaterial = null;

            if (jellyfishAnimator != null)
            {
                jellyfishAnimator.ResetTrigger("Splish");
            }
        }
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

    private IEnumerator SquashEffect()
    {
        // Squash horizontally (wider) and slightly reduce height
        transform.localScale = new Vector3(originalScale.x * 1.3f, originalScale.y * 0.8f, originalScale.z);
        yield return new WaitForSeconds(squashTime);

        // Restore to original size smoothly
        transform.localScale = originalScale;
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
