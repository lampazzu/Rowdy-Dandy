using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ore rocks (DMG_OreBreak art). Three hits from Rowdy's weapon (or a cat) break one: it crumbles and a fountain
// of gems pops out - emerald, sapphire, ruby, rarely crystal. Each gem picked up raises the drop luck (DropLuck).
//  - Every level gets ItemArt.oresPerLevel ores scattered on the ground at fixed, seeded spots; a broken ore stays
//    broken (PlayerPrefs RD_Ore_*), so the luck you can farm is limited.
//  - You can also hand-place one: add an OreNode component to an empty GameObject where you want it.
public class OreNode : MonoBehaviour
{
    private const float PixelsPerUnit = 100f;
    private const int HitsToBreak = 3;

    [Tooltip("Hand-placed ores: leave empty, the save id comes from the position.")]
    public string saveId;

    private SpriteRenderer body, flash;
    private BoxCollider2D box;
    private int hits;
    private bool broken;
    private float shake, flashAlpha, lastHit = -10f;
    private Vector3 home;

    private string Key => "RD_Ore_" + SceneManager.GetActiveScene().name + "_" +
                          (string.IsNullOrEmpty(saveId) ? Mathf.RoundToInt(transform.position.x * 10f) + "_" + Mathf.RoundToInt(transform.position.y * 10f) : saveId);

    private void Start()
    {
        ItemArt art = ItemArt.Get;
        Sprite[] frames = art != null ? ItemArt.Frames(art.oreBreak, 10, 1, new Vector2(0.5f, 0.1f), PixelsPerUnit) : null;
        if (frames == null || PlayerPrefs.GetInt(Key, 0) == 1) { Destroy(gameObject); return; }
        Remember(Key);
        home = transform.position;

        var bodyGo = new GameObject("Ore");
        bodyGo.transform.SetParent(transform, false);
        body = bodyGo.AddComponent<SpriteRenderer>();
        body.sprite = frames[0];
        body.sortingLayerName = "Default";
        body.sortingOrder = 5;
        if (ItemArt.Lit != null) body.sharedMaterial = ItemArt.Lit;

        var flashGo = new GameObject("Hit Flash");
        flashGo.transform.SetParent(bodyGo.transform, false);
        flash = flashGo.AddComponent<SpriteRenderer>();
        flash.sprite = frames[0];
        flash.sortingLayerName = "Default";
        flash.sortingOrder = 6;
        if (CatFX.Silhouette != null) flash.sharedMaterial = CatFX.Silhouette;
        flash.color = new Color(1f, 1f, 1f, 0f);

        box = gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.75f, 0.7f);
        box.offset = new Vector2(0f, 0.38f);
    }

    private static void Remember(string key)
    {
        string list = PlayerPrefs.GetString("RD_OreList", "");
        if (!list.Contains(key + ";")) PlayerPrefs.SetString("RD_OreList", list + key + ";");
    }

    // Dev reset: every ore key ever seen
    public static void ForgetAllSaved()
    {
        foreach (string k in PlayerPrefs.GetString("RD_OreList", "").Split(';')) if (k.Length > 0) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.DeleteKey("RD_OreList");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (broken || Time.time - lastHit < 0.12f) return;
        if (other.GetComponent<PlayerDamage>() == null) return; // Rowdy's (or Wig's) attack hitboxes
        lastHit = Time.time;
        hits++;
        shake = 1f;
        flashAlpha = 0.85f;
        TimeSlowController.HitStop(0.04f, 0.1f);
        ScreenShake.Impulse(0.15f);
        Chips(6);
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySound("QuebraTudo");
        if (hits >= HitsToBreak) StartCoroutine(Break());
    }

    private void Update()
    {
        if (body == null) return;
        shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 6f);
        float s = shake * 0.05f;
        transform.position = home + new Vector3(Mathf.Round(Random.Range(-s, s) * 64f) / 64f, 0f, 0f);
        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.deltaTime * 6f);
        if (flash != null) flash.color = new Color(1f, 0.95f, 1f, flashAlpha);
    }

    private IEnumerator Break()
    {
        broken = true;
        RowdyNotes.MarkTopicNews("ores");
        PlayerPrefs.SetInt(Key, 1);
        PlayerPrefs.Save();
        if (box != null) box.enabled = false;
        ScreenShake.Impulse(0.35f);
        Chips(14);

        // The gem fountain
        int count = Random.Range(3, 6);
        for (int i = 0; i < count; i++)
        {
            OreGem.Spawn(home + Vector3.up * 0.4f, RandomKind(), i);
        }
        HairGel.TryDrop(home + Vector3.up * 0.5f); // super rare boon reroll (2% .. 10% with luck)

        Sprite[] frames = ItemArt.Frames(ItemArt.Get.oreBreak, 10, 1, new Vector2(0.5f, 0.1f), PixelsPerUnit);
        for (int f = 1; f < frames.Length; f++)
        {
            body.sprite = frames[f];
            if (flash != null) flash.enabled = false;
            yield return new WaitForSeconds(1f / 14f);
        }
        float t = 0f;
        while (t < 0.6f) { t += Time.deltaTime; body.color = new Color(1f, 1f, 1f, 1f - t / 0.6f); yield return null; }
        Destroy(gameObject);
    }

    private static OreGem.Kind RandomKind()
    {
        float r = Random.value;
        return r < 0.05f ? OreGem.Kind.Crystal : r < 0.2f ? OreGem.Kind.Ruby : r < 0.5f ? OreGem.Kind.Sapphire : OreGem.Kind.Emerald;
    }

    // Purple-grey rock chips
    private void Chips(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Ore Chip");
            go.transform.position = home + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.2f, 0.6f), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = OverlayUI.WhiteSprite;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 7;
            sr.color = Random.value < 0.5f ? new Color(0.35f, 0.25f, 0.4f) : new Color(0.65f, 0.5f, 0.75f);
            go.transform.localScale = Vector3.one * Random.Range(0.8f, 1.5f); // the white sprite is 4 px at 100 ppu
            go.AddComponent<Chip>().velocity = new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(2f, 4.5f));
        }
    }

    private class Chip : MonoBehaviour
    {
        public Vector2 velocity;
        private float age;
        private void Update()
        {
            age += Time.deltaTime;
            velocity.y -= 14f * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            if (age > 0.6f) Destroy(gameObject);
        }
    }
}

