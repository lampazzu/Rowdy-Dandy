using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// THE COLOSSEUMS (Hollow Knight's Colosseum of Fools idea). Three of them run on this (tier):
//   0 THE FRONTIER      - the island past Pelich (scene object, made by Tools > Rowdy Dandy > Frontier > Build The Frontier)
//   1 THE GLOOMWOOD     - built at runtime by GloomArena far to the east, through the Gloom Gate once the Frontier is cleared
//   2 THE PURPLE REIGN  - built at runtime by GloomArena even further east, a pool in the middle, through a gate in the
//                         Gloomwood once the Gloomwood is cleared. The hardest.
// Ring the war gong ({INTERACT}) to start a trial: the gates slam shut and 30 WAVES come in (ArenaWaves) back to back -
// no banner per wave, no breathers: only WAVE START, BOSS FIGHT (waves 10 / 20 / 30) and the END.
// Every trial is a fresh run (ArenaRun): level 1, no boons, the cats wait outside - over the 30 waves Rowdy climbs to
// level 10. The boons he earns wait: one to start with, then all of them at the seals (waves 10 / 20) and at the end.
// During a trial {INTERACT} at the gong drops a legendary rat (RatBait): a cat that waits outside comes running.
// Arena enemies always know where Rowdy is (EnemyMovement.ArenaAggro). Ore rocks and wolf statues sometimes show up as
// a wave starts. A SEAL saves the build: dying after one, the next trial starts there with it.
// Dying ends the trial (the scene reloads, the gates are open again).
public class FrontierArena : MonoBehaviour
{
    [Tooltip("Arena floor, between the two gates (world x)")]
    public float leftX = 271f, rightX = 293f;
    [Tooltip("Arena floor height (world y)")]
    public float floorY = -3.4f;
    [Tooltip("Where the island starts: the random wave spawner leaves everything right of this alone")]
    public float islandStartX = 256f;
    public Transform gong;
    [Tooltip("At load, push the gates out to where the arena floor really ends (a wall or the drop into the sea), up to 30 units. Off = the gates stay exactly at Left X / Right X.")]
    public bool fitGatesToFloor = true;
    [Tooltip("Built at runtime (GloomArena): the Gloomwood or the Purple Reign")]
    public bool gloom;
    [Tooltip("0 The Frontier, 1 The Gloomwood, 2 The Purple Reign")]
    public int tier;

    public static int Clears => PlayerPrefs.GetInt("RD_FrontierClears", 0);
    public static int GloomClears => PlayerPrefs.GetInt("RD_GloomClears", 0);
    public static int VioletClears => PlayerPrefs.GetInt("RD_VioletClears", 0);

    private static readonly List<FrontierArena> arenas = new List<FrontierArena>();
    public static bool Running { get { foreach (FrontierArena a in arenas) if (a != null && a.running) return true; return false; } }
    // the island and everything past it (the runtime arenas are far to the right): no random waves there
    public static bool IsQuiet(Vector2 p) { foreach (FrontierArena a in arenas) if (a != null && !a.gloom && p.x >= a.islandStartX) return true; return false; }
    public static bool InGloom(Vector2 p) => p.x >= GloomArena.MinX;
    public static FrontierArena Frontier { get { foreach (FrontierArena a in arenas) if (a != null && !a.gloom) return a; return null; } }
    // the colosseum mid-trial that this point is inside (null = none): arena-only behaviour (Wereknights walk, radar)
    public static FrontierArena ArenaAt(Vector2 p)
    {
        foreach (FrontierArena a in arenas)
            if (a != null && a.running && p.x >= a.leftX - 1.5f && p.x <= a.rightX + 1.5f && p.y > a.floorY - 6f && p.y < a.floorY + 20f) return a;
        return null;
    }
    public static FrontierArena Current { get { foreach (FrontierArena a in arenas) if (a != null && a.running) return a; return null; } }
    public IReadOnlyList<EnemyHealth> Alive => alive;

    private static readonly string[] Keys = { "RD_Frontier", "RD_Gloom", "RD_Violet" };
    private static readonly string[] Titles = { "THE FRONTIER", "THE GLOOMWOOD", "THE PURPLE REIGN" };
    private string Key => Keys[Mathf.Clamp(tier, 0, 2)];
    private int ClearsHere => PlayerPrefs.GetInt(Key + "Clears", 0);
    // A seal only counts for the trial it was made in (it's tagged with the clear count). Seals from older saves or
    // test runs had no tag: one of those in the Gloomwood made the first trial there skip the fresh start (no reset,
    // no 3 opening boons). Those are thrown away now.
    private int Seal
    {
        get
        {
            int wave = PlayerPrefs.GetInt(Key + "Seal", 0);
            if (wave <= 0) return 0;
            if (PlayerPrefs.GetInt(Key + "SealTrial", -1) != ClearsHere) { DropSeal(); return 0; }
            return wave;
        }
    }
    private void DropSeal()
    {
        PlayerPrefs.SetInt(Key + "Seal", 0);
        PlayerPrefs.DeleteKey(Key + "SealTrial");
        ArenaRun.ClearSeal(Key);
        PlayerPrefs.Save();
    }
    // Beaten = done: the Frontier and the Gloomwood can't be fought again, on to the next one. The Purple Reign (the
    // last) stays open and gets tougher every clear.
    // Only a win of the current 30-wave trial locks it (Key+"Won"): the old clear counts came from the 5-round
    // version and dev travel, and locked arenas nobody had beaten.
    private bool Locked => tier < 2 && PlayerPrefs.GetInt(Key + "Won", 0) == 1;

