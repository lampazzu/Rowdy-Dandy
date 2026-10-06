using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PetFollower : MonoBehaviour
{
    private static readonly List<PetFollower> ActivePets = new List<PetFollower>();

    [Header("Following Settings")]
    [SerializeField] private Transform player;
    [SerializeField] private float followDelay = 0.5f;
    [SerializeField] private float followSpeed = 3f;
    [SerializeField] private Vector3 offset = new Vector3(-1f, 1.5f, 0f);
    [SerializeField] private float petSpacing = 1f;

    [Header("Hovering Settings")]
    [SerializeField] private float hoverSpeed = 2f;
    [SerializeField] private float hoverAmount = 0.2f;

    [Header("Attack Settings")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private bool backToIdle = false;

    private Transform targetEnemy = null;
    private bool isFollowingPlayer = false;
    private bool canAttack = true;
    private Animator anim;
    private bool facingRight = true;

    // Gives every pet slightly different movement timing
    private float hoverOffset;
    private float movementOffset;

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
    }

    private void Update()
    {
        if (backToIdle)
        {
            ResetToIdle();
            return;
        }

        if (player == null)
            return;

        if (canAttack)
            DetectNearestEnemy();

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
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (player != null)
            return;

        if (other.CompareTag("Player"))
        {
            player = other.transform;
            isFollowingPlayer = true;

            StartCoroutine(StartFollowing());
        }
    }

    private void FollowTarget(Transform target)
    {
        if (target == null)
            return;

        Vector3 formationOffset = GetFormationOffset();

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

    private Vector3 GetFormationOffset()
    {
        List<PetFollower> playerPets = new List<PetFollower>();

        foreach (PetFollower pet in ActivePets)
        {
            if (pet != null && pet.player == player)
            {
                playerPets.Add(pet);
            }
        }

        if (playerPets.Count == 0)
            return offset;

        int index = playerPets.IndexOf(this);
        int count = playerPets.Count;

        // One pet stays on the original side
        if (count == 1)
        {
            return offset;
        }

        // Spread pets horizontally around the player
        float totalWidth = (count - 1) * petSpacing;
        float xOffset = (index * petSpacing) - totalWidth / 2f;

        return new Vector3(
            xOffset,
            offset.y,
            offset.z
        );
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
            if (target != null && target.gameObject != null)
            {
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
        }

        if (closestTarget != null && canAttack)
        {
            EnemyHealth enemyHealth = closestTarget.GetComponent<EnemyHealth>();

            if (enemyHealth != null && !enemyHealth.NoPetFollow)
            {
                targetEnemy = closestTarget;
                isFollowingPlayer = false;
                StartCoroutine(AttackEnemy());
            }
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

        anim.SetTrigger("Attack");

        yield return new WaitForSeconds(attackCooldown);

        if (targetEnemy != null && targetEnemy.gameObject != null)
        {
            targetEnemy = null;
            isFollowingPlayer = true;
        }

        canAttack = true;
    }

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}