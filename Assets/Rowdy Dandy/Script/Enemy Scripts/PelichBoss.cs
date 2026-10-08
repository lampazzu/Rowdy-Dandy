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
        UpdateFeetStomp();
        UpdateHeadMarker();
    }

    // ---------------------------------------------------------------- stomp: hiding at his feet doesn't work
    [Header("Feet stomp (Rowdy under him)")]
    [SerializeField] private float stompDamage = 18f;
    [SerializeField] private float stompWarning = 0.55f;
    [SerializeField] private float stompCooldown = 2.2f;
    [SerializeField] private float stompKnockback = 7f;

    private float stompReadyAt;
    private bool stomping;
    private float underFeetSince = -1f;
    private Vector3 stompBaseScale;
    private Color stompBaseColor = Color.white;

    private Bounds BodyBounds
    {
        get
        {
            Bounds b = new Bounds(transform.position, Vector3.one);
            bool any = false;
            foreach (Collider2D c in GetComponents<Collider2D>())
            {
                if (!c.enabled) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            if (!any && TryGetComponent(out SpriteRenderer sr)) b = sr.bounds;
            return b;
        }
    }

    private bool RowdyUnderFeet(Bounds body)
    {
        if (rowdy == null) return false;
        float dx = Mathf.Abs(rowdy.position.x - body.center.x);
        return dx < body.extents.x + 0.6f && rowdy.position.y < body.center.y && rowdy.position.y > body.min.y - 1.5f;
    }

    private void UpdateFeetStomp()
    {
        if (stomping || rowdy == null) return;
        Bounds body = BodyBounds;
        bool under = RowdyUnderFeet(body);
        if (!under) { underFeetSince = -1f; return; }
        if (underFeetSince < 0f) underFeetSince = Time.time;
        // a short moment under him (not just running past), and the hint about his head
        if (Time.time - underFeetSince < 0.35f || Time.time < stompReadyAt) return;
        StartCoroutine(Stomp());
    }

    private System.Collections.IEnumerator Stomp()
    {
        stomping = true;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Color baseColor = sr != null ? sr.color : Color.white;
        Vector3 baseScale = transform.localScale;
        stompBaseScale = baseScale;
        stompBaseColor = baseColor;
        Bounds body = BodyBounds;
        Vector3 feet = new Vector3(body.center.x, body.min.y, 0f);

        if (GameSettings.BossWeakness) ShowHeadHint(true);
        FXSound.Play("Fear", 0.6f, 0.7f);
        GroundShock.Spawn(feet, body.extents.x + 1.2f, new Color(1f, 0.25f, 0.2f, 0.8f), new Color(0.6f, 0.5f, 0.4f), stompWarning); // warning: where it will hit

        // Wind-up: rises a little, flashes red, trembles
        for (float t = 0f; t < 1f; t += Time.deltaTime / stompWarning)
        {
            if (health.enemydead) { stomping = false; yield break; }
            if (sr != null) sr.color = Color.Lerp(baseColor, new Color(1f, 0.35f, 0.3f, baseColor.a), Mathf.Repeat(t * 6f, 1f) < 0.5f ? 0.8f : 0.2f);
            transform.localScale = new Vector3(baseScale.x, baseScale.y * (1f + 0.08f * t), baseScale.z);
            if (Random.value < 0.4f) FXParticle.Burst(feet + new Vector3(Random.Range(-body.extents.x, body.extents.x), 0.05f, 0f), new Color(0.6f, 0.5f, 0.4f), 1, 0.5f, 1.5f, 6f, 0.4f, true);
            yield return null;
        }

        // SLAM
        if (sr != null) sr.color = baseColor;
        transform.localScale = new Vector3(baseScale.x * 1.12f, baseScale.y * 0.85f, baseScale.z);
        GroundShock.Spawn(feet, body.extents.x + 3f, new Color(1f, 0.92f, 0.7f, 1f), new Color(0.6f, 0.45f, 0.3f), 0.5f);
        FXParticle.Burst(feet, new Color(0.6f, 0.45f, 0.3f), 26, 2f, 6f, 12f, 0.7f, true);
        FXSound.Play("Slam", 1f, 0.7f);
        ScreenShake.Impulse(1.2f);
        GamepadRumble.Pulse(0.9f, 0.4f, 0.3f);

        if (RowdyUnderFeet(BodyBounds) && rowdy.TryGetComponent(out Health h))
        {
            h.TakeDamage(stompDamage);
            if (rowdy.TryGetComponent(out Rigidbody2D rb))
                rb.linearVelocity = new Vector2(Mathf.Sign(rowdy.position.x - body.center.x + 0.001f) * stompKnockback, 4.5f);
        }

        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
        {
            transform.localScale = Vector3.Lerp(new Vector3(baseScale.x * 1.12f, baseScale.y * 0.85f, baseScale.z), baseScale, t);
            yield return null;
        }
        transform.localScale = baseScale;
        stompReadyAt = Time.time + stompCooldown;
        stomping = false;
    }

    // ---------------------------------------------------------------- the head is the weak spot
    private SheetFX headArrow;
    private SpriteRenderer[] brackets;
    private float nextHint;
    private static Sprite bracketSprite;

    // The hittable part: the highest of his colliders (the head)
    private Bounds HeadBounds
    {
        get
        {
            Collider2D best = null;
            foreach (Collider2D c in GetComponents<Collider2D>())
                if (c.enabled && (best == null || c.bounds.max.y > best.bounds.max.y)) best = c;
            if (best != null) return best.bounds;
            return BodyBounds;
        }
    }

    private void ShowHeadHint(bool force)
    {
        if (!force && Time.time < nextHint) return;
        nextHint = Time.time + 8f;
        Bounds head = HeadBounds;
        IconPopup.Show(new Vector3(head.center.x, head.max.y + 1.1f, 0f), null, "HIT HIS HEAD!", new Color(1f, 0.85f, 0.3f), 1.3f, 2f);
    }

    private void UpdateHeadMarker()
    {
        if (rowdy == null) return;
        Bounds head = HeadBounds;
        bool near = GameSettings.BossWeakness && // Accessibility > Boss Weakness Indicator (off by default)
                    Mathf.Abs(rowdy.position.x - head.center.x) < 9f && Mathf.Abs(rowdy.position.y - head.center.y) < 7f;

        ItemArt art = ItemArt.Get;
        if (near && headArrow == null && art != null && art.pointer != null)
            headArrow = SheetFX.Play(art.pointer, 6, head.center, 12f, 64f, 120, null, true);
        if (headArrow != null)
        {
            if (!near) { headArrow.Stop(0.2f); headArrow = null; }
            else headArrow.transform.position = new Vector3(head.center.x, head.max.y + 0.45f + Mathf.Abs(Mathf.Sin(Time.time * 5f)) * 0.18f, 0f);
        }

        // Corner brackets around the head, pulsing in and out
        if (brackets == null)
        {
            brackets = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Head Bracket");
                brackets[i] = go.AddComponent<SpriteRenderer>();
                brackets[i].sprite = BracketSprite;
                brackets[i].sortingLayerName = "Default";
                brackets[i].sortingOrder = 120;
                if (CatFX.Unlit != null) brackets[i].sharedMaterial = CatFX.Unlit;
                brackets[i].transform.localScale = new Vector3(i % 2 == 0 ? 1f : -1f, i < 2 ? 1f : -1f, 1f);
            }
        }
        float pad = 0.08f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.08f;
        Vector3[] corners =
        {
            new Vector3(head.min.x - pad, head.max.y + pad), new Vector3(head.max.x + pad, head.max.y + pad),
            new Vector3(head.min.x - pad, head.min.y - pad), new Vector3(head.max.x + pad, head.min.y - pad),
        };
        bool under = RowdyUnderFeet(BodyBounds);
        for (int i = 0; i < 4; i++)
        {
            brackets[i].enabled = near;
            brackets[i].transform.position = corners[i];
            brackets[i].color = under ? (Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? new Color(1f, 0.3f, 0.25f) : Color.white) : new Color(1f, 0.85f, 0.3f, 0.85f);
        }
        if (under && near) ShowHeadHint(false);
    }

    // 6x6 L-shaped corner (top-left; the others are mirrored)
    private static Sprite BracketSprite
    {
        get
        {
            if (bracketSprite != null) return bracketSprite;
            Sprite raw = OverlayUI.PixelSprite(new[] { "WWWWWW", "WWWWWW", "WW....", "WW....", "WW....", "WW...." },
                                               ch => new Color32(255, 255, 255, 255), "PelichBracket");
            bracketSprite = Sprite.Create(raw.texture, raw.rect, new Vector2(0f, 1f), 32f);
            return bracketSprite;
        }
    }

    private void OnDestroy()
    {
        if (brackets != null) foreach (SpriteRenderer b in brackets) if (b != null) Destroy(b.gameObject);
        if (headArrow != null) Destroy(headArrow.gameObject);
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
        StopAllCoroutines();
        if (stomping)
        {
            transform.localScale = stompBaseScale;
            if (TryGetComponent(out SpriteRenderer pr)) pr.color = stompBaseColor;
            stomping = false;
        }
        if (brackets != null) foreach (SpriteRenderer b in brackets) if (b != null) b.enabled = false;
        if (headArrow != null) { headArrow.Stop(0.2f); headArrow = null; }
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
