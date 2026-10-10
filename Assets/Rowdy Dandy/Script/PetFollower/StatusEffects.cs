using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Status effects for the new cats (placeholder art = tinted Wig, user swaps it later):
//   Paprika   - poison imbue: for a few seconds Rowdy's hits poison enemies (damage over time, green)
//   Mushidon  - stomp: every enemy standing on the ground nearby is stunned for 1 s
//   The Peak  - armor: blocks the next 3 hits Rowdy takes
//   Lallo     - decay: the next enemy Rowdy kills bursts in decay; enemies caught in it burst too (chain reaction)
// RowdyBuffs = the buffs on Rowdy (one per scene, lives on Rowdy). StatusEffects = the effects on one enemy.

// --------------------------------------------------------------------------------------------------- on Rowdy
public class RowdyBuffs : MonoBehaviour
{
    private static RowdyBuffs instance;

    // ---- The Peak's armor
    public const int ArmorHits = 3;
    private int charges;
    private PetFollower armorOwner;
    private SpriteRenderer shieldIcon;
    private Sprite[] shieldFrames;
    private SpriteOutline armorOutline;
    private float shieldPop;

    // ---- Paprika's poison
    private float poisonUntil, poisonLength;
    private PetFollower poisonOwner;
    private SpriteOutline poisonOutline;
    private float dripTimer;

    // ---- Lallo's decay
    private PetFollower decayOwner;
    private SpriteOutline decayOutline;
    private float wispTimer;

    private SpriteRenderer body;
    private Collider2D bodyCollider;

    public static int ShieldCharges => instance != null ? instance.charges : 0;
    public static PetFollower ShieldOwner => instance != null && instance.charges > 0 ? instance.armorOwner : null;
    public static bool PoisonActive => instance != null && Time.time < instance.poisonUntil;
    public static PetFollower PoisonOwner => PoisonActive ? instance.poisonOwner : null;
    public static float PoisonLeftFraction => PoisonActive ? (instance.poisonUntil - Time.time) / Mathf.Max(0.01f, instance.poisonLength) : 0f;
    public static PetFollower DecayArmedBy => instance != null ? instance.decayOwner : null;

    private static RowdyBuffs Get()
    {
        if (instance != null) return instance;
        Health h = FindFirstObjectByType<Health>(); // Rowdy's body (his TriggerCollider child is tagged Player too)
        if (h == null) return null;
        instance = h.GetComponent<RowdyBuffs>();
        if (instance == null) instance = h.gameObject.AddComponent<RowdyBuffs>();
        return instance;
    }

    private void Awake()
    {
        instance = this;
        body = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private Vector3 Center => bodyCollider != null ? bodyCollider.bounds.center : transform.position + Vector3.up * 0.4f;
    // Where the block shield is drawn: halfway up from his middle to his head, so it covers his upper body instead of his legs
    private Vector3 ShieldSpot => Vector3.Lerp(Center, HeadTop, 0.5f);
    private Vector3 HeadTop => bodyCollider != null ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.max.y, 0f) : transform.position + Vector3.up * 0.9f;

    // ================================================================ armor (The Peak)
    public static int MaxCharges => Boons.Has("peakplate") ? ArmorHits * 3 : ArmorHits; // Triple Plated: 3 armors at once
    public static void GiveArmor(PetFollower owner, int hits = ArmorHits)
    {
        RowdyBuffs b = Get();
        if (b == null) return;
        if (hits == ArmorHits) hits = MaxCharges;
        b.charges = hits;
        b.armorOwner = owner;
        b.shieldPop = 0.35f;
        ItemArt art = ItemArt.Get;
        if (art != null) SheetFX.Play(art.vfxBlock, 10, b.ShieldSpot, 22f, 64f, b.body != null ? b.body.sortingOrder + 3 : 90, b.transform, false, null, 0.55f);
        PulseRing.Spawn(b.Center, new Color(0.6f, 0.9f, 1f, 0.9f), 1.1f, 0.35f);
        FXSound.Play("ShieldUp", 0.9f, 1.1f);
        IconPopup.Show(b.HeadTop + Vector3.up * 0.3f, b.ShieldFrame(0), "ARMOR X" + hits, new Color(0.65f, 0.9f, 1f), 0.9f, 1.3f);
    }

    // Called by Health.TakeDamage: true = the hit was blocked
    public static bool TryBlock(Health health)
    {
        RowdyBuffs b = instance;
        if (b == null || b.charges <= 0) return false;
        b.charges--;
        RunStats.HitsBlocked++;
        b.shieldPop = 0.3f;
        TimeSlowController.HitStop(0.07f, 0.05f);
        ScreenShake.Impulse(0.35f);
        GamepadRumble.Pulse(0.4f, 0.7f, 0.12f);
        PulseRing.Spawn(b.Center, new Color(0.7f, 0.95f, 1f, 1f), 0.9f, 0.25f);
        FXParticle.Burst(b.Center, new Color(0.75f, 0.95f, 1f), 10, 1.5f, 3.5f, 6f, 0.45f);
        ItemArt art = ItemArt.Get;
        if (art != null) SheetFX.Play(art.vfxBlock, 10, b.ShieldSpot, 28f, 64f, b.body != null ? b.body.sortingOrder + 3 : 90, b.transform, false, null, 0.45f);
        if (b.charges > 0)
        {
            FXSound.Play("ShieldBreak", 0.6f, 1.45f);
            IconPopup.Show(b.HeadTop + Vector3.up * 0.25f, null, "BLOCKED!", new Color(0.65f, 0.9f, 1f), 0.8f, 0.9f);
        }
        else
        {
            FXSound.Play("ShieldBreak", 1f, 0.85f);
            IconPopup.Show(b.HeadTop + Vector3.up * 0.25f, b.ShieldFrame(5), "ARMOR BROKE!", new Color(1f, 0.6f, 0.5f), 0.9f, 1.3f);
            FXParticle.Burst(b.HeadTop + Vector3.up * 0.3f, new Color(0.85f, 0.85f, 0.95f), 14, 2f, 4f, 9f, 0.6f);
        }
        return true;
    }

