using System.Collections;
using UnityEngine;

// THE MOONBOUND ELDER - what the old man really is. Hurt him and the curse wakes up.
// Built at runtime from the Big Wolf (Resources/Enemies Prefab/Enemy_BigWerewolf): its AI, leaps, claws, hitboxes and
// counter windows stay, but it LOOKS like the TDF shadow werewolf (ItemArt.werewolf, 15 frames: 0-3 rising out of the
// ground... used as the walk cycle: dark run frames 0-3, 4-11 prowl / idle loop, 12-13 lunge, 14 recover) - the Big Wolf animator still runs and picks which frames show.
//   - 1.5x bigger, a pulsing moon-glow outline (red in phase 2), 450 HP, faster
//   - crawls up out of the ground when it appears, sinks back into it when it dies
//   - MOON NOVA: crouches and glows (telegraph), then a ring of explosions around him. Jump away or eat 15.
//   - HOWL at half health: hit-stop, "AWOOOO!", calls two Werefasts and goes red, faster, novas more often
//   - dies in a long chain of explosions and a shower of EXP
// Spawned by EnemyHealth when the old man dies (isOldMan + oldManBecomesElder).
public class CursedElder : MonoBehaviour
{
    public const string ObjectName = "MoonboundElder";

    [Header("Body")]
    [Tooltip("Root scale (hitbox). The art is always drawn at the game's 64 px per unit, whatever this is.")]
    [SerializeField] private float size = 1f;
    [SerializeField] private float maxHealth = 450f;
    [SerializeField] private float speedMultiplier = 1.35f;
    [SerializeField] private Color skin = Color.white;
    [SerializeField] private Color glow = new Color(0.65f, 0.55f, 1f, 1f);
    [SerializeField] private Color rageGlow = new Color(1f, 0.25f, 0.35f, 1f);
    [SerializeField] private int expGems = 30;

    [Header("Moon Nova")]
    [SerializeField] private float novaCooldown = 7f;
    [SerializeField] private float rageNovaCooldown = 4.5f;
    [SerializeField] private float novaTelegraph = 0.85f;
    [SerializeField] private float novaRadius = 2.6f;
    [SerializeField] private float novaDamage = 15f;
    [SerializeField] private float novaTriggerRange = 6f;

    private EnemyHealth health;
    private EnemyMovement movement;
    private Animator animator;
    private SpriteRenderer body;
    private Transform rowdy;
    private SpriteRenderer[] outline;
    private bool raging, dead, busy;
    private float novaTimer = 3f;
    private float baseAnimSpeed = 1f;
    private float flash; // 0..1 extra white on the outline (telegraph)

    // TDF werewolf frames
    private const int CellW = 156, CellH = 108;
    private const float FeetRow = 6f; // the wolf's feet: 6 px above the bottom of every cell
    private Sprite[] frames;
    private float spawnedAt, diedAt = -1f;

    // Rowdy Notes / kill feed: portrait + animated portrait from the same sheet (no generated PNGs needed)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterPortrait()
    {
        ItemArt art = ItemArt.Get;
        EnemyCatalog.Entry entry = EnemyCatalog.Get("moonboundelder");
        if (art == null || art.werewolf == null || entry == null) return;
        Texture2D tex = art.werewolf;
        tex.filterMode = FilterMode.Point;
        var clip = new AnimatedPortraits.Clip { frames = new Sprite[8], durations = new float[8] };
        for (int i = 0; i < 8; i++)
        {
            // square around the wolf in prowl frames 4-11
            clip.frames[i] = Sprite.Create(tex, new Rect((4 + i) * CellW + 32, 0, 92, 92), new Vector2(0.5f, 0.5f), 64f);
            clip.durations[i] = 0.1f;
            clip.length += 0.1f;
        }
        entry.fallbackPortrait = clip.frames[0];
        AnimatedPortraits.Register("moonboundelder", clip);
    }

    public static GameObject Spawn(Vector3 position)
    {
        GameObject prefab = Resources.Load<GameObject>("Enemies Prefab/Enemy_BigWerewolf");
        if (prefab == null) return null;

        // Built under an inactive holder so nothing (catalog, AI, notes) sees it as a Big Wolf before it's renamed
        var holder = new GameObject("Elder Spawn (inactive)");
        holder.SetActive(false);
        GameObject root = Instantiate(prefab, position + Vector3.up * 0.3f, Quaternion.identity, holder.transform);
        root.name = ObjectName;
        EnemyHealth h = root.GetComponentInChildren<EnemyHealth>(true);
        if (h == null) { Destroy(holder); return null; }
        h.gameObject.name = ObjectName;
        h.gameObject.AddComponent<CursedElder>();
        root.transform.SetParent(null, true);
        Destroy(holder);
        return root;
    }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        movement = GetComponent<EnemyMovement>();
        animator = GetComponent<Animator>();
        body = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        Transform root = transform.root;
        root.localScale = Vector3.Scale(root.localScale, new Vector3(size, size, 1f));
        if (health != null)
        {
            health.SetMaxHealth(maxHealth);
            health.SetExpDropIfMissing(Resources.Load<GameObject>("Systems/EXPgem"), expGems);
        }
        if (movement != null) movement.moveSpeed *= speedMultiplier;
        if (animator != null) { baseAnimSpeed = animator.speed * 1.25f; animator.speed = baseAnimSpeed; }
        if (body != null) body.color = skin;
        SetupFrames();
        spawnedAt = Time.time;
        BuildOutline();

