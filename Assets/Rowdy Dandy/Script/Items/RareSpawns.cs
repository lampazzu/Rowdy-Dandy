using System.Collections;
using UnityEngine;

// FLYING RAT (PIV_Flying_Rat art) - very rare, LEGENDARY drop from enemies. It crackles into existence with a golden
// aura (outline, light beam, sparkles, chime), flutters around for a while and then escapes. Walk up to it and press
// Y / E to GRAB it: time slows right down for the moment. Every registered rat is BAIT: dropped at a checkpoint
// (CheckpointMenu) it lures one lost cat back to Rowdy (RatBait, CatRoster.SpendRat).
public class FlyingRat : Pickup
{
    private const float PixelsPerUnit = 64f;
    private const float Lifetime = 32f;
    private const float GrabRadius = 1.4f;
    // A legendary rat is in Rowdy's reach (this frame or the last): the interact button picks the rat up, not a cat
    private static int GrabbableFrame = -10;
    public static bool InReach => Time.frameCount - GrabbableFrame <= 1;
    private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
    private static Sprite beamSprite;

    private Sprite[] frames;
    private Vector3 home;
    private float t, phase;
    private SpriteOutline aura;
    private SpriteRenderer beam, prompt;
    private UnityEngine.Rendering.Universal.Light2D glow;
    private float sparkleTimer, ringTimer, promptAlpha;
    private bool grabbed;

    public static void Spawn(Vector3 at)
    {
        ItemArt art = ItemArt.Get;
        if (art == null || art.flyingRat == null) return;
        var go = new GameObject("Flying Rat");
        go.transform.position = at + Vector3.up * 0.5f;
        var rat = go.AddComponent<FlyingRat>();
        rat.frames = ItemArt.Frames(art.flyingRat, 10, 2, new Vector2(0.5f, 0.3f), PixelsPerUnit);
        rat.sprite = MakeRenderer(go, rat.frames[3], 64);
        rat.flies = true;
        rat.magnetRadius = 0f; // no magnet: it has to be grabbed
        rat.home = go.transform.position;
        rat.phase = Random.Range(0f, 10f);
        rat.BuildAura();

        IconPopup.Show(go.transform.position + Vector3.up * 0.6f, rat.frames[0], "LEGENDARY RAT!", Gold, 1.3f, 2.2f);
        FXSound.Play("Legendary", 1f, 1f);
        PulseRing.Spawn(go.transform.position, Gold, 2.2f, 0.6f);
        PulseRing.Spawn(go.transform.position, Color.white, 1.2f, 0.35f);
        FXParticle.Burst(go.transform.position, Gold, 24, 2f, 5f, 2f, 1f);
        TimeSlowController.HitStop(0.1f, 0.1f);
        ScreenShake.Impulse(0.3f);
    }

    private void BuildAura()
    {
        aura = SpriteOutline.Add(sprite, Gold, 1, -1);

        var b = new GameObject("Legendary Beam");
        b.transform.SetParent(transform, false);
        beam = b.AddComponent<SpriteRenderer>();
        beam.sprite = BeamSprite;
        beam.sortingLayerName = "Default";
        beam.sortingOrder = 62;
        if (CatFX.Unlit != null) beam.sharedMaterial = CatFX.Unlit;

        var p = new GameObject("Grab Prompt");
        p.transform.SetParent(transform, false);
        prompt = p.AddComponent<SpriteRenderer>();
        prompt.sortingLayerName = "Default";
        prompt.sortingOrder = 70;
        if (CatFX.Unlit != null) prompt.sharedMaterial = CatFX.Unlit;

        glow = gameObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        glow.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        glow.color = Gold;
        glow.pointLightOuterRadius = 2.2f;
        glow.pointLightInnerRadius = 0.3f;
        glow.intensity = 1.2f;
    }

    protected override bool CanBeCollected() => false; // only by grabbing (Y / E), see Update

