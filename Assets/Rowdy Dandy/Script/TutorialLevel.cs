using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// DANDY SCHOOL - the tutorial level. A portal on the first island (just after the first stretch of water you surf
// across) takes Rowdy to a long walk-through level far to the left of the map: one STATION per mechanic, left to right
// (moving, jumping, thin platforms, the surf dash, water, blue plants / jellies, weapons, ore rocks, the counter, the
// checkpoint, cats and rats, levels / boons / colosseums / the moon, day and night, the menus). Walking into a
// station shows its tutorial cards (Tutorials) and a hint keeps floating over its sign. Portals at both ends go back.
// The tutorial cards ONLY show in here (Tutorials.Show checks TutorialLevel.Inside).
//
// EDITING IT: Tools > Rowdy Dandy > Level Editing > Bake Tutorial Level Into Scene puts the whole level into the open
// scene as normal objects (ground tilemap, platforms, the pool, props, stations with their signs). Decorate it, move
// things, edit each station (TutorialStation: title, hint, which cards, its zone) in the Inspector. At Play the game
// uses the baked one as it is; without one it builds this same default level at runtime.
//   - "Tutorial Portal Spot" (any scene object with that name) moves the portal into the level.
//   - The level root's TutorialLevel component: arrival / back portal / exit portal spots are its child transforms.
// A checkpoint touched in here is forgotten when you leave (your old one comes back), so dying out in the world never
// sends you back to school.
public class TutorialLevel : MonoBehaviour
{
    public const float DefaultX = -440f, Length = 172f;
    private static readonly Color Theme = new Color(0.45f, 1f, 0.85f);

    [Tooltip("Left end of the level (world x)")] public float left = DefaultX;
    [Tooltip("Right end of the level (world x)")] public float right = DefaultX + Length;
    [Tooltip("Ground height (world y)")] public float floorY = -3.4f;
    public Transform arrivalSpot, backPortalSpot, exitPortalSpot;

    public static TutorialLevel Instance { get; private set; }
    public static bool Inside { get; private set; }
    private static readonly HashSet<Tutorials.Topic> shown = new HashSet<Tutorials.Topic>();
    private static Vector3 worldPortal;

    private TutorialStation[] stations = new TutorialStation[0];

    // A card shows once per visit (walking out and back in shows them again)
    public static bool FirstThisVisit(Tutorials.Topic topic) => shown.Add(topic);

    public static bool Contains(Vector2 p)
    {
        TutorialLevel t = Instance;
        return t != null && p.x >= t.left - 6f && p.x <= t.right + 6f && p.y > t.floorY - 20f && p.y < t.floorY + 30f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { Instance = null; Inside = false; shown.Clear(); }

    private void Awake()
    {
        if (!Application.isPlaying) return;
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) { Instance = null; Inside = false; } }

    private void Start()
    {
        if (!Application.isPlaying) return;
        stations = GetComponentsInChildren<TutorialStation>(true);
    }

    // ---------------------------------------------------------------- setup (GloomArena.Build, once per scene load)
    public static void Setup()
    {
        TutorialLevel level = FindFirstObjectByType<TutorialLevel>(FindObjectsInactive.Include);
        if (level == null)
        {
            GameObject root = TutorialBuilder.Build(new Vector3(DefaultX, FloorGuess(), 0f));
            level = root != null ? root.GetComponent<TutorialLevel>() : null;
        }
        if (level == null) { Debug.LogWarning("Tutorial level: could not build it"); return; }
        Instance = level;
        level.gameObject.SetActive(true);

        // the way in: on the first island, after the first water
        worldPortal = FirstIslandSpot();
        GloomGate.Make(worldPortal, "DANDY SCHOOL (TUTORIAL)", () => true, null, TravelIn, Theme);
        if (level.backPortalSpot != null) GloomGate.Make(level.backPortalSpot.position, "BACK TO THE BEACH", () => true, null, TravelOut, Theme);
        if (level.exitPortalSpot != null) GloomGate.Make(level.exitPortalSpot.position, "BACK TO THE BEACH", () => true, null, TravelOut, Theme);
        Debug.Log("Tutorial level: portal at (" + worldPortal.x.ToString("0.0") + ", " + worldPortal.y.ToString("0.0") + ")");
    }

