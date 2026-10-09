using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// THE GLOOMWOOD: the second colosseum, built at runtime (no scene edits) far east of the Frontier:
//   - the Frontier island's ground tiles copied Shift units to the right into their own tilemap, dyed dark,
//     with its own collider (same layer / tag as BeachMap) and walls at both ends
//   - decorated with the Gloomy Forest art (Resources/GloomArt: trees, horrific trees, bushes, glowing crystals...)
//   - its own night sky + the gloomy tree line as a parallax backdrop while the camera is there, fog and wisps
//   - a FrontierArena (gloom = true): 30 much harder waves (ArenaWaves.Gloom)
//   - THE GLOOM GATE on the Frontier island (right of the colosseum) leads there; sealed until the Frontier's 30
//     waves have been cleared once. A gate in the Gloomwood leads back.
// Dev tools > Travel > The Gloomwood unseals the gate and jumps straight in.
public static class GloomArena
{
    public const float Shift = 640f;
    public const float MinX = 860f; // everything right of this is the Gloomwood
    private const string DevOpenKey = "RD_GloomGateOpen";

    public static bool GateOpen => FrontierArena.Clears > 0 || PlayerPrefs.GetInt(DevOpenKey, 0) == 1;
    private static FrontierArena arena;
    private static Vector3 arrival, frontierReturn;

    private static readonly Color Dye = new Color(0.36f, 0.32f, 0.48f);          // the copied sand: dark soil
    private static readonly Color DecorTint = new Color(0.62f, 0.56f, 0.78f);
    public static readonly Color Purple = new Color(0.72f, 0.55f, 1f);

