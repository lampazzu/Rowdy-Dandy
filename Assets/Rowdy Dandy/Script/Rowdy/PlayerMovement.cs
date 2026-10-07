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
    [SerializeField] private float dropDownDisableDuration = 0.35f;

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
    }

    private void ApplyCustomForce()
    {
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
    }

    private void Update()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer);
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

            isWounded = false;
            ResetAttackState();
            isJumping = false;
            animator.SetBool("IsJumping", false);
        }

        float moveInput = Input.GetAxisRaw("Horizontal");

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
            moveInput = Input.GetAxis("Horizontal");
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
        if ((Input.GetKey(KeyCode.S) || Input.GetAxis("Vertical") < 0) && !isSurfing && isGrounded && !isWounded)
        {
            isDucking = true;
            GetComponent<BoxCollider2D>().size = new Vector2(GetComponent<BoxCollider2D>().size.x, duckedColliderHeight);
            GetComponent<BoxCollider2D>().offset = new Vector2(0f, duckedColliderOffsetY); // Set the offset based on your needs
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
                GetComponent<BoxCollider2D>().size = new Vector2(GetComponent<BoxCollider2D>().size.x, originalColliderHeight);
                GetComponent<BoxCollider2D>().offset = new Vector2(0f, originalColliderOffsetY); // Set the offset based on your needs

                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        if (isGrounded && !isWounded)
        {
            animator.ResetTrigger("BackToIdle");
        }

        // Apply movement only if not ducking
        if (!isDucking || isSurfing || isWounded)
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

        if (rb.linearVelocity.x == 0f || Input.GetKeyUp(KeyCode.S))
        {
            isSliding = false;
            animator.ResetTrigger("Slide");
            animator.SetBool("Slide", false);
        }

        if (isSliding)
        { isDucking = true; }

        // HERE IS THE JUMP & DROP-DOWN FUNCTIONALITY
        bool isHoldingDown = Input.GetKey(KeyCode.S) || Input.GetAxis("Vertical") < -0.5f;
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetButtonDown("Jump");

        // DROP-DOWN CHECK (Down + Jump)
        if (jumpPressed && isHoldingDown && isGrounded && !isWounded)
        {
            StartCoroutine(DropThroughPlatformRoutine());
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
        if ((Input.GetKey(KeyCode.Space) || Input.GetButton("Jump")) && !isAttacking && hasInitiatedJump)
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
        if ((Input.GetKeyUp(KeyCode.Space) || Input.GetButtonUp("Jump")) || jumpTimeCounter >= maxJumpTime)
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
        if (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0) || Input.GetButtonDown("X"))
        {
            if (isGrounded && !isJumping && !isDucking && !isAttacking && !isWatered)
            {
                transform.rotation = Quaternion.Euler(0, 0, 0);// Neutral attack animation
                animator.SetTrigger("NeutralAttack");
                animator.SetBool("IsRunning", false);
            }
            if (!isGrounded && !isDucking && !hasAttackedInAir && !isWatered && !isAttackChecked)
            {
                // Jump attack animation
                animator.SetTrigger("JumpAttack");
                hasAttackedInAir = true;

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

                if (!isAttackingAir)
                {
                    attackAirCheck += 1f;
                }
            }
            if (isDucking && !isAttacking)
            {
                // Ducking attack animation
                animator.SetTrigger("DuckingAttack");
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
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.L) || (Input.GetButtonDown("RB"))
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

    private IEnumerator DropThroughPlatformRoutine()
    {
        Collider2D platformCollider = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);

        if (platformCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, platformCollider, true);
            yield return new WaitForSeconds(dropDownDisableDuration);
            Physics2D.IgnoreCollision(playerCollider, platformCollider, false);
        }
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