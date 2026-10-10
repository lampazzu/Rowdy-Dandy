using Cinemachine;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// The colosseums past the Frontier, built at runtime (no scene edits), far east:
//   THE GLOOMWOOD (Shift 640)       - the Frontier island's ground copied into its own tilemap, dyed dark, dressed with
//                                     the Gloomy Forest art (Resources/GloomArt), one-way platforms for height, its sky
//   THE PURPLE REIGN (VioletShift)  - the same island again, dyed violet, a POOL dug in the middle of the arena (lake
//                                     water you can surf, Shark Wolves come out of it, walkers drown in it), purple
//                                     palms and purple fog only (Resources/ArenaArt), one-way platforms
// GATES (GloomGate, the Graft portal art): Frontier <-> Gloomwood (sealed until the Frontier's 30 waves are cleared),
// Gloomwood <-> Purple Reign (sealed until the Gloomwood is cleared). Dev tools > Travel jumps in (and unseals).
// Also dresses the scene's Frontier arena with orange rocks (ArenaArt) when it builds.
public static class GloomArena
{
    public const float Shift = 640f, VioletShift = 1280f;
    public const float MinX = 860f; // everything right of this is a runtime arena
    public const float VioletMinX = 1500f;
    private const string DevOpenKey = "RD_GloomGateOpen", DevVioletKey = "RD_VioletGateOpen";

    public static bool GateOpen => FrontierArena.Clears > 0 || PlayerPrefs.GetInt(DevOpenKey, 0) == 1;
    public static bool VioletGateOpen => FrontierArena.GloomClears > 0 || PlayerPrefs.GetInt(DevVioletKey, 0) == 1;
    private static FrontierArena arena, violet;
    private static Vector3 arrival, frontierReturn, violetArrival, gloomReturn;

    private static readonly Color Dye = new Color(0.36f, 0.32f, 0.48f);          // the copied sand: dark soil
    private static readonly Color VioletDye = new Color(0.62f, 0.38f, 0.78f);    // ... and violet
    private static readonly Color DecorTint = new Color(0.62f, 0.56f, 0.78f);
    public static readonly Color Purple = new Color(0.72f, 0.55f, 1f);
    public static readonly Color Violet = new Color(0.9f, 0.45f, 1f);

    public static void Build(FrontierArena frontier)
    {
        if (frontier == null) return;
        GameObject beachGo = GameObject.Find("BeachMap");
        Tilemap beach = beachGo != null ? beachGo.GetComponent<Tilemap>() : null;
        if (beach == null || beach.layoutGrid == null) { Debug.LogWarning("Gloomwood: BeachMap not found"); return; }

        DressFrontier(frontier);

        // ---- THE GLOOMWOOD
        GameObject root = BuildIsland("The Gloomwood (runtime)", frontier, beachGo, beach, Shift, Dye, out Tilemap gloomTiles);
        float left = frontier.leftX + Shift, right = frontier.rightX + Shift;
        arena = MakeArena(root, frontier, Shift, 1, new Color(0.75f, 0.65f, 1f));
        Decorate(root, left - 6f, right + 6f, arena.floorY);
        Platforms(root, left, right, arena.floorY, new Color(0.42f, 0.36f, 0.55f), new[] { (0.22f, 2.2f, 3), (0.78f, 2.2f, 3), (0.5f, 4.1f, 4) });
        root.AddComponent<GloomSky>().Setup((left + right) / 2f, arena.floorY, MinX, VioletMinX, false);

        // ---- THE PURPLE REIGN
        GameObject vroot = BuildIsland("The Purple Reign (runtime)", frontier, beachGo, beach, VioletShift, VioletDye, out Tilemap violetTiles);
        float vl = frontier.leftX + VioletShift, vr = frontier.rightX + VioletShift;
        violet = MakeArena(vroot, frontier, VioletShift, 2, new Color(0.95f, 0.6f, 1f));
        Pool(vroot, violet, violetTiles);
        DecorateViolet(vroot, vl - 6f, vr + 6f, violet);
        Platforms(vroot, vl, vr, violet.floorY, new Color(0.75f, 0.45f, 0.9f), new[] { (0.18f, 2.3f, 3), (0.82f, 2.3f, 3), (0.5f, 3.6f, 5) });
        vroot.AddComponent<GloomSky>().Setup((vl + vr) / 2f, violet.floorY, VioletMinX, 99999f, true);

        // ---- the gates between them
        Vector3 frontierGate = GateSpot(frontier.rightX + 3.4f, frontier.floorY, frontier.leftX, frontier.rightX);
        Vector3 gloomBack = GateSpot(left - 4f, arena.floorY, left, right);
        Vector3 gloomOn = GateSpot(right + 4f, arena.floorY, left, right);
        Vector3 violetBack = GateSpot(vl - 4f, violet.floorY, vl, vr);
        arrival = gloomBack + new Vector3(1.6f, 0.6f, 0f);
        frontierReturn = frontierGate + new Vector3(-1.4f, 0.6f, 0f);
        violetArrival = violetBack + new Vector3(1.6f, 0.6f, 0f);
        gloomReturn = gloomOn + new Vector3(-1.4f, 0.6f, 0f);
        GloomGate.Make(frontierGate, "THE GLOOMWOOD", () => GateOpen, "SEALED: CLEAR THE FRONTIER'S 30 WAVES", TravelToGloom, Purple);
        GloomGate.Make(gloomBack, "BACK TO THE FRONTIER", () => true, null, TravelToFrontier, Purple);
        GloomGate.Make(gloomOn, "THE PURPLE REIGN", () => VioletGateOpen, "SEALED: CLEAR THE GLOOMWOOD'S 30 WAVES", TravelToViolet, Violet);
        GloomGate.Make(violetBack, "BACK TO THE GLOOMWOOD", () => true, null, TravelBackToGloom, Violet);
        EnemyMovement.ForgetWaterCache(); // the new pool counts as water for drowning
    }

