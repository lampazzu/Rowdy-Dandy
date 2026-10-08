using UnityEngine;

// HAIR GEL: a super rare drop from ore rocks = one reroll of the boon cards. The chance grows with the drop luck
// from ore gems, from 2% per ore up to 10% at the most luck (DropLuck.Max). Saved with the boons (RD_BoonRerolls).
public class HairGel : Pickup
{
    public const float BaseChance = 2f, MaxChance = 10f;   // percent per broken ore

    private float sparkleTimer;
    private SpriteOutline outline;

    public static float Chance => Mathf.Lerp(BaseChance, MaxChance, Mathf.Clamp01(DropLuck.Bonus / DropLuck.Max));

    // Called by OreNode when it breaks
    public static void TryDrop(Vector3 at)
    {
        if (Random.Range(0f, 100f) >= Chance) return;
        Spawn(at);
    }

    public static void Spawn(Vector3 at)
    {
        var go = new GameObject("Hair Gel");
        go.transform.position = at;
        var gel = go.AddComponent<HairGel>();
        gel.sprite = MakeRenderer(go, JarSprite, 63);
        gel.sprite.transform.localScale = Vector3.one * 1.5f;
        gel.outline = SpriteOutline.Add(gel.sprite, Color.white, 1, -1);
        gel.Launch(new Vector2(Random.Range(-1f, 1f), 7f));
        BoonFX.Popup(at + Vector3.up * 0.8f, "HAIR GEL?!", new Color(0.6f, 1f, 0.9f), 0.9f, 1.4f);
        PulseRing.Spawn(at, new Color(0.6f, 1f, 0.9f, 1f), 1.4f, 0.4f);
        FXSound.Play("Legendary", 0.8f, 1.2f);
    }

    protected override void Update()
    {
        base.Update();
        if (sprite == null) return;
        float t = Time.time;
        if (outline != null) outline.color = BoonFX.Rainbow(t * 0.8f);
        sprite.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 4f) * 8f);
        sparkleTimer -= Time.deltaTime;
        if (sparkleTimer <= 0f)
        {
            sparkleTimer = 0.12f;
            BoonFX.Sparkles(transform.position + (Vector3)Random.insideUnitCircle * 0.25f, BoonFX.Rainbow(t, Random.value), 1, 0.05f, 0.5f);
        }
    }

    protected override void OnCollected()
    {
        Boons.AddRerolls(1);
        RowdyNotes.MarkTopicNews("hairgel");
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.gel : null, 0.8f, 1.1f);
        FXSound.Play("Legendary", 0.6f, 1.4f);
        IconPopup.Show(transform.position + Vector3.up * 0.4f, JarSprite, "HAIR GEL! +1 REROLL", new Color(0.6f, 1f, 0.9f), 1f, 2f);
        BoonFX.Sparkles(transform.position, new Color(0.6f, 1f, 0.9f), 14, 0.5f, 0.8f);
        PulseRing.Spawn(transform.position, new Color(1f, 0.6f, 0.9f, 1f), 1.6f, 0.35f);
        TimeSlowController.HitStop(0.08f, 0.1f);
    }

    public static Sprite JarSprite => BoonFX.FromRows("HairGelJar", new[]
    {
        "..kkkkkk..",
        ".kssssssk.",
        ".kwsssssk.",
        "kkkkkkkkkk",
        "kwccccccck",
        "kwcppppcck",
        "kccpwppcck",
        "kccppppcck",
        "kccccccddk",
        "kdcccccddk",
        ".kkkkkkkk.",
    }, ch =>
    {
        switch (ch)
        {
            case 'k': return new Color32(27, 8, 32, 255);
            case 's': return new Color32(205, 205, 220, 255);
            case 'w': return new Color32(255, 255, 255, 255);
            case 'c': return new Color32(110, 240, 220, 255);
            case 'd': return new Color32(50, 150, 150, 255);
            case 'p': return new Color32(255, 110, 200, 255);
        }
        return new Color32(255, 255, 255, 255);
    }, new Vector2(0.5f, 0.5f), 64f);
}
