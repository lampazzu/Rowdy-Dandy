using UnityEngine;

// RED JELLY (was the Volt Rat; the script / prefab keep the old name): an angry red jellyfish that hovers around Rowdy,
// pulses to charge up, then jets straight
// through where he stands. Small, fragile, comes in packs. Art: PIV_Flying_Rat ("New Stuff you can use"), animated by
// real clips in Rowdy Dandy/Enemies/VoltRat (made by Tools > Rowdy Dandy > Frontier > Build Volt Rat):
//   Fly (hover)  Charge (crackling)  Dash (zipping)  Recover  GetHit  Death (the burst on the sheet's second row)
// Its hitbox (child "Hitbox", Spike) is only on during the dash, so touching it otherwise doesn't hurt.
[RequireComponent(typeof(EnemyHealth))]
public class VoltRat : MonoBehaviour
{
    private enum State { Hover, Charge, Dash, Recover, Dead }

    [SerializeField] private float hoverRadius = 2.4f;
    [SerializeField] private float hoverHeight = 1.3f;
    [SerializeField] private float hoverSpeed = 3.5f;
    [SerializeField] private float chargeTime = 0.6f;
    [SerializeField] private float dashSpeed = 11f;
    [SerializeField] private float recoverTime = 0.5f;
    [SerializeField] private Vector2 attackEvery = new Vector2(1.6f, 3f);
    [SerializeField] private AudioClip crackle;