    private Sprite ShieldFrame(int i)
    {
        if (shieldFrames == null)
        {
            ItemArt art = ItemArt.Get;
            if (art == null || art.stsShield == null) return null;
            shieldFrames = ItemArt.Frames(art.stsShield, 6, 1, new Vector2(0.5f, 0f), 64f);
        }
        return shieldFrames[Mathf.Clamp(i, 0, shieldFrames.Length - 1)];
    }

    // ================================================================ poison imbue (Paprika)
    public static void StartPoison(PetFollower owner, float seconds)
    {
        RowdyBuffs b = Get();
        if (b == null) return;
        b.poisonOwner = owner;
        b.poisonLength = seconds;
        b.poisonUntil = Time.time + seconds;
        ItemArt art = ItemArt.Get;
        if (art != null) SheetFX.Play(art.vfxPoison, 10, b.Center, 20f, 64f, b.body != null ? b.body.sortingOrder + 3 : 90, b.transform, false, null, 0.6f);
        PulseRing.Spawn(b.Center, new Color(0.55f, 1f, 0.3f, 0.9f), 1.2f, 0.4f);
        FXParticle.Burst(b.Center, new Color(0.5f, 1f, 0.35f), 16, 1f, 3f, -1f, 0.7f);
        GraftFX.Play("spr_bug_special_whirlwindCut", b.Center, PoisonGreen, (b.body != null ? b.body.sortingOrder : 80) + 4, b.transform);
        if (owner != null) GraftFX.Play("StephHit", owner.transform.position, PoisonGreen, 96);
        FXSound.Play("Poison", 0.9f, 1f);
        IconPopup.Show(b.HeadTop + Vector3.up * 0.3f, null, "POISON BLADE!", new Color(0.55f, 1f, 0.35f), 1f, 1.4f);
    }

    public static void OnRowdyHit(EnemyHealth enemy)
    {
        if (!PoisonActive || enemy == null || enemy.enemydead || enemy.IsObject) return;
        StatusEffects.Of(enemy).Poison(4f, 6f + enemy.startingenemyHealth * 0.02f, instance.poisonOwner);
        // Paprika's poison shows on every hit: a green venom slash (Graft cut) and drips, a puddle under the enemy.
        // The cut swirls BEHIND the enemy (it used to cover it).
        Vector3 c = EnemyFairness.BodyCenter(enemy);
        SheetFX cut = GraftFX.Play("spr_bug_special_whirlwindCut", c, PoisonGreen, StatusEffects.BehindOrder(enemy), null, false, Random.value < 0.5f, 1.4f);
        FXParticle.Burst(c, PoisonGreen, 8, 1f, 3f, 7f, 0.5f, true);
        if (Time.time - instance.lastPuddle > 0.4f && SolidGround.Ray(c, Vector2.down, 3f, out RaycastHit2D hit))
        {
            instance.lastPuddle = Time.time;
            GraftFX.Play("StephPoolt", hit.point + Vector2.up * 2f / 64f, new Color(0.55f, 1f, 0.3f, 0.85f), Mathf.Min(70, StatusEffects.BehindOrder(enemy) - 1), null, false, false, 0.7f, new Vector2(0.5f, 0f));
        }
    }

    private static readonly Color PoisonGreen = new Color(0.55f, 1f, 0.3f);
    private float lastPuddle = -10f;

    // ================================================================ decay (Lallo)
    public static void ArmDecay(PetFollower owner)
    {
        RowdyBuffs b = Get();
        if (b == null) return;
        b.decayOwner = owner;
        FXSound.Play("DecayArm", 0.7f, 1f);
        PulseRing.Spawn(b.Center, new Color(0.7f, 0.35f, 1f, 0.9f), 0.9f, 0.4f);
        IconPopup.Show(b.HeadTop + Vector3.up * 0.3f, null, "DECAY READY", new Color(0.8f, 0.5f, 1f), 0.8f, 1.2f);
    }

    public static void OnEnemyKilled(EnemyHealth enemy, bool byRowdySide)
    {
        if (!byRowdySide || instance == null || instance.decayOwner == null || enemy == null || enemy.IsObject) return;
        PetFollower owner = instance.decayOwner;
        instance.decayOwner = null;
        CatPowers.DecayBurst(EnemyFairness.BodyCenter(enemy), owner, null, 0, new HashSet<EnemyHealth> { enemy }, new[] { 1 });
    }

