using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// CALL OF THE MOON (Lycanthropy's special boon): press B / Circle (K on the keyboard) when the moon meter is full and Rowdy
// turns into a werewolf for a while - the TDF shadow werewolf art (ItemArt.werewolf, same sheet as the Moonbound Elder).
//   - the meter fills over time, faster with hits and kills, and twice as fast at night
//   - as a wolf: x1.6 damage, x1.35 speed, 40% less damage taken, claw swipes on every attack, lifesteal,
//     weapons don't wear out, kills make the night last longer (+0.6s)
//   - transforming: slow motion, flames, flicker between Rowdy and the wolf, then a HOWL that terrifies everyone near
// Rowdy's own animator keeps running underneath (hitboxes, states): only his sprite is swapped for the wolf frames.
public class Werewolf : MonoBehaviour
{
    public const float DamageMultiplier = 1.6f;
    public const float SpeedMultiplier = 1.35f;
    private const float FearRadius = 5f;
    private const int CellH = 108;
    private const float FeetRow = 6f;

    private static Werewolf instance;
    private static float charge;           // 0..100, kept for the play session
    private static float timeLeft, duration;
    private static bool active, transforming;

    public static bool Active => instance != null && active;
    public static bool Transforming => instance != null && transforming;
    public static float Charge01 => Mathf.Clamp01(charge / 100f);
    public static bool Ready => charge >= 100f;
    public static float TimeLeft01 => duration > 0f ? Mathf.Clamp01(timeLeft / duration) : 0f;

    private Health health;
    private PlayerMovement movement;
    private SpriteRenderer body;
    private Collider2D bodyCollider;
    private SpriteRenderer wolf;
    private SpriteOutline wolfOutline;
    private Light2D eyeLight;
    private Sprite[] frames;
    private float swingAt = -10f, emberTimer, afterTimer, deniedAt = -10f;
    private Image vignette;
    private float vignetteFlash;

    private static readonly string[] BackLines = { "WHO PUT FUR ON MY JACKET?", "...NEED A HAIRCUT, BABY.", "DID I EAT SOMEBODY?", "WHAT A NIGHT." };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { charge = 0f; active = transforming = false; timeLeft = duration = 0f; }

    public static void ResetCharge() { charge = 0f; }
    public static void Fill() { charge = 100f; }

    public static void AddCharge(float amount)
    {
        if (!Boons.Has("moon") || active || transforming) return;
        bool wasReady = Ready;
        charge = Mathf.Min(100f, charge + amount * Boons.MoonChargeMultiplier); // night x2 (Eclipse: always), Moon Feast
        if (!wasReady && Ready && instance != null) instance.OnFull();
    }

