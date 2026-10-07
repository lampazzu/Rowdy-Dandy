using UnityEngine;

public enum WeaponType { Sword, Axe, Naginata, Cleaver }

// Weapon lying in the world: floating weapon icon, a beam of light so it's easy to spot,
// and a button prompt (Y on gamepad / E on keyboard) when Rowdy is close.
public class WeaponDrop : MonoBehaviour
{
    [Header("Drop Options")]
    [SerializeField] public WeaponType weaponType;
    [SerializeField] private float maxDurability = 50f;

    [Header("Icon")]
    [Tooltip("On = shows the same icon as the HUD (WeaponManager's profile thumbnail). Off = keeps this SpriteRenderer's sprite.")]
    [SerializeField] private bool useHudIcon = true;
    [SerializeField] private float bobHeight = 0.04f;
    [SerializeField] private float bobSpeed = 2.5f;

    [Header("Highlight / Pop Effect Settings")]
    [SerializeField] private float scaleMultiplier = 1.3f;        // How big it gets when player is close
    [SerializeField] private float animationSpeed = 8f;          // How fast it scales up/down
    [SerializeField] private Color purpleGlowColor = new Color(0.75f, 0.3f, 1f, 1f); // Bright purple color

    [Header("Light Beam")]
    [SerializeField] private bool showBeam = true;
    [SerializeField] private Color beamColor = new Color(0.85f, 0.5f, 1f, 0.6f);
    [Tooltip("World units")]
    [SerializeField] private float beamHeight = 2.2f;
    [SerializeField] private int beamWidthPixels = 9;
    [SerializeField] private float beamPulseSpeed = 2f;
    [SerializeField] private int sparkleCount = 4;

    [Header("Pickup Prompt")]
    [Tooltip("Height of the button prompt above the icon, in world units")]
    [SerializeField] private float promptHeight = 0.08f;

    [Header("Sounds")]
    [Tooltip("Played when an enemy drops it (not for drops already placed in the level)")]
    [SerializeField] private AudioClip dropSound;
    [SerializeField, Range(0f, 2f)] private float dropVolume = 0.8f;
    [Tooltip("Played when picked up (replaces the regular weapon switch sound)")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField, Range(0f, 2f)] private float pickupVolume = 1f;

    private const float PixelsPerUnit = 64f;
    private static Material unlitSpriteMaterial;
    private static Sprite beamSprite;
    private static int beamSpriteKey;
    private static Sprite sparkleSprite;
    private static Sprite gamepadPromptSprite;
    private static Sprite keyboardPromptSprite;

