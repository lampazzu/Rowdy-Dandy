using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

// Builds and plays pixel-art water splashes (entry / jump-out / drowning gulp) and drowning bubble trails.
// Works with no setup: it creates itself the first time something splashes.
// To tweak the look, add this component to any object in the scene and edit it in the Inspector
// (colors and sizes can be changed live in Play mode).
public class WaterSplashFX : MonoBehaviour
{
    public enum SplashKind { Entry, Exit, Gulp }

    [Header("Detection")]
    [Tooltip("Layers counted as water. Leave as Nothing to use 'waterLayer' + 'Water'.")]
    [SerializeField] private LayerMask waterLayers;
    [Tooltip("Vertical speed needed for a splash at all.")]
    [SerializeField] private float minImpactSpeed = 2.5f;
    [Tooltip("Vertical speed that gives the biggest splash.")]
    [SerializeField] private float maxImpactSpeed = 16f;

    [Header("Look")]
    [SerializeField] private float sizeMultiplier = 1f;
    [SerializeField] private Color dropletColorA = new Color32(0xFF, 0x39, 0xC0, 0xFF);
    [SerializeField] private Color dropletColorB = new Color32(0xFF, 0x72, 0xFF, 0xFF);
    [SerializeField] private Color foamColor = new Color32(0xFF, 0xCC, 0xF5, 0xF0);
    [SerializeField] private Color ringColor = new Color32(0xFF, 0x14, 0xFE, 0xD0);
    [SerializeField] private Color mistColor = new Color32(0xB6, 0x0A, 0x7F, 0x60);
    [SerializeField] private Color bubbleColor = new Color32(0xFF, 0x72, 0xFF, 0xD0);

    [Header("Rendering")]
    [Tooltip("Lit = affected by your 2D lights (darker in dark areas). Unlit = always bright.")]
    [SerializeField] private bool useLitShader = false;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 60;

    [Header("Juice")]
    [Tooltip("Optional: impulse source used to shake the camera on big splashes.")]
    [SerializeField] private CinemachineImpulseSource bigSplashImpulse;
    [Range(0f, 1f)] [SerializeField] private float bigSplashShakeThreshold = 0.8f;

    [Header("Pooling")]
    [SerializeField] private int maxSplashes = 12;
    [SerializeField] private int maxBubbleStreams = 8;

    private static WaterSplashFX instance;
    private static bool isQuitting;

    private Material dropletMat, softMat, ringMat, bubbleMat;
    private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
    private bool isReady;

    private class SplashInstance
    {
        public GameObject root;
        public ParticleSystem crown, column, foam, mist, ring;
        public float busyUntil;
    }

    private class BubbleStream
    {
        public GameObject root;
        public ParticleSystem bubbles;
        public Component owner;
        public Collider2D followCollider;
        public float stopAt;
        public float freeAt;
        public bool emitting;
    }

    private readonly List<SplashInstance> splashes = new List<SplashInstance>();
    private readonly List<BubbleStream> bubbleStreams = new List<BubbleStream>();

    // =========================================================================
    // ACCESS
    // =========================================================================

    public static WaterSplashFX Instance
    {
        get
        {
            if (isQuitting) return null;

            if (instance == null)
            {
                instance = FindFirstObjectByType<WaterSplashFX>();
                if (instance == null)
                {
                    instance = new GameObject("WaterSplashFX (auto)").AddComponent<WaterSplashFX>();
                }
            }
            return instance;
        }
    }

    public static bool HasInstance => instance != null;
    // The current FX object if there is one; never creates one (safe during shutdown / scene unload)
    public static WaterSplashFX Existing => instance;

    public int WaterMask => waterLayers.value != 0 ? waterLayers.value : LayerMask.GetMask("waterLayer", "Water");
    public float MinImpactSpeed => minImpactSpeed;

