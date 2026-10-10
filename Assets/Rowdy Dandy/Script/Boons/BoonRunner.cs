using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Runs the owned boons on Rowdy (added by Health.Awake next to RowdyBuffs). The game calls the static hooks:
//   PlayerMovement: OnAttackStarted / OnSurfDash / OnJump        PlayerDamage: ModifyRowdyHit / AfterRowdyHit / OnCatHit
//   EnemyHealth: OnEnemyKilled                                   Health: ModifyIncoming / PreventDeath / OnPlayerHurt / ModifyHeal
//   PetFollower: OnCatPower                                      WeaponManager: OnWeaponBroken
// Landings, standing still (Admire Yourself), water (Salt Water Blood), the spotlight, the lure, the halo and the
// auras are watched here every frame. The effects themselves live in BoonFX / BoonFXMore.
public class BoonRunner : MonoBehaviour
{
    private static BoonRunner instance;

    private Health health;
    private PlayerMovement movement;
    private Rigidbody2D rb;
    private SpriteRenderer body;
    private Collider2D bodyCollider;
    private SpriteOutline aura;

    // state
    private int swings, hits, sandwichKills, bloomKills;
    private float stillTime, poseSparkTimer;
    private bool posed;
    private float saltyUntil = -10f, saltHealBank, saltFxTimer;
    private bool wasWatered;
    private bool airborne;
    private float airStartTime, peakY;
    private float decoyReadyAt, wipeoutReadyAt, runwayReadyAt, inkReadyAt, snareReadyAt, roarReadyAt, catCallReadyAt, ambushReadyAt, eggsReadyAt, flashReadyAt;
    private float sporeReadyAt, kissReadyAt, hairballReadyAt, ripReadyAt, tomatoReadyAt, anvilReadyAt, sunspotReadyAt, pounceReadyAt;
    private float whistleTimer = 3f, eyeTimer, catFxTimer, rankSparkTimer, jealousyTimer, stenchTimer, regenTimer, leviathanReadyAt, sauceReadyAt;
    private float spotlightTimer = 4f, spotlightUntil = -1f, wellFedUntil = -1f;
    private bool nineLivesUsed;
    private readonly Dictionary<PetFollower, SpriteOutline> catAuras = new Dictionary<PetFollower, SpriteOutline>();
    private readonly Dictionary<EnemyHealth, int> rodHits = new Dictionary<EnemyHealth, int>();

    // speed: the animator keys moveSpeed on most of Rowdy's clips, so it's scaled after the animator wrote it
    private float speedBase = -1f, speedWritten = -1f;