    private static GameObject BuildIsland(string name, FrontierArena frontier, GameObject beachGo, Tilemap beach, float shift, Color dye, out Tilemap tm)
    {
        var root = new GameObject(name);
        float left = frontier.leftX + shift, right = frontier.rightX + shift;
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

        var tmGo = new GameObject("Ground");
        tmGo.layer = beachGo.layer;
        tmGo.tag = beachGo.tag;
        tmGo.transform.SetParent(gridGo.transform, false);
        tmGo.transform.position = beach.transform.position;
        tm = tmGo.AddComponent<Tilemap>();
        tm.tileAnchor = beach.tileAnchor;
        tm.orientation = beach.orientation;
        tm.color = dye;
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
        Vector3Int shiftCells = beach.WorldToCell(new Vector3(frontier.leftX + shift, frontier.floorY, 0f)) - beach.WorldToCell(new Vector3(frontier.leftX, frontier.floorY, 0f));
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
        return root;
    }

    private static FrontierArena MakeArena(GameObject root, FrontierArena frontier, float shift, int tier, Color gongTint)
    {
        // inactive while it's set up, so Awake sees the fields
        var arenaGo = new GameObject(tier == 2 ? "Colosseum (Purple Reign)" : "Colosseum (Gloomwood)");
        arenaGo.SetActive(false);
        arenaGo.transform.SetParent(root.transform, false);
        FrontierArena a = arenaGo.AddComponent<FrontierArena>();
        a.gloom = true;
        a.tier = tier;
        a.leftX = frontier.leftX + shift;
        a.rightX = frontier.rightX + shift;
        a.floorY = frontier.floorY;
        a.islandStartX = MinX;
        var gongGo = new GameObject("Gong");
        gongGo.transform.SetParent(root.transform, false);
        gongGo.transform.position = frontier.gong != null ? frontier.gong.position + Vector3.right * shift : new Vector3(a.leftX + 3f, frontier.floorY, 0f);
        var gongSr = gongGo.AddComponent<SpriteRenderer>();
        gongSr.sprite = FrontierArena.GongSprite;
        gongSr.sortingOrder = 8;
        gongSr.color = gongTint;
        if (ItemArt.Lit != null) gongSr.sharedMaterial = ItemArt.Lit;
        a.gong = gongGo.transform;
        arenaGo.SetActive(true);
        return a;
    }

    private static void Wall(GameObject root, GameObject like, Vector2 center, Vector2 size)
    {
        var go = new GameObject("Arena Wall");
        go.layer = like.layer;
        go.tag = like.tag;
        go.transform.SetParent(root.transform, false);
        go.transform.position = center;
        go.AddComponent<BoxCollider2D>().size = size;
    }

    // A gate on walkable floor: the spot asked for if its floor is near the arena floor, otherwise the nearest one
    // (the Frontier's used to land on top of a cliff, out of reach), at worst just inside the arena wall
    private static Vector3 GateSpot(float x, float floorY, float arenaLeft, float arenaRight)
    {
        for (float d = 0f; d <= 6f; d += 0.5f)
            foreach (float s in new[] { 1f, -1f })
            {
                float px = x + d * s;
                if (SolidGround.Ray(new Vector2(px, floorY + 2f), Vector2.down, 4f, out RaycastHit2D hit) && Mathf.Abs(hit.point.y - floorY) < 0.9f
                    && !SolidGround.Blocked(hit.point + new Vector2(0f, 1.1f), new Vector2(0.8f, 1.6f)))
                    return hit.point;
            }
        float inside = x > arenaRight ? arenaRight - 1.5f : arenaLeft + 1.5f;
        return SolidGround.Ray(new Vector2(inside, floorY + 2f), Vector2.down, 6f, out RaycastHit2D h2) ? (Vector3)h2.point : new Vector3(inside, floorY, 0f);
    }