    public static void Build(FrontierArena frontier)
    {
        if (frontier == null) return;
        GameObject beachGo = GameObject.Find("BeachMap");
        Tilemap beach = beachGo != null ? beachGo.GetComponent<Tilemap>() : null;
        if (beach == null || beach.layoutGrid == null) { Debug.LogWarning("Gloomwood: BeachMap not found"); return; }

        var root = new GameObject("The Gloomwood (runtime)");
        float left = frontier.leftX + Shift, right = frontier.rightX + Shift;

        // ---- ground: the island's tiles, copied and dyed
        Grid srcGrid = beach.layoutGrid;
        var gridGo = new GameObject("Grid");
        gridGo.transform.SetParent(root.transform, false);
        gridGo.transform.SetPositionAndRotation(srcGrid.transform.position, srcGrid.transform.rotation);
        gridGo.transform.localScale = srcGrid.transform.lossyScale;
        Grid grid = gridGo.AddComponent<Grid>();
        grid.cellSize = srcGrid.cellSize;
        grid.cellGap = srcGrid.cellGap;
        grid.cellLayout = srcGrid.cellLayout;
        grid.cellSwizzle = srcGrid.cellSwizzle;

        var tmGo = new GameObject("Gloom Ground");
        tmGo.layer = beachGo.layer;
        tmGo.tag = beachGo.tag;
        tmGo.transform.SetParent(gridGo.transform, false);
        tmGo.transform.position = beach.transform.position;
        Tilemap tm = tmGo.AddComponent<Tilemap>();
        tm.tileAnchor = beach.tileAnchor;
        tm.orientation = beach.orientation;
        tm.color = Dye;
        var tmr = tmGo.AddComponent<TilemapRenderer>();
        if (beachGo.TryGetComponent(out TilemapRenderer srcR))
        {
            tmr.sharedMaterial = srcR.sharedMaterial;
            tmr.sortingLayerID = srcR.sortingLayerID;
            tmr.sortingOrder = srcR.sortingOrder;
            tmr.mode = srcR.mode;
        }
        Vector3Int a = beach.WorldToCell(new Vector3(frontier.leftX - 8f, frontier.floorY - 14f, 0f));
        Vector3Int b = beach.WorldToCell(new Vector3(frontier.rightX + 8f, frontier.floorY + 16f, 0f));
        Vector3Int shiftCells = beach.WorldToCell(new Vector3(frontier.leftX + Shift, frontier.floorY, 0f)) - beach.WorldToCell(new Vector3(frontier.leftX, frontier.floorY, 0f));
        for (int x = a.x; x <= b.x; x++)
            for (int y = a.y; y <= b.y; y++)
            {
                var c = new Vector3Int(x, y, 0);
                TileBase t = beach.GetTile(c);
                if (t == null) continue;
                tm.SetTile(c + shiftCells, t);
                tm.SetTransformMatrix(c + shiftCells, beach.GetTransformMatrix(c));
            }
        var body = tmGo.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        var tc = tmGo.AddComponent<TilemapCollider2D>();
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
        var comp = tmGo.AddComponent<CompositeCollider2D>();
        comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
        if (beachGo.TryGetComponent(out CompositeCollider2D srcComp)) comp.sharedMaterial = srcComp.sharedMaterial;
        tc.ProcessTilemapChanges();
        comp.GenerateGeometry();

        // walls at both ends + a floor far below (nothing to fall off into)
        Wall(root, beachGo, new Vector2(left - 7f, frontier.floorY + 6f), new Vector2(1f, 30f));
        Wall(root, beachGo, new Vector2(right + 7f, frontier.floorY + 6f), new Vector2(1f, 30f));
        Wall(root, beachGo, new Vector2((left + right) / 2f, frontier.floorY - 12f), new Vector2(40f, 1f));
        Physics2D.SyncTransforms();

        // ---- the arena itself (inactive while it's set up, so Awake sees the fields)
        var arenaGo = new GameObject("Colosseum (Gloomwood)");
        arenaGo.SetActive(false);
        arenaGo.transform.SetParent(root.transform, false);
        arena = arenaGo.AddComponent<FrontierArena>();
        arena.gloom = true;
        arena.leftX = left;
        arena.rightX = right;
        arena.floorY = frontier.floorY;
        arena.islandStartX = MinX;
        var gongGo = new GameObject("Gloom Gong");
        gongGo.transform.SetParent(root.transform, false);
        gongGo.transform.position = frontier.gong != null ? frontier.gong.position + Vector3.right * Shift : new Vector3(left + 3f, frontier.floorY, 0f);
        var gongSr = gongGo.AddComponent<SpriteRenderer>();
        gongSr.sprite = FrontierArena.GongSprite;
        gongSr.sortingOrder = 8;
        gongSr.color = new Color(0.75f, 0.65f, 1f);
        if (ItemArt.Lit != null) gongSr.sharedMaterial = ItemArt.Lit;
        arena.gong = gongGo.transform;
        arenaGo.SetActive(true);

        Decorate(root, left - 6f, right + 6f, arena.floorY);
        root.AddComponent<GloomSky>().Setup((left + right) / 2f, arena.floorY);

        // ---- the gates between the two arenas
        Vector3 frontierGate = OnFloor(new Vector3(frontier.rightX + 3.4f, frontier.floorY + 2f, 0f));
        Vector3 gloomGate = OnFloor(new Vector3(left - 4f, arena.floorY + 2f, 0f));
        arrival = OnFloor(new Vector3(left - 2.2f, arena.floorY + 2f, 0f)) + Vector3.up * 0.6f;
        frontierReturn = frontierGate + new Vector3(-1.4f, 0.6f, 0f);
        GloomGate.Make(frontierGate, true);
        GloomGate.Make(gloomGate, false);
    }

    private static void Wall(GameObject root, GameObject like, Vector2 center, Vector2 size)
    {
        var go = new GameObject("Gloom Wall");
        go.layer = like.layer;
        go.tag = like.tag;
        go.transform.SetParent(root.transform, false);
        go.transform.position = center;
        go.AddComponent<BoxCollider2D>().size = size;
    }

    private static Vector3 OnFloor(Vector3 p)
    {
        if (SolidGround.Ray(p + Vector3.up * 2f, Vector2.down, 12f, out RaycastHit2D hit)) return hit.point;
        return p;
    }

