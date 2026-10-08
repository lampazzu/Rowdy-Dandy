using UnityEngine;

// Rules and signals that keep enemies readable / fair:
//  - Melee enemies don't start an attack from off screen, nor in their first moments after spawning (MeleeEnemy).
//  - Archers / bombers may shoot from anywhere, but first draw a red aim trace showing where the shot comes from.
//  - "!" alert: pops once over an enemy's head when it notices Rowdy (starts chasing / sees him), see EnemyAlert.
public static class EnemyFairness
{
    public const float SpawnGrace = 0.8f;

    private static Camera cam;

    // Is this point inside the camera view? inset = fraction of the screen kept as a border (0.05 = 5% each side)
    public static bool OnScreen(Vector3 point, float inset = 0.02f)
    {
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam == null) return true; // no camera to judge with: don't block anything
        Vector3 v = cam.WorldToViewportPoint(point);
        return v.z > 0f && v.x > inset && v.x < 1f - inset && v.y > inset && v.y < 1f - inset;
    }

    public static Vector3 BodyCenter(Component enemy)
    {
        foreach (Collider2D c in enemy.GetComponents<Collider2D>())
            if (c.enabled && !c.isTrigger) return c.bounds.center;
        return enemy.transform.position;
    }

    public static float HeadHeight(Component enemy)
    {
        foreach (Collider2D c in enemy.GetComponents<Collider2D>())
            if (c.enabled && !c.isTrigger) return c.bounds.max.y - enemy.transform.position.y;
        return 0.6f;
    }

    public static bool IsRanged(EnemyHealth health)
    {
        EnemyCatalog.Entry e = health != null ? EnemyCatalog.Identify(health) : null;
        return e != null && (e.id == "gnollarcher" || e.id == "gnollbomber");
    }

    // Melee only: may it start an attack right now?
    public static bool CanStartMeleeAttack(Component enemy, float aliveSince)
    {
        if (Time.time - aliveSince < SpawnGrace) return false;
        return OnScreen(BodyCenter(enemy), 0.01f);
    }

    // Red aim trace in front of an archer / bomber for `duration` seconds before it shoots
    public static void AimTrace(Component enemy, float duration)
    {
        var go = new GameObject("Aim Trace");
        go.AddComponent<EnemyAimTrace>().Begin(enemy, duration);
    }
}

// Dashed red line from the shooter, straight ahead (they shoot the way they face; the art faces left at +x scale).
// Long enough to reach onto the screen from off screen. Flickers faster right before the shot.
public class EnemyAimTrace : MonoBehaviour
{
    private const float Length = 16f;
    private static Sprite lineSprite;
    private static readonly Color Red = new Color(1f, 0.2f, 0.25f, 1f);

    private Component enemy;
    private SpriteRenderer line, glow;
    private float duration, age;

    public void Begin(Component target, float time)
    {
        enemy = target;
        duration = Mathf.Max(0.05f, time);
        SpriteRenderer body = target.GetComponent<SpriteRenderer>();
        int layer = body != null ? body.sortingLayerID : 0, order = body != null ? body.sortingOrder : 0;
        glow = Make("Glow", layer, order - 2, 3f);
        line = Make("Line", layer, order - 1, 1f);
        LateUpdate();
    }

    private SpriteRenderer Make(string name, int layer, int order, float thickness)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LineSprite;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        if (CatFX.Unlit != null) sr.sharedMaterial = CatFX.Unlit;
        go.transform.localScale = new Vector3(Length, thickness, 1f);
        return sr;
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        if (enemy == null || age > duration) { Destroy(gameObject); return; }
        EnemyHealth h = enemy.GetComponent<EnemyHealth>();
        if (h != null && h.enemydead) { Destroy(gameObject); return; }

        float dir = enemy.transform.lossyScale.x >= 0f ? -1f : 1f;
        Vector3 from = EnemyFairness.BodyCenter(enemy) + new Vector3(dir * 0.25f, 0.05f, 0f);
        transform.position = from;
        transform.localScale = new Vector3(dir, 1f, 1f);

        float k = Mathf.Clamp01(age / duration);
        float flicker = Mathf.Repeat(age * Mathf.Lerp(6f, 22f, k), 1f) < 0.65f ? 1f : 0.45f;
        float grow = Mathf.Clamp01(age / 0.12f); // shoots out from the shooter
        line.transform.localScale = new Vector3(Length * grow, 1f, 1f);
        glow.transform.localScale = new Vector3(Length * grow, 3f, 1f);
        line.color = new Color(Red.r, Red.g, Red.b, (0.45f + 0.5f * k) * flicker);
        glow.color = new Color(Red.r, Red.g, Red.b, 0.12f * k * flicker);
    }

    // 1 world unit long, 1 px dashed line, pivot at its start
    private static Sprite LineSprite
    {
        get
        {
            if (lineSprite != null) return lineSprite;
            const int w = 64, h = 1;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "EnemyAimLine" };
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++) px[x] = (x % 6) < 4 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            lineSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), 64f);
            return lineSprite;
        }
    }
}

