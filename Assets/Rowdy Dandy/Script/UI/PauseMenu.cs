using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pause menu (Esc / gamepad Start) with Resume, Settings, Restart Level and Quit, plus a Settings page
// (display, audio, gameplay). Created automatically for every scene; pixel font + HUD panel style.
// Keyboard: arrows / WASD, Enter / Space, Esc.  Gamepad: D-pad, A, B, Start.  Mouse: hover + click.
// While open, time is fully stopped (TimeSlowController and gameplay input check PauseMenu.IsPaused).
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    private static PauseMenu instance;

    private enum Page { Closed, Main, Settings }

    private class Row
    {
        public RectTransform rect;
        public Image plate;
        public PixelText label;
        public PixelText value;
        public Image bar;
        public bool isHeader;
        public Action submit;
        public Action<int> change;
        public Func<string> getValue;
        public Func<float> getBar;
    }

    // Layout (UI pixels at 1080p)
    private const int TextScale = 3;          // 5x7 font -> 15x21 px letters
    private const float RowHeight = 48f;
    private const float RowGap = 6f;
    private const float SettingsRowWidth = 760f;
    private const float ButtonRowWidth = 440f;
    private const float ArrowLeftX = -330f;   // from the row's right edge
    private const float ValueX = -195f;
    private const float ArrowRightX = -60f;

    private static readonly Color Gold = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
    private static readonly Color Pink = new Color32(0xFF, 0x9B, 0xE6, 0xFF);
    private static readonly Color PlateColor = new Color32(0xB6, 0x0A, 0x7F, 0xE6);
    private static readonly Color Dim = new Color(0.04f, 0.01f, 0.07f, 0.72f);
    private static readonly Color TextIdle = new Color(0.82f, 0.76f, 0.88f, 1f);

    private Page page = Page.Closed;
    private GameObject root;
    private GameObject mainPanel, settingsPanel;
    private readonly List<Row> mainRows = new List<Row>();
    private readonly List<Row> settingsRows = new List<Row>();
    private int selected;
    private Sprite panelSprite;
    private float slicedMultiplier = 0.390625f;

    private float navRepeatTimer;
    private int heldVertical, heldHorizontal;
    private bool pendingResume;
    private bool savedCursorVisible;
    private CursorLockMode savedCursorLock;

    // ---------------------------------------------------------------- lifetime
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("PauseMenu (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PauseMenu>();
    }

    private void Awake()
    {
        panelSprite = MakePanelSprite();
        BuildUI();
        root.SetActive(false);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (IsPaused) SetPaused(false);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (page != Page.Closed) Close();
    }

    // ---------------------------------------------------------------- input
    private void Update()
    {
        bool pausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton9);
        bool backPressed = Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.Backspace);

        if (page == Page.Closed)
        {
            if (pausePressed) Open();
            return;
        }

        if (pausePressed || backPressed)
        {
            if (page == Page.Settings) ShowPage(Page.Main, 1);
            else Resume();
            return;
        }

        List<Row> rows = CurrentRows();
        int vertical = ReadDirection(KeyCode.UpArrow, KeyCode.W, KeyCode.DownArrow, KeyCode.S, "Vertical", ref heldVertical);
        int horizontal = ReadDirection(KeyCode.RightArrow, KeyCode.D, KeyCode.LeftArrow, KeyCode.A, "Horizontal", ref heldHorizontal);

        if (vertical != 0) MoveSelection(-vertical);
        if (horizontal != 0 && rows[selected].change != null)
        {
            rows[selected].change(horizontal);
            RefreshValues();
        }

        bool submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
        if (submit) Activate(selected, +1);
    }

    private void LateUpdate()
    {
        // Resume at the end of the frame, so the button press that closed the menu isn't read as a jump/attack
        if (pendingResume)
        {
            pendingResume = false;
            Close();
        }
    }

    // +1 / -1 / 0 with key repeat. For up/down, up is +1.
    private int ReadDirection(KeyCode posA, KeyCode posB, KeyCode negA, KeyCode negB, string axis, ref int held)
    {
        float value = 0f;
        try { value = Input.GetAxisRaw(axis); } catch (ArgumentException) { }
        int dir = 0;
        if (Input.GetKey(posA) || Input.GetKey(posB) || value > 0.5f) dir = 1;
        else if (Input.GetKey(negA) || Input.GetKey(negB) || value < -0.5f) dir = -1;

        if (dir == 0) { held = 0; return 0; }
        if (dir != held)
        {
            held = dir;
            navRepeatTimer = 0.35f;
            return dir;
        }
        navRepeatTimer -= Time.unscaledDeltaTime;
        if (navRepeatTimer <= 0f)
        {
            navRepeatTimer = 0.1f;
            return dir;
        }
        return 0;
    }

    // ---------------------------------------------------------------- open / close
    private void Open()
    {
        SetPaused(true);
        savedCursorVisible = Cursor.visible;
        savedCursorLock = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        EnsureEventSystem();
        root.SetActive(true);
        ShowPage(Page.Main, 0);
    }

    private void Resume() => pendingResume = true;

    private void Close()
    {
        root.SetActive(false);
        page = Page.Closed;
        Cursor.visible = savedCursorVisible;
        Cursor.lockState = savedCursorLock;
        SetPaused(false);
    }

    private static void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused; // music sources ignore this (AudioVolumeManager), so the soundtrack keeps going
    }

    private void ShowPage(Page newPage, int select)
    {
        page = newPage;
        mainPanel.SetActive(page == Page.Main);
        settingsPanel.SetActive(page == Page.Settings);
        RefreshValues();
        Select(FirstSelectable(CurrentRows(), select));
    }

    private List<Row> CurrentRows() => page == Page.Settings ? settingsRows : mainRows;

    // ---------------------------------------------------------------- selection
    private static int FirstSelectable(List<Row> rows, int from)
    {
        for (int i = Mathf.Clamp(from, 0, rows.Count - 1); i < rows.Count; i++) if (!rows[i].isHeader) return i;
        return 0;
    }

    private void MoveSelection(int delta)
    {
        List<Row> rows = CurrentRows();
        int i = selected;
        for (int n = 0; n < rows.Count; n++)
        {
            i = (i + delta + rows.Count) % rows.Count;
            if (!rows[i].isHeader) { Select(i); return; }
        }
    }

    private void Select(int index)
    {
        List<Row> rows = CurrentRows();
        selected = Mathf.Clamp(index, 0, rows.Count - 1);
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (row.isHeader) continue;
            bool on = i == selected;
            row.plate.enabled = on;
            row.label.Color = on ? Color.white : TextIdle;
            if (row.value != null) row.value.Color = on ? Gold : TextIdle;
        }
    }

    private void Activate(int index, int direction)
    {
        List<Row> rows = CurrentRows();
        if (index < 0 || index >= rows.Count) return;
        Row row = rows[index];
        if (row.submit != null) row.submit();
        else if (row.change != null) { row.change(direction); RefreshValues(); }
    }

    private void RefreshValues()
    {
        foreach (Row row in CurrentRows())
        {
            if (row.value != null && row.getValue != null) row.value.SetText(row.getValue());
            if (row.bar != null && row.getBar != null) row.bar.fillAmount = row.getBar();
        }
    }

    // ---------------------------------------------------------------- menu content
    private void BuildUI()
    {
        var canvasObject = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvas.pixelPerfect = true;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        slicedMultiplier = scaler.referencePixelsPerUnit / (64f * 4f);

        root = MakeImage("Dim", canvasObject.transform, Dim).gameObject;
        Stretch((RectTransform)root.transform);

        // ---- Main page
        float mainHeight = 150 + 4 * (RowHeight + RowGap) + 70;
        mainPanel = MakePanel("Main", root.transform, 560, mainHeight);
        float top = mainHeight / 2f;
        MakeLabel(mainPanel.transform, "PAUSED", 6, Gold, 0.5f, new Vector2(0, top - 70));
        float y = top - 150;
        AddButton(mainRows, mainPanel.transform, "Resume", ref y, Resume);
        AddButton(mainRows, mainPanel.transform, "Settings", ref y, () => ShowPage(Page.Settings, 0));
        AddButton(mainRows, mainPanel.transform, "Restart Level", ref y, RestartLevel);
        AddButton(mainRows, mainPanel.transform, "Quit Game", ref y, QuitGame);
        MakeHint(mainPanel.transform, -top + 34);

        // ---- Settings page
        float settingsHeight = 130 + 3 * 44 + 10 * (RowHeight + RowGap) + (RowHeight + RowGap + 16) + 70;
        settingsPanel = MakePanel("Settings", root.transform, 860, settingsHeight);
        top = settingsHeight / 2f;
        MakeLabel(settingsPanel.transform, "SETTINGS", 6, Gold, 0.5f, new Vector2(0, top - 66));
        y = top - 130;

        AddHeader(settingsRows, settingsPanel.transform, "Display", ref y);
        AddOption(settingsRows, settingsPanel.transform, "Resolution", ref y,
            () => { Vector2Int r = GameSettings.Resolutions[GameSettings.ResolutionIndex]; return $"{r.x} x {r.y}"; },
            d => GameSettings.SetResolution(Wrap(GameSettings.ResolutionIndex + d, GameSettings.Resolutions.Count)));
        AddOption(settingsRows, settingsPanel.transform, "Display Mode", ref y,
            () => GameSettings.DisplayModeName(GameSettings.DisplayMode),
            d => GameSettings.SetDisplayMode(GameSettings.DisplayModes[Wrap(Array.IndexOf(GameSettings.DisplayModes, GameSettings.DisplayMode) + d, GameSettings.DisplayModes.Length)]));
        AddOption(settingsRows, settingsPanel.transform, "VSync", ref y,
            () => GameSettings.VSync ? "On" : "Off",
            d => GameSettings.SetVSync(!GameSettings.VSync));
        AddOption(settingsRows, settingsPanel.transform, "Frame Limit", ref y,
            () => GameSettings.VSync ? "VSync" : (GameSettings.FrameLimit < 0 ? "Unlimited" : GameSettings.FrameLimit + " FPS"),
            d => GameSettings.SetFrameLimit(GameSettings.FrameLimits[Wrap(Array.IndexOf(GameSettings.FrameLimits, GameSettings.FrameLimit) + d, GameSettings.FrameLimits.Length)]));

        AddHeader(settingsRows, settingsPanel.transform, "Audio", ref y);
        AddVolume(settingsRows, settingsPanel.transform, "Master Volume", ref y, () => GameSettings.MasterVolume, GameSettings.SetMasterVolume);
        AddVolume(settingsRows, settingsPanel.transform, "Music", ref y, () => GameSettings.MusicVolume, GameSettings.SetMusicVolume);
        AddVolume(settingsRows, settingsPanel.transform, "Sound Effects", ref y, () => GameSettings.SfxVolume, GameSettings.SetSfxVolume);

        AddHeader(settingsRows, settingsPanel.transform, "Gameplay", ref y);
        AddOption(settingsRows, settingsPanel.transform, "Screen Shake", ref y,
            () => GameSettings.ScreenShake <= 0f ? "Off" : Mathf.RoundToInt(GameSettings.ScreenShake * 100f) + "%",
            d => GameSettings.SetScreenShake(GameSettings.ShakeLevels[Wrap(Array.IndexOf(GameSettings.ShakeLevels, GameSettings.ScreenShake) + d, GameSettings.ShakeLevels.Length)]));
        AddOption(settingsRows, settingsPanel.transform, "Vibration", ref y,
            () => GameSettings.Vibration ? "On" : "Off",
            d => GameSettings.SetVibration(!GameSettings.Vibration));
        AddOption(settingsRows, settingsPanel.transform, "Damage Numbers", ref y,
            () => GameSettings.DamageNumbers ? "On" : "Off",
            d => GameSettings.SetDamageNumbers(!GameSettings.DamageNumbers));

        y -= 16;
        AddButton(settingsRows, settingsPanel.transform, "Back", ref y, () => ShowPage(Page.Main, 1));
        MakeHint(settingsPanel.transform, -top + 34);
    }

    private static int Wrap(int i, int count) => count <= 0 ? 0 : ((i % count) + count) % count;

    private void RestartLevel()
    {
        Close();
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- row builders
    // A centered button (Resume, Back...)
    private void AddButton(List<Row> rows, Transform parent, string text, ref float y, Action action)
    {
        Row row = MakeRow(rows, parent, y, ButtonRowWidth);
        row.label = PixelText.Create(row.rect, text, TextScale, TextIdle, 0.5f);
        Anchor(row.label.Rect, new Vector2(0.5f, 0.5f), Vector2.zero);
        row.submit = action;
        y -= RowHeight + RowGap;
    }

    // "LABEL ................ <  VALUE  >"
    private void AddOption(List<Row> rows, Transform parent, string text, ref float y, Func<string> getValue, Action<int> change)
    {
        Row row = MakeRow(rows, parent, y, SettingsRowWidth);
        row.label = PixelText.Create(row.rect, text, TextScale, TextIdle, 0f);
        Anchor(row.label.Rect, new Vector2(0f, 0.5f), new Vector2(28, 0));

        row.value = PixelText.Create(row.rect, "", TextScale, TextIdle, 0.5f);
        Anchor(row.value.Rect, new Vector2(1f, 0.5f), new Vector2(ValueX, 0));
        row.getValue = getValue;
        row.change = change;

        int index = rows.Count - 1;
        MakeArrow(row.rect, "<", ArrowLeftX, () => { Select(index); change(-1); RefreshValues(); }, index);
        MakeArrow(row.rect, ">", ArrowRightX, () => { Select(index); change(+1); RefreshValues(); }, index);
        y -= RowHeight + RowGap;
    }

    // Like an option, with a bar + percentage between the arrows
    private void AddVolume(List<Row> rows, Transform parent, string text, ref float y, Func<float> get, Action<float> set)
    {
        AddOption(rows, parent, text, ref y, () => Mathf.RoundToInt(get() * 100f) + "%", d => set(Mathf.Round((get() + d * 0.1f) * 10f) / 10f));
        Row row = rows[rows.Count - 1];
        Anchor(row.value.Rect, new Vector2(1f, 0.5f), new Vector2(-122, 0));

        Image back = MakeImage("BarBack", row.rect, new Color(0.08f, 0.02f, 0.1f, 0.95f));
        Anchor(back.rectTransform, new Vector2(1f, 0.5f), new Vector2(-240, 0), new Vector2(120, 15));
        row.bar = MakeImage("Bar", row.rect, Pink);
        row.bar.sprite = WhiteSprite(); // Filled mode needs a sprite
        row.bar.type = Image.Type.Filled;
        row.bar.fillMethod = Image.FillMethod.Horizontal;
        Anchor(row.bar.rectTransform, new Vector2(1f, 0.5f), new Vector2(-240, 0), new Vector2(114, 9));
        row.getBar = get;
    }

    private void AddHeader(List<Row> rows, Transform parent, string text, ref float y)
    {
        var row = new Row { isHeader = true };
        var holder = new GameObject("Header " + text, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        row.rect = (RectTransform)holder.transform;
        Anchor(row.rect, new Vector2(0.5f, 0.5f), new Vector2(0, y - 10), new Vector2(SettingsRowWidth, 34));
        row.label = PixelText.Create(row.rect, text, 2, Pink, 0f);
        Anchor(row.label.Rect, new Vector2(0f, 0.5f), new Vector2(8, 0));
        rows.Add(row);
        y -= 44;
    }

    private Row MakeRow(List<Row> rows, Transform parent, float y, float width)
    {
        var row = new Row();
        Image plate = MakeImage("Row", parent, PlateColor);
        Anchor(plate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, y - RowHeight / 2f), new Vector2(width, RowHeight));
        row.rect = plate.rectTransform;
        row.plate = plate;
        plate.enabled = false; // only shown on the selected row

        // Invisible hitbox: hover selects the row, click activates it
        int index = rows.Count;
        Image hitbox = MakeImage("Hitbox", row.rect, new Color(0, 0, 0, 0));
        Stretch(hitbox.rectTransform);
        hitbox.raycastTarget = true;
        var pointer = hitbox.gameObject.AddComponent<MenuPointer>();
        pointer.onEnter = () => Select(index);
        pointer.onClick = () => { Select(index); Activate(index, +1); };

        rows.Add(row);
        return row;
    }

    private void MakeArrow(RectTransform rowRect, string glyph, float x, Action onClick, int index)
    {
        PixelText arrow = PixelText.Create(rowRect, glyph, TextScale, Gold, 0.5f);
        Anchor(arrow.Rect, new Vector2(1f, 0.5f), new Vector2(x, 0));
        // generous click area around the small glyph
        Image hit = MakeImage("ArrowHit", arrow.Rect, new Color(0, 0, 0, 0));
        Anchor(hit.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, RowHeight));
        hit.raycastTarget = true;
        var pointer = hit.gameObject.AddComponent<MenuPointer>();
        pointer.onEnter = () => Select(index);
        pointer.onClick = onClick;
    }

    private void MakeHint(Transform parent, float y)
    {
        MakeLabel(parent, "Arrows: move   Enter/A: OK   Esc/B: back", 2, new Color(1f, 1f, 1f, 0.55f), 0.5f, new Vector2(0, y));
    }

    private void MakeLabel(Transform parent, string text, int scale, Color color, float pivotX, Vector2 position)
    {
        PixelText label = PixelText.Create(parent, text, scale, color, pivotX);
        Anchor(label.Rect, new Vector2(0.5f, 0.5f), position);
    }

    // ---------------------------------------------------------------- UI helpers
    private GameObject MakePanel(string panelName, Transform parent, float width, float height)
    {
        Image panel = MakeImage(panelName, parent, Color.white);
        panel.sprite = panelSprite;
        panel.type = Image.Type.Sliced;
        panel.pixelsPerUnitMultiplier = slicedMultiplier;
        Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
        return panel.gameObject;
    }

    private static Image MakeImage(string objectName, Transform parent, Color color)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // Anchor at a point of the parent (keeps the rect's own pivot); optional size
    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2? size = null)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
        if (size.HasValue) rect.sizeDelta = size.Value;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled) return;
        var go = new GameObject("EventSystem (pause menu)", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(go);
    }

    private static Sprite whiteSprite;
    private static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return whiteSprite;
    }

    // Same pixel frame as the HUD's generated 9-slice panel (12x12, 4px border)
    private static Sprite MakePanelSprite()
    {
        var tex = new Texture2D(12, 12, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PausePanel" };
        Color32 outline = new Color32(0x1B, 0x08, 0x20, 0xFF), highlight = new Color32(0xFF, 0x39, 0xC0, 0xFF), border = new Color32(0xB6, 0x0A, 0x7F, 0xFF), fill = new Color32(0x2A, 0x0A, 0x2E, 0xF0), clear = new Color32(0, 0, 0, 0);
        var pixels = new Color32[144];
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                int top = 11 - y; // texture rows start at the bottom
                int d = Mathf.Min(Mathf.Min(x, 11 - x), Mathf.Min(top, 11 - top));
                bool corner = (x == 0 || x == 11) && (top == 0 || top == 11);
                pixels[y * 12 + x] = corner ? clear : d == 0 ? outline : d == 1 ? ((top <= 1 || x <= 1) ? highlight : border) : d == 2 ? outline : fill;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
    }

    // Mouse hover / click for menu rows and arrows
    private class MenuPointer : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public Action onEnter;
        public Action onClick;
        public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke();
        public void OnPointerClick(PointerEventData eventData) => onClick?.Invoke();
    }
}
