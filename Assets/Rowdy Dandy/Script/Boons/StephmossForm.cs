using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// STEPHMOSS, LORD OF SEVERED WOODS (the poison patron's special boon, art + sounds from the user's Graft):
// press {WOLF} (B / Circle, K) when the meter is full and Rowdy BECOMES Stephmoss for a while. Rowdy is switched off
// underneath (his controller, animator, hitboxes and sounds), so nothing of him plays or sounds - it's all the bug:
//   - WALK = ROLL: moving spins him into the whirlwind (WhirlBegin -> WhirlLoop -> WhirlEnd); everything it touches
//     takes damage and poison. He can jump while rolling.
//   - DASH = TELEPORT: he vanishes (BRJ_Bug_Vanish) and appears (BRJ_Bug_Appear) on a platform nearby in the
//     direction held (or the way he faces).
//   - ATTACK = CROSS BLADE (BRJ_Bug_Charge body + effect): a heavy wind-up, then a huge X cut in front of him.
//   - DOWN + ATTACK = SPAWN (BRJ_Bug_SpawnInsect): locked in place, he releases mosquitoes that hunt enemies and
//     explode on contact.
//   - heavy and strong: slow walk, low jump, big landings shake the ground; x1.4 damage, 35% less damage taken,
//     hits never stagger him (Health), weapons don't wear out.
public class StephmossForm : MonoBehaviour
{
    public const float DamageMultiplier = 1.4f;
    private static readonly Vector2 Feet = new Vector2(136f, 164f); // the bug's feet on the GIFs' 273 x 165 canvas
    public static readonly Color Toxic = new Color(0.72f, 1f, 0.25f);
    private static readonly Color Moss = new Color(0.95f, 0.85f, 0.25f);

    // feel
    private const float WalkSpeed = 2.4f, JumpSpeed = 7f, Accel = 18f;
    private const float WhirlTick = 0.22f, WhirlRadius = 1.25f;
    private const float TeleportRange = 4f, TeleportCooldown = 0.8f;
    private const int SlashHitFrame = 11, SpawnFrame = 6, Mosquitoes = 3;

    private static StephmossForm instance;
    private static float charge, timeLeft, duration;
    private static bool active, transforming;

    public static bool Active => instance != null && active;
    public static bool Transforming => instance != null && transforming;
    public static float Charge01 => Mathf.Clamp01(charge / 100f);
    public static bool Ready => charge >= 100f;
    public static float TimeLeft01 => duration > 0f ? Mathf.Clamp01(timeLeft / duration) : 0f;

    private enum State { Idle, Roll, Slash, Spawn, Teleport }

    private Health health;
    private PlayerMovement movement;
    private Animator rowdyAnimator;
    private Rigidbody2D rb;
    private SpriteRenderer body, bug, fx;
    private Collider2D bodyCollider;
    private Light2D glow;
    private AudioSource whirlLoop;
    private Sprite[] idle, appear, vanish, rollBeginBody, rollBeginFx, rollBody, rollFx, rollEndBody, rollEndFx, slashBody, slashFx, spawn;
    private readonly List<SpriteRenderer> hidden = new List<SpriteRenderer>();

    private State state;
    private float stateAt, rollStoppedAt = -10f, whirlTimer, moteTimer, deniedAt = -10f, teleportReadyAt, savedGravity;
    private bool rolling, slashLanded, spawned, airborne, jumpQueued;
    private float peakY;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { charge = 0f; active = transforming = false; timeLeft = duration = 0f; }

    public static void ResetCharge() { charge = 0f; }
    public static void Fill() { charge = 100f; }

    public static void AddCharge(float amount)
    {
        if (!Boons.Has("stephmoss") || active || transforming) return;
        bool was = Ready;
        charge = Mathf.Min(100f, charge + amount);
        if (!was && Ready && instance != null)
        {
            BoonFX.Popup(instance.HeadTop + Vector3.up * 0.4f, GameInput.Format("THE SWARM IS READY {WOLF}"), Toxic, 0.85f, 1.6f);
            FXSound.Play("StephmossScissor", 0.3f, 1.3f);
            BoonHUD.FlashMoon();
        }
    }

