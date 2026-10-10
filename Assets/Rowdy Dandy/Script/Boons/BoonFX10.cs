using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Mega fix 10 boons:
//   WeaponTricks   - the Blacksmith's weapon moves: Butcher Block's 5 chops, Five Slice, Ground Breaker, Dizzy Fighter,
//                    Sky Spear, Onrush, Iron Apron, Grand Slash (+ Scavenger's weapon shower)
//   WeaponSharpness- Sharp Weapons: over-durability that adds damage and wears off fast (like overheal)
//   SkyBeam        - Sky Spear's magic lance
//   LeviathanArms  - Leviathan: squishy tentacles out of Rowdy's body below 30% health, smashing everything
//   AllyJelly      - Jelly Buddies: one blue jellyfish per cat, fighting like the Red Jelly
//   CatLoyalty     - keeps the cat party full
//   SpecialBoons   - the Chef's INGREDIENT RAIN and the Blacksmith's THE ARMORY (Special slot, {WOLF} button)

// ================================================================================================ weapon moves
public class WeaponTricks : MonoBehaviour
{
    private static WeaponTricks instance;
    private static bool inTrick; // a trick's own hits don't set off more tricks

    private PlayerMovement move;
    private Animator anim;
    private Rigidbody2D rb;
    private SpriteRenderer body;
    private Collider2D bodyCollider;
    private readonly float[] lastHit = new float[4];
    private readonly HashSet<EnemyHealth> swingHit = new HashSet<EnemyHealth>();
    private float swingAt = -10f;

    private float slicesArmedUntil, quakeArmedUntil, airborneSince = -1f;
    private bool dizzy;
    private float dizzyHitTimer, dizzyReplayTimer, dizzyFxTimer;
    private float onrushHeld, onrushHitTimer, onrushAnimTimer, onrushWearTimer;
    private bool charging;
    private bool apronUp;
    private float apronReadyAt;
    private SpriteOutline apronOutline;

    public static bool ApronUp => instance != null && instance.apronUp;

    private void Awake()
    {
        instance = this;
        move = GetComponent<PlayerMovement>();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private Vector3 Center => bodyCollider != null ? bodyCollider.bounds.center : transform.position + Vector3.up * 0.4f;
    private Vector3 Feet => bodyCollider != null ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y, 0f) : transform.position;
    private float Facing => transform.localScale.x >= 0f ? 1f : -1f;

    // a weapon hit's worth when the trick has no fresh hit to copy
    private float HitWorth(int weapon)
    {
        float v = lastHit[Mathf.Clamp(weapon, 0, 3)];
        return v > 0f ? v : (8f + 3f * PlayerStats.Level) * Boons.OutgoingMultiplier * Boons.WeaponMultiplier;
    }

    // A hit that counts as one of Rowdy's own weapon hits (kill feed, Feral Swipe, Feline Fury... all go off)
    public static void AutoHit(EnemyHealth e, float damage, string with)
    {
        if (e == null || e.enemydead || damage <= 0f) return;
        inTrick = true;
        KillCredit credit = KillCredit.Rowdy();
        credit.with = with;
        EnemyHealth.CreditNextHit(credit);
        e.TakeDamageEnemy(Mathf.Round(damage));
        RowdyBuffs.OnRowdyHit(e);
        BoonRunner.AfterRowdyHit(e, damage, false);
        inTrick = false;
    }

    // ---------------------------------------------------------------- hooks
    public static void OnAttack(BoonRunner runner, int kind)
    {
        WeaponTricks t = instance;
        if (t == null || Werewolf.Active || StephmossForm.Active) return;
        int w = Boons.ActiveWeapon;
        t.swingAt = Time.time;
        t.swingHit.Clear();
        if (kind == 0 && w == 3 && Boons.Has("butcher")) t.StartCoroutine(t.ExtraChops());
        if (kind == 1 && w == 3 && Boons.Has("slices")) t.slicesArmedUntil = Time.time + 0.9f;
        if (kind == 1 && w == 2 && Boons.Has("quake")) t.quakeArmedUntil = Time.time + 2f;
        if (kind == 1 && w == 1 && Boons.Has("dizzy")) { t.dizzy = true; t.dizzyReplayTimer = 0.22f; }
        if (kind == 2 && w == 2 && Boons.Has("skybeam")) BoonRunner.Delay(0.12f, () => SkyBeam.Fire(t.Center + new Vector3(t.Facing * 0.4f, 0.3f, 0f), t.Facing, Boons.V("skybeam", 0)));
        if (kind == 2 && w == 3 && Boons.Has("apron")) t.RaiseApron();
        if (kind == 2 && w == 1 && Boons.Has("swordcharge") && !t.charging) t.StartCoroutine(t.SwordCharge());
    }

    public static void AfterHit(EnemyHealth e, float damage)
    {
        WeaponTricks t = instance;
        if (t == null || inTrick || e == null) return;
        int w = Boons.ActiveWeapon;
        t.lastHit[w] = damage;
        if (Time.time - t.swingAt < 0.8f) t.swingHit.Add(e);
        if (w == 3 && Time.time < t.slicesArmedUntil && !e.enemydead)
        {
            t.slicesArmedUntil = 0f;
            t.StartCoroutine(t.Slices(e, damage));
        }
    }

    // ---------------------------------------------------------------- Butcher Block: 5 chops, not 3
    private IEnumerator ExtraChops()
    {
        yield return new WaitForSeconds(0.42f); // after the clip's own three chops
        for (int i = 0; i < 2; i++)
        {
            if (Boons.ActiveWeapon != 3) yield break;
            var targets = new List<EnemyHealth>();
            foreach (EnemyHealth e in swingHit) if (e != null && !e.enemydead) targets.Add(e);
            if (targets.Count == 0) targets = BoonFX.EnemiesInBox(Center + new Vector3(Facing * 0.9f, 0f, 0f), new Vector2(1.7f, 1.3f));
            Vector3 at = Center + new Vector3(Facing * 0.9f, 0.05f, 0f);
            BoonArt art = BoonArt.Get;
            if (art != null)
            {
                SheetFX fx = BoonFX.Sheet(art.clawSlash, 6, at, 30f, 1f, new Color(0.9f, 0.95f, 1f));
                if (fx != null) fx.transform.localScale = new Vector3(-Facing, i == 0 ? 1f : -1f, 1f);
                BoonArt.Play(art.clang, 0.25f, Random.Range(1.3f, 1.5f));
            }
            foreach (EnemyHealth e in targets)
            {
                AutoHit(e, HitWorth(3), "Cleaver");
                Blood.Spill(BoonFX.Center(e), Facing, 3);
            }
            if (targets.Count > 0) TimeSlowController.HitStop(0.03f, 0.08f);
            yield return new WaitForSeconds(0.09f);
        }
    }

