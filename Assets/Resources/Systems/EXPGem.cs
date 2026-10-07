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
    [Tooltip("Sideways drift while hovering (figure-8 with the bob).")]
    [SerializeField] private float driftAmplitude = 0.06f;
    [Tooltip("How far the gem snakes side to side while flying to Rowdy (fades out as it gets close).")]
    [SerializeField] private float waveAmplitude = 0.35f;
    [Tooltip("Snaking speed while flying to Rowdy.")]
    [SerializeField] private float waveFrequency = 9f;

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
    private Vector3 flightPosition;   // the straight homing path; the gem snakes around it
    private float wavePhase;
    private float magnetTime;

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
        wavePhase = Random.Range(0f, Mathf.PI * 2f);

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
            if (!isBeingMagneted) flightPosition = transform.position;
            isBeingMagneted = true;
            magnetTime += Time.deltaTime;

            // Accelerate towards player like a homing missile, snaking side to side around the straight path
            currentFlySpeed += acceleration * Time.deltaTime;
            flightPosition = Vector2.MoveTowards(flightPosition, playerTransform.position, currentFlySpeed * Time.deltaTime);
            Vector2 toPlayer = (Vector2)(playerTransform.position - flightPosition);
            float remaining = toPlayer.magnitude;
            Vector2 side = remaining > 0.001f ? new Vector2(-toPlayer.y, toPlayer.x) / remaining : Vector2.zero;
            float fade = Mathf.Clamp01(remaining / 1.5f) * Mathf.Clamp01(magnetTime * 4f); // eases in, settles on arrival
            float wave = Mathf.Sin(magnetTime * waveFrequency + wavePhase) * waveAmplitude * fade;
            transform.position = flightPosition + (Vector3)(side * wave);
        }
        else
        {
            // Hover: bob up and down while drifting side to side (a lazy figure 8)
            float t = aliveTimer * floatSpeed + wavePhase;
            float hoverOffsetY = Mathf.Sin(t) * floatAmplitude;
            float hoverOffsetX = Mathf.Sin(t * 0.5f) * driftAmplitude;
            transform.position = initialPosition + new Vector3(hoverOffsetX, hoverOffsetY, 0f);
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