    public static bool SaltyActive => instance != null && Time.time < instance.saltyUntil;
    public static bool Posed => instance != null && instance.posed;
    public static bool InSpotlight => instance != null && Time.time < instance.spotlightUntil;
    public static bool WellFed => instance != null && Time.time < instance.wellFedUntil;
    public static bool AtFullHealth => instance != null && instance.health != null && instance.health.currentHealth >= instance.health.startingHealth - 0.01f;
    public static Transform Rowdy => instance != null ? instance.transform : null;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        if (movement == null) { Destroy(this); return; } // a Health that isn't Rowdy's
        instance = this;
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        if (GetComponent<Werewolf>() == null) gameObject.AddComponent<Werewolf>();
        if (GetComponent<StephmossForm>() == null) gameObject.AddComponent<StephmossForm>();
        if (GetComponent<WeaponTricks>() == null) gameObject.AddComponent<WeaponTricks>();
        if (GetComponent<SpecialBoons>() == null) gameObject.AddComponent<SpecialBoons>();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private Vector3 Center => bodyCollider != null ? bodyCollider.bounds.center : transform.position + Vector3.up * 0.4f;
    private Vector3 HeadTop => bodyCollider != null ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.max.y, 0f) : transform.position + Vector3.up * 0.9f;
    private Vector3 Feet => bodyCollider != null ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y, 0f) : transform.position;
    private float Facing => transform.localScale.x >= 0f ? 1f : -1f;
    private bool Grounded => movement == null || movement.IsGrounded;

    public static Vector3 RowdyCenter => instance != null ? instance.Center : Vector3.zero;
    public static Vector3 RowdyHead => instance != null ? instance.HeadTop : Vector3.zero;
    public static Vector3 RowdyFeet => instance != null ? instance.Feet : Vector3.zero;
    public static float RowdyFacing => instance != null ? instance.Facing : 1f;
    public static PlayerMovement Movement => instance != null ? instance.movement : null;

    private static bool Ok => instance != null && instance.health != null && !instance.health.IsDead;

    // Runs an action a moment later (on Rowdy, so it stops with him)
    public static void Delay(float seconds, System.Action action)
    {
        if (instance != null) instance.StartCoroutine(instance.DelayRoutine(seconds, action));
        else action();
    }

    private IEnumerator DelayRoutine(float seconds, System.Action action)
    {
        yield return new WaitForSeconds(seconds);
        action();
    }

    // ================================================================ hooks: Rowdy's actions
    public static void OnAttackStarted()
    {
        if (!Ok) return;
        BoonRunner r = instance;
        r.swings++;
        Vector3 front = r.Center + new Vector3(r.Facing * 0.45f, 0.1f, 0f);
        if (Boons.Has("hairflip") && r.swings % 3 == 0)
        {
            HairCrescent.Fire(front, r.Facing, Boons.V("hairflip", 0));
            BoonFX.Sparkles(r.HeadTop, BoonFX.Pink, 4, 0.25f, 0.4f);
        }
        if (Boons.Has("sporelob") && r.swings % 3 == 0) Lobbed.SporePod(front + Vector3.up * 0.2f, r.Facing, Boons.V("sporelob", 0));
        if (Boons.Has("meatball") && r.swings % 3 == 0)
            for (int i = 0; i < FoodStock; i++) Lobbed.Meatball(front + Vector3.up * (0.2f + 0.1f * i), r.Facing * (1f - 0.18f * i), Boons.V("meatball", 0) * FoodDamage, Boons.Has("fermented"));
        if (Boons.Has("splitedge") && Boons.ActiveWeapon == 1) HairCrescent.FireSteel(front, r.Facing, Boons.V("splitedge", 0));
        Werewolf.OnSwing();
        StephmossForm.OnSwing();
    }

    // Which attack it was (PlayerMovement): 0 = ground, 1 = jump attack, 2 = down + attack. The weapon boons (WeaponTricks).
    public static void OnAttackKind(int kind)
    {
        if (!Ok) return;
        WeaponTricks.OnAttack(instance, kind);
    }

    // Food Critic (Narcissism + Crazy Chef): 5 s in the spotlight after eating
    private float criticUntil = -1f;
    public static bool InCriticSpotlight => instance != null && Time.time < instance.criticUntil;

    // A sandwich / fish treat was eaten: Food Critic, Midnight Snack
    public static void OnFoodEaten(float size)
    {
        if (!Ok) return;
        BoonRunner r = instance;
        if (Boons.Has("foodcritic"))
        {
            r.criticUntil = Time.time + 5f;
            SpotlightFX.Begin(r.transform, 5f);
            foreach (EnemyHealth e in BoonFX.EnemiesIn(r.Center, 4.5f)) BoonFX.Charm(e, 5f);
        }
        Werewolf.OnFoodEaten(size);
    }

    // Surf dash boons have no cooldown (user rule): every surf dash sets them all off
    public static void OnSurfDash()
    {
        if (!Ok) return;
        BoonRunner r = instance;
        float now = Time.time;
        if (Boons.Has("decoy"))
        {
            r.decoyReadyAt = now + 4f;
            Decoy.Spawn(r.body, Boons.V("decoy", 0));
        }
        if (Boons.Has("wipeout"))
        {
            r.wipeoutReadyAt = now + Balance.WipeoutCooldown;
            TideRider.Begin(r, Boons.V("wipeout", 0), Boons.Has("beachbod"), Boons.Has("redtide"));
        }
        if (Boons.Has("rootsnare") && r.Grounded)
        {
            r.snareReadyAt = now + 3f;
            RootSnare.Spawn(r.Feet, Boons.V("rootsnare", 0));
        }
        if (Boons.Has("alpharoar"))
        {
            r.roarReadyAt = now + 6f;
            r.AlphaRoar(Boons.V("alpharoar", 0));
        }
        if (Boons.Has("eggs"))
        {
            r.eggsReadyAt = now + 1.5f;
            // one egg at every enemy on screen (Food Stock: three each), aimed so they land on them
            var targets = BoonFX.EnemiesOnScreen();
            int each = FoodStock;
            int n = 0;
            foreach (EnemyHealth e in targets)
                for (int k = 0; k < each; k++, n++)
                    Lobbed.EggAt(r.Center + new Vector3(r.Facing * 0.3f, 0.2f, 0f), e, n * 0.03f, Boons.V("eggs", 0) * FoodDamage, Boons.Has("fermented"));
            if (targets.Count == 0) for (int i = 0; i < each; i++) Lobbed.Egg(r.Center + new Vector3(r.Facing * 0.3f, 0.2f, 0f), r.Facing, i, Boons.V("eggs", 0) * FoodDamage, Boons.Has("fermented"));
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.hairFlip : null, 0.3f, 1.6f);
        }
        if (Boons.Has("blinding"))
        {
            r.flashReadyAt = now + 4f;
            SunFX.BlindingFlash(r.Center, Boons.V("blinding", 0));
        }
        if (Boons.Has("catcall") && Boons.CatsWithRowdy > 0)
        {
            // only the leader and the sub-leader are ready at once; the others get a slice of their cooldown cut
            float cut = Boons.V("catcall", 0) / 100f;
            foreach (PetFollower p in PetFollower.Pets)
            {
                if (p == null || !p.IsCollected) continue;
                if (!CatRoster.IsLeader(p) && !CatRoster.IsSubLeader(p)) { p.CutCooldown(cut); continue; }
                p.WakeUp();
                PulseRing.Spawn(p.transform.position, new Color(BoonFX.Lavender.r, BoonFX.Lavender.g, BoonFX.Lavender.b, 0.9f), 0.7f, 0.3f);
                BoonFX.Sparkles(p.transform.position, BoonFX.Lavender, 3, 0.2f, 0.5f);
            }
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.catCall : null, 0.5f * GameSettings.CatVoiceVolume, 1.15f);
        }
        if (Boons.Has("ambush") && Boons.CatsWithRowdy > 0)
        {
            r.ambushReadyAt = now + 5f;
            r.AlleyAmbush(Boons.V("ambush", 0));
        }
    }

    private void AlphaRoar(float seconds)
    {
        BoonArt art = BoonArt.Get;
        if (art != null)
        {
            BoonArt.Play(art.wolfHowl, 0.45f, 1.35f);
            BoonArt.Play(art.fearSfx, 0.5f, 0.9f);
        }
        PulseRing.Spawn(Center, new Color(1f, 0.2f, 0.3f, 1f), 3.5f, 0.35f);
        PulseRing.Spawn(Center, new Color(1f, 0.9f, 0.9f, 0.7f), 2.2f, 0.25f);
        ScreenShake.Impulse(0.4f);
        GamepadRumble.Pulse(0.4f, 0.6f, 0.2f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(Center, 3.5f))
        {
            BoonFX.Fear(e, seconds);
            BoonFX.Push(e, new Vector2(Mathf.Sign(BoonFX.Center(e).x - Center.x) * 4f, 2f));
        }
    }

    private void AlleyAmbush(float damage)
    {
        var used = new List<EnemyHealth>();
        int n = 0;
        foreach (PetFollower p in PetFollower.Pets)
        {
            if (p == null || !p.IsCollected) continue;
            EnemyHealth target = BoonFX.Nearest(p.transform.position, 7f, used);
            if (target == null) target = BoonFX.Nearest(p.transform.position, 7f);
            if (target == null) break;
            used.Add(target);
            Sprite s = p.TryGetComponent(out SpriteRenderer sr) ? sr.sprite : null;
            GhostCat.Pounce(p.transform.position + Vector3.up * 0.3f, target, damage * Boons.CatDamageFor(p), s, BoonFX.Lavender, "Alley Ambush", false);
            n++;
        }
    }

    public static void OnJump()
    {
        if (!Ok) return;
        BoonRunner r = instance;
        float now = Time.time;
        if (Boons.Has("kiss") && now >= r.kissReadyAt)
        {
            EnemyHealth target = BoonFX.Nearest(r.Center, 7f);
            if (target != null)
            {
                r.kissReadyAt = now + 0.8f;
                KissHeart.Fire(r.HeadTop + new Vector3(r.Facing * 0.2f, -0.1f, 0f), target, Boons.V("kiss", 0));
            }
        }
        if (Boons.Has("hairball") && now >= r.hairballReadyAt && Boons.CatsWithRowdy > 0)
        {
            EnemyHealth target = BoonFX.Nearest(r.Center, 7f);
            PetFollower cat = RandomCat();
            if (target != null && cat != null)
            {
                r.hairballReadyAt = now + 1f;
                Lobbed.Hairball(cat.transform.position + Vector3.up * 0.15f, target, Boons.V("hairball", 0) * Boons.CatDamageFor(cat));
            }
        }
    }

    // Food Stock (Crazy Chef): every food boon makes three of everything
    public static int FoodStock => Boons.Has("foodstock") ? 3 : 1;
    public static float FoodDamage => Boons.Has("foodstock") ? 1f + Boons.V("foodstock", 0) / 100f : 1f;

    private static PetFollower RandomCat()
    {
        var mine = new List<PetFollower>();
        foreach (PetFollower p in PetFollower.Pets) if (p != null && p.IsCollected) mine.Add(p);
        return mine.Count > 0 ? mine[Random.Range(0, mine.Count)] : null;
    }

    // ================================================================ hooks: Rowdy's hits
    public static float ModifyRowdyHit(EnemyHealth e, float damage, ref bool crit)
    {
        if (!Ok || e == null) return damage;
        BoonRunner r = instance;
        damage *= Boons.OutgoingMultiplier * Boons.TargetMultiplier(e) * Boons.WeaponMultiplier;
        if (Boons.Has("butcher") && Boons.ActiveWeapon == 3 && !e.IsObject && e.currentenemyHealth <= e.startingenemyHealth * 0.4f)
        {
            BoonFX.Popup(BoonFX.Center(e) + Vector3.up * 0.6f, "CHOP!", new Color(1f, 0.6f, 0.5f), 0.7f, 0.7f);
            Blood.Spill(BoonFX.Center(e), r.Facing, 4);
        }
        if (r.posed && !e.IsObject)
        {
            r.posed = false;
            damage *= Boons.V("admire", 0);
            Vector3 c = BoonFX.Center(e);
            BoonFX.Sparkles(c, BoonFX.Gold, 10, 0.5f, 0.7f);
            PulseRing.Spawn(c, new Color(1f, 0.6f, 0.9f, 1f), 1.4f, 0.3f);
            TimeSlowController.HitStop(0.09f, 0.05f);
            ScreenShake.Impulse(0.4f);
        }
        return damage;
    }

    public static void AfterRowdyHit(EnemyHealth e, float damage, bool crit)
    {
        if (!Ok || e == null || e.IsObject) return;
        BoonRunner r = instance;
        Vector3 c = BoonFX.Center(e);
        Werewolf.AddCharge(2.5f);
        StephmossForm.AddCharge(2.5f);
        SpecialBoons.AddCharge(0.6f);
        if (Werewolf.Active) BankHeal(1f, true); // lifesteal
        r.hits++;

        if (Boons.Has("undertow") && !e.enemydead)
        {
            float toward = Mathf.Sign(r.Center.x - c.x);
            UndertowPull.Begin(e, r.transform, 1.3f, 0.22f, 0.45f); // owns the speed through the hit's own knockback
            BoonFX.Slow(e, 2f, Boons.V("undertow", 0) / 100f);
            FXParticle.Burst(c, BoonFX.Cyan, 6, 1f, 2.5f, 2f, 0.4f);
            // a streak of dark water pulling back toward Rowdy, so the drag reads
            for (int i = 0; i < 5; i++)
                FXParticle.Burst(c + new Vector3(-toward * (0.15f + i * 0.18f), Random.Range(-0.15f, 0.15f), 0f), BoonFX.Deep, 1, 0.5f, 1.2f + i * 0.4f, 0f, 0.3f);
            if (Random.value < 0.35f) BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.boto : null, 0.25f, Random.Range(1.1f, 1.3f));
        }
        if (Boons.Has("rottenedge") && !e.enemydead) BoonFX.Poison(e, 4f, Boons.V("rottenedge", 0));
        if (Boons.Has("feral")) r.StartCoroutine(r.ClawEcho(e, damage * Boons.V("feral", 0) / 100f * (Boons.Has("silverclaws") ? 2f : 1f)));
        if (Boons.Has("felinefury") && r.hits % 4 == 0 && !e.enemydead)
            GhostCat.Pounce(c + new Vector3(-r.Facing * 1.2f, 1.6f, 0f), e, Boons.V("felinefury", 0), RandomCatSprite(), BoonFX.Lavender);
        if (Boons.Has("hairspray") && !e.enemydead)
        {
            Glossed.Apply(e, 3f);
            BoonFX.Slow(e, 3f, 0.3f);
        }
        if (Boons.Has("pressure") && r.hits % 5 == 0) AbyssFX.Crush(c, Boons.V("pressure", 0));
        if (Boons.Has("openwounds") && crit && !e.enemydead) BoonDot.Bleed(e, Boons.V("openwounds", 0), 4f);
        if (Boons.Has("rustededge") && !e.enemydead) BoonFX.Poison(e, 4f, 5f + 0.5f * PlayerStats.Level);
        WeaponTricks.AfterHit(e, damage);
        if (Boons.Has("solarflare") && r.hits % 4 == 0) SunFX.SunPillar(c, Boons.V("solarflare", 0));
        if (Boons.Has("sunburn") && !e.enemydead) BoonDot.Burn(e, Boons.V("sunburn", 0), 3f);
        if (Boons.Has("forgefire") && !e.enemydead) BoonDot.Burn(e, 6f, 3f);
        if (Boons.Has("skewer") && Boons.ActiveWeapon == 2) SmithFX.Skewer(e, r.Facing, damage * Boons.V("skewer", 0) / 100f);
        if (Boons.Has("hookline") && Boons.ActiveWeapon == 0 && !e.enemydead) r.HookLine(e, c);
        if (crit && Boons.Has("cheese")) ChefFX.CheeseSpray(c, r.Facing);
        if (crit && Boons.Has("glamourpuss"))
            foreach (PetFollower p in PetFollower.Pets)
                if (p != null && p.IsCollected) BoonFX.Sparkles(p.transform.position, BoonFX.Gold, 2, 0.2f, 0.5f);
    }

    // Hook Line And Sinker: reel them in, every 3rd rod hit on the same enemy lands the big one
    private void HookLine(EnemyHealth e, Vector3 c)
    {
        UndertowPull.Begin(e, transform, 0.8f, 0.18f, 0.35f);
        rodHits.TryGetValue(e, out int n);
        n++;
        if (n >= 3)
        {
            n = 0;
            BoonFX.Stun(e, 1f);
            BoonFX.Hit(e, Boons.V("hookline", 0), "Hook Line And Sinker");
            PulseRing.Spawn(c, new Color(0.7f, 0.85f, 1f, 1f), 1f, 0.25f);
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.clang : null, 0.5f, 0.8f);
            TimeSlowController.HitStop(0.05f, 0.08f);
        }
        rodHits[e] = n;
        if (rodHits.Count > 40) rodHits.Clear();
        SmithFX.FishingLine(Center, c);
    }

    private IEnumerator ClawEcho(EnemyHealth e, float damage)
    {
        yield return new WaitForSeconds(0.09f);
        if (e == null || e.enemydead) yield break;
        Vector3 c = BoonFX.Center(e);
        BoonArt art = BoonArt.Get;
        if (art != null)
        {
            SheetFX fx = BoonFX.Sheet(art.clawSlash, 6, c, 28f, 1f, Boons.Has("silverclaws") ? new Color(0.85f, 0.92f, 1f) : new Color(1f, 0.55f, 0.6f));
            if (fx != null) fx.transform.localScale = new Vector3(-Facing, 1f, 1f);
            BoonArt.Play(art.claw, 0.25f, Random.Range(1.2f, 1.4f));
        }
        BoonFX.Hit(e, damage, "Feral Swipe");
        if (Boons.Has("rabid")) BoonFX.Poison(e, 4f, 6f + 0.6f * PlayerStats.Level);
    }

    private static Sprite RandomCatSprite()
    {
        var sprites = new List<Sprite>();
        foreach (PetFollower p in PetFollower.Pets)
            if (p != null && p.IsCollected && p.TryGetComponent(out SpriteRenderer sr) && sr.sprite != null) sprites.Add(sr.sprite);
        if (sprites.Count == 0)
            foreach (PetFollower p in PetFollower.Pets)
                if (p != null && p.TryGetComponent(out SpriteRenderer sr) && sr.sprite != null) { sprites.Add(sr.sprite); break; }
        return sprites.Count > 0 ? sprites[Random.Range(0, sprites.Count)] : null;
    }

    // ================================================================ hooks: cats
    public static void OnCatHit(PetFollower cat, EnemyHealth e)
    {
        if (!Ok || cat == null || e == null || e.IsObject) return;
        if (Boons.Has("moldcats") && !e.enemydead) StatusEffects.Of(e).Poison(4f, 5f + 0.5f * PlayerStats.Level, cat); // Mold Cats
    }

    public static void OnCatPower(PetFollower cat)
    {
        if (!Ok || cat == null) return;
        BoonRunner r = instance;
        if (Boons.Has("clawsharpener") && WeaponManager.Instance != null) WeaponManager.Instance.RepairActive(1f); // Claw Sharpener
        if (Boons.Has("compost") && r.health != null)
        {
            BankHeal(Boons.V("compost", 0), false);
            r.StartCoroutine(r.HealStream(cat.transform.position));
        }
        if (Boons.Has("foodfight"))
        {
            EnemyHealth target = BoonFX.Nearest(cat.transform.position, 5f);
            if (target != null)
                for (int i = 0; i < FoodStock; i++) Lobbed.FoodFight(cat.transform.position + Vector3.up * (0.2f + 0.08f * i), target, Boons.V("foodfight", 0) * FoodDamage * Boons.CatDamageFor(cat), cat);
        }
    }

    // green motes drifting from the cat to Rowdy
    private IEnumerator HealStream(Vector3 from)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = Vector3.Lerp(from, Center, i / 5f) + (Vector3)Random.insideUnitCircle * 0.15f;
            FXParticle.Burst(p, BoonFX.Toxic, 2, 0.1f, 0.5f, -1f, 0.5f);
            yield return new WaitForSeconds(0.03f);
        }
    }

    // Small heals (lifesteal, Compost) are pooled and paid out together, so the heal popup doesn't spam every hit
    private float bankedHeal, bankedOverheal, bankFlushAt;

    public static void BankHeal(float amount, bool overheal)
    {
        if (instance == null || amount <= 0f) return;
        if (overheal) instance.bankedOverheal += amount; else instance.bankedHeal += amount;
        if (instance.bankFlushAt <= 0f) instance.bankFlushAt = Time.time + 0.7f;
    }

    private void FlushHeals()
    {
        if (bankFlushAt <= 0f || Time.time < bankFlushAt || health == null) return;
        bankFlushAt = 0f;
        if (bankedHeal > 0f) health.AddHealth(bankedHeal, false);
        if (bankedOverheal > 0f) health.AddHealth(bankedOverheal, true);
        bankedHeal = bankedOverheal = 0f;
    }

    // Health.AddHealth: Secret Sauce makes every heal bigger, and big heals splash hot sauce
    public static float ModifyHeal(float amount)
    {
        if (instance == null || amount <= 0f) return amount;
        amount *= Boons.HealMultiplier;
        if (Boons.Has("secretsauce") && amount >= 5f && Time.time >= instance.sauceReadyAt)
        {
            instance.sauceReadyAt = Time.time + 1.2f;
            ChefFX.SauceSplash(instance.Center, 10f);
        }
        return amount;
    }

    // ================================================================ hooks: kills
    public static void OnEnemyKilled(EnemyHealth e, KillCredit credit, bool byRowdySide)
    {
        if (!Ok || e == null || e.IsObject || !byRowdySide) return;
        BoonRunner r = instance;
        Vector3 c = BoonFX.Center(e);
        Werewolf.AddCharge(8f);
        Werewolf.OnKill();
        StephmossForm.AddCharge(8f);
        StephmossForm.OnKill(c);
        SpecialBoons.AddCharge(2.5f);
        bool poisoned = e.TryGetComponent(out StatusEffects s) && s.IsPoisoned;

        if (Boons.Has("bloodthirst") && r.health != null)
        {
            r.health.AddHealth(Boons.V("bloodthirst", 0), true);
            r.StartCoroutine(r.BloodStream(c));
        }
        if (Boons.Has("feast") && Werewolf.Active) BankHeal(3f, true);
        if (Boons.Has("rabid") && poisoned) Werewolf.AddChargeRaw(5f); // Rabid: poisoned kills feed the moon
        if (Boons.Has("scavenger") && !Boons.Has("justrod") && Random.value < 0.05f * DropLuck.Multiplier) WeaponTricks.DropEveryWeapon(c);
        if (Boons.Has("fishingcats") && credit != null && credit.kind == KillCredit.Kind.Cat && BoonFX.InWater(e)) FishTreat.Drop(c);
        if (Boons.Has("overgrowth") && poisoned) EarthErupt.Spawn(e, c, Boons.V("overgrowth", 0));
        if (Boons.Has("plague") && poisoned) RotFX.Plague(c, e, Mathf.RoundToInt(Boons.V("plague", 0)));
        // Photosynthesis: in daylight flowers grow twice as fast (every 3rd poison kill instead of every 5th)
        int bloomEvery = Boons.Has("photosynthesis") && Boons.DayBoons ? 3 : 5;
        if (Boons.Has("bloom") && poisoned && ++r.bloomKills % bloomEvery == 0) BloomFlower.Spawn(c, Boons.V("bloom", 0));
        if (Boons.Has("huntmark") && HuntersMark.Target == e)
        {
            r.health.AddHealth(10f, false);
            HuntersMark.Clear();
        }
        if (Boons.Has("straytax") && Random.value < Boons.V("straytax", 0) / 100f) FishTreat.Drop(c);
        if (Boons.Has("sandwich") && ++r.sandwichKills >= Mathf.RoundToInt(Boons.V("sandwich", 0)))
        {
            r.sandwichKills = 0;
            for (int i = 0; i < FoodStock; i++) GiantSandwich.Drop(r.Center + new Vector3(r.Facing * (1.2f + 0.9f * i), 0f, 0f));
        }
    }

    private IEnumerator BloodStream(Vector3 from)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = Vector3.Lerp(from, Center, i / 7f) + (Vector3)Random.insideUnitCircle * 0.12f;
            FXParticle.Burst(p, BoonFX.Blood, 2, 0.1f, 0.4f, 0f, 0.35f);
            yield return new WaitForSeconds(0.02f);
        }
        if (body != null) CatFX.Afterimage(body, new Color(1f, 0.2f, 0.3f, 0.35f), 0.2f);
    }

    // Sandwich eaten: +20% damage for a while
    public static void Feed(float seconds)
    {
        if (instance == null) return;
        instance.wellFedUntil = Time.time + seconds;
    }

    // Tempered Steel saved a swing
    public static void OnDurabilitySaved()
    {
        if (instance == null || Random.value > 0.5f) return;
        BoonFX.Sparkles(instance.Center + new Vector3(instance.Facing * 0.4f, 0f, 0f), new Color(0.85f, 0.9f, 1f), 3, 0.2f, 0.4f);
    }

    // ================================================================ hooks: getting hurt
    public static float ModifyIncoming(float damage)
    {
        if (instance == null || damage <= 0f) return damage;
        float m = Boons.IncomingMultiplier;
        if (m < 0.999f && Boons.Has("furcoat") && Boons.CatsWithRowdy > 0)
            FXParticle.Burst(instance.Center, new Color(0.95f, 0.85f, 0.75f), 8, 1f, 3f, 2f, 0.5f);
        return damage * m;
    }

    public static void OnPlayerHurt()
    {
        if (instance == null) return;
        BoonRunner r = instance;
        if (r.posed) r.posed = false;

        // Thorn Skin: the enemy whose hitbox just landed
        if (Boons.Has("thornskin") && Spike.LastAttackFrame >= Time.frameCount - 1 && Spike.LastAttacker != null && !Spike.LastAttacker.enemydead)
            RotFX.Thorns(Spike.LastAttacker, Boons.V("thornskin", 0));

    }

    // True = the hit that would have killed Rowdy was survived (Nine Lives)
    public static bool PreventDeath(Health h)
    {
        if (instance == null || h == null || !Boons.Has("ninelives") || instance.nineLivesUsed) return false;
        BoonRunner r = instance;
        r.nineLivesUsed = true;
        h.SetHealth(h.startingHealth * Boons.V("ninelives", 0) / 100f);
        h.GrantInvulnerability(1.6f);
        TimeSlowController.SlowMotion(0.8f, 0.25f);
        ScreenShake.Impulse(0.8f);
        GamepadRumble.Pulse(0.6f, 0.9f, 0.4f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.nineLives : null, 0.8f, 1f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.guild : null, 0.6f * GameSettings.CatVoiceVolume, 1.2f);
        PulseRing.Spawn(r.Center, new Color(0.9f, 0.8f, 1f, 1f), 3f, 0.5f);
        BoonFX.Sparkles(r.Center, BoonFX.Lavender, 16, 0.8f, 0.9f);
        // ghost cats fly up out of him
        Sprite cat = RandomCatSprite();
        for (int i = 0; i < 4; i++) r.StartCoroutine(r.SpiritCat(cat, i));
        foreach (EnemyHealth e in BoonFX.EnemiesIn(r.Center, 3f)) BoonFX.Push(e, new Vector2(Mathf.Sign(BoonFX.Center(e).x - r.Center.x) * 6f, 3f));
        return true;
    }

    private IEnumerator SpiritCat(Sprite sprite, int i)
    {
        SpriteRenderer sr = BoonFX.MakeRenderer("Spirit Cat", sprite != null ? sprite : BoonFX.Heart, Center, BoonFX.Order + 5, null, CatFX.Silhouette);
        Vector3 start = Center;
        float side = (i - 1.5f) * 0.5f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 1.1f;
            sr.transform.position = start + new Vector3(side * t + Mathf.Sin(t * 8f + i) * 0.15f, t * 2.2f, 0f);
            sr.color = new Color(0.9f, 0.85f, 1f, 0.8f * (1f - t));
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    // ================================================================ hooks: weapons
    public static void OnWeaponBroken(Vector3 at)
    {
        if (!Ok) return;
        if (Boons.Has("rustededge")) SporeCloud.Spawn(instance.Feet + new Vector3(instance.Facing * 0.5f, 0f, 0f), 8f + 0.8f * PlayerStats.Level, false); // Rusted Edge
        if (!Boons.Has("weaponsnob")) return;
        BoonRunner r = instance;
        Vector3 c = r.Center + new Vector3(r.Facing * 0.6f, 0f, 0f);
        ExplosionChain.Boom(c, 1.4f, 0.6f, 0.7f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.bigBoom : null, 0.6f, 1f);
        TimeSlowController.HitStop(0.1f, 0.05f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(c, 2.8f))
        {
            BoonFX.Hit(e, Boons.V("weaponsnob", 0), "Weapon Snob");
            BoonFX.Push(e, new Vector2(Mathf.Sign(BoonFX.Center(e).x - c.x) * 6f, 4f));
        }
    }

    // ================================================================ the boon you just took
    public static void OnBoonTaken(BoonDef d)
    {
        if (d != null) WeaponBoons.OnTaken(d.id); // a weapon boon comes with the weapon
        if (instance == null || d == null) return;
        instance.StartCoroutine(instance.Celebrate(d));
    }

    private IEnumerator Celebrate(BoonDef d)
    {
        while (PauseMenu.IsPaused) yield return null;
        PatronInfo p = BoonCatalog.Of(d.patron);
        Color main = p.color, accent = d.IsDuo ? BoonCatalog.Of(d.partner.Value).color : p.accent;
        PulseRing.Spawn(Center, new Color(main.r, main.g, main.b, 1f), 2.4f, 0.45f);
        PulseRing.Spawn(Center, new Color(accent.r, accent.g, accent.b, 0.8f), 1.5f, 0.35f);
        FXParticle.Burst(Center, main, 24, 1.5f, 4.5f, 1f, 0.9f);
        FXParticle.Burst(Center, accent, 12, 1f, 3.5f, 1f, 0.9f);
        BoonFX.Sparkles(Center, Color.Lerp(main, Color.white, 0.4f), 10, 0.7f, 0.9f);
        if (body != null) CatFX.Afterimage(body, new Color(main.r, main.g, main.b, 0.8f), 0.35f);
        IconPopup.Show(HeadTop + Vector3.up * 0.45f, BoonIcons.Get(d), d.name, main, 1f, 2f);
        ScreenShake.Impulse(0.35f);
        GamepadRumble.Pulse(0.3f, 0.6f, 0.2f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.sparkle : null, 0.35f, 1.1f);
        if (d.id == "moon")
        {
            Werewolf.Fill(); // the first moon is on the house
            Tutorials.Show(Tutorials.Topic.Werewolf, BoonIcons.Get(d), 0.9f);
        }
        else if (d.id == "stephmoss")
        {
            StephmossForm.Fill(); // the first swarm is on the house
            Tutorials.Show(Tutorials.Topic.Boons, BoonIcons.Get(d), 0.9f);
        }
        else if (d.id == "sandwichrain" || d.id == "armory")
        {
            SpecialBoons.Fill(); // the first one is on the house
            Tutorials.Show(Tutorials.Topic.Boons, BoonIcons.Get(d), 0.9f);
        }
        else if (d.id == "mainchar") StyleRank.Announce();
        else Tutorials.Show(Tutorials.Topic.Boons, BoonIcons.Get(d), 0.9f);
    }

    // ================================================================ every frame
    private void Update()
    {
        if (health == null || health.IsDead || PauseMenu.IsPaused) return;
        float dt = Time.deltaTime;
        float now = Time.time;
        FlushHeals();
        bool grounded = Grounded;
        bool watered = movement != null && movement.IsWatered;

        // ---- landings
        if (!grounded && !watered)
        {
            if (!airborne) { airborne = true; airStartTime = now; peakY = Feet.y; }
            peakY = Mathf.Max(peakY, Feet.y);
        }
        else if (airborne)
        {
            airborne = false;
            Land(peakY - Feet.y, now - airStartTime, grounded, now);
        }

        // ---- Admire Yourself: stand still to pose
        if (Boons.Has("admire") && !posed)
        {
            bool still = grounded && rb != null && Mathf.Abs(rb.linearVelocity.x) < 0.05f && Mathf.Abs(GameInput.MoveX) < 0.1f && !Werewolf.Active;
            stillTime = still ? stillTime + dt : 0f;
            if (stillTime >= 1.5f) StrikePose();
        }
        if (posed)
        {
            poseSparkTimer -= dt;
            if (poseSparkTimer <= 0f)
            {
                poseSparkTimer = 0.2f;
                BoonFX.Sparkles(HeadTop + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.1f, 0.15f), 0f), Random.value < 0.5f ? BoonFX.Pink : BoonFX.Gold, 1, 0.05f, 0.5f);
            }
        }

        // ---- Salt Water Blood
        if (Boons.Has("saltwater"))
        {
            if (watered)
            {
                saltHealBank += Boons.V("saltwater", 0) * dt;
                if (saltHealBank >= 2f && health.currentHealth < health.startingHealth)
                {
                    health.AddHealth(saltHealBank, false);
                    saltHealBank = 0f;
                }
                saltFxTimer -= dt;
                if (saltFxTimer <= 0f) { saltFxTimer = 0.15f; FXParticle.Burst(Center + (Vector3)Random.insideUnitCircle * 0.3f, BoonFX.Cyan, 1, 0.2f, 0.6f, -1.5f, 0.6f); }
            }
            else if (wasWatered)
            {
                saltyUntil = now + 4f;
                FXParticle.Burst(Center, BoonFX.Foam, 10, 1f, 3f, 6f, 0.5f);
            }
        }
        wasWatered = watered;

        // ---- Wolf Whistle
        if (Boons.Has("wolfwhistle"))
        {
            whistleTimer -= dt;
            if (whistleTimer <= 0f)
            {
                whistleTimer = 5f;
                int charmed = 0;
                foreach (EnemyHealth e in BoonFX.EnemiesIn(Center, 4.5f))
                {
                    if (Random.value > 0.3f) continue;
                    BoonFX.Charm(e, 1.5f);
                    BoonFX.Popup(BoonFX.Center(e) + Vector3.up * 0.6f, "WOW...", BoonFX.Pink, 0.55f, 0.8f);
                    charmed++;
                }
                if (charmed > 0) BoonFX.Sparkles(HeadTop, BoonFX.Pink, 5, 0.3f, 0.6f);
            }
        }

        // ---- Spotlight: every 10 s, 4 s in the light
        if (Boons.Has("spotlight"))
        {
            spotlightTimer -= dt;
            if (spotlightTimer <= 0f)
            {
                spotlightTimer = 10f;
                spotlightUntil = now + 4f;
                SpotlightFX.Begin(transform, 4f);
            }
        }

        // ---- Jealousy: charmed enemies slap the closest other enemy
        if (Boons.Has("jealousy"))
        {
            jealousyTimer -= dt;
            if (jealousyTimer <= 0f)
            {
                jealousyTimer = 1f;
                NarcFX.JealousSlaps(Center, Boons.V("jealousy", 0));
            }
        }

        // ---- moonrage eye glints at night
        if (Boons.Has("moonrage") && Boons.NightBoons && !Werewolf.Active)
        {
            eyeTimer -= dt;
            if (eyeTimer <= 0f)
            {
                eyeTimer = 0.18f;
                FXParticle.Burst(HeadTop + new Vector3(Facing * 0.08f, -0.14f, 0f), BoonFX.Blood, 1, 0.05f, 0.2f, -0.5f, 0.3f);
            }
        }

        // ---- Daybreak: a slow regen in daylight
        if (Boons.Has("daybreak") && Boons.DayBoons)
        {
            regenTimer -= dt;
            if (regenTimer <= 0f)
            {
                regenTimer = 2f;
                if (health.currentHealth < health.startingHealth)
                {
                    health.AddHealth(1f, false);
                    FXParticle.Burst(Center + (Vector3)Random.insideUnitCircle * 0.25f, BoonFX.Sunny, 2, 0.2f, 0.6f, -1.5f, 0.6f);
                }
            }
        }

        // ---- Main Character sparkles at S and up
        if (Boons.Has("mainchar") && StyleRank.Rank >= 4)
        {
            rankSparkTimer -= dt;
            if (rankSparkTimer <= 0f)
            {
                rankSparkTimer = Mathf.Lerp(0.3f, 0.08f, (StyleRank.Rank - 4) / 2f);
                BoonFX.Sparkles(Center + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.5f, 0.5f), 0f), BoonFX.Gold, 1, 0.05f, 0.6f);
            }
        }

        // ---- things that follow Rowdy around while the boon is owned
        AnglerLure.Keep(Boons.Has("angler"), transform);
        SunHalo.Keep(Boons.Has("halo"), transform);
        HuntersMark.Keep(Boons.Has("huntmark"), Center);
        // Leviathan: below 30% health the tentacles are out, smashing, for as long as it lasts
        LeviathanArms.Keep(Boons.Has("leviathan") && health.currentHealth <= health.startingHealth * 0.3f, this, Boons.V("leviathan", 0));
        AllyJelly.Keep(Boons.Has("jellypals") ? Boons.CatsWithRowdy : 0, transform);
        CatLoyalty.Tick(Boons.Has("catloyalty"), transform);
        WeaponSharpness.Tick(dt);

        UpdateCats(dt);
    }

    // Every landing: Depth Charge, Spore Step, Rip Current, Pounce, Tomato Splat, Anvil Drop, Sunspot
    private void Land(float fall, float air, bool grounded, float now)
    {
        if (!grounded) return;
        if (Boons.Has("hangten") && fall >= 1f) Rings.FoamRing(Feet, Boons.V("hangten", 0) * Mathf.Lerp(1f, 1.6f, Mathf.Clamp01((fall - 1f) / 3f)), Boons.Has("redtide"));
        if (air <= 0.25f) return;
        if (Boons.Has("sporestep") && now >= sporeReadyAt)
        {
            sporeReadyAt = now + (Boons.Has("photosynthesis") && Boons.DayBoons ? 0.25f : 0.5f);
            SporeCloud.Spawn(Feet, Boons.V("sporestep", 0), false);
        }
        if (Boons.Has("ripcurrent") && now >= ripReadyAt)
        {
            ripReadyAt = now + 0.6f;
            AbyssFX.RipCurrent(Feet, transform, Boons.V("ripcurrent", 0) / 100f, Boons.Has("redtide"));
        }
        if (Boons.Has("pounce") && now >= pounceReadyAt)
        {
            List<EnemyHealth> near = BoonFX.EnemiesIn(Feet + Vector3.up * 0.4f, 1.4f);
            if (near.Count > 0)
            {
                pounceReadyAt = now + 0.5f;
                float dmg = Boons.V("pounce", 0) * (Boons.Has("silverclaws") ? 2f : 1f) * Boons.OutgoingMultiplier;
                foreach (EnemyHealth e in near) LycFX.Pounce(e, dmg, Boons.Has("silverclaws"));
                BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.claw : null, 0.45f, 0.9f);
                TimeSlowController.HitStop(0.05f, 0.08f);
            }
        }
        if (Boons.Has("tomato") && now >= tomatoReadyAt)
        {
            tomatoReadyAt = now + 0.5f;
            ChefFX.TomatoSplat(Feet, Boons.V("tomato", 0) * FoodDamage, Boons.Has("fermented"), FoodStock);
        }
        if (Boons.Has("sunspot") && now >= sunspotReadyAt)
        {
            sunspotReadyAt = now + 1.5f;
            SunFX.Sunspot(Feet, Boons.V("sunspot", 0));
        }
    }

    private void StrikePose()
    {
        posed = true;
        stillTime = 0f;
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.pose : null, 0.5f, 1.2f);
        BoonFX.Sparkles(HeadTop, BoonFX.Gold, 8, 0.4f, 0.7f);
        PulseRing.Spawn(Center, new Color(1f, 0.6f, 0.9f, 0.9f), 1f, 0.3f);
        if (body != null) CatFX.Afterimage(body, new Color(1f, 1f, 1f, 0.9f), 0.2f);
    }

    private void UpdateCats(float dt)
    {
        bool red = Boons.Has("packleader") || Boons.CatsFeral;
        bool sparkly = Boons.Has("catnip");
        bool stinky = Boons.Has("stench");
        catFxTimer -= dt;
        bool emit = catFxTimer <= 0f;
        if (emit) catFxTimer = 0.1f;
        stenchTimer -= dt;
        bool stenchTick = stinky && stenchTimer <= 0f;
        if (stenchTick) stenchTimer = 0.5f;
        foreach (PetFollower p in PetFollower.Pets)
        {
            if (p == null) continue;
            bool mine = p.IsCollected;
            catAuras.TryGetValue(p, out SpriteOutline o);
            if (o == null && red && mine && p.TryGetComponent(out SpriteRenderer sr))
            {
                o = SpriteOutline.Add(sr, Color.clear, 1, -2);
                catAuras[p] = o;
            }
            if (o != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (Boons.CatsFeral ? 10f : 4f) + p.GetInstanceID());
                o.color = red && mine ? new Color(1f, 0.15f, 0.25f, (Boons.CatsFeral ? 0.6f : 0.35f) + 0.3f * pulse) : Color.clear;
            }
            if (!mine) continue;
            if (emit)
            {
                if (sparkly && Random.value < 0.6f) BoonFX.Sparkles(p.transform.position + (Vector3)Random.insideUnitCircle * 0.1f, Color.Lerp(BoonFX.Lavender, Color.white, 0.3f), 1, 0.05f, 0.45f);
                if (Boons.CatsFeral && Random.value < 0.5f) FXParticle.Burst(p.transform.position, BoonFX.Blood, 1, 0.2f, 0.5f, -2f, 0.4f);
                if (stinky && Random.value < 0.5f) GuildFX.StinkLine(p.transform.position);
            }
            if (stenchTick) GuildFX.StenchTick(p, Boons.V("stench", 0) * 0.5f);
        }
        CatCrown.Keep(Boons.Has("topcat") ? CatRoster.Leader : null);

        // Sunbathing (Guild + Sun God): in daylight, cats with their power ready glow and heal Rowdy 1 HP every 3 s each
        if (Boons.Has("sunbathing") && Boons.DayBoons)
        {
            sunbathTimer -= dt;
            bool tick = sunbathTimer <= 0f;
            if (tick) sunbathTimer = 3f;
            int ready = 0;
            foreach (PetFollower p in PetFollower.Pets)
            {
                if (p == null || !p.IsCollected || p.CooldownFraction < 1f) continue;
                ready++;
                if (emit && Random.value < 0.35f) BoonFX.Sparkles(p.transform.position + (Vector3)Random.insideUnitCircle * 0.15f, BoonFX.Sunny, 1, 0.05f, 0.5f);
                if (tick) { FXParticle.Burst(p.transform.position, BoonFX.Sunny, 4, 0.5f, 1.5f, -1f, 0.5f); StartCoroutine(HealStream(p.transform.position)); }
            }
            if (tick && ready > 0) BankHeal(ready, false);
        }
    }
    private float sunbathTimer = 3f;

    private void LateUpdate()
    {
        if (movement != null)
        {
            // scale Rowdy's speed after the animator wrote this frame's moveSpeed (see speedBase)
            float m = Boons.SpeedMultiplier;
            if (Mathf.Abs(movement.moveSpeed - speedWritten) > 0.0001f) speedBase = movement.moveSpeed; // fresh from a clip (or untouched)
            if (speedBase >= 0f)
            {
                movement.moveSpeed = speedBase * m;
                speedWritten = movement.moveSpeed;
            }
        }

        // One aura outline, the most important look wins
        if (body == null) return;
        if (aura == null) aura = SpriteOutline.Add(body, Color.clear, 1, -5);
        float t = Time.time;
        Color c = Color.clear;
        if (posed) c = Color.Lerp(BoonFX.Pink, BoonFX.Gold, 0.5f + 0.5f * Mathf.Sin(t * 8f));
        else if (TideRider.Riding) c = TideRider.RidingPink ? new Color(1f, 0.5f, 0.85f, 0.9f) : new Color(0.4f, 0.95f, 1f, 0.9f);
        else if (InSpotlight) c = new Color(1f, 0.95f, 0.75f, 0.6f + 0.3f * Mathf.Sin(t * 9f));
        else if (SaltyActive) c = new Color(0.4f, 0.95f, 1f, 0.55f + 0.25f * Mathf.Sin(t * 10f));
        else if (WellFed) c = new Color(1f, 0.6f, 0.25f, 0.45f + 0.2f * Mathf.Sin(t * 7f));
        else if (Boons.Has("moonrage") && Boons.NightBoons && DayNight.IsNight) c = new Color(1f, 0.15f, 0.25f, 0.3f + 0.15f * Mathf.Sin(t * 3f));
        else if (Boons.Has("mainchar") && StyleRank.Rank > 0) c = new Color(1f, 0.85f, 0.3f, StyleRank.Rank / 6f * (0.45f + 0.2f * Mathf.Sin(t * 6f)));
        else if (Boons.Has("daybreak") && !DayNight.IsNight) c = new Color(1f, 0.85f, 0.4f, 0.18f + 0.08f * Mathf.Sin(t * 2f));
        aura.color = c;
    }
}
