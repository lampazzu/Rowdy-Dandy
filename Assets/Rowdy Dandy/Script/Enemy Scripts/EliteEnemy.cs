using UnityEngine;

// Night elites (WaveEnemySpawner rolls them while DayNight.IsNight): a bit bigger, more health, more EXP,
// better weapon drop chance, with a pulsing moon-red outline and drifting embers so they read at a glance.
public class EliteEnemy : MonoBehaviour
{
    public static readonly Color OutlineColor = new Color(1f, 0.25f, 0.45f, 1f);

    private EnemyHealth health;
    private SpriteOutline outline;
    private float emberTimer;

    public static bool Apply(GameObject enemy, float healthMultiplier = 2.2f, float gemMultiplier = 2.5f, float dropBonus = 20f, float sizeMultiplier = 1.15f)
    {
        EnemyHealth h = enemy != null ? enemy.GetComponentInChildren<EnemyHealth>(true) : null;
        if (h == null || h.IsObject || h.IsElite) return false;
        h.MakeElite(healthMultiplier, gemMultiplier, dropBonus);
        enemy.transform.localScale *= sizeMultiplier;
        var elite = h.gameObject.AddComponent<EliteEnemy>();
        elite.health = h;
        var sr = h.GetComponent<SpriteRenderer>();
        if (sr == null) sr = h.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) elite.outline = SpriteOutline.Add(sr, OutlineColor);
        return true;
    }

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
            outline.color = new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, pulse);
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
