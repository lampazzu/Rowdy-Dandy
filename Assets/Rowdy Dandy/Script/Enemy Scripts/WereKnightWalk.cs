using UnityEngine;

// WEREKNIGHT WALK: the knight's sheet has no walk frames (it slid around on its idle). While it moves it now drops
// into the shadow-wolf run cycle (TDF werewolf frames 0-3, the same walk the Moonbound Elder and Rowdy's werewolf
// use), tinted in the knight's violet, with a little dark smoke where it shifts. Standing, attacking, hurt or dying =
// its own animator, untouched. It only really walks inside a colosseum trial (see Update). Added by EnemyHealth.Start
// to every Wereknight.
public class WereKnightWalk : MonoBehaviour
{
    private const int CellH = 108;
    private const float FeetRow = 6f; // the wolf's feet: 6 px above the bottom of every cell
    private static readonly Color Tint = new Color(0.78f, 0.62f, 1f);

    private EnemyHealth health;
    private SpriteRenderer body;
    private Animator anim;
    private Rigidbody2D rb;
    private Sprite[] frames;
    private bool walking, baseFlip;
    private Color baseColor;
    private float walkStarted, smokeTimer, movingFor;

    public static void TryAttach(EnemyHealth e)
    {
        if (e == null || e.IsObject || e.GetComponent<WereKnightWalk>() != null) return;
        EnemyCatalog.Entry kind = EnemyCatalog.Identify(e);
        if (kind == null || kind.id != "wereknight") return;
        e.gameObject.AddComponent<WereKnightWalk>();
    }

    private void Start()
    {
        health = GetComponent<EnemyHealth>();
        body = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        if (body != null) { baseFlip = body.flipX; baseColor = body.color; }
        ItemArt art = ItemArt.Get;
        if (art == null || art.werewolf == null || body == null) return; // no wolf frames: it still walks in the arena
        float scaleY = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        float ppu = 64f * scaleY; // the game's own pixel size, whatever the prefab scale
        float colliderBottom = transform.position.y;
        foreach (Collider2D c in GetComponents<Collider2D>()) if (c.enabled && !c.isTrigger) { colliderBottom = c.bounds.min.y; break; }
        float localFeet = (colliderBottom - transform.position.y) / scaleY;
        frames = ItemArt.Frames(art.werewolf, 15, 1, new Vector2(0.5f, (FeetRow - localFeet * ppu) / CellH), ppu);
    }

    private bool InIdleClip()
    {
        if (anim == null || !anim.isActiveAndEnabled) return true;
        AnimatorClipInfo[] info = anim.GetCurrentAnimatorClipInfo(0);
        if (info.Length == 0 || info[0].clip == null) return true;
        string n = info[0].clip.name.ToLowerInvariant();
        return n.Contains("idle") || n.Contains("walk") || n.Contains("move");
    }

    // ---------------------------------------------------------------- colosseum only: it actually walks
    // The prefab's speed is 0.0001 (a statue that thrusts). Inside a colosseum during a trial it stalks Rowdy at a
    // knight's pace instead, and stands still to thrust once he's inside its reach (its attack clips don't stop it).
    private const float ArenaSpeed = 1.5f;
    private EnemyMovement mover;
    private MeleeEnemy melee;
    private float baseSpeed = -1f;
    private bool arenaWalking;

    private void Update()
    {
        if (mover == null) { mover = GetComponent<EnemyMovement>(); melee = GetComponent<MeleeEnemy>(); }
        if (mover == null) return;
        if (baseSpeed < 0f) baseSpeed = mover.moveSpeed;
        bool arena = FrontierArena.ArenaAt(transform.position) != null && health != null && !health.enemydead;
        if (!arena)
        {
            if (arenaWalking) { arenaWalking = false; mover.moveSpeed = baseSpeed; } // back to a statue outside
            return;
        }
        arenaWalking = true;
        float reach = melee != null ? melee.AttackReach * 0.7f : 1.2f;
        float dx = BoonRunner.Rowdy != null ? Mathf.Abs(BoonRunner.RowdyCenter.x - transform.position.x) : 0f;
        bool hold = !InIdleClip() || dx < reach || StatusEffects.IsStunned(gameObject);
        mover.moveSpeed = hold ? 0f : ArenaSpeed;
    }

    private void LateUpdate()
    {
        if (frames == null || body == null) return;
        bool dead = health == null || health.enemydead;
        float vx = rb != null ? rb.linearVelocity.x : 0f;
        bool moving = !dead && Mathf.Abs(vx) > 0.35f && !StatusEffects.IsStunned(gameObject) && InIdleClip();
        movingFor = moving ? movingFor + Time.deltaTime : 0f;
        bool want = movingFor > 0.06f; // a one-frame nudge doesn't flicker it into the wolf

        if (want != walking)
        {
            walking = want;
            walkStarted = Time.time;
            Smoke(8);
            if (!walking) { body.flipX = baseFlip; body.color = baseColor; return; }
        }
        if (!walking) return;

        // the run cycle, quicker the faster it goes (the art faces LEFT: flip it toward where it's heading)
        float now = Time.time;
        body.sprite = frames[(int)(now * Mathf.Lerp(11f, 17f, Mathf.Clamp01(Mathf.Abs(vx) / 5f))) % 4];
        float lossy = Mathf.Sign(transform.lossyScale.x);
        body.flipX = -lossy != Mathf.Sign(vx);
        float shift = Mathf.Clamp01((now - walkStarted) / 0.12f);
        body.color = Color.Lerp(Color.white, Tint, shift);

        smokeTimer -= Time.deltaTime;
        if (smokeTimer <= 0f)
        {
            smokeTimer = 0.08f;
            Bounds b = body.bounds;
            FXParticle.Burst(new Vector3(b.center.x - Mathf.Sign(vx) * b.extents.x * 0.5f, b.min.y + 0.1f, 0f), new Color(0.35f, 0.2f, 0.5f, 0.8f), 1, 0.2f, 0.7f, -1f, 0.45f);
        }
    }

    private void Smoke(int n)
    {
        if (body == null) return;
        FXParticle.Burst(body.bounds.center, new Color(0.4f, 0.25f, 0.6f, 0.85f), n, 0.6f, 2f, -0.5f, 0.45f);
    }
}