    // ---------------------------------------------------------------- Five Slice (cleaver jump attack)
    private IEnumerator Slices(EnemyHealth e, float damage)
    {
        // the mark: little steel glints on the enemy while the cuts wait
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            if (e == null || e.enemydead) yield break;
            if (Random.value < 0.25f) BoonFX.Sparkles(BoonFX.Center(e) + (Vector3)Random.insideUnitCircle * 0.25f, BoonFX.Steel, 1, 0.05f, 0.3f);
            yield return null;
        }
        float each = damage * Boons.V("slices", 0) / 100f;
        BoonArt art = BoonArt.Get;
        for (int i = 0; i < 5; i++)
        {
            if (e == null || e.enemydead) yield break;
            Vector3 c = BoonFX.Center(e);
            float ang = Random.Range(0f, 180f) * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * 0.55f;
            Lines.Flash(c - d, c + d, new Color(1f, 1f, 1f, 1f), new Color(0.75f, 0.85f, 1f, 0f), 2f / 64f, 0.12f);
            FXParticle.Burst(c, BoonFX.Steel, 5, 1f, 3f, 5f, 0.3f);
            if (art != null) BoonArt.Play(art.clang, 0.2f, 1.5f + i * 0.08f);
            AutoHit(e, each, "Five Slice");
            Blood.Spill(c, Random.value < 0.5f ? 1f : -1f, 2);
            TimeSlowController.HitStop(0.02f, 0.1f);
            yield return new WaitForSeconds(0.07f);
        }
        ScreenShake.Impulse(0.25f);
    }

    // ---------------------------------------------------------------- Ground Breaker (naginata jump attack landing)
    private void Quake()
    {
        Vector3 f = Feet;
        float dmg = Boons.V("quake", 0) + HitWorth(2) * 0.5f;
        BoonArt art = BoonArt.Get;
        if (art != null)
        {
            BoonArt.Play(art.bigBoom, 0.55f, 0.7f);
            BoonArt.Play(art.rockBreak, 0.5f, 0.8f);
            if (art.groundPound != null) BoonFX.Sheet(art.groundPound, 12, f, 24f, 1f, new Color(1f, 0.85f, 0.6f), null, false, BoonFX.Order + 1, new Vector2(0.5f, 0.2f));
        }
        GroundShock.Spawn(f, 3.6f, new Color(1f, 0.85f, 0.55f), new Color(0.5f, 0.38f, 0.25f), 0.5f);
        FXParticle.Burst(f + Vector3.up * 0.1f, new Color(0.6f, 0.45f, 0.3f), 28, 2f, 6f, 12f, 0.7f, true);
        ScreenShake.Impulse(1f);
        GamepadRumble.Pulse(0.9f, 0.6f, 0.3f);
        TimeSlowController.HitStop(0.08f, 0.05f);
        int n = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(f + Vector3.up * 0.4f, 3.6f))
        {
            AutoHit(e, dmg, "Ground Breaker");
            BoonFX.Stun(e, 1.5f);
            BoonFX.Push(e, new Vector2(0f, 5f));
            n++;
        }
        if (n > 0) FXSound.Play("Stun", 0.7f, 1f);
    }

    // ---------------------------------------------------------------- Iron Apron (cleaver down + attack)
    private void RaiseApron()
    {
        if (apronUp) return;
        if (Time.time < apronReadyAt) { IconPopup.Show(Center + Vector3.up * 0.9f, null, Mathf.CeilToInt(apronReadyAt - Time.time) + "S", new Color(0.7f, 0.8f, 0.9f), 0.6f, 0.6f); return; }
        apronUp = true;
        ItemArt art = ItemArt.Get;
        if (art != null) SheetFX.Play(art.vfxBlock, 10, Vector3.Lerp(Center, Center + Vector3.up * 0.5f, 0.5f), 22f, 64f, body != null ? body.sortingOrder + 3 : 90, transform, false, new Color(0.85f, 0.9f, 1f), 0.55f);
        PulseRing.Spawn(Center, new Color(0.8f, 0.85f, 0.95f, 0.9f), 1f, 0.3f);
        FXSound.Play("ShieldUp", 0.8f, 0.9f);
        IconPopup.Show(Center + Vector3.up * 0.9f, null, "SHIELD UP", new Color(0.8f, 0.9f, 1f), 0.7f, 1f);
    }

    // Health.TakeDamage: true = the apron took the hit
    public static bool TryApron()
    {
        WeaponTricks t = instance;
        if (t == null || !t.apronUp) return false;
        t.apronUp = false;
        t.apronReadyAt = Time.time + Boons.V("apron", 0);
        TimeSlowController.HitStop(0.07f, 0.05f);
        ScreenShake.Impulse(0.35f);
        PulseRing.Spawn(t.Center, new Color(0.85f, 0.9f, 1f, 1f), 0.9f, 0.25f);
        FXParticle.Burst(t.Center, BoonFX.Steel, 12, 1.5f, 4f, 7f, 0.45f);
        FXSound.Play("ShieldBreak", 0.9f, 1.1f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.clang : null, 0.6f, 0.8f);
        IconPopup.Show(t.Center + Vector3.up * 0.9f, null, "BLOCKED!", new Color(0.8f, 0.9f, 1f), 0.8f, 0.9f);
        return true;
    }

    // ---------------------------------------------------------------- Grand Slash (sword down + attack, hold)
    private IEnumerator SwordCharge()
    {
        charging = true;
        yield return new WaitForSeconds(0.25f);
        int ticks = 0;
        float t = 0f;
        float savedSpeed = anim != null ? anim.speed : 1f;
        BoonArt art = BoonArt.Get;
        while (GameInput.Held(GameInput.Act.Attack) && Boons.ActiveWeapon == 1 && t < 2.6f && !Werewolf.Active)
        {
            t += Time.deltaTime;
            if (anim != null) anim.speed = 0.08f; // holds the cast pose while it charges
            if (move != null) move.ExtendHyperArmor(0.2f);
            int now = Mathf.Min(3, Mathf.FloorToInt(t / 0.45f));
            if (now > ticks)
            {
                ticks = now;
                Color c = ticks == 3 ? new Color(1f, 0.6f, 0.25f) : new Color(0.75f, 0.85f, 1f);
                PulseRing.Spawn(Center, new Color(c.r, c.g, c.b, 1f), 0.6f + 0.35f * ticks, 0.3f);
                FXParticle.Burst(Center, c, 8 + 4 * ticks, 1f, 3f, -1f, 0.5f);
                if (art != null) BoonArt.Play(art.clang, 0.35f, 1f + 0.25f * ticks);
                if (body != null) CatFX.Afterimage(body, new Color(c.r, c.g, c.b, 0.8f), 0.25f);
                GamepadRumble.Pulse(0.2f * ticks, 0.4f, 0.1f);
            }
            if (Random.value < 0.3f) FXParticle.Burst(Center + (Vector3)Random.insideUnitCircle * 0.5f, ticks >= 3 ? BoonFX.Ember : BoonFX.Steel, 1, 0.3f, 0.8f, -2f, 0.4f);
            yield return null;
        }
        if (anim != null) anim.speed = savedSpeed <= 0.1f ? 1f : savedSpeed;
        charging = false;
        if (ticks > 0) GrandSlash(ticks);
    }

    private void GrandSlash(int ticks)
    {
        float f = Facing;
        float reach = 1.6f + 0.9f * ticks, height = 1.4f + 0.3f * ticks;
        Vector3 at = Center + new Vector3(f * reach * 0.5f, 0.1f, 0f);
        float dmg = Boons.V("swordcharge", 0) * ticks / 3f + HitWorth(1);
        for (int i = 0; i < ticks; i++)
        {
            int k = i;
            BoonRunner.Delay(0.05f * k, () => HairCrescent.FireSteel(Center + new Vector3(f * 0.4f, -0.2f + 0.25f * k, 0f), f, dmg * 0.25f));
        }
        BoonArt art = BoonArt.Get;
        if (art != null) { BoonArt.Play(art.clang, 0.7f, 0.7f); BoonArt.Play(art.whoosh, 0.6f, 0.7f); }
        PulseRing.Spawn(at, new Color(1f, 0.85f, 0.6f, 1f), reach * 0.6f, 0.3f);
        Lines.Flash(Center - new Vector3(f * 0.2f, 0f, 0f), Center + new Vector3(f * reach, 0f, 0f), Color.white, new Color(1f, 0.6f, 0.3f, 0f), (2f + ticks) / 64f, 0.18f);
        ScreenShake.Impulse(0.3f + 0.2f * ticks);
        GamepadRumble.Pulse(0.3f * ticks, 0.8f, 0.2f);
        TimeSlowController.HitStop(0.04f + 0.02f * ticks, 0.05f);
        foreach (EnemyHealth e in BoonFX.EnemiesInBox(at, new Vector2(reach, height)))
        {
            AutoHit(e, dmg, "Grand Slash");
            BoonFX.Push(e, new Vector2(f * (3f + 2f * ticks), 3f));
            Blood.Spill(BoonFX.Center(e), f, 4);
        }
    }

    // ---------------------------------------------------------------- every frame: Dizzy Fighter, Onrush, Ground Breaker landing
    private void Update()
    {
        if (move == null || PauseMenu.IsPaused) return;
        float dt = Time.deltaTime;
        bool grounded = move.IsGrounded || move.IsWatered;
        if (!grounded) { if (airborneSince < 0f) airborneSince = Time.time; }
        else
        {
            if (airborneSince >= 0f && Time.time < quakeArmedUntil && Boons.ActiveWeapon == 2) { quakeArmedUntil = 0f; Quake(); }
            airborneSince = -1f;
        }

        // apron look
        if (body != null)
        {
            if (apronOutline == null) apronOutline = SpriteOutline.Add(body, Color.clear, 1, -6);
            apronOutline.color = apronUp ? new Color(0.85f, 0.9f, 1f, 0.55f + 0.3f * Mathf.Sin(Time.time * 7f)) : Color.clear;
        }

        UpdateDizzy(dt, grounded);
        UpdateOnrush(dt, grounded);
    }

    private void UpdateDizzy(float dt, bool grounded)
    {
        if (!dizzy) return;
        if (grounded || Boons.ActiveWeapon != 1 || !Boons.Has("dizzy") || !GameInput.Held(GameInput.Act.Attack) || Werewolf.Active) { dizzy = false; return; }
        // keeps the spin going: the jump attack replays, the fall slows to a float
        dizzyReplayTimer -= dt;
        if (dizzyReplayTimer <= 0f && anim != null) { dizzyReplayTimer = 0.2f; anim.Play("JumpAttack", 0, 0.05f); }
        if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -2.2f));
        dizzyFxTimer -= dt;
        if (dizzyFxTimer <= 0f)
        {
            dizzyFxTimer = 0.05f;
            if (body != null) CatFX.Afterimage(body, new Color(0.75f, 0.85f, 1f, 0.4f), 0.15f);
            float a = Time.time * 25f;
            FXParticle.Burst(Center + new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.6f, 0f) * 0.8f, BoonFX.Steel, 1, 0.5f, 1.5f, 0f, 0.25f);
        }
        dizzyHitTimer -= dt;
        if (dizzyHitTimer > 0f) return;
        dizzyHitTimer = 1f / Mathf.Max(1f, Boons.V("dizzy", 0));
        bool any = false;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(Center, 1.2f))
        {
            AutoHit(e, HitWorth(1) * 0.5f, "Dizzy Fighter");
            any = true;
        }
        if (any)
        {
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.clang : null, 0.15f, Random.Range(1.6f, 1.9f));
            TimeSlowController.HitStop(0.015f, 0.1f);
        }
        if (move != null && (onrushWearTimer -= dt * 4f) <= 0f) { onrushWearTimer = 1f; SpendWear(); }
    }

    private void UpdateOnrush(float dt, bool grounded)
    {
        bool on = Boons.Has("onrush") && Boons.ActiveWeapon == 2 && grounded && GameInput.Held(GameInput.Act.Attack) && !Werewolf.Active && !GameInput.HoldingDown;
        if (!on) { onrushHeld = 0f; return; }
        onrushHeld += dt;
        if (onrushHeld < 0.2f) return; // a tap is a normal swing
        // the drill: the swing replays nonstop, a hit every 0.08 s on the closest enemy in front (no piercing)
        onrushAnimTimer -= dt;
        if (onrushAnimTimer <= 0f && anim != null) { onrushAnimTimer = 0.14f; anim.Play("Attack", 0, 0.3f); }
        onrushWearTimer -= dt;
        if (onrushWearTimer <= 0f) { onrushWearTimer = 0.5f; SpendWear(); }
        if (Random.value < 0.4f) FXParticle.Burst(Center + new Vector3(Facing * Random.Range(0.6f, 1.3f), Random.Range(-0.15f, 0.15f), 0f), BoonFX.Steel, 1, 1f, 3f, 2f, 0.2f);
        onrushHitTimer -= dt;
        if (onrushHitTimer > 0f) return;
        onrushHitTimer = 0.08f;
        Vector3 front = Center + new Vector3(Facing * 1.0f, 0f, 0f);
        EnemyHealth best = null;
        float bestD = float.MaxValue;
        foreach (EnemyHealth e in BoonFX.EnemiesInBox(front, new Vector2(1.9f, 1.1f)))
        {
            float d = Mathf.Abs(BoonFX.Center(e).x - Center.x);
            if (d < bestD) { bestD = d; best = e; }
        }
        if (best == null) return;
        AutoHit(best, HitWorth(2) * Boons.V("onrush", 0) / 100f, "Onrush");
        Vector3 c = BoonFX.Center(best);
        Lines.Flash(Center + new Vector3(Facing * 0.3f, 0f, 0f), c + new Vector3(Facing * 0.3f, Random.Range(-0.1f, 0.1f), 0f), Color.white, new Color(0.8f, 0.9f, 1f, 0f), 2f / 64f, 0.06f);
        FXParticle.Burst(c, BoonFX.Steel, 3, 1f, 3f, 4f, 0.2f);
        if (Random.value < 0.35f) BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.clang : null, 0.12f, Random.Range(1.7f, 2f));
        ScreenShake.Impulse(0.05f);
    }

    private void SpendWear()
    {
        WeaponManager wm = WeaponManager.Instance;
        float cost = Boons.DurabilityCost;
        if (wm != null && cost > 0f) wm.DepleteActiveWeaponDurability(cost);
    }

    // ---------------------------------------------------------------- Scavenger (Blacksmith + Guild)
    public static void DropEveryWeapon(Vector3 at)
    {
        string[] drops = { "Systems/WeaponDropSword", "Systems/WeaponDropNaginata", "Systems/WeaponDropCleaver" };
        for (int i = 0; i < drops.Length; i++)
        {
            GameObject prefab = Resources.Load<GameObject>(drops[i]);
            if (prefab != null) Object.Instantiate(prefab, at + new Vector3((i - 1) * 0.45f, 0.3f, 0f), Quaternion.identity);
        }
        PulseRing.Spawn(at, new Color(0.85f, 0.6f, 1f, 1f), 1.1f, 0.3f);
        BoonFX.Sparkles(at, BoonFX.Gold, 8, 0.5f, 0.6f);
    }
}

