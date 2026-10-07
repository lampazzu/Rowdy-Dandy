using System.Collections.Generic;
using UnityEngine;

// Drives all InteractiveVegetation:
//  - global wind (with slow gusts) for the vegetation shader, and the bend springs
//  - leaf bursts (rushing through / landing in / slashing plants), footstep bits, canopy leaves from trees
//  - ambient life that always comes from plants on screen: pollen drifting up off grass and flowers,
//    and leaves blowing off trees (more during gusts)
// Creates itself automatically; add it to a scene object yourself to tweak everything in the Inspector (live in Play mode).
public class VegetationSystem : MonoBehaviour
{
    [Header("Wind")]
    [Tooltip("Sway as a fraction of plant height.")]
    [SerializeField] private float windStrength = 0.06f;
    [SerializeField] private float windSpeed = 1.6f;
    [Tooltip("How quickly the sway changes along the ground (bigger = more ripple between neighbors).")]
    [SerializeField] private float windFrequency = 0.35f;
    [Tooltip("Slow gusts on top of the base wind.")]
    [SerializeField] private float gustStrength = 0.6f;
    [SerializeField] private float gustSpeed = 0.15f;
    [Tooltip("Snap the bending to the 64 PPU pixel grid so it stays crisp.")]
    [SerializeField] private bool pixelSnap = true;

    [Header("Ambient (from plants on screen)")]
    [Tooltip("Pollen motes per second rising off visible grass / flowers.")]
    [SerializeField] private float pollenRate = 3f;
    [Tooltip("Leaves per second blowing off visible trees (scaled up during gusts).")]
    [SerializeField] private float treeLeafRate = 0.5f;

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 40;
    [Tooltip("Lit = leaves get darker at night with your 2D lights. Pollen always glows a little.")]
    [SerializeField] private bool litLeaves = true;

    private static VegetationSystem instance;
    private static bool isQuitting;

    private readonly List<InteractiveVegetation> plants = new List<InteractiveVegetation>();
    private ParticleSystem burstLeaves, driftLeaves, pollen;
    private readonly List<Object> createdAssets = new List<Object>();
    private float currentGust = 1f;
    private float pollenTimer, treeLeafTimer;

    private static readonly int WindStrengthId = Shader.PropertyToID("_VegWindStrength");
    private static readonly int WindSpeedId = Shader.PropertyToID("_VegWindSpeed");
    private static readonly int WindFrequencyId = Shader.PropertyToID("_VegWindFrequency");
    private static readonly int PixelSnapId = Shader.PropertyToID("_VegPixelSnap");

    public static bool HasInstance => instance != null;
    // The current system if there is one; never creates one (safe during shutdown / scene unload)
    public static VegetationSystem Existing => instance;

