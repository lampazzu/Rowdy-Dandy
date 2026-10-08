using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Runs the owned boons on Rowdy (added by Health.Awake next to RowdyBuffs). The game calls the static hooks:
//   PlayerMovement: OnAttackStarted / OnSurfDash / OnJump        PlayerDamage: ModifyRowdyHit / AfterRowdyHit / OnCatHit
//   EnemyHealth: OnEnemyKilled                                   Health: ModifyIncoming / PreventDeath / OnPlayerHurt
//   PetFollower: OnCatPower                                      WeaponManager: OnWeaponBroken
// Landings, standing still (Admire Yourself), water (Salt Water Blood) and the auras are watched here every frame.
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
    private int swings, hits, mirrorKills;
    private float stillTime, poseSparkTimer;
    private bool posed;
    private float napUntil = -1f, napHeal;
    private float saltyUntil = -10f, saltHealBank, saltFxTimer;
    private bool wasWatered;
    private bool airborne;
    private float airStartTime, peakY;
    private float decoyReadyAt, wipeoutReadyAt, floorReadyAt, catCallReadyAt, sporeReadyAt, funkyReadyAt;
    private float whistleTimer = 3f, eyeTimer, catFxTimer, rankSparkTimer;
    private bool nineLivesUsed;
    private int zombies;
    private readonly Dictionary<PetFollower, SpriteOutline> catAuras = new Dictionary<PetFollower, SpriteOutline>();

    // speed: the animator keys moveSpeed on most of Rowdy's clips, so it's scaled after the animator wrote it
    private float speedBase = -1f, speedWritten = -1f;

    public static bool Napping => instance != null && Time.time < instance.napUntil;
    public static bool SaltyActive => instance != null && Time.time < instance.saltyUntil;
    public static bool Posed => instance != null && instance.posed;

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
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private Vector3 Center => bodyCollider != null ? bodyCollider.bounds.center : transform.position + Vector3.up * 0.4f;
    private Vector3 HeadTop => bodyCollider != null ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.max.y, 0f) : transform.position + Vector3.up * 0.9f;
    private Vector3 Feet => bodyCollider != null ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y, 0f) : transform.position;
    private float Facing => transform.localScale.x >= 0f ? 1f : -1f;
    private bool Grounded => movement == null || movement.IsGrounded;

    private static bool Ok => instance != null && instance.health != null && !instance.health.IsDead;

    // ================================================================ hooks: Rowdy's actions
    public static void OnAttackStarted()
    {
        if (!Ok) return;
        BoonRunner r = instance;
        r.swings++;
        if (Boons.Has("hairflip") && r.swings % 3 == 0)
        {
            HairCrescent.Fire(r.Center + new Vector3(r.Facing * 0.45f, 0.1f, 0f), r.Facing, Boons.V("hairflip", 0));
            BoonFX.Sparkles(r.HeadTop, BoonFX.Pink, 4, 0.25f, 0.4f);
        }
        Werewolf.OnSwing();
    }

    public static void OnSurfDash()
    {
        if (!Ok) return;
        BoonRunner r = instance;
        float now = Time.time;
        if (Boons.Has("decoy") && now >= r.decoyReadyAt)
        {
            r.decoyReadyAt = now + 4f;
            Decoy.Spawn(r.body, Boons.V("decoy", 0));
        }
        if (Boons.Has("wipeout") && now >= r.wipeoutReadyAt)
        {
            r.wipeoutReadyAt = now + 1f;
            RiptideWave.Spawn(r.Feet, r.Facing, Boons.V("wipeout", 0), Boons.Has("beachbod"), Boons.Has("redtide"));
        }
        if (Boons.Has("nightfever") && now >= r.floorReadyAt && r.Grounded)
        {
            r.floorReadyAt = now + 2.5f;
            DiscoFloor.Spawn(r.Feet, r.Facing, Boons.V("nightfever", 0));
        }
        if (Boons.Has("catcall") && now >= r.catCallReadyAt && Boons.CatsWithRowdy > 0)
        {
            r.catCallReadyAt = now + Boons.V("catcall", 0);
            foreach (PetFollower p in PetFollower.Pets)
            {
                if (p == null || !p.IsCollected) continue;
                p.WakeUp();
                PulseRing.Spawn(p.transform.position, new Color(BoonFX.Lavender.r, BoonFX.Lavender.g, BoonFX.Lavender.b, 0.9f), 0.7f, 0.3f);
                BoonFX.Sparkles(p.transform.position, BoonFX.Lavender, 3, 0.2f, 0.5f);
            }
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.catCall : null, 0.5f * GameSettings.CatVoiceVolume, 1.15f);
            BoonFX.Popup(r.HeadTop + Vector3.up * 0.3f, "PSPSPS!", BoonFX.Lavender, 0.7f, 0.9f);
        }
    }

    public static void OnJump()
    {
        if (!Ok) return;
        BoonRunner r = instance;
        if (Boons.Has("funkyfeet") && Time.time >= r.funkyReadyAt)
        {
            r.funkyReadyAt = Time.time + 0.35f;
            Rings.SparkRing(r.Feet + Vector3.up * 0.2f, Boons.V("funkyfeet", 0));
        }
    }

    // ================================================================ hooks: Rowdy's hits
    public static float ModifyRowdyHit(EnemyHealth e, float damage, ref bool crit)
    {
        if (!Ok || e == null) return damage;
        BoonRunner r = instance;
        damage *= Boons.OutgoingMultiplier;
        if (r.posed && !e.IsObject)
        {
            r.posed = false;
            damage *= Boons.V("admire", 0);
            Vector3 c = BoonFX.Center(e);
            BoonFX.Popup(c + Vector3.up * 0.7f, "GORGEOUS!", BoonFX.Pink, 1.1f, 1.1f);
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
        if (Werewolf.Active) BankHeal(1f, true); // lifesteal

        if (Boons.Has("glassjaw"))
        {
            FXParticle.Burst(c, BoonFX.Gold, 6, 2f, 4f, 3f, 0.35f);
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.goldFist : null, 0.2f, Random.Range(1.2f, 1.5f));
        }
        if (Boons.Has("undertow") && !e.enemydead)
        {
            float toward = Mathf.Sign(r.Center.x - c.x);
            BoonFX.Push(e, new Vector2(toward * 3.5f, 1f));
            BoonFX.Slow(e, 2f, Boons.V("undertow", 0) / 100f);
            FXParticle.Burst(c, BoonFX.Cyan, 6, 1f, 2.5f, 2f, 0.4f);
            if (Random.value < 0.35f) BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.boto : null, 0.25f, Random.Range(1.1f, 1.3f));
        }
        if (Boons.Has("rottenedge") && !e.enemydead) BoonFX.Poison(e, 4f, Boons.V("rottenedge", 0));
        if (Boons.Has("feral")) r.StartCoroutine(r.ClawEcho(e, damage * Boons.V("feral", 0) / 100f));
        if (Boons.Has("chainslap")) r.ChainLightning(e, Boons.V("chainslap", 0), Mathf.RoundToInt(Boons.V("chainslap", 1)), "Chain Slap");
        if (Boons.Has("felinefury"))
        {
            r.hits++;
            if (r.hits % 4 == 0 && !e.enemydead)
                GhostCat.Pounce(c + new Vector3(-r.Facing * 1.2f, 1.6f, 0f), e, Boons.V("felinefury", 0), RandomCatSprite(), BoonFX.Lavender);
        }
        if (crit && Boons.Has("glamourpuss"))
            foreach (PetFollower p in PetFollower.Pets)
                if (p != null && p.IsCollected) BoonFX.Sparkles(p.transform.position, BoonFX.Gold, 2, 0.2f, 0.5f);
    }

    private IEnumerator ClawEcho(EnemyHealth e, float damage)
    {
        yield return new WaitForSeconds(0.09f);
        if (e == null || e.enemydead) yield break;
        Vector3 c = BoonFX.Center(e);
        BoonArt art = BoonArt.Get;
        if (art != null)
        {
            SheetFX fx = BoonFX.Sheet(art.clawSlash, 6, c, 28f, 1.1f, new Color(1f, 0.55f, 0.6f));
            if (fx != null) fx.transform.localScale = new Vector3(-Facing * 1.1f, 1.1f, 1f);
            BoonArt.Play(art.claw, 0.25f, Random.Range(1.2f, 1.4f));
        }
        BoonFX.Hit(e, damage, "Feral Swipe");
    }

    private void ChainLightning(EnemyHealth from, float damage, int jumps, string with, PetFollower cat = null)
    {
        var visited = new List<EnemyHealth> { from };
        Vector3 a = BoonFX.Center(from);
        bool any = false;
        for (int i = 0; i < jumps; i++)
        {
            EnemyHealth next = BoonFX.Nearest(a, 4f, visited);
            if (next == null) break;
            visited.Add(next);
            Vector3 b = BoonFX.Center(next);
            BoonFX.Lightning(a, b, BoonFX.Disco, 0.2f);
            ItemArtHit(b);
            BoonFX.Hit(next, damage, with, cat);
            a = b;
            any = true;
        }
        if (any) BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.zap : null, 0.3f, Random.Range(1.3f, 1.6f));
    }

    private static void ItemArtHit(Vector3 at)
    {
        BoonArt art = BoonArt.Get;
        if (art != null) BoonFX.Sheet(art.magicalHit, 10, at, 30f, 0.45f, new Color(1f, 0.95f, 0.5f));
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
        if (Boons.Has("catscratch")) instance.ChainLightning(e, 10f, 2, "Cat Scratch Fever", cat);
    }

    public static void OnCatPower(PetFollower cat)
    {
        if (!Ok || cat == null) return;
        BoonRunner r = instance;
        if (Boons.Has("compost") && r.health != null)
        {
            BankHeal(Boons.V("compost", 0), false);
            r.StartCoroutine(r.HealStream(cat.transform.position));
        }
        if (Boons.Has("catscratch"))
        {
            EnemyHealth first = BoonFX.Nearest(cat.transform.position, 5f);
            if (first != null)
            {
                BoonFX.Lightning(cat.transform.position, BoonFX.Center(first), BoonFX.Disco, 0.2f);
                BoonFX.Hit(first, 10f, "Cat Scratch Fever", cat);
                r.ChainLightning(first, 10f, 1, "Cat Scratch Fever", cat);
            }
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

    // ================================================================ hooks: kills
    public static void OnEnemyKilled(EnemyHealth e, KillCredit credit, bool byRowdySide)
    {
        if (!Ok || e == null || e.IsObject || !byRowdySide) return;
        BoonRunner r = instance;
        Vector3 c = BoonFX.Center(e);
        Werewolf.AddCharge(8f);
        Werewolf.OnKill();

        if (Boons.Has("bloodthirst") && r.health != null)
        {
            r.health.AddHealth(Boons.V("bloodthirst", 0), true);
            r.StartCoroutine(r.BloodStream(c));
        }
        if (Boons.Has("overgrowth") && e.TryGetComponent(out StatusEffects s) && s.IsPoisoned)
            VineBurst.Spawn(c, Boons.V("overgrowth", 0));
        if (Boons.Has("mirrorball"))
        {
            r.mirrorKills++;
            if (r.mirrorKills >= 10) { r.mirrorKills = 0; MirrorBall.Drop(r.transform, Boons.V("mirrorball", 0)); }
            else if (r.mirrorKills >= 7) BoonFX.Popup(r.HeadTop + Vector3.up * 0.3f, (10 - r.mirrorKills) + " TO DISCO", BoonFX.Disco, 0.55f, 0.8f);
        }
        if (Boons.Has("thriller") && DayNight.IsNight && r.zombies < 4)
        {
            r.StartCoroutine(r.ZombieLater(e));
        }
        if (Boons.Has("naptime") && credit != null && credit.kind == KillCredit.Kind.Rowdy && !Napping)
        {
            r.napUntil = Time.time + 1.2f;
            r.napHeal = Boons.V("naptime", 0);
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.yawn : null, 0.35f, 0.9f);
            BoonFX.Popup(r.HeadTop + Vector3.up * 0.3f, "YAWN...", new Color(0.7f, 0.8f, 1f), 0.65f, 1f);
        }
    }

    private IEnumerator ZombieLater(EnemyHealth e)
    {
        zombies++;
        yield return new WaitForSeconds(0.5f);
        if (e != null) DanceZombie.Raise(e);
        yield return new WaitForSeconds(8.5f);
        zombies--;
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
        instance.mirrorKills = 0;
        if (instance.posed) { instance.posed = false; BoonFX.Popup(instance.HeadTop + Vector3.up * 0.3f, "MY HAIR!", BoonFX.Pink, 0.6f, 0.8f); }
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
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.meow : null, 0.6f * GameSettings.CatVoiceVolume, 1.2f);
        BoonFX.Popup(r.HeadTop + Vector3.up * 0.5f, "NINE LIVES!", BoonFX.Lavender, 1.3f, 1.8f);
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
        if (!Ok || !Boons.Has("weaponsnob")) return;
        BoonRunner r = instance;
        Vector3 c = r.Center + new Vector3(r.Facing * 0.6f, 0f, 0f);
        ExplosionChain.Boom(c, 1.4f, 0.6f, 0.7f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.bigBoom : null, 0.6f, 1f);
        TimeSlowController.HitStop(0.1f, 0.05f);
        BoonFX.Popup(c + Vector3.up * 0.9f, "SNOB BOMB!", new Color(1f, 0.75f, 0.35f), 1f, 1.3f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(c, 2.8f))
        {
            BoonFX.Hit(e, Boons.V("weaponsnob", 0), "Weapon Snob");
            BoonFX.Push(e, new Vector2(Mathf.Sign(BoonFX.Center(e).x - c.x) * 6f, 4f));
        }
    }

    // ================================================================ the boon you just took
    public static void OnBoonTaken(BoonDef d)
    {
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
        if (d.id == "moon")
        {
            Werewolf.Fill(); // the first moon is on the house
            Tutorials.Show(Tutorials.Topic.Werewolf, BoonIcons.Get(d), 0.9f);
        }
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

        // ---- landings (Hang Ten, Spore Step)
        if (!grounded && !watered)
        {
            if (!airborne) { airborne = true; airStartTime = now; peakY = Feet.y; }
            peakY = Mathf.Max(peakY, Feet.y);
        }
        else if (airborne)
        {
            airborne = false;
            float fall = peakY - Feet.y;
            float air = now - airStartTime;
            if (grounded && Boons.Has("hangten") && fall >= 1f) Rings.FoamRing(Feet, Boons.V("hangten", 0) * Mathf.Lerp(1f, 1.6f, Mathf.Clamp01((fall - 1f) / 3f)), Boons.Has("redtide"));
            if (grounded && Boons.Has("sporestep") && air > 0.25f && now >= sporeReadyAt)
            {
                sporeReadyAt = now + 0.5f;
                SporeCloud.Spawn(Feet, Boons.V("sporestep", 0), Boons.Has("ravemold"));
            }
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
                BoonFX.Popup(HeadTop + Vector3.up * 0.3f, "SALTY!", BoonFX.Cyan, 0.7f, 0.9f);
                FXParticle.Burst(Center, BoonFX.Foam, 10, 1f, 3f, 6f, 0.5f);
            }
        }
        wasWatered = watered;

        // ---- Nap Time
        if (napUntil > 0f)
        {
            if (now < napUntil)
            {
                eyeTimer -= dt;
                if (eyeTimer <= 0f)
                {
                    eyeTimer = 0.3f;
                    SpriteRenderer z = BoonFX.MakeRenderer("Zzz", BoonFX.Zzz, HeadTop + new Vector3(Facing * 0.2f, 0.1f, 0f), BoonFX.Order + 3);
                    z.color = new Color(0.75f, 0.85f, 1f);
                    z.gameObject.AddComponent<Twinkle>().Begin(0.9f, 1.4f);
                }
            }
            else
            {
                napUntil = -1f;
                health.AddHealth(napHeal, false);
                BoonFX.Popup(HeadTop + Vector3.up * 0.3f, "REFRESHED!", new Color(0.7f, 0.85f, 1f), 0.65f, 0.9f);
            }
        }

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

        // ---- moonrage eye glints at night
        if (Boons.Has("moonrage") && DayNight.IsNight && !Werewolf.Active)
        {
            eyeTimer -= dt;
            if (eyeTimer <= 0f && napUntil < 0f)
            {
                eyeTimer = 0.18f;
                FXParticle.Burst(HeadTop + new Vector3(Facing * 0.08f, -0.14f, 0f), BoonFX.Blood, 1, 0.05f, 0.2f, -0.5f, 0.3f);
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

        UpdateCats(dt);
    }

    private void StrikePose()
    {
        posed = true;
        stillTime = 0f;
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.pose : null, 0.5f, 1.2f);
        BoonFX.Popup(HeadTop + Vector3.up * 0.35f, "LOOKING GOOD!", BoonFX.Pink, 0.8f, 1.1f);
        BoonFX.Sparkles(HeadTop, BoonFX.Gold, 8, 0.4f, 0.7f);
        PulseRing.Spawn(Center, new Color(1f, 0.6f, 0.9f, 0.9f), 1f, 0.3f);
        if (body != null) CatFX.Afterimage(body, new Color(1f, 1f, 1f, 0.9f), 0.2f);
    }

    private void UpdateCats(float dt)
    {
        bool red = Boons.Has("packleader") || Boons.CatsFeral;
        bool sparkly = Boons.Has("catnip");
        catFxTimer -= dt;
        bool emit = catFxTimer <= 0f;
        if (emit) catFxTimer = 0.1f;
        foreach (PetFollower p in PetFollower.Pets)
        {
            if (p == null) continue;
            bool mine = p.IsCollected;
            if (!catAuras.TryGetValue(p, out SpriteOutline o) || o == null)
            {
                if (!red || !mine || !p.TryGetComponent(out SpriteRenderer sr)) continue;
                o = SpriteOutline.Add(sr, Color.clear, 1, -2);
                catAuras[p] = o;
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (Boons.CatsFeral ? 10f : 4f) + p.GetInstanceID());
            o.color = red && mine ? new Color(1f, 0.15f, 0.25f, (Boons.CatsFeral ? 0.6f : 0.35f) + 0.3f * pulse) : Color.clear;
            if (emit && mine)
            {
                if (sparkly && Random.value < 0.6f) BoonFX.Sparkles(p.transform.position + (Vector3)Random.insideUnitCircle * 0.1f, Color.Lerp(BoonFX.Lavender, Color.white, 0.3f), 1, 0.05f, 0.45f);
                if (Boons.CatsFeral && Random.value < 0.5f) FXParticle.Burst(p.transform.position, BoonFX.Blood, 1, 0.2f, 0.5f, -2f, 0.4f);
            }
        }
    }

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

        // One aura outline, the most important look wins: pose > salty > moonrage > main character
        if (body == null) return;
        if (aura == null) aura = SpriteOutline.Add(body, Color.clear, 1, -5);
        float t = Time.time;
        Color c = Color.clear;
        if (posed) c = Color.Lerp(BoonFX.Pink, BoonFX.Gold, 0.5f + 0.5f * Mathf.Sin(t * 8f));
        else if (SaltyActive) c = new Color(0.4f, 0.95f, 1f, 0.55f + 0.25f * Mathf.Sin(t * 10f));
        else if (Boons.Has("moonrage") && DayNight.IsNight) c = new Color(1f, 0.15f, 0.25f, 0.3f + 0.15f * Mathf.Sin(t * 3f));
        else if (Boons.Has("mainchar") && StyleRank.Rank > 0) c = new Color(1f, 0.85f, 0.3f, StyleRank.Rank / 6f * (0.45f + 0.2f * Mathf.Sin(t * 6f)));
        else if (Napping) c = new Color(0.6f, 0.7f, 1f, 0.4f);
        aura.color = c;
    }
}
