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
    [Tooltip("1 = same level as the other sound effects. Above 1 boosts it (5 cancels the project's 0.2 global volume = the file at full loudness). The Cat Voice setting scales it on top.")]
    [SerializeField, Range(0f, 5f)] private float voiceVolume = 1f;
    [SerializeField] private float voicePitchVariation = 0.05f;
    [Tooltip("Samurai: speak on every cut of a chain instead of only the first")]
    [SerializeField] private bool voiceEveryStrike = false;

    [Header("Pickup")]
    [Tooltip("Played when Rowdy finds this cat")]
    [SerializeField] private AudioClip collectSound;
    [SerializeField, Range(0f, 2f)] private float collectVolume = 0.8f;

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

    // Where the level placed this cat, also its identity across scene reloads (see CatRoster)
    public Vector3 HomePosition { get; private set; }
    public string RosterKey { get; private set; }

    // For the cat HUD (CatHUD)
    public CatType Type => catType;
    public string CatName => string.IsNullOrEmpty(catName) ? catType.ToString() : catName;
    public Sprite Portrait => portrait != null ? portrait : (TryGetComponent(out SpriteRenderer sr) ? sr.sprite : null);
    public bool IsCollected => player != null;

    // Cat treat fish: ready right now, and cooldowns halved for TreatDuration seconds
    private const float TreatDuration = 12f;
    private float fedUntil = -10f;
    public bool IsFed => Time.time < fedUntil;
    private float Cooldown => IsFed ? attackCooldown * 0.5f : attackCooldown;

    public void Treat()
    {
        fedUntil = Time.time + TreatDuration;
        if (!isExecuting) { canAttack = true; cooldownEnd = Time.time; }
    }

    // 1 = ready, 0 = just used (or mid execution chain)
    public float CooldownFraction
    {
        get
        {
            if (isExecuting) return 0f;
            if (canAttack || attackCooldown <= 0f) return 1f;
            return 1f - Mathf.Clamp01((cooldownEnd - Time.time) / Cooldown);
        }
    }

    private void Awake()
    {
        hoverOffset = Random.Range(0f, 10f);
        movementOffset = Random.Range(0f, 10f);

        HomePosition = transform.position;
        RosterKey = gameObject.scene.name + "/" + catType + "/" + Mathf.RoundToInt(HomePosition.x * 10f) + "," + Mathf.RoundToInt(HomePosition.y * 10f);

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

        // Glowing outline while nobody has found him yet (see CatFX.cs)
        if (GetComponent<LostCatGlow>() == null) gameObject.AddComponent<LostCatGlow>();
    }

    private void PlayVoice()
    {
        if (voiceSource == null || attackVoices == null || attackVoices.Length == 0) return;

        int index = Random.Range(0, attackVoices.Length);
        if (attackVoices.Length > 1 && index == lastVoice) index = (index + 1) % attackVoices.Length;
        lastVoice = index;

        if (attackVoices[index] == null) return;
        voiceSource.pitch = 1f + Random.Range(-voicePitchVariation, voicePitchVariation);
        voiceSource.PlayOneShot(attackVoices[index], voiceVolume * GameSettings.CatVoiceVolume);
    }

    private void Update()
    {
        CatRoster.Tick();

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

            CatRoster.RecordCollected(RosterKey);
            RunStats.CatsRescued++;
            RowdyNotes.MarkCatFound(catType.ToString(), CatName, Portrait); // unlocks its Cats page
            SoundManager.PlaySfx(collectSound, collectVolume);

            StartCoroutine(StartFollowing());
        }
    }

    // Already Rowdy's cat before the scene reloaded: join him straight away, no pickup sound
    public void Rejoin(Transform rowdy, int order)
    {
        player = rowdy;
        pickupOrder = order;
        pickupCounter = Mathf.Max(pickupCounter, order);
        RowdyNotes.MarkCatFound(catType.ToString(), CatName, Portrait);
        isFollowingPlayer = true;
        transform.position = rowdy.position + new Vector3(Random.Range(-0.4f, 0.4f), 0.6f, 0f);
    }

    // Moves a cat that isn't Rowdy's (yet) to a new hiding spot
    public void Relocate(Vector3 position)
    {
        if (player != null) return;
        transform.position = position;
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
        float cooldown = Cooldown;
        cooldownEnd = Time.time + cooldown;

        anim.SetTrigger("Attack");
        PlayVoice();

        while (Time.time < cooldownEnd) yield return null; // (a treat can cut it short)

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
        TrailRenderer trail = CatFX.StartTrail(transform, spriteRenderer);
        float afterimageTimer = 0f;

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

                // Ninja afterimages along the dash
                afterimageTimer -= Time.deltaTime;
                if (afterimageTimer <= 0f)
                {
                    afterimageTimer = 0.03f;
                    CatFX.Afterimage(spriteRenderer, new Color(CatFX.NinjaBlue.r, CatFX.NinjaBlue.g, CatFX.NinjaBlue.b, 0.6f));
                }

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
                    // The cut: glowing slash, flash, hit-stop - so you can actually see what he did
                    SpriteRenderer enemySprite = target.GetComponent<SpriteRenderer>();
                    CatFX.Slash(target.transform.position, spriteRenderer,
                        enemySprite != null ? enemySprite.sortingLayerID : (spriteRenderer != null ? spriteRenderer.sortingLayerID : 0),
                        enemySprite != null ? enemySprite.sortingOrder : 0);
                    StartCoroutine(CatFX.CutFreeze(anim, target.GetComponent<Animator>(), 0.11f));

                    target.ShowCustomText("EXECUTED!", new Color(1f, 0.85f, 0.3f));
                    EnemyHealth.CreditNextHit(KillCredit.Cat(this, true));
                    target.TakeDamageEnemy(target.currentenemyHealth);
                    onExecute?.Invoke();
                    kills++;
                }

                yield return new WaitForSeconds(afterStrikePause);
            }

            if (target != null) ClaimedTargets.Remove(target);
            target = FindExecutable(transform.position, chainRange, visited);
        }

        CatFX.StopTrail(trail);
        targetEnemy = null;
        isFollowingPlayer = true;
        isExecuting = false;
        SetTriggerIfExists("back to idle");

        // Cooldown starts once he's done, so a long chain doesn't eat into it
        cooldownEnd = Time.time + Cooldown;
        while (Time.time < cooldownEnd) yield return null; // (a treat can cut it short)
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