    private EnemyHealth health;
    private Animator anim;
    private SpriteRenderer body;
    private Rigidbody2D rb;
    private GameObject hitbox;
    private State state = State.Hover;
    private float stateAt, nextAttackAt, angle, lastHealth, diedAt = -1f, afterimageTimer;
    private Vector3 dashTarget, dashDir;
    private string playing;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        anim = GetComponent<Animator>();
        body = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        Transform h = transform.Find("Hitbox");
        hitbox = h != null ? h.gameObject : null;
        if (hitbox != null) hitbox.SetActive(false);
        angle = Random.Range(0f, Mathf.PI * 2f);
        nextAttackAt = Time.time + Random.Range(attackEvery.x, attackEvery.y) + 0.8f;
        health.SetExpDropIfMissing(Resources.Load<GameObject>("Systems/EXPgem"), 2);
        // reads against the red sunset: a dark rim and a small red glow
        if (body != null)
        {
            body.color = Color.white;
            rim = SpriteOutline.Add(body, new Color(0.12f, 0.02f, 0.06f, 1f), 1, -1);
            var glow = new GameObject("Glow").AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            glow.transform.SetParent(transform, false);
            glow.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            glow.color = new Color(1f, 0.3f, 0.25f);
            glow.intensity = 0.7f;
            glow.pointLightOuterRadius = 1.2f;
            glow.pointLightInnerRadius = 0.05f;
        }
    }

    private void Start() => lastHealth = health.currentenemyHealth;

    private static Transform Rowdy => BoonRunner.Rowdy;

    private void Play(string clip)
    {
        if (playing == clip || anim == null || !anim.isActiveAndEnabled) return;
        playing = clip;
        anim.Play(clip, 0, 0f);
    }

    // RED JELLY: the art is the little jellyfish (Enemies/WaterViva/WaterViva.png, 7 frames of 24 x 24), recoloured
    // angry red at runtime and drawn over whatever the animator does. The AI (hover, charge, dash) is the Volt Rat's.
    [SerializeField] private Texture2D jellySheet;
    private SpriteOutline rim;
    private static Sprite[] frames;
    private string drawn;
    private float drawnAt;

    private void LateUpdate()
    {
        if (body == null) return;
        if (frames == null)
        {
            if (jellySheet == null) return;
            frames = RedFrames(jellySheet, 7);
            if (frames == null) return;
        }
        string clip = playing ?? "Fly";
        if (clip != drawn) { drawn = clip; drawnAt = Time.time; }
        float t = Time.time - drawnAt;
        int n = frames.Length;
        Color c = Color.white;
        int i;
        switch (clip)
        {
            case "Charge": i = (int)(t * 24f) % n; c = Mathf.Repeat(t * 10f, 1f) < 0.5f ? Color.white : new Color(1f, 0.75f, 0.75f); break; // pulsing fast
            case "Dash": i = 3; break;                                   // tentacles trailing
            case "GetHit": i = (int)(t * 14f) % n; c = Mathf.Repeat(t * 14f, 1f) < 0.5f ? new Color(1f, 1f, 1f, 0.5f) : Color.white; break;
            case "Death":
                i = Mathf.Min((int)(t * 14f), n - 1);
                float k = Mathf.Clamp01(t / 0.5f);
                c = new Color(1f, 1f, 1f, 1f - k);                      // pops and fades
                transform.localScale = new Vector3(Mathf.Sign(transform.localScale.x), 1f, 1f) * (1f + k * 0.6f);
                break;
            default: i = (int)(t * 9f) % n; break;                       // floating
        }
        body.sprite = frames[i];
        body.color = c;
    }

    // Hue-shifts the jelly to red (works on non-readable textures: read back through a render texture)
    private static Sprite[] RedFrames(Texture2D sheet, int count)
    {
        int w = sheet.width, h = sheet.height;
        Color32[] px;
        try
        {
            if (sheet.isReadable) px = sheet.GetPixels32();
            else
            {
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                RenderTexture prev = RenderTexture.active;
                Graphics.Blit(sheet, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                copy.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                px = copy.GetPixels32();
                Destroy(copy);
            }
        }
        catch { return null; }
        for (int p = 0; p < px.Length; p++)
        {
            if (px[p].a == 0) continue;
            Color.RGBToHSV(px[p], out float hh, out float s, out float v);
            // blues / pinks -> reds, keeping the light and dark of the art; the glowing core turns hot orange
            float hue = Mathf.Lerp(0.98f, 1.04f, v) % 1f;
            Color col = Color.HSVToRGB(hue, Mathf.Clamp01(s * 0.6f + 0.45f), Mathf.Clamp01(v * 1.05f));
            col.a = px[p].a / 255f;
            px[p] = col;
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "RedJelly" };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var result = new Sprite[count];
        int fw = w / count;
        for (int f = 0; f < count; f++) result[f] = Sprite.Create(tex, new Rect(f * fw, 0, fw, h), new Vector2(0.5f, 0.4f), 64f);
        return result;
    }

    private void Go(State s)
    {
        state = s;
        stateAt = Time.time;
        if (hitbox != null) hitbox.SetActive(s == State.Dash);
        switch (s)
        {
            case State.Hover: Play("Fly"); break;
            case State.Charge:
                Play("Charge");
                if (crackle != null) FXSound.Play(crackle, 0.35f, Random.Range(1.4f, 1.7f));
                else FXSound.Play(BoonArt.Get != null ? BoonArt.Get.zap : null, 0.25f, Random.Range(1.6f, 1.9f));
                PulseRing.Spawn(transform.position, new Color(1f, 0.35f, 0.3f, 0.9f), 0.6f, 0.25f);
                break;
            case State.Dash: Play("Dash"); break;
            case State.Recover: Play("Recover"); break;
        }
    }

    private void Update()
    {
        if (state == State.Dead)
        {
            if (Time.time - diedAt > 0.8f) Destroy(gameObject);
            return;
        }
        if (health.enemydead) { Die(); return; }

        // got hit: drop whatever it was doing (EnemyHealth plays the GetHit clip itself)
        if (health.currentenemyHealth < lastHealth - 0.01f)
        {
            lastHealth = health.currentenemyHealth;
            if (hitbox != null) hitbox.SetActive(false);
            playing = "GetHit";
            state = State.Recover;
            stateAt = Time.time - recoverTime * 0.3f;
            Vector3 away = Rowdy != null ? (transform.position - Rowdy.position).normalized : Vector3.up;
            transform.position += away * 0.35f;
            return;
        }

        if (StatusEffects.IsStunned(gameObject)) return;
        Transform rowdy = Rowdy;
        if (rowdy == null) return;
        Vector3 target = BoonRunner.RowdyCenter;
        float t = Time.time - stateAt;

        switch (state)
        {
            case State.Hover:
                angle += Time.deltaTime * 1.4f;
                Vector3 spot = target + new Vector3(Mathf.Cos(angle) * hoverRadius, hoverHeight + Mathf.Sin(angle * 2f) * 0.4f, 0f);
                Move(Vector3.MoveTowards(transform.position, spot, hoverSpeed * Time.deltaTime));
                Face(target.x);
                if (playing != "Fly" && playing != "GetHit") Play("Fly");
                if (playing == "GetHit" && t > 0.3f) Play("Fly");
                if (Time.time >= nextAttackAt && EnemyFairness.OnScreen(transform.position, 0.04f)) Go(State.Charge);
                break;

            case State.Charge:
                Face(target.x);
                // crackling jitter, a whole pixel at a time
                transform.position += new Vector3(Mathf.Round(Random.Range(-1f, 1f)) / 64f, Mathf.Round(Random.Range(-1f, 1f)) / 64f, 0f);
                if (Random.value < 0.3f) FXParticle.Burst(transform.position, new Color(1f, 0.45f, 0.35f), 1, 0.5f, 1.5f, 0f, 0.2f);
                if (t >= chargeTime)
                {
                    dashDir = (target - transform.position).normalized;
                    dashTarget = target + dashDir * 1.5f;
                    Go(State.Dash);
                }
                break;

            case State.Dash:
                Vector3 next = Vector3.MoveTowards(transform.position, dashTarget, dashSpeed * Time.deltaTime);
                bool blocked = SolidGround.Blocked(next, new Vector2(0.2f, 0.15f));
                if (!blocked) Move(next);
                afterimageTimer -= Time.deltaTime;
                if (afterimageTimer <= 0f) { afterimageTimer = 0.03f; CatFX.Afterimage(body, new Color(1f, 0.3f, 0.3f, 0.5f), 0.15f); }
                if (blocked || (transform.position - dashTarget).sqrMagnitude < 0.01f || t > 1f) Go(State.Recover);
                break;

            case State.Recover:
                if (playing == "GetHit" && t > recoverTime * 0.3f + 0.25f) Play("Recover");
                if (t >= recoverTime)
                {
                    nextAttackAt = Time.time + Random.Range(attackEvery.x, attackEvery.y);
                    Go(State.Hover);
                }
                break;
        }
    }

    private void Move(Vector3 p)
    {
        if (rb != null && rb.bodyType != RigidbodyType2D.Static) rb.MovePosition(p);
        else transform.position = p;
    }

    // the art faces left
    private void Face(float x)
    {
        if (body != null) body.flipX = x > transform.position.x;
    }

    private void Die()
    {
        state = State.Dead;
        diedAt = Time.time;
        if (hitbox != null) hitbox.SetActive(false);
        foreach (Collider2D c in GetComponents<Collider2D>()) c.enabled = false;
        playing = "Death";
        if (rim != null) Destroy(rim.gameObject);
        if (anim != null) anim.Play("Death", 0, 0f);
        FXSound.Play("Jelly", 0.45f, 0.7f); // squelch
        FXParticle.Burst(transform.position, new Color(1f, 0.35f, 0.3f), 14, 1f, 3.5f, 3f, 0.5f);
    }
}