    // ---------------------------------------------------------------- travel
    public static void TravelToGloom() => Travel(arrival, true);
    public static void TravelToFrontier() => Travel(frontierReturn, false);

    public static void DevTravel()
    {
        PlayerPrefs.SetInt(DevOpenKey, 1);
        PlayerPrefs.Save();
        if (arena != null) TravelToGloom();
    }

    private static void Travel(Vector3 to, bool intoGloom)
    {
        Transform rowdy = BoonRunner.Rowdy;
        if (rowdy == null || to == Vector3.zero) return;
        Vector3 delta = to - rowdy.position;
        if (rowdy.TryGetComponent(out Rigidbody2D rb)) { rb.linearVelocity = Vector2.zero; rb.position = to; }
        rowdy.position = to;
        foreach (PetFollower pet in PetFollower.Pets)
            if (pet != null && pet.IsCollected) pet.transform.position += delta;
        Physics2D.SyncTransforms();
        CinemachineVirtualCamera cam = Object.FindFirstObjectByType<CinemachineVirtualCamera>();
        if (cam != null) { cam.OnTargetObjectWarped(rowdy, delta); cam.PreviousStateIsValid = false; }

        ScreenFlash.Play(new Color(0.45f, 0.25f, 0.7f, 0.85f), 0.6f);
        BoonArt art = BoonArt.Get;
        if (art != null) { FXSound.Play(art.whoosh, 0.6f, 0.7f); FXSound.Play(art.open, 0.4f, 0.6f); }
        PulseRing.Spawn(to, Purple, 2.2f, 0.5f);
        FXParticle.Burst(to, Purple, 26, 2f, 5f, 2f, 0.8f);
        ScreenShake.Impulse(0.4f);
        if (intoGloom) Banner.Show("THE GLOOMWOOD", "WAY HARDER. RING THE GONG IF YOU DARE", Purple, 2f);
    }

    // ---------------------------------------------------------------- decoration
    private static void Decorate(GameObject root, float from, float to, float floorY)
    {
        GloomArt art = GloomArt.Get;
        if (art == null) return;
        var rng = new System.Random(4242);
        var decor = new GameObject("Decor");
        decor.transform.SetParent(root.transform, false);

        // tree line behind everything (trunks sink behind the ground)
        for (float x = from - 6f; x < to + 6f; x += 1.6f + (float)rng.NextDouble() * 1.6f)
        {
            bool horrific = rng.NextDouble() < 0.3f;
            Sprite s = Pick(rng, horrific ? art.horrificTrees : art.trees);
            Place(decor, s, x, floorY, -25 - rng.Next(0, 5), DecorTint * (0.7f + 0.3f * (float)rng.NextDouble()), -0.35f, rng.NextDouble() < 0.5);
        }
        // a few big horrific trees further back, darker
        for (float x = from; x < to; x += 9f + (float)rng.NextDouble() * 4f)
            Place(decor, art.horrificTrees != null && art.horrificTrees.Length > 1 ? art.horrificTrees[1] : Pick(rng, art.horrificTrees), x, floorY, -40, DecorTint * 0.45f, -0.6f, rng.NextDouble() < 0.5);
        // ground cover in front of the tiles, behind the fighters
        for (float x = from; x < to; x += 0.9f + (float)rng.NextDouble() * 1.4f)
        {
            double r = rng.NextDouble();
            Sprite s = r < 0.35 ? Pick(rng, art.grass) : r < 0.6 ? Pick(rng, art.bushes) : r < 0.78 ? Pick(rng, art.flowers) : Pick(rng, art.rocks);
            Place(decor, s, x, floorY, -6, DecorTint, -0.08f, rng.NextDouble() < 0.5);
        }
        // glowing crystals (unlit + a small light each)
        for (float x = from + 1f; x < to; x += 3f + (float)rng.NextDouble() * 3f)
        {
            Sprite s = rng.NextDouble() < 0.5 ? Pick(rng, art.crystals) : Pick(rng, art.crystalPatches);
            SpriteRenderer sr = Place(decor, s, x, floorY, -4, Color.white, -0.06f, rng.NextDouble() < 0.5);
            if (sr == null) continue;
            if (CatFX.Unlit != null) sr.sharedMaterial = CatFX.Unlit;
            var light = new GameObject("Crystal Glow").AddComponent<Light2D>();
            light.transform.SetParent(sr.transform, false);
            light.lightType = Light2D.LightType.Point;
            light.color = rng.NextDouble() < 0.5 ? new Color(0.5f, 0.85f, 1f) : new Color(0.8f, 0.55f, 1f);
            light.intensity = 0.8f;
            light.pointLightOuterRadius = 1.8f;
            light.pointLightInnerRadius = 0.1f;
        }
        // a couple of dark bushes in the foreground at the edges (depth)
        foreach (float x in new[] { from + 1.5f, to - 1.5f })
        {
            SpriteRenderer sr = Place(decor, Pick(rng, art.bushes), x, floorY - 0.3f, 60, new Color(0.18f, 0.15f, 0.25f), -0.1f, x > (from + to) / 2f);
            if (sr != null && ItemArt.Lit != null) sr.sharedMaterial = ItemArt.Lit;
        }
    }

