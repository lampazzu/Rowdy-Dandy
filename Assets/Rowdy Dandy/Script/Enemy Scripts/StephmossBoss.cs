using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// STEPHMOSS, LORD OF SEVERED WOODS - the poison patron tests Rowdy herself: the last wave of THE PURPLE REIGN.
// The user's Graft boss, with the Graft boss song (Resources/Sounds/StephmossTheme). Drawn from the Graft sheets
// (GraftFX), no Animator: a little state machine below.
//   CHARGE      - plants the blades (wind-up, she flashes), then slices across the arena
//   WHIRLWIND   - spins up and drifts after Rowdy: a whirl of blades that cuts everything near
//   SWARM       - calls insects that dive at Rowdy
//   BLINK       - vanishes into the swarm and appears behind him, straight into a charge
//   MOLT        - once, below 40%: heals while shedding (hit her hard to cut it short)
// Hyper armor (no flinch), but stuns / charms / counters still stagger her. Hits flash her white (HitFlash).
public class StephmossBoss : MonoBehaviour
{
    private static readonly Vector2 Feet = new Vector2(136f, 164f); // on the Graft boss canvas (273 x 165)
    private static readonly Color Toxic = new Color(0.72f, 1f, 0.25f);

    private EnemyHealth health;
    private SpriteRenderer body, fx;
    private FrontierArena arena;
    private float left, right, floorY;
    private Sprite[] idle, appear, vanish, chargeBody, chargeFx, whirlBegin, whirlBody, whirlFx, whirlEnd, spawn, heal, death;
    private Sprite[] current;
    private float frameT, fps = 10f;
    private bool loopAnim = true, facingRight, molted, dying;
    private static Health rowdyHealth;

