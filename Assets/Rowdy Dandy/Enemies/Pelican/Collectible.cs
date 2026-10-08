using UnityEngine;

// Heart dropped by pelicans: heals Rowdy on touch (not at full health - then it waits for later).
// Dressed up like the weapon drops: pops out with a little hop, a pink beam with rising sparkles once it rests,
// a heartbeat pulse, and a "+10 HP" popup with a burst of pink pixels when it's taken.
public class Collectible : MonoBehaviour
{
    [SerializeField] private float healAmount = 10f; // Amount of health this collectible restores

    [Header("Look")]
    [SerializeField] private Color beamColor = new Color(1f, 0.35f, 0.6f, 0.55f);
    [SerializeField] private float beamHeight = 1.6f;
    [SerializeField] private int sparkleCount = 4;
    [SerializeField] private Color pickupColor = new Color(1f, 0.45f, 0.65f, 1f);

    private const float PixelsPerUnit = 64f;
    private static Sprite beamSprite, sparkleSprite;
    private static Material unlitMaterial;

    private int ignorePlayerLayer;
    private int originalLayer;
    private Transform player;
    private Health playerHealth;

    private Vector3 baseScale;
    private float age;
    private SpriteRenderer beam;
    private Transform[] sparkles;
    private SpriteRenderer[] sparkleRenderers;
    private float[] sparkleTimes;
    private Rigidbody2D body;
    private float beamAlpha;

    private void Awake()
    {
        ignorePlayerLayer = LayerMask.NameToLayer("IgnorePlayer");
        originalLayer = LayerMask.NameToLayer("projectileLayer");
        baseScale = transform.localScale;
        body = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // A little hop out of the bird, so the drop reads as a reward
        if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
            body.linearVelocity = new Vector2(Random.Range(-0.6f, 0.6f), 2.2f);
        BuildBeam();
    }

    private void Update()
    {
        age += Time.deltaTime;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { player = p.transform; playerHealth = p.GetComponent<Health>(); }
        }

        if (playerHealth != null)
        {
            // Ignore collision if player health is full
            gameObject.layer = playerHealth.currentHealth >= playerHealth.startingHealth ? ignorePlayerLayer : originalLayer;
        }