    // Dev reset: every colosseum back to never fought (clears, bests, seals, wins, the dev-opened gates)
    public static void ResetAllProgress()
    {
        foreach (string k in Keys)
            foreach (string part in new[] { "Clears", "Best", "Seal", "SealTrial", "SealBuild", "Won" })
                PlayerPrefs.DeleteKey(k + part);
        PlayerPrefs.DeleteKey("RD_GloomGateOpen");
        PlayerPrefs.DeleteKey("RD_VioletGateOpen");
        PlayerPrefs.Save();
    }
    private string Title => Titles[Mathf.Clamp(tier, 0, 2)];
    private Color Theme => tier == 2 ? new Color(0.9f, 0.45f, 1f) : tier == 1 ? new Color(0.72f, 0.55f, 1f) : new Color(1f, 0.65f, 0.3f);

    // this trial's 30 waves, drawn from the templates (ArenaWaves.ForRun) when the gong is rung
    private ArenaWaves.Round[] runWaves;
    public int CurrentWave { get; private set; }     // 1-based, 0 = not fighting yet
    public int WaveCount => runWaves != null ? runWaves.Length : ArenaRun.Waves;
    private int pendingSpawns;
    public bool IsRunning => running;
    public int EnemiesLeft
    {
        get
        {
            int n = pendingSpawns;
            foreach (EnemyHealth e in alive) if (e != null && !e.enemydead && e.gameObject.activeInHierarchy) n++;
            return n;
        }
    }
    [HideInInspector] public bool hasPool;          // The Purple Reign: water in the middle (set by its builder)
    [HideInInspector] public float poolX0, poolX1, poolSurface;

    // more of everything than the wave lists say (the first colosseum read as too few, too easy)
    private float CountScale => tier == 2 ? 1.8f : tier == 1 ? 1.6f : 1.6f;
    private bool running;
    private readonly List<EnemyHealth> alive = new List<EnemyHealth>();
    private BoxCollider2D leftGate, rightGate;
    private SpriteRenderer leftBars, rightBars, gongSprite, prompt;
    private float gongShake;
    private Vector3 gongHome;

    // ---------------------------------------------------------------- setup
    private void Awake()
    {
        arenas.Add(this);
        if (!gloom) OpenSeaRoute();
        // stand everything on the real floor
        if (SolidGround.Ray(new Vector2((leftX + rightX) / 2f, floorY + 4f), Vector2.down, 10f, out RaycastHit2D hit)) floorY = hit.point.y;
        if (!gloom) FitToFloor(); // the gates go where the arena floor really ends (the island was rebuilt wider)
        if (gong != null && SolidGround.Ray(gong.position + Vector3.up * 3f, Vector2.down, 8f, out RaycastHit2D g)) gong.position = g.point;
        if (!gloom) MakePelichTemplate();
        if (gong != null) gongHome = gong.position;
        leftGate = MakeGate("Left Gate", leftX - 0.4f, out leftBars);
        rightGate = MakeGate("Right Gate", rightX + 0.4f, out rightBars);
        leftStack = leftBars.GetComponentsInChildren<SpriteRenderer>(true);
        rightStack = rightBars.GetComponentsInChildren<SpriteRenderer>(true);
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
        if (!gloom) GloomArena.Build(this); // the second colosseum + the gates between them
    }

    private void OnDestroy() => arenas.Remove(this);

    // The old end-of-the-world wall right of Pelich's arena ("TerrainCorrector (25)", merged into BeachMap's composite:
    // x 251-254, y -4 to +2.6) stood across the sea to the island, so Rowdy couldn't surf over. Taken out at load.
    private static void OpenSeaRoute()
    {
        GameObject wall = GameObject.Find("TerrainCorrector (25)");
        if (wall == null) return;
        bool changed = false;
        CompositeCollider2D composite = null;
        foreach (Collider2D c in wall.GetComponents<Collider2D>())
        {
            Bounds b = c.bounds;
            if (b.max.x < 248f || b.min.x > 258f) continue; // not the one we mean
            if (c.composite != null) composite = c.composite;
            if (c.enabled) { c.enabled = false; changed = true; }
        }
        if (composite == null) composite = wall.GetComponentInParent<CompositeCollider2D>();
        if (changed && composite != null) composite.GenerateGeometry();
        FillSea();
    }