// ================================================================================================ Sharp Weapons
public static class WeaponSharpness
{
    private static readonly float[] value = new float[4];  // 1 = +100% damage
    private static readonly float[] holdUntil = new float[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { for (int i = 0; i < 4; i++) value[i] = 0f; }

    public static float Cap => Boons.Has("sharp") ? Boons.V("sharp", 0) / 100f : 0f;

    public static void Add(int weapon, float amount)
    {
        if (weapon <= 0 || weapon > 3 || amount <= 0f) return;
        float before = value[weapon];
        value[weapon] = Mathf.Min(Cap, value[weapon] + amount);
        holdUntil[weapon] = Time.time + 0.8f;
        if (value[weapon] > before + 0.01f && BoonRunner.Rowdy != null)
        {
            Vector3 c = BoonRunner.RowdyCenter;
            IconPopup.Show(c + Vector3.up * 0.9f, null, "SHARPENED +" + Mathf.RoundToInt(value[weapon] * 100f) + "%", new Color(0.8f, 0.95f, 1f), 0.75f, 1.2f);
            BoonFX.Sparkles(c + new Vector3(BoonRunner.RowdyFacing * 0.4f, 0f, 0f), Color.white, 6, 0.35f, 0.5f);
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.clang : null, 0.35f, 1.8f);
        }
    }

    public static float Bonus01(int weapon) => weapon > 0 && weapon < 4 ? value[weapon] : 0f;
    public static float Fraction(int weapon) => Bonus01(weapon);