    // the beach floor height near the start (the level is built at the same height, so the parallax matches)
    private static float FloorGuess()
    {
        FrontierArena f = FrontierArena.Frontier;
        return f != null ? f.floorY : -3.4f;
    }

    public static void DevTravel()
    {
        if (Instance == null) Setup();
        TravelIn();
    }

    private static void TravelIn()
    {
        TutorialLevel t = Instance;
        if (t == null) return;
        SaveRespawn();
        Vector3 at = t.arrivalSpot != null ? t.arrivalSpot.position : new Vector3(t.left + 4f, t.floorY + 0.6f, 0f);
        shown.Clear();
        GloomArena.Travel(at + Vector3.up * 0.6f, "DANDY SCHOOL", null, Theme);
    }

    private static void TravelOut()
    {
        RestoreRespawn();
        GloomArena.Travel(worldPortal + new Vector3(1.6f, 0.6f, 0f), null, null, Theme);
    }

    // ---------------------------------------------------------------- the checkpoint in here doesn't stick
    private const string PrevX = "RD_TutPrevRespawnX", PrevY = "RD_TutPrevRespawnY";

    private static void SaveRespawn()
    {
        if (!PlayerPrefs.HasKey("RespawnX")) { PlayerPrefs.DeleteKey(PrevX); PlayerPrefs.DeleteKey(PrevY); return; }
        Vector2 r = new Vector2(PlayerPrefs.GetFloat("RespawnX"), PlayerPrefs.GetFloat("RespawnY"));
        if (Contains(r)) return; // already a school one: keep the real one saved before
        PlayerPrefs.SetFloat(PrevX, r.x);
        PlayerPrefs.SetFloat(PrevY, r.y);
        PlayerPrefs.Save();
    }

    private static void RestoreRespawn()
    {
        if (!PlayerPrefs.HasKey("RespawnX")) return;
        Vector2 r = new Vector2(PlayerPrefs.GetFloat("RespawnX"), PlayerPrefs.GetFloat("RespawnY"));
        if (!Contains(r)) return;
        if (PlayerPrefs.HasKey(PrevX)) { PlayerPrefs.SetFloat("RespawnX", PlayerPrefs.GetFloat(PrevX)); PlayerPrefs.SetFloat("RespawnY", PlayerPrefs.GetFloat(PrevY)); }
        else { PlayerPrefs.DeleteKey("RespawnX"); PlayerPrefs.DeleteKey("RespawnY"); }
        PlayerPrefs.Save();
    }

    // ---------------------------------------------------------------- where the portal goes in the world
    // Right next to the first checkpoint (the left-most one in the world): a free floor spot 2.5 units to its right,
    // else to its left. A scene object named "Tutorial Portal Spot" overrides it.
    private static Vector3 FirstIslandSpot()
    {
        GameObject manual = GameObject.Find("Tutorial Portal Spot");
        if (manual != null) return manual.transform.position;
        Vector2 start = new Vector2(float.MaxValue, 0f);
        foreach (RespawnTrigger r in FindObjectsByType<RespawnTrigger>(FindObjectsSortMode.None))
            if (r.transform.position.x < start.x && !FrontierArena.InGloom(r.transform.position)) start = r.transform.position;
        if (start.x == float.MaxValue) start = Vector2.zero;
        foreach (float dx in new[] { 2.5f, 3.2f, 2f, -2.5f, -3.2f, 4f, -4f })
            if (SolidGround.Ray(new Vector2(start.x + dx, start.y + 3f), Vector2.down, 8f, out RaycastHit2D spot)
                && !SolidGround.Blocked(spot.point + new Vector2(0f, 1.1f), new Vector2(0.8f, 1.6f)))
                return spot.point;
        Debug.LogWarning("Tutorial level: no floor next to the first checkpoint; the portal stands on it");
        return start + Vector2.right * 3f;
    }