    public static VegetationSystem Instance
    {
        get
        {
            if (isQuitting || !Application.isPlaying) return null;
            if (instance == null)
            {
                instance = FindFirstObjectByType<VegetationSystem>();
                if (instance == null) instance = new GameObject("VegetationSystem (auto)").AddComponent<VegetationSystem>();
            }
            return instance;
        }
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
        BuildParticles();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            Shader.SetGlobalFloat(WindStrengthId, 0f); // keep the edit-mode scene still
        }
        foreach (Object asset in createdAssets)
        {
            if (asset != null) Destroy(asset);
        }
    }

    public void Register(InteractiveVegetation plant)
    {
        if (!plants.Contains(plant)) plants.Add(plant);
    }

    public void Unregister(InteractiveVegetation plant)
    {
        plants.Remove(plant);
    }

    private void Update()
    {
        // Wind with slow gusts
        currentGust = Mathf.Max(0f, 1f + gustStrength * (Mathf.PerlinNoise(Time.time * gustSpeed, 0.37f) - 0.5f) * 2f);
        Shader.SetGlobalFloat(WindStrengthId, windStrength * currentGust);
        Shader.SetGlobalFloat(WindSpeedId, windSpeed);
        Shader.SetGlobalFloat(WindFrequencyId, windFrequency);
        Shader.SetGlobalFloat(PixelSnapId, pixelSnap ? 1f / 64f : 0f);

        List<VegetationInteractor> interactors = VegetationInteractor.All;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        for (int i = plants.Count - 1; i >= 0; i--)
        {
            InteractiveVegetation plant = plants[i];
            if (plant == null) { plants.RemoveAt(i); continue; }
            plant.Tick(dt, interactors, this);
        }

        UpdateAmbient(dt);
    }

    // =========================================================================
    // EFFECTS (called by plants)
    // =========================================================================

    // Leaves thrown out of the foliage
    public void Rustle(Vector2 position, Color colorA, Color colorB, float intensity, float direction, float spread)
    {
        if (burstLeaves == null) return;
        int count = Mathf.RoundToInt(Mathf.Lerp(2f, 6f, intensity));
        var emit = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            emit.position = new Vector3(position.x + Random.Range(-spread, spread), position.y + Random.Range(-0.05f, 0.08f), 0f);
            emit.velocity = new Vector3(direction * Random.Range(0.3f, 1.4f) * (0.5f + intensity) + Random.Range(-0.3f, 0.3f), Random.Range(0.7f, 2.2f) * (0.6f + intensity * 0.6f), 0f);
            emit.startColor = Color.Lerp(colorA, colorB, Random.value);
            emit.startSize = Random.Range(0.035f, 0.065f);
            emit.startLifetime = Random.Range(0.6f, 1.1f);
            emit.rotation = Random.Range(0f, 360f);
            emit.angularVelocity = Random.Range(-360f, 360f);
            burstLeaves.Emit(emit, 1);
        }
    }

    // A couple of tiny bits kicked up behind a running foot
    public void Footstep(Vector2 position, Color colorA, Color colorB, float backwards)
    {
        if (burstLeaves == null) return;
        var emit = new ParticleSystem.EmitParams();
        int count = Random.Range(1, 3);
        for (int i = 0; i < count; i++)
        {
            emit.position = new Vector3(position.x + Random.Range(-0.05f, 0.05f), position.y, 0f);
            emit.velocity = new Vector3(backwards * Random.Range(0.3f, 0.9f), Random.Range(0.6f, 1.3f), 0f);
            emit.startColor = Color.Lerp(colorA, colorB, Random.value);
            emit.startSize = Random.Range(0.025f, 0.045f);
            emit.startLifetime = Random.Range(0.35f, 0.6f);
            emit.rotation = Random.Range(0f, 360f);
            emit.angularVelocity = Random.Range(-540f, 540f);
            burstLeaves.Emit(emit, 1);
        }
    }

    // Leaves let go from a tree's canopy and drift down
    public void DropCanopyLeaves(InteractiveVegetation tree, int count)
    {
        if (driftLeaves == null) return;
        var emit = new ParticleSystem.EmitParams();
        float canopyBottom = tree.BaseY + tree.Height * 0.55f;
        float canopyTop = tree.BaseY + tree.Height * 0.95f;
        float inset = (tree.MaxX - tree.MinX) * 0.2f;
        for (int i = 0; i < count; i++)
        {
            emit.position = new Vector3(Random.Range(tree.MinX + inset, tree.MaxX - inset), Random.Range(canopyBottom, canopyTop), 0f);
            emit.velocity = new Vector3(Random.Range(-0.3f, 0.3f) + windStrength * currentGust * 4f, Random.Range(-0.2f, 0.2f), 0f);
            emit.startColor = Color.Lerp(tree.LeafColorA, tree.LeafColorB, Random.value);
            emit.startSize = Random.Range(0.04f, 0.07f);
            emit.startLifetime = Random.Range(2.5f, 4f);
            emit.rotation = Random.Range(0f, 360f);
            emit.angularVelocity = Random.Range(-200f, 200f);
            driftLeaves.Emit(emit, 1);
        }
    }

    // =========================================================================
    // AMBIENT
    // =========================================================================

    private void UpdateAmbient(float dt)
    {
        if (plants.Count == 0) return;

        pollenTimer += dt * pollenRate;
        while (pollenTimer >= 1f)
        {
            pollenTimer -= 1f;
            InteractiveVegetation plant = PickVisiblePlant(false);
            if (plant != null) EmitPollen(plant);
        }

        treeLeafTimer += dt * treeLeafRate * currentGust * currentGust;
        while (treeLeafTimer >= 1f)
        {
            treeLeafTimer -= 1f;
            InteractiveVegetation tree = PickVisiblePlant(true);
            if (tree != null) DropCanopyLeaves(tree, 1);
        }
    }

    private InteractiveVegetation PickVisiblePlant(bool wantTree)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            InteractiveVegetation plant = plants[Random.Range(0, plants.Count)];
            if (plant == null || !plant.IsVisible) continue;
            bool isTree = plant.PlantMode == InteractiveVegetation.Mode.Tree;
            if (isTree == wantTree && plant.PlantMode != InteractiveVegetation.Mode.Hanging) return plant;
        }
        return null;
    }

    private void EmitPollen(InteractiveVegetation plant)
    {
        if (pollen == null) return;
        var emit = new ParticleSystem.EmitParams
        {
            position = new Vector3(Random.Range(plant.MinX, plant.MaxX), plant.BaseY + plant.Height * Random.Range(0.5f, 1f), 0f),
            velocity = new Vector3(Random.Range(-0.1f, 0.1f) + windStrength * currentGust * 3f, Random.Range(0.05f, 0.25f), 0f),
            startColor = Color.Lerp(Color.Lerp(plant.LeafColorA, Color.white, 0.55f), new Color(1f, 0.95f, 0.75f), 0.3f),
            startSize = Random.Range(0.02f, 0.035f),
            startLifetime = Random.Range(3f, 5f),
        };
        pollen.Emit(emit, 1);
    }

    // =========================================================================
    // SETUP
    // =========================================================================

    private void BuildParticles()
    {
        Shader litShader = FindShader(litLeaves
            ? new[] { "Universal Render Pipeline/2D/Sprite-Lit-Default", "Universal Render Pipeline/2D/Sprite-Unlit-Default", "Sprites/Default" }
            : new[] { "Universal Render Pipeline/2D/Sprite-Unlit-Default", "Sprites/Default" });
        Shader unlitShader = FindShader(new[] { "Universal Render Pipeline/2D/Sprite-Unlit-Default", "Sprites/Default" });
        if (litShader == null || unlitShader == null)
        {
            Debug.LogWarning("VegetationSystem: no sprite shader found for vegetation particles; leaves and pollen disabled.", this);
            return;
        }

        // 4x4 pixel leaf: diamond with a lighter center
        Color32 o = new Color32(0, 0, 0, 0), d = new Color32(200, 200, 200, 255), l = new Color32(255, 255, 255, 255);
        Material leafMaterial = MakeMaterial(litShader, "Leaf", 4, new[] { o, d, d, o, d, l, l, d, d, l, d, o, o, d, o, o });

        // 3x3 soft pollen mote
        Color32 h = new Color32(255, 255, 255, 110), f = new Color32(255, 255, 255, 255);
        Material pollenMaterial = MakeMaterial(unlitShader, "Pollen", 3, new[] { o, h, o, h, f, h, o, h, o });

        burstLeaves = MakeSystem("Leaf Bursts", leafMaterial, 0.35f, 1.5f, 0.6f, 0.7f);
        driftLeaves = MakeSystem("Falling Leaves", leafMaterial, 0.08f, 2.5f, 1.1f, 0.85f);
        pollen = MakeSystem("Pollen", pollenMaterial, -0.01f, 1f, 0.25f, 0.25f, true);
    }

    private static Shader FindShader(string[] names)
    {
        foreach (string shaderName in names)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null) return shader;
        }
        return null;
    }

    private Material MakeMaterial(Shader shader, string materialName, int size, Color32[] pixels)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = materialName + "Tex" };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var material = new Material(shader) { name = materialName + " Particles", mainTexture = texture };
        createdAssets.Add(texture);
        createdAssets.Add(material);
        return material;
    }

    private ParticleSystem MakeSystem(string systemName, Material material, float gravity, float drag, float noiseStrength, float fadeStart, bool fadeIn = false)
    {
        var go = new GameObject(systemName);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 500;
        main.gravityModifier = gravity;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.enabled = false;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = noiseStrength;
        noise.frequency = 1f;
        noise.scrollSpeed = 0.4f;

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 100f;
        limit.drag = drag;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            fadeIn
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, fadeStart + 0.4f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(gradient);

        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = material;
        psRenderer.sortingLayerName = sortingLayerName;
        psRenderer.sortingOrder = sortingOrder;

        ps.Play();
        return ps;
    }
}
