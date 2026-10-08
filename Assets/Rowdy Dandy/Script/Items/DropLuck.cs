using System.Collections.Generic;
using UnityEngine;

// Drop luck: every ore gem Rowdy picks up raises it a little. It multiplies the chance of weapon drops and of the
// rare spawns (flying rat, cat treat fish). Saved in PlayerPrefs (RD_DropLuck), wiped by the dev reset.
public static class DropLuck
{
    private const string Key = "RD_DropLuck";
    public const float Max = 1f; // at most +100%

    public static float Bonus => Mathf.Clamp(PlayerPrefs.GetFloat(Key, 0f), 0f, Max);   // 0.05 = +5%
    public static float Multiplier => 1f + Bonus;

    public static void Add(float amount)
    {
        PlayerPrefs.SetFloat(Key, Mathf.Clamp(Bonus + amount, 0f, Max));
        PlayerPrefs.Save();
    }

    public static void Reset() => PlayerPrefs.DeleteKey(Key);

    public static bool Roll(float percent) => Random.Range(0f, 100f) < percent * Multiplier;
}

// The "ding ding ding" of pickups: each one collected soon after the last plays a step higher (major pentatonic),
// so a stream of gems climbs a little melody. Shared by EXP gems and ore gems.
public static class GemChime
{
    private static readonly int[] Scale = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };
    private const float StreakWindow = 0.55f;

    private static AudioSource[] sources;
    private static int next, step;
    private static float lastAt = -10f;

    public static void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (sources == null || sources[0] == null)
        {
            var go = new GameObject("GemChime (auto)");
            Object.DontDestroyOnLoad(go);
            sources = new AudioSource[6];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = go.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
        }

        step = Time.unscaledTime - lastAt < StreakWindow ? Mathf.Min(step + 1, Scale.Length - 1) : 0;
        lastAt = Time.unscaledTime;

        AudioSource s = sources[next];
        next = (next + 1) % sources.Length;
        s.pitch = Mathf.Pow(2f, Scale[step] / 12f);
        s.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }
}

// Rare things an enemy can drop when Rowdy (or a cat) kills it. Called from EnemyHealth on death.
public static class RareDrops
{
    public const float RatChance = 0.8f;    // % per kill, x drop luck
    public const float FishChance = 1.2f;

    public static void OnEnemyKilled(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsObject) return;
        Vector3 at = EnemyFairness.BodyCenter(enemy);
        if (DropLuck.Roll(RatChance)) FlyingRat.Spawn(at);
        else if (DropLuck.Roll(FishChance)) CatTreat.Spawn(at);
    }
}

// Shared pickup motion: pops out in an arc (fountain), bounces once on the ground, bobs, then homes in on Rowdy
// when he's close. Subclasses react in OnCollected.
public abstract class Pickup : MonoBehaviour
{
    protected SpriteRenderer sprite;
    protected Transform rowdy;
    protected float magnetRadius = 2.2f;
    protected float gravity = 16f;
    protected bool flies;                     // the rat flies: no gravity / ground

    private Vector2 velocity;
    private bool landed, magnet, collected;
    private int bounces;
    private float age, baseY, speed, groundY = float.NegativeInfinity;

    protected void Launch(Vector2 initialVelocity)
    {
        velocity = initialVelocity;
        if (!flies) FindGround();
    }

    private void FindGround()
    {
        foreach (RaycastHit2D hit in Physics2D.RaycastAll((Vector2)transform.position + Vector2.up * 0.3f, Vector2.down, 30f))
        {
            Collider2D c = hit.collider;
            if (c == null || c.isTrigger) continue;
            if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
            groundY = hit.point.y + 0.12f;
            return;
        }
    }

    protected virtual void Update()
    {
        age += Time.deltaTime;
        if (rowdy == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) rowdy = p.transform;
        }

        if (!flies && !landed)
        {
            velocity.y -= gravity * Time.deltaTime;
            Vector3 pos = transform.position + (Vector3)(velocity * Time.deltaTime);
            if (velocity.y < 0f && pos.y <= groundY)
            {
                pos.y = groundY;
                if (bounces == 0 && velocity.y < -2f) { velocity = new Vector2(velocity.x * 0.5f, -velocity.y * 0.35f); bounces++; OnBounce(); }
                else { landed = true; baseY = pos.y; velocity = Vector2.zero; }
            }
            transform.position = pos;
        }

        if (rowdy != null && CanBeCollected() && (magnet || (age > 0.5f && Vector2.Distance(transform.position, rowdy.position) < magnetRadius)))
        {
            magnet = true;
            speed += 30f * Time.deltaTime;
            transform.position = Vector2.MoveTowards(transform.position, rowdy.position + Vector3.up * 0.2f, (2f + speed) * Time.deltaTime);
            if (!collected && Vector2.Distance(transform.position, rowdy.position + Vector3.up * 0.2f) < 0.25f) Collect();
        }
        else if (landed && !flies)
        {
            Vector3 p = transform.position;
            p.y = baseY + Mathf.Abs(Mathf.Sin(age * 3f)) * 0.06f;
            transform.position = p;
        }
    }

    private void Collect()
    {
        collected = true;
        OnCollected();
        Destroy(gameObject);
    }

    protected virtual bool CanBeCollected() => true;
    protected virtual void OnBounce() { }
    protected abstract void OnCollected();

    protected static SpriteRenderer MakeRenderer(GameObject go, Sprite s, int order = 60, bool litByScene = false)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = order;
        Material m = litByScene ? ItemArt.Lit : ItemArt.Unlit;
        if (m != null) sr.sharedMaterial = m;
        return sr;
    }
}
