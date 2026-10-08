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

    [Header("Fairness (see EnemyFairness)")]
    [Tooltip("Archers / bombers: seconds the red aim trace shows before the shot. -1 = auto (0.45).")]
    [SerializeField] private float telegraphTime = -1f;
    [Tooltip("Melee enemies only start an attack while on screen (archers / bombers shoot from anywhere, with an aim trace).")]
    [SerializeField] private bool onlyAttackOnScreen = true;
    private bool ranged, rangedChecked, telling;

    // Rowdy is inside the attack box right now (EnemyAlert uses it: archers don't chase, this is their aggro)
    public bool SeesPlayer { get; private set; }
    private float aliveSince;
    private EnemyHealth health;

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
    private int enemyLayerIndex = -2, ignorePlayerLayerIndex = -2;


    // How far in front of the enemy's middle its attack check reaches (world units). Used by EnemyMovement to stop
    // chasing once Rowdy is inside it instead of walking into him.
    public float AttackReach
    {
        get
        {
            if (boxCollider == null) return 1f;
            float width = boxCollider.bounds.size.x * range;
            float offset = Mathf.Abs(range * transform.localScale.x * colliderDistance);
            return Mathf.Max(0.3f, offset + width * 0.5f);
        }
    }

    private void Awake()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        aliveSince = Time.time; // spawn / wake-up grace
        telling = false;
    }

    private void OnDisable()
    {
        telling = false;
    }

    private void Update()
    {
        cooldownTimer += Time.deltaTime;

        // Attack only when player is in sight. Melee: never from off screen / right after spawning.
        // Archers / bombers: anywhere, but a red aim trace shows first.
        SeesPlayer = PlayerInSight();
        if (!telling && SeesPlayer)
        {
            if (cooldownTimer >= attackCooldown)
            {
                if (!rangedChecked) { rangedChecked = true; ranged = EnemyFairness.IsRanged(health); }
                bool allowed = ranged ? Time.time - aliveSince >= EnemyFairness.SpawnGrace
                                      : !onlyAttackOnScreen || EnemyFairness.CanStartMeleeAttack(this, aliveSince);
                if (allowed)
                {
                    cooldownTimer = 0;
                    string selectedAttack = SelectAttackAnimation();
                    float aim = telegraphTime >= 0f ? telegraphTime : 0.45f;
                    if (ranged && aim > 0f) StartCoroutine(AimThenShoot(selectedAttack, aim));
                    else anim.SetTrigger(selectedAttack);
                }
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

        // IgnorePlayer while phasing, Enemy otherwise (layer indices cached, this runs every frame)
        if (enemyLayerIndex < -1)
        {
            enemyLayerIndex = LayerMask.NameToLayer(enemyLayer);
            ignorePlayerLayerIndex = LayerMask.NameToLayer(ignorePlayerLayer);
        }
        int layer = isPhasingDead ? ignorePlayerLayerIndex : enemyLayerIndex;
        if (gameObject.layer != layer) gameObject.layer = layer;
    }

    // Archers / bombers: red aim trace first, then the shot (cancelled if it dies meanwhile)
    private IEnumerator AimThenShoot(string trigger, float aim)
    {
        telling = true;
        EnemyFairness.AimTrace(this, aim);
        yield return new WaitForSeconds(aim);
        telling = false;
        if (health == null || !health.enemydead) anim.SetTrigger(trigger);
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