    private static Sprite Pick(System.Random rng, Sprite[] list) => list == null || list.Length == 0 ? null : list[rng.Next(list.Length)];

    private static SpriteRenderer Place(GameObject parent, Sprite s, float x, float floorY, int order, Color tint, float sink, bool flip)
    {
        if (s == null) return null;
        float ground = floorY;
        if (SolidGround.Ray(new Vector2(x, floorY + 4f), Vector2.down, 9f, out RaycastHit2D hit)) ground = hit.point.y;
        var go = new GameObject(s.name);
        go.transform.SetParent(parent.transform, false);
        float bottom = s.bounds.min.y; // pivot offset
        go.transform.position = new Vector3(Mathf.Round(x * 64f) / 64f, Mathf.Round((ground - bottom + sink) * 64f) / 64f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;
        sr.color = tint;
        sr.flipX = flip;
        if (ItemArt.Lit != null) sr.sharedMaterial = ItemArt.Lit;
        return sr;
    }
}

// The Gloomwood's sky: while the camera is there, a dark gradient covers whatever the main level's backgrounds
// are doing so far from home, the gloomy tree line drifts behind as parallax, fog rolls and wisps float.
public class GloomSky : MonoBehaviour
{
    private SpriteRenderer sky, backdrop, backdrop2;
    private float centerX, floorY, wispTimer;

    public void Setup(float centerX, float floorY)
    {
        this.centerX = centerX;
        this.floorY = floorY;
        int layer = SortingLayer.NameToID("Default");
        sky = Make("Gloom Sky", SkySprite(), -190, Color.white, layer);
        GloomArt art = GloomArt.Get;
        if (art != null && art.backdrop != null)
        {
            backdrop = Make("Gloom Backdrop", art.backdrop, -160, new Color(0.3f, 0.26f, 0.42f), layer);
            backdrop2 = Make("Gloom Backdrop 2", art.backdrop, -175, new Color(0.17f, 0.14f, 0.26f), layer);
            backdrop2.flipX = true;
        }
    }

