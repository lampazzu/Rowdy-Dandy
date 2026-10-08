using System.Collections;
using UnityEngine;

// FLYING RAT (PIV_Flying_Rat art) - very rare drop from enemies. It crackles into existence, flutters around for
// a while and then escapes; touch it to REGISTER it. Every registered rat is bait that keeps one cat from getting
// lost when Rowdy dies: with 2 rats and 2 cats you never lose a cat, only a 3rd one would be at risk (CatRoster).
public class FlyingRat : Pickup
{
    private const float PixelsPerUnit = 64f;
    private const float Lifetime = 24f;

    private Sprite[] frames;
    private Vector3 home;
    private float t, phase;

    public static void Spawn(Vector3 at)
    {
        ItemArt art = ItemArt.Get;
        if (art == null || art.flyingRat == null) return;
        var go = new GameObject("Flying Rat");
        go.transform.position = at + Vector3.up * 0.5f;
        var rat = go.AddComponent<FlyingRat>();
        rat.frames = ItemArt.Frames(art.flyingRat, 10, 2, new Vector2(0.5f, 0.3f), PixelsPerUnit);
        rat.sprite = MakeRenderer(go, rat.frames[3], 64);
        rat.flies = true;
        rat.magnetRadius = 0.9f;
        rat.home = go.transform.position;
        rat.phase = Random.Range(0f, 10f);
        IconPopup.Show(go.transform.position + Vector3.up * 0.5f, rat.frames[0], "A RAT!", new Color(0.75f, 0.9f, 1f), 0.9f, 1.4f);
    }

    protected override bool CanBeCollected() => t > 0.6f;

    protected override void Update()
    {
        t += Time.deltaTime;

        // 0-0.6s: zaps in (frames 3-5), then flaps (0,1,2,1); blinks out at the end of its life
        int f = t < 0.6f ? 3 + Mathf.Min(2, (int)(t / 0.2f)) : new[] { 0, 1, 2, 1 }[(int)(t * 10f) % 4];
        if (sprite != null)
        {
            sprite.sprite = frames[f];
            sprite.enabled = t < Lifetime - 4f || Mathf.Repeat(t * 8f, 1f) < 0.6f;
        }
        if (t > Lifetime) { Destroy(gameObject); return; }

        // Lazy figure-8 around where it appeared, slowly drifting up and away
        Vector3 wander = new Vector3(Mathf.Sin(t * 1.3f + phase) * 1.4f, Mathf.Sin(t * 2.6f + phase) * 0.35f + t * 0.02f, 0f);
        Vector3 before = transform.position;
        transform.position = Vector3.Lerp(transform.position, home + wander, Time.deltaTime * 3f);
        if (sprite != null && Mathf.Abs(transform.position.x - before.x) > 0.001f) sprite.flipX = transform.position.x > before.x;

        base.Update(); // homes in on Rowdy once he's right next to it
    }

    protected override void OnCollected()
    {
        CatRoster.AddRat();
        int rats = CatRoster.Rats;
        IconPopup.Show(transform.position + Vector3.up * 0.4f, frames[0], "RAT REGISTERED (" + rats + ")", new Color(0.55f, 0.95f, 1f), 1f, 2f);
        GemChime.Play(EXPGem.SharedCollectSound, 0.9f);
        RowdyNotes.MarkTopicNews("flyingrat");

        // The burst (bottom row of the sheet)
        var go = new GameObject("Rat Burst");
        go.transform.position = transform.position;
        MakeRenderer(go, frames[10], 65);
        go.AddComponent<FramePlayer>().Play(frames, 10, 10, 18f);
    }
}

// CAT TREAT - a tiny fish, very rare drop. Flops on the ground; picking it up feeds every cat Rowdy has:
// cooldowns refill at once and stay halved for a while. No cats? Then it's a snack (+5 HP).
public class CatTreat : Pickup
{
    private Sprite[] frames;
    private float t;

    public static void Spawn(Vector3 at)
    {
        ItemArt art = ItemArt.Get;
        if (art == null || art.fish == null) return;
        var go = new GameObject("Cat Treat Fish");
        go.transform.position = at + Vector3.up * 0.3f;
        var fish = go.AddComponent<CatTreat>();
        fish.frames = ItemArt.Frames(art.fish, Mathf.Max(1, art.fishFrames), 1, new Vector2(0.5f, 0.5f), 64f);
        fish.sprite = MakeRenderer(go, fish.frames[0], 62);
        fish.magnetRadius = 1.8f;
        fish.Launch(new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(4f, 5.5f)));
    }

    protected override void OnBounce() => t = 0f;

    protected override void Update()
    {
        t += Time.deltaTime;
        if (sprite != null && frames.Length > 0)
        {
            sprite.sprite = frames[(int)(t * 6f) % frames.Length]; // flop flop
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 14f) * 12f);
        }
        base.Update();
    }

    protected override void OnCollected()
    {
        int fed = 0;
        foreach (PetFollower cat in PetFollower.Pets)
            if (cat != null && cat.IsCollected) { cat.Treat(); fed++; }

        Sprite icon = frames.Length > 0 ? frames[0] : null;
        if (fed > 0) IconPopup.Show(transform.position + Vector3.up * 0.3f, icon, fed > 1 ? "CATS FED!" : "CAT FED!", new Color(1f, 0.75f, 0.4f), 1f, 1.6f);
        else
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null && p.TryGetComponent(out Health h)) h.AddHealth(5f);
            IconPopup.Show(transform.position + Vector3.up * 0.3f, icon, "SNACK +5 HP", new Color(1f, 0.75f, 0.4f), 1f, 1.4f);
        }
        GemChime.Play(EXPGem.SharedCollectSound, 0.8f);
        RowdyNotes.MarkTopicNews("cattreat");
    }
}

// Plays frames[from .. from+count-1] once and destroys itself
public class FramePlayer : MonoBehaviour
{
    public void Play(Sprite[] frames, int from, int count, float fps) => StartCoroutine(Run(frames, from, count, fps));

    private IEnumerator Run(Sprite[] frames, int from, int count, float fps)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        for (int i = 0; i < count && from + i < frames.Length; i++)
        {
            if (sr != null) sr.sprite = frames[from + i];
            yield return new WaitForSeconds(1f / fps);
        }
        Destroy(gameObject);
    }
}