    // wears off like overheal: a flat rate plus a slice of what's left
    public static void Tick(float dt)
    {
        for (int i = 1; i < 4; i++)
        {
            if (value[i] <= 0f) continue;
            if (!Boons.Has("sharp")) { value[i] = 0f; continue; }
            if (Time.time < holdUntil[i]) continue;
            value[i] = Mathf.Max(0f, value[i] - (0.035f + value[i] * 0.08f) * dt);
        }
    }
}

// ================================================================================================ Sky Spear
public class SkyBeam : MonoBehaviour
{
    private Vector2 dir;
    private float dps, age, tick;
    private SpriteRenderer sr;
    private const float Life = 1.1f, Speed = 8.5f;

    public static void Fire(Vector3 at, float facing, float dps)
    {
        float ang = facing > 0f ? 35f : 145f;
        SpriteRenderer sr = BoonFX.MakeRenderer("Sky Spear", Lance, at, BoonFX.Order + 5);
        sr.transform.rotation = Quaternion.Euler(0f, 0f, ang);
        var b = sr.gameObject.AddComponent<SkyBeam>();
        b.sr = sr;
        b.dps = dps;
        b.dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
        try
        {
            var l = new GameObject("Glow").AddComponent<Light2D>();
            l.transform.SetParent(sr.transform, false);
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(0.7f, 0.6f, 1f);
            l.intensity = 1.1f;
            l.pointLightOuterRadius = 1.6f;
        }
        catch { }
        BoonArt art = BoonArt.Get;
        if (art != null) { BoonArt.Play(art.zap, 0.4f, 0.8f); BoonArt.Play(art.whoosh, 0.4f, 1.4f); }
        PulseRing.Spawn(at, new Color(0.75f, 0.6f, 1f, 1f), 0.7f, 0.25f);
    }

    // a violet lance with a white core, 40 x 5 px (pointing right; rotated toward where it flies)
    private static Sprite Lance => BoonFX.FromRows("SkySpearLance", new[]
    {
        "..vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv....",
        ".vVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVv..",
        "vVWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWVv",
        ".vVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVVv..",
        "..vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv....",
    }, c => c == 'W' ? new Color32(255, 255, 255, 255) : c == 'V' ? new Color32(190, 150, 255, 255) : new Color32(110, 70, 220, 200), new Vector2(0.5f, 0.5f));

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        transform.position += (Vector3)(dir * Speed * dt);
        sr.color = new Color(1f, 1f, 1f, age > Life - 0.2f ? (Life - age) / 0.2f : 0.85f + 0.15f * Mathf.Sin(age * 40f));
        if (Time.frameCount % 2 == 0) CatFX.Afterimage(sr, new Color(0.7f, 0.55f, 1f, 0.45f), 0.2f);
        if (Random.value < 0.5f) BoonFX.Sparkles(transform.position - (Vector3)(dir * 0.3f), new Color(0.8f, 0.7f, 1f), 1, 0.1f, 0.35f);
        tick -= dt;
        if (tick <= 0f)
        {
            tick = 0.12f;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var hitList = new HashSet<EnemyHealth>();
            foreach (Collider2D c in Physics2D.OverlapBoxAll(transform.position, new Vector2(0.75f, 0.35f), ang, LayerMask.GetMask("Enemy")))
            {
                EnemyHealth e = c.GetComponentInParent<EnemyHealth>();
                if (e == null || e.enemydead || e.IsObject || !e.CompareTag("Enemy") || !hitList.Add(e)) continue;
                BoonFX.Hit(e, Mathf.Max(1f, dps * 0.12f), "Sky Spear", null, true);
                FXParticle.Burst(BoonFX.Center(e), new Color(0.75f, 0.6f, 1f), 3, 1f, 2.5f, 2f, 0.25f);
            }
        }
        if (age >= Life) Destroy(gameObject);
    }
}

// ================================================================================================ Leviathan
// Four tentacles burst out of Rowdy's body below 30% health and stay out while it lasts. Each one is a strip mesh
// bent along a living spine (sway, curl, a peristaltic bulge travelling down it), drawn by Resources/Shaders/Tentacle
// (wet, banded, sucker rings, sliding slime highlight, snapped to the pixel grid). Every ~0.6 s each arm winds back,
// whips its tip onto the nearest enemy (or the ground when nothing is near) and smashes: damage, splash, shake.
public class LeviathanArms : MonoBehaviour
{
    private static LeviathanArms instance;
    private static Shader shader;

    private class Arm
    {
        public MeshFilter mf;
        public MeshRenderer mr;
        public Mesh mesh;
        public Material mat;
        public float side, baseAngle, length, seed, front;
        public int state;          // 0 sway, 1 wind up, 2 strike, 3 recover
        public float stateT, next, bulge, flash;
        public Vector3 target;
        public EnemyHealth victim;
        public Vector3[] pts = new Vector3[Segments + 1];
    }

    private const int Segments = 14;
    private readonly List<Arm> arms = new List<Arm>();
    private BoonRunner runner;
    private float damage, grow, age;
    private bool wanted;

    public static void Keep(bool on, BoonRunner r, float damage)
    {
        if (on)
        {
            if (instance == null)
            {
                if (r == null) return;
                instance = new GameObject("Leviathan Arms").AddComponent<LeviathanArms>();
                instance.runner = r;
                instance.Burst();
            }
            instance.wanted = true;
            instance.damage = damage;
        }
        else if (instance != null) instance.wanted = false;
    }

    private static Shader Shader
    {
        get
        {
            if (shader == null) shader = Resources.Load<Shader>("Shaders/Tentacle");
            if (shader == null) shader = Shader.Find("Rowdy Dandy/Tentacle");
            return shader;
        }
    }

