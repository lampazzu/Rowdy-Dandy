using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Hollow Knight style map. M (keyboard) / R2 (gamepad) opens it from anywhere; the game pauses meanwhile.
//  - Drawn from the level itself: every tilemap with a collider (solid ground, one-way platforms, water) is
//    rasterised at PxPerUnit pixels per unit when the scene loads - no map art to keep up to date.
//  - Only where Rowdy has been is shown (RevealRadius around him), saved per scene in PlayerPrefs (RD_Map_*),
//    with a ragged dithered edge where the explored part ends.
//  - Pins: Rowdy (pulsing), spawners (the active one in gold), Pelich / the Moonbound Elder (skulls), the old man.
//    Area names come from the wave spawner's difficulty zones.
//  - Move: stick / arrows / WASD. Zoom: L1 R1 / Q E / mouse wheel. Center on Rowdy: Y / Space. Close: R2 / M / B / Esc.
public class WorldMap : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static bool BlocksPause => IsOpen || Time.frameCount <= closedFrame + 1;

    private static WorldMap instance;
    private static int closedFrame = -10;

    // ---------------------------------------------------------------- tuning
    private const int PxPerUnit = 4;          // map pixels per world unit
    private const float CellUnits = 2f;       // exploration grid
    private const float RevealRadius = 11f;
    private static readonly float[] Zooms = { 1f, 2f, 3f, 4f, 6f };   // UI pixels per map pixel
    private const int DefaultZoom = 2;

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    private static readonly Color32 SolidFill = new Color32(0x4A, 0x36, 0x66, 0xFF);
    private static readonly Color32 SolidEdge = new Color32(0xE6, 0xD8, 0xFF, 0xFF);
    private static readonly Color32 PlatformTop = new Color32(0xFF, 0x9B, 0xE6, 0xFF);
    private static readonly Color32 PlatformFill = new Color32(0x6B, 0x3A, 0x6E, 0xFF);
    private static readonly Color32 WaterFill = new Color32(0x22, 0x3C, 0x7A, 0xFF);
    private static readonly Color32 WaterTop = new Color32(0x7C, 0xC8, 0xFF, 0xFF);
    private static readonly Color32 GridLine = new Color32(0xFF, 0xFF, 0xFF, 0x0C);
    private static readonly Color Pink = new Color32(0xFF, 0x9B, 0xE6, 0xFF);
    private static readonly Color HotPink = new Color32(0xFF, 0x39, 0xC0, 0xFF);
    private static readonly Color Gold = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
    private static readonly Color TitleColor = new Color32(0xF4, 0xEC, 0xFA, 0xFF);

    // ---------------------------------------------------------------- level data (per scene)
    private string sceneName;
    private bool built;
    private Rect world;
    private int texW, texH, cellsX, cellsY;
    private byte[] kind;          // 0 empty, 1 water, 2 platform, 3 solid
    private bool[] explored;
    private bool exploredDirty;
    private float saveTimer, trackTimer;
    private Texture2D mapTexture;
    private Transform rowdy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("WorldMap (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<WorldMap>();
        SceneManager.sceneLoaded += (s, m) => { if (instance != null) instance.OnSceneChanged(); };
    }

    private void OnSceneChanged()
    {
        SaveExplored();
        if (IsOpen) Close();
        built = false;
        rowdy = null;
    }

    // Dev reset (key 0)
    public static void ResetProgress()
    {
        if (instance == null) return;
        if (instance.built) PlayerPrefs.DeleteKey(instance.SaveKey);
        if (instance.explored != null) System.Array.Clear(instance.explored, 0, instance.explored.Length);
    }

    private void OnApplicationQuit() => SaveExplored();

    // ---------------------------------------------------------------- update
    private void Update()
    {
        if (IsOpen)
        {
            HandleInput();
            Animate();
            return;
        }

        if (!PauseMenu.IsPaused && !RowdyNotes.IsOpen && (Input.GetKeyDown(KeyCode.M) || PadInput.R2Down)) { Open(); return; }
        TrackExploration();
    }

    private void LateUpdate()
    {
        if (pendingClose) { pendingClose = false; Close(); }
    }

    private void TrackExploration()
    {
        trackTimer -= Time.unscaledDeltaTime;
        if (trackTimer > 0f) return;
        trackTimer = 0.25f;

        if (rowdy == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            rowdy = p.transform;
        }
        if (!built) Build();
        if (!built) return;

        Reveal(rowdy.position, RevealRadius);

        saveTimer -= 0.25f;
        if (saveTimer <= 0f) { saveTimer = 5f; SaveExplored(); }
    }

    private void Reveal(Vector2 at, float radius)
    {
        int cx = Mathf.FloorToInt((at.x - world.xMin) / CellUnits), cy = Mathf.FloorToInt((at.y - world.yMin) / CellUnits);
        int r = Mathf.CeilToInt(radius / CellUnits);
        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= cellsY) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= cellsX) continue;
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > r * r) continue;
                int i = y * cellsX + x;
                if (!explored[i]) { explored[i] = true; exploredDirty = true; }
            }
        }
    }

    private bool Explored(Vector2 worldPos)
    {
        if (!built) return false;
        int x = Mathf.FloorToInt((worldPos.x - world.xMin) / CellUnits), y = Mathf.FloorToInt((worldPos.y - world.yMin) / CellUnits);
        return x >= 0 && y >= 0 && x < cellsX && y < cellsY && explored[y * cellsX + x];
    }

    // ---------------------------------------------------------------- reading the level
    private void Build()
    {
        sceneName = SceneManager.GetActiveScene().name;

        // What to draw: the outlines of the level's colliders (the tiles themselves are big decorated sprites,
        // so the collision shapes are the honest picture of where you can stand / swim)
        var shapes = new List<(List<Vector2[]> paths, byte kind)>();
        bool any = false;
        Bounds all = default;
        var done = new HashSet<Collider2D>();

        foreach (CompositeCollider2D comp in FindObjectsByType<CompositeCollider2D>(FindObjectsSortMode.None))
        {
            if (comp.isTrigger || comp.pathCount == 0) continue;
            var paths = new List<Vector2[]>();
            var buffer = new List<Vector2>();
            for (int p = 0; p < comp.pathCount; p++)
            {
                buffer.Clear();
                comp.GetPath(p, buffer);
                var pts = new Vector2[buffer.Count];
                for (int i = 0; i < buffer.Count; i++) pts[i] = comp.transform.TransformPoint(buffer[i]);
                paths.Add(pts);
            }
            shapes.Add((paths, KindOf(comp.gameObject)));
            foreach (Collider2D member in comp.GetComponents<Collider2D>()) done.Add(member);
            if (!any) { all = comp.bounds; any = true; } else all.Encapsulate(comp.bounds);
        }

        // Loose solid pieces on the Ground layer (slope / wall correctors, tilemaps without a composite)
        int groundLayer = LayerMask.NameToLayer("Ground");
        var group = new PhysicsShapeGroup2D();
        var verts = new List<Vector2>();
        foreach (Collider2D c in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (done.Contains(c) || c.isTrigger || !c.enabled || c is CompositeCollider2D) continue;
            if (c.gameObject.layer != groundLayer && !c.CompareTag("Ground") && !c.CompareTag("Water")) continue;
            if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
            if (c.GetComponentInParent<EnemyHealth>() != null) continue;
            group.Clear();
            if (c.GetShapes(group) == 0) continue;
            var paths = new List<Vector2[]>();
            Transform space = c.attachedRigidbody != null ? c.attachedRigidbody.transform : null;
            for (int s = 0; s < group.shapeCount; s++)
            {
                if (group.GetShape(s).shapeType != PhysicsShapeType2D.Polygon) continue;
                verts.Clear();
                group.GetShapeVertices(s, verts);
                var pts = new Vector2[verts.Count];
                for (int i = 0; i < verts.Count; i++) pts[i] = space != null ? (Vector2)space.TransformPoint(verts[i]) : verts[i];
                paths.Add(pts);
            }
            if (paths.Count == 0) continue;
            shapes.Add((paths, KindOf(c.gameObject)));
            if (!any) { all = c.bounds; any = true; } else all.Encapsulate(c.bounds);
        }
        if (!any) return;

        all.Expand(new Vector3(8f, 8f, 0f));
        world = new Rect(Mathf.Floor(all.min.x), Mathf.Floor(all.min.y), Mathf.Ceil(all.size.x), Mathf.Ceil(all.size.y));
        texW = Mathf.Clamp(Mathf.CeilToInt(world.width * PxPerUnit), 8, 4096);
        texH = Mathf.Clamp(Mathf.CeilToInt(world.height * PxPerUnit), 8, 4096);
        kind = new byte[texW * texH];

        foreach (var (paths, k) in shapes) Fill(paths, k);
        cellsX = Mathf.CeilToInt(world.width / CellUnits);
        cellsY = Mathf.CeilToInt(world.height / CellUnits);
        explored = new bool[cellsX * cellsY];
        LoadExplored();
        built = true;
    }

    private static byte KindOf(GameObject go)
    {
        if (go.CompareTag("Water") || go.layer == 6) return 1;
        if (go.GetComponent<PlatformEffector2D>() != null) return 2;
        return 3;
    }

    // Even-odd scanline fill of closed paths (so holes stay holes), at pixel centres
    private void Fill(List<Vector2[]> paths, byte k)
    {
        var xs = new List<float>();
        for (int y = 0; y < texH; y++)
        {
            float wy = world.yMin + (y + 0.5f) / PxPerUnit;
            xs.Clear();
            foreach (Vector2[] path in paths)
            {
                for (int i = 0, j = path.Length - 1; i < path.Length; j = i++)
                {
                    Vector2 a = path[i], b = path[j];
                    if ((a.y > wy) == (b.y > wy)) continue;
                    xs.Add(a.x + (wy - a.y) / (b.y - a.y) * (b.x - a.x));
                }
            }
            if (xs.Count < 2) continue;
            xs.Sort();
            for (int n = 0; n + 1 < xs.Count; n += 2)
            {
                int x0 = Mathf.Max(0, Mathf.CeilToInt((xs[n] - world.xMin) * PxPerUnit - 0.5f));
                int x1 = Mathf.Min(texW - 1, Mathf.FloorToInt((xs[n + 1] - world.xMin) * PxPerUnit - 0.5f));
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * texW + x;
                    if (k > kind[i]) kind[i] = k;
                }
            }
        }
    }

    private string SaveKey => $"RD_Map_{sceneName}_{cellsX}x{cellsY}_{Mathf.RoundToInt(world.xMin)}_{Mathf.RoundToInt(world.yMin)}";

    private void SaveExplored()
    {
        if (!built || !exploredDirty) return;
        exploredDirty = false;
        var bytes = new byte[(explored.Length + 7) / 8];
        for (int i = 0; i < explored.Length; i++) if (explored[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
        PlayerPrefs.SetString(SaveKey, System.Convert.ToBase64String(bytes));
        PlayerPrefs.Save();
    }

    private void LoadExplored()
    {
        string s = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(s)) return;
        try
        {
            byte[] bytes = System.Convert.FromBase64String(s);
            for (int i = 0; i < explored.Length && (i >> 3) < bytes.Length; i++) explored[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
        }
        catch (System.FormatException) { }
    }

    // ---------------------------------------------------------------- drawing the explored part
    private void Compose()
    {
        if (mapTexture == null || mapTexture.width != texW || mapTexture.height != texH)
        {
            if (mapTexture != null) Destroy(mapTexture);
            mapTexture = new Texture2D(texW, texH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "WorldMap" };
        }

        var px = new Color32[texW * texH];
        int cellPx = Mathf.RoundToInt(CellUnits * PxPerUnit);
        int gridPx = 8 * PxPerUnit;
        for (int y = 0; y < texH; y++)
        {
            int cy = Mathf.Min(cellsY - 1, y / cellPx);
            for (int x = 0; x < texW; x++)
            {
                int cx = Mathf.Min(cellsX - 1, x / cellPx);
                bool seen = explored[cy * cellsX + cx];
                bool border = false;
                if (!seen)
                {
                    // ragged, dithered edge one cell into the unknown
                    for (int dy = -1; dy <= 1 && !border; dy++)
                        for (int dx = -1; dx <= 1 && !border; dx++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (nx >= 0 && ny >= 0 && nx < cellsX && ny < cellsY && explored[ny * cellsX + nx]) border = true;
                        }
                    if (!border || ((x + y) & 1) == 0 || ((x * 7 + y * 13) % 5 == 0)) { px[y * texW + x] = Clear; continue; }
                }

                Color32 c = Clear;
                byte k = kind[y * texW + x];
                if (k == 3)
                {
                    bool edge = Kind(x - 1, y) != 3 || Kind(x + 1, y) != 3 || Kind(x, y - 1) != 3 || Kind(x, y + 1) != 3;
                    c = edge ? SolidEdge : SolidFill;
                }
                else if (k == 2) c = Kind(x, y + 1) == 2 ? PlatformFill : PlatformTop;
                else if (k == 1) c = Kind(x, y + 1) == 0 ? WaterTop : WaterFill;
                else if (x % gridPx == 0 || y % gridPx == 0) c = GridLine;

                if (border) c.a = (byte)(c.a * 0.45f);
                px[y * texW + x] = c;
            }
        }
        mapTexture.SetPixels32(px);
        mapTexture.Apply(false);
    }

    private byte Kind(int x, int y) => x < 0 || y < 0 || x >= texW || y >= texH ? (byte)0 : kind[y * texW + x];

    // ---------------------------------------------------------------- UI
    private Canvas canvas;
    private GameObject root;
    private RectTransform panel, viewport, content, rowdyPin;
    private CanvasGroup panelGroup;
    private RawImage mapImage;
    private PixelText areaText;
    private readonly List<(RectTransform rect, Vector2 world, float popDelay)> pins = new List<(RectTransform, Vector2, float)>();
    private readonly List<GameObject> pinObjects = new List<GameObject>();
    private int zoom = DefaultZoom;
    private Vector2 center;       // map pixels
    private float openedAt;
    private bool pendingClose;

    private const float PanelW = 1720f, PanelH = 920f;

    public static void Toggle()
    {
        if (instance == null) return;
        if (IsOpen) instance.pendingClose = true; else instance.Open();
    }

    private void Open()
    {
        if (IsOpen || PauseMenu.IsPaused) return;
        if (!built) Build();
        if (!built) return;
        if (rowdy == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p != null) rowdy = p.transform; }
        if (rowdy != null) Reveal(rowdy.position, RevealRadius);
        if (root == null) BuildUI();

        Compose();
        mapImage.texture = mapTexture;
        BuildPins();
        center = rowdy != null ? WorldToMap(rowdy.position) : new Vector2(texW / 2f, texH / 2f);
        zoom = DefaultZoom;

        IsOpen = true;
        openedAt = Time.unscaledTime;
        PauseMenu.SetExternalPause(true);
        root.SetActive(true);
        UISound.Play(UISound.Cue.Open);
    }

    private void Close()
    {
        IsOpen = false;
        closedFrame = Time.frameCount;
        if (root != null) root.SetActive(false);
        PauseMenu.SetExternalPause(false);
        SaveExplored();
    }

    private Vector2 WorldToMap(Vector2 w) => new Vector2((w.x - world.xMin) * PxPerUnit, (w.y - world.yMin) * PxPerUnit);

    private void HandleInput()
    {
        if (pendingClose) return;
        float now = Time.unscaledTime;
        bool close = PadInput.R2Down || Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape) ||
                     Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton7);
        if (close && now - openedAt > 0.1f)
        {
            UISound.Play(UISound.Cue.Back);
            pendingClose = true;
            return;
        }

        int zoomDir = 0;
        if (Input.GetKeyDown(KeyCode.JoystickButton5) || Input.GetKeyDown(KeyCode.E) || Input.mouseScrollDelta.y > 0.1f) zoomDir = 1;
        if (Input.GetKeyDown(KeyCode.JoystickButton4) || Input.GetKeyDown(KeyCode.Q) || Input.mouseScrollDelta.y < -0.1f) zoomDir = -1;
        if (zoomDir != 0)
        {
            int z = Mathf.Clamp(zoom + zoomDir, 0, Zooms.Length - 1);
            if (z != zoom) { zoom = z; UISound.Play(UISound.Cue.Change); }
        }
        if ((Input.GetKeyDown(KeyCode.JoystickButton3) || Input.GetKeyDown(KeyCode.Space)) && rowdy != null)
        {
            center = WorldToMap(rowdy.position);
            UISound.Play(UISound.Cue.Move);
        }

        float h = 0f, v = 0f;
        try { h = Input.GetAxisRaw("Horizontal"); v = Input.GetAxisRaw("Vertical"); } catch (System.ArgumentException) { }
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) h = 1f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) h = -1f;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) v = 1f;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) v = -1f;
        Vector2 move = new Vector2(Mathf.Abs(h) > 0.2f ? h : 0f, Mathf.Abs(v) > 0.2f ? v : 0f);
        center += move * (700f / Zooms[zoom]) * Time.unscaledDeltaTime;
        center.x = Mathf.Clamp(center.x, 0f, texW);
        center.y = Mathf.Clamp(center.y, 0f, texH);
    }

    private void Animate()
    {
        float now = Time.unscaledTime;
        float open = Ease(Mathf.Clamp01((now - openedAt) / 0.22f));
        panelGroup.alpha = open;
        panel.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, open);

        // Unroll: the viewport opens out from the middle like a scroll
        float unroll = Ease(Mathf.Clamp01((now - openedAt - 0.05f) / 0.3f));
        viewport.offsetMin = new Vector2(Mathf.Lerp(PanelW / 2f, 40f, unroll), 90f);
        viewport.offsetMax = new Vector2(-Mathf.Lerp(PanelW / 2f, 40f, unroll), -130f);

        // Pan / zoom (eased)
        float scale = Zooms[zoom];
        float current = content.localScale.x;
        float s = Mathf.Lerp(current <= 0f ? scale : current, scale, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        content.localScale = new Vector3(s, s, 1f);
        Vector2 target = -center * s;
        content.anchoredPosition = Vector2.Lerp(content.anchoredPosition, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));

        // Pins keep their on-screen size whatever the zoom, pop in one by one, Rowdy pulses
        for (int i = 0; i < pins.Count; i++)
        {
            var (rect, _, delay) = pins[i];
            if (rect == null) continue;
            float pop = Mathf.Clamp01((now - openedAt - 0.25f - delay) / 0.18f);
            float popScale = pop < 1f ? EaseOutBack(pop) : 1f;
            float pulse = rect == rowdyPin ? 1f + 0.18f * Mathf.Sin(now * 7f) : 1f;
            rect.localScale = Vector3.one * (popScale * pulse / s);
        }

        // Area name under the title follows what's in the middle of the view
        string area = AreaAt(MapToWorld(center));
        areaText.SetText(area ?? "");
    }

    private Vector2 MapToWorld(Vector2 m) => new Vector2(m.x / PxPerUnit + world.xMin, m.y / PxPerUnit + world.yMin);

    private string AreaAt(Vector2 w)
    {
        WaveEnemySpawner spawner = FindFirstObjectByType<WaveEnemySpawner>();
        if (spawner == null || spawner.Zones == null) return null;
        foreach (WaveEnemySpawner.Zone z in spawner.Zones)
            if (z != null && z.Contains(w)) return CleanName(z.name);
        return null;
    }

    private static string CleanName(string n)
    {
        if (string.IsNullOrEmpty(n)) return "";
        int p = n.IndexOf('(');
        if (p > 0) n = n.Substring(0, p);
        return n.Trim().ToUpperInvariant();
    }

    private void BuildUI()
    {
        var canvasObject = new GameObject("World Map Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 455;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;

        Image dim = OverlayUI.MakeImage("Dim", canvasObject.transform, new Color(0.03f, 0.01f, 0.05f, 0.88f));
        Stretch(dim.rectTransform, 0f);
        root = dim.gameObject;

        Image back = OverlayUI.MakeImage("Panel", root.transform, Color.white, OverlayUI.PanelSprite);
        back.type = Image.Type.Sliced;
        back.pixelsPerUnitMultiplier = scaler.referencePixelsPerUnit / (64f * 4f);
        panel = back.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelW, PanelH);
        panelGroup = back.gameObject.AddComponent<CanvasGroup>();

        PixelText title = PixelText.Create(panel, "MAP", 5, TitleColor, 0f);
        TopLeft(title.Rect, new Vector2(60f, -36f));
        Image underline = OverlayUI.MakeImage("Underline", panel, HotPink);
        TopLeft(underline.rectTransform, new Vector2(56f, -36f - title.Rect.sizeDelta.y - 10f));
        underline.rectTransform.sizeDelta = new Vector2(title.Rect.sizeDelta.x + 8f, 4f);
        areaText = PixelText.Create(panel, "", 3, Pink, 1f);
        areaText.Rect.anchorMin = areaText.Rect.anchorMax = new Vector2(1f, 1f);
        areaText.Rect.anchoredPosition = new Vector2(-60f, -60f);

        // Map window (masked), dark "paper" behind the drawing
        Image view = OverlayUI.MakeImage("Viewport", panel, new Color(0.07f, 0.03f, 0.1f, 1f));
        viewport = view.rectTransform;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        view.gameObject.AddComponent<RectMask2D>();

        content = OverlayUI.MakeRect("Content", viewport);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = Vector2.zero;
        mapImage = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        mapImage.transform.SetParent(content, false);
        mapImage.raycastTarget = false;
        mapImage.rectTransform.anchorMin = mapImage.rectTransform.anchorMax = Vector2.zero;
        mapImage.rectTransform.pivot = Vector2.zero;

        // Legend + keys along the bottom
        float x = 60f;
        x = LegendItem(PinSprite(PinKind.Rowdy), HotPink, "ROWDY", x);
        x = LegendItem(PinSprite(PinKind.Flag), Gold, "SPAWNER", x);
        x = LegendItem(PinSprite(PinKind.Skull), new Color(1f, 0.35f, 0.4f), "DANGER", x);
        LegendItem(PinSprite(PinKind.Question), Pink, "???", x);
        PixelText keys = PixelText.Create(panel, "MOVE: STICK / ARROWS    ZOOM: L1 R1 / Q E    CENTER: Y / SPACE    CLOSE: R2 / M", 2, new Color(1f, 1f, 1f, 0.5f), 1f);
        keys.Rect.anchorMin = keys.Rect.anchorMax = new Vector2(1f, 0f);
        keys.Rect.anchoredPosition = new Vector2(-60f, 50f);

        root.SetActive(false);
    }

    private float LegendItem(Sprite icon, Color color, string label, float x)
    {
        Image img = OverlayUI.MakeImage("Legend", panel, color, icon);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 0f);
        img.rectTransform.pivot = new Vector2(0f, 0.5f);
        img.rectTransform.sizeDelta = new Vector2(30f, 30f);
        img.rectTransform.anchoredPosition = new Vector2(x, 50f);
        PixelText t = PixelText.Create(panel, label, 2, TitleColor, 0f);
        t.Rect.anchorMin = t.Rect.anchorMax = new Vector2(0f, 0f);
        t.Rect.anchoredPosition = new Vector2(x + 40f, 50f);
        return x + 40f + t.Rect.sizeDelta.x + 40f;
    }

    // ---------------------------------------------------------------- pins
    private enum PinKind { Rowdy, Flag, Skull, Question }
    private static readonly Dictionary<PinKind, Sprite> pinSprites = new Dictionary<PinKind, Sprite>();

    private void BuildPins()
    {
        foreach (GameObject go in pinObjects) if (go != null) Destroy(go);
        pinObjects.Clear();
        pins.Clear();

        mapImage.rectTransform.sizeDelta = new Vector2(texW, texH);

        // Area names (only once some of the area has been seen)
        WaveEnemySpawner spawner = FindFirstObjectByType<WaveEnemySpawner>();
        if (spawner != null && spawner.Zones != null)
        {
            var used = new HashSet<string>();
            foreach (WaveEnemySpawner.Zone z in spawner.Zones)
            {
                if (z == null) continue;
                string name = CleanName(z.name);
                if (string.IsNullOrEmpty(name) || !used.Add(name)) continue;
                float x0 = Mathf.Max(z.fromX, world.xMin), x1 = Mathf.Min(z.toX, world.xMax);
                float y0 = Mathf.Max(z.fromY, world.yMin), y1 = Mathf.Min(z.toY, world.yMax);
                if (x1 <= x0 || y1 <= y0) continue;
                // zones with a height band (Gloomy Forest up top) are labelled inside it, the rest under the ground line
                Vector2 c = new Vector2((x0 + x1) / 2f, z.fromY > world.yMin ? Mathf.Min((y0 + y1) / 2f, y0 + 8f) : world.yMin + 4f);
                if (!AnyExplored(x0, x1, y0, y1)) continue;
                PixelText label = PixelText.Create(content, name, 3, new Color(1f, 1f, 1f, 0.55f), 0.5f);
                AddPin(label.Rect, c, 0f);
            }
        }

        // Spawners: gold = the one you'd respawn at
        Vector2 respawn = new Vector2(PlayerPrefs.GetFloat("RespawnX", float.NaN), PlayerPrefs.GetFloat("RespawnY", float.NaN));
        float delay = 0.02f;
        foreach (RespawnTrigger r in FindObjectsByType<RespawnTrigger>(FindObjectsSortMode.None))
        {
            Vector2 p = r.transform.position;
            if (!Explored(p)) continue;
            bool active = !float.IsNaN(respawn.x) && Vector2.Distance(respawn, p) < 3f;
            AddIcon(PinKind.Flag, active ? Gold : new Color(0.75f, 0.65f, 0.45f, 0.8f), p, active ? 30f : 22f, delay += 0.03f);
        }

        // Danger: Pelich, the Elder; the old man as a question mark
        foreach (EnemyHealth e in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (e == null || !Explored(e.transform.position)) continue;
            EnemyCatalog.Entry entry = EnemyCatalog.Identify(e);
            if (entry == null) continue;
            if (entry.id == "pelich" || entry.id == "moonboundelder")
                AddIcon(PinKind.Skull, e.enemydead ? new Color(0.5f, 0.45f, 0.5f, 0.7f) : new Color(1f, 0.35f, 0.4f), e.transform.position, 30f, delay += 0.03f);
            else if (entry.id == "oldman" && !e.enemydead)
                AddIcon(PinKind.Question, Pink, e.transform.position, 26f, delay += 0.03f);
        }

        // Rowdy last, on top
        if (rowdy != null)
        {
            Image pin = AddIcon(PinKind.Rowdy, HotPink, rowdy.position, 30f, 0f);
            rowdyPin = pin.rectTransform;
        }
    }

    private bool AnyExplored(float x0, float x1, float y0, float y1)
    {
        for (float y = y0; y < y1; y += CellUnits)
            for (float x = x0; x < x1; x += CellUnits)
                if (Explored(new Vector2(x, y))) return true;
        return false;
    }

    private Image AddIcon(PinKind kindOfPin, Color color, Vector2 worldPos, float size, float delay)
    {
        Image img = OverlayUI.MakeImage("Pin", content, color, PinSprite(kindOfPin));
        img.rectTransform.sizeDelta = new Vector2(size, size);
        AddPin(img.rectTransform, worldPos, delay);
        return img;
    }

    private void AddPin(RectTransform rect, Vector2 worldPos, float delay)
    {
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = WorldToMap(worldPos);
        rect.localScale = Vector3.zero;
        pins.Add((rect, worldPos, delay));
        pinObjects.Add(rect.gameObject);
    }

    private static Sprite PinSprite(PinKind k)
    {
        if (pinSprites.TryGetValue(k, out Sprite s) && s != null) return s;
        string[] rows;
        switch (k)
        {
            case PinKind.Rowdy:
                rows = new[] { "....O....", "...OWO...", "..OWWWO..", ".OWWWWWO.", "OWWWWWWWO", ".OWWWWWO.", "..OWWWO..", "...OWO...", "....O...." };
                break;
            case PinKind.Flag:
                rows = new[] { ".OOOOO...", ".OWWWWO..", ".OWWWWWO.", ".OWWWWO..", ".OOOOO...", ".OW......", ".OW......", ".OW......", "OOOO....." };
                break;
            case PinKind.Skull:
                rows = new[] { "..OOOOO..", ".OWWWWWO.", "OWWWWWWWO", "OWOOWOOWO", "OWOOWOOWO", "OWWWOWWWO", ".OWWWWWO.", "..OWOWO..", "..OOOOO.." };
                break;
            default:
                rows = new[] { "..OOOOO..", ".OWWWWWO.", ".OWOOOWO.", "..O.OWWO.", "...OWWO..", "...OWO...", "...OOO...", "...OWO...", "...OOO..." };
                break;
        }
        s = OverlayUI.PixelSprite(rows, c => c == 'W' ? new Color32(255, 255, 255, 255) : new Color32(0x1B, 0x08, 0x20, 0xFF), "MapPin" + k);
        pinSprites[k] = s;
        return s;
    }

    // ---------------------------------------------------------------- helpers
    private static float Ease(float t) => 1f - (1f - t) * (1f - t);

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static void TopLeft(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