    private bool playerIsClose = false;
    private Vector3 iconBaseScale;
    private Color originalColor;
    private SpriteRenderer spriteRenderer;
    private Transform icon;
    private SpriteRenderer beamRenderer;
    private SpriteRenderer promptRenderer;
    private Transform[] sparkles;
    private float[] sparkleTimes;
    private float promptAlpha;
    private float spawnPop;
    private float bobOffset;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        bobOffset = Random.Range(0f, 10f);
    }

    private void Start()
    {
        if (useHudIcon && spriteRenderer != null)
        {
            WeaponManager wm = FindFirstObjectByType<WeaponManager>();
            Sprite hudIcon = wm != null ? wm.GetProfile(weaponType) : null;
            if (hudIcon != null) spriteRenderer.sprite = hudIcon;
        }

        BuildVisuals();

        // Dropped by an enemy mid-game (level-placed drops stay quiet on load)
        if (Time.timeSinceLevelLoad > 0.5f)
        {
            SoundManager.PlaySfx(dropSound, dropVolume);
            spawnPop = 1f;
        }
    }

    // The icon moves to its own child so the bob / pop / tint don't drag the beam and prompt along
    private void BuildVisuals()
    {
        if (spriteRenderer == null) return;

        int baseOrder = spriteRenderer.sortingOrder;
        int layer = spriteRenderer.sortingLayerID;

        if (spriteRenderer.gameObject == gameObject)
        {
            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(transform, false);
            SpriteRenderer iconRenderer = iconGo.AddComponent<SpriteRenderer>();
            iconRenderer.sprite = spriteRenderer.sprite;
            iconRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
            iconRenderer.color = spriteRenderer.color;
            iconRenderer.sortingLayerID = layer;
            iconRenderer.sortingOrder = baseOrder;
            spriteRenderer.enabled = false;
            spriteRenderer = iconRenderer;
        }
        icon = spriteRenderer.transform;
        iconBaseScale = icon.localScale;

        Material unlit = GetUnlitMaterial();

        if (showBeam)
        {
            GameObject beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(transform, false);
            beamGo.transform.localPosition = new Vector3(0f, -0.12f, 0f); // starts a bit under the icon, like it comes off the ground
            beamRenderer = beamGo.AddComponent<SpriteRenderer>();
            beamRenderer.sprite = GetBeamSprite(beamWidthPixels, Mathf.Max(8, Mathf.RoundToInt(beamHeight * PixelsPerUnit)));
            beamRenderer.color = beamColor;
            beamRenderer.sortingLayerID = layer;
            beamRenderer.sortingOrder = baseOrder - 2;
            if (unlit != null) beamRenderer.sharedMaterial = unlit;

            sparkles = new Transform[Mathf.Max(0, sparkleCount)];
            sparkleTimes = new float[sparkles.Length];
            for (int i = 0; i < sparkles.Length; i++)
            {
                GameObject sp = new GameObject("Sparkle");
                sp.transform.SetParent(beamGo.transform, false);
                SpriteRenderer sr = sp.AddComponent<SpriteRenderer>();
                sr.sprite = GetSparkleSprite();
                sr.sortingLayerID = layer;
                sr.sortingOrder = baseOrder - 1;
                if (unlit != null) sr.sharedMaterial = unlit;
                sparkles[i] = sp.transform;
                sparkleTimes[i] = i / (float)sparkles.Length; // staggered
            }
        }

        GameObject promptGo = new GameObject("Prompt");
        promptGo.transform.SetParent(transform, false);
        promptRenderer = promptGo.AddComponent<SpriteRenderer>();
        promptRenderer.sortingLayerID = layer;
        promptRenderer.sortingOrder = baseOrder + 5;
        if (unlit != null) promptRenderer.sharedMaterial = unlit;
        promptRenderer.color = new Color(1f, 1f, 1f, 0f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsClose = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsClose = false;
        }
    }

    private void Update()
    {
        AnimateVisuals();

        // --- PICKUP INPUT ---
        // Press E on Keyboard OR Triangle/Y on Gamepad
        if (playerIsClose && !PauseMenu.IsPaused && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton3)))
        {
            WeaponManager wm = FindFirstObjectByType<WeaponManager>();
            if (wm != null)
            {
                wm.PickupWeapon(weaponType, maxDurability, pickupSound, pickupVolume);
                Destroy(gameObject);
            }
        }
    }

    private void AnimateVisuals()
    {
        if (icon == null) return;

        // --- FLOAT, POP SCALE & PURPLE GLOW ---
        float bob = Mathf.Sin((Time.time + bobOffset) * bobSpeed) * bobHeight;
        icon.localPosition = new Vector3(0f, bob, 0f);

        spawnPop = Mathf.MoveTowards(spawnPop, 0f, Time.deltaTime * 3f);
        float pop = 1f + Mathf.Sin(spawnPop * Mathf.PI) * 0.6f;
        Vector3 targetScale = (playerIsClose ? iconBaseScale * scaleMultiplier : iconBaseScale) * pop;
        icon.localScale = Vector3.Lerp(icon.localScale, targetScale, Time.deltaTime * animationSpeed);

        Color targetColor = playerIsClose ? purpleGlowColor : originalColor;
        spriteRenderer.color = Color.Lerp(spriteRenderer.color, targetColor, Time.deltaTime * animationSpeed);

        // --- BEAM ---
        if (beamRenderer != null)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin((Time.time + bobOffset) * beamPulseSpeed);
            if (playerIsClose) pulse = Mathf.Min(1.2f, pulse + 0.3f);
            Color c = beamColor;
            c.a *= pulse;
            beamRenderer.color = c;

            float height = beamRenderer.sprite != null ? beamRenderer.sprite.bounds.size.y : beamHeight;
            for (int i = 0; i < sparkles.Length; i++)
            {
                sparkleTimes[i] += Time.deltaTime * 0.45f;
                if (sparkleTimes[i] > 1f) sparkleTimes[i] -= 1f;
                float t = sparkleTimes[i];

                // Rise up the beam, drifting side to side, fading near the top. Snapped to the pixel grid.
                float x = Mathf.Sin((t * 3f + i) * Mathf.PI) * (beamWidthPixels * 0.3f) / PixelsPerUnit;
                float y = t * height * 0.8f;
                sparkles[i].localPosition = new Vector3(Snap(x), Snap(y), 0f);

                SpriteRenderer sr = sparkles[i].GetComponent<SpriteRenderer>();
                Color sc = Color.Lerp(Color.white, beamColor, 0.3f);
                sc.a = Mathf.Sin(t * Mathf.PI) * (0.6f + 0.4f * pulse);
                sr.color = sc;
            }
        }

        // --- BUTTON PROMPT ---
        if (promptRenderer != null)
        {
            bool gamepad = IsGamepadConnected();
            promptRenderer.sprite = gamepad ? GetGamepadPrompt() : GetKeyboardPrompt();

            promptAlpha = Mathf.MoveTowards(promptAlpha, playerIsClose && !PauseMenu.IsPaused ? 1f : 0f, Time.deltaTime * 6f);
            promptRenderer.color = new Color(1f, 1f, 1f, promptAlpha);
            promptRenderer.enabled = promptAlpha > 0f;

            float iconTop = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.extents.y * iconBaseScale.y : 0.12f;
            float rise = (1f - promptAlpha) * -0.06f; // slides up a touch as it fades in
            promptRenderer.transform.localPosition = new Vector3(0f, Snap(iconTop + promptHeight + bob + rise), 0f);
        }
    }

    private static float Snap(float v) => Mathf.Round(v * PixelsPerUnit) / PixelsPerUnit;

    private static float gamepadCheckTime = -10f;
    private static bool gamepadCached;

    private static bool IsGamepadConnected()
    {
        if (Time.unscaledTime - gamepadCheckTime < 1f) return gamepadCached;
        gamepadCheckTime = Time.unscaledTime;
        gamepadCached = false;
        foreach (string name in Input.GetJoystickNames())
        {
            if (!string.IsNullOrEmpty(name)) { gamepadCached = true; break; }
        }
        return gamepadCached;
    }

    // ------------------------------------------------------------------ generated pixel art

    private static Material GetUnlitMaterial()
    {
        if (unlitSpriteMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) unlitSpriteMaterial = new Material(shader) { name = "Weapon Drop (Unlit)" };
        }
        return unlitSpriteMaterial;
    }

    private static Texture2D NewTexture(int w, int h, string name)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
    }

    // White column, brightest in the middle and at the bottom, fading out upward in pixel steps
    private static Sprite GetBeamSprite(int width, int height)
    {
        int key = width * 10000 + height;
        if (beamSprite != null && beamSpriteKey == key) return beamSprite;

        Texture2D tex = NewTexture(width, height, "WeaponDropBeam");
        var pixels = new Color32[width * height];
        float half = (width - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);
            float vertical = Mathf.Pow(1f - v, 1.6f);
            if (y < 3) vertical *= 0.55f + 0.15f * y; // soft foot
            for (int x = 0; x < width; x++)
            {
                float d = Mathf.Abs(x - half) / (half + 0.5f);
                float horizontal = d < 0.25f ? 1f : d < 0.55f ? 0.6f : d < 0.85f ? 0.3f : 0.12f;
                float a = horizontal * vertical;
                a = Mathf.Round(a * 6f) / 6f; // banded, pixel-art style
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false);

        beamSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        beamSprite.name = "WeaponDropBeam";
        beamSpriteKey = key;
        return beamSprite;
    }

    private static Sprite GetSparkleSprite()
    {
        if (sparkleSprite != null) return sparkleSprite;
        Texture2D tex = NewTexture(3, 3, "WeaponDropSparkle");
        Color32 w = new Color32(255, 255, 255, 255), e = new Color32(255, 255, 255, 0);
        tex.SetPixels32(new[] { e, w, e, w, w, w, e, w, e });
        tex.Apply(false);
        sparkleSprite = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        return sparkleSprite;
    }

    // Round yellow Y button (Xbox colors) with the pixel-font letter
    private static Sprite GetGamepadPrompt()
    {
        if (gamepadPromptSprite != null) return gamepadPromptSprite;

        const int size = 13;
        Color32 outline = new Color32(20, 6, 26, 255);
        Color32 face = new Color32(240, 190, 40, 255);
        Color32 shade = new Color32(190, 130, 20, 255);
        Color32 light = new Color32(255, 230, 130, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        var pixels = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                Color32 col = clear;
                if (dist <= c + 0.3f) col = outline;
                if (dist <= c - 0.7f) col = dy < -2.5f ? shade : face; // y = 0 is the bottom row
                if (dist <= c - 0.7f && dy > 2.5f && dx < 1f) col = light;
                pixels[y * size + x] = col;
            }
        }
        StampLetter(pixels, size, size, "Y", outline);

        Texture2D tex = NewTexture(size, size, "PromptButtonY");
        tex.SetPixels32(pixels);
        tex.Apply(false);
        gamepadPromptSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        return gamepadPromptSprite;
    }

    // Light keycap with E
    private static Sprite GetKeyboardPrompt()
    {
        if (keyboardPromptSprite != null) return keyboardPromptSprite;

        const int w = 13, h = 13;
        Color32 outline = new Color32(20, 6, 26, 255);
        Color32 face = new Color32(225, 220, 235, 255);
        Color32 side = new Color32(140, 130, 160, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                Color32 col = corner ? clear : outline;
                if (x > 0 && x < w - 1 && y > 0 && y < h - 1) col = y <= 2 ? side : face; // thicker bottom = keycap depth
                pixels[y * w + x] = col;
            }
        }
        StampLetter(pixels, w, h, "E", outline, 1);

        Texture2D tex = NewTexture(w, h, "PromptKeyE");
        tex.SetPixels32(pixels);
        tex.Apply(false);
        keyboardPromptSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        return keyboardPromptSprite;
    }

    // Copies a pixel-font letter (5x7) into the middle of a pixel buffer
    private static void StampLetter(Color32[] pixels, int w, int h, string letter, Color32 color, int raise = 0)
    {
        Texture2D glyph = PixelFont.Render(letter, PixelFont.Edge.DropShadow);
        Color32[] g = glyph.GetPixels32();
        int gw = PixelFont.GlyphW, gh = PixelFont.GlyphH;
        int x0 = (w - gw) / 2, y0 = (h - gh) / 2 + raise;
        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                // Render's texture is (5+1)x(7+1) with the ink starting at the top-left
                Color32 p = g[(glyph.height - 1 - y) * glyph.width + x];
                if (p.a == 255 && p.r == 255) pixels[(y0 + gh - 1 - y) * w + x0 + x] = color;
            }
        }
        Destroy(glyph);
    }
}
