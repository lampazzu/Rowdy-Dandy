using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    public Transform[] patrolPoints;
    public float moveSpeed;
    public int patrolDestination;
    public Transform playerTransform;
    public Transform marchTarget;
    public bool isChasing;
    public bool isMarching;
    public float chaseDistance;

    [Header("Patrol")]
    [SerializeField] private bool isPatrolGround;

    [Header("Drowning & Aquatic Settings")]
    [SerializeField] private bool isDrownable;
    [SerializeField] private bool isOnlyAquatic;
    [SerializeField] private LayerMask waterLayer;

    [Header("Aquatic: Stay In The Pool & Leap (Only Aquatic)")]
    [Tooltip("Keep the body this far from the shore line while swimming.")]
    [SerializeField] private float shoreMargin = 0.6f;
    [Tooltip("Leap out of the water at Rowdy when he's on the shore or in the air above the water.")]
    [SerializeField] private bool enableAquaticLeap = true;
    [Tooltip("Rowdy must be this close (horizontally) for a leap.")]
    [SerializeField] private float leapTriggerRange = 4f;
    [Tooltip("How far onto the shore Rowdy can stand and still be hunted / leapt at.")]
    [SerializeField] private float leapShoreReach = 2.5f;
    [Tooltip("A leap may land at most this far past the shore line (then it hops straight back in).")]
    [SerializeField] private float leapShoreOvershoot = 1.5f;
    [SerializeField] private float leapMinHeight = 1.2f;
    [SerializeField] private float leapMaxHeight = 3f;
    [SerializeField] private float leapMaxSpeedX = 7f;
    [SerializeField] private float leapCooldown = 2.5f;
    [Tooltip("Rowdy counts as 'above the water' (worth leaping at) when his feet are this high over the surface.")]
    [SerializeField] private float leapWhenPlayerAbove = 1f;
    [SerializeField] private float hopBackHeight = 1f;

    [Header("Aquatic: Leap Tilt (dolphin arc)")]
    [SerializeField] private bool enableLeapTilt = true;
    [Tooltip("Keep it small: rotated pixel art gets jaggy fast.")]
    [SerializeField] private float maxLeapTilt = 20f;
    [SerializeField] private float leapTiltSpeed = 120f;
    [Tooltip("Snap the tilt to steps of this many degrees (0 = smooth).")]
    [SerializeField] private float leapTiltStep = 0f;

    private bool waterSpanReady;
    private float waterMinX;
    private float waterMaxX;
    private float waterSurfaceY;
    private bool isLeaping;
    private float leapStartTime;
    private float lastLeapTime = -999f;
    private float leapSettledTimer;
    private float leapTilt;
    private bool swimRight;
    private Collider2D bodyCollider;
    private Collider2D playerCollider;

    [Header("Obstacle & Wall Checks")]
    [SerializeField] private float wallCheckDistance = 0.8f;
    [SerializeField] private float obstacleCheckHeight = 0.5f;
    [SerializeField] private float headClearanceDistance = 1.5f;

    [Header("JumperMan")]
    [SerializeField] private bool isJumperMan;

    [Range(1, 10)]
    [SerializeField] private int jumpCapability = 5;

    [Range(1, 10)]
    [SerializeField] private int leapCapability = 5;

    [SerializeField] private float jumpCooldown = 1f;

    [Header("Airborne Animation")]
    [SerializeField] private bool hasFallAnimation;
    [SerializeField] private string jumpingUpAnimatorParameter = "jumpingUp";
    [SerializeField] private string fallingAnimatorParameter = "falling";

    private float lastJumpTime = -999f;

    private Animator animator;
    private Rigidbody2D rb;
    private bool isMoving;

    [SerializeField] private bool isPhasing;
    private string enemyLayer = "Enemy";
    private string ignorePlayerLayer = "IgnorePlayer";

    [SerializeField] private bool randomizeSpeed;
    [SerializeField] private float minSpeed = 1f;
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float groundCheckDistance = 1f;
    [SerializeField] private LayerMask groundLayer; // Used for Ground, Platforms, and Walls
    [SerializeField] private bool isGrounded;
    private bool wasGroundedLastFrame;
    [SerializeField] private AnimatorOverrideController groundedAnimatorOverrideController;
    [SerializeField] private AnimatorOverrideController airAnimatorOverrideController;
    [SerializeField] private bool usesOverride = false;

    // Dynamic stuck-against-obstacle tracker
    private float pushAgainstWallTimer = 0f;

    // =========================================================================
    // GAME FEEL & POLISH NUANCE TOGGLES (ALL OFF BY DEFAULT)
    // =========================================================================

    [Header("Smart Ground Offset")]
    [SerializeField] private float groundCheckOffset = 0.2f;

    [Header("Polish: Acceleration & Deceleration")]
    [SerializeField] private bool enableSmoothMovement = false;
    [SerializeField] private float acceleration = 25f;
    [SerializeField] private float deceleration = 30f;

    [Header("Polish: Squash & Stretch Routine")]
    [SerializeField] private bool enableLandingSquash = false;
    [SerializeField] private bool enableJumpTakeoffSquash = false;
    [SerializeField] private bool enableAirborneStretch = false;
    [SerializeField] private Vector3 landingSquashScale = new Vector3(1.25f, 0.75f, 1f);
    [SerializeField] private Vector3 jumpTakeoffScale = new Vector3(0.8f, 1.25f, 1f);
    [SerializeField] private float squashDuration = 0.15f;
    private Vector3 originalScale = Vector3.one;
    private bool isSquashing;

    [Header("Polish: Patrol Turn Hesitation")]
    [SerializeField] private bool enablePatrolHesitation = false;
    [SerializeField] private float patrolWaitTime = 1f;
    private float patrolWaitTimer;
    private bool isWaitingAtPatrolPoint;
    private bool hasWarnedMissingPatrolPoint;

    [Header("Polish: Target Detection Alert Pause")]
    [SerializeField] private bool enableAlertPause = false;
    [SerializeField] private float alertPauseDuration = 0.4f;
    [SerializeField] private string alertAnimatorTrigger = "Alert";
    private float alertTimer;

    [Header("Polish: Dynamic Variable Turn Rate")]
    [SerializeField] private bool enableSmoothFacing = false;
    [SerializeField] private float turnCooldown = 0.2f;
    private float lastTurnTime = -999f;
    private float currentFacingDirection = -1f;

    [Header("Polish: Drop & Fall Landing Recovery / Stun")]
    [SerializeField] private bool enableLandingStun = false;
    [SerializeField] private float minDropHeightForStun = 3f;
    [SerializeField] private float stunDuration = 1.2f;
    [SerializeField] private string landingStunAnimatorParameter = "isStunned";
    private bool isStunned;
    private float stunTimer;
    private float peakAirHeight;

    [Header("Polish: Wall Hop / Vaulting")]
    [SerializeField] private bool enableWallHop = true;
    [SerializeField] private float wallHopForceY = 6f;

    [Header("Polish: Edge Avoidance (Ledge Stop)")]
    [SerializeField] private bool enableEdgeAvoidance = false;
    [SerializeField] private float ledgeCheckDistance = 0.5f;

    [Header("Polish: Stuck Detection")]
    [SerializeField] private bool enableStuckDetection = false;
    [SerializeField] private float stuckCheckTime = 1f;
    [SerializeField] private float minStuckDistance = 0.05f;
    private float stuckTimer;
    private Vector2 lastStuckCheckPosition;

    [Header("Polish: Target Sight Line Check")]
    [SerializeField] private bool enableLineOfSightCheck = false;
    [SerializeField] private LayerMask lineOfSightObstacles;

    [Header("Polish: Dynamic Chase Timeout")]
    [SerializeField] private bool enableChaseTimeout = false;
    [SerializeField] private float loseTargetTime = 3f;
    private float timeOutOfRange;

    [Header("Chase: Rowdy On Another Floor")]
    [Tooltip("Walkers stop copying Rowdy's X when he's on a different floor: they walk as close as their floor allows (never off a ledge), stand there watching him, then give up and go back to patrolling. They also won't start a chase at him from another floor. JumperMen only do this when he's below (they still jump up to him). Flyers, sea creatures and marchers are unaffected.")]
    [SerializeField] private bool enableFloorAwareChase = true;
    [Tooltip("Rowdy's feet more than this above/below the enemy's feet = another floor.")]
    [SerializeField] private float floorHeightTolerance = 1.5f;
    [Tooltip("Seconds it stands watching Rowdy on another floor before giving up.")]
    [SerializeField] private float giveUpTime = 2.5f;
    [Tooltip("Debug: ticks on while it thinks Rowdy is on another floor.")]
    [SerializeField] private bool playerOnOtherFloor;
    private bool playerIsBelow;
    private float watchTimer;
    private PlayerMovement playerMovement;

    public enum SpacingMode { Auto, On, Off }

    [Header("Chase: Keep Attack Distance & Take Turns")]
    [Tooltip("Stop walking once Rowdy is inside its attack reach (from MeleeEnemy) instead of pushing into him, and take turns: only Crowd Limit of these (pause menu setting, default 4) press him at once, the rest hang back. Auto = on for Big Wolf, Transform Wolf and Werefast.")]
    [SerializeField] private SpacingMode keepAttackDistance = SpacingMode.Auto;
    [Tooltip("Stop when Rowdy is this fraction of the attack reach away (a bit inside it, so the attack check catches him).")]
    [SerializeField] private float stopAtReachFraction = 0.75f;
    [Tooltip("Ones waiting for a turn hold this much further out than the attack distance.")]
    [SerializeField] private float waitingDistance = 2.2f;
    [Tooltip("With others waiting, an attacker gives up its turn after this many seconds and backs off.")]
    [SerializeField] private float turnLength = 5f;
    [Tooltip("Debug: on while it's hanging back waiting for a turn.")]
    [SerializeField] private bool waitingForTurn;

    private static readonly List<EnemyMovement> attackers = new List<EnemyMovement>();
    private static readonly HashSet<EnemyMovement> waiters = new HashSet<EnemyMovement>();
    private bool usesSpacing;
    private MeleeEnemy melee;
    private EnemyHealth health;
    private float turnStartedAt;
    private float rejoinAfter;
    private float personalOffset; // spreads the waiting ones out a little

    [Header("Patrol: Turn Back When Blocked")]
    [Tooltip("Walking to a patrol point but not getting anywhere (a wall in the way, or hanging on a wall's side) for this long = give up on that point and head for the next one.")]
    [SerializeField] private float patrolBlockedTime = 0.6f;
    private float patrolBlockedTimer;
    private float patrolBlockStartX;

    [Header("Performance: Sleep When Far")]
    [Tooltip("Far from Rowdy and not chasing: stand still and skip the AI (raycasts etc.) until he comes closer.")]
    [SerializeField] private bool sleepWhenFar = true;
    [SerializeField] private float sleepDistance = 40f;
    private bool isSleeping;

    [Header("Polish: Dust Particle Effects")]
    [SerializeField] private bool enableDustParticles = false;
    [SerializeField] private ParticleSystem landingDustParticle;
    [SerializeField] private ParticleSystem jumpDustParticle;

    // -------------------------------------------------------------
    // JUMPER MAN DROP STATE
    // -------------------------------------------------------------

    private bool hasCommittedDropDirection;
    private float committedDropDirection;

    // Cached lookups (these used to run every frame: string layer lookups, and Animator.parameters allocates an array per call)
    private int enemyLayerIndex;
    private int ignorePlayerLayerIndex;
    private readonly HashSet<string> animatorParameters = new HashSet<string>();
    private RuntimeAnimatorController cachedController;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
        enemyLayerIndex = LayerMask.NameToLayer(enemyLayer);
        ignorePlayerLayerIndex = LayerMask.NameToLayer(ignorePlayerLayer);

        // Start out believing whatever way the sprite really faces (negative x scale = facing right, see GetFlippedScale).
        // It used to always assume "left": prefabs saved facing right (Gnoll Warrior) with smooth facing on then
        // chased Rowdy walking backwards until he switched sides.
        currentFacingDirection = transform.localScale.x < 0f ? 1f : -1f;

        // Extras only for enemies that physically move around. Plants (kinematic bodies, e.g. the BluePlant
        // launch effector) are left exactly as they were.
        bool movesPhysically = rb != null && rb.bodyType == RigidbodyType2D.Dynamic;
        if (movesPhysically)
        {
            // Water splash / drowning effects
            WaterSplashBody.AttachTo(gameObject, false, isDrownable);
            VegetationInteractor.AttachTo(gameObject, 0.8f);
            if (isDrownable && GetComponent<EnemyDrowning>() == null) gameObject.AddComponent<EnemyDrowning>();

            // Dead bodies: stop the AI, don't block Rowdy, rest on the ground, match the slope.
            // Not for flyers (Manta, Pelican...) or anything using effectors: their death clips handle it.
            bool isFlyer = rb.gravityScale < 0.5f;
            bool hasEffector = GetComponentInChildren<Effector2D>(true) != null;
            if (!isFlyer && !hasEffector && GetComponent<EnemyHealth>() != null && GetComponent<EnemyCorpse>() == null)
            {
                gameObject.AddComponent<EnemyCorpse>();
            }
        }

        GameObject player = GameObject.FindWithTag("Player");

        if (player != null)
        {
            playerTransform = player.transform;
        }

        bodyCollider = FindSolidCollider(gameObject);
        if (playerTransform != null) playerCollider = FindSolidCollider(playerTransform.gameObject);

        gameObject.layer = enemyLayerIndex;

        if (randomizeSpeed)
        {
            moveSpeed = Random.Range(minSpeed, maxSpeed);
        }

        if (isDrownable)
        {
            SetupDrowningCollisions();
        }

        lastStuckCheckPosition = transform.position;

        melee = GetComponent<MeleeEnemy>();
        health = GetComponent<EnemyHealth>();
        personalOffset = Random.Range(0f, 1.2f);
        if (keepAttackDistance == SpacingMode.Auto)
        {
            EnemyCatalog.Entry entry = health != null ? EnemyCatalog.Identify(health) : null;
            usesSpacing = entry != null && (entry.id == "bigwolf" || entry.id == "transformwolf" || entry.id == "werefast");
        }
        else usesSpacing = keepAttackDistance == SpacingMode.On;
        usesSpacing &= melee != null && !isOnlyAquatic;
    }

    private void OnDisable() => LeaveTurnQueue();
    private void OnDestroy() => LeaveTurnQueue();

    private void LeaveTurnQueue()
    {
        attackers.Remove(this);
        waiters.Remove(this);
        waitingForTurn = false;
    }

    // Keep-distance + take-turns chase. Returns true when it moved the enemy itself this frame.
    private bool HandleSpacing(Vector2 toTarget)
    {
        if (health != null && health.enemydead) { LeaveTurnQueue(); return false; }

        float dist = Mathf.Abs(toTarget.x);
        float stopAt = Mathf.Max(0.35f, melee.AttackReach * stopAtReachFraction);
        float holdAt = stopAt + waitingDistance + personalOffset;
        int limit = GameSettings.CrowdLimit;
        attackers.RemoveAll(a => a == null || !a.isActiveAndEnabled);
        waiters.RemoveWhere(w => w == null || !w.isActiveAndEnabled);

        bool near = dist < holdAt + 1.5f;
        bool hasTurn = attackers.Contains(this);
        if (!near)
        {
            if (hasTurn) attackers.Remove(this);
            waiters.Remove(this);
            waitingForTurn = false;
            return false; // far away: normal chase
        }

        if (!hasTurn && (limit <= 0 || attackers.Count < limit) && Time.time >= rejoinAfter)
        {
            attackers.Add(this);
            turnStartedAt = Time.time;
            hasTurn = true;
        }
        // Been at it a while and others are waiting: step back and let one in
        if (hasTurn && limit > 0 && waiters.Count > 0 && Time.time - turnStartedAt > turnLength)
        {
            attackers.Remove(this);
            hasTurn = false;
            rejoinAfter = Time.time + 2f;
        }

        waitingForTurn = !hasTurn;
        if (waitingForTurn) waiters.Add(this); else waiters.Remove(this);

        float towards = Mathf.Sign(toTarget.x);
        float want = hasTurn ? stopAt : holdAt;
        float moveDir;
        if (dist > want + 0.25f)
        {
            if (hasTurn) return false; // still closing in: normal chase (jumps, wall hops...)
            moveDir = towards;
        }
        else if (!hasTurn && dist < want - 0.5f) moveDir = -towards; // too close while waiting: back off
        else moveDir = 0f;

        if (moveDir != 0f && IsAtEdge(moveDir)) moveDir = 0f; // never back off a ledge

        FlipSprite(towards); // always facing Rowdy, even walking backwards
        ApplyHorizontalVelocity(moveDir * moveSpeed * (moveDir == towards ? 1f : 0.7f));
        bool walking = moveDir != 0f;
        SetAnimatorBool("moving", walking);
        isMoving = walking;
        pushAgainstWallTimer = 0f;
        return true;
    }

    // Water / AntiEnemy colliders per water mask, found once per scene instead of searching every collider in the
    // scene for every drownable enemy (the wave spawner makes new ones all the time)
    private static readonly Dictionary<int, List<Collider2D>> drowningCollidersByMask = new Dictionary<int, List<Collider2D>>();
    private static int drowningCacheScene = -1;

    private List<Collider2D> GetDrowningColliders()
    {
        int sceneHandle = gameObject.scene.handle;
        if (sceneHandle != drowningCacheScene)
        {
            drowningCollidersByMask.Clear();
            drowningCacheScene = sceneHandle;
        }

        if (!drowningCollidersByMask.TryGetValue(waterLayer.value, out List<Collider2D> list))
        {
            list = new List<Collider2D>();
            foreach (Collider2D col in FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (col != null && IsDrowningLayer(col.gameObject.layer)) list.Add(col);
            }
            drowningCollidersByMask[waterLayer.value] = list;
        }
        return list;
    }

    private void SetupDrowningCollisions()
    {
        Collider2D[] enemyColliders = GetComponentsInChildren<Collider2D>(true);
        if (enemyColliders.Length == 0) return;

        foreach (Collider2D otherCollider in GetDrowningColliders())
        {
            if (otherCollider == null) continue;
            bool isOwnCollider = false;

            foreach (Collider2D enemyCollider in enemyColliders)
            {
                if (otherCollider == enemyCollider)
                {
                    isOwnCollider = true;
                    break;
                }
            }

            if (isOwnCollider || !IsDrowningLayer(otherCollider.gameObject.layer))
                continue;

            foreach (Collider2D enemyCollider in enemyColliders)
            {
                if (enemyCollider == null) continue;
                Physics2D.IgnoreCollision(enemyCollider, otherCollider, true);
            }
        }
    }

    // Used by EnemyCorpse so a drowning corpse keeps sinking through water like the living enemy did
    public bool PassesThroughLayer(int layer) => isDrownable && IsDrowningLayer(layer);

    // Water this enemy drowns in (its own Water Layer setting plus the 'Water' layer; not AntiEnemy walls)
    public int DrowningWaterMask
    {
        get
        {
            int mask = waterLayer.value;
            int water = LayerMask.NameToLayer("Water");
            if (water >= 0) mask |= 1 << water;
            return mask;
        }
    }

    private bool IsDrowningLayer(int layer)
    {
        if (waterLayer.value != 0 && (waterLayer.value & (1 << layer)) != 0) return true;
        if (layer == LayerMask.NameToLayer("Water")) return true;
        if (layer == LayerMask.NameToLayer("AntiEnemy")) return true;

        return false;
    }

    private void Update()
    {
        int layer = isPhasing ? ignorePlayerLayerIndex : enemyLayerIndex;
        if (gameObject.layer != layer) gameObject.layer = layer;

        if (UpdateSleep()) return;

        GroundCheck();

        // Stun State Handler
        if (enableLandingStun && isStunned)
        {
            HandleStunState();
            return;
        }

        // Alert Reaction Pause
        if (enableAlertPause && alertTimer > 0f)
        {
            alertTimer -= Time.deltaTime;
            ApplyHorizontalVelocity(0f);
            SetAnimatorBool("moving", false);
            return;
        }

        UpdateAirborneAnimation();
        UpdateAirborneStretch();

        if (usesOverride)
        {
            if (isGrounded)
            {
                if (animator.runtimeAnimatorController != groundedAnimatorOverrideController)
                {
                    animator.runtimeAnimatorController = groundedAnimatorOverrideController;
                }
            }
            else
            {
                if (animator.runtimeAnimatorController != airAnimatorOverrideController)
                {
                    animator.runtimeAnimatorController = airAnimatorOverrideController;
                }
            }
        }

        Transform target = isMarching && marchTarget != null ? marchTarget : playerTransform;

        UpdateToggledTargetChecks(target);

        // Sea creatures have their own swim / leap logic and never walk on land
        if (isOnlyAquatic)
        {
            UpdateAquatic();
            return;
        }

        UpdatePlayerFloor();

        if (isChasing && !isMarching && ShouldWatchPlayer)
        {
            WatchPlayerOnOtherFloor();
        }
        else if (isChasing || isMarching)
        {
            watchTimer = 0f;
            ChaseTarget(target);
        }
        else
        {
            bool canChasePlayer = playerTransform != null &&
                !ShouldWatchPlayer &&
                Vector2.Distance(transform.position, playerTransform.position) < chaseDistance &&
                HasLineOfSightToTarget(playerTransform);

            if (canChasePlayer)
            {
                if (enableAlertPause && !isChasing)
                {
                    alertTimer = alertPauseDuration;
                    if (animator != null && AnimatorHasParameter(alertAnimatorTrigger))
                    {
                        animator.SetTrigger(alertAnimatorTrigger);
                    }
                }

                isChasing = true;
                timeOutOfRange = 0f;
            }
            else
            {
                Patrol();
            }
        }

        CheckStuckStatus();
    }

    // Far from Rowdy, not chasing / marching / mid-leap: stand still and skip the rest of the AI this frame
    private bool UpdateSleep()
    {
        bool far = sleepWhenFar && playerTransform != null && !isChasing && !isMarching && !isLeaping &&
                   Mathf.Abs(playerTransform.position.x - transform.position.x) > sleepDistance;

        if (far)
        {
            if (!isSleeping)
            {
                isSleeping = true;
                if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) ApplyHorizontalVelocity(0f);
                SetAnimatorBool("moving", false);
                isMoving = false;
            }
            return true;
        }

        isSleeping = false;
        return false;
    }

    private static Collider2D FindSolidCollider(GameObject target)
    {
        foreach (Collider2D col in target.GetComponents<Collider2D>())
        {
            if (!col.isTrigger) return col;
        }
        return target.GetComponent<Collider2D>();
    }

    // =========================================================================
    // AQUATIC (Only Aquatic): swims inside its own stretch of water and never walks on land.
    // It can leap out like a dolphin to snap at Rowdy (on the shore or jumping over the water),
    // and always falls / hops straight back in. The water is found by raycasting down on the
    // Water Layer (the water tilemap uses an outline collider, so point-overlap checks don't work).
    // =========================================================================

    private void UpdateAquatic()
    {
        if (!waterSpanReady) SetupWaterSpan();

        if (isLeaping)
        {
            UpdateLeap();
        }
        else if (!IsOverWater(transform.position.x))
        {
            // Knocked / landed on the shore: never walk, hop back in as soon as it's down
            isChasing = false;
            SetAnimatorBool("moving", false);
            if (Mathf.Abs(rb.linearVelocity.y) < 0.05f) HopBackToWater();
        }
        else
        {
            Swim();
        }

        UpdateLeapTilt();
    }

    // Finds the left/right shore lines of the water under the enemy's start position
    private void SetupWaterSpan()
    {
        waterSpanReady = true;
        int mask = DrowningWaterMask;
        float startX = transform.position.x;

        if (ProbeWater(startX, mask, out waterSurfaceY))
        {
            const float step = 0.25f;
            const float maxScan = 80f;
            float x = startX;
            while (startX - x < maxScan && ProbeWater(x - step, mask, out _)) x -= step;
            waterMinX = x;
            x = startX;
            while (x - startX < maxScan && ProbeWater(x + step, mask, out _)) x += step;
            waterMaxX = x;
            Debug.Log($"{name}: swims between x {waterMinX:F2} and {waterMaxX:F2} (surface y {waterSurfaceY:F2})", this);
            return;
        }

        // No water found: keep to the patrol points so it at least never wanders off
        Debug.LogWarning($"EnemyMovement on '{name}' is Only Aquatic but found no water under it (check Water Layer). It will stay between its patrol points.", this);
        waterMinX = waterMaxX = startX;
        if (patrolPoints != null)
        {
            foreach (Transform p in patrolPoints)
            {
                if (p == null) continue;
                waterMinX = Mathf.Min(waterMinX, p.position.x);
                waterMaxX = Mathf.Max(waterMaxX, p.position.x);
            }
        }
        waterSurfaceY = FeetY;
    }

    // Water at this x, and not buried under the beach (the water tilemap runs on under the sand in places)
    private bool ProbeWater(float x, int mask, out float surfaceY)
    {
        Vector2 origin = new Vector2(x, transform.position.y + 2f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 4f, mask);
        surfaceY = hit.collider != null ? hit.point.y : 0f;
        if (hit.collider == null) return false;

        int groundMask = groundLayer.value != 0 ? groundLayer.value : LayerMask.GetMask("groundLayer");
        RaycastHit2D ground = Physics2D.Raycast(origin, Vector2.down, 4f, groundMask);
        return ground.collider == null || ground.point.y < hit.point.y - 0.05f;
    }

    private float FeetY => bodyCollider != null ? bodyCollider.bounds.min.y : transform.position.y;

    private bool IsOverWater(float x) => x >= waterMinX && x <= waterMaxX;

    private bool IsFloating()
    {
        return IsOverWater(transform.position.x) && FeetY <= waterSurfaceY + 0.15f && Mathf.Abs(rb.linearVelocity.y) < 0.5f;
    }

    private bool CanSeePlayerFromWater()
    {
        if (playerTransform == null) return false;

        Vector2 p = playerTransform.position;
        if (p.x < waterMinX - leapShoreReach || p.x > waterMaxX + leapShoreReach) return false; // too far inland
        if (Mathf.Abs(p.x - transform.position.x) > chaseDistance) return false;
        if (p.y > waterSurfaceY + leapMaxHeight + 2f || p.y < waterSurfaceY - 2f) return false;

        return HasLineOfSightToTarget(playerTransform);
    }

    private void Swim()
    {
        float x = transform.position.x;
        float minX = waterMinX + shoreMargin;
        float maxX = waterMaxX - shoreMargin;
        if (minX > maxX) minX = maxX = (waterMinX + waterMaxX) * 0.5f;

        bool seesPlayer = CanSeePlayerFromWater();
        isChasing = seesPlayer;

        float targetX;
        if (seesPlayer)
        {
            if (TryLeapAtPlayer()) return;
            targetX = playerTransform.position.x;
        }
        else
        {
            targetX = GetAquaticPatrolX(minX, maxX);
        }
        targetX = Mathf.Clamp(targetX, minX, maxX);

        float dx = targetX - x;
        if (Mathf.Abs(dx) < 0.1f)
        {
            // At the shore line with Rowdy out on land: wait there facing him (until the next leap), don't push into the bank
            ApplyHorizontalVelocity(0f);
            SetAnimatorBool("moving", false);
            isMoving = false;
            if (seesPlayer) FlipSprite(playerTransform.position.x - x);
        }
        else
        {
            float direction = Mathf.Sign(dx);
            ApplyHorizontalVelocity(direction * moveSpeed);
            FlipSprite(direction);
            SetAnimatorBool("moving", true);
            isMoving = true;
        }

        // Hard leash: nothing (patrol, chase, the hurt clip's backwards push) moves it past the shore margin
        Vector2 v = rb.linearVelocity;
        if ((x <= minX && v.x < 0f) || (x >= maxX && v.x > 0f))
        {
            rb.linearVelocity = new Vector2(0f, v.y);
        }
    }

    // Patrol points are clamped into the water, so a point placed on the beach can't drag it onto land.
    // No patrol points = cruise the whole pool.
    private float GetAquaticPatrolX(float minX, float maxX)
    {
        float x = transform.position.x;
        bool hasPoints = patrolPoints != null && patrolPoints.Length > 0;
        if (hasPoints && patrolDestination >= patrolPoints.Length) patrolDestination = 0;
        Transform point = hasPoints ? GetValidPatrolTarget() : null;

        if (point == null)
        {
            float edge = swimRight ? maxX : minX;
            if (Mathf.Abs(edge - x) < 0.3f) swimRight = !swimRight;
            return swimRight ? maxX : minX;
        }

        float targetX = Mathf.Clamp(point.position.x, minX, maxX);
        if (Mathf.Abs(targetX - x) < 0.3f)
        {
            patrolDestination = (patrolDestination + 1) % patrolPoints.Length;
            point = GetValidPatrolTarget();
            if (point != null) targetX = Mathf.Clamp(point.position.x, minX, maxX);
        }
        return targetX;
    }

    private bool TryLeapAtPlayer()
    {
        // moveSpeed <= 0 while the hurt clip plays
        if (!enableAquaticLeap || moveSpeed <= 0f || Time.time < lastLeapTime + leapCooldown) return false;
        if (!IsFloating()) return false;

        Vector2 target = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)playerTransform.position;
        float playerFeetY = playerCollider != null ? playerCollider.bounds.min.y : target.y;

        if (Mathf.Abs(target.x - transform.position.x) > leapTriggerRange) return false;

        // Rowdy surfing on the water at its level: just swim at him and bite
        bool playerOnShore = !IsOverWater(target.x);
        bool playerAbove = playerFeetY > waterSurfaceY + leapWhenPlayerAbove;
        if (!playerOnShore && !playerAbove) return false;

        float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        if (gravity < 0.01f) return false;

        float x = transform.position.x;
        float rise = Mathf.Clamp(target.y - transform.position.y + 0.3f, leapMinHeight, leapMaxHeight);
        float vy = Mathf.Sqrt(2f * gravity * rise);
        float timeToTop = vy / gravity;

        // Reach him at the top of the arc, but never land further than leapShoreOvershoot past the shore
        float vx = (target.x - x) / timeToTop;
        float airTime = 2f * timeToTop;
        vx = Mathf.Clamp(vx, (waterMinX - leapShoreOvershoot - x) / airTime, (waterMaxX + leapShoreOvershoot - x) / airTime);
        vx = Mathf.Clamp(vx, -leapMaxSpeedX, leapMaxSpeedX);

        StartLeap(new Vector2(vx, vy));
        if (Mathf.Abs(vx) < 0.01f) FlipSprite(target.x - x);
        return true;
    }

    private void HopBackToWater()
    {
        float x = transform.position.x;
        float inward = shoreMargin + 0.5f;
        float targetX = x < waterMinX ? waterMinX + inward : waterMaxX - inward;
        targetX = Mathf.Clamp(targetX, waterMinX, waterMaxX);

        float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        if (gravity < 0.01f)
        {
            // Floaty body: just slide back in
            ApplyHorizontalVelocity(Mathf.Sign(targetX - x) * Mathf.Abs(moveSpeed));
            FlipSprite(targetX - x);
            return;
        }

        float vy = Mathf.Sqrt(2f * gravity * hopBackHeight);
        float drop = Mathf.Max(0f, FeetY - waterSurfaceY);
        float airTime = (vy + Mathf.Sqrt(vy * vy + 2f * gravity * drop)) / gravity;
        float vx = Mathf.Clamp((targetX - x) / airTime, -leapMaxSpeedX, leapMaxSpeedX);

        StartLeap(new Vector2(vx, vy));
    }

    private void StartLeap(Vector2 velocity)
    {
        isLeaping = true;
        leapStartTime = Time.time;
        lastLeapTime = Time.time;
        leapSettledTimer = 0f;
        rb.linearVelocity = velocity;
        if (Mathf.Abs(velocity.x) > 0.01f) FlipSprite(velocity.x);
        SetAnimatorBool("moving", true);
    }

    private void UpdateLeap()
    {
        // Ballistic until it comes to rest on something (the apex passes too fast to count as resting)
        leapSettledTimer = Mathf.Abs(rb.linearVelocity.y) < 0.05f ? leapSettledTimer + Time.deltaTime : 0f;
        float airTime = Time.time - leapStartTime;

        if ((airTime > 0.2f && leapSettledTimer > 0.08f) || airTime > 4f)
        {
            isLeaping = false;
            if (!IsOverWater(transform.position.x))
            {
                HopBackToWater(); // landed on the beach
            }
            else
            {
                ApplyHorizontalVelocity(0f);
            }
        }
    }

    // Nose up on the way out, nose down on the way back, levelling off just before touching the water
    private void UpdateLeapTilt()
    {
        if (!enableLeapTilt)
        {
            if (leapTilt != 0f) { leapTilt = 0f; rb.rotation = 0f; }
            return;
        }

        float targetTilt = 0f;
        if (isLeaping)
        {
            Vector2 v = rb.linearVelocity;
            float pitch = Mathf.Atan2(v.y, Mathf.Abs(v.x) + 2f) * Mathf.Rad2Deg; // +2 keeps near-vertical leaps subtle

            float height = FeetY - waterSurfaceY;
            if (v.y < 0f && height < 0.5f) pitch *= Mathf.Clamp01(height / 0.5f);

            targetTilt = Mathf.Clamp(pitch, -maxLeapTilt, maxLeapTilt) * currentFacingDirection;
        }

        leapTilt = Mathf.MoveTowards(leapTilt, targetTilt, leapTiltSpeed * Time.deltaTime);
        float shown = leapTiltStep > 0f ? Mathf.Round(leapTilt / leapTiltStep) * leapTiltStep : leapTilt;
        if (!Mathf.Approximately(rb.rotation, shown)) rb.rotation = shown;
    }

    // =========================================================================
    // VELOCITY & ACCELERATION WRAPPER
    // =========================================================================

    private void ApplyHorizontalVelocity(float targetXVelocity)
    {
        if (enableSmoothMovement)
        {
            float accelRate = (Mathf.Abs(targetXVelocity) > 0.01f) ? acceleration : deceleration;
            float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetXVelocity, accelRate * Time.deltaTime);
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(targetXVelocity, rb.linearVelocity.y);
        }
    }

    // =========================================================================
    // POLISH NUANCE HANDLERS
    // =========================================================================

    private void HandleStunState()
    {
        ApplyHorizontalVelocity(0f);
        SetAnimatorBool("moving", false);
        SetAnimatorBool(landingStunAnimatorParameter, true);

        stunTimer -= Time.deltaTime;
        if (stunTimer <= 0f)
        {
            isStunned = false;
            SetAnimatorBool(landingStunAnimatorParameter, false);
        }
    }

    private void TriggerLandingStun()
    {
        isStunned = true;
        stunTimer = stunDuration;
        ApplyHorizontalVelocity(0f);
        SetAnimatorBool("moving", false);
        SetAnimatorBool(landingStunAnimatorParameter, true);
    }

    private IEnumerator SquashAndStretchRoutine(Vector3 targetScale)
    {
        isSquashing = true;
        float elapsed = 0f;

        Vector3 adjustedScale = new Vector3(
            targetScale.x * Mathf.Sign(originalScale.x) * (currentFacingDirection > 0 ? -1f : 1f),
            targetScale.y * originalScale.y,
            originalScale.z
        );

        while (elapsed < squashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / squashDuration;
            transform.localScale = Vector3.Lerp(adjustedScale, GetFlippedScale(currentFacingDirection), t);
            yield return null;
        }

        transform.localScale = GetFlippedScale(currentFacingDirection);
        isSquashing = false;
    }

    private void UpdateAirborneStretch()
    {
        if (!enableAirborneStretch || isGrounded || isSquashing) return;

        if (rb.linearVelocity.y > 0.1f)
        {
            transform.localScale = new Vector3(
                GetFlippedScale(currentFacingDirection).x * jumpTakeoffScale.x,
                originalScale.y * jumpTakeoffScale.y,
                originalScale.z
            );
        }
        else
        {
            transform.localScale = GetFlippedScale(currentFacingDirection);
        }
    }

    private Vector3 GetFlippedScale(float direction)
    {
        float absX = Mathf.Abs(originalScale.x);
        return new Vector3(direction > 0 ? -absX : absX, originalScale.y, originalScale.z);
    }

    private void UpdateToggledTargetChecks(Transform target)
    {
        if (!enableChaseTimeout || !isChasing || target == null) return;

        float distance = Vector2.Distance(transform.position, target.position);
        if (distance > chaseDistance || !HasLineOfSightToTarget(target))
        {
            timeOutOfRange += Time.deltaTime;
            if (timeOutOfRange >= loseTargetTime)
            {
                isChasing = false;
                timeOutOfRange = 0f;
            }
        }
        else
        {
            timeOutOfRange = 0f;
        }
    }

    private bool HasLineOfSightToTarget(Transform target)
    {
        if (!enableLineOfSightCheck || target == null) return true;

        Vector2 direction = target.position - transform.position;
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            direction.normalized,
            direction.magnitude,
            lineOfSightObstacles
        );

        return hit.collider == null;
    }

    private bool IsAtEdge(float direction)
    {
        if (!enableEdgeAvoidance || !isGrounded || direction == 0f) return false;

        Vector2 rayOrigin = (Vector2)transform.position + new Vector2(Mathf.Sign(direction) * ledgeCheckDistance, 0f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, groundCheckDistance, groundLayer);

        return hit.collider == null;
    }

    private bool IsObstacleInFront(float direction)
    {
        if (direction == 0f) return false;

        Vector2 boxSize = new Vector2(0.5f, 3.5f);
        Vector2 boxCenter = (Vector2)transform.position + new Vector2(direction * 0.4f, 1.5f);

        RaycastHit2D hit = Physics2D.BoxCast(boxCenter, boxSize, 0f, Vector2.right * direction, 0.3f, groundLayer);

        return hit.collider != null;
    }

    private bool HasHeadClearance()
    {
        RaycastHit2D ceilingHit = Physics2D.Raycast(transform.position, Vector2.up, headClearanceDistance, groundLayer);
        return ceilingHit.collider == null;
    }

    private void CheckStuckStatus()
    {
        if (!enableStuckDetection || (!isChasing && !isMarching)) return;

        stuckTimer += Time.deltaTime;
        if (stuckTimer >= stuckCheckTime)
        {
            float distanceMoved = Vector2.Distance(transform.position, lastStuckCheckPosition);
            if (distanceMoved < minStuckDistance && isGrounded)
            {
                if (!isMarching && !isJumperMan)
                {
                    isChasing = false;
                }
            }

            lastStuckCheckPosition = transform.position;
            stuckTimer = 0f;
        }
    }

    private void UpdateAirborneAnimation()
    {
        if (!hasFallAnimation) return;

        if (isGrounded)
        {
            SetAnimatorBool(jumpingUpAnimatorParameter, false);
            SetAnimatorBool(fallingAnimatorParameter, false);
            return;
        }

        float verticalVelocity = rb.linearVelocity.y;
        SetAnimatorBool(jumpingUpAnimatorParameter, verticalVelocity > 0.05f);
        SetAnimatorBool(fallingAnimatorParameter, verticalVelocity < -0.05f);
    }

    // -------------------------------------------------------------
    // CHASE
    // -------------------------------------------------------------

    private void ChaseTarget(Transform target)
    {
        if (target == null) return;

        Vector2 directionToTarget = target.position - transform.position;
        float horizontalDirection = Mathf.Sign(directionToTarget.x);

        if (enableEdgeAvoidance && !isJumperMan && IsAtEdge(horizontalDirection))
        {
            ApplyHorizontalVelocity(0f);
            SetAnimatorBool("moving", false);
            isMoving = false;
            return;
        }

        // Pushy melee enemies stop at attack range and take turns (same floor, on the ground only)
        if (usesSpacing && target == playerTransform && isGrounded && Mathf.Abs(directionToTarget.y) < 1.2f)
        {
            if (HandleSpacing(directionToTarget)) return;
        }
        else if (usesSpacing && waitingForTurn)
        {
            LeaveTurnQueue();
        }

        if (isJumperMan && isGrounded && directionToTarget.y < -0.5f)
        {
            if (TryMoveToLowerPlatform(target)) return;
        }
        else
        {
            ClearDropCommitment();
        }

        bool isBlocked = IsObstacleInFront(horizontalDirection);

        if (isGrounded && Mathf.Abs(rb.linearVelocity.x) < 0.2f)
        {
            pushAgainstWallTimer += Time.deltaTime;
        }
        else
        {
            pushAgainstWallTimer = 0f;
        }

        bool isStuckAgainstObstacle = pushAgainstWallTimer > 0.1f;

        if (isGrounded && (isBlocked || isStuckAgainstObstacle))
        {
            if (HasHeadClearance() && Time.time >= lastJumpTime + jumpCooldown)
            {
                pushAgainstWallTimer = 0f;

                if (isJumperMan)
                {
                    if (TryJumpToPlatform(target)) return;

                    PerformJump(target.position, new Vector2(horizontalDirection * moveSpeed * 1.3f, 9f));
                    return;
                }

                if (enableWallHop)
                {
                    rb.linearVelocity = new Vector2(horizontalDirection * moveSpeed, wallHopForceY);
                    return;
                }
            }
        }

        if (isJumperMan && CanInitiateJump() && Time.time >= lastJumpTime + jumpCooldown && directionToTarget.y > 0.5f)
        {
            if (TryJumpToPlatform(target)) return;
        }

        Vector2 movementDirection = directionToTarget.normalized;
        ApplyHorizontalVelocity(movementDirection.x * moveSpeed);

        FlipSprite(movementDirection.x);
        SetAnimatorBool("moving", true);
        isMoving = true;
    }

    private bool UsesFloorAwareChase =>
        enableFloorAwareChase && !isOnlyAquatic && rb != null && rb.gravityScale >= 0.5f;

    // JumperMen still jump up to Rowdy; they only stop and watch when he's below them
    private bool ShouldWatchPlayer => playerOnOtherFloor && (!isJumperMan || playerIsBelow);

    // Mid-jump (Rowdy's or the enemy's own wall hop) keeps the last answer, so a jump on the same floor never counts
    private void UpdatePlayerFloor()
    {
        if (!UsesFloorAwareChase || playerTransform == null)
        {
            playerOnOtherFloor = false;
            return;
        }

        if (Mathf.Abs(rb.linearVelocity.y) > 0.1f) return;

        if (playerMovement == null) playerMovement = playerTransform.GetComponent<PlayerMovement>();
        if (playerMovement != null && !playerMovement.IsGrounded) return;

        float playerFeetY = playerCollider != null ? playerCollider.bounds.min.y : playerTransform.position.y;
        playerOnOtherFloor = Mathf.Abs(playerFeetY - FeetY) > floorHeightTolerance;
        playerIsBelow = playerFeetY < FeetY;
    }

    // Probes from the body's feet (not the pivot), so it works whatever the sprite's pivot / Ground Check Distance is
    private bool HasFloorAhead(float direction)
    {
        float aheadX = bodyCollider != null
            ? (direction > 0f ? bodyCollider.bounds.max.x : bodyCollider.bounds.min.x) + direction * 0.2f
            : transform.position.x + direction * ledgeCheckDistance;
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(aheadX, FeetY + 0.2f), Vector2.down, 0.6f, groundLayer);
        return hit.collider != null;
    }

    // Rowdy is above/below: get as close as this floor allows, then stand and watch him instead of running on the spot
    private void WatchPlayerOnOtherFloor()
    {
        float dx = playerTransform.position.x - transform.position.x;
        float direction = Mathf.Sign(dx);

        bool canGetCloser = Mathf.Abs(dx) > 0.5f && HasFloorAhead(direction) && !IsObstacleInFront(direction);

        if (canGetCloser)
        {
            ApplyHorizontalVelocity(direction * moveSpeed);
            FlipSprite(direction);
            SetAnimatorBool("moving", true);
            isMoving = true;
            return;
        }

        ApplyHorizontalVelocity(0f);
        FlipSprite(dx);
        SetAnimatorBool("moving", false);
        isMoving = false;

        watchTimer += Time.deltaTime;
        if (watchTimer >= giveUpTime)
        {
            isChasing = false;
            watchTimer = 0f;
        }
    }

    private bool CanInitiateJump()
    {
        if (!isJumperMan || !isGrounded) return false;
        if (Mathf.Abs(rb.linearVelocity.y) > 0.1f) return false;

        return true;
    }

    private bool TryMoveToLowerPlatform(Transform target)
    {
        if (target == null) return false;

        float directionToPlayerX = Mathf.Sign(target.position.x - transform.position.x);

        ApplyHorizontalVelocity(directionToPlayerX * moveSpeed);
        FlipSprite(directionToPlayerX);
        SetAnimatorBool("moving", true);
        isMoving = true;

        return true;
    }

    private void ClearDropCommitment()
    {
        hasCommittedDropDirection = false;
        committedDropDirection = 0f;
    }

    private bool TryJumpToPlatform(Transform target)
    {
        if (!CanInitiateJump()) return false;

        Vector2? platform = FindBestReachablePlatform(target);
        if (!platform.HasValue) return false;

        Vector2 jumpVelocity;
        if (!CalculateJumpTrajectory(platform.Value, out jumpVelocity)) return false;

        PerformJump(platform.Value, jumpVelocity);
        return true;
    }

    private Vector2? FindBestReachablePlatform(Transform target)
    {
        float searchDistance = GetLeapDistanceCapability();
        float step = Mathf.Max(0.1f, searchDistance / 15f);
        int steps = Mathf.CeilToInt(searchDistance / step);

        Vector2 bestPlatform = Vector2.zero;
        float bestScore = float.MinValue;
        bool found = false;

        for (int i = 1; i <= steps; i++)
        {
            float horizontalOffset = i * step;
            CheckPlatformCandidate(transform.position.x + horizontalOffset, target, ref bestPlatform, ref bestScore, ref found);
            CheckPlatformCandidate(transform.position.x - horizontalOffset, target, ref bestPlatform, ref bestScore, ref found);
        }

        return found ? bestPlatform : null;
    }

    private void CheckPlatformCandidate(float checkX, Transform target, ref Vector2 bestPlatform, ref float bestScore, ref bool found)
    {
        float searchHeight = GetJumpHeightCapability() * 2f;
        Vector2 origin = new Vector2(checkX, transform.position.y + searchHeight);

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, searchHeight * 2f, groundLayer);

        if (hit.collider == null) return;

        float verticalDifference = hit.point.y - transform.position.y;
        if (Mathf.Abs(verticalDifference) < GetMinimumFloorDifference() || verticalDifference > GetJumpHeightCapability()) return;

        Vector2 candidate = hit.point;
        Vector2 calculatedVelocity;

        if (!CalculateJumpTrajectory(candidate, out calculatedVelocity)) return;

        float targetDistance = Vector2.Distance(target.position, candidate);
        float score = 1f / (1f + targetDistance);

        if (!found || score > bestScore)
        {
            bestScore = score;
            bestPlatform = candidate;
            found = true;
        }
    }

    private bool CalculateJumpTrajectory(Vector2 target, out Vector2 velocity)
    {
        velocity = Vector2.zero;
        float gravity = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        if (gravity <= 0.01f) return false;

        float horizontalDistance = target.x - transform.position.x;
        float verticalDistance = target.y - transform.position.y;

        if (Mathf.Abs(horizontalDistance) > GetLeapDistanceCapability() || verticalDistance > GetJumpHeightCapability())
            return false;

        bool found = false;
        Vector2 bestVelocity = Vector2.zero;
        float bestScore = float.MaxValue;

        for (float time = 0.15f; time <= 3f; time += 0.025f)
        {
            float xVelocity = horizontalDistance / time;
            if (Mathf.Abs(xVelocity) > GetMaximumHorizontalJumpVelocity()) continue;

            float yVelocity = (verticalDistance + 0.5f * gravity * time * time) / time;
            if (yVelocity <= 0f) continue;

            float timeToApex = yVelocity / gravity;
            float apexHeight = (yVelocity * timeToApex) - (0.5f * gravity * timeToApex * timeToApex);

            if (apexHeight > GetJumpHeightCapability()) continue;

            float score = Mathf.Abs(xVelocity) + (time * 0.15f);
            if (!found || score < bestScore)
            {
                found = true;
                bestScore = score;
                bestVelocity = new Vector2(xVelocity, yVelocity);
            }
        }

        if (!found) return false;

        velocity = bestVelocity;
        return true;
    }

    private void PerformJump(Vector2 target, Vector2 velocity)
    {
        if (!CanInitiateJump()) return;

        lastJumpTime = Time.time;
        ClearDropCommitment();

        if (enableJumpTakeoffSquash && !isSquashing)
        {
            StartCoroutine(SquashAndStretchRoutine(jumpTakeoffScale));
        }

        if (enableDustParticles && jumpDustParticle != null)
        {
            jumpDustParticle.Play();
        }

        rb.linearVelocity = velocity;

        if (velocity.x != 0f) FlipSprite(Mathf.Sign(velocity.x));

        SetAnimatorBool("moving", true);
        isMoving = true;
    }

    private float GetJumpHeightCapability()
    {
        return Mathf.Lerp(1.5f, 6f, Mathf.InverseLerp(1f, 10f, jumpCapability));
    }

    private float GetMaximumHorizontalJumpVelocity()
    {
        return Mathf.Lerp(2f, 16f, Mathf.InverseLerp(1f, 10f, leapCapability));
    }

    private float GetLeapDistanceCapability()
    {
        return GetMaximumHorizontalJumpVelocity() * 1.5f;
    }

    private float GetMinimumFloorDifference() => 0.25f;

    private void Patrol()
    {
        if (patrolPoints != null && patrolPoints.Length > 0 && patrolDestination < patrolPoints.Length)
        {
            if (isWaitingAtPatrolPoint)
            {
                ApplyHorizontalVelocity(0f);
                SetAnimatorBool("moving", false);

                patrolWaitTimer -= Time.deltaTime;
                if (patrolWaitTimer <= 0f)
                {
                    isWaitingAtPatrolPoint = false;
                }
                return;
            }

            Transform patrolTarget = GetValidPatrolTarget();

            // No usable patrol points (all slots empty or destroyed) -> stand still instead of erroring every frame
            if (patrolTarget == null)
            {
                isMoving = false;
                SetAnimatorBool("moving", false);
                ApplyHorizontalVelocity(0f);
                return;
            }

            MoveTowards(patrolTarget.position);

            bool reachedPatrolPoint = isPatrolGround
                ? Mathf.Abs(transform.position.x - patrolTarget.position.x) < 1f
                : Vector2.Distance(transform.position, patrolTarget.position) < 1f;

            // Walking into a wall (or hanging on its side, held up by friction) without getting anywhere:
            // count that point as done and turn back. Works even with no Ground Layer set (Horse Rider).
            // (Measured by how far it actually got: the velocity read back here is just what was asked for this frame.)
            if (!reachedPatrolPoint && isPatrolGround && isMoving && Mathf.Abs(moveSpeed) > 0.1f && patrolBlockedTime > 0f)
            {
                if (patrolBlockedTimer <= 0f) patrolBlockStartX = transform.position.x;
                patrolBlockedTimer += Time.deltaTime;
                if (patrolBlockedTimer >= patrolBlockedTime)
                {
                    float expected = Mathf.Abs(moveSpeed) * patrolBlockedTimer;
                    if (Mathf.Abs(transform.position.x - patrolBlockStartX) < Mathf.Min(0.2f, expected * 0.25f)) reachedPatrolPoint = true;
                    patrolBlockedTimer = 0f;
                }
            }
            else
            {
                patrolBlockedTimer = 0f;
            }

            if (reachedPatrolPoint)
            {
                patrolBlockedTimer = 0f;
                patrolDestination = (patrolDestination + 1) % patrolPoints.Length;
                isMoving = true;

                if (enablePatrolHesitation)
                {
                    isWaitingAtPatrolPoint = true;
                    patrolWaitTimer = patrolWaitTime;
                }
            }
        }
        else
        {
            isMoving = false;
            SetAnimatorBool("moving", false);
            ApplyHorizontalVelocity(0f);
        }
    }

    // Returns the current patrol point, skipping over empty (None) slots in the patrolPoints array.
    // Warns once per enemy so the broken Inspector setup can be found and fixed.
    private Transform GetValidPatrolTarget()
    {
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            Transform candidate = patrolPoints[patrolDestination];
            if (candidate != null)
            {
                return candidate;
            }

            if (!hasWarnedMissingPatrolPoint)
            {
                hasWarnedMissingPatrolPoint = true;
                Debug.LogWarning($"EnemyMovement on '{name}' has an empty slot in Patrol Points (element {patrolDestination}). Assign it or remove it in the Inspector.", this);
            }

            patrolDestination = (patrolDestination + 1) % patrolPoints.Length;
        }

        return null;
    }

    private void MoveTowards(Vector3 targetPosition)
    {
        Vector2 movementDirection;
        if (isPatrolGround)
        {
            float direction = Mathf.Sign(targetPosition.x - transform.position.x);

            if (enableEdgeAvoidance && IsAtEdge(direction))
            {
                ApplyHorizontalVelocity(0f);
                SetAnimatorBool("moving", false);
                isMoving = false;
                return;
            }

            movementDirection = new Vector2(direction, 0f);
        }
        else
        {
            movementDirection = (targetPosition - transform.position).normalized;
        }

        ApplyHorizontalVelocity(movementDirection.x * moveSpeed);
        FlipSprite(movementDirection.x);
        SetAnimatorBool("moving", true);
        isMoving = true;
    }

    private void FlipSprite(float direction)
    {
        if (direction == 0) return;

        float targetDirection = Mathf.Sign(direction);

        if (enableSmoothFacing && targetDirection != currentFacingDirection)
        {
            // Turning around waits for the cooldown; facing the same way still re-applies the scale below,
            // so the sprite can never stay out of sync with the direction it thinks it faces
            if (Time.time < lastTurnTime + turnCooldown) return;
            currentFacingDirection = targetDirection;
            lastTurnTime = Time.time;
        }
        else
        {
            currentFacingDirection = targetDirection;
        }

        if (!isSquashing)
        {
            Vector3 flipped = GetFlippedScale(currentFacingDirection);
            if (transform.localScale != flipped) transform.localScale = flipped;
        }
    }

    private void SetAnimatorBool(string paramName, bool value)
    {
        if (animator != null && AnimatorHasParameter(paramName))
        {
            animator.SetBool(paramName, value);
        }
    }

    private bool AnimatorHasParameter(string paramName)
    {
        if (animator == null) return false;

        // usesOverride swaps the controller at runtime, so rebuild the cache whenever it changes
        if (cachedController != animator.runtimeAnimatorController)
        {
            cachedController = animator.runtimeAnimatorController;
            animatorParameters.Clear();
            if (cachedController != null)
            {
                foreach (AnimatorControllerParameter param in animator.parameters) animatorParameters.Add(param.name);
            }
        }

        return animatorParameters.Contains(paramName);
    }

    private void GroundCheck()
    {
        Vector2 rayOrigin = (Vector2)transform.position + Vector2.up * groundCheckOffset;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, groundCheckDistance + groundCheckOffset, groundLayer);

        wasGroundedLastFrame = isGrounded;
        isGrounded = hit.collider != null;

        if (!isGrounded)
        {
            if (transform.position.y > peakAirHeight)
            {
                peakAirHeight = transform.position.y;
            }
        }
        else
        {
            if (!wasGroundedLastFrame)
            {
                float totalFallDistance = peakAirHeight - transform.position.y;

                if (enableLandingSquash && !isSquashing)
                {
                    StartCoroutine(SquashAndStretchRoutine(landingSquashScale));
                }

                if (enableDustParticles && landingDustParticle != null)
                {
                    landingDustParticle.Play();
                }

                if (enableLandingStun && totalFallDistance >= minDropHeightForStun)
                {
                    TriggerLandingStun();
                }

                peakAirHeight = transform.position.y;
            }
            else
            {
                peakAirHeight = transform.position.y;
            }
        }
    }
}