        // The reveal
        ExplosionChain.Boom(transform.position, 0.9f, 0.6f, 0.8f);
        SoundManager.PlaySfx(Resources.Load<AudioClip>("Sounds/Howl"), 0.9f);
        IconPopup.Show(transform.position + Vector3.up * 1.6f, null, "THE CURSE AWAKENS", glow, 1.3f, 2.6f);
        TimeSlowController.HitStop(0.15f, 0.1f);
    }

    private void Update()
    {
        if (health == null) return;
        if (health.enemydead) { if (!dead) StartCoroutine(Die()); return; }

        if (rowdy == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) rowdy = p.transform;
        }

        // Phase 2
        if (!raging && health.currentenemyHealth <= maxHealth * 0.5f) StartCoroutine(Howl());

        if (body != null && !busy) body.color = raging ? Color.Lerp(skin, rageGlow, 0.35f) : skin;

        novaTimer -= Time.deltaTime;
        if (!busy && novaTimer <= 0f && rowdy != null && Vector2.Distance(rowdy.position, transform.position) < novaTriggerRange)
            StartCoroutine(MoonNova());
    }

    private void LateUpdate()
    {
        ShowFrame();
        UpdateOutline();
    }

    private void SetupFrames()
    {
        ItemArt art = ItemArt.Get;
        if (art == null || art.werewolf == null || body == null) return;
        // Same pixel size as the rest of the game (64 px per world unit), whatever the root scale is
        float scaleY = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        float ppu = 64f * scaleY;
        // Feet on the bottom of the body collider (the hitbox), not floating above it
        float colliderBottom = transform.position.y;
        foreach (Collider2D c in GetComponents<Collider2D>()) if (c.enabled && !c.isTrigger) { colliderBottom = c.bounds.min.y; break; }
        float localFeet = (colliderBottom - transform.position.y) / scaleY; // local units, usually <= 0
        float pivotPixels = FeetRow - localFeet * ppu;
        frames = ItemArt.Frames(art.werewolf, 15, 1, new Vector2(0.5f, pivotPixels / CellH), ppu);
        body.sprite = frames[4];
    }

    // The Big Wolf animator keeps running (hitboxes, counter windows live in its clips); here its current
    // clip picks which werewolf frames to draw, after it has set its own sprite this frame.
    private void ShowFrame()
    {
        if (frames == null || body == null) return;
        float now = Time.time;

        if (diedAt >= 0f) // staggers on the recover frame and fades into the dark
        {
            body.sprite = frames[14];
            body.color = new Color(skin.r, skin.g, skin.b, Mathf.Clamp01(1.4f - (now - diedAt) / 0.8f));
            return;
        }
        if (now - spawnedAt < 0.5f) // out of the shadows: fades in on the lunge pose
        {
            body.sprite = frames[(now - spawnedAt) < 0.25f ? 12 : 13];
            body.color = new Color(skin.r, skin.g, skin.b, Mathf.Clamp01((now - spawnedAt) / 0.4f));
            return;
        }

        string clip = "";
        if (animator != null && animator.isActiveAndEnabled)
        {
            AnimatorClipInfo[] info = animator.GetCurrentAnimatorClipInfo(0);
            if (info.Length > 0 && info[0].clip != null) clip = info[0].clip.name.ToLowerInvariant();
        }
        float normalized = animator != null && animator.isActiveAndEnabled ? Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f) : 0f;

        if (clip.Contains("attack") || clip.Contains("slash") || clip.Contains("swipe") || clip.Contains("claw"))
            body.sprite = frames[normalized < 0.45f ? 12 : normalized < 0.8f ? 13 : 14];
        else if (clip.Contains("jump") || clip.Contains("leap") || clip.Contains("fall") || clip.Contains("air"))
            body.sprite = frames[13];
        else if (clip.Contains("hit") || clip.Contains("hurt"))
            body.sprite = frames[14];
        else
        {
            // walking: the dark run cycle (frames 0-3), quick; standing: the prowl loop (4-11)
            float speed = TryGetComponent(out Rigidbody2D rb) ? Mathf.Abs(rb.linearVelocity.x) : 0f;
            if (speed > 0.3f) body.sprite = frames[(int)(now * Mathf.Lerp(12f, 18f, Mathf.Clamp01(speed / 6f))) % 4];
            else body.sprite = frames[4 + (int)(now * 12f) % 8];
        }
    }

    // ---------------------------------------------------------------- moves
    private IEnumerator MoonNova()
    {
        busy = true;
        novaTimer = raging ? rageNovaCooldown : novaCooldown;

        // Telegraph: freeze in place, glow brighter and brighter, little sparks of the ring showing where it'll hit
        bool moved = movement != null && movement.enabled;
        if (moved) movement.enabled = false;
        if (TryGetComponent(out Rigidbody2D rb)) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animator != null) animator.speed = 0.25f;
        SoundManager.PlaySfx(Resources.Load<AudioClip>("Sounds/Slam"), 0.5f);

        float t = 0f;
        while (t < novaTelegraph && !health.enemydead)
        {
            t += Time.deltaTime;
            flash = t / novaTelegraph;
            if (body != null) body.color = Color.Lerp(skin, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 40f));
            yield return null;
        }
        flash = 0f;

        if (!health.enemydead)
        {
            // The ring
            Vector3 c = transform.position;
            const int count = 10;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                ExplosionChain.Boom(c + new Vector3(Mathf.Cos(a) * novaRadius * 0.8f, Mathf.Sin(a) * novaRadius * 0.35f + 0.2f, 0f), 0.45f, 0.12f, 0.05f);
            }
            ExplosionChain.Boom(c, 0.8f, 0.5f, 0.7f);
            if (rowdy != null && Vector2.Distance(rowdy.position, c) < novaRadius && rowdy.TryGetComponent(out Health rowdyHealth))
                rowdyHealth.TakeDamage(novaDamage);
        }

        if (animator != null) animator.speed = raging ? baseAnimSpeed * 1.2f : baseAnimSpeed;
        if (moved && movement != null) movement.enabled = true;
        yield return new WaitForSeconds(0.4f);
        busy = false;
    }

    private IEnumerator Howl()
    {
        raging = true;
        busy = true;
        TimeSlowController.HitStop(0.25f, 0.08f);
        SoundManager.PlaySfx(Resources.Load<AudioClip>("Sounds/Howl"), 1f);
        ScreenShake.Impulse(1f);
        GamepadRumble.Pulse(0.7f, 0.9f, 0.5f);
        health.ShowCustomText("AWOOOOO!", rageGlow);

        // Children of the moon
        GameObject pup = Resources.Load<GameObject>("Enemies Prefab/Enemy_Werefast");
        if (pup != null)
        {
            foreach (float side in new[] { -2.2f, 2.2f })
            {
                Vector3 at = transform.position + new Vector3(side, 0.5f, 0f);
                ExplosionChain.Boom(at, 0.5f, 0.2f, 0.1f);
                Instantiate(pup, at, Quaternion.identity);
            }
        }

        if (movement != null) movement.moveSpeed *= 1.2f;
        if (animator != null) animator.speed = baseAnimSpeed * 1.2f;
        yield return new WaitForSeconds(0.6f);
        novaTimer = 1.5f;
        busy = false;
    }

    private IEnumerator Die()
    {
        dead = true;
        diedAt = Time.time;
        if (outline != null) foreach (SpriteRenderer o in outline) if (o != null) o.enabled = false;
        ExplosionChain.Play(transform.position + Vector3.up * 0.4f, 26, 1.4f, 2f, 0.45f);
        SoundManager.PlaySfx(Resources.Load<AudioClip>("Sounds/Howl"), 0.7f);
        yield break;
    }

    // ---------------------------------------------------------------- moon-glow outline
    private void BuildOutline()
    {
        if (body == null) return;
        Vector2[] offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down, new Vector2(-1, -1), new Vector2(1, 1) };
        outline = new SpriteRenderer[offsets.Length];
        // 1 art pixel of the drawn sprite, in this object's local space
        float px = 1f / (body.sprite != null ? body.sprite.pixelsPerUnit : 64f);
        for (int i = 0; i < offsets.Length; i++)
        {
            var go = new GameObject("Elder Glow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (Vector3)(offsets[i] * px);
            var sr = go.AddComponent<SpriteRenderer>();
            if (CatFX.Silhouette != null) sr.sharedMaterial = CatFX.Silhouette;
            outline[i] = sr;
        }
    }

    private void UpdateOutline()
    {
        if (outline == null || body == null || dead) return;
        Color c = raging ? rageGlow : glow;
        float pulse = 0.55f + 0.35f * Mathf.Sin(Time.time * (raging ? 9f : 4f));
        c = Color.Lerp(c, Color.white, flash);
        c.a = Mathf.Clamp01(pulse + flash * 0.5f) * (body.enabled ? 1f : 0f);
        foreach (SpriteRenderer sr in outline)
        {
            sr.sprite = body.sprite;
            sr.flipX = body.flipX;
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder - 1;
            sr.color = c;
        }
    }
}
