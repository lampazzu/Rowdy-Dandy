using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class EXPGem : MonoBehaviour
{
    [Header("EXP Value")]
    [SerializeField] private float expValue = 15f;

    [Header("Burst / Ejection Phase")]
    [SerializeField] private float burstForceMin = 2.5f;
    [SerializeField] private float burstForceMax = 4.5f;
    [SerializeField] private float burstDuration = 0.35f;

    [Header("Magnet / Collection Phase")]
    [SerializeField] private float magnetRadius = 4f;
    [SerializeField] private float initialSpeed = 2f;
    [SerializeField] private float acceleration = 25f;

    [Header("Visual Juice & Polish")]
    [SerializeField] private float floatSpeed = 3f;
    [SerializeField] private float floatAmplitude = 0.08f;
    [SerializeField] private Vector3 spawnScale = new Vector3(0.1f, 0.1f, 1f);
    [SerializeField] private Vector3 targetScale = Vector3.one;

    [Header("Audio & FX")]
    [SerializeField] private AudioClip collectSFX;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.7f;
    [SerializeField] private GameObject collectParticlePrefab;

    private Transform playerTransform;
    private CircleCollider2D circleCollider;
    private SpriteRenderer spriteRenderer;

    private Vector3 initialPosition;
    private Vector3 burstVelocity;
    private float currentFlySpeed;
    private bool isBursting = true;
    private bool isBeingMagneted = false;
    private float aliveTimer = 0f;

    private void Awake()
    {
        circleCollider = GetComponent<CircleCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Auto-configure trigger collider if not set in Inspector
        circleCollider.isTrigger = true;
        if (circleCollider.radius <= 0) circleCollider.radius = 0.3f;
    }

    private void Start()
    {
        // Find Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // Setup Ejection (Explosion outwards)
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        float randomForce = Random.Range(burstForceMin, burstForceMax);
        burstVelocity = randomDir * randomForce;

        transform.localScale = spawnScale;
        currentFlySpeed = initialSpeed;

        StartCoroutine(BurstRoutine());
    }

    private IEnumerator BurstRoutine()
    {
        isBursting = true;
        float elapsed = 0f;

        while (elapsed < burstDuration)
        {
            // Decelerate the burst over time
            float progress = elapsed / burstDuration;
            transform.position += burstVelocity * (1f - progress) * Time.deltaTime;

            // Scale pop-in animation
            transform.localScale = Vector3.Lerp(spawnScale, targetScale, progress);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = targetScale;
        initialPosition = transform.position;
        isBursting = false;
    }

    private void Update()
    {
        if (isBursting) return;

        aliveTimer += Time.deltaTime;

        if (playerTransform == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // Check if player is within magnet radius
        if (distanceToPlayer <= magnetRadius || isBeingMagneted)
        {
            isBeingMagneted = true;

            // Accelerate towards player like a homing missile
            currentFlySpeed += acceleration * Time.deltaTime;
            transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, currentFlySpeed * Time.deltaTime);
        }
        else
        {
            // Hover / Bobbing effect while waiting on ground
            float hoverOffsetY = Mathf.Sin(aliveTimer * floatSpeed) * floatAmplitude;
            transform.position = initialPosition + new Vector3(0f, hoverOffsetY, 0f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            CollectGem();
        }
    }

    private void CollectGem()
    {
        // Add EXP
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.AddEXP(expValue);
        }

        // Play SFX (Works even if GameObject is destroyed immediately)
        if (collectSFX != null)
        {
            AudioSource.PlayClipAtPoint(collectSFX, transform.position, soundVolume);
        }

        // Optional particle hit effect
        if (collectParticlePrefab != null)
        {
            Instantiate(collectParticlePrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}