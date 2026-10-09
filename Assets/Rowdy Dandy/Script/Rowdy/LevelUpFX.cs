using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Level up moment on Rowdy: full heal, a golden pillar of light rising from his feet, gold sparks floating up,
// "LEVEL UP!" over his head and a rising chime - and a golden shockwave that deals AoeDamage to every enemy
// it reaches (each one hit gets a small explosion from the gnoll archers' explosion art, Resources/VFX/Explosion). Everything is generated at runtime (no prefab to set up).
// PlayerStats calls LevelUpFX.Play(newLevel). Optional: put an AudioClip at Resources/LevelUpSound to replace the chime.
public class LevelUpFX : MonoBehaviour
{
    private static readonly Color Gold = new Color(1f, 0.82f, 0.25f, 1f);
    private static readonly Color PaleGold = new Color(1f, 0.95f, 0.65f, 1f);

    public const float AoeDamage = 10f;
    public const float AoeRadius = 3.4f;

    private static Sprite sparkSprite, beamSprite, ringSprite;
    private static Material unlitMaterial;
    private static AudioClip chime;

    // title: replaces "LEVEL UP! / LV n" (Encore levels)
    public static void Play(int newLevel, string title = null)
    {
        if (newLevel >= 2) Tutorials.Show(Tutorials.Topic.LevelUp, null, 2.4f); // first level up: +1 cat slot
        Boons.OnLevelUp(); // the boon picker opens once this moment has played
        GameObject rowdy = GameObject.FindGameObjectWithTag("Player");
        if (rowdy == null) return;

        // Full heal
        if (rowdy.TryGetComponent(out Health health)) health.AddHealth(health.startingHealth);

        var go = new GameObject("LevelUpFX");
        go.transform.position = rowdy.transform.position;
        var fx = go.AddComponent<LevelUpFX>();
        fx.StartCoroutine(fx.Run(rowdy.transform, newLevel, title));
        fx.StartCoroutine(fx.Shockwave(rowdy.transform.position));

        AudioClip clip = Resources.Load<AudioClip>("LevelUpSound");
        SoundManager.PlaySfx(clip != null ? clip : Chime, 1f);
    }

    private IEnumerator Run(Transform rowdy, int level, string title)
    {
        SpriteRenderer rowdyRenderer = rowdy.GetComponent<SpriteRenderer>();
        int sortingLayer = rowdyRenderer != null ? rowdyRenderer.sortingLayerID : 0;
        int sortingOrder = rowdyRenderer != null ? rowdyRenderer.sortingOrder : 0;
        Collider2D body = rowdy.GetComponent<Collider2D>();
        float feetOffset = body != null ? body.bounds.min.y - rowdy.position.y : -0.5f;
        float headOffset = body != null ? body.bounds.max.y - rowdy.position.y : 0.6f;

        // Pillar of light (behind him) + a soft gold Light2D
        SpriteRenderer beam = MakeRenderer("Beam", BeamSprite, sortingLayer, sortingOrder - 1, new Color(Gold.r, Gold.g, Gold.b, 0f));
        Light2D glow = null;
        try
        {
            var lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(transform, false);
            glow = lightObject.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = Gold;
            glow.intensity = 0f;
            glow.pointLightOuterRadius = 3.5f;
            glow.pointLightInnerRadius = 0.3f;
        }
        catch (System.Exception) { glow = null; }

        // "LEVEL UP!" + the new level
        Texture2D textTexture = PixelFont.Render(title ?? "LEVEL UP!\nLV " + level, PixelFont.Edge.Outline);
        var textSprite = Sprite.Create(textTexture, new Rect(0, 0, textTexture.width, textTexture.height), new Vector2(0.5f, 0f), 64f);
        SpriteRenderer text = MakeRenderer("Text", textSprite, sortingLayer, sortingOrder + 60, Gold);

        // Sparks
        const int sparkCount = 26;
        var sparks = new Transform[sparkCount];
        var sparkRenderers = new SpriteRenderer[sparkCount];
        var sparkVelocity = new Vector2[sparkCount];
        var sparkDelay = new float[sparkCount];
        for (int i = 0; i < sparkCount; i++)
        {
            sparkRenderers[i] = MakeRenderer("Spark", SparkSprite, sortingLayer, sortingOrder + 1 + (i % 2) * -2, i % 3 == 0 ? PaleGold : Gold);
            sparks[i] = sparkRenderers[i].transform;
            sparks[i].localPosition = new Vector3(Random.Range(-0.45f, 0.45f), feetOffset + Random.Range(0f, 0.4f), 0f);
            sparkVelocity[i] = new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(1.4f, 3.2f));
            sparkDelay[i] = Random.Range(0f, 0.6f);
            sparkRenderers[i].enabled = false;
        }

