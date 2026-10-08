using System.Collections;
using UnityEngine;

// EXP gem: a fountain out of the enemy (arcs up, bounces once on the ground), hovers, then homes in on Rowdy.
// Picking several up in a row plays a climbing chime (GemChime).
[RequireComponent(typeof(CircleCollider2D))]
public class EXPGem : MonoBehaviour
{
    // The pickup sound, shared with the other pickups (ore gems, rat, fish) so they chime together
    private static AudioClip sharedSound;
    public static AudioClip SharedCollectSound
    {
        get
        {
            if (sharedSound == null)
            {
                GameObject prefab = Resources.Load<GameObject>("Systems/EXPgem");
                if (prefab != null && prefab.TryGetComponent(out EXPGem gem)) sharedSound = gem.collectSFX;
            }
            return sharedSound;
        }
        private set => sharedSound = value;
    }

    [Header("EXP Value")]
    [SerializeField] private float expValue = 15f;

    [Header("Burst / Ejection Phase")]
    [SerializeField] private float burstForceMin = 2.5f;
    [SerializeField] private float burstForceMax = 4.5f;
    [SerializeField] private float burstDuration = 0.35f;
    [Tooltip("Fountain: launch speed up, sideways spread, gravity")]
    [SerializeField] private Vector2 fountainUp = new Vector2(3.5f, 5.5f);
    [SerializeField] private float fountainSpread = 1.8f;
    [SerializeField] private float fountainGravity = 15f;

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
        if (collectSFX != null) SharedCollectSound = collectSFX;

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

        // Fountain: up and out in an arc
        burstVelocity = new Vector2(Random.Range(-fountainSpread, fountainSpread), Random.Range(fountainUp.x, fountainUp.y));

        transform.localScale = spawnScale;
        currentFlySpeed = initialSpeed;
        wavePhase = Random.Range(0f, Mathf.PI * 2f);

        StartCoroutine(BurstRoutine());
    }

    private IEnumerator BurstRoutine()
    {
        isBursting = true;
        float elapsed = 0f;
        float groundY = GroundBelow();
        bool bounced = false;

        // Arc under gravity until it lands (one small bounce), at most ~1.2 s
        while (elapsed < 1.2f)
        {
            burstVelocity.y -= fountainGravity * Time.deltaTime;
            Vector3 p = transform.position + burstVelocity * Time.deltaTime;
            if (burstVelocity.y < 0f && p.y <= groundY)
            {
                p.y = groundY;
                if (!bounced && burstVelocity.y < -2f) { burstVelocity = new Vector3(burstVelocity.x * 0.5f, -burstVelocity.y * 0.3f, 0f); bounced = true; }
                else { transform.position = p; break; }
            }
            transform.position = p;

            // Scale pop-in animation
            transform.localScale = Vector3.Lerp(spawnScale, targetScale, Mathf.Clamp01(elapsed / burstDuration));

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = targetScale;
        initialPosition = transform.position;
        isBursting = false;
    }

    // Top of the solid ground under the gem (falls back to a short drop if there's none)
    private float GroundBelow()
    {
        foreach (RaycastHit2D hit in Physics2D.RaycastAll((Vector2)transform.position + Vector2.up * 0.3f, Vector2.down, 30f))
        {
            Collider2D c = hit.collider;
            if (c == null || c.isTrigger) continue;
            if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
            return hit.point.y + 0.15f;
        }
        return transform.position.y - 1f;
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

        // Chime: each gem in a quick streak plays a step higher
        GemChime.Play(collectSFX, soundVolume);

        // Optional particle hit effect
        if (collectParticlePrefab != null)
        {
            Instantiate(collectParticlePrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}