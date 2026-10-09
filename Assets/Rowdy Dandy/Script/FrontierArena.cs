using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// THE FRONTIER: the island past Pelich, and its colosseum (Hollow Knight's Colosseum of Fools idea).
// Ring the war gong ({INTERACT}) to start a trial: the gates slam shut and five rounds of strong foes come in
// (elites, packs, Volt Rats). Clear them all: an extra boon pick, Hair Gel, a pile of EXP, and the title.
// Every clear makes the next trial tougher (+20% enemy health per clear, more with Encore levels / Blood Moons).
// Dying ends the trial (the scene reloads, the gates are open again).
// The island and the arena objects are made by Tools > Rowdy Dandy > Frontier > Build The Frontier; this runs them.
public class FrontierArena : MonoBehaviour
{
    [Tooltip("Arena floor, between the two gates (world x)")]
    public float leftX = 271f, rightX = 293f;
    [Tooltip("Arena floor height (world y)")]
    public float floorY = -3.4f;
    [Tooltip("Where the island starts: the random wave spawner leaves everything right of this alone")]
    public float islandStartX = 256f;
    public Transform gong;

    private const string ClearsKey = "RD_FrontierClears", BestKey = "RD_FrontierBest";
    public static int Clears => PlayerPrefs.GetInt(ClearsKey, 0);

    private static FrontierArena instance;
    public static bool Running => instance != null && instance.running;
    public static bool IsQuiet(Vector2 p) => instance != null && p.x >= instance.islandStartX;

    private bool running;
    private readonly List<EnemyHealth> alive = new List<EnemyHealth>();
    private BoxCollider2D leftGate, rightGate;
    private SpriteRenderer leftBars, rightBars, gongSprite, prompt;
    private float gongShake;
    private Vector3 gongHome;

    // ---------------------------------------------------------------- the rounds
    private struct Spawn { public string prefab; public int count; public bool elite, ranged, flying; public float delay; }
    private struct Round { public string title; public Spawn[] spawns; }

    private static Spawn S(string prefab, int count, bool elite = false, float delay = 0f, bool ranged = false, bool flying = false)
        => new Spawn { prefab = prefab, count = count, elite = elite, delay = delay, ranged = ranged, flying = flying };

    private static readonly Round[] Rounds =
    {
        new Round { title = "THE WARM-UP", spawns = new[] { S("Enemy_Werefast", 3), S("Enemy_VoltRat", 3, false, 1.5f, false, true) } },
        new Round { title = "GNOLL PARTY", spawns = new[] { S("Enemy_GnollWarrior", 2), S("Enemy_GnollArcher", 2, false, 0.5f, true), S("Enemy_GnollBomber", 1, false, 2f, true), S("Enemy_GnollWarrior", 1, true, 6f) } },
        new Round { title = "BIG BAD WOLVES", spawns = new[] { S("Enemy_BigWerewolf", 2, true), S("Enemy_TransformWolf", 2, false, 2f), S("Enemy_VoltRat", 4, false, 4f, false, true) } },
        new Round { title = "KNIGHTS OF THE FRONTIER", spawns = new[] { S("Enemy_WereKnight", 2, true), S("Enemy_HorseRider", 1, false, 2f), S("Enemy_Werefast", 2, true, 5f) } },
        new Round { title = "THE CHAMPIONS", spawns = new[] { S("Enemy_MegaCreature", 2, true), S("Enemy_VoltRat", 3, false, 3f, false, true), S("Enemy_VoltRat", 3, true, 9f, false, true), S("Enemy_GnollArcher", 2, true, 6f, true) } },
    };