    // ---------------------------------------------------------------- every frame
    private void Update()
    {
        if (!Application.isPlaying) return;
        Transform rowdy = BoonRunner.Rowdy;
        bool inside = rowdy != null && Contains(BoonRunner.RowdyCenter);
        if (inside && !Inside) shown.Clear(); // a new visit: every card shows again
        Inside = inside;
        if (!inside || PauseMenu.IsPaused) return;
        Vector3 c = BoonRunner.RowdyCenter;
        foreach (TutorialStation s in stations) if (s != null) s.Tick(c);
    }
}

// Builds the default DANDY SCHOOL (at runtime, or into the scene by the editor bake). Ground = a stretch of the
// Frontier's beach tiles repeated (the same art as the islands), thin platforms of the sand pass-through tile, a
// pool, the level's own prefabs (blue plant, jelly, weapons, checkpoint, wolf statues), signs.
public static class TutorialBuilder
{
    private static Sprite sign;

    public static GameObject Build(Vector3 origin)
    {
        float L = origin.x, Y = origin.y;
        var root = new GameObject("Tutorial Level (DANDY SCHOOL)");
        root.transform.position = Vector3.zero;
        TutorialLevel level = root.AddComponent<TutorialLevel>();
        level.left = L;
        level.right = L + TutorialLevel.Length;
        level.floorY = Y;

        GameObject beachGo = GameObject.Find("BeachMap");
        Tilemap ground = Ground(root, beachGo, L - 8f, L + TutorialLevel.Length + 8f, Y);
        Physics2D.SyncTransforms();
        if (SolidGround.Ray(new Vector2(L + 6f, Y + 4f), Vector2.down, 10f, out RaycastHit2D fh)) Y = level.floorY = fh.point.y;

        // walls at both ends, a floor far below
        Wall(root, beachGo, new Vector2(L - 2f, Y + 10f), new Vector2(1f, 40f));
        Wall(root, beachGo, new Vector2(L + TutorialLevel.Length + 2f, Y + 10f), new Vector2(1f, 40f));
        Wall(root, beachGo, new Vector2(L + TutorialLevel.Length / 2f, Y - 14f), new Vector2(TutorialLevel.Length + 20f, 1f));

        level.arrivalSpot = Spot(root, "Arrival Spot", new Vector3(L + 4f, Y, 0f));
        level.backPortalSpot = Spot(root, "Back Portal Spot", new Vector3(L + 1.5f, Y, 0f));
        level.exitPortalSpot = Spot(root, "Exit Portal Spot", new Vector3(L + 168f, Y, 0f));
        var props = new GameObject("Props");
        props.transform.SetParent(root.transform, false);
        var decor = new GameObject("Decor (yours)");
        decor.transform.SetParent(root.transform, false);
        Color plat = new Color(1f, 1f, 1f);

        // 1 - welcome
        Station(root, "WELCOME", L + 7f, Y, 7f, 5f, "{MOVE} MOVE   {JUMP} JUMP   {ATTACK} ATTACK", Tutorials.Topic.Controls);
        // 2 - jumping: stairs of platforms, the last one needs a full (held) jump
        Station(root, "JUMPING", L + 15f, Y, 3f, 13f, "HOLD {JUMP} TO JUMP HIGHER", Tutorials.Topic.Jump);
        GloomArena.Platforms(props, L + 18f, L + 18f, Y, plat, new[] { (0f, 0.85f, 2) });
        GloomArena.Platforms(props, L + 21f, L + 21f, Y, plat, new[] { (0f, 1.7f, 2) });
        GloomArena.Platforms(props, L + 24.5f, L + 24.5f, Y, plat, new[] { (0f, 2.55f, 3) }); // ~0.85 a step: Rowdy reaches ~1.3
        // 3 - thin platforms: up through, down through
        Station(root, "THIN PLATFORMS", L + 31f, Y, 3f, 9f, "{DOWN} + {JUMP} DROPS THROUGH", Tutorials.Topic.Platform);
        GloomArena.Platforms(props, L + 35f, L + 35f, Y, plat, new[] { (0f, 0.9f, 5) });
        GloomArena.Platforms(props, L + 35f, L + 35f, Y, plat, new[] { (0f, 1.8f, 5) });
        // 4 - surf dash: a runway with statues to dash through
        Station(root, "SURF DASH", L + 43f, Y, 3f, 13f, "{SURF} WHILE MOVING: SURF DASH", Tutorials.Topic.SurfDash);
        Prefab(props, "Enemies Prefab/Breakables/RDR_WolfStatues_A", L + 50f, Y, true, 0f, true);
        Prefab(props, "Enemies Prefab/Breakables/RDR_WolfStatues_B", L + 53.5f, Y, true, 0f, true);
        // 5 - water
        Station(root, "WATER", L + 59f, Y, 3f, 13f, "SURF THE WATER.  {JUMP} HOPS OUT", Tutorials.Topic.Water);
        if (ground != null) GloomArena.PoolAt(props, L + 65f, Y + GloomArena.ArtSink(beachGo), ground);
        // 6 - blue plant + jelly up to a high ledge
        Station(root, "BOUNCY THINGS", L + 75f, Y, 3f, 13f, "HIT THE BLUE PLANT, THEN STAND IN IT", Tutorials.Topic.BluePlant);
        Prefab(props, "Interactables/BluePlant ", L + 79f, Y, true, 0f, true);
        GloomArena.Platforms(props, L + 80.5f, L + 80.5f, Y, plat, new[] { (0f, 2f, 4) }); // over the plant: hit it, it launches you ~2.5 up (play-tested)
        GloomArena.Platforms(props, L + 84.5f, L + 84.5f, Y, plat, new[] { (0f, 1f, 2) });  // ... and a step back down
        Prefab(props, "Interactables/Jelly", L + 86f, Y + 0.8f, false); // low: hop onto it to bounce
        // 7 - weapons
        Station(root, "WEAPONS", L + 91f, Y, 3f, 9f, "{INTERACT} PICK UP   {SWITCH} SWITCH", Tutorials.Topic.Weapon);
        Prefab(props, "Systems/WeaponDropSword", L + 94.5f, Y, true, 0.3f);
        Prefab(props, "Systems/WeaponDropNaginata", L + 96.5f, Y, true, 0.3f);
        Prefab(props, "Systems/WeaponDropCleaver", L + 98.5f, Y, true, 0.3f);
        // 8 - ore rocks and statues
        Station(root, "SMASH IT", L + 103f, Y, 3f, 9f, "SMASH THE ORE ROCK", Tutorials.Topic.Breakables);
        var ore = new GameObject("Ore Rock");
        ore.transform.SetParent(props.transform, false);
        ore.transform.position = new Vector3(L + 107f, Y - 0.02f, 0f);
        ore.AddComponent<OreNode>().transient = true;
        Prefab(props, "Enemies Prefab/Breakables/RDR_WolfStatues_C", L + 110f, Y, true, 0f, true);
        // 9 - the counter: a practice wolf that keeps coming back
        TutorialStation counter = Station(root, "COUNTER", L + 115f, Y, 3f, 13f, "HIT IT AS IT ATTACKS: COUNTER!", Tutorials.Topic.Counter, Tutorials.Topic.Hurt);
        counter.practiceEnemy = "Enemy_BigWerewolf";
        counter.enemySpot = Spot(counter.gameObject, "Enemy Spot", new Vector3(L + 124f, Y, 0f));
        // 10 - checkpoint
        Station(root, "CHECKPOINT", L + 131f, Y, 3f, 7f, "TOUCH IT.  {INTERACT} TO REST", Tutorials.Topic.Checkpoint);
        Prefab(props, "Interactables/Spawner", L + 135f, Y, true, 0f, true);
        // 11 - cats and rats
        Station(root, "CATS AND RATS", L + 141f, Y, 3f, 7f, "CATS FIGHT FOR YOU", Tutorials.Topic.Cat, Tutorials.Topic.Rat);
        // 12 - levels, boons, colosseums, the moon (a gong to look at)
        Station(root, "LEVELS AND BOONS", L + 151f, Y, 3f, 7f, "LEVEL UP.  RING A GONG.  GET BOONS.", Tutorials.Topic.LevelUp, Tutorials.Topic.Colosseum, Tutorials.Topic.Boons, Tutorials.Topic.Werewolf);
        SpriteRenderer gong = Renderer(props, "Gong (look only)", FrontierArena.GongSprite, new Vector3(L + 155f, Y, 0f), 8);
        if (gong != null && ItemArt.Lit != null) gong.sharedMaterial = ItemArt.Lit;
        // 13 - day / night, the menus
        Station(root, "DAY, NIGHT, MENUS", L + 161f, Y, 3f, 4f, "{MAP} MAP   {NOTES} NOTES   {STATS} STATS", Tutorials.Topic.DayNight, Tutorials.Topic.Menus);
        // 14 - goodbye, the exit
        Station(root, "GRADUATION", L + 166f, Y, 1f, 6f, "BACK TO THE BEACH", Tutorials.Topic.Goodbye);
        return root;
    }