// Remembers Rowdy's cats across scene reloads (death, checkpoint reload) for this play session.
//  - Cats he has come back with him after a reload.
//  - Dying costs the most recently found cat: it gets lost and has to be found again.
//  - Every load, the cats he doesn't have are hidden again at random reachable spots: the spots the level
//    placed cats at, plus places Rowdy has actually stood on solid ground (so always reachable).
public static class CatRoster
{
    private const float MinDistanceFromRowdy = 8f;   // don't hide a cat right where he respawns
    private const float MinSpacing = 4f;             // keep hidden cats apart
    private const float VisitedSampleSpacing = 3f;   // how often his footsteps are remembered
    private const int MaxVisitedPerScene = 400;
    private static readonly Vector3 HoverAboveFeet = new Vector3(0f, 0.35f, 0f);

    private static readonly List<string> collected = new List<string>(); // pickup order

    // Registered flying rats: each one is bait that keeps one cat from getting lost on death
    public static int Rats { get; private set; }
    public static void AddRat() => Rats++;
    public static int CatCount => collected.Count;
    private static readonly Dictionary<string, List<Vector3>> visitedSpots = new Dictionary<string, List<Vector3>>();
    private static bool needsApply = true;
    private static bool diedBeforeReload;
    private static int lastTickFrame = -1;

    private static Transform rowdy;
    private static PlayerMovement rowdyMovement;
    private static Collider2D[] rowdyColliders;
    private static float findRowdyTimer;
    private static Vector3 lastSample = new Vector3(float.MaxValue, float.MaxValue, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        collected.Clear();
        visitedSpots.Clear();
        Rats = 0;
        needsApply = true;
        diedBeforeReload = false;
        lastTickFrame = -1;
        rowdy = null;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        needsApply = true;
        rowdy = null;
        lastSample = new Vector3(float.MaxValue, float.MaxValue, 0f);
    }

    public static void MarkDied() => diedBeforeReload = true;

    // Dev reset (key 0): forget every collected cat and the remembered hiding spots
    public static void ClearAll()
    {
        Rats = 0;
        collected.Clear();
        visitedSpots.Clear();
        needsApply = true;
        diedBeforeReload = false;
    }

    public static void RecordCollected(string key)
    {
        if (!collected.Contains(key)) collected.Add(key);
    }

    // Called by every cat's Update; runs once per frame
    public static void Tick()
    {
        if (Time.frameCount == lastTickFrame) return;
        lastTickFrame = Time.frameCount;

        if (rowdy == null && !FindRowdy()) return;

        // First frame after a load (after Rowdy was moved to his checkpoint)
        if (needsApply) ApplyToScene();

        SampleVisitedSpot();
    }

