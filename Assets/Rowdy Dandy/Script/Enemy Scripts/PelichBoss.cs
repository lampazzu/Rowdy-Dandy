using UnityEngine;
using UnityEngine.SceneManagement;

// Makes Pelich Anus a proper enemy from any side:
//  - turns to face Rowdy between attacks (only while in Idle / Walk, so attacks never flip mid-swing)
//  - the toughest HP in the game, and drops a pile of EXP
//  - once dead: no more attacks and no collision (Rowdy can walk through the body)
// Added automatically to the scene object named "PelichAnus" if it doesn't have it yet; it's also on the
// Enemy_Pelich prefab made by Tools > Rowdy Dandy > Make Pelich Prefab (Editor/PelichPrefabMaker).
[DisallowMultipleComponent]
public class PelichBoss : MonoBehaviour
{
    [Tooltip("Max HP. Highest of any enemy (Gnoll Warrior has 200).")]
    [SerializeField] private float bossHealth = 600f;
    [Tooltip("EXP gems dropped on death (if none are set on EnemyHealth).")]
    [SerializeField] private int expGems = 40;
    [Tooltip("The art faces left at scale x = +1.")]
    [SerializeField] private bool spriteFacesLeft = true;
    [Tooltip("Rowdy has to be this far past Pelich's middle before he turns (no jittering when Rowdy is right on top).")]
    [SerializeField] private float turnDeadZone = 0.4f;
    [Tooltip("Minimum seconds between turns.")]
    [SerializeField] private float turnCooldown = 0.5f;

    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int WalkState = Animator.StringToHash("Walk");

    private EnemyHealth health;
    private Animator animator;
    private Transform rowdy;
    private float lastTurn = -10f;
    private bool deathHandled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        AttachToScene();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToScene();

    private static void AttachToScene()
    {
        foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            EnemyCatalog.Entry entry = EnemyCatalog.Identify(enemy);
            if (entry != null && entry.id == "pelich" && enemy.GetComponent<PelichBoss>() == null)
                enemy.gameObject.AddComponent<PelichBoss>();
        }
    }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        if (health == null) return;
        if (health.startingenemyHealth < bossHealth) health.SetMaxHealth(bossHealth);
        health.SetExpDropIfMissing(Resources.Load<GameObject>("Systems/EXPgem"), expGems);
    }

    private void Update()
    {
        if (health == null) return;

        if (health.enemydead)
        {
            if (!deathHandled) OnDeath();
            return;
        }

        if (rowdy == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            rowdy = player.transform;
        }
        FaceRowdy();
    }

    private void FaceRowdy()
    {
        if (Time.time - lastTurn < turnCooldown) return;
        if (animator != null && animator.isActiveAndEnabled)
        {
            if (animator.IsInTransition(0)) return;
            int state = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            if (state != IdleState && state != WalkState) return; // never turn during an attack
        }

        float dx = rowdy.position.x - transform.position.x;
        if (Mathf.Abs(dx) < turnDeadZone) return;

        bool rowdyOnLeft = dx < 0f;
        float wantSign = rowdyOnLeft == spriteFacesLeft ? 1f : -1f;
        Vector3 scale = transform.localScale;
        if (Mathf.Sign(scale.x) == wantSign) return;
        scale.x = Mathf.Abs(scale.x) * wantSign;
        transform.localScale = scale;
        lastTurn = Time.time;
    }

    private void OnDeath()
    {
        deathHandled = true;
        if (TryGetComponent(out MeleeEnemy melee)) melee.enabled = false;
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>(true))
        {
            EnemyHealth owner = c.GetComponentInParent<EnemyHealth>(true);
            if (owner == null || owner == health) c.enabled = false; // leave any minions parented under him alone
        }
        if (TryGetComponent(out Rigidbody2D body))
        {
            body.linearVelocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic; // no collider left to stand on, so don't let it fall
        }
    }
}