    private SpriteRenderer Make(string name, Sprite s, int order, Color tint, int layer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingLayerID = layer;
        sr.sortingOrder = order;
        sr.color = tint;
        if (CatFX.Unlit != null) sr.sharedMaterial = CatFX.Unlit; // the sky ignores the day / night light
        sr.enabled = false;
        return sr;
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || sky == null) return;
        Vector3 c = cam.transform.position;
        bool here = c.x >= GloomArena.MinX;
        sky.enabled = here;
        if (backdrop != null) backdrop.enabled = backdrop2.enabled = here;
        if (!here) return;
        float h = cam.orthographicSize * 2f + 2f, w = h * cam.aspect + 2f;
        sky.transform.position = new Vector3(c.x, c.y, 0f);
        Vector2 size = sky.sprite.bounds.size;
        sky.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
        // the tree lines drift slower than the world (parallax), whole pixels
        if (backdrop != null)
        {
            float px = centerX + (c.x - centerX) * 0.55f;
            backdrop.transform.position = new Vector3(Mathf.Round(px * 64f) / 64f, Mathf.Round((floorY - 0.6f + backdrop.sprite.bounds.extents.y + (c.y - floorY) * 0.4f) * 64f) / 64f, 0f);
            float px2 = centerX + (c.x - centerX) * 0.75f;
            backdrop2.transform.position = new Vector3(Mathf.Round(px2 * 64f) / 64f, Mathf.Round((floorY + 1.2f + backdrop2.sprite.bounds.extents.y + (c.y - floorY) * 0.6f) * 64f) / 64f, 0f);
        }
        // fog puffs low and wisps floating up
        wispTimer -= Time.deltaTime;
        if (wispTimer <= 0f)
        {
            wispTimer = 0.12f;
            Vector3 p = new Vector3(c.x + Random.Range(-w / 2f, w / 2f), floorY + Random.Range(0f, 0.6f), 0f);
            FXParticle.Burst(p, new Color(0.55f, 0.5f, 0.7f, 0.35f), 1, 0.1f, 0.4f, -0.2f, 2.2f);
            if (Random.value < 0.4f)
                FXParticle.Burst(new Vector3(c.x + Random.Range(-w / 2f, w / 2f), floorY + Random.Range(0.5f, 4f), 0f),
                                 Random.value < 0.5f ? new Color(0.55f, 0.9f, 1f, 0.9f) : new Color(0.85f, 0.6f, 1f, 0.9f), 1, 0.05f, 0.3f, -0.4f, 1.6f);
        }
    }

    // 4 x 64 vertical gradient: deep violet at the top to a misty purple at the horizon
    private static Sprite skySprite;
    private static Sprite SkySprite()
    {
        if (skySprite != null) return skySprite;
        const int w = 4, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "GloomSky" };
        var px = new Color32[w * h];
        Color top = new Color(0.04f, 0.03f, 0.08f), mid = new Color(0.1f, 0.08f, 0.17f), low = new Color(0.24f, 0.19f, 0.32f);
        for (int y = 0; y < h; y++)
        {
            float t = y / (h - 1f);
            Color col = t < 0.5f ? Color.Lerp(low, mid, t * 2f) : Color.Lerp(mid, top, (t - 0.5f) * 2f);
            for (int x = 0; x < w; x++) px[y * w + x] = col;
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        skySprite = AIArt.Use("Gloomwood_Sky", Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 4f));
        return skySprite;
    }
}

// A swirling purple gate. {INTERACT} next to it travels (Frontier <-> Gloomwood). The Frontier one stays sealed
// until the Frontier's 30 waves have been cleared once.
public class GloomGate : MonoBehaviour
{
    private bool toGloom;
    private SpriteRenderer ring, core, prompt;
    private Light2D glow;
    private float t;

    public static void Make(Vector3 floor, bool toGloom)
    {
        var go = new GameObject(toGloom ? "Gloom Gate" : "Gloom Gate (back to the Frontier)");
        go.transform.position = floor;
        var g = go.AddComponent<GloomGate>();
        g.toGloom = toGloom;
        g.core = g.Part("Core", CoreSprite(), 6);
        g.ring = g.Part("Ring", RingSprite(), 7);
        g.prompt = g.Part("Prompt", null, 120);
        if (CatFX.Unlit != null) g.ring.sharedMaterial = g.core.sharedMaterial = g.prompt.sharedMaterial = CatFX.Unlit;
        g.glow = new GameObject("Glow").AddComponent<Light2D>();
        g.glow.transform.SetParent(go.transform, false);
        g.glow.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        g.glow.lightType = Light2D.LightType.Point;
        g.glow.color = GloomArena.Purple;
        g.glow.pointLightOuterRadius = 3f;
        g.glow.intensity = 1f;
    }