    // ================================================================ visuals
    private void LateUpdate()
    {
        if (body == null) return;
        float t = Time.time;

        // Armor: outline + the shield icon over his head (cracks as it's spent)
        if (armorOutline == null) armorOutline = SpriteOutline.Add(body, Color.clear, 1, -3);
        armorOutline.color = charges > 0 ? new Color(0.7f, 0.92f, 1f, 0.45f + 0.25f * Mathf.Sin(t * 5f)) : Color.clear;
        if (charges > 0 || shieldPop > 0f)
        {
            if (shieldIcon == null)
            {
                var go = new GameObject("Armor Icon");
                shieldIcon = go.AddComponent<SpriteRenderer>();
                shieldIcon.sortingLayerID = body.sortingLayerID;
                if (CatFX.Unlit != null) shieldIcon.sharedMaterial = CatFX.Unlit;
            }
            shieldPop = Mathf.Max(0f, shieldPop - Time.deltaTime);
            int frame = charges >= 3 ? (int)(t * 6f) % 3 : charges == 2 ? 3 : charges == 1 ? 4 : 5;
            shieldIcon.sprite = ShieldFrame(frame);
            shieldIcon.sortingOrder = body.sortingOrder + 6;
            shieldIcon.enabled = shieldIcon.sprite != null && (charges > 0 || Mathf.Repeat(shieldPop * 20f, 1f) < 0.5f);
            float pop = 1f + shieldPop * 1.4f;
            float bob = Mathf.Round(Mathf.Sin(t * 3f) * 1.5f) / 64f;
            shieldIcon.transform.position = HeadTop + new Vector3(0f, 0.12f + bob, 0f);
            shieldIcon.transform.localScale = Vector3.one * pop;
        }
        else if (shieldIcon != null) shieldIcon.enabled = false;

        // Poison: green outline + drips
        bool poisoned = PoisonActive;
        if (poisonOutline == null) poisonOutline = SpriteOutline.Add(body, Color.clear, 1, -2);
        float left = PoisonLeftFraction;
        bool blinkOut = left < 0.25f && Mathf.Repeat(t * 8f, 1f) < 0.4f; // about to run out
        poisonOutline.color = poisoned && !blinkOut ? new Color(0.45f, 1f, 0.3f, 0.6f + 0.3f * Mathf.Sin(t * 9f)) : Color.clear;
        if (poisoned)
        {
            dripTimer -= Time.deltaTime;
            if (dripTimer <= 0f)
            {
                dripTimer = 0.12f;
                Vector3 p = Center + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.3f, 0.3f), 0f);
                FXParticle.Burst(p, new Color(0.45f, 1f, 0.3f), 1, 0.1f, 0.4f, 3f, 0.5f);
            }
        }

        // Decay ready: faint purple shimmer + rising wisps
        if (decayOutline == null) decayOutline = SpriteOutline.Add(body, Color.clear, 1, -4);
        decayOutline.color = decayOwner != null ? new Color(0.65f, 0.3f, 1f, 0.25f + 0.2f * Mathf.Sin(t * 4f)) : Color.clear;
        if (decayOwner != null)
        {
            wispTimer -= Time.deltaTime;
            if (wispTimer <= 0f)
            {
                wispTimer = 0.25f;
                Vector3 p = Center + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.35f, 0.1f), 0f);
                FXParticle.Burst(p, new Color(0.7f, 0.4f, 1f, 0.8f), 1, 0.2f, 0.5f, -1.5f, 0.8f);
            }
        }
    }
}

// --------------------------------------------------------------------------------------------------- on enemies
// Runs after the enemy AI (it sets velocities in its own updates), so stun / slow get the last word.
[DefaultExecutionOrder(50)]
public class StatusEffects : MonoBehaviour
{
    private static readonly HashSet<GameObject> stunned = new HashSet<GameObject>();
    private static Sprite[] stunFrames, charmFrames, fearFrames, slowFrames;

    // Boons reuse the stun (the AI stops) with their own look: charmed (pink hearts), feared, rooted (vines)
    public enum HoldKind { Stun, Charm, Fear, Root }
    private HoldKind holdKind;
    private float heartTimer, holdStartedAt;
    private readonly List<SpriteRenderer> rootVines = new List<SpriteRenderer>();

    // slow (Undertow)
    private float slowUntil, slowFactor = 1f;
    private SpriteRenderer slowIcon;

    public bool IsPoisoned => Time.time < poisonUntil;
    public bool IsSlowed => Time.time < slowUntil;
    public bool IsCharmed => holdKind == HoldKind.Charm && IsStunned(gameObject);

    private EnemyHealth health;
    private SpriteRenderer body;
    private Animator anim;
    private Rigidbody2D rb;
    private Color baseColor = Color.white;
    private bool tinting;

    // poison: every ability that poisons adds its OWN stack (they add up); the same ability again refreshes its stack
    private struct PoisonStack { public float until, dps; public PetFollower source; }
    private readonly Dictionary<int, PoisonStack> poisonStacks = new Dictionary<int, PoisonStack>();
    private float poisonUntil, nextTick;
    private float bubbleTimer;
    private int shownStacks;
    private SpriteRenderer poisonOverlay;

    public int PoisonStacks
    {
        get
        {
            int n = 0;
            foreach (PoisonStack s in poisonStacks.Values) if (Time.time < s.until) n++;
            return n;
        }
    }