    // ---------------------------------------------------------------- platforms (one-way, the sand pass-through tile)
    private static void Platforms(GameObject root, float left, float right, float floorY, Color tint, (float at, float height, int tiles)[] list)
    {
        ArenaArt art = ArenaArt.Get;
        Sprite tile = art != null && art.passThrough != null ? ItemArt.Frames(art.passThrough, 1, 1, new Vector2(0f, 1f), 64f)[0] : null;
        // copy the one-way setup of the level's platforms (layer, tag, effector) so Rowdy treats them the same
        PlatformEffector2D like = null;
        foreach (PlatformEffector2D e in Object.FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None))
            if (e.GetComponent<Tilemap>() != null) { like = e; break; }
        var holder = new GameObject("Platforms");
        holder.transform.SetParent(root.transform, false);
        foreach (var (at, height, tiles) in list)
        {
            float w = tiles; // 64 px per tile = 1 unit
            float x0 = Mathf.Round((Mathf.Lerp(left, right, at) - w / 2f) * 64f) / 64f, y = Mathf.Round((floorY + height) * 64f) / 64f;
            var go = new GameObject("Platform");
            go.transform.SetParent(holder.transform, false);
            go.transform.position = new Vector3(x0, y, 0f);
            if (like != null) { go.layer = like.gameObject.layer; go.tag = like.gameObject.tag; }
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(w, 0.2f);
            box.offset = new Vector2(w / 2f, -0.1f);
            box.usedByEffector = true;
            var eff = go.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true;
            eff.surfaceArc = like != null ? like.surfaceArc : 160f;
            eff.useSideFriction = false;
            eff.useSideBounce = false;
            if (like != null) { eff.colliderMask = like.colliderMask; eff.useColliderMask = like.useColliderMask; }
            for (int i = 0; i < tiles && tile != null; i++)
            {
                var t = new GameObject("Tile").AddComponent<SpriteRenderer>();
                t.transform.SetParent(go.transform, false);
                t.transform.localPosition = new Vector3(i, 0f, 0f);
                t.sprite = tile;
                t.color = tint;
                t.sortingOrder = 2;
                if (ItemArt.Lit != null) t.sharedMaterial = ItemArt.Lit;
            }
        }
    }

    // ---------------------------------------------------------------- the Purple Reign's pool
    private const float PoolWidth = 5f, PoolDepth = 2.5f;

    private static void Pool(GameObject root, FrontierArena a, Tilemap ground)
    {
        float cx = Mathf.Round((a.leftX + a.rightX) / 2f), x0 = cx - PoolWidth / 2f, x1 = cx + PoolWidth / 2f;
        float surface = a.floorY - 0.3f;
        // dig: take the ground out where the water goes
        Vector3Int c0 = ground.WorldToCell(new Vector3(x0 + 0.05f, a.floorY - PoolDepth, 0f)), c1 = ground.WorldToCell(new Vector3(x1 - 0.05f, a.floorY + 0.5f, 0f));
        for (int x = c0.x; x <= c1.x; x++)
            for (int y = c0.y; y <= c1.y; y++)
                ground.SetTile(new Vector3Int(x, y, 0), null);
        if (ground.TryGetComponent(out TilemapCollider2D tc)) tc.ProcessTilemapChanges();
        if (ground.TryGetComponent(out CompositeCollider2D comp)) comp.GenerateGeometry();

        var pool = new GameObject("Pool");
        pool.transform.SetParent(root.transform, false);
        // the water you surf on (like the Watermap: a solid surface on the water layer) + the body enemies drown in
        int waterLayer = 6;
        var surf = new GameObject("Water Surface");
        surf.transform.SetParent(pool.transform, false);
        surf.layer = waterLayer;
        surf.tag = "Water";
        surf.transform.position = new Vector3(cx, surface - 0.1f, 0f);
        surf.AddComponent<BoxCollider2D>().size = new Vector2(PoolWidth, 0.2f);
        var body = new GameObject("Water Body");
        body.transform.SetParent(pool.transform, false);
        body.layer = waterLayer;
        body.tag = "Water";
        body.transform.position = new Vector3(cx, surface - PoolDepth / 2f, 0f);
        var bc = body.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(PoolWidth, PoolDepth);
        bc.isTrigger = true;
        // a floor under the water
        var floor = new GameObject("Pool Floor");
        floor.transform.SetParent(pool.transform, false);
        floor.layer = ground.gameObject.layer;
        floor.tag = ground.gameObject.tag;
        floor.transform.position = new Vector3(cx, a.floorY - PoolDepth - 0.25f, 0f);
        floor.AddComponent<BoxCollider2D>().size = new Vector2(PoolWidth + 0.5f, 0.5f);

        // the look: one row of the lake tiles (64 x 64, 6 frames) on top, animated, whole pixels - and a flat deep
        // violet under it down to the pool floor
        ArenaArt art = ArenaArt.Get;
        if (art != null && art.lake != null)
        {
            Sprite[] frames = ItemArt.Frames(art.lake, 6, 1, new Vector2(0f, 1f), 64f);
            var anim = pool.AddComponent<PoolWater>();
            for (int i = 0; i < Mathf.RoundToInt(PoolWidth); i++)
            {
                var t = new GameObject("Lake").AddComponent<SpriteRenderer>();
                t.transform.SetParent(pool.transform, false);
                t.transform.position = new Vector3(x0 + i, surface + 0.15f, 0f);
                t.sortingOrder = 41;
                if (CatFX.Unlit != null) t.sharedMaterial = CatFX.Unlit;
                anim.Add(t, frames, i);
            }
        }
        var deep = new GameObject("Deep Water").AddComponent<SpriteRenderer>();
        deep.transform.SetParent(pool.transform, false);
        deep.sprite = BoonFX.Pixel; // a flat colour: stretching it is fine
        deep.transform.position = new Vector3(cx, (surface - 0.4f + a.floorY - PoolDepth) / 2f, 0f);
        deep.transform.localScale = new Vector3(PoolWidth * 64f, (surface - 0.4f - (a.floorY - PoolDepth)) * 64f, 1f);
        deep.color = new Color(0.32f, 0.08f, 0.38f, 0.92f);
        deep.sortingOrder = 40;
        if (CatFX.Unlit != null) deep.sharedMaterial = CatFX.Unlit;
        a.hasPool = true;
        a.poolX0 = x0;
        a.poolX1 = x1;
        a.poolSurface = surface;
    }

    // ---------------------------------------------------------------- travel
    public static void TravelToGloom() => Travel(arrival, "THE GLOOMWOOD", null, Purple);
    public static void TravelToFrontier() => Travel(frontierReturn, null, null, Purple);
    public static void TravelToViolet() => Travel(violetArrival, "THE PURPLE REIGN", "THE HARDEST ONE. LONG LIVE THE QUEEN... OF NOTHING YET", Violet);
    public static void TravelBackToGloom() => Travel(gloomReturn, null, null, Violet);

    public static void DevTravel()
    {
        PlayerPrefs.SetInt(DevOpenKey, 1);
        PlayerPrefs.Save();
        if (arena != null) TravelToGloom();
    }

    public static void DevTravelViolet()
    {
        PlayerPrefs.SetInt(DevOpenKey, 1);
        PlayerPrefs.SetInt(DevVioletKey, 1);
        PlayerPrefs.Save();
        if (violet != null) TravelToViolet();
    }

    private static void Travel(Vector3 to, string title, string sub, Color color)
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

        ScreenFlash.Play(new Color(color.r * 0.6f, color.g * 0.4f, color.b * 0.8f, 0.85f), 0.6f);
        BoonArt art = BoonArt.Get;
        if (art != null) { FXSound.Play(art.whoosh, 0.6f, 0.7f); FXSound.Play(art.open, 0.4f, 0.6f); }
        GraftFX.Play("BRJ_WeirdTeleport", to + Vector3.up * 0.5f, Color.white, 95);
        PulseRing.Spawn(to, color, 2.2f, 0.5f);
        FXParticle.Burst(to, color, 26, 2f, 5f, 2f, 0.8f);
        ScreenShake.Impulse(0.4f);
        if (title != null) Banner.Show(title, sub, color, 2f);
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
        for (float x = from; x < to; x += 9f + (float)rng.NextDouble() * 4f)
            Place(decor, art.horrificTrees != null && art.horrificTrees.Length > 1 ? art.horrificTrees[1] : Pick(rng, art.horrificTrees), x, floorY, -40, DecorTint * 0.45f, -0.6f, rng.NextDouble() < 0.5);
        for (float x = from; x < to; x += 0.9f + (float)rng.NextDouble() * 1.4f)
        {
            double r = rng.NextDouble();
            Sprite s = r < 0.35 ? Pick(rng, art.grass) : r < 0.6 ? Pick(rng, art.bushes) : r < 0.78 ? Pick(rng, art.flowers) : Pick(rng, art.rocks);
            Place(decor, s, x, floorY, -6, DecorTint, -0.08f, rng.NextDouble() < 0.5);
        }
        for (float x = from + 1f; x < to; x += 3f + (float)rng.NextDouble() * 3f)
        {
            Sprite s = rng.NextDouble() < 0.5 ? Pick(rng, art.crystals) : Pick(rng, art.crystalPatches);
            SpriteRenderer sr = Place(decor, s, x, floorY, -4, Color.white, -0.06f, rng.NextDouble() < 0.5);
            if (sr == null) continue;
            if (CatFX.Unlit != null) sr.sharedMaterial = CatFX.Unlit;
            Glow(sr.transform, rng.NextDouble() < 0.5 ? new Color(0.5f, 0.85f, 1f) : new Color(0.8f, 0.55f, 1f), 0.8f, 1.8f);
        }
        foreach (float x in new[] { from + 1.5f, to - 1.5f })
        {
            SpriteRenderer sr = Place(decor, Pick(rng, art.bushes), x, floorY - 0.3f, 60, new Color(0.18f, 0.15f, 0.25f), -0.1f, x > (from + to) / 2f);
            if (sr != null && ItemArt.Lit != null) sr.sharedMaterial = ItemArt.Lit;
        }
    }

    // The Purple Reign: purple palms (animated), purple plants, purple fog - nothing else
    private static void DecorateViolet(GameObject root, float from, float to, FrontierArena a)
    {
        ArenaArt art = ArenaArt.Get;
        var rng = new System.Random(777);
        var decor = new GameObject("Decor");
        decor.transform.SetParent(root.transform, false);
        if (art != null && art.bigPalm != null)
        {
            Sprite[] palm = ItemArt.Frames(art.bigPalm, 22, 1, new Vector2(0.5f, 0f), 64f);
            for (float x = from; x < to; x += 3.5f + (float)rng.NextDouble() * 3f)
            {
                if (a.hasPool && x > a.poolX0 - 1f && x < a.poolX1 + 1f) continue;
                SpriteRenderer sr = Place(decor, palm[0], x, a.floorY, -20 - rng.Next(0, 4), new Color(0.85f, 0.8f, 0.95f), -0.1f, rng.NextDouble() < 0.5);
                if (sr != null) sr.gameObject.AddComponent<LoopFrames>().Set(palm, 10f, (float)rng.NextDouble() * 2f);
            }
        }
        if (art != null && art.palm != null)
        {
            Sprite small = ArenaArt.Standing(art.palm);
            for (float x = from + 2f; x < to; x += 5f + (float)rng.NextDouble() * 4f)
                Place(decor, small, x, a.floorY, -30, new Color(0.6f, 0.5f, 0.75f), -0.2f, rng.NextDouble() < 0.5);
        }
        // purple plants you can cut (juice!) along the edges of the arena
        GameObject plant = Resources.Load<GameObject>("Interactables/PurplePlant");
        if (plant != null)
            foreach (float x in new[] { a.leftX + 1f, a.rightX - 1f, from + 1.5f, to - 1.5f })
                if (SolidGround.Ray(new Vector2(x, a.floorY + 3f), Vector2.down, 6f, out RaycastHit2D hit))
                    Object.Instantiate(plant, hit.point, Quaternion.identity, decor.transform);
        // fog drifting low over the floor
        if (art != null)
            foreach (Texture2D fog in new[] { art.fog1, art.fog2, art.fog3 })
            {
                if (fog == null) continue;
                Sprite s = ArenaArt.Standing(fog);
                var go = new GameObject(fog.name).AddComponent<SpriteRenderer>();
                go.transform.SetParent(decor.transform, false);
                go.transform.position = new Vector3(Mathf.Round(Mathf.Lerp(from, to, (float)rng.NextDouble()) * 64f) / 64f, a.floorY - 2.5f, 0f);
                go.sprite = s;
                go.sortingOrder = -15;
                go.color = new Color(1f, 1f, 1f, 0.55f);
                if (CatFX.Unlit != null) go.sharedMaterial = CatFX.Unlit;
                go.gameObject.AddComponent<FogDrift>();
            }
        // violet lamps (a few lights)
        for (float x = from + 3f; x < to; x += 7f) Glow(decor.transform, Violet, 0.9f, 3f, new Vector3(x, a.floorY + 1.5f, 0f));
    }

    // The scene's Frontier arena gets orange rocks and earth: piles, walls, pillars, the big orange cliff, a wolf skull
    private static void DressFrontier(FrontierArena f)
    {
        ArenaArt art = ArenaArt.Get;
        if (art == null) return;
        var rng = new System.Random(1999);
        var decor = new GameObject("Frontier Rocks (runtime)");
        float from = f.leftX - 5f, to = f.rightX + 5f;
        Texture2D[] back = { art.earthWall, art.earthWallHole, art.earthPile, art.sandPile };
        Texture2D[] front = { art.pillar, art.doublePillar, art.fatPillar, art.earthPile };
        for (float x = from; x < to; x += 2.2f + (float)rng.NextDouble() * 1.8f)
            Place(decor, ArenaArt.Standing(back[rng.Next(back.Length)]), x, f.floorY, -12 - rng.Next(0, 3), Color.white, -0.12f, rng.NextDouble() < 0.5);
        for (float x = from + 1f; x < to; x += 3.5f + (float)rng.NextDouble() * 3f)
        {
            if (f.gong != null && Mathf.Abs(x - f.gong.position.x) < 1.2f) continue;
            Place(decor, ArenaArt.Standing(front[rng.Next(front.Length)]), x, f.floorY, -5, Color.white, -0.05f, rng.NextDouble() < 0.5);
        }
        if (art.wolfSkull != null) Place(decor, ArenaArt.Standing(art.wolfSkull), f.rightX - 2.2f, f.floorY, -4, Color.white, -0.15f, false);
        GameObject plant = Resources.Load<GameObject>("Interactables/OrangePlant");
        if (plant != null)
            foreach (float x in new[] { f.leftX + 0.8f, f.rightX - 0.8f })
                if (SolidGround.Ray(new Vector2(x, f.floorY + 3f), Vector2.down, 6f, out RaycastHit2D hit))
                    Object.Instantiate(plant, hit.point, Quaternion.identity, decor.transform);
        // warm light over the sand
        for (float x = from + 3f; x < to; x += 8f) Glow(decor.transform, new Color(1f, 0.65f, 0.3f), 0.6f, 4f, new Vector3(x, f.floorY + 2f, 0f));
    }

    private static void Glow(Transform parent, Color color, float intensity, float radius, Vector3? at = null)
    {
        var light = new GameObject("Glow").AddComponent<Light2D>();
        light.transform.SetParent(parent, false);
        if (at.HasValue) light.transform.position = at.Value;
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightOuterRadius = radius;
        light.pointLightInnerRadius = 0.1f;
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

// Loops sprite frames on a renderer (palms swaying)
public class LoopFrames : MonoBehaviour
{
    private Sprite[] frames;
    private float fps, offset;
    private SpriteRenderer sr;
    public void Set(Sprite[] f, float framesPerSecond, float timeOffset) { frames = f; fps = framesPerSecond; offset = timeOffset; sr = GetComponent<SpriteRenderer>(); }
    private void Update() { if (sr != null && frames != null && frames.Length > 0) sr.sprite = frames[(int)((Time.time + offset) * fps) % frames.Length]; }
}

// The Purple Reign's lake tiles, animated together
public class PoolWater : MonoBehaviour
{
    private readonly List<(SpriteRenderer sr, Sprite[] frames, int offset)> tiles = new List<(SpriteRenderer, Sprite[], int)>();
    public void Add(SpriteRenderer sr, Sprite[] frames, int offset) => tiles.Add((sr, frames, offset));
    private void Update()
    {
        int f = (int)(Time.time * 6f);
        foreach (var t in tiles) if (t.sr != null) t.sr.sprite = t.frames[(f + t.offset) % t.frames.Length];
    }
}

// Fog that drifts slowly back and forth, a whole pixel at a time
public class FogDrift : MonoBehaviour
{
    private Vector3 home;
    private float phase;
    private void Start() { home = transform.position; phase = Random.Range(0f, 10f); }
    private void Update()
    {
        float x = home.x + Mathf.Sin(Time.time * 0.15f + phase) * 3f;
        transform.position = new Vector3(Mathf.Round(x * 64f) / 64f, home.y, 0f);
    }
}

// A runtime arena's sky: while the camera is inside [minX, maxX], a dark gradient covers whatever the main level's
// backgrounds are doing so far from home; the Gloomwood gets its gloomy tree line as parallax, the Purple Reign its
// purple fog. Fog puffs and wisps float.
public class GloomSky : MonoBehaviour
{
    private SpriteRenderer sky, backdrop, backdrop2;
    private float centerX, floorY, wispTimer, minX, maxX;
    private bool violet;

    public void Setup(float centerX, float floorY, float minX, float maxX, bool violet)
    {
        this.centerX = centerX;
        this.floorY = floorY;
        this.minX = minX;
        this.maxX = maxX;
        this.violet = violet;
        int layer = SortingLayer.NameToID("Default");
        sky = Make("Sky", SkySprite(violet), -190, Color.white, layer);
        if (!violet)
        {
            GloomArt art = GloomArt.Get;
            if (art != null && art.backdrop != null)
            {
                backdrop = Make("Gloom Backdrop", art.backdrop, -160, new Color(0.3f, 0.26f, 0.42f), layer);
                backdrop2 = Make("Gloom Backdrop 2", art.backdrop, -175, new Color(0.17f, 0.14f, 0.26f), layer);
                backdrop2.flipX = true;
            }
        }
        else
        {
            ArenaArt art = ArenaArt.Get;
            if (art != null && art.frontzada != null && art.fog3 != null)
            {
                backdrop = Make("Purple Palms", ArenaArt.Standing(art.frontzada), -160, new Color(0.75f, 0.6f, 0.9f), layer);
                backdrop2 = Make("Purple Fog", ArenaArt.Standing(art.fog3), -175, new Color(0.8f, 0.6f, 1f, 0.7f), layer);
            }
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
        bool here = c.x >= minX && c.x < maxX;
        sky.enabled = here;
        if (backdrop != null) backdrop.enabled = backdrop2.enabled = here;
        if (!here) return;
        float h = cam.orthographicSize * 2f + 2f, w = h * cam.aspect + 2f;
        sky.transform.position = new Vector3(c.x, c.y, 0f);
        Vector2 size = sky.sprite.bounds.size;
        sky.transform.localScale = new Vector3(w / size.x, h / size.y, 1f); // a smooth gradient: scaling it is fine
        if (backdrop != null)
        {
            // the tree lines / fog drift slower than the world (parallax), whole pixels
            float px = centerX + (c.x - centerX) * 0.55f, px2 = centerX + (c.x - centerX) * 0.75f;
            float y1 = violet ? floorY - 1.5f - backdrop.sprite.bounds.min.y : floorY - 0.6f + backdrop.sprite.bounds.extents.y;
            float y2 = violet ? floorY - 0.5f - backdrop2.sprite.bounds.min.y : floorY + 1.2f + backdrop2.sprite.bounds.extents.y;
            backdrop.transform.position = new Vector3(Mathf.Round(px * 64f) / 64f, Mathf.Round((y1 + (c.y - floorY) * 0.4f) * 64f) / 64f, 0f);
            backdrop2.transform.position = new Vector3(Mathf.Round(px2 * 64f) / 64f, Mathf.Round((y2 + (c.y - floorY) * 0.6f) * 64f) / 64f, 0f);
        }
        wispTimer -= Time.deltaTime;
        if (wispTimer <= 0f)
        {
            wispTimer = 0.12f;
            Vector3 p = new Vector3(c.x + Random.Range(-w / 2f, w / 2f), floorY + Random.Range(0f, 0.6f), 0f);
            FXParticle.Burst(p, violet ? new Color(0.8f, 0.5f, 0.9f, 0.35f) : new Color(0.55f, 0.5f, 0.7f, 0.35f), 1, 0.1f, 0.4f, -0.2f, 2.2f);
            if (Random.value < 0.4f)
                FXParticle.Burst(new Vector3(c.x + Random.Range(-w / 2f, w / 2f), floorY + Random.Range(0.5f, 4f), 0f),
                                 violet ? new Color(1f, 0.6f, 1f, 0.9f) : Random.value < 0.5f ? new Color(0.55f, 0.9f, 1f, 0.9f) : new Color(0.85f, 0.6f, 1f, 0.9f), 1, 0.05f, 0.3f, -0.4f, 1.6f);
        }
    }

    // 4 x 64 vertical gradient (smooth, bilinear): night violet for the Gloomwood, magenta dusk for the Purple Reign
    private static Sprite gloomSky, violetSky;
    private static Sprite SkySprite(bool violet)
    {
        if (violet ? violetSky != null : gloomSky != null) return violet ? violetSky : gloomSky;
        const int w = 4, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = violet ? "VioletSky" : "GloomSky" };
        var px = new Color32[w * h];
        Color top = violet ? new Color(0.12f, 0.03f, 0.18f) : new Color(0.04f, 0.03f, 0.08f);
        Color mid = violet ? new Color(0.3f, 0.08f, 0.38f) : new Color(0.1f, 0.08f, 0.17f);
        Color low = violet ? new Color(0.62f, 0.25f, 0.6f) : new Color(0.24f, 0.19f, 0.32f);
        for (int y = 0; y < h; y++)
        {
            float t = y / (h - 1f);
            Color col = t < 0.5f ? Color.Lerp(low, mid, t * 2f) : Color.Lerp(mid, top, (t - 0.5f) * 2f);
            for (int x = 0; x < w; x++) px[y * w + x] = col;
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        Sprite s = AIArt.Use(violet ? "PurpleReign_Sky" : "Gloomwood_Sky", Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 4f));
        if (violet) violetSky = s; else gloomSky = s;
        return s;
    }
}

// A portal between colosseums (the Graft portal art, BRJ_Portal_Loop). {INTERACT} next to it travels; a sealed one
// stays grey and says what opens it.
public class GloomGate : MonoBehaviour
{
    private string label, sealedText;
    private System.Func<bool> isOpen;
    private System.Action travel;
    private Color color;
    private SpriteRenderer portal, prompt;
    private Sprite[] frames;
    private Light2D glow;
    private float t;
    private bool wasOpen;

    public static void Make(Vector3 floor, string label, System.Func<bool> isOpen, string sealedText, System.Action travel, Color color)
    {
        var go = new GameObject("Gate: " + label);
        go.transform.position = floor;
        var g = go.AddComponent<GloomGate>();
        g.label = label; g.isOpen = isOpen; g.sealedText = sealedText; g.travel = travel; g.color = color;
        g.frames = GraftFX.Frames("BRJ_Portal_Loop", null);
        if (g.frames != null)
        {
            // stand the portal on the floor
            Sprite[] f = ItemArt.Frames(GraftFX.Get("BRJ_Portal_Loop").texture, g.frames.Length, 1, new Vector2(0.5f, 0f), 64f);
            g.frames = f;
        }
        g.portal = g.Part("Portal", g.frames != null ? g.frames[0] : null, 6);
        g.prompt = g.Part("Prompt", null, 120);
        if (CatFX.Unlit != null) g.portal.sharedMaterial = g.prompt.sharedMaterial = CatFX.Unlit;
        g.glow = new GameObject("Glow").AddComponent<Light2D>();
        g.glow.transform.SetParent(go.transform, false);
        g.glow.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        g.glow.lightType = Light2D.LightType.Point;
        g.glow.color = color;
        g.glow.pointLightOuterRadius = 3f;
        g.glow.intensity = 1f;
        g.wasOpen = isOpen();
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

    private void Update()
    {
        if (isOpen == null || portal == null) return; // (a script reload in play mode drops the delegate)
        t += Time.deltaTime;
        bool open = isOpen();
        if (open && !wasOpen) GraftFX.Play("BRJ_Portal_Create", transform.position + Vector3.up * 0.7f, Color.white, 7); // just unsealed
        wasOpen = open;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
        if (frames != null) portal.sprite = frames[(int)(t * (open ? 12f : 4f)) % frames.Length];
        portal.color = open ? Color.white : new Color(0.45f, 0.42f, 0.5f, 0.85f);
        glow.intensity = open ? 0.8f + 0.5f * pulse : 0.2f;
        if (open && Random.value < 0.4f)
        {
            float ang = t * 4f + Random.value * 6.28f;
            Vector3 p = transform.position + new Vector3(Mathf.Cos(ang) * 0.45f, 0.55f + Mathf.Sin(ang) * 0.5f, 0f);
            FXParticle.Burst(p, Random.value < 0.5f ? color : new Color(1f, 0.8f, 1f), 1, 0.1f, 0.4f, -0.6f, 0.6f);
        }

        Transform rowdy = BoonRunner.Rowdy;
        bool near = rowdy != null && !PauseMenu.IsPaused && !FrontierArena.Running && Vector2.Distance(BoonRunner.RowdyCenter, transform.position + Vector3.up * 0.6f) < 1.5f;
        prompt.enabled = near;
        if (near)
        {
            prompt.sprite = WeaponDrop.GetInteractPrompt();
            prompt.transform.position = transform.position + new Vector3(0f, 1.8f + Mathf.Round(Mathf.Sin(t * 4f) * 2f) / 64f, 0f);
            if (Time.frameCount % 200 == 0)
                IconPopup.Show(transform.position + Vector3.up * 2.3f, null, open ? label : sealedText, color, 0.7f, 1.6f);
        }
        if (near && GameInput.Down(GameInput.Act.Interact) && !Interact.UsedThisFrame)
        {
            Interact.Use();
            if (!open)
            {
                UISound.Play(UISound.Cue.Locked);
                ScreenShake.Impulse(0.15f);
                IconPopup.Show(transform.position + Vector3.up * 2.3f, null, sealedText, new Color(1f, 0.5f, 0.55f), 0.8f, 2f);
                return;
            }
            travel?.Invoke();
        }
    }
}