    // ---------------------------------------------------------------- setup
    private void Awake()
    {
        instance = this;
        // stand everything on the real floor
        if (SolidGround.Ray(new Vector2((leftX + rightX) / 2f, floorY + 4f), Vector2.down, 10f, out RaycastHit2D hit)) floorY = hit.point.y;
        if (gong != null && SolidGround.Ray(gong.position + Vector3.up * 3f, Vector2.down, 8f, out RaycastHit2D g)) gong.position = g.point;
        if (gong != null) gongHome = gong.position;
        leftGate = MakeGate("Left Gate", leftX - 0.4f, out leftBars);
        rightGate = MakeGate("Right Gate", rightX + 0.4f, out rightBars);
        SetGates(false, true);
        if (gong != null)
        {
            gongSprite = gong.GetComponent<SpriteRenderer>();
            if (gongSprite == null) gongSprite = gong.gameObject.AddComponent<SpriteRenderer>();
            if (gongSprite.sprite == null)
            {
                gongSprite.sprite = GongSprite;
                gongSprite.sortingOrder = 8;
                if (ItemArt.Lit != null) gongSprite.sharedMaterial = ItemArt.Lit;
            }
            var p = new GameObject("Prompt");
            p.transform.SetParent(gong, false);
            prompt = p.AddComponent<SpriteRenderer>();
            prompt.sortingOrder = 120;
            if (CatFX.Unlit != null) prompt.sharedMaterial = CatFX.Unlit;
        }
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private BoxCollider2D MakeGate(string name, float x, out SpriteRenderer bars)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(x, floorY, 0f);
        go.layer = LayerMask.NameToLayer("groundLayer") >= 0 ? LayerMask.NameToLayer("groundLayer") : 0;
        go.tag = "Ground";
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.6f, 9f);
        col.offset = new Vector2(0f, 4.5f);
        var barsGo = new GameObject("Bars");
        barsGo.transform.SetParent(go.transform, false);
        bars = barsGo.AddComponent<SpriteRenderer>();
        bars.sprite = BarsSprite;
        bars.sortingOrder = 30;
        if (ItemArt.Lit != null) bars.sharedMaterial = ItemArt.Lit;
        return col;
    }

    // ---------------------------------------------------------------- every frame
    private bool announced;

    private void Update()
    {
        UpdateGates();
        // first time on the island this session
        if (!announced && BoonRunner.Rowdy != null && BoonRunner.RowdyCenter.x > islandStartX + 8f && !PauseMenu.IsPaused)
        {
            announced = true;
            Banner.Show("THE FRONTIER", Clears > 0 ? "TRIALS CLEARED: " + Clears : "RING THE GONG IF YOU DARE", new Color(1f, 0.8f, 0.35f));
        }
        if (gong == null) return;
        Transform rowdy = BoonRunner.Rowdy;
        bool near = rowdy != null && !running && Vector2.Distance(BoonRunner.RowdyCenter, gong.position + Vector3.up * 0.5f) < 1.6f && !PauseMenu.IsPaused;
        if (prompt != null)
        {
            prompt.enabled = near;
            if (near)
            {
                prompt.sprite = WeaponDrop.GetInteractPrompt();
                prompt.transform.position = gong.position + new Vector3(0f, 2.1f + Mathf.Round(Mathf.Sin(Time.time * 4f) * 2f) / 64f, 0f);
            }
        }
        gongShake = Mathf.Max(0f, gongShake - Time.deltaTime);
        if (gong != null) gong.position = gongHome + new Vector3(Mathf.Round(Mathf.Sin(Time.time * 60f) * 2f * gongShake / 0.6f) / 64f, 0f, 0f);
        if (near && GameInput.Down(GameInput.Act.Interact))
        {
            Interact.Use();
            StartCoroutine(Trial());
        }
        else if (near && Time.frameCount % 240 == 0)
            IconPopup.Show(gong.position + Vector3.up * 2.6f, null, Clears > 0 ? "TRIAL " + (Clears + 1) + " AWAITS" : "RING THE GONG", new Color(1f, 0.8f, 0.4f), 0.7f, 1.6f);
    }

    // ---------------------------------------------------------------- the trial
    private IEnumerator Trial()
    {
        running = true;
        RingGong();
        int trial = Clears + 1;
        yield return new WaitForSeconds(0.6f);
        SetGates(true, false);
        Banner.Show("THE FRONTIER", "TRIAL " + trial, new Color(1f, 0.8f, 0.35f));
        yield return new WaitForSeconds(2.2f);

        float hp = 1f + 0.2f * Clears + 0.03f * Encore.Level + (Encore.BloodMoon ? 0.3f : 0f);
        for (int r = 0; r < Rounds.Length; r++)
        {
            Banner.Show("ROUND " + (r + 1), Rounds[r].title, r == Rounds.Length - 1 ? new Color(1f, 0.3f, 0.35f) : Color.white);
            FXSound.Play(BoonArt.Get != null ? BoonArt.Get.open : null, 0.5f, 1f);
            yield return new WaitForSeconds(1.4f);
            Banner.Show("FIGHT!", null, new Color(1f, 0.85f, 0.3f));
            ScreenShake.Impulse(0.4f);
            yield return SpawnRound(Rounds[r], hp);
            // wait until the arena is clear
            while (true)
            {
                alive.RemoveAll(e => e == null || e.enemydead);
                if (alive.Count == 0) break;
                if (BoonRunner.Rowdy == null) yield break; // died: the scene reloads
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            if (r < Rounds.Length - 1)
            {
                Banner.Show("ROUND " + (r + 1) + " CLEAR", "A BREATHER, BABY", new Color(0.55f, 1f, 0.6f));
                if (BoonRunner.Rowdy != null && BoonRunner.Rowdy.TryGetComponent(out Health h)) h.AddHealth(h.startingHealth * 0.15f, false);
                yield return new WaitForSeconds(2.2f);
            }
        }
        Victory(trial);
    }

    private IEnumerator SpawnRound(Round round, float hp)
    {
        var pending = new List<(Spawn s, float at)>();
        float start = Time.time;
        foreach (Spawn s in round.spawns) pending.Add((s, start + s.delay));
        int side = Random.value < 0.5f ? -1 : 1;
        while (pending.Count > 0)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].at) continue;
                Spawn s = pending[i].s;
                for (int n = 0; n < s.count; n++) { SpawnOne(s, hp, side); side = -side; }
                pending.RemoveAt(i);
            }
            yield return null;
        }
    }

    private void SpawnOne(Spawn s, float hp, int side)
    {
        GameObject prefab = Resources.Load<GameObject>("Enemies Prefab/" + s.prefab);
        if (prefab == null) { Debug.LogWarning("FrontierArena: no prefab " + s.prefab); return; }
        float x = s.ranged ? (side < 0 ? leftX + 1.2f : rightX - 1.2f) : Mathf.Lerp(leftX + 1.5f, rightX - 1.5f, side < 0 ? Random.Range(0f, 0.35f) : Random.Range(0.65f, 1f));
        float y = s.flying ? floorY + Random.Range(2.5f, 4f) : floorY + 0.3f;
        GameObject enemy = Instantiate(prefab, new Vector3(x, y, 0f), Quaternion.identity);
        enemy.name = prefab.name;
        if (s.elite) EliteEnemy.Apply(enemy, 2f, 2.5f, 25f, 1f); // no size change: pixel art stays at its own size
        EnemyHealth h = enemy.GetComponentInChildren<EnemyHealth>(true);
        if (h != null)
        {
            h.SetMaxHealth(h.startingenemyHealth * hp);
            alive.Add(h);
        }
        if (!s.flying)
        {
            Collider2D col = enemy.GetComponentInChildren<Collider2D>();
            if (col != null)
            {
                Physics2D.SyncTransforms();
                float bottom = enemy.transform.position.y - col.bounds.min.y;
                enemy.transform.position = new Vector3(x, floorY + bottom + 0.05f, 0f);
            }
        }
        // face Rowdy (the art faces left at +x scale)
        Vector3 sc = enemy.transform.localScale;
        if (BoonRunner.Rowdy != null) sc.x = Mathf.Abs(sc.x) * (BoonRunner.RowdyCenter.x > x ? -1f : 1f);
        enemy.transform.localScale = sc;
        // a burst where they come in
        PulseRing.Spawn(enemy.transform.position + Vector3.up * 0.5f, s.elite ? EliteEnemy.OutlineColor : new Color(1f, 0.8f, 0.4f, 0.9f), 1.2f, 0.35f);
        FXParticle.Burst(enemy.transform.position + Vector3.up * 0.3f, new Color(0.9f, 0.7f, 0.45f), 12, 1f, 3f, 6f, 0.5f, true);
    }

    private void Victory(int trial)
    {
        running = false;
        SetGates(false, false);
        PlayerPrefs.SetInt(ClearsKey, Clears + 1);
        PlayerPrefs.SetInt(BestKey, Mathf.Max(PlayerPrefs.GetInt(BestKey, 0), trial));
        PlayerPrefs.Save();
        Banner.Show("FRONTIER CHAMPION!", "TRIAL " + trial + " CLEARED", new Color(1f, 0.82f, 0.3f));
        BoonArt art = BoonArt.Get;
        if (art != null) { FXSound.Play(art.fanfare, 0.8f, 1f); FXSound.Play(art.sparkle, 0.6f, 1.1f); }
        ScreenShake.Impulse(0.6f);
        Vector3 c = BoonRunner.Rowdy != null ? BoonRunner.RowdyCenter : gong.position;
        PulseRing.Spawn(c, new Color(1f, 0.82f, 0.3f, 1f), 4f, 0.6f);
        FXParticle.Burst(c, new Color(1f, 0.82f, 0.3f), 40, 2f, 6f, 2f, 1f);
        // rewards: a boon pick, hair gel, EXP
        Boons.DevOfferPick();
        Boons.AddRerolls(2);
        IconPopup.Show(c + Vector3.up * 1.6f, null, "+1 BOON  +2 HAIR GEL", new Color(1f, 0.85f, 0.5f), 1f, 3f);
        if (PlayerStats.Instance != null) PlayerStats.Instance.AddEXP(300f + 100f * trial);
    }

    private void RingGong()
    {
        gongShake = 0.6f;
        BoonArt art = BoonArt.Get;
        if (art != null) { FXSound.Play(art.clang, 0.9f, 0.55f); FXSound.Play(art.bigBoom, 0.4f, 0.6f); }
        if (gong != null) PulseRing.Spawn(gong.position + Vector3.up * 0.9f, new Color(1f, 0.8f, 0.4f, 1f), 3f, 0.5f);
        ScreenShake.Impulse(0.5f);
        GamepadRumble.Pulse(0.5f, 0.6f, 0.3f);
    }

    // ---------------------------------------------------------------- gates
    private bool gatesClosed;
    private float gateMoveAt = -10f;

    private void SetGates(bool closed, bool instant)
    {
        gatesClosed = closed;
        gateMoveAt = instant ? -10f : Time.time;
        leftGate.enabled = rightGate.enabled = closed;
        if (!instant)
        {
            BoonArt art = BoonArt.Get;
            if (art != null) FXSound.Play(art.clang, 0.7f, closed ? 0.6f : 0.8f);
            ScreenShake.Impulse(closed ? 0.45f : 0.25f);
        }
    }

    private void UpdateGates()
    {
        float k = Mathf.Clamp01((Time.time - gateMoveAt) / 0.25f);
        float down = gatesClosed ? k * k : 1f - k;            // 1 = fully down
        foreach (SpriteRenderer gate in new[] { leftBars, rightBars })
        {
            if (gate == null) continue;
            gate.transform.localPosition = new Vector3(0f, Mathf.Round(Mathf.Lerp(7f, 0f, down) * 64f) / 64f, 0f);
            gate.enabled = down > 0.02f;
        }
        if (gatesClosed && k >= 1f && Time.time - gateMoveAt < 0.3f)
        {
            FXParticle.Burst(leftBars.transform.position, new Color(0.6f, 0.5f, 0.4f), 6, 1f, 3f, 8f, 0.4f, true);
            FXParticle.Burst(rightBars.transform.position, new Color(0.6f, 0.5f, 0.4f), 6, 1f, 3f, 8f, 0.4f, true);
        }
    }

    // ---------------------------------------------------------------- art (drawn)
    private static Sprite bars, gongArt;

    // Portcullis: iron bars with two braces, 20 x 140 px, pivot at the bottom
    private static Sprite BarsSprite
    {
        get
        {
            if (bars != null) return bars;
            const int w = 20, h = 140;
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                bool brace = y % 46 < 4;
                var row = new char[w];
                for (int x = 0; x < w; x++)
                {
                    int col = x % 5;
                    char c = '.';
                    if (brace) c = (y % 46 == 0 || y % 46 == 3) ? 'k' : (x % 2 == 0 ? 's' : 'i');
                    else if (col == 1) c = 'k';
                    else if (col == 2) c = 's';
                    else if (col == 3) c = 'i';
                    if (y >= h - 6 && (col == 2 || col == 3)) c = (y >= h - 3) ? 'k' : 's'; // spikes at the bottom
                    row[x] = c;
                }
                rows[h - 1 - y] = new string(row); // rows[0] is the top
            }
            bars = BoonFX.FromRows("FrontierBars", rows, ch =>
                ch == 'k' ? new Color32(27, 8, 32, 255) : ch == 's' ? new Color32(150, 156, 176, 255) : new Color32(84, 88, 104, 255), new Vector2(0.5f, 0f));
            return bars;
        }
    }

    // War gong on a wooden frame, 34 x 44 px
    public static Sprite GongSprite
    {
        get
        {
            if (gongArt != null) return gongArt;
            const int w = 34, h = 44;
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var row = new char[w];
                for (int x = 0; x < w; x++)
                {
                    char c = '.';
                    bool post = (x >= 2 && x <= 5) || (x >= 28 && x <= 31);
                    bool beam = y >= 2 && y <= 6;
                    if (post && y >= 2) c = (x == 2 || x == 31 || y == 43) ? 'k' : 'n';
                    if (beam && x >= 0 && x <= 33) c = (y == 2 || y == 6 || x == 0 || x == 33) ? 'k' : 'n';
                    float dx = x - 16.5f, dy = y - 24f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 11f) c = d > 10f ? 'k' : d > 8.5f ? 'o' : (d < 3f ? 'y' : 'g');
                    if ((x == 12 || x == 21) && y > 6 && y < 14) c = 'k'; // ropes
                    row[x] = c;
                }
                rows[y] = new string(row);
            }
            gongArt = BoonFX.FromRows("FrontierGong", rows, ch =>
            {
                switch (ch)
                {
                    case 'k': return new Color32(27, 8, 32, 255);
                    case 'n': return new Color32(120, 74, 44, 255);
                    case 'o': return new Color32(200, 120, 40, 255);
                    case 'g': return new Color32(240, 180, 60, 255);
                    case 'y': return new Color32(255, 240, 150, 255);
                }
                return new Color32(0, 0, 0, 0);
            }, new Vector2(0.5f, 0f));
            return gongArt;
        }
    }
}