    // The solid water surface Rowdy surfs on (the "Watermap" tilemap) stopped at x 231: the sea between Pelich and
    // the island (x 252-270) was only the animated water + a trigger, so he sank into the kill zone. Its last water
    // column is copied across the gap at load.
    private static void FillSea()
    {
        foreach (UnityEngine.Tilemaps.Tilemap tm in FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
        {
            if (tm.name != "Watermap") continue;
            tm.CompressBounds();
            BoundsInt b = tm.cellBounds;
            int source = -1;
            for (int x = b.xMax - 1; x >= b.xMin && source < 0; x--)
                for (int y = b.yMin; y < b.yMax; y++)
                    if (tm.GetTile(new Vector3Int(x, y, 0)) != null) { source = x; break; }
            if (source < 0) return;
            int from = tm.WorldToCell(new Vector3(250.5f, -5f, 0f)).x, to = tm.WorldToCell(new Vector3(271f, -5f, 0f)).x;
            if (from <= source) return; // already filled
            for (int x = from; x <= to; x++)
                for (int y = b.yMin; y < b.yMax; y++)
                {
                    UnityEngine.Tilemaps.TileBase t = tm.GetTile(new Vector3Int(source, y, 0));
                    if (t != null) tm.SetTile(new Vector3Int(x, y, 0), t);
                }
            if (tm.TryGetComponent(out UnityEngine.Tilemaps.TilemapCollider2D tc)) tc.ProcessTilemapChanges();
            if (tm.TryGetComponent(out CompositeCollider2D comp)) comp.GenerateGeometry();
            return;
        }
    }

    // The arena floor runs from wall to wall (or to the drop into the sea): walk out from the middle along the floor and
    // put leftX / rightX where it really ends, so the gates close the WHOLE arena. Never narrower than the Inspector
    // values, at most 30 units out, never left of the island's start.
    private void FitToFloor()
    {
        if (!fitGatesToFloor) return;
        float mid = (leftX + rightX) / 2f;
        float l = FloorEnd(mid, -1f), r = FloorEnd(mid, 1f);
        float wasL = leftX, wasR = rightX;
        leftX = Mathf.Max(Mathf.Min(leftX, l), islandStartX + 1f);
        rightX = Mathf.Max(rightX, r);
        if (leftX != wasL || rightX != wasR)
            Debug.Log("Frontier: gates fitted to the floor, x " + leftX.ToString("0.0") + " to " + rightX.ToString("0.0") + " (Inspector " + wasL + " to " + wasR + ")");
    }

    private float FloorEnd(float from, float dir)
    {
        const float step = 0.25f;
        float last = from, lastY = floorY, gap = 0f;
        for (float d = step; d <= 30f; d += step)
        {
            float x = from + dir * d;
            // a wall at body height = the end
            if (SolidGround.Ray(new Vector2(x - dir * step, lastY + 1.2f), new Vector2(dir, 0f), step, out _)) return last;
            if (SolidGround.Ray(new Vector2(x, lastY + 2.5f), Vector2.down, 5f, out RaycastHit2D h) && Mathf.Abs(h.point.y - floorY) < 2.5f)
            {
                last = x;
                lastY = h.point.y;
                gap = 0f;
            }
            else if ((gap += step) > 1.5f) return last; // the floor drops away (the sea)
        }
        return last;
    }

    private const float GateHeight = 40f; // nothing jumps, dashes or flies over it

    private BoxCollider2D MakeGate(string name, float x, out SpriteRenderer bars)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(x, floorY, 0f);
        go.layer = LayerMask.NameToLayer("groundLayer") >= 0 ? LayerMask.NameToLayer("groundLayer") : 0;
        go.tag = "Ground";
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.6f, GateHeight);
        col.offset = new Vector2(0f, GateHeight / 2f - 2f); // from just under the floor all the way up
        var barsGo = new GameObject("Bars");
        barsGo.transform.SetParent(go.transform, false);
        bars = barsGo.AddComponent<SpriteRenderer>();
        bars.sprite = BarsSprite;
        bars.sortingOrder = 30;
        if (ItemArt.Lit != null) bars.sharedMaterial = ItemArt.Lit;
        if (gloom) bars.color = new Color(0.7f, 0.6f, 0.9f);
        // more bars stacked on top (they all drop together) + a shimmering barrier up past the top of the screen
        float segment = bars.sprite != null ? bars.sprite.bounds.size.y : 2.1875f;
        for (int k = 1; k <= 3; k++)
        {
            var up = new GameObject("Bars " + k);
            up.transform.SetParent(barsGo.transform, false);
            up.transform.localPosition = new Vector3(0f, segment * k, 0f);
            SpriteRenderer s = up.AddComponent<SpriteRenderer>();
            s.sprite = bars.sprite;
            s.sortingOrder = bars.sortingOrder;
            s.sharedMaterial = bars.sharedMaterial;
            s.color = bars.color;
        }
        SpriteRenderer barrier = PixelShape.Make("Barrier", PixelShape.Kind.Shaft, go.transform.position, 29, go.transform);
        if (barrier != null)
        {
            PixelShape.Size(barrier, 0.5f, 18f);
            barrier.color = Color.clear;
            if (name.StartsWith("Left")) leftBarrier = barrier; else rightBarrier = barrier;
        }
        return col;
    }

    private SpriteRenderer leftBarrier, rightBarrier;
    private SpriteRenderer[] leftStack = new SpriteRenderer[0], rightStack = new SpriteRenderer[0];

    // ---------------------------------------------------------------- every frame
    private bool announced;

    private void Update()
    {
        UpdateGates();
        // first time near this arena this session
        if (!announced && BoonRunner.Rowdy != null && !PauseMenu.IsPaused && Mathf.Abs(BoonRunner.RowdyCenter.x - (leftX + rightX) / 2f) < 20f
            && Mathf.Abs(BoonRunner.RowdyCenter.y - floorY) < 12f)
        {
            announced = true;
            Banner.Show(Title, null, Theme);
        }
        if (gong == null) return;
        Transform rowdy = BoonRunner.Rowdy;
        bool close = rowdy != null && Vector2.Distance(BoonRunner.RowdyCenter, gong.position + Vector3.up * 0.5f) < 1.6f && !PauseMenu.IsPaused;
        // before a trial the gong starts it; during one it takes a legendary rat (bait: a cat waiting outside comes back)
        bool canBait = running && CatRoster.Rats > 0 && CatRoster.Lurable() != null && !baitBusy;
        if (close && !running && Locked)
        {
            // beaten: the gong stays quiet
            if (prompt != null) prompt.enabled = false;
            if (Time.frameCount % 240 == 0)
                IconPopup.Show(gong.position + Vector3.up * 2.6f, null, tier == 0 ? "CLEARED: ON TO THE GLOOMWOOD" : "CLEARED: ON TO THE PURPLE REIGN", Theme, 0.7f, 1.6f);
            gong.position = gongHome;
            return;
        }
        bool near = close && (!running || canBait);
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
        gong.position = gongHome + new Vector3(Mathf.Round(Mathf.Sin(Time.time * 60f) * 2f * gongShake / 0.6f) / 64f, 0f, 0f);
        if (near && GameInput.Down(GameInput.Act.Interact) && !Interact.UsedThisFrame)
        {
            Interact.Use();
            if (!running) StartCoroutine(Trial());
            else DropBait();
        }
        else if (near && Time.frameCount % 240 == 0)
            IconPopup.Show(gong.position + Vector3.up * 2.6f, null,
                running ? "DROP A RAT (X" + CatRoster.Rats + ")" : Seal > 0 ? "WAVE " + (Seal + 1) + " AWAITS" : "TRIAL " + (ClearsHere + 1) + " AWAITS",
                running ? new Color(1f, 0.82f, 0.25f) : Theme, 0.7f, 1.6f);
    }

    private bool baitBusy;

    private void DropBait()
    {
        if (!CatRoster.SpendRat(out PetFollower cat)) { UISound.Play(UISound.Cue.Locked); return; }
        gongShake = 0.3f;
        BoonArt art = BoonArt.Get;
        if (art != null) FXSound.Play(art.clang, 0.5f, 1.3f);
        RatBait.Drop(gong.position + Vector3.left * 0.9f, cat);
        StartCoroutine(BaitCooldown());
    }

    private IEnumerator BaitCooldown() { baitBusy = true; yield return new WaitForSeconds(2.5f); baitBusy = false; }

    // ---------------------------------------------------------------- the trial
    private float HealthScale(int wave)
    {
        float encore = 1f + 0.03f * Encore.Level + (Encore.BloodMoon ? 0.3f : 0f);
        switch (tier)
        {
            case 2: return (2.6f + 0.1f * wave) * (1f + 0.3f * ClearsHere) * encore;
            case 1: return (1.8f + 0.07f * wave) * (1f + 0.25f * ClearsHere) * encore;
            default: return (1.1f + 0.045f * wave) * (1f + 0.2f * ClearsHere) * encore;
        }
    }

    private static readonly Color BossRed = new Color(1f, 0.3f, 0.35f);

    private IEnumerator Trial()
    {
        running = true;
        ArenaRun.InTrial = true;
        Boons.Unlock(); // ringing the gong is where boons begin
        RingGong();
        int trial = ClearsHere + 1;
        ArenaWaves.Round[] waves = runWaves = ArenaWaves.ForRun(tier); // a fresh draw of the templates every trial
        int start = Mathf.Clamp(Seal, 0, waves.Length - 1);
        CurrentWave = 0;
        ArenaHUD.Show(this, Theme);
        yield return new WaitForSeconds(0.6f);
        SetGates(true, false);

        // a fresh run: level 1, no boons, the cats wait outside (a seal brings back the build it saved)
        bool fromSeal = start > 0 && ArenaRun.RestoreSeal(Key);
        if (fromSeal && Boons.OwnedIds.Count == 0) fromSeal = false; // a seal with no build in it is no use
        if (!fromSeal) { start = 0; DropSeal(); ArenaRun.FreshStart(); }
        Banner.Show(fromSeal ? "WAVE " + (start + 1) : "TRIAL " + trial, null, Theme, 2f);
        yield return new WaitForSeconds(2.2f);

        // the opening gift: three boons to build on
        if (!fromSeal) { Boons.Grant(3); yield return TakeBoons(); }

        ArenaRun.HoldBoons = true;
        Banner.Show("WAVE START!", null, Theme, 1f);
        BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.open : null, 0.6f, 1f);
        ScreenShake.Impulse(0.4f);
        yield return new WaitForSeconds(0.9f);

        for (int w = start; w < waves.Length; w++)
        {
            int n = w + 1;
            CurrentWave = n;
            bool boss = n % 10 == 0;
            ArenaRun.LevelCap = ArenaRun.CapDuring(n);
            if (boss)
            {
                Banner.Show("BOSS FIGHT!", waves[w].title, BossRed, 1.6f);
                BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.bigBoom : null, 0.5f, 0.7f);
                ScreenShake.Impulse(0.7f);
                yield return new WaitForSeconds(1.4f);
            }
            else
            {
                // a quiet tap on the gong instead of a banner
                gongShake = 0.25f;
                BoonArt art = BoonArt.Get;
                if (art != null) FXSound.Play(art.clang, 0.25f, 1.1f);
            }
            Treasure(n);
            yield return SpawnRound(waves[w], HealthScale(w), w);
            // wait until the arena is clear (strays and anything summoned count too)
            float scanAt = 0f, pullAt = Time.time + 40f;
            while (true)
            {
                if (Time.time >= scanAt) { scanAt = Time.time + 0.5f; AdoptStrays(); }
                if (Time.time >= pullAt) { pullAt = Time.time + 15f; PullStragglers(); } // a long wave: nobody stuck somewhere
                alive.RemoveAll(e => e == null || e.enemydead || !e.gameObject.activeInHierarchy
                                     || e.transform.position.y < floorY - 8f); // fell out of the world
                if (alive.Count == 0) break;
                if (BoonRunner.Rowdy == null) yield break; // died: the scene reloads
                yield return null;
            }
            // cleared: a sip of health, and the level plan (1 to 10 over the 30 waves) catches him up if needed
            Health h = BoonRunner.Rowdy != null ? BoonRunner.Rowdy.GetComponent<Health>() : null;
            if (h != null) h.AddHealth(h.startingHealth * (boss ? 0.35f : 0.06f), false);
            ArenaRun.LevelCap = ArenaRun.CapDuring(n + 1);
            if (PlayerStats.Instance != null) PlayerStats.Instance.ReachLevel(ArenaRun.MinLevelAfter(n));
            if (n == waves.Length) { Boons.Grant(1); break; } // the last one opens with the victory picks
            if (n % 5 == 0) yield return BoonTime(n);
            else yield return new WaitForSeconds(1.1f);
        }
        Victory(trial);
    }

    // Lets the boon picker run until every banked pick is taken
    private IEnumerator TakeBoons()
    {
        ArenaRun.HoldBoons = false;
        float until = Time.unscaledTime + 1.5f;
        while (Time.unscaledTime < until && !BoonPicker.IsOpen) yield return null; // it opens after the level-up moment
        float guard = Time.unscaledTime + 180f;
        while ((BoonPicker.IsOpen || Boons.PendingPicks > 0) && Time.unscaledTime < guard && BoonRunner.Rowdy != null) yield return null;
        yield return new WaitForSeconds(0.4f);
    }

    // Every 5 waves: one boon. Waves 10 / 20 are also seals (half heal, a reroll, the build is saved)
    private IEnumerator BoonTime(int n)
    {
        bool seal = n % 10 == 0;
        Health h = BoonRunner.Rowdy != null ? BoonRunner.Rowdy.GetComponent<Health>() : null;
        Banner.Show("BOON TIME", null, Theme, 2.2f);
        if (seal && h != null) h.AddHealth(h.startingHealth * 0.5f, false);
        if (seal) Boons.AddRerolls(1);
        Boons.Grant(1);
        yield return new WaitForSeconds(1.6f);
        yield return TakeBoons();
        ArenaRun.HoldBoons = true;
        if (seal)
        {
            PlayerPrefs.SetInt(Key + "Seal", n);
            PlayerPrefs.SetInt(Key + "SealTrial", ClearsHere); // only good for this trial
            ArenaRun.SaveSeal(Key); // the build as it is now: dying from here, the next trial starts at wave n+1 with it
        }
        Banner.Show("WAVE START!", null, Theme, 1f);
        ScreenShake.Impulse(0.4f);
        yield return new WaitForSeconds(1f);
    }

    // Some waves open with an ore rock or a wolf statue somewhere on the floor (gems, drop luck, hair gel)
    private void Treasure(int wave)
    {
        if (wave <= 1 || Random.value > 0.3f) return;
        float x = Random.Range(leftX + 2f, rightX - 2f);
        if (BoonRunner.Rowdy != null && Mathf.Abs(x - BoonRunner.RowdyCenter.x) < 1.5f) x = Mathf.Clamp(x + 3f, leftX + 2f, rightX - 2f);
        if (!SolidGround.Ray(new Vector2(x, floorY + 6f), Vector2.down, 10f, out RaycastHit2D hit)) return;
        Vector3 at = hit.point;
        if (Random.value < 0.6f)
        {
            var go = new GameObject("Arena Ore");
            go.transform.position = at + Vector3.down * 0.02f;
            go.AddComponent<OreNode>().transient = true;
        }
        else StatueScatter.SpawnNear(at + Vector3.up * 0.3f);
        PulseRing.Spawn(at + Vector3.up * 0.3f, new Color(1f, 0.9f, 0.6f, 0.9f), 1.2f, 0.4f);
        FXParticle.Burst(at + Vector3.up * 0.2f, new Color(0.85f, 0.7f, 0.5f), 12, 1f, 3f, 8f, 0.5f, true);
        FXSound.Play("RockSmash", 0.3f, 1.4f);
    }

    private IEnumerator SpawnRound(ArenaWaves.Round round, float hp, int wave)
    {
        var pending = new List<(ArenaWaves.Spawn s, float at, int n)>();
        float start = Time.time;
        foreach (ArenaWaves.Spawn s in round.spawns)
        {
            int count = s.prefab == ArenaWaves.Elder || s.prefab == ArenaWaves.Steph || s.prefab == ArenaWaves.Pelich ? s.count : Mathf.CeilToInt(s.count * CountScale);
            float every = s.every > 0f ? s.every : (count > s.count ? 0.35f : 0f);
            for (int n = 0; n < count; n++) pending.Add((s, start + s.delay + n * every, n));
        }
        int side = Random.value < 0.5f ? -1 : 1;
        // the HUD counts what's still to come (sea creatures only count where there's a pool)
        pendingSpawns = 0;
        foreach (var p in pending) if (!p.s.Has('w') || hasPool) pendingSpawns++;
        while (pending.Count > 0)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].at) continue;
                if (!pending[i].s.Has('w') || hasPool) pendingSpawns = Mathf.Max(0, pendingSpawns - 1);
                SpawnOne(pending[i].s, hp, side, wave, pending[i].n == 0);
                side = -side;
                pending.RemoveAt(i);
            }
            if (BoonRunner.Rowdy == null) { pendingSpawns = 0; yield break; }
            yield return null;
        }
        pendingSpawns = 0;
    }
    private void SpawnOne(ArenaWaves.Spawn s, float hp, int side, int wave, bool firstOfGroup)
    {
        if (s.Has('<')) side = -1;
        if (s.Has('>')) side = 1;
        bool flying = s.Has('f'), ranged = s.Has('r'), sky = s.Has('s'), water = s.Has('w');
        if (water && !hasPool) return; // sea creatures only where there's a pool (The Purple Reign)
        float x = ranged ? (side < 0 ? leftX + 1.2f : rightX - 1.2f) : Mathf.Lerp(leftX + 1.5f, rightX - 1.5f, side < 0 ? Random.Range(0f, 0.35f) : Random.Range(0.65f, 1f));
        if (sky) x = Mathf.Clamp(BoonRunner.Rowdy != null ? BoonRunner.RowdyCenter.x + Random.Range(-4f, 4f) : x, leftX + 1f, rightX - 1f);
        // walkers never spawn over the pool
        if (hasPool && !flying && !water && x > poolX0 - 0.5f && x < poolX1 + 0.5f) x = side < 0 ? poolX0 - 1.5f : poolX1 + 1.5f;
        float y = flying ? floorY + Random.Range(2.5f, 4f) : floorY + 0.3f;
        if (s.prefab == "Neutral_MantaRay") y = floorY + Random.Range(0.8f, 1.4f); // skims at Rowdy's height: touching hurts
        if (water) { x = Random.Range(poolX0 + 0.8f, poolX1 - 0.8f); y = poolSurface - 0.3f; }

        GameObject enemy;
        if (s.prefab == ArenaWaves.Steph)
        {
            enemy = StephmossBoss.Spawn(new Vector3((leftX + rightX) / 2f + (hasPool ? (poolX1 - poolX0) / 2f + 2f : 0f), floorY, 0f), this, 1f + 0.3f * ClearsHere);
            EnemyHealth sh = enemy.GetComponent<EnemyHealth>();
            if (sh != null && !alive.Contains(sh)) alive.Add(sh);
            return;
        }
        if (s.prefab == ArenaWaves.Elder)
        {
            enemy = CursedElder.Spawn(new Vector3(x, floorY + 0.3f, 0f));
            if (enemy == null) return;
            CursedElder elder = enemy.GetComponentInChildren<CursedElder>(true);
            if (elder != null) elder.healthScale = hp * 0.8f;
            Banner.Show("THE MOONBOUND ELDER", null, new Color(0.7f, 0.6f, 1f), 1.4f);
        }
        else if (s.prefab == ArenaWaves.Pelich)
        {
            // he's big: comes in on the side away from Rowdy
            float mid = (leftX + rightX) / 2f;
            float px = BoonRunner.Rowdy != null && BoonRunner.RowdyCenter.x > mid ? leftX + 3.5f : rightX - 3.5f;
            if (hasPool && px > poolX0 - 2f && px < poolX1 + 2f) px = px < mid ? poolX0 - 3f : poolX1 + 3f;
            x = px;
            enemy = SpawnPelich(new Vector3(x, floorY + 0.3f, 0f));
            if (enemy == null) return;
            Banner.Show("PELICH ANUS", null, new Color(1f, 0.82f, 0.3f), 1.4f);
        }
        else
        {
            GameObject prefab = Resources.Load<GameObject>("Enemies Prefab/" + s.prefab);
            if (prefab == null) { Debug.LogWarning("Arena: no prefab " + s.prefab); return; }
            enemy = Instantiate(prefab, new Vector3(x, y, 0f), Quaternion.identity);
            enemy.name = prefab.name;
        }

        bool elite = s.Has('E') || (s.Has('e') && firstOfGroup) || (tier == 1 && Random.value < 0.1f + 0.01f * wave) || (tier == 2 && Random.value < 0.2f + 0.012f * wave);
        if (elite) EliteEnemy.Apply(enemy, 2f, 2.5f, 25f, 1f); // no size change: pixel art stays at its own size
        EnemyHealth h = enemy.GetComponentInChildren<EnemyHealth>(true);
        if (h != null)
        {
            // Pelich's own health is the beach boss's (2000): a wave boss gets about a third of it
            if (s.prefab != ArenaWaves.Elder) h.SetMaxHealth(h.startingenemyHealth * hp * (s.prefab == ArenaWaves.Pelich ? 0.3f : 1f));
            if (!alive.Contains(h)) alive.Add(h);
        }
        foreach (EnemyMovement m in enemy.GetComponentsInChildren<EnemyMovement>(true)) m.ArenaAggro();

        if (water)
        {
            BoonArt.Play(BoonArt.Get != null ? BoonArt.Get.waterBoom : null, 0.4f, 0.8f);
            GraftFX.Play("spr_bug_special_shockwave", new Vector3(x, poolSurface + 0.2f, 0f), new Color(0.8f, 0.6f, 1f), 95);
        }
        else if (!flying)
        {
            Collider2D col = enemy.GetComponentInChildren<Collider2D>();
            if (col != null)
            {
                Physics2D.SyncTransforms();
                float bottom = enemy.transform.position.y - col.bounds.min.y;
                enemy.transform.position = new Vector3(x, (sky ? floorY + 7f : floorY) + bottom + 0.05f, 0f);
            }
        }
        // face Rowdy. Each AI flips its own transform (some keep it on a child: flipping the root mirrored them)
        if (BoonRunner.Rowdy != null)
        {
            EnemyMovement[] movers = enemy.GetComponentsInChildren<EnemyMovement>(true);
            foreach (EnemyMovement m in movers) m.FaceTowards(BoonRunner.RowdyCenter.x);
            if (movers.Length == 0)
            {
                Vector3 sc = enemy.transform.localScale; // the art faces left at +x scale
                sc.x = Mathf.Abs(sc.x) * (BoonRunner.RowdyCenter.x > x ? -1f : 1f);
                enemy.transform.localScale = sc;
            }
        }
        // a burst where they come in (a warning ring on the floor for the ones falling from the sky)
        Vector3 at = sky ? new Vector3(x, floorY + 0.1f, 0f) : enemy.transform.position + Vector3.up * 0.5f;
        Color ring = elite ? EliteEnemy.OutlineColor : new Color(Theme.r, Theme.g, Theme.b, 0.9f);
        PulseRing.Spawn(at, ring, sky ? 1.6f : 1.2f, sky ? 0.6f : 0.35f);
        FXParticle.Burst(at, Color.Lerp(Theme, Color.gray, 0.4f), 12, 1f, 3f, 6f, 0.5f, true);
    }

    // ---------------------------------------------------------------- Pelich Anus as a wave boss
    // There's no Pelich prefab: at load (before anyone has hit him) the beach's Pelich is copied into a switched-off
    // holder, and arena waves spawn copies of that. His song stays on the beach; his death has no tutorial banner.
    private static GameObject pelichTemplate;
    private static Vector3 pelichScale = Vector3.one;

    private void MakePelichTemplate()
    {
        if (pelichTemplate != null) return;
        GameObject src = GameObject.Find("PelichAnus");
        if (src == null)
            foreach (EnemyHealth e in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                EnemyCatalog.Entry kind = EnemyCatalog.Identify(e);
                if (kind != null && kind.id == "pelich" && e.GetComponent<PelichBoss>() is var pb && (pb == null || !pb.arenaCopy)) { src = e.gameObject; break; }
            }
        if (src == null) return;
        var holder = new GameObject("Arena Templates (off)");
        holder.SetActive(false);
        holder.transform.SetParent(transform, false);
        pelichScale = src.transform.lossyScale;
        pelichTemplate = Instantiate(src, holder.transform);
        pelichTemplate.name = "Pelich Anus (Arena)";
        foreach (AudioSource a in pelichTemplate.GetComponentsInChildren<AudioSource>(true)) if (a.loop || a.playOnAwake) a.enabled = false;
    }

    private static GameObject SpawnPelich(Vector3 at)
    {
        if (pelichTemplate == null) { Debug.LogWarning("Arena: no Pelich to copy"); return null; }
        GameObject p = Instantiate(pelichTemplate, at, Quaternion.identity);
        p.name = pelichTemplate.name;
        p.transform.localScale = pelichScale;
        p.SetActive(true);
        PelichBoss boss = p.GetComponent<PelichBoss>();
        if (boss == null) boss = p.AddComponent<PelichBoss>();
        boss.arenaCopy = true;
        return p;
    }

    // Enemies of a long wave that are far from Rowdy (stuck on a ledge, in a wall, hiding at an edge) blink next to him
    private void PullStragglers()
    {
        if (BoonRunner.Rowdy == null) return;
        Vector3 r = BoonRunner.RowdyCenter;
        foreach (EnemyHealth e in alive)
        {
            if (e == null || e.enemydead) continue;
            Transform root = e.transform.root;
            if (Vector2.Distance(root.position, r) < 6f) continue;
            float x = Mathf.Clamp(r.x + (Random.value < 0.5f ? -3f : 3f), leftX + 1f, rightX - 1f);
            PulseRing.Spawn(root.position + Vector3.up * 0.5f, Theme, 1f, 0.3f);
            root.position = new Vector3(x, floorY + 1.5f, 0f);
            if (root.TryGetComponent(out Rigidbody2D rb)) { rb.position = root.position; rb.linearVelocity = Vector2.zero; }
            PulseRing.Spawn(root.position + Vector3.up * 0.5f, Theme, 1.4f, 0.35f);
            FXParticle.Burst(root.position, Theme, 10, 1f, 3f, 4f, 0.5f);
        }
    }

    // Anything hostile inside the arena that isn't counted yet (Elder's howl summons, strays that walked in)
    private void AdoptStrays()
    {
        for (int i = EnemyHealth.All.Count - 1; i >= 0; i--)
        {
            EnemyHealth e = EnemyHealth.All[i];
            if (e == null || !e.gameObject.activeInHierarchy || e.enemydead || e.IsObject || alive.Contains(e)) continue;
            Vector3 p = e.transform.position;
            if (p.x < leftX - 1f || p.x > rightX + 1f || p.y < floorY - 3f || p.y > floorY + 14f) continue;
            // only real fighters: bombs and arrows carry an EnemyHealth too (and a spent bomb that only gets switched
            // off never counts as dead - that held wave 5, the first one with Gnoll Bombers, open forever)
            if (e.GetComponentInParent<EnemyMovement>() == null && e.GetComponentInChildren<EnemyMovement>() == null && e.GetComponent<VoltRat>() == null) continue;
            EnemyCatalog.Entry kind = EnemyCatalog.Identify(e);
            if (kind != null && (kind.id == "pelican" || kind.id == "mantaray" || kind.id == "statue" || kind.id == "oldman" || kind.id == "pelich")) continue;
            alive.Add(e);
            foreach (EnemyMovement m in e.GetComponentsInParent<EnemyMovement>(true)) m.ArenaAggro();
            foreach (EnemyMovement m in e.GetComponentsInChildren<EnemyMovement>(true)) m.ArenaAggro();
        }
    }

    private static readonly string[] Champions = { "FRONTIER CHAMPION!", "LORD OF THE GLOOM!", "LONG LIVE THE PURPLE REIGN!" };

    private void Victory(int trial)
    {
        running = false;
        CurrentWave = 0;
        ArenaRun.LevelCap = 0;
        ArenaRun.HoldBoons = false; // everything banked + the prize picks open now
        ArenaRun.ClearSeal(Key);
        SetGates(false, false);
        PlayerPrefs.SetInt(Key + "Clears", ClearsHere + 1);
        PlayerPrefs.SetInt(Key + "Won", 1);
        PlayerPrefs.SetInt(Key + "Best", Mathf.Max(PlayerPrefs.GetInt(Key + "Best", 0), trial));
        PlayerPrefs.SetInt(Key + "Seal", 0); // next trial: from wave 1 again, tougher
        PlayerPrefs.DeleteKey(Key + "SealTrial");
        PlayerPrefs.Save();
        Banner.Show(Champions[Mathf.Clamp(tier, 0, 2)], null, Theme, 3f);
        BoonArt art = BoonArt.Get;
        if (art != null) { FXSound.Play(art.fanfare, 0.8f, 1f); FXSound.Play(art.sparkle, 0.6f, 1.1f); }
        ScreenShake.Impulse(0.6f);
        Vector3 c = BoonRunner.Rowdy != null ? BoonRunner.RowdyCenter : new Vector3((leftX + rightX) / 2f, floorY + 1f, 0f);
        PulseRing.Spawn(c, Theme, 4f, 0.6f);
        FXParticle.Burst(c, Theme, 40, 2f, 6f, 2f, 1f);
        GraftFX.Play("BRJ_FireCircle", c, new Color(Theme.r, Theme.g, Theme.b, 0.9f), 140);
        if (PlayerStats.Instance != null) PlayerStats.Instance.ReachLevel(PlayerStats.MaxLevel);
        ArenaRun.InTrial = false;
        // prizes: boons, hair gel (the later colosseums pay more)
        for (int i = 0; i <= tier; i++) Boons.DevOfferPick();
        Boons.AddRerolls(2 + 2 * tier);
        IconPopup.Show(c + Vector3.up * 1.6f, null, "+" + (tier + 1) + " BOON" + (tier > 0 ? "S" : "") + "  +" + (2 + 2 * tier) + " HAIR GEL", new Color(1f, 0.85f, 0.5f), 1f, 3f);
        if (ClearsHere == 1 && tier < 2)
            IconPopup.Show(c + Vector3.up * 2.4f, null, tier == 0 ? "THE GLOOM GATE IS OPEN" : "THE VIOLET GATE IS OPEN", new Color(0.8f, 0.55f, 1f), 1.1f, 3.5f);
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
            foreach (SpriteRenderer s in gate == leftBars ? leftStack : rightStack) if (s != null) s.enabled = gate.enabled;
        }
        foreach (SpriteRenderer b in new[] { leftBarrier, rightBarrier })
        {
            if (b == null) continue;
            float a = down * (0.22f + 0.08f * Mathf.Sin(Time.time * 5f + b.transform.position.x));
            b.color = new Color(Theme.r, Theme.g, Theme.b, a);
            b.enabled = a > 0.01f;
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

// The colosseum's little tracker, top middle: WAVE 3/30 and how many enemies are left in it (the ones still on their
// way count too). Shown while a trial runs, gone with it (victory, death, leaving the scene).
public class ArenaHUD : MonoBehaviour
{
    private static ArenaHUD instance;
    private FrontierArena arena;
    private PixelText wave, left;
    private Image panel;
    private CanvasGroup group;
    private int shownWave = -1, shownLeft = -1;
    private float bump;

    public static void Show(FrontierArena arena, Color theme)
    {
        if (instance == null)
        {
            RectTransform root = OverlayUI.MakeRect("Arena HUD", OverlayUI.Root);
            OverlayUI.Place(root, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(380f, 104f));
            instance = root.gameObject.AddComponent<ArenaHUD>();
            instance.group = root.gameObject.AddComponent<CanvasGroup>();
            instance.panel = OverlayUI.MakePanel("Panel", root);
            RectTransform p = instance.panel.rectTransform;
            p.anchorMin = Vector2.zero; p.anchorMax = Vector2.one; p.offsetMin = p.offsetMax = Vector2.zero;
            instance.wave = PixelText.Create(root, "", 5, theme, 0.5f);
            instance.wave.Rect.anchorMin = instance.wave.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            instance.wave.Rect.anchoredPosition = new Vector2(0f, 16f);
            instance.left = PixelText.Create(root, "", 3, new Color(1f, 0.9f, 0.95f), 0.5f);
            instance.left.Rect.anchorMin = instance.left.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            instance.left.Rect.anchoredPosition = new Vector2(0f, -24f);
        }
        instance.arena = arena;
        instance.wave.Color = theme;
        instance.shownWave = instance.shownLeft = -1;
        instance.gameObject.SetActive(true);
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void LateUpdate()
    {
        if (arena == null || !arena.IsRunning) { gameObject.SetActive(false); return; }
        bool hide = PauseMenu.IsPaused || BoonPicker.IsOpen || arena.CurrentWave <= 0;
        group.alpha = Mathf.MoveTowards(group.alpha, hide ? 0f : 1f, Time.unscaledDeltaTime * 6f);
        int w = arena.CurrentWave, n = arena.EnemiesLeft;
        if (w != shownWave) { shownWave = w; wave.SetText("WAVE " + Mathf.Max(1, w) + "/" + arena.WaveCount); bump = 1f; }
        if (n != shownLeft) { shownLeft = n; left.SetText(n == 0 ? "WAVE CLEAR" : n == 1 ? "1 ENEMY LEFT" : n + " ENEMIES LEFT"); }
        bump = Mathf.MoveTowards(bump, 0f, Time.unscaledDeltaTime * 4f);
        wave.Rect.localScale = Vector3.one * (1f + 0.25f * bump * bump);
    }
}

// Big announcer text across the middle of the screen: slams in, holds, fades
public class Banner : MonoBehaviour
{
    private static Banner instance;
    private PixelText title, sub;
    private Image band;
    private CanvasGroup group;
    private float shownAt = -10f, hold = 1.5f;

    public static void Show(string title, string sub, Color color, float hold = 1.5f)
    {
        sub = null; // banners are the main line only (user rule: no sub-messages under them)
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
        instance.hold = hold;
        UISound.Play(UISound.Cue.Open);
    }

    private void Update()
    {
        float t = Time.unscaledTime - shownAt;
        float alpha = t < 0.12f ? t / 0.12f : t < hold ? 1f : Mathf.Clamp01(1f - (t - hold) / 0.4f);
        group.alpha = alpha;
        float slam = t < 0.15f ? Mathf.Lerp(2.2f, 1f, t / 0.15f) : 1f + 0.02f * Mathf.Sin(t * 6f);
        title.Rect.localScale = Vector3.one * slam;
        band.rectTransform.sizeDelta = new Vector2(2200f, Mathf.Lerp(0f, 150f, Mathf.Clamp01(t / 0.1f)));
    }
}