    private void Awake()
    {
        instance = this;
        health = GetComponent<Health>();
        movement = GetComponent<PlayerMovement>();
        body = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        active = transforming = false;
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; active = transforming = false; }
        if (wolf != null) Destroy(wolf.gameObject);
        if (vignette != null) Destroy(vignette.gameObject);
    }

    // ---------------------------------------------------------------- input / meter
    private void Update()
    {
        if (!Boons.Has("moon")) return;
        if (active && (health == null || health.IsDead)) { EndNow(false); return; }
        if (PauseMenu.IsPaused || BoonPicker.IsOpen) return;

        if (!active && !transforming) charge = Mathf.Min(100f, charge + Time.deltaTime * 0.9f * Boons.MoonChargeMultiplier);

        bool menuJustClosed = RowdyNotes.BlocksPause || WorldMap.BlocksPause || Tutorials.BlocksPause || BoonPicker.BlocksInput || CatParty.BlocksPause || CheckpointMenu.BlocksPause || StatsPause.BlocksPause;
        if (!menuJustClosed && GameInput.Down(GameInput.Act.Werewolf) && health != null && !health.IsDead)
        {
            if (Ready && !active && !transforming) StartCoroutine(Transform());
            else if (!active && !transforming && Time.unscaledTime - deniedAt > 0.5f)
            {
                deniedAt = Time.unscaledTime;
                BoonHUD.ShakeMoon();
                BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.cancel : null, 0.4f, 1f);
            }
        }

        if (active)
        {
            timeLeft -= Time.deltaTime;
            Embers();
            if (timeLeft <= 0f) EndNow(true);
        }
    }

    private void OnFull()
    {
        BoonFX.Popup(HeadTop + Vector3.up * 0.4f, GameInput.Format("FULL MOON! {WOLF}"), BoonFX.Blood, 0.85f, 1.6f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.wolfHowl : null, 0.25f, 1.4f);
        PulseRing.Spawn(Center, new Color(1f, 0.3f, 0.4f, 0.8f), 1.2f, 0.4f);
        BoonHUD.FlashMoon();
    }

    private Vector3 Center => bodyCollider != null ? bodyCollider.bounds.center : transform.position + Vector3.up * 0.4f;
    private Vector3 HeadTop => bodyCollider != null ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.max.y, 0f) : transform.position + Vector3.up * 0.9f;
    private Vector3 Feet => bodyCollider != null ? new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y, 0f) : transform.position;
    private float Facing => transform.localScale.x >= 0f ? 1f : -1f;

    // ---------------------------------------------------------------- the change
    private IEnumerator Transform()
    {
        transforming = true;
        charge = 0f;
        duration = timeLeft = Boons.V("moon", 0);
        RunStats.Transformations++;
        EnsureWolf();
        BoonArt art = BoonArt.Get;

        TimeSlowController.SlowMotion(1f, 0.3f);
        if (art != null) { BoonArt.Play(art.wolfTransform, 0.8f, 1f); BoonArt.Play(art.wolfSpawn, 0.6f, 0.9f); }
        if (art != null) BoonFX.Sheet(art.charge, 10, Feet + Vector3.up * 0.9f, 16f, 0.95f, new Color(1f, 0.45f, 0.55f), transform);
        GamepadRumble.Pulse(0.3f, 0.5f, 0.4f);
        vignetteFlash = 0.6f;

        // flicker between Rowdy and the wolf rising out of him
        float t = 0f;
        const float change = 0.5f;
        while (t < change)
        {
            t += Time.unscaledDeltaTime;
            bool showWolf = Mathf.Repeat(t, 0.1f) < 0.05f + 0.05f * (t / change);
            wolf.enabled = showWolf;
            wolf.sprite = frames[Mathf.Clamp((int)(t / change * 4f), 0, 3)];
            PlaceWolf();
            if (body != null) body.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.45f), t / change);
            if (Random.value < 0.5f) FXParticle.Burst(Center + (Vector3)Random.insideUnitCircle * 0.3f, Random.value < 0.6f ? BoonFX.Blood : new Color(0.25f, 0.1f, 0.2f), 2, 0.5f, 2f, -2f, 0.6f);
            yield return null;
        }
        if (body != null) body.color = Color.white;

        // HOWL
        transforming = false;
        active = true;
        if (art != null) BoonArt.Play(art.wolfHowl, 0.9f, 1f);
        SoundManager.PlaySfx(Resources.Load<AudioClip>("Sounds/Howl"), 0.7f);
        TimeSlowController.HitStop(0.14f, 0.05f);
        ScreenShake.Impulse(1.1f);
        GamepadRumble.Pulse(0.9f, 1f, 0.45f);
        vignetteFlash = 1f;
        BoonFX.Popup(HeadTop + Vector3.up * 0.5f, "AWOOOOOO!", BoonFX.Blood, 1.4f, 1.6f);
        PulseRing.Spawn(Center, new Color(1f, 0.25f, 0.35f, 1f), FearRadius, 0.5f);
        PulseRing.Spawn(Center, new Color(0.9f, 0.9f, 1f, 0.8f), FearRadius * 0.6f, 0.35f);
        GroundShock.Spawn(Feet, FearRadius, new Color(1f, 0.35f, 0.4f), new Color(0.3f, 0.1f, 0.15f), 0.5f);
        FXParticle.Burst(Center, BoonFX.Blood, 30, 2f, 6f, 3f, 0.8f);
        FXParticle.Burst(Center, new Color(0.85f, 0.85f, 1f), 14, 2f, 5f, 2f, 0.7f);
        int scared = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesIn(Center, FearRadius))
        {
            BoonFX.Fear(e, 1.6f);
            float side = Mathf.Sign(BoonFX.Center(e).x - Center.x);
            BoonFX.Push(e, new Vector2(side * 6f, 3f));
            scared++;
        }
        if (scared > 0 && art != null) BoonArt.Play(art.fearSfx, 0.6f, 1f);
        Tutorials.Show(Tutorials.Topic.Werewolf, frames != null ? frames[5] : null, 1.6f);
    }

    public static void OnKill()
    {
        if (!Active) return;
        timeLeft = Mathf.Min(duration, timeLeft + 0.6f);
    }

    private void EndNow(bool withStyle)
    {
        active = transforming = false;
        timeLeft = 0f;
        if (wolf != null) wolf.enabled = false;
        if (eyeLight != null) eyeLight.enabled = false;
        if (body != null) body.enabled = true;
        if (!withStyle) return;

        BoonArt art = BoonArt.Get;
        if (art != null) BoonFX.Sheet(art.ail, 10, Center, 18f, 0.8f, new Color(0.75f, 0.65f, 0.85f, 0.9f));
        FXParticle.Burst(Center, new Color(0.5f, 0.45f, 0.6f), 22, 1f, 3.5f, -1f, 0.9f);
        if (body != null) CatFX.Afterimage(body, new Color(1f, 1f, 1f, 0.9f), 0.25f);
        BoonFX.Popup(HeadTop + Vector3.up * 0.3f, BackLines[Random.Range(0, BackLines.Length)], new Color(0.9f, 0.85f, 1f), 0.7f, 1.6f);
        BoonArt.Play(art != null ? art.cancel : null, 0.4f, 0.8f);
    }

    // ---------------------------------------------------------------- claws
    // BoonRunner calls this when Rowdy starts a swing while transformed
    public static void OnSwing()
    {
        if (!Active) return;
        instance.Claw();
    }

    private void Claw()
    {
        swingAt = Time.time;
        float f = Facing;
        Vector3 at = Center + new Vector3(f * 0.9f, 0.05f, 0f);
        BoonArt art = BoonArt.Get;
        if (art != null)
        {
            SheetFX fx = BoonFX.Sheet(art.clawSlash, 6, at, 26f, 1f, new Color(1f, 0.85f, 0.88f));
            if (fx != null) fx.transform.localScale = new Vector3(-f, 1f, 1f); // the slash art at its own pixel size
            BoonArt.Play(art.claw, 0.45f, Random.Range(0.95f, 1.15f));
        }
        float damage = (12f + 4f * PlayerStats.Level) * Boons.OutgoingMultiplier * (Boons.Has("silverclaws") ? 2f : 1f);
        int hits = 0;
        foreach (EnemyHealth e in BoonFX.EnemiesInBox(at, new Vector2(1.9f, 1.3f)))
        {
            BoonFX.Hit(e, damage, "Werewolf Claws");
            FXParticle.Burst(BoonFX.Center(e), BoonFX.Blood, 6, 1.5f, 4f, 6f, 0.4f);
            hits++;
        }
        if (hits > 0)
        {
            BoonRunner.BankHeal(2f * hits, true); // lifesteal
            TimeSlowController.HitStop(0.04f, 0.08f);
        }
    }

    // ---------------------------------------------------------------- drawing
    private void EnsureWolf()
    {
        if (wolf != null) return;
        ItemArt items = ItemArt.Get;
        if (items != null && items.werewolf != null) frames = ItemArt.Frames(items.werewolf, 15, 1, new Vector2(0.5f, FeetRow / CellH), 64f);
        var go = new GameObject("Rowdy Werewolf");
        wolf = go.AddComponent<SpriteRenderer>();
        wolf.enabled = false;
        if (body != null) { wolf.sharedMaterial = body.sharedMaterial; wolf.sortingLayerID = body.sortingLayerID; }
        if (frames == null) frames = new[] { body != null ? body.sprite : null };
        wolfOutline = SpriteOutline.Add(wolf, new Color(1f, 0.3f, 0.4f, 0.8f), 1, -1);
        try
        {
            var lightGo = new GameObject("Wolf Eyes");
            lightGo.transform.SetParent(go.transform, false);
            eyeLight = lightGo.AddComponent<Light2D>();
            eyeLight.lightType = Light2D.LightType.Point;
            // a red glow around him: lights the (lit) wolf art up so it reads even in dark spots
            eyeLight.color = new Color(1f, 0.7f, 0.75f);
            eyeLight.intensity = 1.1f;
            eyeLight.pointLightOuterRadius = 2.6f;
            eyeLight.pointLightInnerRadius = 0.4f;
            eyeLight.enabled = false;
        }
        catch (System.Exception) { eyeLight = null; }
    }

    private void PlaceWolf()
    {
        if (wolf == null) return;
        wolf.transform.position = Feet + new Vector3(0f, -0.02f, 0f);
        wolf.transform.rotation = transform.rotation;
        wolf.flipX = Facing > 0f; // the art faces left
        if (body != null) wolf.sortingOrder = body.sortingOrder;
    }

    private void LateUpdate()
    {
        UpdateVignette();
        if (!active || wolf == null) return;
        if (body != null) body.enabled = false; // after the animator has drawn this frame
        wolf.enabled = true;
        PlaceWolf();
        if (eyeLight != null) { eyeLight.enabled = true; eyeLight.transform.localPosition = new Vector3(Facing > 0f ? 0.15f : -0.15f, 0.5f, 0f); }

        float now = Time.time;
        float sinceSwing = now - swingAt;
        Rigidbody2D rb = movement != null ? movement.GetComponent<Rigidbody2D>() : null;
        float speed = rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
        bool grounded = movement == null || movement.IsGrounded || movement.IsWatered;
        int frame;
        if (sinceSwing < 0.3f) frame = sinceSwing < 0.1f ? 12 : sinceSwing < 0.2f ? 13 : 14;
        else if (health != null && now - health.lastDamageTime < 0.25f) frame = 14;
        else if (!grounded) frame = 13;
        else if (speed > 0.3f) frame = (int)(now * 16f) % 4;
        else frame = 4 + (int)(now * 11f) % 8;
        wolf.sprite = frames[Mathf.Clamp(frame, 0, frames.Length - 1)];

        float pulse = 0.5f + 0.5f * Mathf.Sin(now * 7f);
        float ending = timeLeft < 2f ? (Mathf.Repeat(now * 6f, 1f) < 0.5f ? 1f : 0.3f) : 1f;
        wolfOutline.color = new Color(1f, 0.3f + 0.25f * pulse, 0.45f + 0.15f * pulse, (0.75f + 0.25f * pulse) * ending);
    }

    private void Embers()
    {
        emberTimer -= Time.deltaTime;
        if (emberTimer <= 0f)
        {
            emberTimer = 0.06f;
            Vector3 p = Center + new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(-0.4f, 0.3f), 0f);
            FXParticle.Burst(p, Random.value < 0.7f ? BoonFX.Blood : new Color(0.3f, 0.1f, 0.2f), 1, 0.2f, 0.6f, -2.5f, 0.6f);
        }
        afterTimer -= Time.deltaTime;
        Rigidbody2D rb = movement != null ? movement.GetComponent<Rigidbody2D>() : null;
        if (afterTimer <= 0f && rb != null && Mathf.Abs(rb.linearVelocity.x) > 3f && wolf != null)
        {
            afterTimer = 0.07f;
            CatFX.Afterimage(wolf, new Color(0.9f, 0.15f, 0.25f, 0.4f), 0.22f);
        }
    }

    // Red-violet glow around the screen edges while transformed (flashes on the change)
    private void UpdateVignette()
    {
        bool show = active || transforming || vignetteFlash > 0f;
        if (!show && vignette == null) return;
        if (vignette == null)
        {
            vignette = OverlayUI.MakeImage("Werewolf Vignette", OverlayUI.Root, Color.clear, VignetteSprite());
            RectTransform r = vignette.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            r.SetAsFirstSibling();
        }
        vignetteFlash = Mathf.Max(0f, vignetteFlash - Time.unscaledDeltaTime * 1.5f);
        float baseA = active ? 0.32f + 0.08f * Mathf.Sin(Time.unscaledTime * 3f) : transforming ? 0.25f : 0f;
        if (active && timeLeft < 2f) baseA *= Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f ? 1f : 0.5f;
        float a = Mathf.Clamp01(baseA + vignetteFlash * 0.6f);
        vignette.color = new Color(0.75f, 0.08f, 0.22f, a);
        vignette.enabled = a > 0.005f && !PauseMenu.IsPaused;
    }

    private static Sprite vignetteSprite;
    private static Sprite VignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int w = 64, h = 36;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "WolfVignette" };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                float a = Mathf.Clamp01((d - 0.55f) / 0.6f);
                px[y * w + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        vignetteSprite = AIArt.Use("Werewolf_Vignette", Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f)));
        return vignetteSprite;
    }
}
