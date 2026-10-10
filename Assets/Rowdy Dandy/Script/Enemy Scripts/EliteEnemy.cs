using UnityEngine;

// Night elites (WaveEnemySpawner rolls them while DayNight.IsNight): a bit bigger, more health, more EXP,
// better weapon drop chance, with a pulsing moon-red outline and drifting embers so they read at a glance.
public class EliteEnemy : MonoBehaviour
{
    public static readonly Color OutlineColor = new Color(1f, 0.25f, 0.45f, 1f);

    private EnemyHealth health;
    private SpriteOutline outline;
    private float emberTimer;

    // Elites of the heavy kinds also get HYPER ARMOR (EnemyHealth.HyperArmor): hits don't stagger them unless they're
    // stunned / charmed / rooted or countered. Their outline is a steel-gold instead of moon red.
    public static readonly Color ArmorColor = new Color(1f, 0.78f, 0.3f, 1f);
    private static readonly string[] Armored = { "bigwolf", "wereknight", "megacreature", "horserider", "gnollwarrior", "transformwolf", "moonboundelder" };

    // sizeMultiplier: kept for old callers, but pixel art is never scaled up any more (it smeared their pixels)
    public static bool Apply(GameObject enemy, float healthMultiplier = 2.2f, float gemMultiplier = 2.5f, float dropBonus = 20f, float sizeMultiplier = 1f)
    {
        EnemyHealth h = enemy != null ? enemy.GetComponentInChildren<EnemyHealth>(true) : null;
        if (h == null || h.IsObject || h.IsElite) return false;
        h.MakeElite(healthMultiplier, gemMultiplier, dropBonus);
        if (sizeMultiplier < 1f) enemy.transform.localScale *= sizeMultiplier;
        var elite = h.gameObject.AddComponent<EliteEnemy>();
        elite.health = h;
        EnemyCatalog.Entry kind = EnemyCatalog.Identify(h);
        elite.armored = kind != null && System.Array.IndexOf(Armored, kind.id) >= 0;
        if (elite.armored) h.HyperArmor = true;
        var sr = h.GetComponent<SpriteRenderer>();
        if (sr == null) sr = h.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) elite.outline = SpriteOutline.Add(sr, elite.armored ? ArmorColor : OutlineColor);
        return true;
    }

    private bool armored;

    private void Update()
    {
        if (health == null) { Destroy(this); return; }
        if (health.enemydead)
        {
            if (outline != null) Destroy(outline.gameObject);
            Destroy(this);
            return;
        }
        if (outline != null)
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 5f);
            Color c = armored ? (health.ArmorHolds ? ArmorColor : new Color(0.55f, 0.55f, 0.6f)) : OutlineColor; // grey while its armor is down
            outline.color = new Color(c.r, c.g, c.b, pulse);
        }
        emberTimer -= Time.deltaTime;
        if (emberTimer <= 0f)
        {
            emberTimer = Random.Range(0.12f, 0.3f);
            Vector3 at = transform.position + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.8f), 0f);
            FXParticle.Burst(at, OutlineColor, 1, 0.2f, 0.6f, -1.2f, 0.7f, true);
        }
    }
}