        Animate();
    }

    private void Animate()
    {
        // Pop in (overshoot), then a heartbeat: two quick beats and a rest
        float pop = age < 0.25f ? Mathf.Lerp(0.2f, 1.35f, age / 0.25f) : age < 0.4f ? Mathf.Lerp(1.35f, 1f, (age - 0.25f) / 0.15f) : 1f;
        float beat = Mathf.Repeat(age, 1.1f);
        float pulse = 1f + (beat < 0.1f ? Mathf.Sin(beat / 0.1f * Mathf.PI) * 0.14f : beat > 0.2f && beat < 0.3f ? Mathf.Sin((beat - 0.2f) / 0.1f * Mathf.PI) * 0.09f : 0f);
        transform.localScale = baseScale * pop * pulse;

        if (beam == null) return;
        bool resting = body == null || body.linearVelocity.sqrMagnitude < 0.05f;
        beamAlpha = Mathf.MoveTowards(beamAlpha, resting ? 1f : 0f, Time.deltaTime * 2.5f);
        float shimmer = 0.75f + 0.25f * Mathf.Sin(age * 2.2f);
        Color c = beamColor;
        c.a *= beamAlpha * shimmer;
        beam.color = c;
        beam.transform.rotation = Quaternion.identity; // stays upright even if the heart tumbles

        float height = beamHeight * 0.8f;
        for (int i = 0; i < sparkles.Length; i++)
        {
            sparkleTimes[i] = Mathf.Repeat(sparkleTimes[i] + Time.deltaTime * 0.5f, 1f);
            float t = sparkleTimes[i];
            float x = Mathf.Sin((t * 3f + i) * Mathf.PI) * 2f / PixelsPerUnit;
            sparkles[i].localPosition = new Vector3(Snap(x), Snap(t * height), 0f);
            Color sc = Color.Lerp(Color.white, beamColor, 0.35f);
            sc.a = Mathf.Sin(t * Mathf.PI) * beamAlpha;
            sparkleRenderers[i].color = sc;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Health health = other.GetComponent<Health>();

        if (health != null && health.currentHealth < health.startingHealth)
        {
            health.AddHealth(healAmount);
            IconPopup.Show(transform.position + Vector3.up * 0.4f, GetComponent<SpriteRenderer>()?.sprite, "+" + healAmount.ToString("0") + " HP", pickupColor);
            Burst();
            SoundManager.PlaySfx(Resources.Load<AudioClip>("Sounds/HeartPickup"), 0.8f);
            Destroy(gameObject);
        }
    }

    // ---------------------------------------------------------------- visuals
    private void BuildBeam()
    {
        SpriteRenderer own = GetComponent<SpriteRenderer>();
        Material unlit = UnlitMaterial();

        var beamGo = new GameObject("Heart Beam");
        beamGo.transform.SetParent(transform, false);
        beamGo.transform.localPosition = new Vector3(0f, -0.06f, 0f);
        // undo the heart's own scale so the beam is pixel-sized
        Vector3 s = transform.lossyScale;
        beamGo.transform.localScale = new Vector3(s.x != 0f ? 1f / Mathf.Abs(s.x) : 1f, s.y != 0f ? 1f / Mathf.Abs(s.y) : 1f, 1f);
        beam = beamGo.AddComponent<SpriteRenderer>();
        beam.sprite = BeamSprite(7, Mathf.RoundToInt(beamHeight * PixelsPerUnit));
        beam.color = new Color(0f, 0f, 0f, 0f);
        if (own != null) { beam.sortingLayerID = own.sortingLayerID; beam.sortingOrder = own.sortingOrder - 1; }
        if (unlit != null) beam.sharedMaterial = unlit;

        sparkles = new Transform[Mathf.Max(0, sparkleCount)];
        sparkleRenderers = new SpriteRenderer[sparkles.Length];
        sparkleTimes = new float[sparkles.Length];
        for (int i = 0; i < sparkles.Length; i++)
        {
            var sp = new GameObject("Sparkle");
            sp.transform.SetParent(beamGo.transform, false);
            var sr = sp.AddComponent<SpriteRenderer>();
            sr.sprite = SparkleSprite();
            sr.sortingLayerID = beam.sortingLayerID;
            sr.sortingOrder = beam.sortingOrder + 2;
            if (unlit != null) sr.sharedMaterial = unlit;
            sparkles[i] = sp.transform;
            sparkleRenderers[i] = sr;
            sparkleTimes[i] = i / (float)sparkles.Length;
        }
    }

    // Pink pixels flying out when it's picked up
    private void Burst()
    {
        var go = new GameObject("Heart Burst");
        go.transform.position = transform.position;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(2f / PixelsPerUnit, 4f / PixelsPerUnit);
        main.startColor = new ParticleSystem.MinMaxGradient(pickupColor, Color.white);
        main.gravityModifier = 0.4f;
        main.stopAction = ParticleSystemStopAction.Destroy;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        Material unlit = UnlitMaterial();
        if (unlit != null) { r.sharedMaterial = new Material(unlit) { mainTexture = Texture2D.whiteTexture }; }
        SpriteRenderer own = GetComponent<SpriteRenderer>();
        if (own != null) { r.sortingLayerID = own.sortingLayerID; r.sortingOrder = own.sortingOrder + 5; }
        ps.Play();
    }

    private static float Snap(float v) => Mathf.Round(v * PixelsPerUnit) / PixelsPerUnit;

    private static Material UnlitMaterial()
    {
        if (unlitMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) unlitMaterial = new Material(shader) { name = "Heart Drop (Unlit)" };
        }
        return unlitMaterial;
    }

    private static Sprite BeamSprite(int width, int height)
    {
        if (beamSprite != null) return beamSprite;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "HeartBeam" };
        var px = new Color32[width * height];
        float half = (width - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            float v = Mathf.Pow(1f - (float)y / (height - 1), 1.7f);
            for (int x = 0; x < width; x++)
            {
                float d = Mathf.Abs(x - half) / (half + 0.5f);
                float h = d < 0.3f ? 1f : d < 0.65f ? 0.5f : 0.18f;
                float a = Mathf.Round(h * v * 5f) / 5f;
                px[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        beamSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        return beamSprite;
    }

    private static Sprite SparkleSprite()
    {
        if (sparkleSprite != null) return sparkleSprite;
        var tex = new Texture2D(3, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "HeartSparkle" };
        Color32 w = new Color32(255, 255, 255, 255), e = new Color32(255, 255, 255, 0);
        tex.SetPixels32(new[] { e, w, e, w, w, w, e, w, e });
        tex.Apply(false);
        sparkleSprite = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        return sparkleSprite;
    }
}