// A gem from a broken ore: fountain out, bounce, bob; collected = +drop luck, a "+1% LUCK" popup and a chime that
// climbs with each gem in a row.
public class OreGem : Pickup
{
    public enum Kind { Emerald, Sapphire, Ruby, Crystal }

    private Kind kind;

    private static float LuckOf(Kind k) => k == Kind.Crystal ? 0.015f : k == Kind.Ruby ? 0.01f : k == Kind.Sapphire ? 0.0075f : 0.005f;
    private static Color ColorOf(Kind k) => k == Kind.Crystal ? new Color(0.85f, 0.95f, 1f) : k == Kind.Ruby ? new Color(1f, 0.35f, 0.4f) : k == Kind.Sapphire ? new Color(0.45f, 0.65f, 1f) : new Color(0.45f, 1f, 0.55f);

    private static Texture2D TextureOf(Kind k)
    {
        ItemArt a = ItemArt.Get;
        if (a == null) return null;
        return k == Kind.Crystal ? a.crystals : k == Kind.Ruby ? a.ruby : k == Kind.Sapphire ? a.sapphire : a.emerald;
    }

    public static void Spawn(Vector3 at, Kind kind, int index)
    {
        var go = new GameObject("Ore Gem " + kind);
        go.transform.position = at;
        var gem = go.AddComponent<OreGem>();
        gem.kind = kind;
        gem.sprite = MakeRenderer(go, ItemArt.Single(TextureOf(kind), 96f), 62);
        float side = (index % 2 == 0 ? 1f : -1f) * Random.Range(0.4f, 1.8f);
        gem.Launch(new Vector2(side, Random.Range(4.5f, 6.5f)));
    }

