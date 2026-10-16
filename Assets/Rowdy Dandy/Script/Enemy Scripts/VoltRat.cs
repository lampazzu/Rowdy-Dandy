using UnityEngine;

// RED JELLY (was the Volt Rat; the script / prefab keep the old name): an angry red jellyfish that drifts around Rowdy
// at his own height, pulses to charge up, then jets straight through where he stands. Small, fragile, comes in packs.
// Art: the little jellyfish (Enemies/WaterViva/WaterViva.png, 7 frames of 24 x 24) recoloured red at runtime and drawn
// over whatever the animator does; the clips (Fly / Charge / Dash / Recover / GetHit / Death) only pick the frames.
// Its hitbox (child "Hitbox", Spike) is only on during the dash, so touching it otherwise doesn't hurt.
//   - moves in FixedUpdate with a smoothed velocity (the old MovePosition from Update stuttered against interpolation)
//   - HYPER ARMOR (EnemyHealth.HyperArmor): hits flash it white but don't knock it out of a charge / dash, unless it's
//     stunned or charmed
//   - dies with a pop: hit-stop, white flash, a red goo burst (Graft blood hit), droplets and a ring
[RequireComponent(typeof(EnemyHealth))]
public class VoltRat : MonoBehaviour
{
    private enum State { Hover, Charge, Dash, Recover, Dead }

    [SerializeField] private float hoverRadius = 2.2f;
    [Tooltip("Above Rowdy's middle. Keep it low: he reaches ~1.3 units up")]
    [SerializeField] private float hoverHeight = 0.35f;
    [SerializeField] private float hoverSpeed = 3.5f;
    [SerializeField] private float chargeTime = 0.6f;
    [SerializeField] private float dashSpeed = 11f;
    [SerializeField] private float recoverTime = 0.5f;
    [SerializeField] private Vector2 attackEvery = new Vector2(1.6f, 3f);
    [SerializeField] private AudioClip crackle;

    public static readonly Color Goo = new Color(1f, 0.32f, 0.38f);