    protected override void Update()
    {
        if (grabbed) return;
        t += Time.deltaTime;

        // 0-0.6s: zaps in (frames 3-5), then flaps (0,1,2,1); blinks out at the end of its life
        int f = t < 0.6f ? 3 + Mathf.Min(2, (int)(t / 0.2f)) : new[] { 0, 1, 2, 1 }[(int)(t * 10f) % 4];
        bool blinking = t > Lifetime - 4f;
        bool visible = !blinking || Mathf.Repeat(t * 8f, 1f) < 0.6f;
        if (sprite != null)
        {
            sprite.sprite = frames[f];
            sprite.enabled = visible;
        }
        if (t > Lifetime) { Destroy(gameObject); return; }

        // Lazy figure-8 around where it appeared, slowly drifting up and away
        Vector3 wander = new Vector3(Mathf.Sin(t * 1.3f + phase) * 1.4f, Mathf.Sin(t * 2.6f + phase) * 0.35f + t * 0.02f, 0f);
        Vector3 before = transform.position;
        transform.position = Vector3.Lerp(transform.position, home + wander, Time.deltaTime * 3f);
        if (sprite != null && Mathf.Abs(transform.position.x - before.x) > 0.001f) sprite.flipX = transform.position.x > before.x;

        base.Update(); // finds Rowdy
        AnimateAura(visible);

        // Grab it
        bool close = rowdy != null && Vector2.Distance(transform.position, rowdy.position + Vector3.up * 0.4f) < GrabRadius;
        if (close && !grabbed) GrabbableFrame = Time.frameCount; // cats in reach wait: the rat wins the button
        promptAlpha = Mathf.MoveTowards(promptAlpha, close && !PauseMenu.IsPaused ? 1f : 0f, Time.deltaTime * 6f);
        if (close && t > 0.6f && !PauseMenu.IsPaused && GameInput.Down(GameInput.Act.Interact)) Grab();
    }

    private void AnimateAura(bool visible)
    {
        float pulse = 0.6f + 0.4f * Mathf.Sin(t * 6f);
        if (aura != null) aura.color = visible ? new Color(Gold.r, Gold.g, Gold.b, 0.6f + 0.4f * pulse) : Color.clear;
        if (glow != null) glow.intensity = visible ? 0.9f + 0.6f * pulse : 0f;
        if (beam != null)
        {
            beam.enabled = visible;
            beam.color = new Color(Gold.r, Gold.g, Gold.b, 0.35f + 0.2f * pulse);
            beam.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        }
        if (prompt != null)
        {
            prompt.sprite = WeaponDrop.GetInteractPrompt();
            prompt.color = new Color(1f, 1f, 1f, promptAlpha);
            prompt.enabled = promptAlpha > 0f;
            prompt.transform.localPosition = new Vector3(0f, 0.55f + Mathf.Round(Mathf.Sin(t * 4f) * 2f) / 64f, 0f);
        }

        sparkleTimer -= Time.deltaTime;
        if (sparkleTimer <= 0f && visible)
        {
            sparkleTimer = 0.07f;
            Vector3 p = transform.position + (Vector3)(Random.insideUnitCircle * 0.35f);
            FXParticle.Burst(p, Random.value < 0.3f ? Color.white : Gold, 1, 0.1f, 0.5f, -1.2f, 0.7f);
        }
        ringTimer -= Time.deltaTime;
        if (ringTimer <= 0f && visible)
        {
            ringTimer = 1.1f;
            PulseRing.Spawn(transform.position, new Color(Gold.r, Gold.g, Gold.b, 0.6f), 0.9f, 0.5f, 61);
        }
    }

    private void Grab()
    {
        grabbed = true;
        Interact.Use();
        Tutorials.Show(Tutorials.Topic.Rat, frames != null && frames.Length > 0 ? frames[0] : null, 1.6f);
        CatRoster.AddRat();
        RunStats.RatsGrabbed++;
        int rats = CatRoster.Rats;

        TimeSlowController.HitStop(0.15f, 0.02f);
        TimeSlowController.SlowMotion(1.3f, 0.25f);
        ScreenShake.Impulse(0.5f);
        GamepadRumble.Pulse(0.5f, 0.9f, 0.25f);
        FXSound.Play("RatGrab", 1f, 1f);
        GemChime.Play(EXPGem.SharedCollectSound, 0.9f);
        PulseRing.Spawn(transform.position, Gold, 2.6f, 0.7f);
        PulseRing.Spawn(transform.position, Color.white, 1.4f, 0.4f);
        FXParticle.Burst(transform.position, Gold, 30, 2f, 6f, 3f, 1.1f);
        IconPopup.Show(transform.position + Vector3.up * 0.5f, frames[0], "RAT REGISTERED (" + rats + ")", Gold, 1.3f, 2.4f);
        RowdyNotes.MarkTopicNews("flyingrat");

        // The burst (bottom row of the sheet)
        var go = new GameObject("Rat Burst");
        go.transform.position = transform.position;
        MakeRenderer(go, frames[10], 65);
        go.AddComponent<FramePlayer>().Play(frames, 10, 10, 18f);
        Destroy(gameObject);
    }