    // ---------------------------------------------------------------- pieces
    private static TutorialStation Station(GameObject root, string title, float x, float floorY, float zoneLeft, float zoneRight, string hint, params Tutorials.Topic[] topics)
    {
        var holder = root.transform.Find("Stations");
        if (holder == null) { holder = new GameObject("Stations").transform; holder.SetParent(root.transform, false); }
        float y = SolidGround.Ray(new Vector2(x, floorY + 3f), Vector2.down, 8f, out RaycastHit2D g) ? g.point.y : floorY;
        var go = new GameObject("Station - " + title);
        go.transform.SetParent(holder, false);
        go.transform.position = new Vector3(x, y, 0f);
        TutorialStation s = go.AddComponent<TutorialStation>();
        s.title = title;
        s.hint = hint;
        s.topics = topics;
        s.zoneLeft = zoneLeft;
        s.zoneRight = zoneRight;
        s.sign = Renderer(go, "Sign", SignSprite, go.transform.position, 6);
        if (s.sign != null && ItemArt.Lit != null) s.sign.sharedMaterial = ItemArt.Lit;
        return s;
    }

    private static SpriteRenderer Renderer(GameObject parent, string name, Sprite sprite, Vector3 at, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    private static Transform Spot(GameObject parent, string name, Vector3 at)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.position = at;
        return go.transform;
    }