    protected override void Update()
    {
        base.Update();
        if (sprite != null)
        {
            float tw = 0.85f + 0.15f * Mathf.Sin(Time.time * 9f + transform.position.x * 3f);
            sprite.color = new Color(tw, tw, tw, 1f); // twinkle
        }
    }

    protected override void OnCollected()
    {
        float luck = LuckOf(kind);
        DropLuck.Add(luck);
        GemChime.Play(EXPGem.SharedCollectSound, 0.8f);
        IconPopup.Show(transform.position + Vector3.up * 0.3f, sprite != null ? sprite.sprite : null,
            "+" + (luck * 100f).ToString("0.#") + "% LUCK", ColorOf(kind), 0.8f, 1f);
    }
}

// Places the level's ores: random-but-fixed (seeded by the scene) spots on top of solid ground, away from water,
// spawners and Pelich's arena, with room above. Runs on every scene load.
public static class OreScatter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        Scatter();
        SceneManager.sceneLoaded += (s, m) => Scatter();
    }

    // string.GetHashCode isn't stable between runs on every platform; this is
    private static int StableHash(string s)
    {
        unchecked { int h = 17; foreach (char c in s) h = h * 31 + c; return h; }
    }

    private static void Scatter()
    {
        ItemArt art = ItemArt.Get;
        if (art == null || !art.scatterOres || art.oresPerLevel <= 0) return;

        // Level extent = the ground colliders
        int groundLayer = LayerMask.NameToLayer("Ground");
        bool any = false;
        Bounds level = default;
        foreach (CompositeCollider2D c in Object.FindObjectsByType<CompositeCollider2D>(FindObjectsSortMode.None))
        {
            if (c.gameObject.layer != groundLayer && !c.CompareTag("Ground")) continue;
            if (!any) { level = c.bounds; any = true; } else level.Encapsulate(c.bounds);
        }
        if (!any) return;

        float? pelichX = null;
        foreach (EnemyHealth e in Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            EnemyCatalog.Entry entry = EnemyCatalog.Identify(e);
            if (entry != null && entry.id == "pelich") { pelichX = e.transform.position.x; break; }
        }

        var rng = new System.Random(StableHash(SceneManager.GetActiveScene().name));
        var placed = new List<Vector2>();
        int attempts = 0;
        while (placed.Count < art.oresPerLevel && attempts++ < art.oresPerLevel * 40)
        {
            float x = Mathf.Lerp(level.min.x + 4f, level.max.x - 4f, (float)rng.NextDouble());
            RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(x, level.max.y + 2f), Vector2.down, level.size.y + 4f);
            var surfaces = new List<RaycastHit2D>();
            foreach (RaycastHit2D h in hits)
            {
                if (h.collider == null || h.collider.isTrigger || h.normal.y < 0.85f) continue;
                if (h.collider.attachedRigidbody != null && h.collider.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
                surfaces.Add(h);
            }
            if (surfaces.Count == 0) continue;
            RaycastHit2D pick = surfaces[rng.Next(surfaces.Count)];
            if (pick.collider.CompareTag("Water") || pick.collider.gameObject.layer == 6) continue;
            if (pick.collider.gameObject.layer != groundLayer && !pick.collider.CompareTag("Ground")) continue;
            Vector2 spot = pick.point;

            // room above, not inside other ground, nothing else close
            Collider2D lid = Physics2D.OverlapBox(spot + new Vector2(0f, 0.75f), new Vector2(0.8f, 1.1f), 0f, 1 << groundLayer);
            if (lid != null && !lid.isTrigger) continue;
            if (RespawnTrigger.IsNear(spot, 2f)) continue;
            bool crowded = false;
            foreach (Vector2 p in placed) if (Vector2.Distance(p, spot) < 8f) { crowded = true; break; }
            if (crowded) continue;
            if (pelichX.HasValue && Mathf.Abs(spot.x - pelichX.Value) < 16f) continue;

            placed.Add(spot);
            var go = new GameObject("Ore (scattered)");
            go.transform.position = new Vector3(spot.x, spot.y - 0.02f, 0f);
            go.AddComponent<OreNode>().saveId = "s" + placed.Count;
        }
    }
}