    public static GameObject Spawn(Vector3 at, FrontierArena arena, float healthScale)
    {
        var go = new GameObject("Stephmoss");
        go.SetActive(false);
        go.transform.position = at;
        go.tag = "Enemy";
        go.layer = LayerMask.NameToLayer("Enemy") >= 0 ? LayerMask.NameToLayer("Enemy") : 0;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.1f, 1.6f);
        col.offset = new Vector2(0f, 0.85f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 20;
        if (ItemArt.Lit != null) sr.sharedMaterial = ItemArt.Lit;
        var h = go.AddComponent<EnemyHealth>();
        var boss = go.AddComponent<StephmossBoss>();
        boss.arena = arena;
        go.SetActive(true);
        h.SetMaxHealth(3200f * healthScale);
        h.HyperArmor = true;
        h.SetExpDropIfMissing(Resources.Load<GameObject>("Systems/EXPgem"), 40);
        return go;
    }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        body = GetComponent<SpriteRenderer>();
        fx = new GameObject("Blades").AddComponent<SpriteRenderer>();
        fx.transform.SetParent(transform, false);
        fx.sortingOrder = 21;
        if (CatFX.Unlit != null) fx.sharedMaterial = CatFX.Unlit;
        idle = F("BRJ_Protobug_Idle"); appear = F("BRJ_Bug_Appear"); vanish = F("BRJ_Bug_Vanish");
        chargeBody = F("BRJ_Bug_Charge_Body"); chargeFx = F("BRJ_Bug_Charge_Effect");
        whirlBegin = F("BRJ_Bug_WhirlBegin_Body"); whirlBody = F("BRJ_Bug_WhirlLoop_Body"); whirlFx = F("BRJ_Bug_WhirlLoop_Effect"); whirlEnd = F("BRJ_Bug_WhirlEnd_Body");
        spawn = F("BRJ_Bug_SpawnInsect"); heal = F("BRJ_Bug_Heal"); death = F("BRJ_Bug_Death");
    }

    private static Sprite[] F(string name) => GraftFX.Frames(name, Feet) ?? new Sprite[0];

    private void Start()
    {
        if (arena != null) { left = arena.leftX + 1f; right = arena.rightX - 1f; floorY = arena.floorY; }
        else { left = transform.position.x - 8f; right = transform.position.x + 8f; floorY = transform.position.y; }
        transform.position = new Vector3(transform.position.x, floorY, 0f);
        SoundtrackManager.Override(Resources.Load<AudioClip>("Sounds/StephmossTheme"), 1f);
        Banner.Show("STEPHMOSS", "LORD OF SEVERED WOODS", Toxic, 2.4f);
        StartCoroutine(Brain());
    }

    private void OnDestroy() => SoundtrackManager.Override(null);

    private Vector3 Rowdy => BoonRunner.Rowdy != null ? BoonRunner.RowdyCenter : transform.position;
    private float Facing => facingRight ? 1f : -1f;

    private void Face(float x) { facingRight = x > transform.position.x; }

    private void Play(Sprite[] frames, float framesPerSecond, bool loop)
    {
        if (frames == null || frames.Length == 0) frames = idle;
        current = frames; fps = framesPerSecond; loopAnim = loop; frameT = 0f;
    }

    private void LateUpdate()
    {
        if (current == null || current.Length == 0) return;
        frameT += Time.deltaTime * fps;
        int f = loopAnim ? (int)frameT % current.Length : Mathf.Min((int)frameT, current.Length - 1);
        body.sprite = current[f];
        body.flipX = !facingRight; // the art faces right
        fx.flipX = body.flipX;
        transform.position = new Vector3(Mathf.Round(transform.position.x * 64f) / 64f, floorY, 0f);
    }

    private bool AnimDone => !loopAnim && current != null && frameT >= current.Length;

    // ---------------------------------------------------------------- the fight
    private IEnumerator Brain()
    {
        Play(appear, 14f, false);
        FXSound.Play("StephmossWhirl", 0.7f, 0.9f);
        GraftFX.Play("Steph_BounceVFx", transform.position, Color.white, 22, null, false, false, 1f, new Vector2(0.5f, 0.05f));
        ScreenShake.Impulse(0.8f);
        yield return new WaitForSeconds(1f);
        int last = -1;
        while (!health.enemydead)
        {
            if (!molted && health.currentenemyHealth < health.startingenemyHealth * 0.4f) { yield return Molt(); continue; }
            if (StatusEffects.IsStunned(gameObject)) { Play(idle, 3f, true); yield return null; continue; }
            Play(idle, 6f, true);
            Face(Rowdy.x);
            yield return new WaitForSeconds(Mathf.Lerp(0.5f, 0.9f, health.currentenemyHealth / health.startingenemyHealth));
            int pick;
            do pick = Random.Range(0, 4); while (pick == last && Random.value < 0.75f);
            last = pick;
            switch (pick)
            {
                case 0: yield return Charge(); break;
                case 1: yield return Whirlwind(); break;
                case 2: yield return Swarm(); break;
                default: yield return Blink(); break;
            }
        }
        yield return Die();
    }

    private IEnumerator Charge()
    {
        Face(Rowdy.x);
        // wind-up: the first frames of her charge, flashing so it reads
        Play(chargeBody, 10f, false);
        FXSound.Play("StephmossScissor", 0.6f, 0.8f);
        for (float t = 0f; t < 0.75f; t += Time.deltaTime)
        {
            if (Mathf.Repeat(t, 0.2f) < 0.06f) HitFlash.Flash(this, 0.05f, Toxic);
            frameT = Mathf.Min(frameT, 7f);
            yield return null;
        }
        float dir = Facing, speed = 15f;
        float end = dir > 0f ? right : left;
        var hit = new HashSet<Health>();
        fx.sprite = null;
        int fxFrame = 0;
        while ((end - transform.position.x) * dir > 0.05f && !health.enemydead)
        {
            transform.position += new Vector3(dir * speed * Time.deltaTime, 0f, 0f);
            if ((end - transform.position.x) * dir < 0f) transform.position = new Vector3(end, floorY, 0f);
            frameT = Mathf.Clamp(frameT, 9f, chargeBody.Length - 1);
            if (chargeFx.Length > 0) fx.sprite = chargeFx[Mathf.Clamp(9 + (fxFrame++ / 3) % 6, 0, chargeFx.Length - 1)];
            CatFX.Afterimage(body, new Color(Toxic.r, Toxic.g, Toxic.b, 0.45f), 0.2f);
            HurtRowdyIn(transform.position + Vector3.up * 0.8f, new Vector2(1.6f, 1.4f), 24f, hit);
            yield return null;
        }
        fx.sprite = null;
        GraftFX.Play("Steph_BounceVFx", transform.position, Color.white, 22, null, false, dir < 0f, 1.3f, new Vector2(0.5f, 0.05f));
        ScreenShake.Impulse(0.5f);
        FXSound.Play("RockSmash", 0.4f, 1.1f);
        yield return new WaitForSeconds(0.35f);
    }

    private IEnumerator Whirlwind()
    {
        Play(whirlBegin, 14f, false);
        FXSound.Play("StephmossWhirl", 0.7f, 1f);
        yield return new WaitForSeconds(whirlBegin.Length / 14f);
        Play(whirlBody, 20f, true);
        float tick = 0f, t = 0f;
        while (t < 2.4f && !health.enemydead)
        {
            t += Time.deltaTime;
            float x = Mathf.MoveTowards(transform.position.x, Mathf.Clamp(Rowdy.x, left, right), 3.2f * Time.deltaTime);
            transform.position = new Vector3(x, floorY, 0f);
            if (whirlFx.Length > 0) fx.sprite = whirlFx[(int)(t * 20f) % whirlFx.Length];
            tick -= Time.deltaTime;
            if (tick <= 0f)
            {
                tick = 0.4f;
                HurtRowdyIn(transform.position + Vector3.up * 0.9f, new Vector2(3f, 2f), 10f, null);
                if (Random.value < 0.5f) FXSound.Play("StephmossScissor", 0.3f, Random.Range(1.2f, 1.4f));
            }
            yield return null;
        }
        fx.sprite = null;
        Play(whirlEnd, 14f, false);
        yield return new WaitForSeconds(whirlEnd.Length / 14f);
    }

    private IEnumerator Swarm()
    {
        Face(Rowdy.x);
        Play(spawn, 12f, false);
        yield return new WaitForSeconds(0.45f);
        int n = health.currentenemyHealth < health.startingenemyHealth * 0.5f ? 5 : 3;
        for (int i = 0; i < n; i++)
            HostileInsect.Launch(transform.position + new Vector3(Facing * 0.6f, 1.4f + i * 0.15f, 0f), 9f);
        FXSound.Play("StephmossScissor", 0.4f, 1.8f);
        yield return new WaitForSeconds(Mathf.Max(0.2f, spawn.Length / 12f - 0.45f));
    }

    private IEnumerator Blink()
    {
        Play(vanish, 16f, false);
        FXSound.Play("SmokePoof", 0.6f, 1f);
        yield return new WaitForSeconds(vanish.Length / 16f);
        float side = Random.value < 0.5f ? -1f : 1f;
        float x = Mathf.Clamp(Rowdy.x + side * 2.6f, left, right);
        if (Mathf.Abs(x - Rowdy.x) < 1.5f) x = Mathf.Clamp(Rowdy.x - side * 2.6f, left, right);
        transform.position = new Vector3(x, floorY, 0f);
        Face(Rowdy.x);
        Play(appear, 18f, false);
        GraftFX.Play("BRJ_WeirdTeleport", transform.position + Vector3.up * 0.6f, Toxic, 22);
        yield return new WaitForSeconds(appear.Length / 18f);
        yield return Charge();
    }

    private IEnumerator Molt()
    {
        molted = true;
        Play(heal, 6f, true);
        Banner.Show("MOLT!", "HIT HER HARD TO CUT IT SHORT", Toxic, 1.2f);
        FXSound.Play("StephmossWhirl", 0.5f, 1.4f);
        float start = health.currentenemyHealth;
        for (float t = 0f; t < 3f && !health.enemydead; t += Time.deltaTime)
        {
            if (health.currentenemyHealth < start - health.startingenemyHealth * 0.06f) break; // hit hard enough: interrupted
            health.AddHealthEnemy(health.startingenemyHealth * 0.05f * Time.deltaTime);
            start = Mathf.Max(start, health.currentenemyHealth - 1f);
            if (Random.value < 0.4f) FXParticle.Burst(transform.position + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 1.6f), 0f), Toxic, 1, 0.2f, 0.6f, -2f, 0.6f);
            yield return null;
        }
    }

    private IEnumerator Die()
    {
        if (dying) yield break;
        dying = true;
        fx.sprite = null;
        SoundtrackManager.Override(null);
        Play(death, 10f, false);
        TimeSlowController.SlowMotion(1.2f, 0.25f);
        ScreenShake.Impulse(1f);
        FXSound.Play("StephmossHit", 0.9f, 0.7f);
        Banner.Show("STEPHMOSS FALLS", "THE WOODS GO QUIET", Toxic, 2.6f);
        PulseRing.Spawn(transform.position + Vector3.up, Toxic, 4f, 0.6f);
        yield return new WaitForSeconds(death.Length / 10f + 0.6f);
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- hurting Rowdy
    private void HurtRowdyIn(Vector3 center, Vector2 size, float damage, HashSet<Health> once)
    {
        if (BoonRunner.Rowdy == null) return;
        if (rowdyHealth == null || rowdyHealth.transform != BoonRunner.Rowdy) rowdyHealth = BoonRunner.Rowdy.GetComponent<Health>();
        if (rowdyHealth == null) return;
        Vector3 r = BoonRunner.RowdyCenter;
        if (Mathf.Abs(r.x - center.x) > size.x / 2f || Mathf.Abs(r.y - center.y) > size.y / 2f) return;
        if (once != null && !once.Add(rowdyHealth)) return;
        rowdyHealth.TakeDamage(damage);
        GraftFX.Play("StephHit", r, Color.white, 97, null, false, Random.value < 0.5f);
    }
}