    private void Burst()
    {
        float[] sides = { -1f, 1f, -1f, 1f };
        float[] angles = { 35f, 35f, 70f, 70f };
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Tentacle " + i);
            go.transform.SetParent(transform, false);
            var a = new Arm
            {
                side = sides[i],
                baseAngle = angles[i] + Random.Range(-8f, 8f),
                length = Random.Range(1.7f, 2.2f),
                seed = Random.Range(0f, 100f),
                front = i < 2 ? 1f : -1f,
                next = Time.time + 0.3f + i * 0.15f,
            };
            a.mf = go.AddComponent<MeshFilter>();
            a.mr = go.AddComponent<MeshRenderer>();
            a.mesh = new Mesh { name = "Tentacle" };
            a.mesh.MarkDynamic();
            a.mf.sharedMesh = a.mesh;
            if (Shader != null)
            {
                a.mat = new Material(Shader);
                a.mat.SetFloat("_Seed", a.seed);
                // a touch of colour variety between arms
                a.mat.SetColor("_Tip", Color.Lerp(new Color(0.35f, 0.9f, 1f), new Color(0.75f, 0.5f, 1f), i / 3f));
                a.mr.sharedMaterial = a.mat;
            }
            a.mr.sortingLayerName = "Default";
            arms.Add(a);
        }
        Vector3 c = BoonRunner.RowdyCenter;
        BoonArt art = BoonArt.Get;
        if (art != null) { BoonArt.Play(art.waterBoom, 0.7f, 0.55f); BoonArt.Play(art.squish, 0.6f, 0.6f); }
        FXParticle.Burst(c, BoonFX.Deep, 26, 2f, 5f, 6f, 0.6f);
        FXParticle.Burst(c, new Color(0.5f, 0.35f, 0.9f), 14, 1.5f, 4f, 6f, 0.5f);
        PulseRing.Spawn(c, new Color(0.45f, 0.4f, 1f, 1f), 2.2f, 0.4f);
        ScreenShake.Impulse(0.7f);
        GamepadRumble.Pulse(0.6f, 0.8f, 0.3f);
        TimeSlowController.HitStop(0.08f, 0.05f);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        foreach (Arm a in arms) { if (a.mesh != null) Destroy(a.mesh); if (a.mat != null) Destroy(a.mat); }
    }

    private void LateUpdate()
    {
        Transform rowdy = BoonRunner.Rowdy;
        if (rowdy == null) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        age += dt;
        grow = Mathf.MoveTowards(grow, wanted ? 1f : 0f, dt * (wanted ? 4f : 3f));
        if (!wanted && grow <= 0f) { Destroy(gameObject); return; }
        SpriteRenderer body = rowdy.GetComponent<SpriteRenderer>();
        int order = body != null ? body.sortingOrder : 80;
        Vector3 center = BoonRunner.RowdyCenter;
        float facing = BoonRunner.RowdyFacing;

        foreach (Arm a in arms)
        {
            a.mr.sortingOrder = order + (a.front > 0f ? 2 : -2);
            float side = a.side * facing; // the arm pair turns with him
            Vector3 b = center + new Vector3(side * 0.16f, a.front > 0f ? -0.08f : 0.12f, 0f);
            Think(a, b, dt);
            BuildSpine(a, b, side);
            BuildMesh(a);
            a.flash = Mathf.Max(0f, a.flash - dt * 4f);
            if (a.mat != null) a.mat.SetFloat("_Flash", a.flash);
            // slime drips off the tips
            if (Random.value < 0.06f) FXParticle.Burst(a.pts[Segments], new Color(0.45f, 0.4f, 0.9f, 0.9f), 1, 0.1f, 0.4f, 6f, 0.5f);
        }
    }

    private void Think(Arm a, Vector3 b, float dt)
    {
        a.stateT += dt;
        switch (a.state)
        {
            case 0:
                if (grow < 1f || Time.time < a.next) return;
                // the nearest enemy in reach that no other arm is going for, else a random spot of ground
                EnemyHealth best = null;
                float bestD = float.MaxValue;
                foreach (EnemyHealth e in BoonFX.EnemiesIn(b, 3.3f))
                {
                    bool taken = false;
                    foreach (Arm o in arms) if (o != a && o.victim == e && o.state != 0) taken = true;
                    float d = Vector2.Distance(b, BoonFX.Center(e)) + (taken ? 2f : 0f);
                    if (d < bestD) { bestD = d; best = e; }
                }
                a.victim = best;
                if (best != null) a.target = BoonFX.Center(best);
                else
                {
                    if (Random.value > 0.35f) { a.next = Time.time + 0.4f; return; } // nothing near: smash the floor now and then
                    Vector3 spot = b + new Vector3(a.side * BoonRunner.RowdyFacing * Random.Range(1f, 2.4f), 0.5f, 0f);
                    a.target = SolidGround.Ray(spot, Vector2.down, 3f, out RaycastHit2D hit) ? (Vector3)hit.point : spot + Vector3.down;
                }
                a.state = 1; a.stateT = 0f;
                break;
            case 1:
                if (a.victim != null && !a.victim.enemydead) a.target = Vector3.Lerp(a.target, BoonFX.Center(a.victim), dt * 10f);
                if (a.stateT >= 0.16f) { a.state = 2; a.stateT = 0f; BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.whoosh : null, 0.18f, Random.Range(0.7f, 0.9f)); }
                break;
            case 2:
                if (a.stateT >= 0.09f) { a.state = 3; a.stateT = 0f; Smash(a); }
                break;
            case 3:
                if (a.stateT >= 0.32f) { a.state = 0; a.stateT = 0f; a.next = Time.time + Random.Range(0.35f, 0.8f); a.victim = null; }
                break;
        }
    }

    private void Smash(Arm a)
    {
        Vector3 at = a.target;
        a.bulge = 1f;
        a.flash = 0.6f;
        FXParticle.Burst(at, new Color(0.45f, 0.4f, 0.95f), 12, 1.5f, 4f, 8f, 0.45f, true);
        FXParticle.Burst(at, BoonFX.Foam, 6, 1f, 3f, 8f, 0.35f, true);
        PulseRing.Spawn(at, new Color(0.5f, 0.45f, 1f, 0.9f), 0.9f, 0.22f);
        if (SolidGround.Ray(at + Vector3.up * 0.3f, Vector2.down, 0.6f, out RaycastHit2D ground))
            GroundShock.Spawn(ground.point, 1.1f, new Color(0.55f, 0.45f, 1f), BoonFX.Deep, 0.28f);
        BoonArt art = BoonArt.Get;
        if (art != null) { BoonArt.Play(art.squish, 0.35f, Random.Range(0.7f, 0.9f)); BoonArt.Play(art.waterBoom, 0.25f, Random.Range(0.8f, 1f)); }
        ScreenShake.Impulse(0.22f);
        int n = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(at, 0.95f))
        {
            BoonFX.Hit(e, damage, "Leviathan");
            BoonFX.Push(e, new Vector2(Mathf.Sign(BoonFX.Center(e).x - BoonRunner.RowdyCenter.x) * 3f, 4.5f));
            n++;
        }
        if (n > 0) TimeSlowController.HitStop(0.04f, 0.07f);
    }

    // rest = a swaying, curling arm reaching out and up; strike = a whip onto the target; blended by the state
    private void BuildSpine(Arm a, Vector3 b, float side)
    {
        float t = age + a.seed;
        float len = a.length * Mathf.SmoothStep(0f, 1f, grow) * (1f + 0.06f * Mathf.Sin(t * 2.1f));
        float step = len / Segments;
        float ang = (90f - a.baseAngle * side) + Mathf.Sin(t * 1.7f) * 12f; // degrees: 90 = straight up
        Vector3 p = b;
        var rest = new Vector3[Segments + 1];
        rest[0] = b;
        for (int j = 1; j <= Segments; j++)
        {
            float s = j / (float)Segments;
            float curl = Mathf.Sin(t * 2.3f + s * 3.2f) * 38f * s + side * -40f * s * s; // tips curl over
            if (a.state == 1) curl += side * 70f * s * Mathf.Clamp01(a.stateT / 0.16f); // winding back
            float r = (ang + curl) * Mathf.Deg2Rad;
            p += new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f) * step;
            rest[j] = p;
        }
        float w = 0f;
        if (a.state == 2) w = Mathf.SmoothStep(0f, 1f, a.stateT / 0.09f);
        else if (a.state == 3) w = 1f - Mathf.SmoothStep(0f, 1f, a.stateT / 0.32f);
        Vector3 tip = a.target;
        float reach = Vector2.Distance(b, tip);
        if (reach > len * 1.25f) tip = b + (tip - b).normalized * len * 1.25f; // stretches, but only so far
        Vector3 ctrl = (b + tip) * 0.5f + Vector3.up * (0.6f + 0.3f * Mathf.Sin(t * 5f));
        for (int j = 0; j <= Segments; j++)
        {
            float s = j / (float)Segments;
            Vector3 whip = (1 - s) * (1 - s) * b + 2 * (1 - s) * s * ctrl + s * s * tip;
            whip += new Vector3(0f, Mathf.Sin(s * 9f + t * 14f) * 0.05f * s, 0f); // the whip wobbles
            a.pts[j] = Vector3.Lerp(rest[j], whip, w);
        }
        a.bulge = Mathf.Max(0f, a.bulge - Time.deltaTime * 3f);
    }

    private void BuildMesh(Arm a)
    {
        int n = Segments + 1;
        var verts = new Vector3[n * 2];
        var uvs = new Vector2[n * 2];
        var cols = new Color[n * 2];
        var tris = new int[Segments * 6];
        float t = age + a.seed;
        for (int j = 0; j < n; j++)
        {
            float s = j / (float)Segments;
            Vector3 tangent = (a.pts[Mathf.Min(j + 1, n - 1)] - a.pts[Mathf.Max(j - 1, 0)]).normalized;
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
            // thick at the root, thin at the tip; a squishy bulge travels down it, and smashing fattens the end
            float width = Mathf.Lerp(0.38f, 0.06f, Mathf.Pow(s, 0.8f)) * (1f + 0.22f * Mathf.Sin(t * 7f - s * 11f)) * (1f + a.bulge * 0.6f * s) * Mathf.Clamp01(grow * 1.5f);
            Vector3 p = a.pts[j] - transform.position;
            verts[j * 2] = p - normal * width * 0.5f;
            verts[j * 2 + 1] = p + normal * width * 0.5f;
            float belly = a.side > 0f ? 0f : 1f; // the suckers face inward
            uvs[j * 2] = new Vector2(s, belly);
            uvs[j * 2 + 1] = new Vector2(s, 1f - belly);
            cols[j * 2] = cols[j * 2 + 1] = Color.white;
        }
        for (int j = 0; j < Segments; j++)
        {
            int k = j * 6, v = j * 2;
            tris[k] = v; tris[k + 1] = v + 2; tris[k + 2] = v + 1;
            tris[k + 3] = v + 1; tris[k + 4] = v + 2; tris[k + 5] = v + 3;
        }
        a.mesh.Clear();
        a.mesh.vertices = verts;
        a.mesh.uv = uvs;
        a.mesh.colors = cols;
        a.mesh.triangles = tris;
        a.mesh.RecalculateBounds();
    }
}