    // (Rowdy's own swings don't happen in this form: kept for BoonRunner)
    public static void OnSwing() { }
    public static void OnKill(Vector3 at) { if (Active) timeLeft = Mathf.Min(duration, timeLeft + 0.5f); }

    private void Awake()
    {
        instance = this;
        health = GetComponent<Health>();
        movement = GetComponent<PlayerMovement>();
        rowdyAnimator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        active = transforming = false;
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; active = transforming = false; }
        if (bug != null) Destroy(bug.gameObject);
    }

    private Vector3 Center => bodyCollider != null ? bodyCollider.bounds.center : transform.position + Vector3.up * 0.4f;
    private Vector3 HeadTop => bodyCollider != null ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.max.y, 0f) : transform.position + Vector3.up * 0.9f;
    private Vector3 FeetPos => bodyCollider != null ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y, 0f) : transform.position;
    private float Facing => transform.localScale.x >= 0f ? 1f : -1f;
    private float PoisonDps => 6f + 1.5f * PlayerStats.Level;
    private float Power => Boons.OutgoingMultiplier;

    private bool Grounded
    {
        get
        {
            if (rb != null && rb.linearVelocity.y > 0.1f) return false;
            Vector3 f = FeetPos;
            float half = bodyCollider != null ? bodyCollider.bounds.extents.x * 0.8f : 0.2f;
            foreach (float dx in new[] { -half, 0f, half })
                if (SolidGround.Ray(new Vector2(f.x + dx, f.y + 0.06f), Vector2.down, 0.14f, out RaycastHit2D h) && !IsMine(h.collider)) return true;
            return false;
        }
    }

    private bool IsMine(Collider2D c) => c != null && c.transform.IsChildOf(transform);

    // ---------------------------------------------------------------- input / meter
    private void Update()
    {
        if (!Boons.Has("stephmoss")) { if (active || transforming) EndNow(false); return; }
        if ((active || transforming) && (health == null || health.IsDead)) { EndNow(false); return; }
        if (PauseMenu.IsPaused || BoonPicker.IsOpen) return;

        if (!active && !transforming) charge = Mathf.Min(100f, charge + Time.deltaTime * 0.9f);

        bool menuJustClosed = RowdyNotes.BlocksPause || WorldMap.BlocksPause || Tutorials.BlocksPause || BoonPicker.BlocksInput || CatParty.BlocksPause || CheckpointMenu.BlocksPause || StatsPause.BlocksPause;
        if (!active && !transforming && !menuJustClosed && GameInput.Down(GameInput.Act.Werewolf) && health != null && !health.IsDead)
        {
            if (Ready) StartCoroutine(Become());
            else if (Time.unscaledTime - deniedAt > 0.5f)
            {
                deniedAt = Time.unscaledTime;
                BoonHUD.ShakeMoon();
                BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.cancel : null, 0.4f, 1f);
            }
        }

        if (!active) return;
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f && state != State.Teleport) { StartCoroutine(Leave()); return; }
        if (menuJustClosed) return;

        Controls();
        Ambient();
        Landing();
    }

    private void Controls()
    {
        float t = Time.time - stateAt;
        bool locked = state == State.Slash || state == State.Spawn || state == State.Teleport;
        float mx = GameInput.MoveX;

        if (!locked)
        {
            if (mx != 0f) transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * Mathf.Sign(mx), transform.localScale.y, transform.localScale.z);
            bool grounded = Grounded;
            if (GameInput.Down(GameInput.Act.Jump) && grounded) jumpQueued = true;

            if (GameInput.Down(GameInput.Act.SurfDash) && Time.time >= teleportReadyAt) { StartCoroutine(Teleport(mx, GameInput.MoveY)); return; }
            if (GameInput.Down(GameInput.Act.Attack))
            {
                if (GameInput.MoveY < 0f && grounded) Go(State.Spawn);
                else Go(State.Slash);
                return;
            }

            // walking = rolling
            bool wantsRoll = mx != 0f;
            if (wantsRoll && !rolling) { rolling = true; stateAt = Time.time; state = State.Roll; whirlTimer = 0.1f; }
            else if (!wantsRoll && rolling) { rolling = false; rollStoppedAt = Time.time; state = State.Idle; }
            if (rolling) Whirlwind();
        }

        if (state == State.Slash)
        {
            if (!slashLanded && FrameAt(t, 1.6f, 2, slashBody.Length) >= SlashHitFrame) { slashLanded = true; CrossCut(); }
            if (t >= (slashBody.Length - 2) / (GraftFX.Fps("BRJ_Bug_Charge_Body") * 1.6f)) Go(State.Idle);
        }
        else if (state == State.Spawn)
        {
            if (!spawned && FrameAt(t, 1.3f, 0, spawn.Length) >= SpawnFrame) { spawned = true; SpawnMosquitoes(); }
            if (t >= spawn.Length / (GraftFX.Fps("BRJ_Bug_SpawnInsect") * 1.3f)) Go(State.Idle);
        }
        UpdateLoopSound();
    }

    private static int FrameAt(float t, float speed, int first, int count) => Mathf.Clamp(first + (int)(t * 10f * speed), 0, count - 1);

    private void Go(State s)
    {
        state = s;
        stateAt = Time.time;
        slashLanded = spawned = false;
        if (s != State.Roll) rolling = false;
        if (s == State.Slash) FXSound.Play("StephmossWhirl", 0.35f, 0.7f);  // the heavy wind-up
        if (s == State.Spawn) FXSound.Play("StephmossScissor", 0.35f, 0.6f);
    }

    private void FixedUpdate()
    {
        if (!active || rb == null || state == State.Teleport) return;
        bool locked = state == State.Slash || state == State.Spawn;
        float target = locked ? 0f : GameInput.MoveX * WalkSpeed * Boons.SpeedMultiplier;
        Vector2 v = rb.linearVelocity;
        v.x = Mathf.MoveTowards(v.x, target, Accel * Time.fixedDeltaTime);
        if (jumpQueued) { jumpQueued = false; v.y = JumpSpeed; FXSound.Play("StephmossWhirl", 0.2f, 1.5f); }
        rb.linearVelocity = v;
    }

    // ---------------------------------------------------------------- moves
    private void Whirlwind()
    {
        whirlTimer -= Time.deltaTime;
        if (whirlTimer > 0f) return;
        whirlTimer = WhirlTick;
        int hits = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(Center, WhirlRadius))
        {
            BoonFX.Hit(e, (5f + 1.5f * PlayerStats.Level) * Power, "Stephmoss Whirlwind");
            BoonFX.Poison(e, 3f, PoisonDps);
            BoonFX.Push(e, new Vector2(Mathf.Sign(BoonFX.Center(e).x - Center.x) * 3f, 1.5f));
            GraftFX.Play("StephHit", BoonFX.Center(e), Color.white, 97, null, false, Random.value < 0.5f, 1.4f);
            hits++;
        }
        if (hits > 0) { FXSound.Play("StephmossHit", 0.35f, Random.Range(1.05f, 1.2f)); ScreenShake.Impulse(0.12f); }
    }

    private void CrossCut()
    {
        Vector3 at = Center + new Vector3(Facing * 1.1f, 0.1f, 0f);
        FXSound.Play("StephmossScissor", 0.8f, 0.85f);
        ScreenShake.Impulse(0.55f);
        GamepadRumble.Pulse(0.5f, 0.8f, 0.2f);
        int hits = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(at, 1.9f))
        {
            BoonFX.Hit(e, (24f + 7f * PlayerStats.Level) * Power, "Stephmoss Cross");
            BoonFX.Poison(e, 5f, PoisonDps);
            BoonFX.Push(e, new Vector2(Facing * 6f, 3f));
            GraftFX.Play("StephHit", BoonFX.Center(e), Color.white, 97, null, false, Facing < 0f);
            hits++;
        }
        if (hits > 0) { TimeSlowController.HitStop(0.09f, 0.05f); FXSound.Play("StephmossHit", 0.7f, 0.8f); }
        FXParticle.Burst(at, Toxic, 16, 1.5f, 5f, 2f, 0.5f);
    }

    private void SpawnMosquitoes()
    {
        FXSound.Play("StephmossWhirl", 0.4f, 1.6f);
        PulseRing.Spawn(Center, new Color(Toxic.r, Toxic.g, Toxic.b, 0.9f), 1.4f, 0.3f);
        for (int i = 0; i < Mosquitoes; i++)
            StephInsect.Hatch(HeadTop + new Vector3(Facing * (0.2f + 0.3f * i), -0.1f * i, 0f), PoisonDps, (10f + 3f * PlayerStats.Level) * Power);
    }

    // Vanishes, then appears on the ground nearby in the direction held (the way he faces if nothing is held)
    private IEnumerator Teleport(float mx, float my)
    {
        Vector2 dir = new Vector2(mx, my);
        if (dir.sqrMagnitude < 0.01f) dir = new Vector2(Facing, 0f);
        dir.Normalize();
        if (!FindLanding(dir, out Vector3 landing))
        {
            UISound.Play(UISound.Cue.Locked);
            teleportReadyAt = Time.time + 0.3f;
            yield break;
        }
        Go(State.Teleport);
        rolling = false;
        teleportReadyAt = Time.time + TeleportCooldown;
        health.GrantInvulnerability(0.75f);
        savedGravity = rb != null ? rb.gravityScale : savedGravity;
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.gravityScale = 0f; }
        FXSound.Play("SmokePoof", 0.5f, 0.8f);
        FXParticle.Burst(Center, Moss, 14, 1f, 3f, 0f, 0.5f);

        float len = vanish.Length / (GraftFX.Fps("BRJ_Bug_Vanish") * 2.4f);
        for (float t = 0f; t < len; t += Time.deltaTime) { bug.sprite = vanish[Mathf.Clamp((int)(t / len * vanish.Length), 0, vanish.Length - 1)]; Place(); yield return null; }

        Vector3 offset = transform.position - FeetPos;
        Vector3 to = landing + offset + Vector3.up * 0.02f;
        if (rb != null) rb.position = to;
        transform.position = to;
        if (dir.x != 0f) transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * Mathf.Sign(dir.x), transform.localScale.y, transform.localScale.z);

        FXSound.Play("StephmossScissor", 0.45f, 1.2f);
        len = appear.Length / (GraftFX.Fps("BRJ_Bug_Appear") * 2.4f);
        for (float t = 0f; t < len; t += Time.deltaTime) { bug.sprite = appear[Mathf.Clamp((int)(t / len * appear.Length), 0, appear.Length - 1)]; Place(); yield return null; }

        if (rb != null) rb.gravityScale = savedGravity;
        // arriving knocks back whoever stands there
        foreach (EnemyHealth e in BoonFX.EnemiesIn(Center, 1.4f))
        {
            BoonFX.Hit(e, (8f + 2f * PlayerStats.Level) * Power, "Stephmoss Teleport");
            BoonFX.Push(e, new Vector2(Mathf.Sign(BoonFX.Center(e).x - Center.x) * 5f, 3f));
        }
        Go(State.Idle);
    }

    private bool FindLanding(Vector2 dir, out Vector3 landing)
    {
        landing = Vector3.zero;
        Vector3 feet = FeetPos;
        float halfW = bodyCollider != null ? bodyCollider.bounds.extents.x : 0.25f, height = bodyCollider != null ? bodyCollider.bounds.size.y : 1.1f;
        for (float d = TeleportRange; d >= 1.2f; d -= 0.4f)
        {
            Vector2 probe = (Vector2)feet + dir * d;
            // the first floor under that point (from a bit above it)
            if (!SolidGround.Ray(probe + Vector2.up * 1.6f, Vector2.down, 4f, out RaycastHit2D hit) || IsMine(hit.collider) || hit.normal.y < 0.6f) continue;
            if (SolidGround.Blocked(hit.point + new Vector2(0f, height / 2f + 0.08f), new Vector2(halfW * 1.6f, height * 0.85f))) continue;
            landing = hit.point;
            return true;
        }
        return false;
    }

    private void Landing()
    {
        bool grounded = Grounded;
        if (!grounded) { if (!airborne) { airborne = true; peakY = FeetPos.y; } peakY = Mathf.Max(peakY, FeetPos.y); return; }
        if (!airborne) return;
        airborne = false;
        float fall = peakY - FeetPos.y;
        ScreenShake.Impulse(fall > 1f ? 0.35f : 0.12f); // every landing is heavy
        FXParticle.Burst(FeetPos, new Color(0.6f, 0.55f, 0.35f), 6, 1f, 2.5f, 6f, 0.4f, true);
        if (fall < 1f) return;
        // a big landing: the ground splits, poison spills out
        GraftFX.Play("Steph_BounceVFx", FeetPos, Color.white, (body != null ? body.sortingOrder : 80) + 2, null, false, false, 1.3f, new Vector2(0.5f, 0.05f));
        GroundShock.Spawn(FeetPos, 2.4f, Toxic, new Color(0.35f, 0.4f, 0.15f), 0.4f);
        FXSound.Play("RockSmash", 0.35f, 0.9f);
        foreach (EnemyHealth e in BoonFX.EnemiesIn(FeetPos + Vector3.up * 0.4f, 2.4f))
        {
            BoonFX.Hit(e, (8f + 2f * PlayerStats.Level) * Power, "Stephmoss Slam");
            BoonFX.Poison(e, 4f, PoisonDps);
            BoonFX.Push(e, new Vector2(Mathf.Sign(BoonFX.Center(e).x - FeetPos.x) * 5f, 4f));
        }
    }

    private void UpdateLoopSound()
    {
        if (whirlLoop == null) return;
        bool on = rolling && active;
        if (on && !whirlLoop.isPlaying) whirlLoop.Play();
        else if (!on && whirlLoop.isPlaying) whirlLoop.Stop();
        whirlLoop.volume = 0.3f * GameSettings.SfxVolume;
    }

    private void Ambient()
    {
        moteTimer -= Time.deltaTime;
        if (moteTimer > 0f) return;
        moteTimer = 0.08f;
        FXParticle.Burst(Center + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.4f, 0.5f), 0f), Random.value < 0.7f ? Toxic : Moss, 1, 0.2f, 0.6f, -1.5f, 0.6f);
    }

    // ---------------------------------------------------------------- in and out
    private IEnumerator Become()
    {
        transforming = true;
        charge = 0f;
        duration = timeLeft = Mathf.Max(6f, Boons.V("stephmoss", 0));
        RunStats.Transformations++;
        Ensure();
        TakeOver(true);
        TimeSlowController.SlowMotion(0.9f, 0.35f);
        FXSound.Play("StephmossWhirl", 0.55f, 1.1f);
        GamepadRumble.Pulse(0.3f, 0.5f, 0.4f);

        // the swarm gathers where Rowdy stands, and he's gone
        float t = 0f, len = appear.Length / GraftFX.Fps("BRJ_Bug_Appear");
        while (t < len)
        {
            t += Time.unscaledDeltaTime;
            bug.enabled = true;
            bug.sprite = appear[Mathf.Clamp((int)(t / len * appear.Length), 0, appear.Length - 1)];
            Place();
            if (Random.value < 0.5f) FXParticle.Burst(Center + (Vector3)Random.insideUnitCircle * 0.6f, Random.value < 0.6f ? Moss : new Color(0.2f, 0.25f, 0.1f), 1, 0.5f, 2f, 1f, 0.5f);
            yield return null;
        }

        transforming = false;
        active = true;
        state = State.Idle;
        rolling = false;
        TimeSlowController.HitStop(0.1f, 0.05f);
        ScreenShake.Impulse(0.7f);
        FXSound.Play("StephmossScissor", 0.7f, 0.9f);
        PulseRing.Spawn(Center, new Color(Toxic.r, Toxic.g, Toxic.b, 1f), 3.5f, 0.45f);
        GraftFX.Play("Steph_BounceVFx", FeetPos, new Color(1f, 1f, 0.8f), body != null ? body.sortingOrder + 2 : 90, null, false, false, 1f, new Vector2(0.5f, 0.05f));
        foreach (EnemyHealth e in BoonFX.EnemiesIn(Center, 3.5f))
        {
            BoonFX.Poison(e, 5f, PoisonDps);
            BoonFX.Push(e, new Vector2(Mathf.Sign(BoonFX.Center(e).x - Center.x) * 5f, 3f));
        }
    }

    private IEnumerator Leave()
    {
        if (!active) yield break;
        active = false;
        transforming = true;
        rolling = false;
        UpdateLoopSound();
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        float t = 0f, len = vanish.Length / GraftFX.Fps("BRJ_Bug_Vanish");
        FXSound.Play("SmokePoof", 0.5f, 0.9f);
        while (t < len)
        {
            t += Time.deltaTime;
            bug.sprite = vanish[Mathf.Clamp((int)(t / len * vanish.Length), 0, vanish.Length - 1)];
            Place();
            yield return null;
        }
        EndNow(true);
    }

    private void EndNow(bool withStyle)
    {
        StopAllCoroutines();
        bool was = active || transforming;
        active = transforming = false;
        timeLeft = 0f;
        rolling = false;
        if (whirlLoop != null) whirlLoop.Stop();
        if (bug != null) bug.enabled = false;
        if (fx != null) fx.enabled = false;
        if (glow != null) glow.enabled = false;
        if (was) TakeOver(false);
        if (withStyle) FXParticle.Burst(Center, Moss, 18, 1f, 3.5f, -1f, 0.8f);
    }

    // Rowdy off (controller, animator - so no clip events, sounds or hitboxes - and every sprite on him) / back on
    private void TakeOver(bool on)
    {
        if (on)
        {
            if (rowdyAnimator != null && rowdyAnimator.isActiveAndEnabled)
            {
                rowdyAnimator.Play("Idle", 0, 0f); // hitboxes the clips switch on go back off first
                rowdyAnimator.Update(0f);
                rowdyAnimator.enabled = false;
            }
            if (movement != null) movement.enabled = false;
            hidden.Clear();
            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
                if (sr.enabled) { hidden.Add(sr); sr.enabled = false; }
            savedGravity = rb != null ? rb.gravityScale : 0f;
        }
        else
        {
            if (rb != null && savedGravity > 0f) rb.gravityScale = savedGravity;
            foreach (SpriteRenderer sr in hidden) if (sr != null) sr.enabled = true;
            hidden.Clear();
            if (rowdyAnimator != null)
            {
                rowdyAnimator.enabled = true;
                rowdyAnimator.ResetTrigger("hit");
                rowdyAnimator.Play("Idle", 0, 0f);
            }
            if (movement != null && health != null && !health.IsDead) movement.enabled = true;
            if (body != null) body.enabled = true;
        }
    }

    // ---------------------------------------------------------------- drawing
    private void Ensure()
    {
        if (bug != null) return;
        Vector2 feet = Feet;
        idle = GraftFX.Frames("BRJ_Protobug_Idle", feet);
        appear = GraftFX.Frames("BRJ_Bug_Appear", feet);
        vanish = GraftFX.Frames("BRJ_Bug_Vanish", feet);
        rollBeginBody = GraftFX.Frames("BRJ_Bug_WhirlBegin_Body", feet);
        rollBeginFx = GraftFX.Frames("BRJ_Bug_WhirlBegin_Effect", feet);
        rollBody = GraftFX.Frames("BRJ_Bug_WhirlLoop_Body", feet);
        rollFx = GraftFX.Frames("BRJ_Bug_WhirlLoop_Effect", feet);
        rollEndBody = GraftFX.Frames("BRJ_Bug_WhirlEnd_Body", feet);
        rollEndFx = GraftFX.Frames("BRJ_Bug_WhirlEnd_Effect", feet);
        slashBody = GraftFX.Frames("BRJ_Bug_Charge_Body", feet);
        slashFx = GraftFX.Frames("BRJ_Bug_Charge_Effect", feet);
        spawn = GraftFX.Frames("BRJ_Bug_SpawnInsect", feet);
        Sprite fallback = body != null ? body.sprite : null;
        idle = idle ?? new[] { fallback };
        appear = appear ?? idle; vanish = vanish ?? idle; rollBody = rollBody ?? idle; rollBeginBody = rollBeginBody ?? rollBody;
        rollEndBody = rollEndBody ?? idle; slashBody = slashBody ?? idle; spawn = spawn ?? idle;

        bug = new GameObject("Rowdy Stephmoss").AddComponent<SpriteRenderer>();
        bug.enabled = false;
        if (body != null) { bug.sharedMaterial = body.sharedMaterial; bug.sortingLayerID = body.sortingLayerID; }
        fx = new GameObject("Stephmoss FX").AddComponent<SpriteRenderer>();
        fx.transform.SetParent(bug.transform, false);
        fx.enabled = false;
        if (CatFX.Unlit != null) fx.sharedMaterial = CatFX.Unlit;
        glow = new GameObject("Stephmoss Glow").AddComponent<Light2D>();
        glow.transform.SetParent(bug.transform, false);
        glow.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        glow.lightType = Light2D.LightType.Point;
        glow.color = new Color(0.85f, 1f, 0.6f);
        glow.intensity = 1f;
        glow.pointLightOuterRadius = 2.8f;
        glow.pointLightInnerRadius = 0.4f;
        glow.enabled = false;
        whirlLoop = bug.gameObject.AddComponent<AudioSource>();
        whirlLoop.clip = FXSound.Clip("StephmossWhirl");
        whirlLoop.loop = true;
        whirlLoop.playOnAwake = false;
        whirlLoop.spatialBlend = 0f;
    }

    private void Place()
    {
        bug.transform.position = new Vector3(Mathf.Round(FeetPos.x * 64f) / 64f, Mathf.Round((FeetPos.y - 0.02f) * 64f) / 64f, 0f);
        bug.flipX = Facing < 0f; // the art faces right
        if (body != null) bug.sortingOrder = body.sortingOrder;
        fx.flipX = bug.flipX;
        fx.sortingLayerID = bug.sortingLayerID;
        fx.sortingOrder = bug.sortingOrder + 1;
    }

    private static Sprite Pick(Sprite[] frames, float t, float fps)
    {
        if (frames == null || frames.Length == 0) return null;
        return frames[Mathf.Clamp((int)(t * fps), 0, frames.Length - 1)];
    }

    private static Sprite Loop(Sprite[] frames, float t, float fps) => frames == null || frames.Length == 0 ? null : frames[(int)(t * fps) % frames.Length];

    private void LateUpdate()
    {
        if (!active || bug == null) return;
        foreach (SpriteRenderer sr in hidden) if (sr != null) sr.enabled = false; // anything that switched itself back on
        bug.enabled = true;
        glow.enabled = true;
        Place();
        float now = Time.time, t = now - stateAt;
        Sprite b = null, f = null;
        switch (state)
        {
            case State.Slash:
            {
                float fps = 10f * 1.6f;
                b = Pick(slashBody, t, fps) ?? idle[0];
                int i = Mathf.Clamp(2 + (int)(t * fps), 0, slashBody.Length - 1);
                b = slashBody[i];
                f = slashFx != null && i < slashFx.Length ? slashFx[i] : null;
                break;
            }
            case State.Spawn:
                b = Pick(spawn, t, 10f * 1.3f);
                break;
            case State.Teleport:
                return; // the coroutine draws it
            default:
                if (rolling)
                {
                    float beginLen = rollBeginBody.Length / 20f; // WhirlBegin at double speed
                    if (t < beginLen) { b = Pick(rollBeginBody, t, 20f); f = Pick(rollBeginFx, t, 20f); }
                    else { b = Loop(rollBody, t - beginLen, 20f); f = Loop(rollFx, t - beginLen, 20f); }
                }
                else if (now - rollStoppedAt < rollEndBody.Length / 20f)
                {
                    b = Pick(rollEndBody, now - rollStoppedAt, 20f);
                    f = Pick(rollEndFx, now - rollStoppedAt, 20f);
                }
                else b = Loop(idle, now, 5f);
                break;
        }
        bug.sprite = b != null ? b : idle[0];
        fx.enabled = f != null;
        if (f != null) fx.sprite = f;
        // the last 2 seconds: flicker so the player knows it's ending
        bug.color = timeLeft < 2f && Mathf.Repeat(now * 6f, 1f) < 0.4f ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
    }
}

