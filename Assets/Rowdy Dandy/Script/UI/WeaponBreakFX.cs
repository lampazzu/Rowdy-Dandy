using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The "your weapon just broke" moment, played by WeaponManager when durability hits 0:
// weapon-colored shards from Rowdy's hands, a short slow-mo beat, a break sound, "[icon] BROKEN" popup,
// and the HUD weapon icon splitting in two before the Rod pops in.
// Creates itself automatically; add it to a scene object to tweak it in the Inspector.
public class WeaponBreakFX : MonoBehaviour
{
    [Header("Slow-Mo")]
    [SerializeField] private float slowMoScale = 0.2f;
    [Tooltip("Real-time seconds.")]
    [SerializeField] private float slowMoDuration = 0.22f;

    [Header("Sound & Text")]
    [Tooltip("SoundManager sound name. Leave empty for none.")]
    [SerializeField] private string breakSound = "QuebraTudo";
    [SerializeField] private string breakText = "BROKEN";
    [SerializeField] private Color breakTextColor = new Color(1f, 0.35f, 0.4f, 1f);

    [Header("Shards")]
    [SerializeField] private int shardCount = 16;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 70;

    [Header("HUD Icon Split")]
    [SerializeField] private float iconSplitDuration = 0.6f;
    [SerializeField] private Color iconFlashColor = new Color(1f, 0.3f, 0.35f, 1f);

    // Same palette as the generated weapon icons
    private static readonly Color Light = new Color32(0xF2, 0xEC, 0xFA, 0xFF);
    private static readonly Color Mid = new Color32(0xB7, 0xA9, 0xD6, 0xFF);
    private static readonly Color Dark = new Color32(0x6E, 0x5A, 0x8C, 0xFF);
    private static readonly Color Gold = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
    private static readonly Color Wood = new Color32(0xB8, 0x74, 0x3E, 0xFF);
    private static readonly Color Magenta = new Color32(0xFF, 0x39, 0xC0, 0xFF);

    private static WeaponBreakFX instance;
    private ParticleSystem shards;
    private Material shardMaterial;
    private Texture2D shardTexture;
    private GameObject damageTextPrefab;
    private Coroutine slowMoRoutine;

    public static WeaponBreakFX Instance
    {
        get
        {
            if (!Application.isPlaying) return null;
            if (instance == null)
            {
                instance = FindFirstObjectByType<WeaponBreakFX>();
                if (instance == null) instance = new GameObject("WeaponBreakFX (auto)").AddComponent<WeaponBreakFX>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        damageTextPrefab = Resources.Load<GameObject>("DamageTextPrefab");
        BuildShards();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            if (slowMoRoutine != null && Mathf.Approximately(Time.timeScale, slowMoScale)) Time.timeScale = 1f;
        }
        if (shardMaterial != null) Destroy(shardMaterial);
        if (shardTexture != null) Destroy(shardTexture);
    }

    // weaponName picks the shard colors (Sword / Naginata / Cleaver); hudIcon + brokenIcon drive the HUD split
    public void Play(Vector3 worldPosition, float facing, string weaponName, Image hudIcon, Sprite brokenIcon)
    {
        EmitShards(worldPosition + new Vector3(facing * 0.25f, 0.1f, 0f), facing, PaletteFor(weaponName));

        if (!string.IsNullOrEmpty(breakSound) && SoundManager.Instance != null) SoundManager.Instance.PlaySound(breakSound);

        if (brokenIcon != null && !string.IsNullOrEmpty(breakText))
        {
            IconPopup.Show(worldPosition + Vector3.up * 0.8f, brokenIcon, breakText, breakTextColor, 1.2f); // [icon] BROKEN
        }
        else if (damageTextPrefab != null && !string.IsNullOrEmpty(breakText))
        {
            GameObject text = Instantiate(damageTextPrefab, worldPosition + Vector3.up * 0.8f, Quaternion.identity);
            if (text.TryGetComponent(out FloatingDamageText floating)) floating.SetupCustomText(breakText, breakTextColor, 1.6f);
        }

        if (hudIcon != null && brokenIcon != null) StartCoroutine(SplitIcon(hudIcon, brokenIcon));

        if (slowMoScale < 1f && slowMoDuration > 0f)
        {
            if (slowMoRoutine != null) StopCoroutine(slowMoRoutine);
            slowMoRoutine = StartCoroutine(SlowMo());
        }
    }

    private static Color[] PaletteFor(string weaponName)
    {
        string n = (weaponName ?? "").ToLowerInvariant();
        if (n.Contains("naginata")) return new[] { Light, Mid, Wood, Gold, Magenta };
        if (n.Contains("cleaver")) return new[] { Light, Mid, Dark, Wood, Gold };
        return new[] { Light, Mid, Dark, Gold }; // sword / default metal
    }

    private IEnumerator SlowMo()
    {
        float previous = Time.timeScale;
        Time.timeScale = slowMoScale;
        yield return new WaitForSecondsRealtime(slowMoDuration);
        // Only restore if nothing else changed time in the meantime
        if (Mathf.Approximately(Time.timeScale, slowMoScale)) Time.timeScale = previous > 0f ? previous : 1f;
        slowMoRoutine = null;
    }

    private void EmitShards(Vector3 position, float facing, Color[] palette)
    {
        if (shards == null) return;
        var emit = new ParticleSystem.EmitParams();
        for (int i = 0; i < shardCount; i++)
        {
            Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(-70f, 70f)) * new Vector2(facing, 1.2f).normalized;
            emit.position = position + (Vector3)(Random.insideUnitCircle * 0.12f);
            emit.velocity = dir * Random.Range(2.5f, 6f);
            emit.startColor = palette[Random.Range(0, palette.Length)];
            emit.startSize = Random.Range(0.04f, 0.09f);
            emit.startLifetime = Random.Range(0.5f, 0.9f);
            emit.rotation = Random.Range(0f, 360f);
            emit.angularVelocity = Random.Range(-720f, 720f);
            shards.Emit(emit, 1);
        }
    }