    private static void Wall(GameObject root, GameObject like, Vector2 center, Vector2 size)
    {
        var go = new GameObject("Wall");
        if (like != null) { go.layer = like.layer; go.tag = like.tag; }
        go.transform.SetParent(root.transform, false);
        go.transform.position = center;
        go.AddComponent<BoxCollider2D>().size = size;
    }

    // a level prefab standing on the floor (linked to its prefab when baked in the editor)
    private static GameObject Prefab(GameObject parent, string path, float x, float y, bool onFloor = true, float lift = 0f, bool sit = false)
    {
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null) { Debug.LogWarning("Tutorial level: no prefab " + path); return null; }
        Vector3 at = new Vector3(x, y, 0f);
        if (onFloor && SolidGround.Ray(new Vector2(x, y + 3f), Vector2.down, 8f, out RaycastHit2D g)) at = new Vector3(x, g.point.y + lift, 0f);
        GameObject go;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            go.transform.position = at;
        }
        else
#endif
        {
            go = Object.Instantiate(prefab, at, Quaternion.identity, parent.transform);
            go.name = prefab.name;
        }
        // sit: its art stands ON the floor like the hand-placed ones (their pivots are not at their feet: a checkpoint
        // placed by its pivot sank 1.1 into the ground, a blue plant 0.5)
        if (sit && onFloor)
        {
            SpriteRenderer main = MainSprite(go);
            if (main != null) go.transform.position += Vector3.up * (at.y - lift - 0.06f - main.bounds.min.y);
        }
        return go;
    }

    // the prop's own picture: the tallest sprite that isn't an effect (fog, beams, prompts, glows)
    private static SpriteRenderer MainSprite(GameObject go)
    {
        SpriteRenderer best = null;
        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            string n = sr.name.ToLowerInvariant();
            if (n.Contains("fog") || n.Contains("beam") || n.Contains("prompt") || n.Contains("glow") || n.Contains("sparkle") || n.Contains("shadow")) continue;
            if (best == null || sr.bounds.size.y > best.bounds.size.y) best = sr;
        }
        return best;
    }
    // ---------------------------------------------------------------- the ground
    // A window of the Frontier's beach tiles (8 units wide, from just above its floor down 7 units) repeated along the
    // level. The tiles there are big sprites placed every few cells: the repeat step is a whole number of those.
    private static Tilemap Ground(GameObject root, GameObject beachGo, float x0, float x1, float surfaceY)
    {
        Tilemap beach = beachGo != null ? beachGo.GetComponent<Tilemap>() : null;
        FrontierArena frontier = null;
        foreach (FrontierArena a in Object.FindObjectsByType<FrontierArena>(FindObjectsSortMode.None)) if (!a.gloom) { frontier = a; break; }
        if (beach == null || beach.layoutGrid == null || frontier == null) { FlatGround(root, beachGo, x0, x1, surfaceY); return null; }

        float sx = (frontier.leftX + frontier.rightX) / 2f, sy = frontier.floorY;
        Grid srcGrid = beach.layoutGrid;
        var gridGo = new GameObject("Grid");
        gridGo.transform.SetParent(root.transform, false);
        gridGo.transform.SetPositionAndRotation(srcGrid.transform.position, srcGrid.transform.rotation);
        gridGo.transform.localScale = srcGrid.transform.lossyScale;
        Grid grid = gridGo.AddComponent<Grid>();
        grid.cellSize = srcGrid.cellSize; grid.cellGap = srcGrid.cellGap; grid.cellLayout = srcGrid.cellLayout; grid.cellSwizzle = srcGrid.cellSwizzle;

        var tmGo = new GameObject("Ground");
        tmGo.layer = beachGo.layer;
        tmGo.tag = beachGo.tag;
        tmGo.transform.SetParent(gridGo.transform, false);
        tmGo.transform.position = beach.transform.position;
        Tilemap tm = tmGo.AddComponent<Tilemap>();
        tm.tileAnchor = beach.tileAnchor;
        tm.orientation = beach.orientation;
        tm.color = beach.color;
        var tmr = tmGo.AddComponent<TilemapRenderer>();
        if (beachGo.TryGetComponent(out TilemapRenderer srcR)) { tmr.sharedMaterial = srcR.sharedMaterial; tmr.sortingLayerID = srcR.sortingLayerID; tmr.sortingOrder = srcR.sortingOrder; tmr.mode = srcR.mode; }

        // the sample window and the tiles in it
        Vector3Int a0 = beach.WorldToCell(new Vector3(sx - 4f, sy - 7f, 0f)), a1 = beach.WorldToCell(new Vector3(sx + 4f, sy + 1.5f, 0f));
        var tiles = new List<(Vector3Int c, TileBase t, Matrix4x4 m)>();
        for (int x = a0.x; x <= a1.x; x++)
            for (int y = a0.y; y <= a1.y; y++)
            {
                var c = new Vector3Int(x, y, 0);
                TileBase t = beach.GetTile(c);
                if (t != null) tiles.Add((c, t, beach.GetTransformMatrix(c)));
            }
        if (tiles.Count == 0) { Object.DestroyImmediate(gridGo); FlatGround(root, beachGo, x0, x1, surfaceY); return null; }
        // the step between tiles in a row (big sprites sit every few cells)
        int step = int.MaxValue;
        foreach (var p in tiles) foreach (var q in tiles) if (q.c.y == p.c.y && q.c.x > p.c.x) step = Mathf.Min(step, q.c.x - p.c.x);
        if (step == int.MaxValue || step <= 0) step = 1;
        int width = a1.x - a0.x + 1;
        int period = Mathf.Max(step, width / step * step);

        int shiftY = tm.WorldToCell(new Vector3(x0, surfaceY, 0f)).y - beach.WorldToCell(new Vector3(sx, sy, 0f)).y;
        int c0 = tm.WorldToCell(new Vector3(x0, surfaceY, 0f)).x, c1 = tm.WorldToCell(new Vector3(x1, surfaceY, 0f)).x;
        for (int baseX = c0; baseX <= c1; baseX += period)
            foreach (var p in tiles)
            {
                int dx = p.c.x - a0.x;
                if (dx >= period || baseX + dx > c1) continue;
                var at = new Vector3Int(baseX + dx, p.c.y + shiftY, 0);
                tm.SetTile(at, p.t);
                tm.SetTransformMatrix(at, p.m);
            }

        var body = tmGo.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        var tc = tmGo.AddComponent<TilemapCollider2D>();
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
        var comp = tmGo.AddComponent<CompositeCollider2D>();
        comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
        if (beachGo.TryGetComponent(out CompositeCollider2D srcComp)) { comp.sharedMaterial = srcComp.sharedMaterial; comp.offset = srcComp.offset; }
        if (beachGo.TryGetComponent(out TilemapCollider2D srcTc)) tc.offset = srcTc.offset;
        tc.ProcessTilemapChanges();
        comp.GenerateGeometry();
        return tm;
    }

    // no beach tiles to copy: a plain sandy strip (flat colour, so stretching it is fine)
    private static void FlatGround(GameObject root, GameObject like, float x0, float x1, float y)
    {
        var go = new GameObject("Ground (flat)");
        if (like != null) { go.layer = like.layer; go.tag = like.tag; }
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3((x0 + x1) / 2f, y - 3f, 0f);
        go.AddComponent<BoxCollider2D>().size = new Vector2(x1 - x0, 6f);
        var look = new GameObject("Look");
        look.transform.SetParent(go.transform, false);
        look.transform.localScale = new Vector3((x1 - x0) * 64f, 6f * 64f, 1f); // a 1 px sprite at 64 ppu
        var sr = look.AddComponent<SpriteRenderer>();
        sr.sprite = BoonFX.Pixel;
        sr.color = new Color(0.86f, 0.72f, 0.5f);
        sr.sortingOrder = 0;
    }

    // A wooden sign post, 20 x 26 px (AI placeholder: redraw it at AI Placeholders/Drawn In Code/TutorialSign.png)
    public static Sprite SignSprite
    {
        get
        {
            if (sign != null) return sign;
            // board 30 x 15 (planks, three lines of "writing", a nail), post 4 x 25, a little foot
            var rows = new string[40];
            for (int y = 0; y < 40; y++)
            {
                var r = new char[30];
                for (int x = 0; x < 30; x++)
                {
                    char ch = '.';
                    if (y < 15)
                    {
                        bool edge = x == 0 || x == 29 || y == 0 || y == 14;
                        bool rim = x == 1 || x == 28 || y == 1 || y == 13;
                        ch = edge ? 'k' : rim ? 'b' : (y == 5 || y == 9) ? 'b' : 'n';
                        if ((y == 3 && x >= 4 && x <= 24) || (y == 7 && x >= 4 && x <= 20) || (y == 11 && x >= 4 && x <= 15)) ch = 'a';
                        if (y == 11 && x == 22) ch = 't';
                    }
                    else if (y < 36) ch = x == 13 || x == 16 ? 'k' : x == 14 || x == 15 ? 'b' : '.';
                    else if (y < 39) ch = x == 10 || x == 19 ? 'k' : x > 10 && x < 19 ? 'b' : '.';
                    else ch = x >= 10 && x <= 19 ? 'k' : '.';
                    r[x] = ch;
                }
                rows[y] = new string(r);
            }
            sign = BoonFX.FromRows("TutorialSign", rows, c =>
            {
                switch (c)
                {
                    case 'k': return new Color32(0x1B, 0x08, 0x20, 0xFF);
                    case 'b': return new Color32(120, 74, 44, 255);
                    case 'n': return new Color32(176, 120, 72, 255);
                    case 'a': return new Color32(110, 255, 220, 255);
                    case 't': return new Color32(255, 220, 90, 255);
                }
                return new Color32(0, 0, 0, 0);
            }, new Vector2(0.5f, 0f));
            return sign;
        }
    }
}