    protected override void OnCollected() { } // grabbed instead (Grab)

    // Soft vertical light column (7 px wide, fades to the top)
    private static Sprite BeamSprite
    {
        get
        {
            if (beamSprite != null) return beamSprite;
            const int w = 9, h = 96;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "RatBeam" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Sin((float)y / (h - 1) * Mathf.PI); // fades at both ends
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Abs(x - (w - 1) * 0.5f) / (w * 0.5f);
                    float a = Mathf.Round((d < 0.3f ? 1f : d < 0.65f ? 0.5f : 0.18f) * v * 5f) / 5f;
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            beamSprite = AIArt.Use("LegendaryRat_Beam", Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit));
            return beamSprite;
        }
    }
}

// CAT TREAT - a tiny fish, very rare drop. Flops on the ground; picking it up feeds every cat Rowdy has:
// cooldowns refill at once and stay halved for a while. No cats? Then it's a snack (+5 HP).
public class CatTreat : Pickup
{
    private Sprite[] frames;
    private float t;

    public static void Spawn(Vector3 at)
    {
        ItemArt art = ItemArt.Get;
        if (art == null || art.fish == null) return;
        var go = new GameObject("Cat Treat Fish");
        go.transform.position = at + Vector3.up * 0.3f;
        var fish = go.AddComponent<CatTreat>();
        fish.frames = ItemArt.Frames(art.fish, Mathf.Max(1, art.fishFrames), 1, new Vector2(0.5f, 0.5f), 64f);
        fish.sprite = MakeRenderer(go, fish.frames[0], 62);
        fish.magnetRadius = 1.8f;
        fish.Launch(new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(4f, 5.5f)));
    }

    protected override void OnBounce() => t = 0f;

    protected override void Update()
    {
        t += Time.deltaTime;
        if (sprite != null && frames.Length > 0)
        {
            sprite.sprite = frames[(int)(t * 6f) % frames.Length]; // flop flop
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 14f) * 12f);
        }
        base.Update();
    }

    protected override void OnCollected()
    {
        int fed = 0;
        foreach (PetFollower cat in PetFollower.Pets)
            if (cat != null && cat.IsCollected) { cat.Treat(); fed++; }

        Sprite icon = frames.Length > 0 ? frames[0] : null;
        if (fed > 0) IconPopup.Show(transform.position + Vector3.up * 0.3f, icon, fed > 1 ? "CATS FED!" : "CAT FED!", new Color(1f, 0.75f, 0.4f), 1f, 1.6f);
        else
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null && p.TryGetComponent(out Health h)) h.AddHealth(5f);
            IconPopup.Show(transform.position + Vector3.up * 0.3f, icon, "SNACK +5 HP", new Color(1f, 0.75f, 0.4f), 1f, 1.4f);
        }
        GemChime.Play(EXPGem.SharedCollectSound, 0.8f);
        RowdyNotes.MarkTopicNews("cattreat");
    }
}

// Plays frames[from .. from+count-1] once and destroys itself
public class FramePlayer : MonoBehaviour
{
    public void Play(Sprite[] frames, int from, int count, float fps) => StartCoroutine(Run(frames, from, count, fps));

    private IEnumerator Run(Sprite[] frames, int from, int count, float fps)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        for (int i = 0; i < count && from + i < frames.Length; i++)
        {
            if (sr != null) sr.sprite = frames[from + i];
            yield return new WaitForSeconds(1f / fps);
        }
        Destroy(gameObject);
    }
}
