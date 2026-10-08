using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Rowdy's outlines (added by Health.Awake):
//  - blue 1 px outline so he's easy to find (Accessibility > Rowdy Outline, off by default)
//  - gold shimmering outline + sparkles while he's overhealed (pelican hearts at full health)
public class RowdyAura : MonoBehaviour
{
    private static readonly Color OutlineBlue = new Color(0.25f, 0.65f, 1f, 1f);
    private static readonly Color OverhealGold = new Color(1f, 0.82f, 0.25f, 1f);

    private SpriteRenderer body;
    private Health health;
    private SpriteOutline blue, gold;
    private float sparkleTimer;

    private void Start()
    {
        body = GetComponent<SpriteRenderer>();
        health = GetComponent<Health>();
        if (body == null) { enabled = false; return; }
        blue = SpriteOutline.Add(body, Color.clear, 1, -1);
        gold = SpriteOutline.Add(body, Color.clear, 2, -2);
    }

    private void LateUpdate()
    {
        if (body == null) return;
        bool dead = health != null && health.IsDead;
        blue.color = GameSettings.RowdyOutline && !dead ? OutlineBlue : Color.clear;

        float over = health != null && health.MaxOverheal > 0f ? health.Overheal / health.MaxOverheal : 0f;
        if (over > 0.001f && !dead)
        {
            float shimmer = 0.55f + 0.35f * Mathf.Sin(Time.time * (6f + over * 6f));
            gold.color = new Color(OverhealGold.r, OverhealGold.g, OverhealGold.b, Mathf.Lerp(0.25f, 0.9f, over) * shimmer);

            sparkleTimer -= Time.deltaTime * (0.5f + over * 2f);
            if (sparkleTimer <= 0f)
            {
                sparkleTimer = 0.12f;
                Bounds b = body.bounds;
                Vector3 p = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), 0f);
                FXParticle.Burst(p, OverhealGold, 1, 0.1f, 0.4f, -1.5f, 0.6f);
            }
        }
        else gold.color = Color.clear;
    }
}

// Rowdy healed: the TBZG heal effect on him, a "+N" popup (gold "OVERHEAL" when it went past full)
public static class HealFX
{
    private static float lastAt = -10f;

    public static void Play(Health health, float amount, bool overheal)
    {
        if (health == null || amount < 0.5f) return;
        Collider2D c = health.GetComponent<Collider2D>();
        Vector3 feet = c != null ? new Vector3(c.bounds.center.x, c.bounds.min.y, 0f) : health.transform.position;
        Vector3 head = c != null ? new Vector3(c.bounds.center.x, c.bounds.max.y, 0f) : health.transform.position + Vector3.up;
        SpriteRenderer body = health.GetComponent<SpriteRenderer>();
        int order = body != null ? body.sortingOrder + 2 : 80;

        ItemArt art = ItemArt.Get;
        if (art != null && Time.unscaledTime - lastAt > 0.25f)
        {
            SheetFX fx = SheetFX.Play(art.vfxHeal, 10, feet, 20f, 64f, order, health.transform, false,
                                      overheal ? new Color(1f, 0.95f, 0.6f) : (Color?)null, 0.6f, new Vector2(0.5f, 0.08f));
            if (fx != null && body != null) fx.SetSorting(body.sortingLayerID, order);
            lastAt = Time.unscaledTime;
        }
        IconPopup.Show(head + Vector3.up * 0.25f, null, overheal ? "+" + Mathf.RoundToInt(amount) + " OVERHEAL" : "+" + Mathf.RoundToInt(amount) + " HP",
                       overheal ? new Color(1f, 0.85f, 0.3f) : new Color(0.45f, 1f, 0.6f), 0.9f, 1.2f);
        FXSound.Play("Heal", 0.55f, overheal ? 1.25f : 1f);
    }
}

// Rowdy died: time slows right down and the colour drains out of the world until the respawn reload
public static class DeathFX
{
    public static void Play()
    {
        TimeSlowController.HitStop(0.12f, 0.02f);   // the hit lands...
        TimeSlowController.SlowMotion(3f, 0.35f);    // ...then everything crawls
        var go = new GameObject("Death Desaturate (auto)");
        go.AddComponent<DeathFXRunner>();
    }
}

public class DeathFXRunner : MonoBehaviour
{
    private Volume volume;

    private void Start()
    {
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1000;
        volume.weight = 0f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(-80f);
        color.contrast.Override(12f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.38f);
        vignette.color.Override(new Color(0.18f, 0f, 0.03f));
        volume.sharedProfile = profile;
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.7f)
        {
            if (volume != null) volume.weight = t;
            yield return null;
        }
        if (volume != null) volume.weight = 1f;
    }
}