// "!" over an enemy's head the moment it notices Rowdy: hops up out of the head, squashes on landing, then fades. Once per aggro - it shows again only after the enemy has lost him for a while.
// Added by EnemyMovement.Start; aggro = EnemyMovement.isChasing, or MeleeEnemy seeing him (archers don't move).
public class EnemyAlert : MonoBehaviour
{
    private const float ForgetAfter = 4f;     // seconds without aggro before it can alert again
    private const float Life = 0.9f;
    private static Sprite bang;

    private EnemyMovement movement;
    private MeleeEnemy melee;
    private EnemyHealth health;
    private bool aggro, pendingShow;
    private float lostAt = -100f;

    private SpriteRenderer icon;
    private float shownAt = -10f;

    public static void Attach(GameObject enemy)
    {
        if (enemy.GetComponent<EnemyAlert>() == null) enemy.AddComponent<EnemyAlert>();
    }

    private void Start()
    {
        movement = GetComponent<EnemyMovement>();
        melee = GetComponent<MeleeEnemy>();
        health = GetComponent<EnemyHealth>();
        if (health != null && health.IsObject) enabled = false;
    }

    private void LateUpdate()
    {
        if (health != null && health.enemydead) { Hide(); return; }

        bool now = (movement != null && movement.isChasing) || (melee != null && melee.SeesPlayer);
        if (now && !aggro && Time.time - lostAt > ForgetAfter) pendingShow = true;
        if (!now && aggro) lostAt = Time.time;
        aggro = now;
        if (!now) pendingShow = false;

        // Only worth showing where you can see it
        if (pendingShow && EnemyFairness.OnScreen(EnemyFairness.BodyCenter(this), 0.02f))
        {
            pendingShow = false;
            shownAt = Time.time;
            if (icon == null) Build();
        }
        Animate();
    }

    private void Build()
    {
        var go = new GameObject("Alert !");
        icon = go.AddComponent<SpriteRenderer>();
        icon.sprite = Bang;
        SpriteRenderer body = GetComponent<SpriteRenderer>();
        icon.sortingLayerID = body != null ? body.sortingLayerID : 0;
        icon.sortingOrder = (body != null ? body.sortingOrder : 0) + 40;
        if (CatFX.Unlit != null) icon.sharedMaterial = CatFX.Unlit;
    }

    private void Animate()
    {
        if (icon == null) return;
        float t = Time.time - shownAt;
        if (t > Life) { icon.enabled = false; return; }
        icon.enabled = true;

        float head = EnemyFairness.HeadHeight(this);
        // hop: up fast, settle down; squash on the landing
        float hop = t < 0.12f ? Mathf.Lerp(0f, 0.22f, t / 0.12f) : t < 0.26f ? Mathf.Lerp(0.22f, 0.1f, (t - 0.12f) / 0.14f) : 0.1f;
        float squash = t > 0.24f && t < 0.34f ? 1f - Mathf.Sin((t - 0.24f) / 0.1f * Mathf.PI) * 0.25f : 1f;
        float pop = t < 0.08f ? Mathf.Lerp(0.4f, 1.15f, t / 0.08f) : 1f;
        icon.transform.position = new Vector3(transform.position.x, transform.position.y + head + 0.08f + hop, transform.position.z);
        icon.transform.localScale = new Vector3(pop / squash, pop * squash, 1f);

        // white flash in, warm yellow body, fade out at the end
        float fade = t > Life - 0.25f ? 1f - (t - (Life - 0.25f)) / 0.25f : 1f;
        Color c = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.3f), Mathf.Clamp01((t - 0.05f) / 0.15f));
        c.a = fade;
        icon.color = c;
    }

    private void Hide() { if (icon != null) icon.enabled = false; }

    private void OnDestroy() { if (icon != null) Destroy(icon.gameObject); }

    // 5 x 12: chunky "!" with a dark outline and a highlight column
    private static Sprite Bang
    {
        get
        {
            if (bang != null) return bang;
            Sprite raw = OverlayUI.PixelSprite(new[]
            {
                ".OOO.",
                "OHWWO",
                "OHWWO",
                "OHWWO",
                "OHWWO",
                ".OWO.",
                ".OWO.",
                "..O..",
                ".OOO.",
                "OHWWO",
                "OWWWO",
                ".OOO.",
            }, ch => ch == 'H' ? new Color32(255, 255, 255, 255) : ch == 'W' ? new Color32(235, 225, 215, 255) : new Color32(0x1B, 0x08, 0x20, 0xFF), "EnemyAlertBang");
            bang = Sprite.Create(raw.texture, raw.rect, new Vector2(0.5f, 0f), 64f);
            return bang;
        }
    }
}
