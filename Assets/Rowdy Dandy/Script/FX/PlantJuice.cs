using UnityEngine;

// Cutting the Blue / Orange / Purple plants: a tiny hit-stop, a bright slice streak, a burst of leaves and petals
// in the plant's colour, a little shake + rumble and a crunchy synth "thwip". Cutting several in a row raises the
// pitch (a combo), so mowing through a bush feels great. Called from EnemyHealth when an object dies.
public static class PlantJuice
{
    private static float lastCut = -10f, lastStop = -10f;
    private static int combo;
    private static AudioClip thwip;

    public static bool IsPlant(EnemyHealth e) => e != null && e.name.IndexOf("Plant", System.StringComparison.OrdinalIgnoreCase) >= 0;

    public static void OnCut(EnemyHealth plant, KillCredit credit)
    {
        if (!IsPlant(plant)) return;
        bool byRowdy = credit != null && credit.kind == KillCredit.Kind.Rowdy;
        Color color = PlantColor(plant.name);
        Vector3 at = plant.transform.position + Vector3.up * 0.25f;
        var sr = plant.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) at = sr.bounds.center;

        float now = Time.unscaledTime;
        combo = now - lastCut < 1.2f ? Mathf.Min(combo + 1, 8) : 0;
        lastCut = now;

        // Feel: only Rowdy's own cuts stop time (cats mowing a field shouldn't stutter the game)
        if (byRowdy && now - lastStop > 0.12f)
        {
            lastStop = now;
            TimeSlowController.HitStop(0.045f, 0.05f);
            GamepadRumble.Pulse(0.15f, 0.35f, 0.06f);
        }
        ScreenShake.Impulse(byRowdy ? 0.12f : 0.06f);

        // Look
        float dirX = 1f;
        var rowdy = Object.FindFirstObjectByType<Health>();
        if (rowdy != null) dirX = Mathf.Sign(at.x - rowdy.transform.position.x + 0.0001f);
        SliceStreak.Spawn(at, dirX, Color.Lerp(color, Color.white, 0.6f));
        FXParticle.Burst(at, color, 10 + combo, 1.5f, 3.6f, 7f, 0.55f, true);
        FXParticle.Burst(at, new Color(0.45f, 0.85f, 0.35f), 6, 1.2f, 2.6f, 6f, 0.5f, true); // leaves
        FXParticle.Burst(at, Color.white, 4, 2.5f, 4.5f, 2f, 0.18f);                           // sparkle
        PulseRing.Spawn(at, new Color(color.r, color.g, color.b, 0.8f), 0.45f + combo * 0.03f, 0.22f);

        // Sound: rising with the combo
        if (thwip == null) thwip = BuildThwip();
        FXSound.Play(thwip, 0.5f, 1f + combo * 0.06f + Random.Range(-0.04f, 0.04f));
        FXSound.Play("BloodHit", 0.25f, 1.7f + combo * 0.08f + Random.Range(-0.05f, 0.05f));
    }

    private static Color PlantColor(string n)
    {
        n = n.ToLowerInvariant();
        if (n.Contains("blue")) return new Color(0.35f, 0.75f, 1f);
        if (n.Contains("orange")) return new Color(1f, 0.6f, 0.2f);
        if (n.Contains("purple")) return new Color(0.75f, 0.4f, 1f);
        return new Color(0.5f, 0.9f, 0.4f);
    }

    // A short airy swish with a plucky pop on top
    private static AudioClip BuildThwip()
    {
        const int rate = 44100;
        int length = (int)(rate * 0.16f);
        var data = new float[length];
        var rng = new System.Random(7);
        float lp = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * Mathf.Lerp(0.6f, 0.08f, t / 0.16f);       // swish gets duller
            float swish = lp * Mathf.Exp(-t * 22f);
            float freq = Mathf.Lerp(1400f, 380f, Mathf.Clamp01(t / 0.07f)); // pop: fast pitch drop
            float pop = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 45f);
            data[i] = Mathf.Clamp(swish * 0.7f + pop * 0.55f, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("PlantThwip", length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

// Thin bright line across the cut, slightly tilted, that snaps wide and fades in ~0.12 s
public class SliceStreak : MonoBehaviour
{
    private SpriteRenderer sr;
    private float age;
    private Color color;
    private static Sprite pixel;

    public static void Spawn(Vector3 at, float dirX, Color color)
    {
        if (pixel == null)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 64f);
        }
        var go = new GameObject("Slice Streak");
        go.transform.position = at;
        go.transform.rotation = Quaternion.Euler(0f, 0f, dirX * Random.Range(-28f, -8f));
        var s = go.AddComponent<SliceStreak>();
        s.color = color;
        s.sr = go.AddComponent<SpriteRenderer>();
        s.sr.sprite = pixel;
        s.sr.sortingLayerName = "Default";
        s.sr.sortingOrder = 97;
        if (CatFX.Unlit != null) s.sr.sharedMaterial = CatFX.Unlit;
        s.sr.color = color;
        s.Update();
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = age / 0.14f;
        if (t >= 1f) { Destroy(gameObject); return; }
        float width = Mathf.Lerp(30f, 60f, Mathf.Sqrt(t));   // in pixels
        float thick = Mathf.Lerp(3f, 1f, t);
        transform.localScale = new Vector3(width, thick, 1f);
        sr.color = new Color(color.r, color.g, color.b, 1f - t * t);
    }
}
