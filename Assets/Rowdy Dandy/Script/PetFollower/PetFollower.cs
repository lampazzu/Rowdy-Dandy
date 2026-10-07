using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PetFollower : MonoBehaviour
{
    public enum CatType { Wig, Samurai }

    private static readonly List<PetFollower> ActivePets = new List<PetFollower>();
    public static IReadOnlyList<PetFollower> Pets => ActivePets;

    // Enemies a samurai is already on its way to, so two samurai don't chase the same kill
    private static readonly HashSet<EnemyHealth> ClaimedTargets = new HashSet<EnemyHealth>();

    private static int pickupCounter;
    private int pickupOrder;

    [Header("Cat Type")]
    [Tooltip("Wig: small AoE hits on enemies and projectiles. Samurai: dashes to weakened enemies and executes them, chaining between kills.")]
    [SerializeField] private CatType catType = CatType.Wig;

    [Header("HUD")]
    [Tooltip("Shown next to the portrait. Empty = the cat type's name.")]
    [SerializeField] private string catName = "";
    [Tooltip("16x16 face for the HUD slot. Empty = the cat's own sprite.")]
    [SerializeField] private Sprite portrait;

    [Header("Voice")]
    [Tooltip("One is picked at random on attack (never the same twice in a row)")]
    [SerializeField] private AudioClip[] attackVoices;
    [Tooltip("1 = same level as the other sound effects. Above 1 boosts it (5 cancels the project's 0.2 global volume = the file at full loudness).")]
    [SerializeField, Range(0f, 5f)] private float voiceVolume = 1f;
    [SerializeField] private float voicePitchVariation = 0.05f;
    [Tooltip("Samurai: speak on every cut of a chain instead of only the first")]
    [SerializeField] private bool voiceEveryStrike = false;

    [Header("Following Settings")]
    [SerializeField] private Transform player;
    [SerializeField] private float followDelay = 0.5f;
    [SerializeField] private float followSpeed = 3f;
    [Tooltip("Position next to an enemy while attacking (the attack clip keys this)")]
    [SerializeField] private Vector3 offset = new Vector3(-1f, 1.5f, 0f);

    [Header("Formation (Soulmass ring around Rowdy)")]
    [Tooltip("The first cat picked up sets these for the whole ring")]
    [SerializeField] private Vector2 orbitCenter = new Vector2(0f, 0.6f);
    [Tooltip("x = ring width, y = how tilted it looks (small = flat ring seen from the side)")]
    [SerializeField] private Vector2 orbitRadius = new Vector2(0.6f, 0.12f);
    [Tooltip("Extra width per cat so a big ring doesn't crowd")]
    [SerializeField] private float orbitRadiusPerCat = 0.06f;
    [Tooltip("Degrees per second")]
    [SerializeField] private float orbitSpeed = 50f;

    [Header("Hovering Settings")]
    [SerializeField] private float hoverSpeed = 2f;
    [SerializeField] private float hoverAmount = 0.2f;

    [Header("Attack Settings")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private bool backToIdle = false;
    [Tooltip("Off = ignore breakables ticked 'Is Object' on their EnemyHealth (plants, wolf statues...)")]
    [SerializeField] private bool targetObjects = false;

    [Header("Samurai: Execution")]
    [Tooltip("Executes enemies at or below this fraction of their max health (0.1 = 10%)")]
    [SerializeField, Range(0f, 1f)] private float executeHealthPercent = 0.1f;
    [Tooltip("...or at or below this many health points (fodder with tiny max health counts right away)")]
    [SerializeField] private float executeHealthPoints = 10f;
    [Tooltip("Most kills in one execution chain")]
    [SerializeField] private int maxChain = 5;
    [Tooltip("How far from the last kill he looks for the next one")]
    [SerializeField] private float chainRange = 4f;
    [SerializeField] private float dashSpeed = 18f;
    [Tooltip("Pause next to the enemy before the cut lands")]
    [SerializeField] private float strikeDelay = 0.15f;
    [Tooltip("Pause after the cut before dashing to the next one")]
    [SerializeField] private float afterStrikePause = 0.2f;
    [Tooltip("Animator state played on every cut (restarts each time). Swap when the samurai art is in.")]
    [SerializeField] private string strikeState = "WigAttack";
    [SerializeField] private UnityEvent onExecute;

    private Transform targetEnemy = null;
    private bool isFollowingPlayer = false;
    private bool canAttack = true;
    private bool isExecuting = false;
    private float cooldownEnd;
    private Animator anim;
    private AudioSource voiceSource;
    private int lastVoice = -1;
    private bool facingRight = true;

    // Drawn behind Rowdy on the back half of the ring, in front on the front half
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer playerRenderer;
    private int baseSortingLayer;
    private int baseSortingOrder;
    private bool orbitBehind;

    // Gives every pet slightly different movement timing
    private float hoverOffset;
    private float movementOffset;

    // For the cat HUD (CatHUD)
    public CatType Type => catType;
    public string CatName => string.IsNullOrEmpty(catName) ? catType.ToString() : catName;
    public Sprite Portrait => portrait != null ? portrait : (TryGetComponent(out SpriteRenderer sr) ? sr.sprite : null);
    public bool IsCollected => player != null;

    // 1 = ready, 0 = just used (or mid execution chain)
    public float CooldownFraction
    {
        get
        {
            if (isExecuting) return 0f;
            if (canAttack || attackCooldown <= 0f) return 1f;
            return 1f - Mathf.Clamp01((cooldownEnd - Time.time) / attackCooldown);
        }
    }

    private void Awake()
    {
        hoverOffset = Random.Range(0f, 10f);
        movementOffset = Random.Range(0f, 10f);

        ActivePets.Add(this);
    }

    private void OnDestroy()
    {
        ActivePets.Remove(this);
    }

    private void Start()
    {
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            baseSortingLayer = spriteRenderer.sortingLayerID;
            baseSortingOrder = spriteRenderer.sortingOrder;
        }

        // Own source for the voice lines (AudioVolumeManager scales it with the SFX setting)
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.volume = 1f; // the boost goes through PlayOneShot, AudioSource.volume can't go above 1

        // Clips imported without "Preload Audio Data" stay unloaded and a one-shot on them can play nothing
        if (attackVoices != null)
        {
            foreach (AudioClip clip in attackVoices)
            {
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            }
        }
    }

    private void PlayVoice()
    {
        if (voiceSource == null || attackVoices == null || attackVoices.Length == 0) return;

        int index = Random.Range(0, attackVoices.Length);
        if (attackVoices.Length > 1 && index == lastVoice) index = (index + 1) % attackVoices.Length;
        lastVoice = index;

        if (attackVoices[index] == null) return;
        voiceSource.pitch = 1f + Random.Range(-voicePitchVariation, voicePitchVariation);
        voiceSource.PlayOneShot(attackVoices[index], voiceVolume);
    }

    private void Update()
    {
        // The execution chain moves him itself; the placeholder Wig clip still keys backToIdle, ignore it meanwhile
        if (isExecuting)
        {
            backToIdle = false;
            return;
        }

        if (backToIdle)
        {
            ResetToIdle();
            return;
        }

        if (player == null)
            return;

        if (canAttack)
        {
            if (catType == CatType.Samurai) TryStartExecution();
            else DetectNearestEnemy();
        }

        if (isFollowingPlayer)
        {
            FollowTarget(player);
            MatchPlayerDirection();
        }
        else
        {
            FollowTarget(targetEnemy);
            FlipTowards(targetEnemy);
        }

        UpdateDepth();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (player != null)
            return;

        if (other.CompareTag("Player"))
        {
            player = other.transform;
            pickupOrder = ++pickupCounter;
            isFollowingPlayer = true;

            StartCoroutine(StartFollowing());
        }
    }

    private void FollowTarget(Transform target)
    {
        if (target == null)
            return;

        // Formation spot behind Rowdy, or the (clip-keyed) attack offset next to an enemy
        Vector3 formationOffset = target == player ? GetFormationOffset() : offset;

        // Slightly desynchronize the hovering between pets
        float hoverY = Mathf.Sin((Time.time + hoverOffset) * hoverSpeed) * hoverAmount;

        Vector3 targetPosition =
            target.position +
            formationOffset +
            new Vector3(0, hoverY, 0);

        // Slight variation in follow speed
        float individualFollowSpeed =
            followSpeed + Mathf.Sin(Time.time + movementOffset) * 0.15f;

        transform.position = Vector2.Lerp(
            transform.position,
            targetPosition,
            individualFollowSpeed * Time.deltaTime
        );
    }

    // This cat's spot on the ring circling Rowdy's head (Soulmass style). Cats are spaced evenly by pickup order
    // (a cat off attacking keeps its spot, so nobody reshuffles) and the whole ring turns slowly, whatever way he faces.
    private Vector3 GetFormationOffset()
    {
        int index = 0, count = 1;
        PetFollower leader = this;
        foreach (PetFollower pet in ActivePets)
        {
            if (pet == null || pet == this || pet.player != player) continue;
            count++;
            if (ComesBefore(pet, this)) index++;
            if (ComesBefore(pet, leader)) leader = pet;
        }

        // The first cat's settings drive the ring, so mixed prefabs still spin together
        float angle = (Time.time * leader.orbitSpeed + index * 360f / count) * Mathf.Deg2Rad;
        float width = leader.orbitRadius.x + leader.orbitRadiusPerCat * (count - 1);

        // sin > 0 = far side of the ring (higher up, behind him)
        orbitBehind = Mathf.Sin(angle) > 0f;
        return new Vector3(
            leader.orbitCenter.x + Mathf.Cos(angle) * width,
            leader.orbitCenter.y + Mathf.Sin(angle) * leader.orbitRadius.y,
            0f);
    }

    private static bool ComesBefore(PetFollower a, PetFollower b)
    {
        if (a.pickupOrder != b.pickupOrder) return a.pickupOrder < b.pickupOrder;
        return a.GetInstanceID() < b.GetInstanceID();
    }

    // Behind Rowdy on the far half of the ring, in front on the near half; normal sorting when off attacking
    private void UpdateDepth()
    {
        if (spriteRenderer == null) return;

        if (isFollowingPlayer && player != null)
        {
            if (playerRenderer == null) playerRenderer = player.GetComponent<SpriteRenderer>();
            if (playerRenderer == null) return;

            spriteRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = playerRenderer.sortingOrder + (orbitBehind ? -1 : 1);
        }
        else
        {
            spriteRenderer.sortingLayerID = baseSortingLayer;
            spriteRenderer.sortingOrder = baseSortingOrder;
        }
    }

    private void DetectNearestEnemy()
    {
        int enemyLayer = LayerMask.GetMask("Enemy");
        int projectileLayer = LayerMask.GetMask("projectileLayer");
        int combinedLayerMask = enemyLayer | projectileLayer;

        Collider2D[] targets = Physics2D.OverlapCircleAll(
            transform.position,
            detectionRange,
            combinedLayerMask
        );

        float closestDistance = Mathf.Infinity;
        Transform closestTarget = null;

        foreach (Collider2D target in targets)
        {
            if (target == null)
                continue;

            // Only things he's allowed to hit (so a plant next to him doesn't block the enemy behind it)
            EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
            if (enemyHealth == null || enemyHealth.NoPetFollow || (enemyHealth.IsObject && !targetObjects))
                continue;

            float distance = Vector2.Distance(
                transform.position,
                target.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = target.transform;
            }
        }

        if (closestTarget != null && canAttack)
        {
            targetEnemy = closestTarget;
            isFollowingPlayer = false;
            StartCoroutine(AttackEnemy());
        }
        else
        {
            if (targetEnemy != null && targetEnemy.gameObject == null)
            {
                targetEnemy = null;
                isFollowingPlayer = true;
            }
        }
    }

    private IEnumerator AttackEnemy()
    {
        canAttack = false;
        cooldownEnd = Time.time + attackCooldown;

        anim.SetTrigger("Attack");
        PlayVoice();

        yield return new WaitForSeconds(attackCooldown);

        if (targetEnemy != null && targetEnemy.gameObject != null)
        {
            targetEnemy = null;
            isFollowingPlayer = true;
        }

        canAttack = true;
    }

    // ---------------------------------------------------------------- Samurai

    private void TryStartExecution()
    {
        EnemyHealth first = FindExecutable(transform.position, detectionRange, null);
        if (first != null)
        {
            StartCoroutine(ExecutionChain(first));
        }
    }

    private IEnumerator ExecutionChain(EnemyHealth target)
    {
        canAttack = false;
        isExecuting = true;
        isFollowingPlayer = false;

        var visited = new HashSet<EnemyHealth>();
        int kills = 0;
        bool spoke = false;

        while (target != null && kills < maxChain)
        {
            visited.Add(target);
            ClaimedTargets.Add(target);
            targetEnemy = target.transform;

            // Dash to the enemy's near side (gives up if it takes too long, e.g. the enemy got knocked far away)
            float dashTimer = 0f;
            while (target != null && IsExecutable(target) && dashTimer < 1f)
            {
                Vector3 enemyPos = target.transform.position;
                float side = enemyPos.x >= transform.position.x ? -1f : 1f;
                Vector3 strikePos = enemyPos + new Vector3(side * 0.4f, 0.1f, 0f);

                FlipTowards(target.transform);
                transform.position = Vector2.MoveTowards(transform.position, strikePos, dashSpeed * Time.deltaTime);
                if (Vector2.Distance(transform.position, strikePos) < 0.05f) break;

                dashTimer += Time.deltaTime;
                yield return null;
            }

            if (target != null && IsExecutable(target))
            {
                PlayStrike();
                if (!spoke || voiceEveryStrike) PlayVoice();
                spoke = true;
                yield return new WaitForSeconds(strikeDelay);

                // Still alive and still weak enough (the player may have killed it meanwhile)
                if (target != null && IsExecutable(target))
                {
                    target.ShowCustomText("EXECUTED!", new Color(1f, 0.85f, 0.3f));
                    target.TakeDamageEnemy(target.currentenemyHealth);
                    onExecute?.Invoke();
                    kills++;
                }

                yield return new WaitForSeconds(afterStrikePause);
            }

            if (target != null) ClaimedTargets.Remove(target);
            target = FindExecutable(transform.position, chainRange, visited);
        }

        targetEnemy = null;
        isFollowingPlayer = true;
        isExecuting = false;
        SetTriggerIfExists("back to idle");

        // Cooldown starts once he's done, so a long chain doesn't eat into it
        cooldownEnd = Time.time + attackCooldown;
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    private bool IsExecutable(EnemyHealth enemy)
    {
        if (enemy == null || enemy.enemydead || enemy.NoPetFollow) return false;
        if (enemy.IsObject && !targetObjects) return false;

        float health = enemy.currentenemyHealth;
        if (health <= 0f) return false;

        return health <= enemy.startingenemyHealth * executeHealthPercent || health <= executeHealthPoints;
    }

    // Closest executable enemy around 'center' that no other samurai is already going for
    private EnemyHealth FindExecutable(Vector2 center, float range, HashSet<EnemyHealth> skip)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, range, LayerMask.GetMask("Enemy"));

        EnemyHealth best = null;
        float bestDistance = Mathf.Infinity;
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;

            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy == null || !enemy.CompareTag("Enemy") || !IsExecutable(enemy)) continue;
            if (ClaimedTargets.Contains(enemy) || (skip != null && skip.Contains(enemy))) continue;

            float distance = Vector2.Distance(center, enemy.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = enemy;
            }
        }
        return best;
    }

    // Restart the cut animation on every kill of the chain (a trigger wouldn't replay while already in the state)
    private void PlayStrike()
    {
        if (anim == null) return;

        int hash = Animator.StringToHash(strikeState);
        if (!string.IsNullOrEmpty(strikeState) && anim.HasState(0, hash))
        {
            anim.Play(hash, 0, 0f);
        }
        else
        {
            anim.SetTrigger("Attack");
        }
    }

    private void SetTriggerIfExists(string trigger)
    {
        if (anim == null) return;
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == trigger) { anim.SetTrigger(trigger); return; }
        }
    }

    // ----------------------------------------------------------------

    private void ResetToIdle()
    {
        anim.SetTrigger("back to idle");

        targetEnemy = null;
        isFollowingPlayer = true;
        backToIdle = false;
    }

    private IEnumerator StartFollowing()
    {
        yield return new WaitForSeconds(followDelay);

        if (player != null)
            isFollowingPlayer = true;
    }

    private void FlipTowards(Transform target)
    {
        if (target == null)
            return;

        if (target.position.x > transform.position.x && !facingRight)
        {
            Flip();
        }
        else if (target.position.x < transform.position.x && facingRight)
        {
            Flip();
        }
    }

    private void MatchPlayerDirection()
    {
        if (player == null)
            return;

        if ((player.localScale.x > 0 && !facingRight) ||
            (player.localScale.x < 0 && facingRight))
        {
            Flip();
        }
    }

    private void Flip()
    {
        facingRight = !facingRight;

        transform.localScale = new Vector3(
            -transform.localScale.x,
            transform.localScale.y,
            transform.localScale.z
        );
    }

    private void OnDisable()
    {
        // Disabled mid-chain (coroutines stop): don't leave enemies claimed or him stuck unable to attack
        if (isExecuting)
        {
            ClaimedTargets.Clear();
            canAttack = true;
            isFollowingPlayer = true;
            targetEnemy = null;
        }
        isExecuting = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (catType == CatType.Samurai)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, chainRange);
        }
    }
}