        const float duration = 1.8f;
        float t = 0f;
        while (t < duration)
        {
            float dt = Time.unscaledDeltaTime; // hit-stop / slow-mo shouldn't stall it
            t += dt;
            if (rowdy != null) transform.position = rowdy.position;

            // Beam: grows up from the feet fast, then thins and fades
            float grow = Mathf.Clamp01(t / 0.35f);
            float fade = 1f - Mathf.Clamp01((t - 0.6f) / 1.0f);
            float width = Mathf.Lerp(1.1f, 0.35f, Mathf.Clamp01(t / duration));
            beam.transform.localPosition = new Vector3(0f, feetOffset, 0f);
            beam.transform.localScale = new Vector3(width, Mathf.Lerp(0.2f, 4.5f, grow), 1f);
            beam.color = new Color(Gold.r, Gold.g, Gold.b, 0.55f * fade * Mathf.Clamp01(t / 0.1f));

            if (glow != null) glow.intensity = 2.2f * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);

            // Text pops in, floats up, fades out
            float pop = t < 0.12f ? Mathf.Lerp(1.6f, 1f, t / 0.12f) : 1f;
            text.transform.localPosition = new Vector3(0f, headOffset + 0.25f + t * 0.35f, 0f);
            text.transform.localScale = Vector3.one * pop;
            text.color = new Color(Gold.r, Gold.g, Gold.b, 1f - Mathf.Clamp01((t - 1.2f) / 0.6f));

            for (int i = 0; i < sparkCount; i++)
            {
                float st = t - sparkDelay[i];
                if (st < 0f) continue;
                sparkRenderers[i].enabled = true;
                sparks[i].localPosition += (Vector3)(sparkVelocity[i] * dt);
                sparks[i].localPosition += new Vector3(Mathf.Sin((st + i) * 9f) * 0.004f, 0f, 0f); // shimmer
                Color c = sparkRenderers[i].color;
                c.a = 1f - Mathf.Clamp01(st / 1.1f);
                sparkRenderers[i].color = c;
            }