// A Stephmoss mosquito (BRJ_Insect): buzzes to the nearest enemy and EXPLODES on contact (damage + poison around it)
public class StephInsect : MonoBehaviour
{
    private Sprite[] frames;
    private SpriteRenderer sr;
    private EnemyHealth target;
    private float dps, blast, age, wobble;
    private Vector3 velocity;

    public static StephInsect Hatch(Vector3 at, float dps, float blast = 4f)
    {
        var go = new GameObject("Stephmoss Mosquito");
        go.transform.position = at;
        var i = go.AddComponent<StephInsect>();
        i.frames = GraftFX.Frames("BRJ_Insect");
        i.sr = go.AddComponent<SpriteRenderer>();
        i.sr.sortingOrder = 96;
        if (ItemArt.Lit != null) i.sr.sharedMaterial = ItemArt.Lit;
        i.dps = dps;
        i.blast = blast;
        i.wobble = Random.Range(0f, 6f);
        i.velocity = new Vector3(Random.Range(-2f, 2f), 3f, 0f);
        FXSound.Play("StephmossScissor", 0.15f, 2f);
        return i;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (frames != null) sr.sprite = frames[(int)(age * 12f) % frames.Length];
        if (age > 8f) { Pop(); return; }
        if (target == null || target.enemydead) target = BoonFX.Nearest(transform.position, 9f);
        Vector3 goal = target != null ? BoonFX.Center(target) : BoonRunner.RowdyCenter + new Vector3(Mathf.Sin(age * 2f + wobble) * 1.2f, 1.2f, 0f);
        Vector3 want = (goal - transform.position).normalized * (target != null ? 7f : 3f) + new Vector3(0f, Mathf.Sin(age * 14f + wobble) * 1.5f, 0f);
        velocity = Vector3.MoveTowards(velocity, want, 22f * Time.deltaTime);
        transform.position += velocity * Time.deltaTime;
        sr.flipX = velocity.x < 0f;
        if (target != null && (BoonFX.Center(target) - transform.position).sqrMagnitude < 0.12f) Explode();
    }

    private void Explode()
    {
        Vector3 at = transform.position;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(at, 1f))
        {
            BoonFX.Hit(e, blast, "Stephmoss Mosquito", null, true);
            BoonFX.Poison(e, 4f, dps);
        }
        GraftFX.Play("StephHit", at, Color.white, 97);
        PulseRing.Spawn(at, new Color(StephmossForm.Toxic.r, StephmossForm.Toxic.g, StephmossForm.Toxic.b, 1f), 1f, 0.25f);
        FXParticle.Burst(at, StephmossForm.Toxic, 12, 1.5f, 4f, 3f, 0.4f);
        FXSound.Play("StephmossHit", 0.4f, 1.3f);
        ScreenShake.Impulse(0.1f);
        Destroy(gameObject);
    }

    private void Pop()
    {
        GraftFX.Play("BRJ_Insect_Death", transform.position, Color.white, 96);
        Destroy(gameObject);
    }
}