    private static bool FindRowdy()
    {
        findRowdyTimer -= Time.unscaledDeltaTime;
        if (findRowdyTimer > 0f) return false;
        findRowdyTimer = 0.5f;

        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) return false;
        rowdy = go.transform;
        rowdyMovement = go.GetComponent<PlayerMovement>();
        rowdyColliders = go.GetComponentsInChildren<Collider2D>();
        return true;
    }

    private static void ApplyToScene()
    {
        needsApply = false;
        string sceneName = rowdy.gameObject.scene.name;

        // Death penalty: the newest cat gets lost - unless there's a registered rat for every cat
        string lostKey = null;
        if (diedBeforeReload && collected.Count > Rats)
        {
            lostKey = collected[collected.Count - 1];
            collected.RemoveAt(collected.Count - 1);
            RunStats.CatsLost++;
        }
        diedBeforeReload = false;

        var pets = new List<PetFollower>();
        foreach (PetFollower pet in PetFollower.Pets)
        {
            if (pet != null && pet.gameObject.scene.name == sceneName) pets.Add(pet);
        }

        // Cats he still has rejoin him
        for (int i = 0; i < collected.Count; i++)
        {
            foreach (PetFollower pet in pets)
            {
                if (!pet.IsCollected && pet.RosterKey == collected[i]) { pet.Rejoin(rowdy, i + 1); break; }
            }
        }

        // Hide the rest again
        var homeSpots = new List<Vector3>();
        foreach (PetFollower pet in pets) homeSpots.Add(pet.HomePosition);
        visitedSpots.TryGetValue(sceneName, out List<Vector3> visited);

        var taken = new List<Vector3>();
        PetFollower lostPet = null;
        foreach (PetFollower pet in pets)
        {
            if (!pet.IsCollected && pet.RosterKey == lostKey) lostPet = pet;
        }

        // The cat that just got lost goes somewhere he has been (if we know any), the others anywhere valid
        if (lostPet != null) PlaceCat(lostPet, visited != null && visited.Count > 0 ? visited : homeSpots, homeSpots, taken);
        foreach (PetFollower pet in pets)
        {
            if (pet.IsCollected || pet == lostPet) continue;
            var pool = new List<Vector3>(homeSpots);
            if (visited != null) pool.AddRange(visited);
            PlaceCat(pet, pool, homeSpots, taken);
        }

        if (lostPet != null) LostNotice = new LostCat { name = lostPet.CatName, portrait = lostPet.Portrait, pet = lostPet };
    }

    // The cat lost by the last death, picked up by CatHUD (its slot lingers and fades: "NICK GOT LOST")
    public class LostCat { public string name; public Sprite portrait; public PetFollower pet; }
    public static LostCat LostNotice;

    private static void PlaceCat(PetFollower pet, List<Vector3> pool, List<Vector3> homeSpots, List<Vector3> taken)
    {
        // Random order, take the first spot that's far enough from Rowdy and the other hidden cats
        for (int attempt = 0; attempt < 2; attempt++)
        {
            List<Vector3> source = attempt == 0 ? pool : homeSpots;
            int count = source.Count;
            if (count == 0) continue;
            int start = Random.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                Vector3 spot = source[(start + i) % count];
                if (Vector2.Distance(spot, rowdy.position) < MinDistanceFromRowdy) continue;
                bool crowded = false;
                foreach (Vector3 t in taken) if (Vector2.Distance(spot, t) < MinSpacing) { crowded = true; break; }
                if (crowded) continue;

                pet.Relocate(spot);
                taken.Add(spot);
                return;
            }
        }
        taken.Add(pet.HomePosition); // nowhere better: stays where the level put it
    }

    // Remembers where Rowdy stands on solid ground (not water) as future hiding spots
    private static void SampleVisitedSpot()
    {
        if (rowdyMovement == null || !rowdyMovement.IsGrounded) return;
        Vector3 pos = rowdy.position;
        if (Vector2.Distance(pos, lastSample) < VisitedSampleSpacing) return;

        RaycastHit2D[] hits = Physics2D.RaycastAll(pos, Vector2.down, 2f);
        foreach (RaycastHit2D hit in hits)
        {
            Collider2D col = hit.collider;
            if (col == null || col.isTrigger || IsRowdyCollider(col)) continue;

            bool water = col.CompareTag("Water") || col.gameObject.layer == LayerMask.NameToLayer("waterLayer") || col.gameObject.layer == LayerMask.NameToLayer("Water");
            if (water) return; // standing on water: not a hiding spot

            string sceneName = rowdy.gameObject.scene.name;
            if (!visitedSpots.TryGetValue(sceneName, out List<Vector3> list))
            {
                list = new List<Vector3>();
                visitedSpots[sceneName] = list;
            }
            if (list.Count >= MaxVisitedPerScene) list.RemoveAt(Random.Range(0, list.Count));
            list.Add(pos + HoverAboveFeet);
            lastSample = pos;
            return;
        }
    }

    private static bool IsRowdyCollider(Collider2D col)
    {
        if (rowdyColliders == null) return false;
        foreach (Collider2D c in rowdyColliders) if (c == col) return true;
        return false;
    }

}
