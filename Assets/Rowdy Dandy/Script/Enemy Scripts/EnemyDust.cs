using UnityEngine;

// Enemies kick up the same dust as Rowdy: a copy of his running dust (PlayerMovement.dust) puffs at their feet
// while they walk on solid ground, and his big dust (bigdust) when they land from a leap / fall.
// Added by EnemyMovement.Start to walkers (not swimmers, flyers or objects).
public class EnemyDust : MonoBehaviour
{
    private static ParticleSystem runTemplate, landTemplate;
    private static float templateSearchedAt = -10f;

    private Rigidbody2D body;
    private Collider2D col;
    private EnemyHealth health;
    private ParticleSystem run, land;
    private float lastVy;
    private float groundCheckTimer;
    private bool onSolidGround;

    public static void Attach(GameObject enemy)
    {
        if (enemy.GetComponent<EnemyDust>() == null) enemy.AddComponent<EnemyDust>();
    }

    private void Start()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        foreach (Collider2D c in GetComponents<Collider2D>()) if (c.enabled && !c.isTrigger) { col = c; break; }
        if (body == null || body.gravityScale <= 0f || (health != null && health.IsObject)) { enabled = false; return; }
        FindTemplates();
        if (runTemplate == null) { enabled = false; return; }
        run = Copy(runTemplate, "Enemy Dust");
        if (landTemplate != null) land = Copy(landTemplate, "Enemy Land Dust");
    }

    private static void FindTemplates()
    {
        if (runTemplate != null || Time.time - templateSearchedAt < 3f) return;
        templateSearchedAt = Time.time;
        PlayerMovement rowdy = FindFirstObjectByType<PlayerMovement>();
        if (rowdy == null) return;
        runTemplate = rowdy.dust;
        landTemplate = rowdy.bigdust;
    }

    private ParticleSystem Copy(ParticleSystem template, string name)
    {
        ParticleSystem ps = Instantiate(template);
        ps.name = name;
        ps.transform.SetParent(null, true);
        ps.transform.localScale = Vector3.one;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        return ps;
    }

    private void LateUpdate()
    {
        if (run == null) return;
        bool alive = health == null || !health.enemydead;
        Vector3 feet = col != null ? new Vector3(col.bounds.center.x, col.bounds.min.y, transform.position.z) : transform.position;
        run.transform.position = feet;
        if (land != null) land.transform.position = feet;

        // Solid ground under the feet (not water), checked a few times a second
        groundCheckTimer -= Time.deltaTime;
        if (groundCheckTimer <= 0f)
        {
            groundCheckTimer = 0.15f;
            onSolidGround = false;
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(feet + Vector3.up * 0.05f, Vector2.down, 0.2f))
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.attachedRigidbody == body) continue;
                if (hit.collider.CompareTag("Water") || hit.collider.gameObject.layer == 6) break;
                onSolidGround = true;
                break;
            }
        }

        float vx = body.linearVelocity.x, vy = body.linearVelocity.y;
        if (alive && onSolidGround && Mathf.Abs(vx) > 0.4f && Mathf.Abs(vy) < 1f) run.Play();

        // Landing: was falling fast, now stopped on the ground
        if (alive && land != null && lastVy < -3f && Mathf.Abs(vy) < 0.5f && onSolidGround) land.Play();
        lastVy = vy;
    }

    private void OnDestroy()
    {
        if (run != null) Destroy(run.gameObject, 2f);
        if (land != null) Destroy(land.gameObject, 2f);
    }
}