    // Two halves of the old icon fly apart, spin and fade (unscaled time, so it plays through the slow-mo)
    private IEnumerator SplitIcon(Image hudIcon, Sprite brokenIcon)
    {
        RectTransform source = hudIcon.rectTransform;
        Image left = MakeHalf(source, brokenIcon, (int)Image.OriginHorizontal.Left);
        Image right = MakeHalf(source, brokenIcon, (int)Image.OriginHorizontal.Right);

        float t = 0f;
        Vector2 start = left.rectTransform.anchoredPosition;
        float width = source.rect.width;
        while (t < iconSplitDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / iconSplitDuration);
            float drop = k * k * width * 1.2f;

            left.rectTransform.anchoredPosition = start + new Vector2(-k * width * 0.35f, -drop);
            right.rectTransform.anchoredPosition = start + new Vector2(k * width * 0.35f, -drop);
            left.rectTransform.localRotation = Quaternion.Euler(0f, 0f, k * 35f);
            right.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -k * 35f);

            Color c = Color.Lerp(iconFlashColor, Color.white, Mathf.Clamp01(k * 5f));
            c.a = 1f - Mathf.Clamp01((k - 0.4f) / 0.6f);
            left.color = right.color = c;
            yield return null;
        }

        Destroy(left.gameObject);
        Destroy(right.gameObject);
    }

    private static Image MakeHalf(RectTransform source, Sprite sprite, int origin)
    {
        var go = new GameObject("BrokenIconHalf", typeof(RectTransform), typeof(Image));
        go.layer = source.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(source.parent, false);
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition = source.anchoredPosition;
        rect.sizeDelta = source.sizeDelta;
        rect.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = origin;
        image.fillAmount = 0.5f;
        return image;
    }

    private void BuildShards()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return;

        shardTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "ShardTex" };
        shardTexture.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(220, 220, 220, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
        shardTexture.Apply(false, true);
        shardMaterial = new Material(shader) { name = "Weapon Shards", mainTexture = shardTexture };

        var go = new GameObject("Shards");
        go.transform.SetParent(transform, false);
        shards = go.AddComponent<ParticleSystem>();
        shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = shards.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true; // shards fly at full speed while the world is in slow-mo
        main.gravityModifier = 1.6f;
        main.maxParticles = 200;

        var emission = shards.emission;
        emission.rateOverTime = 0f;
        var shape = shards.shape;
        shape.enabled = false;

        var fade = shards.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = shardMaterial;
        psRenderer.sortingLayerName = sortingLayerName;
        psRenderer.sortingOrder = sortingOrder;

        shards.Play();
    }
}