    // Sorting order just behind this enemy's body (effects that should swirl around it, not cover it)
    public static int BehindOrder(EnemyHealth e)
    {
        SpriteRenderer sr = e != null ? e.GetComponentInChildren<SpriteRenderer>() : null;
        return sr != null ? sr.sortingOrder - 1 : 40;
    }

    // stun
    private float stunUntil;
    private SpriteRenderer stunIcon;
    private float savedSpeed = 1f;
    private bool animFrozen;
    private float stunFlash;

    // decay
    public bool DecayInfected { get; private set; }

    public static StatusEffects Of(EnemyHealth enemy)
    {
        StatusEffects s = enemy.GetComponent<StatusEffects>();
        return s != null ? s : enemy.gameObject.AddComponent<StatusEffects>();
    }

    public static bool IsStunned(GameObject enemy) => stunned.Contains(enemy);

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        body = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        if (body != null) baseColor = body.color;
    }

    private void OnDisable() => EndStun();
    private void OnDestroy() { stunned.Remove(gameObject); OnDestroyBoonBits(); }

    private Vector3 Center => EnemyFairness.BodyCenter(this);
    // Where the block shield is drawn: halfway up from his middle to his head, so it covers his upper body instead of his legs
    private Vector3 ShieldSpot => Vector3.Lerp(Center, HeadTop, 0.5f);
    private Vector3 HeadTop => transform.position + Vector3.up * EnemyFairness.HeadHeight(this);

    // ---------------------------------------------------------------- poison
    // The stack is keyed by where the poison comes from (the calling line: Rotten Edge, Spore Step, Paprika... each
    // its own), so two different poison boons stack while one boon hitting again just refreshes its own.
    public void Poison(float seconds, float dps, PetFollower source,
                       [System.Runtime.CompilerServices.CallerFilePath] string file = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        => PoisonKeyed(file.GetHashCode() * 31 + line, seconds, dps, source);

    public void PoisonKeyed(int key, float seconds, float dps, PetFollower source)
    {
        if (health != null && health.enemydead) return;
        bool fresh = Time.time >= poisonUntil;
        float until = Time.time + seconds;
        if (poisonStacks.TryGetValue(key, out PoisonStack s) && Time.time < s.until)
        {
            s.until = Mathf.Max(s.until, until);
            s.dps = Mathf.Max(s.dps, dps);
            if (source != null) s.source = source;
        }
        else s = new PoisonStack { until = until, dps = dps, source = source };
        poisonStacks[key] = s;
        poisonUntil = Mathf.Max(poisonUntil, until);
        int stacks = PoisonStacks;
        if (fresh)
        {
            nextTick = Time.time + 0.5f;
            RunStats.EnemiesPoisoned++;
            ItemArt art = ItemArt.Get;
            // the green puff swirls behind the enemy (it used to cover the sprite)
            if (art != null) SheetFX.Play(art.vfxPoison, 10, Center, 22f, 64f, body != null ? body.sortingOrder - 1 : 40, transform, false, null, 0.5f);
            FXSound.Play("Poison", 0.35f, Random.Range(1.2f, 1.4f));
            FXParticle.Burst(Center, new Color(0.45f, 1f, 0.3f), 8, 0.8f, 2f, 2f, 0.5f);
        }
        else if (stacks > shownStacks && stacks > 1)
        {
            // a new kind of poison joined in: say how many are eating it now
            IconPopup.Show(HeadTop + Vector3.up * 0.2f, null, "POISON X" + stacks, new Color(0.55f, 1f, 0.35f), 0.6f, 0.8f);
            FXSound.Play("Poison", 0.25f, 1.5f + 0.1f * stacks);
        }
        shownStacks = Mathf.Max(shownStacks, stacks);
    }

    // Total poison damage per second right now (all the stacks), and whose stack is the strongest (kill credit)
    private float PoisonDps(out PetFollower source)
    {
        float total = 0f, best = -1f;
        source = null;
        foreach (PoisonStack s in poisonStacks.Values)
        {
            if (Time.time >= s.until) continue;
            total += s.dps;
            if (s.dps > best) { best = s.dps; source = s.source; }
        }
        return total;
    }

    // ---------------------------------------------------------------- stun
    public void Charm(float seconds)
    {
        // Toxic Beauty (Narcissism + Stephmoss): charmed = poisoned, poisoned = charmed longer
        if (Boons.Has("toxicbeauty") && health != null && !health.enemydead)
        {
            if (IsPoisoned) seconds *= 1.5f;
            Poison(4f, 6f + 0.6f * PlayerStats.Level, null);
        }
        bool fresh = holdKind != HoldKind.Charm || !IsStunned(gameObject);
        Stun(seconds);
        holdKind = HoldKind.Charm;
        if (fresh) holdStartedAt = Time.time;
        BoonArt art = BoonArt.Get;
        if (art != null) BoonArt.Play(art.charmSfx, 0.25f, Random.Range(1.1f, 1.3f));
    }

    public void Fear(float seconds)
    {
        bool fresh = holdKind != HoldKind.Fear || !IsStunned(gameObject);
        Stun(seconds);
        holdKind = HoldKind.Fear;
        if (fresh) holdStartedAt = Time.time;
    }

    public void Root(float seconds)
    {
        Stun(seconds);
        holdKind = HoldKind.Root;
        if (rootVines.Count > 0) return;
        BoonArt art = BoonArt.Get;
        Sprite vine = art != null && art.earthPillar != null ? ItemArt.Frames(art.earthPillar, 1, 1, new Vector2(0.5f, 0f), 64f)[0] : null;
        if (vine == null) return;
        Bounds b = body != null ? body.bounds : new Bounds(transform.position, Vector3.one * 0.6f);
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("Root Vine");
            go.transform.position = new Vector3(b.center.x + (i - 1) * b.extents.x * 0.7f, b.min.y - 0.03f, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, (i - 1) * -18f);
            go.transform.localScale = new Vector3(0.8f, 0f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = vine;
            sr.sortingLayerID = body != null ? body.sortingLayerID : 0;
            sr.sortingOrder = (body != null ? body.sortingOrder : 0) + 1;
            sr.color = new Color(0.55f, 0.85f, 0.4f);
            if (ItemArt.Lit != null) sr.sharedMaterial = ItemArt.Lit;
            rootVines.Add(sr);
        }
    }

    // Undertow: moves at (1 - factor) speed for a while
    public void Slow(float seconds, float factor)
    {
        if (health != null && health.enemydead) return;
        slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
        slowFactor = Mathf.Clamp01(1f - factor);
        if (slowIcon == null)
        {
            var go = new GameObject("Slow Icon");
            slowIcon = go.AddComponent<SpriteRenderer>();
            slowIcon.sortingLayerID = body != null ? body.sortingLayerID : 0;
            slowIcon.sortingOrder = (body != null ? body.sortingOrder : 0) + 29;
            if (CatFX.Unlit != null) slowIcon.sharedMaterial = CatFX.Unlit;
        }
    }

    public void Stun(float seconds)
    {
        if (health != null && health.enemydead) return;
        holdKind = HoldKind.Stun;
        stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
        stunFlash = 0.15f;
        stunned.Add(gameObject);
        // Back to Idle (keeps playing); no Idle state = held still on the first frame of Walk
        if (anim != null && !animFrozen && anim.isActiveAndEnabled)
        {
            savedSpeed = anim.speed <= 0f ? 1f : anim.speed;
            animFrozen = true;
            SetMoving(false);
            int idle = FirstState(IdleStates);
            if (idle != 0) { anim.Play(idle, 0, 0f); holdPose = false; }
            else
            {
                int walk = FirstState(WalkStates);
                if (walk != 0) anim.Play(walk, 0, 0f);
                holdPose = true;
                anim.speed = 0f;
            }
        }
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, Mathf.Min(rb.linearVelocity.y, 0f) + 1.5f); // little bump
        if (stunIcon == null)
        {
            var go = new GameObject("Stun Stars");
            stunIcon = go.AddComponent<SpriteRenderer>();
            stunIcon.sortingLayerID = body != null ? body.sortingLayerID : 0;
            stunIcon.sortingOrder = (body != null ? body.sortingOrder : 0) + 30;
            if (CatFX.Unlit != null) stunIcon.sharedMaterial = CatFX.Unlit;
        }
    }

    // Also called by EnemyHealth the moment the enemy dies, so the death animation always plays
    public void EndStun()
    {
        stunned.Remove(gameObject);
        stunUntil = 0f;
        if (animFrozen && anim != null && holdPose) anim.speed = savedSpeed <= 0f ? 1f : savedSpeed;
        animFrozen = false;
        holdPose = false;
        stunFlash = 0f;
        if (stunIcon != null) stunIcon.enabled = false;
        foreach (SpriteRenderer v in rootVines) if (v != null) Destroy(v.gameObject);
        rootVines.Clear();
        if (holdKind != HoldKind.Stun && body != null && !DecayInfected) body.color = baseColor;
        holdKind = HoldKind.Stun;
    }

    private Sprite[] HoldFrames()
    {
        BoonArt art = BoonArt.Get;
        switch (holdKind)
        {
            case HoldKind.Charm:
                if (charmFrames == null && art != null && art.charm != null) charmFrames = ItemArt.Frames(art.charm, 9, 1, new Vector2(0.5f, 0f), 64f);
                return charmFrames;
            case HoldKind.Fear:
                // the panic sheet is big (54 x 74 px): drawn at half size so it sits small on the enemy's head
                if (fearFrames == null && art != null && art.fear != null) fearFrames = ItemArt.Frames(art.fear, 9, 1, new Vector2(0.5f, 0f), 128f);
                return fearFrames;
            case HoldKind.Root:
                return null; // the vines are the icon
        }
        return StunFrames;
    }

    private static Sprite[] SlowFrames
    {
        get
        {
            BoonArt art = BoonArt.Get;
            if (slowFrames == null && art != null && art.slow != null) slowFrames = ItemArt.Frames(art.slow, 4, 1, new Vector2(0.5f, 0.5f), 64f);
            return slowFrames;
        }
    }

    // The idle / walk state names used across the enemy animators (they aren't all called Idle)
    private static readonly string[] IdleStates = { "Idle", "idle", "IdleBigWolf", "WF_Idle", "Mega C Idle", "WKidle", "WaterVivaIdle" };
    private static readonly string[] WalkStates = { "Walk", "RiderMoving", "WF_Move", "MovingCrabby" };

    private int FirstState(string[] names)
    {
        foreach (string n in names)
        {
            int hash = Animator.StringToHash(n);
            if (anim.HasState(0, hash)) return hash;
        }
        return 0;
    }
    private bool holdPose;

    private void SetMoving(bool on)
    {
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.name == "moving" && p.type == AnimatorControllerParameterType.Bool) { anim.SetBool("moving", on); return; }
    }

    private static Sprite[] StunFrames
    {
        get
        {
            if (stunFrames == null)
            {
                ItemArt art = ItemArt.Get;
                if (art != null && art.stsStun != null) stunFrames = ItemArt.Frames(art.stsStun, 18, 1, new Vector2(0.5f, 0f), 64f);
            }
            return stunFrames;
        }
    }

    // ---------------------------------------------------------------- decay
    public void InfectDecay(float delay, PetFollower source, int generation, HashSet<EnemyHealth> chain, int[] count)
    {
        if (DecayInfected) return;
        DecayInfected = true;
        StartCoroutine(DecayFuse(delay, source, generation, chain, count));
    }

    private IEnumerator DecayFuse(float delay, PetFollower source, int generation, HashSet<EnemyHealth> chain, int[] count)
    {
        float end = Time.time + delay;
        while (Time.time < end)
        {
            // swelling, flickering purple
            if (body != null) body.color = Mathf.Repeat(Time.time * 18f, 1f) < 0.5f ? new Color(0.75f, 0.4f, 1f) : Color.white;
            tinting = true;
            yield return null;
        }
        if (body != null) body.color = baseColor;
        CatPowers.DecayBurst(Center, source, health, generation, chain, count);
    }

    // ---------------------------------------------------------------- update
    private void Update()
    {
        bool dead = health != null && health.enemydead;

        // Poison ticks (quiet damage: no flinch-lock)
        bool poisoned = Time.time < poisonUntil && !dead;
        if (poisoned && Time.time >= nextTick)
        {
            nextTick = Time.time + 0.5f;
            float dps = PoisonDps(out PetFollower poisonSource); // every stack adds up: one tick, one number
            float dmg = Mathf.Max(1f, Mathf.Round(dps * 0.5f));
            if (poisonSource != null) EnemyHealth.CreditNextHit(KillCredit.Cat(poisonSource));
            else EnemyHealth.CreditNextHit(KillCredit.Rowdy());
            health.TakeDamageEnemy(dmg, false, true);
            FXParticle.Burst(Center + Vector3.up * 0.1f, new Color(0.45f, 1f, 0.3f), 3, 0.4f, 1.2f, -1f, 0.5f);
        }
        if (!poisoned && poisonStacks.Count > 0) { poisonStacks.Clear(); shownStacks = 0; }
        UpdatePoisonOverlay(poisoned);
        if (poisoned)
        {
            bubbleTimer -= Time.deltaTime;
            if (bubbleTimer <= 0f)
            {
                bubbleTimer = 0.18f;
                FXParticle.Burst(Center + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.2f, 0.3f), 0f), new Color(0.5f, 1f, 0.35f, 0.9f), 1, 0.1f, 0.3f, -1.2f, 0.6f);
            }
        }

        // Stun
        bool isStunned = stunned.Contains(gameObject);
        if (isStunned && (dead || Time.time >= stunUntil)) { EndStun(); isStunned = false; }
        if (isStunned && stunIcon != null)
        {
            Sprite[] f = HoldFrames();
            stunIcon.enabled = f != null;
            if (f != null)
            {
                // charm / fear sheets play through once and hold their last frames
                int frame;
                if (holdKind == HoldKind.Stun) frame = (int)(Time.time * 18f) % f.Length;
                else
                {
                    int played = (int)((Time.time - holdStartedAt) * (holdKind == HoldKind.Fear ? 32f : 16f)); // panic: 2x speed
                    frame = played < f.Length ? played : f.Length - 4 + (played % 4);
                }
                stunIcon.sprite = f[Mathf.Clamp(frame, 0, f.Length - 1)];
            }
            stunIcon.transform.position = HeadTop + new Vector3(0f, 0.05f, 0f);
        }
        if (isStunned && holdKind == HoldKind.Charm)
        {
            heartTimer -= Time.deltaTime;
            if (heartTimer <= 0f)
            {
                heartTimer = 0.3f;
                BoonFX.Sparkles(HeadTop + new Vector3(Random.Range(-0.2f, 0.2f), 0.1f, 0f), new Color(1f, 0.45f, 0.8f), 1, 0.05f, 0.6f);
            }
        }
        if (rootVines.Count > 0)
            foreach (SpriteRenderer v in rootVines)
                if (v != null) v.transform.localScale = new Vector3(0.8f, Mathf.MoveTowards(v.transform.localScale.y, 0.45f, Time.deltaTime * 4f), 1f);

        // Slow: little dust swirl under the feet
        bool slowed = IsSlowed && !dead;
        if (slowIcon != null)
        {
            Sprite[] sf = SlowFrames;
            slowIcon.enabled = slowed && sf != null;
            if (slowIcon.enabled)
            {
                slowIcon.sprite = sf[(int)(Time.time * 10f) % sf.Length];
                Bounds b = body != null ? body.bounds : new Bounds(transform.position, Vector3.one * 0.5f);
                slowIcon.transform.position = new Vector3(b.center.x, b.min.y + 0.05f, 0f);
            }
        }

        // Tint: green while poisoned, white flash when stunned, pink charmed, violet feared, blue slowed
        if (DecayInfected) return; // the fuse owns the colour
        bool holdTint = isStunned && (holdKind == HoldKind.Charm || holdKind == HoldKind.Fear);
        if (body != null && (stunFlash > 0f || tinting || holdTint || slowed))
        {
            stunFlash = Mathf.Max(0f, stunFlash - Time.deltaTime);
            Color c = baseColor;
            if (slowed) c = Color.Lerp(c, new Color(0.55f, 0.8f, 1f), 0.35f);
            if (holdTint && holdKind == HoldKind.Charm) c = Color.Lerp(c, new Color(1f, 0.55f, 0.85f), 0.4f + 0.15f * Mathf.Sin(Time.time * 8f));
            if (holdTint && holdKind == HoldKind.Fear) c = Color.Lerp(c, new Color(0.6f, 0.4f, 0.9f), 0.45f);
            if (stunFlash > 0f) c = Color.Lerp(c, new Color(1f, 0.95f, 0.5f), stunFlash / 0.15f);
            body.color = c;
            tinting = stunFlash > 0f || holdTint || slowed;
            if (!tinting) body.color = baseColor;
        }
    }

    private void FixedUpdate()
    {
        if (stunned.Contains(gameObject) && rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        else if (IsSlowed && rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * slowFactor, rb.linearVelocity.y);
    }

    // Poisoned = a green glaze over the whole sprite (a flat-colour copy on top, see-through), stronger with more stacks
    private void UpdatePoisonOverlay(bool poisoned)
    {
        if (body == null) return;
        if (!poisoned || !body.enabled || body.sprite == null)
        {
            if (poisonOverlay != null) poisonOverlay.enabled = false;
            return;
        }
        if (poisonOverlay == null)
        {
            var go = new GameObject("Poison Overlay");
            go.transform.SetParent(body.transform, false);
            poisonOverlay = go.AddComponent<SpriteRenderer>();
            if (CatFX.Silhouette != null) poisonOverlay.sharedMaterial = CatFX.Silhouette;
        }
        int stacks = Mathf.Max(1, PoisonStacks);
        poisonOverlay.enabled = true;
        poisonOverlay.sprite = body.sprite;
        poisonOverlay.flipX = body.flipX;
        poisonOverlay.flipY = body.flipY;
        poisonOverlay.sortingLayerID = body.sortingLayerID;
        poisonOverlay.sortingOrder = body.sortingOrder + 1;
        float a = Mathf.Min(0.6f, 0.26f + 0.08f * stacks) + 0.08f * Mathf.Sin(Time.time * 8f);
        poisonOverlay.color = new Color(0.4f, 1f, 0.25f, a);
    }

    private void OnDestroyBoonBits()
    {
        if (poisonOverlay != null) Destroy(poisonOverlay.gameObject);
        if (slowIcon != null) Destroy(slowIcon.gameObject);
        if (stunIcon != null) Destroy(stunIcon.gameObject);
        foreach (SpriteRenderer v in rootVines) if (v != null) Destroy(v.gameObject);
    }
}