    private EnemyHealth health;
    private Animator anim;
    private SpriteRenderer body;
    private Rigidbody2D rb;
    private GameObject hitbox;
    private State state = State.Hover;
    private float stateAt, nextAttackAt, angle, lastHealth, diedAt = -1f, afterimageTimer;
    private Vector3 dashTarget, dashDir;
    private Vector2 wanted, smoothVel, knock;
    private bool dashing;
    private string playing;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        anim = GetComponent<Animator>();
        body = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        Transform h = transform.Find("Hitbox");
        hitbox = h != null ? h.gameObject : null;
        if (hitbox != null) hitbox.SetActive(false);
        angle = Random.Range(0f, Mathf.PI * 2f);
        nextAttackAt = Time.time + Random.Range(attackEvery.x, attackEvery.y) + 0.8f;
        health.SetExpDropIfMissing(Resources.Load<GameObject>("Systems/EXPgem"), 2);
        // 2026-10-13 playtest: "too fast, too much damage, can't react". The prefab values are overridden here:
        // a longer, readable wind-up (aim locks at the end of it), a slower dash, more time between attacks, and hits
        // knock it out of the charge again (no hyper armor)
        health.HyperArmor = false;
        chargeTime = Mathf.Max(chargeTime, 1.05f);
        dashSpeed = Mathf.Min(dashSpeed, 7f);
        attackEvery = new Vector2(Mathf.Max(attackEvery.x, 2.6f), Mathf.Max(attackEvery.y, 4.5f));
        hoverHeight = Mathf.Min(hoverHeight, 0.4f); // old prefab value (1.3) floated out of reach
        wanted = transform.position;
        // a small red glow so it reads against the sunset (no dark rim: it drew a heavy black outline)
        if (body != null)
        {
            body.color = Color.white;
            var glow = new GameObject("Glow").AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            glow.transform.SetParent(transform, false);
            glow.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            glow.color = new Color(1f, 0.3f, 0.25f);
            glow.intensity = 0.7f;
            glow.pointLightOuterRadius = 1.2f;
            glow.pointLightInnerRadius = 0.05f;
        }
    }

    private void Start() => lastHealth = health.currentenemyHealth;
    private float lastHurtSound = -10f;

    private static Transform Rowdy => BoonRunner.Rowdy;

    private void Play(string clip)
    {
        if (playing == clip) return;
        playing = clip;
        if (anim != null && anim.isActiveAndEnabled) anim.Play(clip, 0, 0f);
    }

    [SerializeField] private Texture2D jellySheet;
    public Texture2D Sheet => jellySheet; // the blue WaterViva jelly (Jelly Buddies use it as it is)
    private static Sprite[] frames;
    private string drawn;
    private float drawnAt;

    private void LateUpdate()
    {
        if (body == null) return;
        if (frames == null)
        {
            if (jellySheet == null) return;
            frames = RedFrames(jellySheet, 7);
            if (frames == null) return;
        }
        string clip = playing ?? "Fly";
        if (clip != drawn) { drawn = clip; drawnAt = Time.time; }
        float t = Time.time - drawnAt;
        int n = frames.Length;
        Color c = Color.white;
        int i;
        switch (clip)
        {
            case "Charge": // pulsing, then a hard white blink once the aim is locked: "it's coming NOW"
                i = (int)(t * 24f) % n;
                c = aimLocked ? (Mathf.Repeat(t * 16f, 1f) < 0.5f ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 0.35f, 0.3f))
                              : (Mathf.Repeat(t * 6f, 1f) < 0.5f ? Color.white : new Color(1f, 0.7f, 0.7f));
                break;
            case "Dash": i = 3; break;                                   // tentacles trailing
            case "GetHit": i = (int)(t * 14f) % n; break;
            case "Death": i = Mathf.Min(2 + (int)(t * 20f), n - 1); break;
            default: i = (int)(t * 9f) % n; break;                       // floating
        }
        body.sprite = frames[i];
        body.color = c;
    }

    // Hue-shifts the jelly to red (works on non-readable textures: read back through a render texture)
    private static Sprite[] RedFrames(Texture2D sheet, int count)
    {
        int w = sheet.width, h = sheet.height;
        Color32[] px;
        try
        {
            if (sheet.isReadable) px = sheet.GetPixels32();
            else
            {
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                RenderTexture prev = RenderTexture.active;
                Graphics.Blit(sheet, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                copy.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                px = copy.GetPixels32();
                Destroy(copy);
            }
        }
        catch { return null; }
        for (int p = 0; p < px.Length; p++)
        {
            if (px[p].a == 0) continue;
            Color.RGBToHSV(px[p], out float hh, out float s, out float v);
            // blues / pinks -> reds, keeping the light and dark of the art; the darkest outline pixels lift to a
            // deep wine red (pure black read as a thick black rim against the sunset)
            float hue = Mathf.Lerp(0.98f, 1.04f, v) % 1f;
            Color col = Color.HSVToRGB(hue, Mathf.Clamp01(s * 0.6f + 0.45f), Mathf.Clamp(v * 1.05f, 0.32f, 1f));
            col.a = px[p].a / 255f;
            px[p] = col;
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "RedJelly" };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var result = new Sprite[count];
        int fw = w / count;
        for (int f = 0; f < count; f++) result[f] = Sprite.Create(tex, new Rect(f * fw, 0, fw, h), new Vector2(0.5f, 0.4f), 64f);
        return result;
    }

    // only a couple of jellies wind up / dash at the same time
    private const int MaxAttacking = 2;
    private const float AimLock = 0.35f;
    private static int Attacking;
    private bool counted, aimLocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => Attacking = 0;

    private void SetCounted(bool on)
    {
        if (on == counted) return;
        counted = on;
        Attacking = Mathf.Max(0, Attacking + (on ? 1 : -1));
    }

    private void OnDisable() => SetCounted(false);

    private void Go(State s)
    {
        SetCounted(s == State.Charge || s == State.Dash);
        if (s == State.Charge) aimLocked = false;
        state = s;
        stateAt = Time.time;
        dashing = s == State.Dash;
        if (hitbox != null) hitbox.SetActive(s == State.Dash);
        switch (s)
        {
            case State.Hover: Play("Fly"); break;
            case State.Charge:
                Play("Charge");
                if (crackle != null) FXSound.Play(crackle, 0.35f, Random.Range(1.4f, 1.7f));
                else BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.zap : null, 0.25f, Random.Range(1.6f, 1.9f));
                PulseRing.Spawn(transform.position, new Color(1f, 0.35f, 0.3f, 0.9f), 0.6f, 0.25f);
                break;
            case State.Dash: Play("Dash"); break;
            case State.Recover: Play("Recover"); break;
        }
    }

    private void Update()
    {
        if (state == State.Dead)
        {
            if (Time.time - diedAt > 0.35f) Destroy(gameObject);
            return;
        }
        if (health.enemydead) { Die(); return; }

        // got hit
        if (health.currentenemyHealth < lastHealth - 0.01f)
        {
            bool realHit = PlayerDamage.hitEnemy || lastHealth - health.currentenemyHealth >= 8f; // not a poison / burn tick
            lastHealth = health.currentenemyHealth;
            Vector2 away = Rowdy != null ? ((Vector2)(transform.position - BoonRunner.RowdyCenter)).normalized : Vector2.up;
            HitFlash.Flash(this, 0.1f); // every hit flashes it white
            // it had no hurt sound (no hurt events on the prefab): a wet jelly slap + a little zap, throttled for poison ticks
            if (Time.time - lastHurtSound > 0.08f)
            {
                lastHurtSound = Time.time;
                FXSound.Play("Jelly", 0.55f, Random.Range(1.05f, 1.3f));
                BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.squish : null, 0.45f, Random.Range(1.3f, 1.6f));
                BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.plop : null, 0.25f, Random.Range(1.7f, 2f));
                if (Random.value < 0.5f) BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.zap : null, 0.15f, Random.Range(1.8f, 2.1f));
                if (realHit) HitJuice(away);
            }
            if (health.ArmorHolds)
            {
                knock += away * 4f; // knocked back, but keeps doing what it was doing
            }
            else
            {
                if (hitbox != null) hitbox.SetActive(false);
                dashing = false;
                playing = null;
                Play("GetHit");
                SetCounted(false);
                state = State.Recover;
                stateAt = Time.time - recoverTime * 0.3f;
                knock += away * 7f;          // knocked back smoothly (it used to teleport 0.35 units)
                wanted = transform.position;
                return;
            }
        }

        if (StatusEffects.IsStunned(gameObject)) { wanted = transform.position; return; }
        Transform rowdy = Rowdy;
        if (rowdy == null) return;
        Vector3 target = BoonRunner.RowdyCenter;
        float t = Time.time - stateAt;

        switch (state)
        {
            case State.Hover:
                angle += Time.deltaTime * 1.4f;
                // circles Rowdy at about his own height, dipping low now and then - always in reach
                wanted = target + new Vector3(Mathf.Cos(angle) * hoverRadius, hoverHeight + Mathf.Sin(angle * 2f) * 0.25f, 0f);
                Face(target.x);
                if (playing != "Fly" && playing != "GetHit") Play("Fly");
                if (playing == "GetHit" && t > 0.3f) Play("Fly");
                if (Time.time >= nextAttackAt && EnemyFairness.OnScreen(transform.position, 0.04f))
                {
                    if (Attacking < MaxAttacking) Go(State.Charge);
                    else nextAttackAt = Time.time + Random.Range(0.4f, 0.9f); // a pack takes turns
                }
                break;

            case State.Charge:
                Face(target.x);
                wanted = transform.position; // holds still while it crackles
                if (Random.value < 0.35f) FXParticle.Burst(transform.position + (Vector3)Random.insideUnitCircle * 0.2f, new Color(1f, 0.45f, 0.35f), 1, 0.5f, 1.5f, 0f, 0.2f);
                // the aim locks AimLock seconds before it goes: dodge after the lock and it misses
                if (!aimLocked && t >= chargeTime - AimLock)
                {
                    aimLocked = true;
                    dashDir = (target - transform.position).normalized;
                    dashTarget = target + dashDir * 1.5f;
                    PulseRing.Spawn(transform.position, new Color(1f, 0.9f, 0.85f, 1f), 0.8f, AimLock);
                    BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.zap : null, 0.3f, 2.2f);
                }
                if (t >= chargeTime) Go(State.Dash);
                break;

            case State.Dash:
                afterimageTimer -= Time.deltaTime;
                if (afterimageTimer <= 0f) { afterimageTimer = 0.03f; CatFX.Afterimage(body, new Color(1f, 0.3f, 0.3f, 0.5f), 0.15f); }
                if ((transform.position - dashTarget).sqrMagnitude < 0.01f || t > 1f) Go(State.Recover);
                break;

            case State.Recover:
                wanted = transform.position;
                if (playing == "GetHit" && t > recoverTime * 0.3f + 0.25f) Play("Recover");
                if (t >= recoverTime)
                {
                    nextAttackAt = Time.time + Random.Range(attackEvery.x, attackEvery.y);
                    Go(State.Hover);
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (state == State.Dead || rb == null) return;
        float dt = Time.fixedDeltaTime;
        Vector2 pos = rb.position;
        Vector2 next;
        if (dashing)
        {
            next = Vector2.MoveTowards(pos, dashTarget, dashSpeed * dt);
            if (SolidGround.Blocked(next, new Vector2(0.2f, 0.15f))) { next = pos; Go(State.Recover); }
        }
        else next = Vector2.SmoothDamp(pos, wanted, ref smoothVel, 0.22f, hoverSpeed, dt);
        next += knock * dt;
        knock = Vector2.MoveTowards(knock, Vector2.zero, 14f * dt);
        if (knock.sqrMagnitude > 0.0001f && SolidGround.Blocked(next, new Vector2(0.2f, 0.15f))) { knock = Vector2.zero; next = pos; }
        if (rb.bodyType != RigidbodyType2D.Static) rb.MovePosition(next);
        else transform.position = next;
    }

    // the art faces left
    private void Face(float x)
    {
        if (body != null) body.flipX = x > transform.position.x;
    }

    private void Die()
    {
        SetCounted(false);
        state = State.Dead;
        diedAt = Time.time;
        if (hitbox != null) hitbox.SetActive(false);
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        playing = "Death";
        if (anim != null && anim.isActiveAndEnabled) anim.Play("Death", 0, 0f);
        Vector3 p = transform.position;
        // the pop: a beat of hit-stop and a white flash, then it bursts into goo
        TimeSlowController.HitStop(0.05f, 0.08f);
        HitFlash.Flash(this, 0.07f);
        FXSound.Play("Jelly", 0.5f, 0.75f);                                                 // squelch
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.squish : null, 0.45f, Random.Range(1.2f, 1.4f));
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.plop : null, 0.3f, 1.6f);
        SheetFX burst = GraftFX.Play("BRJ_Blood_Hit_Effect", p, Goo, 96);
        if (burst != null && Random.value < 0.5f) burst.transform.localScale = new Vector3(-1f, 1f, 1f);
        PulseRing.Spawn(p, new Color(1f, 0.5f, 0.5f, 0.9f), 0.9f, 0.25f);
        FXParticle.Burst(p, Goo, 16, 1.5f, 4.5f, 9f, 0.7f, true);                         // goo droplets that fall
        FXParticle.Burst(p, new Color(1f, 0.8f, 0.75f), 8, 1f, 3f, 2f, 0.35f);
        ScreenShake.Impulse(0.22f);
        Invoke(nameof(HideBody), 0.08f);
    }

    private void HideBody() { if (body != null) body.enabled = false; }

    // A hit that lands (not every poison tick: throttled with the sound): a beat of hit-stop, goo splattering out the
    // far side, a small goo burst, a ring and a little shake + rumble
    private void HitJuice(Vector2 away)
    {
        Vector3 p = transform.position;
        TimeSlowController.HitStop(0.035f, 0.08f);
        SheetFX goo = GraftFX.Play("BRJ_Blood_Hit_Effect", p + (Vector3)(away * 0.15f), new Color(Goo.r, Goo.g, Goo.b, 0.85f), 96, null, false, away.x < 0f, 1.6f);
        if (goo != null) goo.Stop(0.25f);
        FXParticle.Burst(p + (Vector3)(away * 0.2f), Goo, 10, 2f, 5f, 9f, 0.55f, true);      // droplets flung away from the hit
        FXParticle.Burst(p, new Color(1f, 0.85f, 0.8f), 5, 1f, 3f, 2f, 0.25f);
        PulseRing.Spawn(p, new Color(1f, 0.45f, 0.4f, 0.9f), 0.7f, 0.18f);
        ScreenShake.Impulse(0.12f);
        GamepadRumble.Pulse(0.25f, 0.5f, 0.08f);
    }
}