// ================================================================================================ Jelly Buddies
// One little blue jellyfish per cat. They circle Rowdy at his height, pulse to charge, then jet straight through the
// nearest enemy (the Red Jelly's attack, on Rowdy's side). The WaterViva jelly art in its own blue.
public class AllyJelly : MonoBehaviour
{
    private static readonly List<AllyJelly> jellies = new List<AllyJelly>();
    private static Sprite[] frames;

    private enum State { Hover, Charge, Dash, Recover }
    private State state;
    private SpriteRenderer sr;
    private float stateAt, nextAttack, angle;
    private Vector3 dashTarget, vel;
    private readonly HashSet<EnemyHealth> hit = new HashSet<EnemyHealth>();
    private int index;

    public static void Keep(int count, Transform rowdy)
    {
        jellies.RemoveAll(j => j == null);
        if (rowdy == null) return;
        while (jellies.Count < count) jellies.Add(Spawn(rowdy, jellies.Count));
        while (jellies.Count > count)
        {
            AllyJelly j = jellies[jellies.Count - 1];
            jellies.RemoveAt(jellies.Count - 1);
            if (j != null) j.Pop();
        }
    }

    private static Sprite[] Frames
    {
        get
        {
            if (frames != null) return frames;
            GameObject prefab = Resources.Load<GameObject>("Enemies Prefab/Enemy_VoltRat");
            VoltRat red = prefab != null ? prefab.GetComponentInChildren<VoltRat>(true) : null;
            Texture2D sheet = red != null ? red.Sheet : null;
            if (sheet != null) frames = ItemArt.Frames(sheet, 7, 1, new Vector2(0.5f, 0.4f), 64f);
            return frames;
        }
    }

    private static AllyJelly Spawn(Transform rowdy, int i)
    {
        Vector3 c = BoonRunner.RowdyCenter;
        Sprite[] f = Frames;
        SpriteRenderer sr = BoonFX.MakeRenderer("Jelly Buddy", f != null ? f[0] : BoonFX.Heart, c, BoonFX.Order - 2, null, ItemArt.Lit);
        var j = sr.gameObject.AddComponent<AllyJelly>();
        j.sr = sr;
        j.index = i;
        j.angle = i * 2.1f;
        j.nextAttack = Time.time + 1f + i * 0.4f;
        SpriteOutline.Add(sr, new Color(0.5f, 0.85f, 1f, 0.6f), 1, -1);
        try
        {
            var l = new GameObject("Glow").AddComponent<Light2D>();
            l.transform.SetParent(sr.transform, false);
            l.lightType = Light2D.LightType.Point;
            l.color = new Color(0.4f, 0.75f, 1f);
            l.intensity = 0.6f;
            l.pointLightOuterRadius = 1.1f;
        }
        catch { }
        FXParticle.Burst(c, new Color(0.5f, 0.85f, 1f), 10, 1f, 3f, 3f, 0.5f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.plop : null, 0.3f, 1.5f);
        return j;
    }

    private void Pop()
    {
        FXParticle.Burst(transform.position, new Color(0.5f, 0.85f, 1f), 12, 1f, 3f, 5f, 0.5f);
        FXSound.Play("Jelly", 0.35f, 1.2f);
        Destroy(gameObject);
    }

    private void Go(State s) { state = s; stateAt = Time.time; }

    private void Update()
    {
        Transform rowdy = BoonRunner.Rowdy;
        if (rowdy == null) { Destroy(gameObject); return; }
        float dt = Time.deltaTime, t = Time.time - stateAt;
        Vector3 rc = BoonRunner.RowdyCenter;
        Sprite[] f = Frames;
        switch (state)
        {
            case State.Hover:
            {
                angle += dt * 1.5f;
                Vector3 want = rc + new Vector3(Mathf.Cos(angle + index * 1.3f) * (1.2f + 0.25f * (index % 3)), 0.45f + Mathf.Sin(angle * 2f + index) * 0.2f, 0f);
                transform.position = Vector3.SmoothDamp(transform.position, want, ref vel, 0.25f, 9f);
                if (f != null) sr.sprite = f[(int)(Time.time * 9f + index) % f.Length];
                sr.color = Color.white;
                if (Time.time >= nextAttack)
                {
                    EnemyHealth target = BoonFX.Nearest(rc, 6f);
                    if (target != null && EnemyFairness.OnScreen(BoonFX.Center(target), 0.04f)) { dashTarget = BoonFX.Center(target); Go(State.Charge); }
                    else nextAttack = Time.time + 0.5f;
                }
                break;
            }
            case State.Charge:
                if (f != null) sr.sprite = f[(int)(t * 24f) % f.Length];
                sr.color = Mathf.Repeat(t * 10f, 1f) < 0.5f ? Color.white : new Color(0.7f, 0.9f, 1f);
                if (Random.value < 0.3f) FXParticle.Burst(transform.position, new Color(0.5f, 0.85f, 1f), 1, 0.5f, 1.5f, 0f, 0.2f);
                if (t >= 0.4f)
                {
                    Vector3 d = (dashTarget - transform.position).normalized;
                    dashTarget += d * 1.3f; // through it
                    hit.Clear();
                    Go(State.Dash);
                    BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.whoosh : null, 0.15f, 1.6f);
                }
                break;
            case State.Dash:
            {
                if (f != null) sr.sprite = f[Mathf.Min(3, f.Length - 1)];
                Vector3 next = Vector3.MoveTowards(transform.position, dashTarget, 12f * dt);
                transform.position = next;
                if (Time.frameCount % 2 == 0) CatFX.Afterimage(sr, new Color(0.5f, 0.85f, 1f, 0.5f), 0.15f);
                float dmg = (8f + 2.5f * PlayerStats.Level) * Boons.CatDamageMultiplier;
                foreach (EnemyHealth e in BoonFX.EnemiesIn(transform.position, 0.45f))
                {
                    if (!hit.Add(e)) continue;
                    BoonFX.Hit(e, dmg, "Jelly Buddies");
                    FXParticle.Burst(BoonFX.Center(e), new Color(0.5f, 0.85f, 1f), 6, 1f, 3f, 5f, 0.35f);
                    FXSound.Play("Jelly", 0.25f, 1.3f);
                }
                if ((transform.position - dashTarget).sqrMagnitude < 0.01f || t > 0.6f) Go(State.Recover);
                break;
            }
            case State.Recover:
                if (f != null) sr.sprite = f[(int)(t * 14f) % f.Length];
                if (t >= 0.35f) { nextAttack = Time.time + Random.Range(1.4f, 2.4f); Go(State.Hover); }
                break;
        }
        // the art faces left
        float lookX = state == State.Dash || state == State.Charge ? dashTarget.x : rc.x;
        sr.flipX = lookX > transform.position.x;
    }
}

// ================================================================================================ Cat Loyalty
public static class CatLoyalty
{
    private static float nextAt;

