using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeleeEnemy : MonoBehaviour
{
    [SerializeField] private float attackCooldown;
    [SerializeField] private float range;
    [SerializeField] private float colliderDistance;
    [SerializeField] private int damage;
    [SerializeField] private BoxCollider2D boxCollider;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private string meleeAttackAnimation = "meleeAttack";
    [SerializeField] private string meleeRetreatAnimation = "meleeRetreat";
    [SerializeField] private string meleeAttack2Animation = "meleeAttack2";
    [SerializeField] private bool  isPhasingDead = false;
    private bool isPlayerInsight = false;
    [SerializeField] private bool isGnollWarrior = false;

    private float cooldownTimer = Mathf.Infinity;
    private Animator anim;

    // jump stuff begin

    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float horizontalForce = 5f;
    [SerializeField] private float jumpInterval = 3f;

    private float jumpcooldownTimer = Mathf.Infinity;
    private float jumpTimer;
    private Rigidbody2D rb;

    //jump stuff end
    private string enemyLayer = "Enemy";
    private string ignorePlayerLayer = "IgnorePlayer";


    private void Awake()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        cooldownTimer += Time.deltaTime;

        // Attack only when player is in sight
        if (PlayerInSight())
        {
            if (cooldownTimer >= attackCooldown)
            {
                // Attack
                cooldownTimer = 0;
                string selectedAttack = SelectAttackAnimation();
                anim.SetTrigger(selectedAttack);
            }
        }
        // GnollWarrior jumping behavior
        if (isGnollWarrior && isPlayerInsight)
        {
            jumpTimer -= Time.deltaTime;
            if (jumpTimer <= 0f)
            {
                Jump();
                jumpTimer = jumpInterval; // Reset the jump timer
            }
        }

        if (isPhasingDead)
        {
            // Change the layer to IgnorePlayer when phasing
            gameObject.layer = LayerMask.NameToLayer(ignorePlayerLayer);
        }
        else
        {
            // Revert back to Enemy layer when not phasing
            gameObject.layer = LayerMask.NameToLayer(enemyLayer);
        }
    }

    private bool PlayerInSight()
    {
        RaycastHit2D hit = Physics2D.BoxCast(
            boxCollider.bounds.center + transform.right * range * transform.localScale.x * colliderDistance,
            new Vector3(boxCollider.bounds.size.x * range, boxCollider.bounds.size.y, boxCollider.bounds.size.z),
            0, Vector2.left, 0, playerLayer);
        isPlayerInsight = true;

        return hit.collider != null;
    }

    private void Jump()
    {
        // Apply upward and horizontal force for jumping
        rb.linearVelocity = new Vector2(horizontalForce * transform.localScale.x, jumpForce);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            boxCollider.bounds.center + transform.right * range * transform.localScale.x * colliderDistance,
            new Vector3(boxCollider.bounds.size.x * range, boxCollider.bounds.size.y, boxCollider.bounds.size.z));
    }

    private string SelectAttackAnimation()
    {
        // Randomize between meleeAttack and meleeRetreat
        string[] animations = { meleeAttackAnimation, meleeRetreatAnimation, meleeAttack2Animation };
        int randomIndex = Random.Range(0, animations.Length);
        return animations[randomIndex];
    }
}