// Big announcer text across the middle of the screen: slams in, holds, fades
public class Banner : MonoBehaviour
{
    private static Banner instance;
    private PixelText title, sub;
    private Image band;
    private CanvasGroup group;
    private float shownAt = -10f;

    public static void Show(string title, string sub, Color color)
    {
        if (instance == null)
        {
            RectTransform root = OverlayUI.MakeRect("Frontier Banner", OverlayUI.Root);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, 180f);
            instance = root.gameObject.AddComponent<Banner>();
            instance.group = root.gameObject.AddComponent<CanvasGroup>();
            instance.band = OverlayUI.MakeImage("Band", root, new Color(0.05f, 0.02f, 0.08f, 0.7f), OverlayUI.WhiteSprite);
            instance.band.rectTransform.sizeDelta = new Vector2(2200f, 150f);
            instance.title = PixelText.Create(root, "", 9, Color.white, 0.5f);
            instance.title.Rect.anchorMin = instance.title.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            instance.title.Rect.anchoredPosition = new Vector2(0f, 18f);
            instance.sub = PixelText.Create(root, "", 3, Color.white, 0.5f);
            instance.sub.Rect.anchorMin = instance.sub.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            instance.sub.Rect.anchoredPosition = new Vector2(0f, -46f);
        }
        instance.title.SetText(title);
        instance.title.Color = color;
        instance.sub.SetText(sub ?? "");
        instance.shownAt = Time.unscaledTime;
        UISound.Play(UISound.Cue.Open);
    }

    private void Update()
    {
        float t = Time.unscaledTime - shownAt;
        float alpha = t < 0.12f ? t / 0.12f : t < 1.5f ? 1f : Mathf.Clamp01(1f - (t - 1.5f) / 0.4f);
        group.alpha = alpha;
        float slam = t < 0.15f ? Mathf.Lerp(2.2f, 1f, t / 0.15f) : 1f + 0.02f * Mathf.Sin(t * 6f);
        title.Rect.localScale = Vector3.one * slam;
        band.rectTransform.sizeDelta = new Vector2(2200f, Mathf.Lerp(0f, 150f, Mathf.Clamp01(t / 0.1f)));
    }
}