    public static void Tick(bool on, Transform rowdy)
    {
        if (!on || rowdy == null || Time.time < nextAt || PauseMenu.IsPaused || FirstDrop.Running) return;
        nextAt = Time.time + 1.2f;
        if (!CatRoster.HasRoom) return;
        // a cat out there in the level comes running, or (none left) a brand new stray turns up
        PetFollower cat = CatRoster.Lurable();
        Vector3 at = BoonRunner.RowdyCenter + new Vector3(Random.Range(-1f, 1f), 0.9f, 0f);
        if (cat == null) cat = CatSpawner.MakeRandom(at);
        if (cat == null) return;
        cat.transform.position = at;
        PulseRing.Spawn(at, new Color(0.85f, 0.65f, 1f, 1f), 0.9f, 0.3f);
        BoonFX.Sparkles(at, BoonFX.Lavender, 8, 0.4f, 0.6f);
        cat.Collect(rowdy);
    }
}

// ================================================================================================ Special slot: Ingredient Rain / The Armory
// Same button and HUD meter as the werewolf / Stephmoss ({WOLF}). The meter fills slowly over time (long cooldown),
// a little faster with hits and kills.
public class SpecialBoons : MonoBehaviour
{
    private static SpecialBoons instance;
    private static float charge;
    private static float activeUntil = -1f, activeLength = 1f;
    private float deniedAt = -10f;

    public static bool Owned => Boons.Has("sandwichrain") || Boons.Has("armory");
    private static bool Rain => Boons.Has("sandwichrain");
    private static float ChargeSeconds => Rain ? 45f : 50f;
    public static float Charge01 => Mathf.Clamp01(charge / 100f);
    public static bool Ready => charge >= 100f;
    public static bool Active => Time.time < activeUntil;
    public static float TimeLeft01 => Active ? Mathf.Clamp01((activeUntil - Time.time) / activeLength) : 0f;
    public static Color Tone => Rain ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.6f, 0.3f);
    public static string ActiveLabel => Rain ? "RAIN!" : "FORGE!";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { charge = 0f; activeUntil = -1f; }
    public static void ResetCharge() { charge = 0f; }
    public static void Fill() { charge = 100f; }

    public static void AddCharge(float amount)
    {
        if (!Owned || Active) return;
        bool was = Ready;
        charge = Mathf.Min(100f, charge + amount);
        if (!was && Ready && instance != null) instance.OnFull();
    }

    private void Awake() => instance = this;
    private void OnDestroy() { if (instance == this) instance = null; }

    private void OnFull()
    {
        BoonFX.Popup(BoonRunner.RowdyHead + Vector3.up * 0.4f, GameInput.Format((Rain ? "THE KITCHEN IS READY! " : "THE FORGE IS HOT! ") + "{WOLF}"), Tone, 0.85f, 1.6f);
        PulseRing.Spawn(BoonRunner.RowdyCenter, new Color(Tone.r, Tone.g, Tone.b, 0.8f), 1.2f, 0.4f);
        BoonHUD.FlashMoon();
    }

    private void Update()
    {
        if (!Owned) return;
        Health h = BoonRunner.Rowdy != null ? BoonRunner.Rowdy.GetComponent<Health>() : null;
        if (h == null || h.IsDead || PauseMenu.IsPaused || BoonPicker.IsOpen) return;
        if (!Active) AddCharge(Time.deltaTime * 100f / ChargeSeconds);
        bool menus = RowdyNotes.BlocksPause || WorldMap.BlocksPause || Tutorials.BlocksPause || BoonPicker.BlocksInput || CatParty.BlocksPause || CheckpointMenu.BlocksPause || StatsPause.BlocksPause;
        if (menus || !GameInput.Down(GameInput.Act.Werewolf)) return;
        if (Ready && !Active)
        {
            charge = 0f;
            if (Rain) StartCoroutine(IngredientRain());
            else StartCoroutine(Armory());
        }
        else if (!Active && Time.unscaledTime - deniedAt > 0.5f)
        {
            deniedAt = Time.unscaledTime;
            BoonHUD.ShakeMoon();
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.cancel : null, 0.4f, 1f);
        }
    }

    // ---------------------------------------------------------------- INGREDIENT RAIN
    private IEnumerator IngredientRain()
    {
        activeLength = 2.8f;
        activeUntil = Time.time + activeLength;
        BoonArt art = BoonArt.Get;
        if (art != null) { BoonArt.Play(art.chef, 0.7f, 1f); BoonArt.Play(art.sparkle, 0.5f, 0.9f); }
        ScreenFlash.Play(new Color(1f, 0.75f, 0.35f, 0.35f), 0.3f);
        ScreenShake.Impulse(0.5f);
        TimeSlowController.HitStop(0.1f, 0.05f);
        Camera cam = Camera.main;
        int count = Mathf.Min(66, 22 * BoonRunner.FoodStock);
        float dmg = (16f + 5f * PlayerStats.Level) * BoonRunner.FoodDamage * Boons.OutgoingMultiplier;
        BoonArt.Food[] parts = { BoonArt.Food.Bread, BoonArt.Food.Lettuce, BoonArt.Food.Tomato, BoonArt.Food.Cheese, BoonArt.Food.Meatball, BoonArt.Food.Egg };
        for (int i = 0; i < count; i++)
        {
            Vector3 rc = BoonRunner.RowdyCenter;
            float top = cam != null ? cam.transform.position.y + cam.orthographicSize + 0.5f : rc.y + 6f;
            // half of them hunt enemies on screen, the rest carpet the area around Rowdy
            float x = rc.x + Random.Range(-7.5f, 7.5f);
            var targets = BoonFX.EnemiesOnScreen();
            if (targets.Count > 0 && Random.value < 0.55f) x = BoonFX.Center(targets[Random.Range(0, targets.Count)]).x + Random.Range(-0.4f, 0.4f);
            FallingFood.Drop(new Vector3(x, top + Random.Range(0f, 1.5f), 0f), BoonArt.FoodSprite(parts[i % parts.Length]), dmg);
            yield return new WaitForSeconds(2.2f / count);
        }
        // the grand finale: the whole sandwich, on the biggest crowd
        yield return new WaitForSeconds(0.25f);
        Vector3 at = BoonRunner.RowdyCenter + new Vector3(BoonRunner.RowdyFacing * 2f, 0f, 0f);
        EnemyHealth crowd = null;
        int most = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesOnScreen()) { int n = BoonFX.EnemiesIn(BoonFX.Center(e), 2.5f).Count; if (n > most) { most = n; crowd = e; } }
        if (crowd != null) at = BoonFX.Center(crowd);
        float topY = cam != null ? cam.transform.position.y + cam.orthographicSize + 1f : at.y + 7f;
        FallingFood.Drop(new Vector3(at.x, topY, 0f), MoreSprites.Sandwich, dmg * 3f, 2.6f);
    }

    // ---------------------------------------------------------------- THE ARMORY
    private IEnumerator Armory()
    {
        activeLength = Boons.V("armory", 0);
        activeUntil = Time.time + activeLength;
        BoonArt art = BoonArt.Get;
        if (art != null) { BoonArt.Play(art.clang, 0.8f, 0.6f); BoonArt.Play(art.sparkle, 0.5f, 0.8f); }
        ScreenFlash.Play(new Color(1f, 0.65f, 0.3f, 0.3f), 0.3f);
        ScreenShake.Impulse(0.5f);
        TimeSlowController.HitStop(0.1f, 0.05f);
        var blades = new List<ArmoryBlade>();
        for (int i = 0; i < 4; i++) blades.Add(ArmoryBlade.Make(i));
        while (Active && BoonRunner.Rowdy != null) yield return null;
        foreach (ArmoryBlade b in blades) if (b != null) b.Finish();
    }
}

// One ingredient falling from the sky: a shadow ring warns where, it lands with a sauce burst and hurts everything near
public class FallingFood : MonoBehaviour
{
    private SpriteRenderer sr;
    private float vy, groundY, damage, radius;
    private bool warned;

