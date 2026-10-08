using System.Collections.Generic;
using System.Collections;
using System.Linq.Expressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TextCore.Text;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public float duckedColliderHeight = 0.3518873f;
    public float originalColliderHeight = 2f;
    public float duckedColliderOffsetY = -0.1540564f; // Adjust this value based on your needs
    public float originalColliderOffsetY = 0f;  // Adjust this value based on your needs
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    [SerializeField] public float slideForce = 6000f;
    public string shadowSurfRight = "Sssr";
    public string shadowSurfLeft = "Sssl";
    private GameObject Sssr;
    private GameObject Sssl;
    [SerializeField] private bool canCoyote = false;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float coyoteTimer = 0f;
    private float originalSpeed = 1;
    [SerializeField] private float attackAirCheck = 0f;
    public GameObject effectPrefab; // dust when land

    [Header("Drop-Down Platform Settings")]
    [SerializeField] private float dropDownMaxDuration = 1f; // safety: collision comes back after this even if Rowdy is still inside the platform

    [Header("Body Collider")]
    [SerializeField] private float maxStepHeight = 0.06f; // lips/seams lower than this get stepped over automatically (0 = off)
    [SerializeField] private float groundSnapDistance = 0.12f; // keeps him on the ground over slope crests / small dips instead of launching (0 = off)

    public Transform groundCheck;
    public LayerMask groundLayer;
    public LayerMask waterLayer;
    public LayerMask enemyLayer;
    public AudioClip audioClip;
    public GameObject rowdy;

    private AudioSource audioSource;

    public ParticleSystem dust;
    public ParticleSystem surfWater;
    public ParticleSystem surfSplash;
    public ParticleSystem bigdust;

    private Rigidbody2D rb;
    private Animator animator;
    [SerializeField] private bool isGrounded = false;
    public bool IsGrounded => isGrounded;
    [SerializeField] private float jumpTimeCounter = 0f;
    [SerializeField] private bool isJumping = false;
    [SerializeField] private bool isDucking = false;
    [SerializeField] private bool isAttacking = false;
    [SerializeField] private bool isSliding = false;
    [SerializeField] private bool isWatered = false;
    [SerializeField] private bool isSurfing = false;
    [SerializeField] private bool canFlip = true;
    [SerializeField] private bool isWounded = false;
    [SerializeField] private bool isOnEnemy = false;
    [SerializeField] private bool isNeutralAttacking = false;
    [SerializeField] private bool isAirAttackDone = false;
    [SerializeField] private bool isDead = false;
    [SerializeField] private bool isAttackChecked = false;
    [SerializeField] private bool isAttackingAir = false;
    [SerializeField] private bool hasAttackedInAir = false;
    [SerializeField] private bool hasInitiatedJump = false;
    [SerializeField] private bool isBypassingLayer = false;  // Boolean to toggle the layer change
    [SerializeField] private bool isCanMoveDucking = false;
    [SerializeField] private bool isJumpAttackReset = false;
    [SerializeField] private bool isCanCancelGroundedAttack = false;
    private WeaponManager weaponManager;

    [SerializeField] private float originalLinearDrag;
    [SerializeField] private float duckedLinearDrag;
    [SerializeField] private bool applyForce = false;
    private float knockbackTimer; // set by ApplyKnockback
    [SerializeField] private float forceX = 10f;  // Horizontal force
    [SerializeField] private float forceY = 5f;   // Vertical force

    [SerializeField] public float maxJumpTime = 0.5f;

    private List<UnityEvent> rodAttackEvents = new List<UnityEvent>();
    private List<UnityEvent> slidingEvents = new List<UnityEvent>();

    [SerializeField] public UnityEvent onPullRod;
    [SerializeField] public UnityEvent onEnterWater;
    [SerializeField] public UnityEvent onJumping;
    [SerializeField] public UnityEvent onSliding;
    [SerializeField] public UnityEvent onSlidingD;
    [SerializeField] public UnityEvent onSlidingB;
    [SerializeField] public UnityEvent onSlidingC;
    [SerializeField] public UnityEvent onReDive;
    [SerializeField] public UnityEvent onLanding;
    [SerializeField] public UnityEvent onRodAttack;
    [SerializeField] public UnityEvent onBodySlide;
    [SerializeField] public UnityEvent onGruntA;
    [SerializeField] public UnityEvent onGruntB;
    [SerializeField] public UnityEvent onGruntC;
    [SerializeField] public UnityEvent onGruntD;

    private Collider2D playerCollider;
    private BoxCollider2D bodyBox;
    private ContactFilter2D groundFilter;
    private readonly Collider2D[] groundHits = new Collider2D[8];
    private readonly List<RaycastHit2D> surfaceHits = new List<RaycastHit2D>();
    private Collider2D droppingThrough; // one-way platform currently being dropped through
    private float lastMoveInput; // read in Update, used by the step-up in FixedUpdate
    private float lastStepCheckX;
    private bool wasGroundedFixed;
    private ContactFilter2D effectorFilter;
    private readonly Collider2D[] effectorHits = new Collider2D[8];

    public void ResetAttackState()
    {
        animator.ResetTrigger("NeutralAttack");
        animator.ResetTrigger("JumpAttack");
        animator.ResetTrigger("DuckingAttack");
    }

    public void EndHitAnimation()
    {
        // Animation event triggered when getting hit animation ends
        isWounded = false;
        animator.SetTrigger("BackToIdle");
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        animator.SetTrigger("Respawn");
        originalSpeed = animator.speed;
        playerCollider = GetComponent<Collider2D>();
        playerCollider.enabled = true;

        bodyBox = GetComponent<BoxCollider2D>();
        bodyBox.edgeRadius = 0f; // clean up the rounded corners an earlier version set (they lifted him off slopes)

        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = Physics2D.queriesHitTriggers;

        effectorFilter = new ContactFilter2D();
        effectorFilter.useTriggers = true;

        audioSource = GetComponent<AudioSource>();
        originalLinearDrag = rb.linearDamping;
        Sssr = transform.Find(shadowSurfRight).gameObject;
        Sssl = transform.Find(shadowSurfLeft).gameObject;
        rodAttackEvents.Add(onGruntA);
        rodAttackEvents.Add(onGruntB);
        rodAttackEvents.Add(onGruntC);
        rodAttackEvents.Add(onGruntD);
        slidingEvents.Add(onSliding);
        slidingEvents.Add(onSlidingB);
        slidingEvents.Add(onSlidingC);
        slidingEvents.Add(onSlidingD);
        weaponManager = FindFirstObjectByType<WeaponManager>();

        // Water splash effects (sound already comes from On Enter Water)
        WaterSplashBody.AttachTo(gameObject, true);

        // Pushes interactive grass aside; attack hitboxes whip plants and shake trees
        VegetationInteractor vegetationBody = VegetationInteractor.AttachTo(gameObject, 1f);
        VegetationInteractor.AttachSlashes(gameObject, vegetationBody);

        // Better getting-hit feel (knockback away from the attacker, freeze, blink, screen-edge flash)
        if (GetComponent<PlayerHitReaction>() == null) gameObject.AddComponent<PlayerHitReaction>();
    }

    // Called by PlayerHitReaction: shove Rowdy and ignore input for a moment so the knockback reads
    public void ApplyKnockback(Vector2 velocity, float lockTime)
    {
        rb.linearVelocity = velocity;
        knockbackTimer = lockTime;
    }

    private void ApplyCustomForce()
    {
        // During a knockback, the hit clip's facing-based force would fight the real knockback direction
        if (knockbackTimer > 0f) return;

        float direction = transform.localScale.x > 0 ? 1f : -1f; // Character facing direction

        if (applyForce)
        {
            // Apply continuous horizontal force
            rb.AddForce(new Vector2(forceX * direction, 0), ForceMode2D.Force);
        }

        if (applyForce && forceY != 0)
        {
            // Apply vertical force once when activated
            rb.AddForce(new Vector2(0, forceY), ForceMode2D.Impulse);

            // Reset vertical force after one application
            forceY = 0;
        }
    }

    //splash sound surf
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy") && isAttacking && !isGrounded)
        {

        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // A one-way platform Rowdy is passing through from below reports a disabled collision: not a landing
        if (collision.gameObject.CompareTag("Ground") && !collision.enabled) return;

        if (collision.gameObject.CompareTag("Ground") && isSurfing) // Adjust the tag as needed
        {
            // Create an array of the sliding events
            UnityEvent[] slidingEvents = { onSliding, onSlidingB, onSlidingC, onSlidingD };

            // Randomly choose one event to invoke
            int randomIndex = Random.Range(0, slidingEvents.Length);

            // Invoke the randomly chosen event
            slidingEvents[randomIndex].Invoke();
        }

        if (collision.gameObject.CompareTag("Ground")) // Adjust the tag as needed
        {
            onLanding.Invoke();

            if (isGrounded && !isJumping)
            {
                jumpTimeCounter = 0f;
            }

            if (effectPrefab != null)
            {
                GameObject effectInstance = Instantiate(effectPrefab, transform.position, Quaternion.identity);
                Destroy(effectInstance, 2f); // Destroy after 7 frames (~0.12 seconds)
            }
        }

        if (collision.gameObject.CompareTag("Water")) // Adjust the tag as needed
        {
            onEnterWater.Invoke();
            if (!isWatered) { surfSplash.Play(); }
        }

        if (collision.gameObject.CompareTag("Enemy") && !isGrounded)
        {
            isOnEnemy = true; // Set to false when no longer colliding with an enemy
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            isOnEnemy = false; // Set to false when no longer colliding with an enemy
        }
    }

    void FixedUpdate()
    {
        if (isSurfing && isGrounded)
        {
            RaycastHit2D hit = Physics2D.Raycast(groundCheck.position, Vector2.down, 2.5f, groundLayer);

            if (hit.collider != null)
            {
                float angle = Mathf.Atan2(hit.normal.y, hit.normal.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        ApplyCustomForce();
        TryStepUp();
        SnapToGround();
    }

    // Walking up a slope gives him upward speed; at the crest that speed launched him into the air ("flying").
    // If he was grounded last step, didn't jump, and the ground is just below his feet, keep him glued to it.
    private void SnapToGround()
    {
        bool groundedNow = CheckGrounded();
        bool canSnap = wasGroundedFixed && !groundedNow && !hasInitiatedJump && !isJumping && !applyForce
            && knockbackTimer <= 0f && droppingThrough == null && !isWatered && !isSurfing
            && rb.linearVelocity.y <= moveSpeed + 0.2f && !InsideEffectorField();
        wasGroundedFixed = groundedNow;
        if (!canSnap) return;

        Bounds b = bodyBox.bounds;
        float inset = Mathf.Min(0.02f, b.extents.x);
        float[] rayXs = { b.min.x + inset, b.center.x, b.max.x - inset };
        float nearest = float.MaxValue;
        foreach (float x in rayXs)
        {
            int count = Physics2D.Raycast(new Vector2(x, b.min.y + 0.01f), Vector2.down, groundFilter, surfaceHits, groundSnapDistance + 0.01f);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = surfaceHits[i];
                if (hit.normal.y < 0.5f || hit.collider == droppingThrough) continue;
                nearest = Mathf.Min(nearest, hit.distance - 0.01f);
                break;
            }
        }

        if (nearest > 0.005f && nearest <= groundSnapDistance)
        {
            rb.position += new Vector2(0f, -(nearest - 0.005f));
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, 0f));
            wasGroundedFixed = true;
        }
    }

    // Plants etc. push Rowdy with force effectors; never snap him back down while one is acting on him
    private bool InsideEffectorField()
    {
        int count = bodyBox.Overlap(effectorFilter, effectorHits);
        for (int i = 0; i < count; i++)
        {
            if (effectorHits[i].isTrigger && effectorHits[i].usedByEffector) return true;
        }
        return false;
    }

    // Tilemap seams and tiny steps snag the body box. If only the bottom maxStepHeight of the body is blocked
    // in the direction he's moving (and the rest of the body is clear), lift him on top of the lip.
    private void TryStepUp()
    {
        // Only when actually snagged: holding a direction but barely moving (otherwise slopes made of tiny steps launch him)
        float movedX = Mathf.Abs(rb.position.x - lastStepCheckX);
        lastStepCheckX = rb.position.x;
        if (maxStepHeight <= 0f || Mathf.Abs(lastMoveInput) < 0.1f || knockbackTimer > 0f || droppingThrough != null || isSurfing) return;
        if (rb.linearVelocity.y > 0.1f || movedX > moveSpeed * Time.fixedDeltaTime * 0.25f) return;

        float dir = Mathf.Sign(lastMoveInput);
        Bounds b = bodyBox.bounds;
        const float probe = 0.03f;
        float width = b.size.x * 0.9f;

        // Upper body blocked = a real wall, leave it alone
        float upperBottom = b.min.y + maxStepHeight + 0.01f;
        Vector2 upperSize = new Vector2(width, b.max.y - upperBottom);
        if (upperSize.y <= 0f || IsBlocked(new Vector2(b.center.x, upperBottom + upperSize.y * 0.5f), upperSize, dir, probe, 0.9f)) return;

        // Feet band (kept just above the floor so the floor itself doesn't count). Any face pushing back counts here,
        // not just vertical ones: corrector polygon corners are often angled. He's stuck, so it isn't a walkable slope.
        Vector2 lowerSize = new Vector2(width, maxStepHeight);
        if (!IsBlocked(new Vector2(b.center.x, b.min.y + 0.01f + maxStepHeight * 0.5f), lowerSize, dir, probe, 0.3f)) return;

        // Find the top of the lip just in front of him (a few spots, an angled corner's top can be a bit further in)
        float frontX = dir > 0 ? b.max.x : b.min.x;
        foreach (float ahead in new[] { 0.01f, 0.03f, 0.06f })
        {
            int count = Physics2D.Raycast(new Vector2(frontX + dir * ahead, upperBottom), Vector2.down, groundFilter, surfaceHits, maxStepHeight + 0.01f);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = surfaceHits[i];
                if (hit.collider.usedByEffector || hit.normal.y < 0.5f) continue;

                float step = hit.point.y - b.min.y;
                if (step > 0.001f && step <= maxStepHeight)
                {
                    rb.position += new Vector2(dir * 0.01f, step + 0.005f);
                    return;
                }
                break;
            }
        }
    }

    // A solid face within 'distance' that pushes back against moving in 'dir' (one-way platforms don't count).
    // minFacing 0.9 = only near-vertical walls, lower = angled faces too.
    private bool IsBlocked(Vector2 center, Vector2 size, float dir, float distance, float minFacing)
    {
        int count = Physics2D.BoxCast(center, size, 0f, new Vector2(dir, 0f), groundFilter, surfaceHits, distance);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = surfaceHits[i];
            if (!hit.collider.usedByEffector && hit.normal.x * dir < -minFacing) return true;
        }
        return false;
    }

    private void Update()
    {
        if (PauseMenu.IsPaused) return; // no jumping / attacking from menu button presses

        isGrounded = CheckGrounded();
        isWatered = Physics2D.OverlapCircle(groundCheck.position, 0.1f, waterLayer);

        {
            // Check if the checkbox is checked and change the layer accordingly
            if (isBypassingLayer)
            {
                gameObject.layer = LayerMask.NameToLayer("PlayerBypass");  // Change to "PlayerBypass" layer
            }
            else
            {
                gameObject.layer = LayerMask.NameToLayer("Player");  // Change back to "Player" layer
            }
        }
        if (!isSurfing || !isGrounded || isAttacking)
        {
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }

        {
            if (isNeutralAttacking && !isGrounded)
            {
                animator.SetTrigger("BackToIdle");
            }
        }

        {
            if (isAirAttackDone && !isGrounded)
            {
                animator.SetTrigger("BackToIdle");
                hasAttackedInAir = false;
                isAttackingAir = false;
            }
        }

        if (isWatered && isWounded)
        {
            animator.SetTrigger("SurfDash");
        }

        if (isGrounded || isWatered)
        {
            attackAirCheck = 0f;
            isAttackChecked = false;
            isAttackingAir = false;
        }

        if (attackAirCheck >= 1)
        {
            isAttackChecked = true;
        }

        if (isSurfing)
            if (isOnEnemy && !isGrounded || (isWatered && isSurfing))
            {
                // Apply force if on top of an enemy
                Vector2 pushDirection = new Vector2(transform.localScale.x * slideForce, 0);
                rb.AddForce(pushDirection * Time.deltaTime);
            }
            else
            {
                // Stop applying force if not on top
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y); // Ensures the force stops being applied
                isOnEnemy = false;
            }

        //flipp stupid fucking wave
        {
            Vector3 characterScale = rowdy.transform.localScale;
            Vector3 particleScale = new Vector3(Mathf.Sign(characterScale.x), 1f, 1f);
            surfWater.transform.localScale = particleScale;
        }

        if (isJumping)
        {
            originalLinearDrag = 0;
            playerCollider.enabled = true;
        }

        if (isWatered && isSurfing && !isWounded)
        {
            // Play the audio clip
            if (!audioSource.isPlaying) // Check if audio is not already playing
            {
                audioSource.PlayOneShot(audioClip);
                CreateWave();
                originalLinearDrag = 0;
                playerCollider.enabled = true;
            }
        }
        else
        {
            // Stop the audio if conditions are not met
            audioSource.Stop();
            surfWater.Stop();
        }

        if (isWatered && !isSurfing)
        {
            animator.SetTrigger("SurfDash");
        }

        if (isWatered)
        {
            hasAttackedInAir = false;
            Tutorials.Show(Tutorials.Topic.Water, null, 0.6f);

            isWounded = false;
            ResetAttackState();
            isJumping = false;
            animator.SetBool("IsJumping", false);
        }

        // First time standing on a one-way platform: how to drop through (checked a few times a second)
        if (isGrounded && Time.frameCount % 15 == 0 && !Tutorials.Done(Tutorials.Topic.Platform) && GetOneWayPlatformUnderFeet() != null)
            Tutorials.Show(Tutorials.Topic.Platform, null, 0.3f);

        float moveInput = GameInput.MoveX;
        lastMoveInput = moveInput;

        if (!isSurfing)
        {
            DisableShadowSurfLeft();
            DisableShadowSurfRight();
        }

        if (isSurfing) // Check if the player is surfing
        {
            if (moveInput > 0 && (isGrounded || isWatered)) // Moving right (D key or right joystick)
            {
                EnableShadowSurfRight();
                DisableShadowSurfLeft(); // Disable left shadow when moving right
            }
            else if (moveInput < 0 && (isGrounded || isWatered)) // Moving left (A key or left joystick)
            {
                EnableShadowSurfLeft();
                DisableShadowSurfRight(); // Disable right shadow when moving left
            }
            else // Not moving horizontally (moveInput == 0)
            {
                DisableShadowSurfRight();
                DisableShadowSurfLeft(); // Disable both shadows when not moving
            }
        }

        if (isSurfing && isGrounded)
        {
            CreateBigDust();
        }

        if (isWatered)
        {
            moveInput = GameInput.MoveXSmoothed;
        }

        if (Mathf.Abs(moveInput) > 0 && isGrounded) { CreateDust(); }

        if (!isAttacking)
        {
            animator.speed = originalSpeed;
        }

        if (isNeutralAttacking)
        {
            animator.SetBool("IsNeutralAttacking", true);
        }

        if (!isNeutralAttacking)
        {
            animator.SetBool("IsNeutralAttacking", false);
        }

        if (isAirAttackDone)
        {
            animator.SetBool("IsAirAttackDone", true);
        }

        if (!isAirAttackDone)
        {
            animator.SetBool("IsAirAttackDone", false);
        }

        // Check if the player is ducking
        if (GameInput.HoldingDown && !isSurfing && isGrounded && !isWounded)
        {
            isDucking = true;
            SetBodyBoxHeight(duckedColliderHeight);
            bodyBox.offset = new Vector2(0f, duckedColliderOffsetY); // Set the offset based on your needs
            if (!isCanMoveDucking)
            {
                rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
            }

            if (isCanMoveDucking)
            {
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }
        else
        {
            if (!isAttacking)
            {
                isDucking = false;
                SetBodyBoxHeight(originalColliderHeight);
                bodyBox.offset = new Vector2(0f, originalColliderOffsetY); // Set the offset based on your needs

                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        if (isGrounded && !isWounded)
        {
            animator.ResetTrigger("BackToIdle");
        }

        // Apply movement only if not ducking (and not while being knocked back, or input would cancel the shove)
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.deltaTime;
        }
        else if (!isDucking || isSurfing || isWounded)
        {
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

            if (moveInput != 0 && canFlip)
            {
                transform.localScale = new Vector3(Mathf.Sign(moveInput), transform.localScale.y, transform.localScale.z);
            }
        }

        if (isGrounded)
        {
            isJumping = false;
            animator.SetBool("IsJumping", false);
            animator.ResetTrigger("JumpAttack");
            hasAttackedInAir = false;
        }

        // coyote functionality
        if (isGrounded && !hasInitiatedJump)
        {
            canCoyote = true;
            coyoteTimer = coyoteTime;
        }
        else if (coyoteTimer > 0f)
        {
            coyoteTimer -= Time.deltaTime;
        }

        if (isDucking && Mathf.Abs(moveInput) > 0)
        {
            /*
             isSliding = true;
             animator.SetTrigger("Slide");
            */
        }

        if (rb.linearVelocity.x == 0f || GameInput.KeyUp(UnityEngine.InputSystem.Key.S))
        {
            isSliding = false;
            animator.ResetTrigger("Slide");
            animator.SetBool("Slide", false);
        }

        if (isSliding)
        { isDucking = true; }

        // HERE IS THE JUMP & DROP-DOWN FUNCTIONALITY
        bool isHoldingDown = GameInput.HoldingDown;
        bool jumpPressed = GameInput.Down(GameInput.Act.Jump);

        // DROP-DOWN CHECK (Down + Jump)
        if (jumpPressed && isHoldingDown && isGrounded && !isWounded)
        {
            Collider2D platform = GetOneWayPlatformUnderFeet();
            if (platform != null)
            {
                StartCoroutine(DropThroughPlatformRoutine(platform));
            }
        }
        // NORMAL JUMP (Only when NOT holding down)
        else if (jumpPressed && (isGrounded || isWatered || coyoteTimer > 0) && !isDucking && !isAttacking && !isWounded)
        {
            canCoyote = false;
            coyoteTimer = 0f;
            isJumping = true;
            hasInitiatedJump = true;
            jumpTimeCounter = 0;
            if (isGrounded)
            {
                onJumping.Invoke();
            }
            if (!isGrounded && isWatered && !isJumping)
            {
                onReDive.Invoke();
            }
        }

        // Holding the jump key (spacebar or joystick button) to control jump height and duration
        if (GameInput.Held(GameInput.Act.Jump) && !isAttacking && hasInitiatedJump)
        {
            if (jumpTimeCounter < maxJumpTime)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce); // Apply jump force
                jumpTimeCounter += Time.deltaTime; // Increment time
                isJumping = true;
                animator.SetBool("IsJumping", true); // Set jumping animation state
                animator.SetTrigger("Jumped"); // Trigger jump animation
            }
        }

        // End the jump when the spacebar is released, joystick button is released, or jump time exceeds max duration
        if (GameInput.Up(GameInput.Act.Jump) || jumpTimeCounter >= maxJumpTime)
        {
            isJumping = false;
            animator.SetBool("IsJumping", false); // Stop jumping animation
            jumpTimeCounter = maxJumpTime; // Reset jump time counter
            hasInitiatedJump = false; // Reset jump initiation flag
        }

        // WEAPONS GIMMICKERY
        if (PlayerDamage.hitEnemy && weaponManager.isWK_Sword && !isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 4f);
            ResetAirAttack();
        }

        // Implement attack animations
        if (GameInput.Down(GameInput.Act.Attack))
        {
            bool attackStarted = false; // weapon durability: 1 per swing, no matter how many enemies it hits

            if (isGrounded && !isJumping && !isDucking && !isAttacking && !isWatered)
            {
                transform.rotation = Quaternion.Euler(0, 0, 0);// Neutral attack animation
                animator.SetTrigger("NeutralAttack");
                animator.SetBool("IsRunning", false);
                attackStarted = true;
            }
            if (!isGrounded && !isDucking && !hasAttackedInAir && !isWatered && !isAttackChecked)
            {
                // Jump attack animation
                animator.SetTrigger("JumpAttack");
                hasAttackedInAir = true;
                attackStarted = true;

                if (!isAttackingAir)
                {
                    attackAirCheck += 1f;
                }
            }
            if (!isGrounded && !isDucking && !hasAttackedInAir && isWatered && !isAttackChecked)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

                // Jump attack animation
                isSurfing = false;
                isWatered = false;

                StartCoroutine(DelayedJumpAttack()); // Delay the attack trigger
                hasAttackedInAir = true;
                attackStarted = true;

                if (!isAttackingAir)
                {
                    attackAirCheck += 1f;
                }
            }
            if (isDucking && !isAttacking)
            {
                // Ducking attack animation
                animator.SetTrigger("DuckingAttack");
                attackStarted = true;
            }

            if (attackStarted && weaponManager != null)
            {
                StartCoroutine(SpendWeaponDurability());
            }
        }

        if (isJumpAttackReset)
        {
            ResetAirAttack();
        }

        animator.SetBool("IsRunning", Mathf.Abs(moveInput) > 0);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetBool("IsDucking", isDucking);
        animator.SetBool("IsAttacking", isAttacking);
        animator.SetBool("IsWatered", isWatered);
        animator.SetBool("IsSliding", isSliding);

        //Implement surfing
        // (as before: the keyboard keys skip the checks below, the gamepad's RB needs them)
        if (GameInput.KeyDown(UnityEngine.InputSystem.Key.LeftShift) || GameInput.KeyDown(UnityEngine.InputSystem.Key.L) || (GameInput.PadDown(GameInput.Act.SurfDash))
     && Mathf.Abs(moveInput) > 0
     && !isDucking
     && !isSurfing
     && !isAttacking
     && !isWounded
     && !hasAttackedInAir)
        {
            animator.SetTrigger("SurfDash");

            onPullRod.Invoke();

            if (isGrounded)
            {
                // Create an array of the sliding events
                UnityEvent[] slidingEvents = { onSliding, onSlidingB, onSlidingC, onSlidingD };

                // Randomly choose one event to invoke
                int randomIndex = Random.Range(0, slidingEvents.Length);

                // Invoke the randomly chosen event
                slidingEvents[randomIndex].Invoke();
            }
        }

        if (Mathf.Abs(moveInput) < 0.1f && isGrounded)
        {
            animator.SetTrigger("StopMoving");
        }
        else
        {
            animator.ResetTrigger("StopMoving");
        }

        if (isNeutralAttacking && !isGrounded && isCanCancelGroundedAttack)
        {
            animator.SetTrigger("DuckToAir");
        }
    }

    // One-way platforms (colliders used by a PlatformEffector2D) only count as ground while Rowdy is actually
    // standing on top of them. Otherwise his feet overlapping one while jumping up through it kills the jump state.
    private bool CheckGrounded()
    {
        int count = Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundFilter, groundHits);
        for (int i = 0; i < count; i++)
        {
            if (!groundHits[i].usedByEffector || IsStandingOn(groundHits[i]))
            {
                return true;
            }
        }

        // On a slope the box rests on one corner and the centered circle can miss the surface, so also check at the feet
        return IsStandingOn(null);
    }

    private bool IsStandingOn(Collider2D platform)
    {
        // platform == null: any ground (solid or one-way)
        if (platform != null && platform == droppingThrough) return false;

        // Standing on top = the platform's top surface is right at his feet (not above them, which means he's inside it).
        // Checked with short rays at both feet edges and the middle so standing on a ledge / on a slope (box rests on one corner) still counts.
        Vector2 velocity = rb.linearVelocity;
        Bounds body = bodyBox.bounds;
        float inset = Mathf.Min(0.02f, body.extents.x);
        float[] rayXs = { body.min.x + inset, body.center.x, body.max.x - inset };
        foreach (float x in rayXs)
        {
            Vector2 origin = new Vector2(x, body.min.y + 0.05f);
            int count = Physics2D.Raycast(origin, Vector2.down, groundFilter, surfaceHits, 0.09f);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = surfaceHits[i];
                bool isPlatform = platform != null ? hit.collider == platform : hit.collider != droppingThrough;
                if (!isPlatform || hit.normal.y < 0.5f) continue;

                // Walking up a slope moves him up too; only rising faster than the slope means jumping (or passing up through a one-way)
                float slopeClimb = Mathf.Abs(velocity.x * hit.normal.x / hit.normal.y);
                if (velocity.y <= slopeClimb + 0.3f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // Down + Jump only works on one-way platforms; on solid ground (or straddling solid ground) it does nothing
    private Collider2D GetOneWayPlatformUnderFeet()
    {
        Collider2D oneWay = null;
        int count = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundFilter, groundHits);
        for (int i = 0; i < count; i++)
        {
            if (!groundHits[i].usedByEffector) return null;
            oneWay = groundHits[i];
        }
        return oneWay;
    }

    private void SetBodyBoxHeight(float height)
    {
        bodyBox.size = new Vector2(bodyBox.size.x, height);
    }

    private IEnumerator DropThroughPlatformRoutine(Collider2D platform)
    {
        List<Collider2D> bodyColliders = new List<Collider2D>();
        foreach (Collider2D col in GetComponentsInChildren<Collider2D>(true))
        {
            if (!col.isTrigger) bodyColliders.Add(col);
        }

        float surfaceY = bodyBox.bounds.min.y;
        foreach (Collider2D col in bodyColliders) Physics2D.IgnoreCollision(col, platform, true);
        droppingThrough = platform;

        // Keep ignoring until his feet are below the surface he left AND he's clear of the platform's tiles.
        // The whole one-way tilemap is a single collider, so a fixed timer would also make him fall through
        // any platform below; this way the collision comes back as soon as he's out of the one he dropped from.
        float timer = 0f;
        while (timer < dropDownMaxDuration)
        {
            yield return new WaitForFixedUpdate();
            timer += Time.fixedDeltaTime;

            bool belowSurface = bodyBox.bounds.min.y < surfaceY - 0.05f;
            if (belowSurface && Physics2D.Distance(bodyBox, platform).distance > 0.01f)
            {
                break;
            }
        }

        foreach (Collider2D col in bodyColliders)
        {
            if (col != null) Physics2D.IgnoreCollision(col, platform, false);
        }
        droppingThrough = null;
    }

    // Spent a moment into the swing, so the swing that breaks the weapon still plays and hits
    // before WeaponManager swaps back to the Rod's animator
    IEnumerator SpendWeaponDurability()
    {
        yield return new WaitForSeconds(0.25f);
        weaponManager.DepleteActiveWeaponDurability(1f);
    }

    IEnumerator DelayedJumpAttack()
    {
        yield return new WaitForSeconds(0.1f);
        animator.SetTrigger("JumpAttack");
        hasAttackedInAir = true;
    }

    // Left Shadow Surf
    void EnableShadowSurfLeft()
    {
        if (shadowSurfLeft != null)
        {
            Sssl.SetActive(true);
        }
    }

    void DisableShadowSurfLeft()
    {
        Sssl.SetActive(false);
    }

    // Right Shadow Surf
    void EnableShadowSurfRight()
    {
        if (shadowSurfRight != null)
        {
            Sssr.SetActive(true);
        }
    }

    void DisableShadowSurfRight()
    {
        Sssr.SetActive(false);
    }

    void CreateDust()
    {
        dust.Play();
    }

    void CreateBigDust()
    {
        bigdust.Play();
    }

    void CreateWave()
    {
        surfWater.Play();
    }

    void RodAttack()
    {
        // Choose a random event from the list
        UnityEvent randomEvent = rodAttackEvents[Random.Range(0, rodAttackEvents.Count)];

        // Invoke the selected event
        randomEvent.Invoke();

        // You can keep the original invocation here if needed
        onRodAttack.Invoke();
    }

    void ResetAirAttack()
    {
        hasAttackedInAir = false;
        isAttackingAir = false;
        isAttackChecked = false;
        attackAirCheck = 0f;
        isAttackingAir = false;
    }

    void BodySlide()
    { onBodySlide.Invoke(); }

    void JumpAnimationCancel()
    {
        animator.SetTrigger("JumpAttackCancel");
    }
}