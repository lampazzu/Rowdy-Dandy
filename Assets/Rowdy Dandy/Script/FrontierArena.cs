using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// THE COLOSSEUMS (Hollow Knight's Colosseum of Fools idea). Two of them run on this:
//   THE FRONTIER   - the island past Pelich (scene object, made by Tools > Rowdy Dandy > Frontier > Build The Frontier)
//   THE GLOOMWOOD  - a gloomy-forest arena far away, built at runtime by GloomArena, reached through the Gloom Gate
//                    on the Frontier island once the Frontier has been cleared. Way harder.
// Ring the war gong ({INTERACT}) to start a trial: the gates slam shut and 30 WAVES come in (ArenaWaves), each a
// different group. Arena enemies always know where Rowdy is (EnemyMovement.ArenaAggro) - no hunting for strays.
// Every 5th wave: a big breather (heal + EXP). Waves 10 and 20 are SEALS: a boon + hair gel, and if Rowdy dies
// after one, the next trial starts from there. Clear wave 30: boons, hair gel, a pile of EXP, the title - and the
// next trial starts over at wave 1, tougher (+20% / +25% enemy health per clear).
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
    [Tooltip("The Gloomwood (set by GloomArena when it builds the second colosseum)")]
    public bool gloom;

    public static int Clears => PlayerPrefs.GetInt("RD_FrontierClears", 0);
    public static int GloomClears => PlayerPrefs.GetInt("RD_GloomClears", 0);

    private static readonly List<FrontierArena> arenas = new List<FrontierArena>();
    public static bool Running { get { foreach (FrontierArena a in arenas) if (a != null && a.running) return true; return false; } }
    // the island and everything past it (the Gloomwood is far to the right): no random waves there
    public static bool IsQuiet(Vector2 p) { foreach (FrontierArena a in arenas) if (a != null && !a.gloom && p.x >= a.islandStartX) return true; return false; }
    public static bool InGloom(Vector2 p) => p.x >= GloomArena.MinX;
    public static FrontierArena Frontier { get { foreach (FrontierArena a in arenas) if (a != null && !a.gloom) return a; return null; } }

    private string Key => gloom ? "RD_Gloom" : "RD_Frontier";
    private int ClearsHere => PlayerPrefs.GetInt(Key + "Clears", 0);
    private int Seal => PlayerPrefs.GetInt(Key + "Seal", 0);
    private string Title => gloom ? "THE GLOOMWOOD" : "THE FRONTIER";
    private Color Theme => gloom ? new Color(0.72f, 0.55f, 1f) : new Color(1f, 0.8f, 0.35f);
    private ArenaWaves.Round[] Waves => gloom ? ArenaWaves.Gloom : ArenaWaves.Frontier;

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
        if (gloom) bars.color = new Color(0.7f, 0.6f, 0.9f);
        return col;
    }

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
            int seal = Seal;
            Banner.Show(Title, ClearsHere > 0 ? "TRIALS CLEARED: " + ClearsHere : seal > 0 ? "SEAL REACHED: WAVE " + (seal + 1) : "RING THE GONG IF YOU DARE", Theme);
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
            IconPopup.Show(gong.position + Vector3.up * 2.6f, null, Seal > 0 ? "WAVE " + (Seal + 1) + " AWAITS" : "TRIAL " + (ClearsHere + 1) + " AWAITS", Theme, 0.7f, 1.6f);
    }

    // ---------------------------------------------------------------- the trial
    private float HealthScale(int wave)
    {
        float encore = 1f + 0.03f * Encore.Level + (Encore.BloodMoon ? 0.3f : 0f);
        return gloom ? (1.8f + 0.07f * wave) * (1f + 0.25f * ClearsHere) * encore
                     : (1f + 0.035f * wave) * (1f + 0.2f * ClearsHere) * encore;
    }

    private IEnumerator Trial()
    {
        running = true;
        RingGong();
        int trial = ClearsHere + 1;
        int start = Mathf.Clamp(Seal, 0, Waves.Length - 1);
        yield return new WaitForSeconds(0.6f);
        SetGates(true, false);
        Banner.Show(Title, "TRIAL " + trial + (start > 0 ? "  -  FROM THE SEAL: WAVE " + (start + 1) : "  -  30 WAVES"), Theme, 2f);
        yield return new WaitForSeconds(2.6f);

        ArenaWaves.Round[] waves = Waves;
        for (int w = start; w < waves.Length; w++)
        {
            bool boss = (w + 1) % 10 == 0;
            Banner.Show("WAVE " + (w + 1) + " / " + waves.Length, waves[w].title, boss ? new Color(1f, 0.3f, 0.35f) : Color.white);
            FXSound.Play(BoonArt.Get != null ? BoonArt.Get.open : null, 0.5f, boss ? 0.8f : 1f);
            yield return new WaitForSeconds(1.3f);
            Banner.Show("FIGHT!", null, Theme, 0.6f);
            ScreenShake.Impulse(boss ? 0.7f : 0.4f);
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
            yield return new WaitForSeconds(0.7f);
            if (w == waves.Length - 1) break;
            yield return Breather(w);
        }
        Victory(trial);
    }

    private IEnumerator Breather(int w)
    {
        int n = w + 1;
        Health h = BoonRunner.Rowdy != null ? BoonRunner.Rowdy.GetComponent<Health>() : null;
        if (n % 10 == 0)
        {
            // a SEAL: dying from here on restarts the trial at the next wave
            PlayerPrefs.SetInt(Key + "Seal", n);
            PlayerPrefs.Save();
            Banner.Show("SEAL " + (n == 10 ? "I" : "II") + " BROKEN", "A BOON FOR YOU.  DIE NOW AND YOU START AT WAVE " + (n + 1), Theme, 2.4f);
            if (h != null) h.AddHealth(h.startingHealth * 0.5f, false);
            Boons.AddRerolls(1);
            if (PlayerStats.Instance != null) PlayerStats.Instance.AddEXP((gloom ? 260f : 120f) * n / 10f * 3f);
            yield return new WaitForSeconds(1.2f);
            Boons.DevOfferPick();
            yield return null;
            while (BoonPicker.IsOpen) yield return null;
            yield return new WaitForSeconds(1.5f);
        }
        else if (n % 5 == 0)
        {
            Banner.Show("WAVE " + n + " CLEAR", "BIG BREATHER: +30% HEALTH, EXP", new Color(0.55f, 1f, 0.6f), 1.8f);
            if (h != null) h.AddHealth(h.startingHealth * 0.3f, false);
            if (PlayerStats.Instance != null) PlayerStats.Instance.AddEXP((gloom ? 160f : 70f) * n / 5f);
            yield return new WaitForSeconds(2.8f);
        }
        else
        {
            Banner.Show("WAVE " + n + " CLEAR", n % 2 == 0 ? "KEEP IT PRETTY" : "A BREATHER, BABY", new Color(0.55f, 1f, 0.6f), 0.9f);
            if (h != null) h.AddHealth(h.startingHealth * 0.1f, false);
            yield return new WaitForSeconds(1.6f);
        }
    }

    private IEnumerator SpawnRound(ArenaWaves.Round round, float hp, int wave)
    {
        var pending = new List<(ArenaWaves.Spawn s, float at, int n)>();
        float start = Time.time;
        foreach (ArenaWaves.Spawn s in round.spawns)
            for (int n = 0; n < s.count; n++) pending.Add((s, start + s.delay + n * s.every, n));
        int side = Random.value < 0.5f ? -1 : 1;
        while (pending.Count > 0)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (Time.time < pending[i].at) continue;
                SpawnOne(pending[i].s, hp, side, wave, pending[i].n == 0);
                side = -side;
                pending.RemoveAt(i);
            }
            if (BoonRunner.Rowdy == null) yield break;
            yield return null;
        }
    }

    private void SpawnOne(ArenaWaves.Spawn s, float hp, int side, int wave, bool firstOfGroup)
    {
        if (s.Has('<')) side = -1;
        if (s.Has('>')) side = 1;
        bool flying = s.Has('f'), ranged = s.Has('r'), sky = s.Has('s');
        float x = ranged ? (side < 0 ? leftX + 1.2f : rightX - 1.2f) : Mathf.Lerp(leftX + 1.5f, rightX - 1.5f, side < 0 ? Random.Range(0f, 0.35f) : Random.Range(0.65f, 1f));
        if (sky) x = Mathf.Clamp(BoonRunner.Rowdy != null ? BoonRunner.RowdyCenter.x + Random.Range(-4f, 4f) : x, leftX + 1f, rightX - 1f);
        float y = flying ? floorY + Random.Range(2.5f, 4f) : floorY + 0.3f;

        GameObject enemy;
        if (s.prefab == ArenaWaves.Elder)
        {
            enemy = CursedElder.Spawn(new Vector3(x, floorY + 0.3f, 0f));
            if (enemy == null) return;
            Banner.Show("THE MOONBOUND ELDER", "HAS ENTERED THE ARENA", new Color(0.7f, 0.6f, 1f), 1.4f);
        }
        else
        {
            GameObject prefab = Resources.Load<GameObject>("Enemies Prefab/" + s.prefab);
            if (prefab == null) { Debug.LogWarning("Arena: no prefab " + s.prefab); return; }
            enemy = Instantiate(prefab, new Vector3(x, y, 0f), Quaternion.identity);
            enemy.name = prefab.name;
        }

        bool elite = s.Has('E') || (s.Has('e') && firstOfGroup) || (gloom && Random.value < 0.1f + 0.01f * wave);
        if (elite) EliteEnemy.Apply(enemy, 2f, 2.5f, 25f, 1f); // no size change: pixel art stays at its own size
        EnemyHealth h = enemy.GetComponentInChildren<EnemyHealth>(true);
        if (h != null)
        {
            h.SetMaxHealth(h.startingenemyHealth * hp);
            if (!alive.Contains(h)) alive.Add(h);
        }
        foreach (EnemyMovement m in enemy.GetComponentsInChildren<EnemyMovement>(true)) m.ArenaAggro();

        if (!flying)
        {
            Collider2D col = enemy.GetComponentInChildren<Collider2D>();
            if (col != null)
            {
                Physics2D.SyncTransforms();
                float bottom = enemy.transform.position.y - col.bounds.min.y;
                enemy.transform.position = new Vector3(x, (sky ? floorY + 7f : floorY) + bottom + 0.05f, 0f);
            }
        }
        // face Rowdy (the art faces left at +x scale)
        Vector3 sc = enemy.transform.localScale;
        if (BoonRunner.Rowdy != null) sc.x = Mathf.Abs(sc.x) * (BoonRunner.RowdyCenter.x > x ? -1f : 1f);
        enemy.transform.localScale = sc;
        // a burst where they come in (a warning ring on the floor for the ones falling from the sky)
        Vector3 at = sky ? new Vector3(x, floorY + 0.1f, 0f) : enemy.transform.position + Vector3.up * 0.5f;
        Color ring = elite ? EliteEnemy.OutlineColor : gloom ? new Color(0.7f, 0.5f, 1f, 0.9f) : new Color(1f, 0.8f, 0.4f, 0.9f);
        PulseRing.Spawn(at, ring, sky ? 1.6f : 1.2f, sky ? 0.6f : 0.35f);
        FXParticle.Burst(at, gloom ? new Color(0.45f, 0.35f, 0.6f) : new Color(0.9f, 0.7f, 0.45f), 12, 1f, 3f, 6f, 0.5f, true);
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
        foreach (EnemyHealth e in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (e == null || e.enemydead || e.IsObject || alive.Contains(e)) continue;
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

    private void Victory(int trial)
    {
        running = false;
        SetGates(false, false);
        PlayerPrefs.SetInt(Key + "Clears", ClearsHere + 1);
        PlayerPrefs.SetInt(Key + "Best", Mathf.Max(PlayerPrefs.GetInt(Key + "Best", 0), trial));
        PlayerPrefs.SetInt(Key + "Seal", 0); // next trial: from wave 1 again, tougher
        PlayerPrefs.Save();
        Banner.Show(gloom ? "LORD OF THE GLOOM!" : "FRONTIER CHAMPION!", "TRIAL " + trial + " CLEARED  -  ALL 30 WAVES", Theme, 3f);
        BoonArt art = BoonArt.Get;
        if (art != null) { FXSound.Play(art.fanfare, 0.8f, 1f); FXSound.Play(art.sparkle, 0.6f, 1.1f); }
        ScreenShake.Impulse(0.6f);
        Vector3 c = BoonRunner.Rowdy != null ? BoonRunner.RowdyCenter : new Vector3((leftX + rightX) / 2f, floorY + 1f, 0f);
        PulseRing.Spawn(c, Theme, 4f, 0.6f);
        FXParticle.Burst(c, Theme, 40, 2f, 6f, 2f, 1f);
        // rewards: boons, hair gel, EXP (the Gloomwood pays double)
        Boons.DevOfferPick();
        if (gloom) Boons.DevOfferPick();
        Boons.AddRerolls(gloom ? 4 : 2);
        IconPopup.Show(c + Vector3.up * 1.6f, null, gloom ? "+2 BOONS  +4 HAIR GEL" : "+1 BOON  +2 HAIR GEL", new Color(1f, 0.85f, 0.5f), 1f, 3f);
        if (PlayerStats.Instance != null) PlayerStats.Instance.AddEXP((gloom ? 1500f : 600f) + 150f * trial);
        if (!gloom && ClearsHere == 1)
            IconPopup.Show(c + Vector3.up * 2.4f, null, "THE GLOOM GATE IS OPEN", new Color(0.72f, 0.55f, 1f), 1.1f, 3.5f);
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
    private float shownAt = -10f, hold = 1.5f;

    public static void Show(string title, string sub, Color color, float hold = 1.5f)
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