    public static void Drop(Vector3 from, Sprite sprite, float damage, float radius = 1.2f)
    {
        float ground = from.y - 12f;
        if (SolidGround.Ray(from, Vector2.down, 20f, out RaycastHit2D hit)) ground = hit.point.y;
        SpriteRenderer sr = BoonFX.MakeRenderer("Falling Food", sprite != null ? sprite : MoreSprites.Pod, from, BoonFX.Order + 3, null, ItemArt.Lit);
        sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        var f = sr.gameObject.AddComponent<FallingFood>();
        f.sr = sr; f.groundY = ground; f.damage = damage; f.radius = radius; f.vy = -6f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        vy -= 30f * dt;
        Vector3 p = transform.position + Vector3.up * vy * dt;
        transform.Rotate(0f, 0f, 540f * dt);
        if (!warned && p.y - groundY < 4f)
        {
            warned = true;
            PulseRing.Spawn(new Vector3(p.x, groundY + 0.03f, 0f), new Color(0.35f, 0.15f, 0.1f, 0.6f), radius * 0.6f, 0.25f, 60, true);
        }
        if (Time.frameCount % 2 == 0) FXParticle.Burst(transform.position, new Color(1f, 0.8f, 0.5f, 0.6f), 1, 0.1f, 0.3f, 0f, 0.25f);
        if (p.y <= groundY) { transform.position = new Vector3(p.x, groundY, 0f); Land(); return; }
        transform.position = p;
    }

    private void Land()
    {
        Vector3 at = transform.position + Vector3.up * 0.1f;
        bool big = radius > 2f;
        FXParticle.Burst(at, BoonFX.Sauce, big ? 30 : 10, 1.5f, big ? 6f : 4f, 9f, 0.55f, true);
        FXParticle.Burst(at, new Color(1f, 0.8f, 0.45f), big ? 16 : 6, 1f, 3f, 8f, 0.45f, true);
        PulseRing.Spawn(at, new Color(1f, 0.55f, 0.25f, 0.9f), radius, 0.25f);
        GroundShock.Spawn(transform.position, radius, new Color(1f, 0.7f, 0.4f), new Color(0.6f, 0.3f, 0.15f), big ? 0.45f : 0.25f);
        BoonArt art = BoonArt.Get;
        if (art != null) BoonArt.Play(big ? art.bigBoom : art.squish, big ? 0.7f : 0.22f, Random.Range(0.9f, 1.3f));
        ScreenShake.Impulse(big ? 1f : 0.12f);
        if (big) { TimeSlowController.HitStop(0.1f, 0.05f); GamepadRumble.Pulse(0.8f, 0.8f, 0.3f); }
        foreach (EnemyHealth e in BoonFX.EnemiesIn(at + Vector3.up * 0.3f, radius))
        {
            BoonFX.Hit(e, damage, "Ingredient Rain");
            if (Boons.Has("fermented")) BoonFX.Poison(e, 4f, 8f);
            BoonFX.Push(e, new Vector2(Mathf.Sign(BoonFX.Center(e).x - at.x) * (big ? 6f : 2f), big ? 6f : 2.5f));
        }
        if (big) Shard.Break(sr, 3, 3, transform.position, 3f);
        Destroy(gameObject);
    }
}

// The Armory: one of Rowdy's four weapons, floating round him, darting at the nearest enemies on its own
public class ArmoryBlade : MonoBehaviour
{
    private int weapon;
    private SpriteRenderer sr;
    private SpriteOutline outline;
    private float angle, nextAt, age;
    private EnemyHealth target;
    private Vector3 strikeFrom;
    private float strikeT = -1f;
    private bool finishing;
    private static readonly string[] Names = { "Armory Rod", "Armory Sword", "Armory Naginata", "Armory Cleaver" };

    public static ArmoryBlade Make(int weapon)
    {
        WeaponManager wm = WeaponManager.Instance;
        Sprite s = wm != null ? wm.GetProfileByIndex(weapon) : null;
        SpriteRenderer sr = BoonFX.MakeRenderer(Names[weapon], s != null ? s : BoonFX.Sparkle, BoonRunner.RowdyCenter, BoonFX.Order + 4, null, ItemArt.Lit);
        var b = sr.gameObject.AddComponent<ArmoryBlade>();
        b.sr = sr; b.weapon = weapon; b.angle = weapon * Mathf.PI * 0.5f; b.nextAt = Time.time + 0.3f + weapon * 0.12f;
        b.outline = SpriteOutline.Add(sr, new Color(1f, 0.6f, 0.25f, 0.9f), 1, -1);
        FXParticle.Burst(sr.transform.position, BoonFX.Ember, 8, 1f, 3f, 2f, 0.4f);
        return b;
    }

    public void Finish()
    {
        if (finishing) return;
        finishing = true;
        FXParticle.Burst(transform.position, BoonFX.Ember, 10, 1f, 3f, 6f, 0.45f);
        Shard.Break(sr, 2, 2, transform.position, 2f);
        Destroy(gameObject);
    }

    private Vector3 Home => BoonRunner.RowdyCenter + new Vector3(Mathf.Cos(angle) * 1.15f, 0.25f + Mathf.Sin(angle) * 0.45f, 0f);

    private void Update()
    {
        if (BoonRunner.Rowdy == null) { Destroy(gameObject); return; }
        float dt = Time.deltaTime;
        age += dt;
        angle += dt * 2.4f;
        outline.color = new Color(1f, 0.55f + 0.2f * Mathf.Sin(age * 8f + weapon), 0.25f, 0.9f);
        if (strikeT >= 0f)
        {
            strikeT += dt;
            Vector3 to = target != null ? BoonFX.Center(target) : strikeFrom;
            float k = Mathf.Clamp01(strikeT / 0.12f);
            transform.position = Vector3.Lerp(strikeFrom, to, k * k);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - strikeFrom.y, to.x - strikeFrom.x) * Mathf.Rad2Deg - 45f);
            if (Time.frameCount % 2 == 0) CatFX.Afterimage(sr, new Color(1f, 0.6f, 0.3f, 0.5f), 0.15f);
            if (k >= 1f) { Strike(); strikeT = -1f; nextAt = Time.time + Random.Range(0.45f, 0.7f); }
            return;
        }
        transform.position = Vector3.Lerp(transform.position, Home, dt * 10f);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 3f + weapon) * 15f);
        if (Time.time < nextAt) return;
        target = BoonFX.Nearest(BoonRunner.RowdyCenter, 4.8f);
        if (target == null) { nextAt = Time.time + 0.3f; return; }
        strikeFrom = transform.position;
        strikeT = 0f;
    }

    private void Strike()
    {
        if (target == null || target.enemydead) return;
        Vector3 c = BoonFX.Center(target);
        float dmg = (12f + 4f * PlayerStats.Level) * Boons.OutgoingMultiplier * (weapon == 3 ? 1.4f : 1f);
        float facing = Mathf.Sign(c.x - BoonRunner.RowdyCenter.x);
        BoonArt art = BoonArt.Get;
        if (art != null)
        {
            SheetFX fx = BoonFX.Sheet(art.clawSlash, 6, c, 30f, 1f, new Color(1f, 0.8f, 0.55f));
            if (fx != null) fx.transform.localScale = new Vector3(-facing, 1f, 1f);
            BoonArt.Play(art.clang, 0.3f, Random.Range(1.1f, 1.5f));
        }
        BoonFX.Hit(target, dmg, "The Armory");
        switch (weapon)
        {
            case 0: BoonFX.Stun(target, 0.35f); break;                                          // the rod bonks
            case 1: HairCrescent.FireSteel(c - new Vector3(facing * 0.3f, 0f, 0f), facing, dmg * 0.4f); break; // the sword: a steel wave
            case 2: SmithFX.Skewer(target, facing, dmg * 0.6f); break;                          // the naginata pierces
            case 3: Blood.Spill(c, facing, 4); BoonFX.Push(target, new Vector2(facing * 4f, 3f)); break; // the cleaver chops hard
        }
        TimeSlowController.HitStop(0.03f, 0.08f);
    }
}