    private SpriteRenderer Part(string name, Sprite s, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;
        return sr;
    }

    private bool Open => !toGloom || GloomArena.GateOpen;

    private void Update()
    {
        t += Time.deltaTime;
        bool open = Open;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
        core.color = open ? new Color(0.25f + 0.15f * pulse, 0.1f, 0.4f + 0.2f * pulse, 0.95f) : new Color(0.15f, 0.12f, 0.18f, 0.9f);
        ring.color = open ? Color.Lerp(GloomArena.Purple, new Color(0.55f, 0.95f, 1f), pulse) : new Color(0.4f, 0.38f, 0.45f);
        glow.intensity = open ? 0.8f + 0.5f * pulse : 0.2f;
        // swirling motes
        if (open && Random.value < 0.5f)
        {
            float ang = t * 4f + Random.value * 6.28f;
            Vector3 p = transform.position + new Vector3(Mathf.Cos(ang) * 0.5f, 0.9f + Mathf.Sin(ang) * 0.75f, 0f);
            FXParticle.Burst(p, Random.value < 0.5f ? GloomArena.Purple : new Color(0.55f, 0.95f, 1f), 1, 0.1f, 0.4f, -0.6f, 0.6f);
        }

        Transform rowdy = BoonRunner.Rowdy;
        bool near = rowdy != null && !PauseMenu.IsPaused && !FrontierArena.Running && Vector2.Distance(BoonRunner.RowdyCenter, transform.position + Vector3.up * 0.9f) < 1.5f;
        prompt.enabled = near;
        if (near)
        {
            prompt.sprite = WeaponDrop.GetInteractPrompt();
            prompt.transform.position = transform.position + new Vector3(0f, 2.2f + Mathf.Round(Mathf.Sin(t * 4f) * 2f) / 64f, 0f);
            if (Time.frameCount % 200 == 0)
                IconPopup.Show(transform.position + Vector3.up * 2.7f, null, !open ? "SEALED: CLEAR THE FRONTIER'S 30 WAVES" : toGloom ? "THE GLOOMWOOD" : "BACK TO THE FRONTIER", GloomArena.Purple, 0.7f, 1.6f);
        }
        if (near && GameInput.Down(GameInput.Act.Interact) && !Interact.UsedThisFrame)
        {
            Interact.Use();
            if (!open)
            {
                UISound.Play(UISound.Cue.Locked);
                ScreenShake.Impulse(0.15f);
                IconPopup.Show(transform.position + Vector3.up * 2.7f, null, "SEALED: CLEAR THE FRONTIER'S 30 WAVES", new Color(1f, 0.5f, 0.55f), 0.8f, 2f);
                return;
            }
            if (toGloom) GloomArena.TravelToGloom(); else GloomArena.TravelToFrontier();
        }
    }

    // 30 x 52 px oval: a bright rim, and a dark swirl inside
    private static Sprite ringSprite, coreSprite;
    private static Sprite RingSprite() => ringSprite != null ? ringSprite : ringSprite = Oval("GateRing", true);
    private static Sprite CoreSprite() => coreSprite != null ? coreSprite : coreSprite = Oval("GateCore", false);

    private static Sprite Oval(string name, bool rim)
    {
        const int w = 30, h = 52;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - (w - 1) / 2f) / (w / 2f), dy = (y - (h - 1) / 2f) / (h / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                bool on;
                byte a = 255;
                if (rim) on = d <= 1f && d > 0.84f;
                else
                {
                    on = d <= 0.86f;
                    // swirl bands
                    float ang = Mathf.Atan2(dy, dx) + d * 6f;
                    a = (byte)(Mathf.Repeat(ang, 1.2f) < 0.6f ? 255 : 170);
                }
                px[y * w + x] = on ? new Color32(255, 255, 255, a) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return AIArt.Use("GloomGate_" + name, Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 64f));
    }
}