// --------------------------------------------------------------------------------------------------- the big moves
public static class CatPowers
{
    public const float StompRadius = 9f;
    public const float StunTime = 1f;
    public static float StompDamage => 10f + 2f * Mathf.Max(0, PlayerStats.Level - 1); // Mushidon: 10 at level 1
    public const float DecayRadius = 2.2f;
    public const int MaxDecayBursts = 16;

    // Mushidon lands: shockwave on the ground, everything standing on Ground within reach is stunned
    public static int Stomp(Vector3 groundPoint, PetFollower cat)
    {
        GroundShock.Spawn(groundPoint, StompRadius, new Color(1f, 0.92f, 0.65f, 1f), new Color(0.6f, 0.45f, 0.3f), 0.55f);
        FXParticle.Burst(groundPoint, new Color(0.6f, 0.45f, 0.3f), 22, 2f, 5.5f, 12f, 0.7f, true);
        FXSound.Play("Slam", 1f, 0.8f);
        ScreenShake.Impulse(1.1f);
        GamepadRumble.Pulse(0.9f, 0.5f, 0.3f);
        TimeSlowController.HitStop(0.08f, 0.05f);

        int stunnedCount = 0;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(groundPoint, StompRadius, LayerMask.GetMask("Enemy")))
        {
            EnemyHealth e = hit.GetComponentInParent<EnemyHealth>();
            if (e == null || e.enemydead || e.IsObject) continue;
            StatusEffects s = e.GetComponent<StatusEffects>();
            if (s != null && StatusEffects.IsStunned(e.gameObject)) continue;
            // on the ground layer = feet touching ground
            if (!SolidGround.Under(hit)) continue; // on the ground = feet touching ground
            StatusEffects.Of(e).Stun(StunTime);
            // the landing hurts too: at least 10 (x cat damage boons, x Rowdy's level like Wig)
            EnemyHealth.CreditNextHit(cat != null ? KillCredit.Cat(cat) : KillCredit.Rowdy());
            e.TakeDamageEnemy(Mathf.Round(StompDamage * Boons.CatDamageFor(cat)), false, true);
            stunnedCount++;
            RunStats.EnemiesStunned++;
        }
        if (stunnedCount > 0) FXSound.Play("Stun", 0.8f, 1f);
        return stunnedCount;
    }

    // Tchogon: one pull of the vortex. Everything within VortexRadius of Rowdy is dragged in toward him, slowed and nicked.
    public const float VortexRadius = 4.5f;
    public static void Vortex(Transform rowdy, PetFollower cat, bool last)
    {
        Vector3 c = rowdy.position + Vector3.up * 0.5f;
        Color swirl = new Color(0.55f, 1f, 0.85f, 1f);
        InwardMote.Ring(c, VortexRadius, 22, swirl, 0.3f, 1.2f);
        PulseRing.Spawn(c, new Color(swirl.r, swirl.g, swirl.b, 0.6f), VortexRadius, 0.3f, 90, true);
        FXSound.Play(BoonArt.Get != null ? BoonArt.Get.tornado : null, 0.35f, 1.3f + (last ? 0.2f : 0f));
        if (last) { ScreenShake.Impulse(0.35f); GamepadRumble.Pulse(0.3f, 0.4f, 0.2f); }
        int pulled = 0;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(c, VortexRadius, LayerMask.GetMask("Enemy")))
        {
            EnemyHealth e = hit.GetComponentInParent<EnemyHealth>();
            if (e == null || e.enemydead || e.IsObject || !e.CompareTag("Enemy")) continue;
            float d = Mathf.Abs(e.transform.position.x - rowdy.position.x);
            UndertowPull.Begin(e, rowdy, Mathf.Clamp(d - 1f, 0f, 1.6f), 0.25f);
            StatusEffects.Of(e).Slow(1.5f, 0.5f);
            EnemyHealth.CreditNextHit(KillCredit.Cat(cat));
            e.TakeDamageEnemy((last ? 8f : 3f) * Boons.CatDamageFor(cat), false, !last);
            pulled++;
        }
        if (last && pulled > 1) IconPopup.Show(c + Vector3.up * 1.4f, null, "SUCKED IN X" + pulled, swirl, 0.8f, 1f);
    }

    // One decay burst: damages everything around, and infects the survivors / other enemies so they burst too
    public static void DecayBurst(Vector3 at, PetFollower cat, EnemyHealth source, int generation, HashSet<EnemyHealth> chain, int[] count)
    {
        RunStats.DecayBursts++;
        ItemArt art = ItemArt.Get;
        float scale = Mathf.Lerp(0.85f, 0.6f, generation / 6f);
        if (art != null) SheetFX.Play(art.vfxDecay, 10, at, 22f, 64f, 95, null, false, null, scale);
        PulseRing.Spawn(at, new Color(0.7f, 0.35f, 1f, 0.95f), DecayRadius, 0.35f);
        FXParticle.Burst(at, new Color(0.65f, 0.3f, 0.95f), 18, 2f, 5f, 4f, 0.6f);
        FXParticle.Burst(at, new Color(0.45f, 0.9f, 0.35f), 8, 1f, 3f, 2f, 0.5f);
        FXSound.Play("Explosion", 0.7f, Random.Range(0.85f, 1.15f) + generation * 0.04f);
        ScreenShake.Impulse(generation == 0 ? 0.9f : 0.45f);
        if (generation == 0) { TimeSlowController.HitStop(0.12f, 0.05f); GamepadRumble.Pulse(0.8f, 0.8f, 0.25f); }

        // the infected enemy itself takes the brunt
        if (source != null && !source.enemydead)
        {
            if (cat != null) EnemyHealth.CreditNextHit(KillCredit.Cat(cat)); else EnemyHealth.CreditNextHit(KillCredit.Rowdy());
            source.TakeDamageEnemy(40f + source.startingenemyHealth * 0.35f);
        }

        foreach (Collider2D hit in Physics2D.OverlapCircleAll(at, DecayRadius, LayerMask.GetMask("Enemy")))
        {
            EnemyHealth e = hit.GetComponentInParent<EnemyHealth>();
            if (e == null || e.enemydead || chain.Contains(e)) continue;
            chain.Add(e);
            if (cat != null) EnemyHealth.CreditNextHit(KillCredit.Cat(cat)); else EnemyHealth.CreditNextHit(KillCredit.Rowdy());
            e.TakeDamageEnemy(25f);
            if (e.IsObject || count[0] >= MaxDecayBursts) continue;
            // spreads: this one bursts a moment later
            StatusEffects.Of(e).InfectDecay(0.3f + Random.Range(0f, 0.15f), cat, generation + 1, chain, count);
            count[0]++; // reserve its burst so the chain can't grow past the cap
        }
    }
}

// Tiny square pixels: sparks, drips, dust (no physics). Pooled and moved in one loop by FXPool.
public static class FXParticle
{
    public static void Burst(Vector3 at, Color color, int count, float speedMin, float speedMax, float gravity, float life, bool upwards = false)
        => FXPool.Pixels(at, color, count, speedMin, speedMax, gravity, life, upwards);
}