    public float GetIntensity(float speed)
    {
        return Mathf.Clamp01(Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed));
    }

    public bool IsWater(Collider2D col)
    {
        if (col == null) return false;
        return (WaterMask & (1 << col.gameObject.layer)) != 0 || col.CompareTag("Water");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        isQuitting = false;
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
    }

    private static void OnQuitting() => isQuitting = true;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;

        isReady = BuildMaterials();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;

        foreach (Texture2D tex in generatedTextures)
        {
            if (tex != null) Destroy(tex);
        }
        if (dropletMat != null) Destroy(dropletMat);
        if (softMat != null) Destroy(softMat);
        if (ringMat != null) Destroy(ringMat);
        if (bubbleMat != null) Destroy(bubbleMat);
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    // surfacePoint: where the body meets the water top. intensity: 0..1. widthScale: ~1 for Rowdy-sized bodies.
    public void PlaySplash(Vector2 surfacePoint, float intensity, float widthScale, SplashKind kind, Collider2D water = null)
    {
        if (!isReady) return;

        intensity = Mathf.Clamp01(intensity);
        widthScale = Mathf.Max(0.2f, widthScale);

        SplashInstance splash = GetFreeSplash();
        splash.root.transform.position = new Vector3(surfacePoint.x, surfacePoint.y, 0f);
        splash.busyUntil = Time.time + 1.6f;

        ConfigureAndPlay(splash, intensity, widthScale, kind);

        // Make the dynamic water mesh react too. The water tiles and the mesh are separate objects,
        // so find the mesh drawn at this point if the collider isn't part of one.
        DynamicWater2D dynamicWater = water != null ? water.GetComponentInParent<DynamicWater2D>() : null;
        if (dynamicWater == null) dynamicWater = DynamicWater2D.FindAt(surfacePoint);
        if (dynamicWater != null)
        {
            float push = Mathf.Lerp(0.04f, 0.16f, intensity);
            if (kind == SplashKind.Exit) push *= 0.5f;
            dynamicWater.Splash(surfacePoint.x, -push, 0.25f + 0.2f * widthScale);
        }

        if (kind == SplashKind.Entry && bigSplashImpulse != null && intensity >= bigSplashShakeThreshold)
        {
            if (CameraShakeManager.instance != null)
            {
                CameraShakeManager.instance.CameraShake(bigSplashImpulse);
            }
            else
            {
                bigSplashImpulse.GenerateImpulse();
            }
        }
    }

    // Bubbles rising from a sinking body. They follow 'followCollider' (or the owner's transform) until 'duration' ends.
    public void StartBubbles(Component owner, Collider2D followCollider, float duration, float widthScale)
    {
        if (!isReady || owner == null) return;

        // Already bubbling for this owner -> just extend it
        foreach (BubbleStream existing in bubbleStreams)
        {
            if (existing.owner == owner && existing.emitting)
            {
                existing.stopAt = Time.time + duration;
                return;
            }
        }

        BubbleStream stream = GetFreeBubbleStream();
        if (stream == null) return;

        stream.owner = owner;
        stream.followCollider = followCollider;
        stream.stopAt = Time.time + duration;
        stream.emitting = true;
        stream.root.transform.position = GetFollowPosition(stream);

        float w = Mathf.Clamp(widthScale, 0.5f, 2.5f);
        var emission = stream.bubbles.emission;
        emission.rateOverTime = 14f * w;
        var shape = stream.bubbles.shape;
        shape.radius = 0.22f * w;
        var main = stream.bubbles.main;
        main.startColor = bubbleColor;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f * sizeMultiplier, 0.11f * sizeMultiplier);

        stream.bubbles.Clear();
        stream.bubbles.Play();
    }

    public void StopBubbles(Component owner)
    {
        foreach (BubbleStream stream in bubbleStreams)
        {
            if (stream.owner == owner && stream.emitting)
            {
                EndBubbleStream(stream);
            }
        }
    }

    // =========================================================================
    // BUBBLE FOLLOWING
    // =========================================================================

    private void LateUpdate()
    {
        for (int i = 0; i < bubbleStreams.Count; i++)
        {
            BubbleStream stream = bubbleStreams[i];
            if (!stream.emitting) continue;

            bool ownerGone = stream.owner == null || !stream.owner.gameObject.activeInHierarchy;
            if (ownerGone || Time.time >= stream.stopAt)
            {
                EndBubbleStream(stream);
                continue;
            }

            stream.root.transform.position = GetFollowPosition(stream);
        }
    }

    private Vector3 GetFollowPosition(BubbleStream stream)
    {
        if (stream.followCollider != null && stream.followCollider.enabled)
        {
            Vector3 c = stream.followCollider.bounds.center;
            return new Vector3(c.x, c.y, 0f);
        }
        Vector3 p = stream.owner.transform.position;
        return new Vector3(p.x, p.y, 0f);
    }

    private void EndBubbleStream(BubbleStream stream)
    {
        stream.emitting = false;
        stream.owner = null;
        stream.followCollider = null;
        if (stream.bubbles == null) return; // already destroyed (scene unloading)
        stream.bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        stream.freeAt = Time.time + 1.3f; // let the last bubbles finish rising
    }

    // =========================================================================
    // SPLASH CONFIGURATION (per play, so Inspector tweaks apply live)
    // =========================================================================

    private void ConfigureAndPlay(SplashInstance s, float t, float w, SplashKind kind)
    {
        float size = sizeMultiplier;
        float sqrtW = Mathf.Sqrt(w);

        // How much of each layer this kind of splash gets
        float crownAmount = 1f, columnAmount = 1f, foamAmount = 1f, mistAmount = 1f, speedScale = 1f;
        switch (kind)
        {
            case SplashKind.Exit:
                crownAmount = 0.5f; columnAmount = 0.4f; foamAmount = 0.6f; mistAmount = 0.4f; speedScale = 0.75f;
                break;
            case SplashKind.Gulp:
                crownAmount = 0.35f; columnAmount = 0.8f; foamAmount = 0.5f; mistAmount = 0f; speedScale = 0.7f;
                break;
        }

        // --- Crown: droplets thrown up and out ---
        {
            var main = s.crown.main;
            float speed = Mathf.Lerp(2.5f, 7.5f, t) * speedScale * Mathf.Lerp(1f, sqrtW, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.047f * size, 0.11f * size * Mathf.Lerp(1f, 1.4f, t));
            main.startColor = new ParticleSystem.MinMaxGradient(dropletColorA, dropletColorB);
            var shape = s.crown.shape;
            shape.radius = 0.08f * w;
            SetBurst(s.crown, Mathf.RoundToInt(Mathf.Lerp(8f, 28f, t) * w * crownAmount));
        }

        // --- Column: a few fast streaks straight up (the "jump" of the splash) ---
        {
            var main = s.column.main;
            float speed = Mathf.Clamp(Mathf.Lerp(3.5f, 9f, t) * sqrtW, 3f, 12f) * speedScale;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f * size, 0.16f * size);
            main.startColor = new ParticleSystem.MinMaxGradient(dropletColorB, foamColor);
            var shape = s.column.shape;
            shape.radius = 0.05f * w;
            SetBurst(s.column, Mathf.RoundToInt(Mathf.Lerp(2f, 7f, t) * columnAmount));
        }

        // --- Foam: low spray sliding out sideways along the surface ---
        {
            var main = s.foam.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f * size, 0.25f * size * Mathf.Lerp(0.8f, 1.2f, w));
            main.startColor = foamColor;
            var shape = s.foam.shape;
            shape.scale = new Vector3(0.6f * w, 0.03f, 0.01f);

            var vel = s.foam.velocityOverLifetime;
            float side = Mathf.Lerp(1.2f, 3f, t) * sqrtW;
            vel.x = new ParticleSystem.MinMaxCurve(side, AnimationCurve.Linear(0f, -1f, 1f, 0f), AnimationCurve.Linear(0f, 1f, 1f, 0f));
            vel.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.15f, 1f, 0f), AnimationCurve.Linear(0f, 0.7f, 1f, 0f));
            vel.z = new ParticleSystem.MinMaxCurve(0f, AnimationCurve.Constant(0f, 1f, 0f), AnimationCurve.Constant(0f, 1f, 0f));
            SetBurst(s.foam, Mathf.RoundToInt(Mathf.Lerp(5f, 14f, t) * w * foamAmount));
        }

        // --- Mist: big soft puffs hanging over the impact ---
        {
            var main = s.mist.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * size * w, 0.7f * size * w);
            main.startColor = mistColor;
            var shape = s.mist.shape;
            shape.radius = 0.2f * w;
            SetBurst(s.mist, Mathf.RoundToInt(Mathf.Lerp(1f, 5f, t) * mistAmount));
        }

        // --- Ring: flat ripple ellipse expanding on the surface ---
        {
            var main = s.ring.main;
            float ringWidth = 0.55f * w * size * (0.8f + 0.6f * t);
            main.startSizeX = ringWidth;
            main.startSizeY = ringWidth * 0.28f;
            main.startSizeZ = 1f;
            main.startColor = ringColor;
            SetBurst(s.ring, 1);
        }

        PlayFresh(s.mist);
        PlayFresh(s.ring);
        PlayFresh(s.foam);
        PlayFresh(s.column);
        PlayFresh(s.crown);
    }

    private static void SetBurst(ParticleSystem ps, int count)
    {
        var emission = ps.emission;
        emission.burstCount = 1;
        emission.SetBurst(0, new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 0, 200)));
    }

    private static void PlayFresh(ParticleSystem ps)
    {
        ps.Clear();
        ps.Play();
    }

    // =========================================================================
    // POOLS
    // =========================================================================

    private SplashInstance GetFreeSplash()
    {
        SplashInstance oldest = null;
        foreach (SplashInstance s in splashes)
        {
            if (Time.time >= s.busyUntil) return s;
            if (oldest == null || s.busyUntil < oldest.busyUntil) oldest = s;
        }

        if (splashes.Count < Mathf.Max(1, maxSplashes))
        {
            SplashInstance created = BuildSplash();
            splashes.Add(created);
            return created;
        }

        // Pool is full -> reuse the splash that started longest ago
        return oldest;
    }

    private BubbleStream GetFreeBubbleStream()
    {
        foreach (BubbleStream b in bubbleStreams)
        {
            if (!b.emitting && Time.time >= b.freeAt) return b;
        }

        if (bubbleStreams.Count < Mathf.Max(1, maxBubbleStreams))
        {
            BubbleStream created = BuildBubbleStream();
            bubbleStreams.Add(created);
            return created;
        }

        return null; // too many bodies drowning at once; skip bubbles for this one
    }

    // =========================================================================
    // BUILDING PARTICLE SYSTEMS (one-time setup; per-play values are set in ConfigureAndPlay)
    // =========================================================================

    private SplashInstance BuildSplash()
    {
        var s = new SplashInstance();
        s.root = new GameObject("Splash");
        s.root.transform.SetParent(transform, false);

        // Crown droplets
        s.crown = CreateSystem(s.root.transform, "Crown", dropletMat, 3);
        {
            var main = s.crown.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.gravityModifier = 1.4f;
            SetArcShape(s.crown, 100f, 0f);
            SetSizeOverLifetime(s.crown, 1f, 0.4f);
            SetFade(s.crown, 0.7f);
            SetStretched(s.crown, 0.045f);
        }

        // Column streaks
        s.column = CreateSystem(s.root.transform, "Column", dropletMat, 2);
        {
            var main = s.column.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.gravityModifier = 1.8f;
            SetArcShape(s.column, 16f, 0f);
            SetSizeOverLifetime(s.column, 1f, 0.5f);
            SetFade(s.column, 0.75f);
            SetStretched(s.column, 0.07f);
        }

        // Foam spray along the surface
        s.foam = CreateSystem(s.root.transform, "Foam", softMat, 1);
        {
            var main = s.foam.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.gravityModifier = 0.15f;
            main.startSpeed = 0f;
            var shape = s.foam.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            var vel = s.foam.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            SetSizeOverLifetime(s.foam, 1f, 1.8f);
            SetFade(s.foam, 0.4f);
        }

        // Mist puffs
        s.mist = CreateSystem(s.root.transform, "Mist", softMat, 0);
        {
            var main = s.mist.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.gravityModifier = -0.03f;
            SetArcShape(s.mist, 140f, 0f);
            SetSizeOverLifetime(s.mist, 1f, 2f);
            SetFade(s.mist, 0.2f);
        }

        // Ripple ring
        s.ring = CreateSystem(s.root.transform, "Ring", ringMat, -1);
        {
            var main = s.ring.main;
            main.startLifetime = 0.3f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            SetSizeOverLifetime(s.ring, 0.4f, 1.8f);
            SetFade(s.ring, 0.3f);
        }

        return s;
    }

    private BubbleStream BuildBubbleStream()
    {
        var b = new BubbleStream();
        b.root = new GameObject("Bubbles");
        b.root.transform.SetParent(transform, false);

        b.bubbles = CreateSystem(b.root.transform, "BubbleParticles", bubbleMat, 1, true);
        var main = b.bubbles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.gravityModifier = -0.15f; // negative gravity -> bubbles rise

        var shape = b.bubbles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.arc = 360f;
        shape.radiusThickness = 1f;

        var noise = b.bubbles.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 1.5f;
        noise.scrollSpeed = 0.5f;

        SetSizeOverLifetime(b.bubbles, 0.6f, 1.2f);
        SetFade(b.bubbles, 0.75f);

        return b;
    }

    private ParticleSystem CreateSystem(Transform parent, string systemName, Material mat, int orderOffset, bool loop = false)
    {
        var go = new GameObject(systemName);
        go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = loop;
        main.duration = loop ? 1f : 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 256;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = mat;
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        psRenderer.sortingLayerName = sortingLayerName;
        psRenderer.sortingOrder = sortingOrder + orderOffset;

        return ps;
    }

    // Circle-arc emitter in the 2D (XY) plane, centered on straight up (+ optional tilt in degrees)
    private static void SetArcShape(ParticleSystem ps, float arcDegrees, float tilt)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.arc = arcDegrees;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
        shape.radiusThickness = 0f; // emit from the edge so droplets start spread across the body width
        shape.rotation = new Vector3(0f, 0f, 90f - arcDegrees * 0.5f + tilt);
    }

    private static void SetSizeOverLifetime(ParticleSystem ps, float from, float to)
    {
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
    }

    private static void SetFade(ParticleSystem ps, float holdUntil)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void SetStretched(ParticleSystem ps, float velocityScale)
    {
        var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        psRenderer.velocityScale = velocityScale;
        psRenderer.lengthScale = 1f;
    }

    // =========================================================================
    // GENERATED PIXEL-ART TEXTURES + MATERIALS
    // =========================================================================

    private bool BuildMaterials()
    {
        Shader shader = FindShader();
        if (shader == null)
        {
            Debug.LogError("WaterSplashFX: couldn't find a sprite shader for the splash particles. Splashes disabled.", this);
            return false;
        }

        // Point-filtered so they stay crisp next to the 64 PPU pixel art
        dropletMat = MakeMaterial(shader, "Splash Droplet", MakeTexture(8, d => d <= 0.9f ? 1f : 0f));
        softMat = MakeMaterial(shader, "Splash Soft", MakeTexture(16, d => Mathf.Floor(Mathf.Clamp01(1f - d) * 4f) / 4f));
        ringMat = MakeMaterial(shader, "Splash Ring", MakeTexture(16, d => (d > 0.62f && d <= 0.95f) ? 1f : 0f));
        bubbleMat = MakeMaterial(shader, "Splash Bubble", MakeTexture(8, d => (d > 0.55f && d <= 1f) ? 1f : 0f));
        return true;
    }

    private Shader FindShader()
    {
        string[] candidates = useLitShader
            ? new[] { "Universal Render Pipeline/2D/Sprite-Lit-Default", "Universal Render Pipeline/2D/Sprite-Unlit-Default", "Sprites/Default" }
            : new[] { "Universal Render Pipeline/2D/Sprite-Unlit-Default", "Universal Render Pipeline/Particles/Unlit", "Sprites/Default" };

        foreach (string shaderName in candidates)
        {
            Shader found = Shader.Find(shaderName);
            if (found != null) return found;
        }
        return null;
    }

    private Material MakeMaterial(Shader shader, string materialName, Texture2D texture)
    {
        var mat = new Material(shader) { name = materialName, mainTexture = texture };
        return mat;
    }

    // alphaFromDistance gets 0 at the center and 1 at the edge of the texture
    private Texture2D MakeTexture(int size, System.Func<float, float> alphaFromDistance)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "SplashTex"
        };

        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                byte a = (byte)(Mathf.Clamp01(alphaFromDistance(d)) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        generatedTextures.Add(tex);
        return tex;
    }
}
