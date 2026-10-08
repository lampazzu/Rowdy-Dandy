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
    public static void GiveArmor(PetFollower owner, int hits = ArmorHits)
    {
        RowdyBuffs b = Get();
        if (b == null) return;
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
        FXSound.Play("Poison", 0.9f, 1f);
        IconPopup.Show(b.HeadTop + Vector3.up * 0.3f, null, "POISON BLADE!", new Color(0.55f, 1f, 0.35f), 1f, 1.4f);
    }

    public static void OnRowdyHit(EnemyHealth enemy)
    {
        if (!PoisonActive || enemy == null || enemy.enemydead || enemy.IsObject) return;
        StatusEffects.Of(enemy).Poison(4f, 6f + enemy.startingenemyHealth * 0.02f, instance.poisonOwner);
    }

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
public class StatusEffects : MonoBehaviour
{
    private static readonly HashSet<GameObject> stunned = new HashSet<GameObject>();
    private static Sprite[] stunFrames;

    private EnemyHealth health;
    private SpriteRenderer body;
    private Animator anim;
    private Rigidbody2D rb;
    private Color baseColor = Color.white;
    private bool tinting;

    // poison
    private float poisonUntil, poisonDps, nextTick;
    private PetFollower poisonSource;
    private float bubbleTimer;

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
    private void OnDestroy() => stunned.Remove(gameObject);

    private Vector3 Center => EnemyFairness.BodyCenter(this);
    // Where the block shield is drawn: halfway up from his middle to his head, so it covers his upper body instead of his legs
    private Vector3 ShieldSpot => Vector3.Lerp(Center, HeadTop, 0.5f);
    private Vector3 HeadTop => transform.position + Vector3.up * EnemyFairness.HeadHeight(this);

    // ---------------------------------------------------------------- poison
    public void Poison(float seconds, float dps, PetFollower source)
    {
        bool fresh = Time.time >= poisonUntil;
        poisonUntil = Mathf.Max(poisonUntil, Time.time + seconds);
        poisonDps = Mathf.Max(fresh ? 0f : poisonDps, dps);
        poisonSource = source;
        if (fresh)
        {
            nextTick = Time.time + 0.5f;
            RunStats.EnemiesPoisoned++;
            ItemArt art = ItemArt.Get;
            if (art != null) SheetFX.Play(art.vfxPoison, 10, Center, 22f, 64f, body != null ? body.sortingOrder + 2 : 80, transform, false, null, 0.5f);
            FXSound.Play("Poison", 0.35f, Random.Range(1.2f, 1.4f));
            FXParticle.Burst(Center, new Color(0.45f, 1f, 0.3f), 8, 0.8f, 2f, 2f, 0.5f);
        }
    }

    // ---------------------------------------------------------------- stun
    public void Stun(float seconds)
    {
        if (health != null && health.enemydead) return;
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
            float dmg = Mathf.Max(1f, Mathf.Round(poisonDps * 0.5f));
            if (poisonSource != null) EnemyHealth.CreditNextHit(KillCredit.Cat(poisonSource));
            else EnemyHealth.CreditNextHit(KillCredit.Rowdy());
            health.TakeDamageEnemy(dmg, false, true);
            FXParticle.Burst(Center + Vector3.up * 0.1f, new Color(0.45f, 1f, 0.3f), 3, 0.4f, 1.2f, -1f, 0.5f);
        }
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
            Sprite[] f = StunFrames;
            stunIcon.enabled = f != null;
            if (f != null) stunIcon.sprite = f[(int)(Time.time * 18f) % f.Length];
            stunIcon.transform.position = HeadTop + new Vector3(0f, 0.05f, 0f);
        }

        // Tint: green while poisoned, white flash when stunned
        if (DecayInfected) return; // the fuse owns the colour
        if (body != null && (poisoned || stunFlash > 0f || tinting))
        {
            stunFlash = Mathf.Max(0f, stunFlash - Time.deltaTime);
            Color c = baseColor;
            if (poisoned) c = Color.Lerp(baseColor, new Color(0.55f, 1f, 0.45f), 0.45f + 0.15f * Mathf.Sin(Time.time * 10f));
            if (stunFlash > 0f) c = Color.Lerp(c, new Color(1f, 0.95f, 0.5f), stunFlash / 0.15f);
            body.color = c;
            tinting = poisoned || stunFlash > 0f;
            if (!tinting) body.color = baseColor;
        }
    }

    private void FixedUpdate()
    {
        if (stunned.Contains(gameObject) && rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }
}

// --------------------------------------------------------------------------------------------------- the big moves
public static class CatPowers
{
    public const float StompRadius = 9f;
    public const float StunTime = 1f;
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
            stunnedCount++;
            RunStats.EnemiesStunned++;
        }
        if (stunnedCount > 0) FXSound.Play("Stun", 0.8f, 1f);
        return stunnedCount;
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

// Tiny square pixels: sparks, drips, dust (no physics, optional ground stop)
public class FXParticle : MonoBehaviour
{
    private Vector2 velocity;
    private float gravity, life, age;
    private SpriteRenderer sr;
    private Color color;

    public static void Burst(Vector3 at, Color color, int count, float speedMin, float speedMax, float gravity, float life, bool upwards = false)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("FX Pixel");
            go.transform.position = at;
            var p = go.AddComponent<FXParticle>();
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (upwards) dir = new Vector2(dir.x, Mathf.Abs(dir.y) * 0.8f + 0.2f).normalized;
            p.velocity = dir * Random.Range(speedMin, speedMax);
            p.gravity = gravity;
            p.life = life * Random.Range(0.7f, 1.2f);
            p.color = Color.Lerp(color, Color.white, Random.Range(0f, 0.3f));
            p.sr = go.AddComponent<SpriteRenderer>();
            p.sr.sprite = Blood.DropSprite;
            p.sr.sortingLayerName = "Default";
            p.sr.sortingOrder = 96;
            if (CatFX.Unlit != null) p.sr.sharedMaterial = CatFX.Unlit;
            p.sr.color = p.color;
            if (Random.value < 0.35f) go.transform.localScale = Vector3.one * 1.5f;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= life) { Destroy(gameObject); return; }
        velocity.y -= gravity * dt;
        velocity *= 1f - 1.5f * dt;
        transform.position += (Vector3)(velocity * dt);
        sr.color = new Color(color.r, color.g, color.b, color.a * (1f - age / life));
    }
}
