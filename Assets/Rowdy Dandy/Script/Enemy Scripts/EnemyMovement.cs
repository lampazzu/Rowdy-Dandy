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
    [SerializeField] private float aquaticBounceForce = 6f;
    [SerializeField] private float aquaticStuckThreshold = 4f;

    private float aquaticStuckTimer = 0f;
    private float lastAquaticSide = 0f;

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

    [Header("Polish: Dust Particle Effects")]
    [SerializeField] private bool enableDustParticles = false;
    [SerializeField] private ParticleSystem landingDustParticle;
    [SerializeField] private ParticleSystem jumpDustParticle;

    // -------------------------------------------------------------
    // JUMPER MAN DROP STATE
    // -------------------------------------------------------------

    private bool hasCommittedDropDirection;
    private float committedDropDirection;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;

        // Water splash / drowning effects
        WaterSplashBody.AttachTo(gameObject, false);

        GameObject player = GameObject.FindWithTag("Player");

        if (player != null)
        {
            playerTransform = player.transform;
        }

        gameObject.layer = LayerMask.NameToLayer(enemyLayer);

        if (randomizeSpeed)
        {
            moveSpeed = Random.Range(minSpeed, maxSpeed);
        }

        if (isDrownable)
        {
            SetupDrowningCollisions();
        }

        lastStuckCheckPosition = transform.position;
    }

    private void SetupDrowningCollisions()
    {
        Collider2D[] enemyColliders = GetComponentsInChildren<Collider2D>(true);
        if (enemyColliders.Length == 0) return;

        Collider2D[] allColliders = FindObjectsByType<Collider2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Collider2D otherCollider in allColliders)
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

    private bool IsDrowningLayer(int layer)
    {
        if (waterLayer.value != 0 && (waterLayer.value & (1 << layer)) != 0) return true;
        if (layer == LayerMask.NameToLayer("Water")) return true;
        if (layer == LayerMask.NameToLayer("AntiEnemy")) return true;

        return false;
    }

    private void Update()
    {
        if (isPhasing)
        {
            gameObject.layer = LayerMask.NameToLayer(ignorePlayerLayer);
        }
        else
        {
            gameObject.layer = LayerMask.NameToLayer(enemyLayer);
        }

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

        // AQUATIC ENEMY CHASE & LAND ESCAPE RULES
        if (isOnlyAquatic)
        {
            // Drop chase if player leaves water layer
            if (isChasing && !IsPlayerInWater())
            {
                isChasing = false;
            }

            // FORCE RETURN TO WATER: If stranded on land outside chase, prioritize returning to water
            if (!isChasing && !IsSelfInWater())
            {
                ReturnToWater();
                return;
            }
        }

        if (isChasing || isMarching)
        {
            aquaticStuckTimer = 0f;
            ChaseTarget(target);
        }
        else
        {
            bool canChasePlayer = playerTransform != null &&
                Vector2.Distance(transform.position, playerTransform.position) < chaseDistance &&
                HasLineOfSightToTarget(playerTransform);

            if (isOnlyAquatic && canChasePlayer)
            {
                canChasePlayer = IsPlayerInWater();
            }

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

    private bool IsPlayerInWater()
    {
        if (playerTransform == null) return false;
        Collider2D waterHit = Physics2D.OverlapPoint(playerTransform.position, waterLayer);
        return waterHit != null;
    }

    private bool IsSelfInWater()
    {
        Collider2D waterHit = Physics2D.OverlapPoint(transform.position, waterLayer);
        return waterHit != null;
    }

    private void ReturnToWater()
    {
        // Find nearest patrol point that is actually inside water
        int bestWaterPoint = -1;
        float minDistance = float.MaxValue;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null) continue;
            if (Physics2D.OverlapPoint(patrolPoints[i].position, waterLayer) != null)
            {
                float dist = Vector2.Distance(transform.position, patrolPoints[i].position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestWaterPoint = i;
                }
            }
        }

        if (bestWaterPoint != -1)
        {
            patrolDestination = bestWaterPoint;
            Vector2 waterDir = (patrolPoints[bestWaterPoint].position - transform.position).normalized;
            ApplyHorizontalVelocity(waterDir.x * moveSpeed);
            FlipSprite(waterDir.x);
            SetAnimatorBool("moving", true);
        }
        else
        {
            // Emergency hop back towards water if no submerged patrol points exist
            Patrol();
        }
    }

    // =========================================================================
    // AQUATIC GROUND REBOUND SAFETY NET
    // =========================================================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isOnlyAquatic && IsGroundLayer(other.gameObject.layer))
        {
            HandleAquaticGroundRebound(other.bounds.center);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isOnlyAquatic && IsGroundLayer(collision.gameObject.layer))
        {
            HandleAquaticGroundRebound(collision.contacts[0].point);
        }
    }

    private bool IsGroundLayer(int layer)
    {
        return (groundLayer.value & (1 << layer)) != 0;
    }

    private void HandleAquaticGroundRebound(Vector2 groundPoint)
    {
        isChasing = false;

        Vector2 pushDir = ((Vector2)transform.position - groundPoint).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(pushDir * aquaticBounceForce, ForceMode2D.Impulse);

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            patrolDestination = (patrolDestination + 1) % patrolPoints.Length;
            aquaticStuckTimer = 0f;
        }
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

            // Check if stuck on one side of aquatic patrol point for too long
            if (isOnlyAquatic)
            {
                float currentSide = Mathf.Sign(patrolTarget.position.x - transform.position.x);
                if (currentSide == lastAquaticSide)
                {
                    aquaticStuckTimer += Time.deltaTime;
                    if (aquaticStuckTimer >= aquaticStuckThreshold)
                    {
                        patrolDestination = (patrolDestination + 1) % patrolPoints.Length;
                        aquaticStuckTimer = 0f;
                        return;
                    }
                }
                else
                {
                    lastAquaticSide = currentSide;
                    aquaticStuckTimer = 0f;
                }
            }

            MoveTowards(patrolTarget.position);

            bool reachedPatrolPoint = isPatrolGround
                ? Mathf.Abs(transform.position.x - patrolTarget.position.x) < 1f
                : Vector2.Distance(transform.position, patrolTarget.position) < 1f;

            if (reachedPatrolPoint)
            {
                patrolDestination = (patrolDestination + 1) % patrolPoints.Length;
                aquaticStuckTimer = 0f;
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

            if (isOnlyAquatic && IsObstacleInFront(direction))
            {
                patrolDestination = (patrolDestination + 1) % patrolPoints.Length;
                aquaticStuckTimer = 0f;
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

        if (enableSmoothFacing)
        {
            if (targetDirection != currentFacingDirection && Time.time >= lastTurnTime + turnCooldown)
            {
                currentFacingDirection = targetDirection;
                lastTurnTime = Time.time;
            }
            else
            {
                return;
            }
        }
        else
        {
            currentFacingDirection = targetDirection;
        }

        if (!isSquashing)
        {
            transform.localScale = GetFlippedScale(currentFacingDirection);
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

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName) return true;
        }

        return false;
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