            yield return null;
        }

        Destroy(textSprite);
        Destroy(textTexture);
        Destroy(gameObject);
    }

    // Gold ring rushing outwards; every enemy it passes takes AoeDamage (once), credited to Rowdy
    private IEnumerator Shockwave(Vector3 center)
    {
        var ringObject = new GameObject("Shockwave");
        ringObject.transform.position = center;
        var ring = ringObject.AddComponent<SpriteRenderer>();
        ring.sprite = RingSprite;
        ring.sortingLayerName = "Default";
        ring.sortingOrder = 150;
        if (UnlitMaterial != null) ring.sharedMaterial = UnlitMaterial;

        var hit = new System.Collections.Generic.HashSet<EnemyHealth>();
        GameObject boom = Resources.Load<GameObject>("VFX/Explosion");
        ScreenShake.Impulse(0.6f);

        const float expand = 0.3f, linger = 0.25f;
        float t = 0f;
        while (t < expand + linger)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / expand);
            float radius = Mathf.Lerp(0.2f, AoeRadius, 1f - (1f - k) * (1f - k));
            float diameter = RingSprite.bounds.size.x;
            ringObject.transform.localScale = Vector3.one * (radius * 2f / diameter);
            float fade = 1f - Mathf.Clamp01((t - expand) / linger);
            ring.color = new Color(Gold.r, Gold.g, Gold.b, 0.9f * fade);

            foreach (Collider2D c in Physics2D.OverlapCircleAll(center, radius))
            {
                EnemyHealth enemy = c.GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.enemydead || enemy.IsObject || !enemy.CompareTag("Enemy") || !hit.Add(enemy)) continue;
                KillCredit credit = KillCredit.Rowdy();
                credit.with = "Level Up";
                EnemyHealth.CreditNextHit(credit);
                enemy.TakeDamageEnemy(AoeDamage);
                if (boom != null)
                {
                    GameObject b = Instantiate(boom, enemy.transform.position, Quaternion.identity);
                    b.transform.localScale *= 0.4f;
                }
            }
            yield return null;
        }
        Destroy(ringObject);
    }

    private SpriteRenderer MakeRenderer(string objectName, Sprite sprite, int sortingLayer, int sortingOrder, Color color)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingLayerID = sortingLayer;
        sr.sortingOrder = sortingOrder;
        if (UnlitMaterial != null) sr.sharedMaterial = UnlitMaterial; // glow regardless of the scene lighting
        return sr;
    }

    // ---------------------------------------------------------------- generated assets
    private static Material UnlitMaterial
    {
        get
        {
            if (unlitMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) unlitMaterial = new Material(shader) { name = "LevelUp (Unlit)" };
            }
            return unlitMaterial;
        }
    }

    // 1-2 px pixel ring, 48 px across
    private static Sprite RingSprite
    {
        get
        {
            if (ringSprite == null)
            {
                const int size = 48;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "LevelUpRing" };
                var px = new Color32[size * size];
                float c = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                        byte a = d > c - 1.5f && d <= c + 0.5f ? (byte)255 : d > c - 3f && d <= c - 1.5f ? (byte)110 : (byte)0;
                        px[y * size + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                ringSprite = AIArt.Use("LevelUp_Ring", Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f));
            }
            return ringSprite;
        }
    }

    private static Sprite SparkSprite
    {
        get
        {
            if (sparkSprite == null)
            {
                sparkSprite = MakeSprite(new[] { ".#.", "###", ".#." }, new Vector2(0.5f, 0.5f), "LevelUpSpark");
            }
            return sparkSprite;
        }
    }

    // 8 px wide, 32 tall: bright core, soft edges, fading towards the top. Pivot at the bottom.
    private static Sprite BeamSprite
    {
        get
        {
            if (beamSprite == null)
            {
                const int w = 8, h = 32;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "LevelUpBeam" };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    float vertical = 1f - (float)y / (h - 1);
                    for (int x = 0; x < w; x++)
                    {
                        float edge = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f); // 1 at the center
                        float a = (edge > 0.7f ? 1f : edge > 0.35f ? 0.6f : 0.25f) * Mathf.Lerp(0.15f, 1f, vertical);
                        px[y * w + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                beamSprite = AIArt.Use("LevelUp_Beam", Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f));
            }
            return beamSprite;
        }
    }

    private static Sprite MakeSprite(string[] rows, Vector2 pivot, string name)
    {
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[(h - 1 - y) * w + x] = rows[y][x] == '#' ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return AIArt.Use("LevelUp_" + name, Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 64f));
    }

    // Rising major arpeggio with a little sparkle on top (C5 E5 G5 C6)
    private static AudioClip Chime
    {
        get
        {
            if (chime != null) return chime;
            const int rate = 44100;
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            const float noteGap = 0.09f, noteLength = 0.55f;
            float total = noteGap * (notes.Length - 1) + noteLength + 0.1f;
            int samples = Mathf.CeilToInt(total * rate);
            var data = new float[samples];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = Mathf.RoundToInt(n * noteGap * rate);
                int length = Mathf.RoundToInt(noteLength * rate);
                for (int i = 0; i < length && start + i < samples; i++)
                {
                    float time = (float)i / rate;
                    float envelope = Mathf.Min(1f, time / 0.005f) * Mathf.Exp(-time * 5.5f);
                    float f = notes[n];
                    // Square-ish tone (game-y) softened with a sine and an octave sparkle
                    float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * time)) * 0.25f;
                    float sine = Mathf.Sin(2f * Mathf.PI * f * time) * 0.5f;
                    float sparkle = Mathf.Sin(2f * Mathf.PI * f * 2f * time) * 0.15f * Mathf.Exp(-time * 12f);
                    data[start + i] += (square + sine + sparkle) * envelope;
                }
            }

            // Loud: the project's global volume is 0.2, so normalize to just under full scale
            float peak = 0.0001f;
            foreach (float s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            for (int i = 0; i < samples; i++) data[i] *= 0.95f / peak;

            chime = AudioClip.Create("LevelUpChime", samples, 1, rate, false);
            chime.SetData(data, 0);
            return chime;
        }
    }
}