// The insects Stephmoss throws: buzz up, then dive at Rowdy. Touch = damage. One hit kills them.
public class HostileInsect : MonoBehaviour
{
    private Sprite[] frames;
    private SpriteRenderer sr;
    private float age, damage;
    private Vector3 velocity;

    public static void Launch(Vector3 at, float damage)
    {
        var go = new GameObject("Stephmoss Insect");
        go.transform.position = at;
        go.tag = "Enemy";
        var i = go.AddComponent<HostileInsect>();
        i.damage = damage;
        i.frames = GraftFX.Frames("BRJ_Insect");
        i.sr = go.AddComponent<SpriteRenderer>();
        i.sr.sortingOrder = 96;
        if (ItemArt.Lit != null) i.sr.sharedMaterial = ItemArt.Lit;
        i.velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 5f), 0f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (frames != null && frames.Length > 0) sr.sprite = frames[(int)(age * 12f) % frames.Length];
        if (age > 5f) { Pop(); return; }
        Vector3 target = BoonRunner.Rowdy != null ? BoonRunner.RowdyCenter : transform.position;
        Vector3 want = age < 0.6f ? velocity : (target - transform.position).normalized * 5.5f;
        velocity = Vector3.MoveTowards(velocity, want, 12f * Time.deltaTime);
        transform.position += velocity * Time.deltaTime;
        sr.flipX = velocity.x < 0f;
        if (BoonRunner.Rowdy != null && (target - transform.position).sqrMagnitude < 0.12f)
        {
            if (BoonRunner.Rowdy.TryGetComponent(out Health h)) h.TakeDamage(damage);
            Pop();
            return;
        }
        // Rowdy's swing swats them
        foreach (Collider2D c in Physics2D.OverlapCircleAll(transform.position, 0.2f))
            if (c.GetComponent<PlayerDamage>() != null) { Pop(); return; }
    }

    private void Pop()
    {
        GraftFX.Play("BRJ_Insect_Death", transform.position, Color.white, 96);
        Destroy(gameObject);
    }
}
