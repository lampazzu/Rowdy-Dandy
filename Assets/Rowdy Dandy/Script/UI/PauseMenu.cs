using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pause menu (Esc / gamepad Start) with Resume, Settings, Restart Level and Quit, plus a Settings page
// (display, audio, gameplay). Created automatically for every scene; pixel font + HUD panel style.
// Keyboard: arrows / WASD, Enter / Space, Esc.  Gamepad: stick / D-pad, south (A / Cross), east (B / Circle), Start.
// Mouse: hover + click. Hints show the buttons of whatever was used last (GameInput).
// While open, time is fully stopped (TimeSlowController and gameplay input check PauseMenu.IsPaused).
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    private static PauseMenu instance;

    private enum Page { Closed, Main, Settings, Controls, Accessibility, DevTools }

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
        public int column;
        public float y;
        public string description; // shown under the rows on pages with a description line (Accessibility, Dev Tools)

        // Selection feedback (see AnimateRows)
        public Image flash;          // white overlay on the plate: bright on select / confirm, fades out
        public Image accent;         // bright edge on the left of the selected plate
        public PixelText cursor;     // ">" in front of the label, bobbing
        public PixelText arrowLeft, arrowRight;
        public Vector2 labelHome, valueHome, arrowLeftHome, arrowRightHome;
        public float selectedAt = -10f;
        public float pulseAt = -10f; // confirm / value change
        public int pulseDirection;   // -1 / +1 for value changes, 0 for confirm
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

    private static readonly Color Pink = new Color32(0xFF, 0x9B, 0xE6, 0xFF);
    private static readonly Color HotPink = new Color32(0xFF, 0x39, 0xC0, 0xFF);
    private static readonly Color TitleColor = new Color32(0xF4, 0xEC, 0xFA, 0xFF);
    private static readonly Color PlateColor = new Color32(0xB6, 0x0A, 0x7F, 0xE6);
    private static readonly Color Dim = new Color(0.04f, 0.01f, 0.07f, 0.72f);
    private static readonly Color TextIdle = new Color(0.82f, 0.76f, 0.88f, 1f);

    // Feedback timing (seconds, unscaled)
    private const float PlateSlideTime = 0.09f;
    private const float FlashTime = 0.22f;
    private const float PulseTime = 0.16f;
    private const float PageInTime = 0.16f;

    private const float ColumnOffset = 410f;  // settings page: two columns of rows, this far left / right of center

    private Page page = Page.Closed;
    private GameObject root;
    private GameObject mainPanel, settingsPanel, controlsPanel, accessPanel, devPanel;
    private readonly List<Row> mainRows = new List<Row>();
    private readonly List<Row> settingsRows = new List<Row>();
    private readonly List<Row> controlsRows = new List<Row>();
    private readonly List<Row> accessRows = new List<Row>();
    private readonly List<Row> devRows = new List<Row>();
    private PixelText accessDescription, devDescription;
    private readonly List<PixelText> hints = new List<PixelText>();
    private readonly List<(PixelText text, string template)> padButtons = new List<(PixelText, string)>();
    private int hintsVersion = -1;
    private Page accessReturnPage = Page.Main;
    private int accessReturnRow;

    // Dev Tools: hold START (gamepad) while the pause menu opens, or F1 / ` on the keyboard
    private const float DevHoldTime = 0.6f;
    private float openedAt = -10f;
    private bool devHoldUsed;
    private int selected;
    private float columnX;     // x of the rows being built
    private int buildColumn;   // which settings column they belong to
    private Page controlsReturnPage = Page.Main;
    private int controlsReturnRow;
    private Sprite panelSprite;
    private float slicedMultiplier = 0.390625f;

    private float navRepeatTimer;
    private int heldVertical, heldHorizontal;
    private bool pendingResume;
    private Action pendingAction;
    private float pageShownAt = -10f;
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
        bool pausePressed = GameInput.Down(GameInput.Act.Pause);
        bool backPressed = GameInput.Down(GameInput.Act.Back);
        bool devPressed = GameInput.Down(GameInput.Act.DevMenu);

        if (page == Page.Closed)
        {
            bool blocked = RowdyNotes.BlocksPause || WorldMap.BlocksPause || Tutorials.BlocksPause || BoonPicker.BlocksInput;
            if (pausePressed && !blocked) Open();
            else if (devPressed && !blocked) { Open(); ShowPage(Page.DevTools, 0); }
            return;
        }

        if (pendingResume) return;
        if (hintsVersion != GameInput.DeviceVersion) RefreshHints();

        // Still holding START from opening the menu: that's the Dev Tools shortcut
        bool startHeld = GameInput.GamepadStartHeld;
        if (!startHeld) devHoldUsed = true;
        if (page == Page.Main && !devHoldUsed && Time.unscaledTime - openedAt > DevHoldTime)
        {
            devHoldUsed = true;
            UISound.Play(UISound.Cue.Page);
            ShowPage(Page.DevTools, 0);
            return;
        }

        if (devPressed && page != Page.DevTools) { UISound.Play(UISound.Cue.Page); ShowPage(Page.DevTools, 0); return; }

        if (pausePressed || backPressed || (devPressed && page == Page.DevTools))
        {
            UISound.Play(UISound.Cue.Back);
            if (page == Page.Settings) ShowPage(Page.Main, 1);
            else if (page == Page.Controls) CloseControls();
            else if (page == Page.Accessibility) CloseAccessibility();
            else if (page == Page.DevTools) ShowPage(Page.Main, 0);
            else Resume();
            return;
        }

        List<Row> rows = CurrentRows();
        int vertical = ReadDirection(GameInput.MoveY, ref heldVertical);
        int horizontal = ReadDirection(GameInput.MoveX, ref heldHorizontal);

        if (vertical != 0) MoveSelection(-vertical);
        if (horizontal != 0 && rows[selected].change != null)
        {
            ChangeValue(selected, horizontal);
        }
        else if (horizontal != 0 && (page == Page.Settings || page == Page.DevTools))
        {
            JumpColumn(horizontal);
        }

        bool submit = GameInput.Down(GameInput.Act.Submit);
        if (submit) Activate(selected, +1);

        AnimateRows();
    }

    private void LateUpdate()
    {
        // Resume at the end of the frame, so the button press that closed the menu isn't read as a jump/attack
        if (pendingResume)
        {
            pendingResume = false;
            Close();
            Action action = pendingAction;
            pendingAction = null;
            action?.Invoke();
        }
    }

    // +1 / -1 / 0 with key repeat. For up/down, up is +1.
    private int ReadDirection(float value, ref int held)
    {
        int dir = value > 0f ? 1 : value < 0f ? -1 : 0;

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
        UISound.Play(UISound.Cue.Page);
        openedAt = Time.unscaledTime;
        devHoldUsed = false;
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

    // Other full-screen menus (Rowdy Notes) stop the game the same way, so gameplay input / TimeSlowController behave
    public static void SetExternalPause(bool paused)
    {
        if (instance != null && instance.page != Page.Closed) return; // the pause menu owns it right now
        SetPaused(paused);
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
        controlsPanel.SetActive(page == Page.Controls);
        accessPanel.SetActive(page == Page.Accessibility);
        devPanel.SetActive(page == Page.DevTools);
        RefreshValues();
        pageShownAt = Time.unscaledTime;
        selected = -1; // so the first row gets the full select animation
        Select(FirstSelectable(CurrentRows(), select), false);
        AnimateRows();
    }

    private GameObject CurrentPanel()
    {
        switch (page)
        {
            case Page.Settings: return settingsPanel;
            case Page.Controls: return controlsPanel;
            case Page.Accessibility: return accessPanel;
            case Page.DevTools: return devPanel;
            default: return mainPanel;
        }
    }

    private List<Row> CurrentRows()
    {
        switch (page)
        {
            case Page.Settings: return settingsRows;
            case Page.Controls: return controlsRows;
            case Page.Accessibility: return accessRows;
            case Page.DevTools: return devRows;
            default: return mainRows;
        }
    }

    private PixelText CurrentDescription() => page == Page.Accessibility ? accessDescription : page == Page.DevTools ? devDescription : null;

    private void OpenAccessibility()
    {
        accessReturnPage = page;
        accessReturnRow = selected;
        ShowPage(Page.Accessibility, 0);
    }

    private void CloseAccessibility() => ShowPage(accessReturnPage == Page.Settings ? Page.Settings : Page.Main, accessReturnRow);

    private void OpenControls()
    {
        controlsReturnPage = page;
        controlsReturnRow = selected;
        ShowPage(Page.Controls, 0);
    }

    private void CloseControls() => ShowPage(controlsReturnPage == Page.Settings ? Page.Settings : Page.Main, controlsReturnRow);

    // Left / right on a row without a value (a button): hop to the closest row in the other column
    private void JumpColumn(int direction)
    {
        List<Row> rows = CurrentRows();
        Row current = rows[selected];
        int targetColumn = current.column + direction;
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].isHeader || rows[i].column != targetColumn) continue;
            float distance = Mathf.Abs(rows[i].y - current.y);
            if (distance < bestDistance) { bestDistance = distance; best = i; }
        }
        if (best >= 0) Select(best);
    }

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

    private void Select(int index, bool withSound = true)
    {
        List<Row> rows = CurrentRows();
        int previous = selected;
        selected = Mathf.Clamp(index, 0, rows.Count - 1);
        if (selected != previous)
        {
            rows[selected].selectedAt = Time.unscaledTime;
            if (withSound) UISound.Play(UISound.Cue.Move);
        }
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (row.isHeader) continue;
            bool on = i == selected;
            row.plate.enabled = on;
            row.label.Color = on ? Color.white : TextIdle;
            if (row.value != null) row.value.Color = on ? Color.white : TextIdle;
            if (row.accent != null) row.accent.enabled = on;
            if (row.cursor != null) row.cursor.gameObject.SetActive(on);
        }

        PixelText description = CurrentDescription();
        if (description != null) description.SetText(rows[selected].description ?? "");
    }

    private void Activate(int index, int direction)
    {
        List<Row> rows = CurrentRows();
        if (index < 0 || index >= rows.Count) return;
        Row row = rows[index];
        if (row.submit != null)
        {
            row.pulseAt = Time.unscaledTime;
            row.pulseDirection = 0;
            UISound.Play(UISound.Cue.Confirm);
            row.submit();
        }
        else if (row.change != null) ChangeValue(index, direction);
    }

    private void ChangeValue(int index, int direction)
    {
        Row row = CurrentRows()[index];
        row.change(direction);
        row.pulseAt = Time.unscaledTime;
        row.pulseDirection = direction;
        UISound.Play(UISound.Cue.Change);
        RefreshValues();
    }

    // ---------------------------------------------------------------- feedback animation
    // Street Fighter V-ish: the plate wipes in from the left with a white flash, the label slides over to make room
    // for a bobbing ">" cursor, confirming punches the row, changing a value kicks the arrow on that side.
    private void AnimateRows()
    {
        float now = Time.unscaledTime;

        // Page entrance: quick fade + rise
        GameObject panel = CurrentPanel();
        if (panel != null)
        {
            float t = Mathf.Clamp01((now - pageShownAt) / PageInTime);
            float ease = 1f - (1f - t) * (1f - t);
            var group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            group.alpha = ease;
            panel.transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, ease);
            ((RectTransform)panel.transform).anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Lerp(-18f, 0f, ease)));
        }

        List<Row> rows = CurrentRows();
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (row.isHeader) continue;
            bool on = i == selected;

            float sinceSelect = now - row.selectedAt;
            float slide = on ? Mathf.Clamp01(sinceSelect / PlateSlideTime) : 0f;
            row.plate.fillAmount = 1f - (1f - slide) * (1f - slide);

            // Flash: on select, and brighter on confirm
            float sincePulse = now - row.pulseAt;
            float flash = 0f;
            if (on) flash = Mathf.Max(flash, 0.55f * (1f - Mathf.Clamp01(sinceSelect / FlashTime)));
            if (row.pulseDirection == 0) flash = Mathf.Max(flash, 0.8f * (1f - Mathf.Clamp01(sincePulse / FlashTime)));
            if (row.flash != null)
            {
                row.flash.color = new Color(1f, 1f, 1f, flash);
                row.flash.fillAmount = row.plate.fillAmount;
            }

            // Label slides right while selected (room for the cursor); confirm punches it a bit further
            float pulse = 1f - Mathf.Clamp01(sincePulse / PulseTime);
            float nudge = on ? Mathf.Lerp(0f, 14f, row.plate.fillAmount) : 0f;
            if (row.pulseDirection == 0) nudge += 8f * pulse * pulse;
            bool centered = row.label.Rect.pivot.x > 0.25f;
            row.label.Rect.anchoredPosition = row.labelHome + new Vector2(Mathf.Round(centered ? nudge * 0.5f : nudge), 0f);

            if (row.cursor != null && on)
            {
                float bob = Mathf.Round(Mathf.Sin(now * 9f) * 2f);
                float labelLeft = row.label.Rect.anchoredPosition.x - (centered ? row.label.Rect.sizeDelta.x * 0.5f : 0f);
                row.cursor.Rect.anchorMin = row.cursor.Rect.anchorMax = row.label.Rect.anchorMin;
                row.cursor.Rect.anchoredPosition = new Vector2(labelLeft - 26f + bob, 0f);
                row.cursor.Color = new Color(1f, 1f, 1f, row.plate.fillAmount);
            }

            // Value change: value pops, the arrow on that side kicks outwards
            if (row.value != null)
            {
                float pop = row.pulseDirection != 0 ? pulse : 0f;
                row.value.Rect.localScale = Vector3.one * (1f + 0.18f * pop * pop);
                row.value.Rect.anchoredPosition = row.valueHome;
            }
            if (row.arrowLeft != null)
            {
                float kickL = row.pulseDirection < 0 ? pulse : 0f;
                float kickR = row.pulseDirection > 0 ? pulse : 0f;
                row.arrowLeft.Rect.anchoredPosition = row.arrowLeftHome + new Vector2(Mathf.Round(-8f * kickL), 0f);
                row.arrowRight.Rect.anchoredPosition = row.arrowRightHome + new Vector2(Mathf.Round(8f * kickR), 0f);
                Color arrowColor = on ? Color.white : Pink;
                row.arrowLeft.Color = kickL > 0.01f ? HotPink : arrowColor;
                row.arrowRight.Color = kickR > 0.01f ? HotPink : arrowColor;
            }
        }
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
        columnX = 0f;
        buildColumn = 0;
        float mainHeight = 150 + 6 * (RowHeight + RowGap) + 100;
        mainPanel = MakePanel("Main", root.transform, 620, mainHeight);
        float top = mainHeight / 2f;
        MakeTitle(mainPanel.transform, "PAUSED", top - 70);
        float y = top - 150;
        AddButton(mainRows, mainPanel.transform, "Resume", ref y, Resume);
        AddButton(mainRows, mainPanel.transform, "Settings", ref y, () => ShowPage(Page.Settings, 0));
        AddButton(mainRows, mainPanel.transform, "Accessibility", ref y, OpenAccessibility);
        AddButton(mainRows, mainPanel.transform, "Controls", ref y, OpenControls);
        AddButton(mainRows, mainPanel.transform, "Restart Level", ref y, RestartLevel);
        AddButton(mainRows, mainPanel.transform, "Quit Game", ref y, QuitGame);
        MakeHint(mainPanel.transform, -top + 34);

        // ---- Settings page (two columns: display + audio | gameplay + controls)
        float leftHeight = 2 * 44 + 10 + 12 * (RowHeight + RowGap); // right column is the long one now (style rank rows)
        float settingsHeight = 130 + leftHeight + 70;
        settingsPanel = MakePanel("Settings", root.transform, 2 * SettingsRowWidth + 140, settingsHeight);
        top = settingsHeight / 2f;
        MakeTitle(settingsPanel.transform, "SETTINGS", top - 66);
        y = top - 130;

        columnX = -ColumnOffset;
        buildColumn = 0;
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
        AddOption(settingsRows, settingsPanel.transform, "Show FPS", ref y,
            () => GameSettings.ShowFps ? "On" : "Off",
            d => GameSettings.SetShowFps(!GameSettings.ShowFps));

        AddHeader(settingsRows, settingsPanel.transform, "Audio", ref y);
        AddVolume(settingsRows, settingsPanel.transform, "Master Volume", ref y, () => GameSettings.MasterVolume, GameSettings.SetMasterVolume);
        AddVolume(settingsRows, settingsPanel.transform, "Music", ref y, () => GameSettings.MusicVolume, GameSettings.SetMusicVolume);
        AddVolume(settingsRows, settingsPanel.transform, "Sound Effects", ref y, () => GameSettings.SfxVolume, GameSettings.SetSfxVolume);
        AddVolume(settingsRows, settingsPanel.transform, "Rowdy Voice", ref y, () => GameSettings.RowdyVoiceVolume, GameSettings.SetRowdyVoiceVolume);
        AddVolume(settingsRows, settingsPanel.transform, "Cat Voice", ref y, () => GameSettings.CatVoiceVolume, GameSettings.SetCatVoiceVolume);

        // Right column
        columnX = ColumnOffset;
        buildColumn = 1;
        y = top - 130;
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
        AddOption(settingsRows, settingsPanel.transform, "Number Size", ref y,
            () => Mathf.RoundToInt(GameSettings.DamageNumberSize * 100f) + "%",
            d => GameSettings.SetDamageNumberSize(StepSize(GameSettings.DamageNumberSize, d)));
        AddOption(settingsRows, settingsPanel.transform, "Message Size", ref y,
            () => Mathf.RoundToInt(GameSettings.MessageSize * 100f) + "%",
            d => GameSettings.SetMessageSize(StepSize(GameSettings.MessageSize, d)));
        AddOption(settingsRows, settingsPanel.transform, "Style Rank", ref y,
            () => GameSettings.StyleRankOn ? "On" : "Off",
            d => GameSettings.SetStyleRankOn(!GameSettings.StyleRankOn));
        AddOption(settingsRows, settingsPanel.transform, "Rank Size", ref y,
            () => Mathf.RoundToInt(GameSettings.StyleRankSize * 100f) + "%",
            d => GameSettings.SetStyleRankSize(GameSettings.RankSizes[Mathf.Clamp(NearestIndex(GameSettings.RankSizes, GameSettings.StyleRankSize) + d, 0, GameSettings.RankSizes.Length - 1)]));
        AddOption(settingsRows, settingsPanel.transform, "Rank Position", ref y,
            () => GameSettings.RankPositions[GameSettings.StyleRankPosition],
            d => GameSettings.SetStyleRankPosition(GameSettings.StyleRankPosition + d));

        y -= 16;
        AddButton(settingsRows, settingsPanel.transform, "Accessibility", ref y, OpenAccessibility, SettingsRowWidth);
        AddButton(settingsRows, settingsPanel.transform, "Controls", ref y, OpenControls, SettingsRowWidth);
        AddButton(settingsRows, settingsPanel.transform, "Back", ref y, () => ShowPage(Page.Main, 1), SettingsRowWidth);
        MakeHint(settingsPanel.transform, -top + 34);
        columnX = 0f;
        buildColumn = 0;

        BuildControlsPage();
        BuildAccessibilityPage();
        BuildDevToolsPage();
    }

    // ---------------------------------------------------------------- accessibility page
    private void BuildAccessibilityPage()
    {
        const int rowsCount = 11;
        float height = 130 + rowsCount * (RowHeight + RowGap) + 140;
        accessPanel = MakePanel("Accessibility", root.transform, SettingsRowWidth + 120, height);
        float top = height / 2f;
        MakeTitle(accessPanel.transform, "ACCESSIBILITY", top - 66);
        float y = top - 130;
        columnX = 0f;
        buildColumn = 0;

        AddToggle(accessRows, accessPanel.transform, "Enemy Alerts", ref y, () => GameSettings.EnemyAlerts, GameSettings.SetEnemyAlerts,
            "A ! pops over enemies when they notice Rowdy");
        AddToggle(accessRows, accessPanel.transform, "Ranged Aim Lines", ref y, () => GameSettings.RangedAimLines, GameSettings.SetRangedAimLines,
            "Red trace showing where arrows and bombs will fly");
        AddToggle(accessRows, accessPanel.transform, "Auto Pick Up Weapons", ref y, () => GameSettings.AutoPickupWeapons, GameSettings.SetAutoPickupWeapons,
            "Walk over a weapon drop to grab it, no button");
        AddToggle(accessRows, accessPanel.transform, "Auto Equip Weapon", ref y, () => GameSettings.AutoEquipWeapon, GameSettings.SetAutoEquipWeapon,
            "Broken weapon? Switch to the next one. No Rod while you have a weapon");
        AddToggle(accessRows, accessPanel.transform, "Rowdy Outline", ref y, () => GameSettings.RowdyOutline, GameSettings.SetRowdyOutline,
            "Blue outline so Rowdy is easy to spot");
        AddToggle(accessRows, accessPanel.transform, "Boss Weakness Indicator", ref y, () => GameSettings.BossWeakness, GameSettings.SetBossWeakness,
            "Arrow and brackets on a boss's weak spot (Pelich's head)");
        AddToggle(accessRows, accessPanel.transform, "Tutorial Popups", ref y, () => GameSettings.TutorialPopups, GameSettings.SetTutorialPopups,
            "Short explanation the first time you find something new");
        AddToggle(accessRows, accessPanel.transform, "Blood", ref y, () => GameSettings.BloodOn, GameSettings.SetBlood,
            "Blood drops and puddles");
        AddToggle(accessRows, accessPanel.transform, "Kill Feed", ref y, () => GameSettings.KillFeedOn, GameSettings.SetKillFeed,
            "Who killed what, top right");
        AddToggle(accessRows, accessPanel.transform, "Button Hints", ref y, () => GameSettings.ButtonHints, GameSettings.SetButtonHints,
            "Notes / Stats / Map buttons, bottom left");
        y -= 10;
        AddButton(accessRows, accessPanel.transform, "Back", ref y, CloseAccessibility);

        accessDescription = PixelText.Create(accessPanel.transform, "", 2, new Color(1f, 0.85f, 0.95f, 0.8f), 0.5f);
        Anchor(accessDescription.Rect, new Vector2(0.5f, 0.5f), new Vector2(0, -top + 76));
        MakeHint(accessPanel.transform, -top + 34);
    }

    // ---------------------------------------------------------------- dev tools page (hold START / F1)
    // Testing helpers only - everything a player shouldn't see in the normal settings (Crowd Limit lives here).
    private void BuildDevToolsPage()
    {
        float columnHeight = 3 * 44 + 10 + 12 * (RowHeight + RowGap);
        float height = 130 + columnHeight + 120;
        devPanel = MakePanel("DevTools", root.transform, 2 * SettingsRowWidth + 140, height);
        float top = height / 2f;
        MakeTitle(devPanel.transform, "DEV TOOLS", top - 66);
        Transform panel = devPanel.transform;

        // Left: progress + travel
        float y = top - 130;
        columnX = -ColumnOffset;
        buildColumn = 0;
        AddHeader(devRows, panel, "Progress", ref y);
        AddDevButton(panel, "Reset Game", ref y, DevTools.ResetGame, "Wipes level, weapons, notes, stats, cats, tutorials", false);
        AddDevButton(panel, "Reset Tutorials", ref y, Tutorials.ResetAll, "First-time popups show again", true);
        AddDevButton(panel, "Level Up", ref y, DevTools.LevelUp, "+1 level (full heal + FX)", true);
        AddDevButton(panel, "Level Down", ref y, DevTools.LevelDown, "-1 level", true);
        AddDevButton(panel, "Reset Level", ref y, DevTools.ResetLevel, "Back to level 1", true);
        AddDevButton(panel, "Give All Weapons", ref y, DevTools.GiveAllWeapons, "Sword, Naginata and Cleaver, full durability", false);
        AddHeader(devRows, panel, "Travel", ref y);
        AddDevButton(panel, "First Checkpoint", ref y, DevTools.GoToFirstCheckpoint, "Reload at the start of the map", false);
        AddDevButton(panel, "Last Checkpoint", ref y, DevTools.GoToLastCheckpoint, "Reload at the right-most checkpoint (Pelich)", false);
        AddDevButton(panel, "Saved Checkpoint", ref y, DevTools.GoToSavedCheckpoint, "Reload at the checkpoint you touched last", false);
        AddHeader(devRows, panel, "Boons", ref y);
        AddDevButton(panel, "Offer A Boon", ref y, Boons.DevOfferPick, "Opens the boon picker (one extra pick)", false);
        AddDevButton(panel, "Hair Gel +3", ref y, () => Boons.AddRerolls(3), "Three rerolls for the boon cards", true);

        // Right: spawns + cheats
        y = top - 130;
        columnX = ColumnOffset;
        buildColumn = 1;
        AddHeader(devRows, panel, "Spawn", ref y);
        AddDevButton(panel, "Flying Rat", ref y, DevTools.SpawnRat, "Rare drop: protects a cat when you die", false);
        AddDevButton(panel, "Cat Treat Fish", ref y, DevTools.SpawnFish, "Rare drop: feeds every cat", false);
        AddDevButton(panel, "Random Statue", ref y, DevTools.SpawnStatue, "A statue near Rowdy", false);
        AddDevButton(panel, "Collect All Cats", ref y, DevTools.CollectAllCats, "Every cat in the level joins Rowdy", false);
        AddHeader(devRows, panel, "Cheats", ref y);
        AddToggle(devRows, panel, "God Mode", ref y, () => DevTools.GodMode, v => DevTools.GodMode = v, "Rowdy takes no damage");
        AddDevButton(panel, "Heal +50", ref y, DevTools.Heal, "Heals, and overheals past full", false);
        AddDevButton(panel, "Kill Nearby Enemies", ref y, DevTools.KillNearby, "Everything within 12 units", false);
        AddDevButton(panel, "Fill Moon Meter", ref y, Werewolf.Fill, "Call of the Moon is ready (needs the boon)", true);
        AddDevButton(panel, "Clear Boons", ref y, Boons.ClearAll, "Forget every boon and pick", true);
        AddOption(devRows, panel, "Crowd Limit", ref y,
            () => GameSettings.CrowdLimit <= 0 ? "Off" : GameSettings.CrowdLimit + " at once",
            d => GameSettings.SetCrowdLimit(Wrap(GameSettings.CrowdLimit + d, 9)));
        devRows[devRows.Count - 1].description = "How many wolves may press Rowdy at once";

        y -= 10;
        AddButton(devRows, panel, "Back", ref y, () => ShowPage(Page.Main, 0), SettingsRowWidth);
        columnX = 0f;
        buildColumn = 0;

        devDescription = PixelText.Create(panel, "", 2, new Color(1f, 0.85f, 0.4f, 0.85f), 0.5f);
        Anchor(devDescription.Rect, new Vector2(0.5f, 0.5f), new Vector2(0, -top + 76));
        MakeHint(panel, -top + 34);
    }

    // Dev button: stayOpen = the menu stays up (level up/down...), otherwise the game resumes and it runs
    private void AddDevButton(Transform parent, string text, ref float y, Action action, string description, bool stayOpen)
    {
        AddButton(devRows, parent, text, ref y, () =>
        {
            if (stayOpen) { action(); RefreshValues(); return; }
            pendingAction = action; // runs after the menu has closed (end of frame, like Resume)
            Resume();
        }, SettingsRowWidth);
        devRows[devRows.Count - 1].description = description;
    }

    private void AddToggle(List<Row> rows, Transform parent, string text, ref float y, Func<bool> get, Action<bool> set, string description)
    {
        AddOption(rows, parent, text, ref y, () => get() ? "On" : "Off", d => set(!get()));
        rows[rows.Count - 1].description = description;
    }

    private static int NearestIndex(float[] values, float v)
    {
        int best = 0;
        for (int i = 1; i < values.Length; i++) if (Mathf.Abs(values[i] - v) < Mathf.Abs(values[best] - v)) best = i;
        return best;
    }

    // Next / previous entry of GameSettings.TextSizes (no wrap-around, so it's clear where the ends are)
    private static float StepSize(float current, int direction)
    {
        float[] sizes = GameSettings.TextSizes;
        int index = 0;
        for (int i = 0; i < sizes.Length; i++) if (Mathf.Abs(sizes[i] - current) < Mathf.Abs(sizes[index] - current)) index = i;
        return sizes[Mathf.Clamp(index + direction, 0, sizes.Length - 1)];
    }

    // ---------------------------------------------------------------- controls page
    private static readonly Color PadA = new Color32(0x5C, 0xD6, 0x5C, 0xFF);
    private static readonly Color PadB = new Color32(0xF0, 0x4A, 0x4A, 0xFF);
    private static readonly Color PadX = new Color32(0x4A, 0x9C, 0xF5, 0xFF);
    private static readonly Color PadY = new Color32(0xF5, 0xD1, 0x3C, 0xFF);
    private static readonly Color PadGrey = new Color32(0xE6, 0xDC, 0xEE, 0xFF);

    private void BuildControlsPage()
    {
        const float width = 1560f, height = 900f;
        controlsPanel = MakePanel("Controls", root.transform, width, height);
        Transform panel = controlsPanel.transform;
        float top = height / 2f;
        MakeTitle(panel, "CONTROLS", top - 66);

        // ---- Keyboard (left)
        const float leftX = -390f, rightX = 390f;
        MakeLabel(panel, "KEYBOARD + MOUSE", 3, Pink, 0.5f, new Vector2(leftX, top - 140));
        // Button icons (ButtonIcons): keycaps, the arrow keys, the mouse
        string Key(string k) => ButtonIcons.Text("KB:" + k);
        string arrows = ButtonIcons.Text("ARROWS"), click = ButtonIcons.Text("MOUSE:LEFT");
        var keyboard = new (string keys, string action)[]
        {
            (Key("A") + " " + Key("D"), "Move  (or " + arrows + ")"),
            (Key("SPACE"), "Jump  (hold for higher)"),
            (Key("S"), "Duck"),
            (Key("S") + " + " + Key("SPACE"), "Drop through platform"),
            (Key("J"), "Attack  (or " + click + ")"),
            (Key("SHIFT"), "Surf dash  (or " + Key("L") + ")"),
            (Key("Q"), "Switch weapon"),
            (Key("E"), "Pick up / swap cat / rest"),
            (Key("K"), "Werewolf  (Call of the Moon boon)"),
            (Key("TAB"), "Rowdy Notes"),
            (Key("C"), "Stats"),
            (Key("M"), "Map"),
            (Key("ESC"), "Pause"),
        };
        float y = top - 200;
        foreach (var entry in keyboard)
        {
            PixelText keys = PixelText.Create(panel, entry.keys, 3, TextIdle, 0f);
            Anchor(keys.Rect, new Vector2(0.5f, 0.5f), new Vector2(leftX - 330f, y));
            PixelText action = PixelText.Create(panel, entry.action, 2, TextIdle, 0f);
            Anchor(action.Rect, new Vector2(0.5f, 0.5f), new Vector2(leftX - 80f, y));
            y -= 42f;
        }

        // ---- Gamepad (right): drawn controller + button list
        MakeLabel(panel, "GAMEPAD", 3, Pink, 0.5f, new Vector2(rightX, top - 140));
        Image pad = MakeImage("Gamepad", panel, Color.white);
        pad.sprite = GamepadSprite();
        Anchor(pad.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(rightX, top - 282), new Vector2(56 * 6, 34 * 6));

        // Button icons follow the last pad used (Xbox, PlayStation, Switch): see RefreshHints
        var gamepad = new (string button, string action)[]
        {
            ("{MOVE}", "Move  (down: duck)"),
            ("{JUMP}", "Jump  (hold for higher)"),
            ("{ATTACK}", "Attack"),
            ("{SURF}", "Surf dash"),
            ("{SWITCH}", "Switch weapon"),
            ("{INTERACT}", "Pick up / swap cat / rest"),
            ("{WOLF}", "Werewolf  (boon)"),
            ("{DOWN} + {JUMP}", "Drop through platform"),
            ("{STATS}", "Stats"),
            ("{NOTES}", "Rowdy Notes"),
            ("{MAP}", "Map"),
            ("{PAUSE}", "Pause"),
        };
        y = top - 402;
        foreach (var entry in gamepad)
        {
            PixelText button = PixelText.Create(panel, GameInput.Format(entry.button, GameInput.LastGamepad), 3, PadGrey, 1f);
            padButtons.Add((button, entry.button));
            Anchor(button.Rect, new Vector2(0.5f, 0.5f), new Vector2(rightX - 60f, y));
            PixelText action = PixelText.Create(panel, entry.action, 2, TextIdle, 0f);
            Anchor(action.Rect, new Vector2(0.5f, 0.5f), new Vector2(rightX - 30f, y));
            y -= 31f;
        }

        // Back
        y = -top + 130;
        AddButton(controlsRows, panel, "Back", ref y, CloseControls);
        MakeHint(panel, -top + 34);
    }

    // 56x34 pixel-art controller: grips, bumpers, sticks, d-pad, colored A/B/X/Y, view/menu
    private static Sprite gamepadSprite;
    private static Sprite GamepadSprite()
    {
        if (gamepadSprite != null) return gamepadSprite;
        const int W = 56, H = 34;
        var id = new int[W, H]; // 0 empty, 1 body, 2 dark, 3 light, 10+ colors
        void Disc(int cx, int cy, float r, int v) { for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) id[x, y] = v; }
        void Box(int x0, int y0, int x1, int y1, int v) { for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) if (x >= 0 && y >= 0 && x < W && y < H) id[x, y] = v; }

        Box(9, 8, 46, 21, 1);           // body
        Disc(11, 13, 5, 1); Disc(44, 13, 5, 1);
        Disc(13, 23, 8, 1); Disc(42, 23, 8, 1); // grips
        Box(8, 4, 18, 6, 3); Box(37, 4, 47, 6, 3);  // LB / RB
        Disc(15, 14, 4, 2); Disc(15, 14, 2.2f, 3);   // left stick
        Disc(35, 22, 3.6f, 2); Disc(35, 22, 1.8f, 3); // right stick
        Box(20, 19, 22, 25, 2); Box(18, 21, 24, 23, 2); // d-pad
        Disc(42, 10, 2.2f, 13); Disc(38, 14, 2.2f, 12); Disc(46, 14, 2.2f, 11); Disc(42, 18, 2.2f, 10); // Y X B A
        Box(25, 12, 26, 13, 2); Box(30, 12, 31, 13, 2); // view / menu

        Color32 outline = new Color32(0x1B, 0x08, 0x20, 0xFF);
        Color32 Pick(int v)
        {
            switch (v)
            {
                case 1: return new Color32(0x6A, 0x4A, 0x80, 0xFF);
                case 2: return new Color32(0x2A, 0x16, 0x34, 0xFF);
                case 3: return new Color32(0xB8, 0xA4, 0xC8, 0xFF);
                case 10: return PadA;
                case 11: return PadB;
                case 12: return PadX;
                case 13: return PadY;
                default: return new Color32(0, 0, 0, 0);
            }
        }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "GamepadLayout" };
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                Color32 c = Pick(id[x, y]);
                if (id[x, y] == 0)
                {
                    bool nearBody = false;
                    for (int dx = -1; dx <= 1 && !nearBody; dx++)
                        for (int dy = -1; dy <= 1 && !nearBody; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < W && ny < H && id[nx, ny] != 0) nearBody = true;
                        }
                    if (nearBody) c = outline;
                }
                px[(H - 1 - y) * W + x] = c; // texture rows start at the bottom
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        gamepadSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 16f);
        return gamepadSprite;
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
    private void AddButton(List<Row> rows, Transform parent, string text, ref float y, Action action, float width = ButtonRowWidth)
    {
        Row row = MakeRow(rows, parent, y, width);
        row.label = PixelText.Create(row.rect, text, TextScale, TextIdle, 0.5f);
        Anchor(row.label.Rect, new Vector2(0.5f, 0.5f), Vector2.zero);
        row.labelHome = row.label.Rect.anchoredPosition;
        row.submit = action;
        y -= RowHeight + RowGap;
    }

    // "LABEL ................ <  VALUE  >"
    private void AddOption(List<Row> rows, Transform parent, string text, ref float y, Func<string> getValue, Action<int> change)
    {
        Row row = MakeRow(rows, parent, y, SettingsRowWidth);
        row.label = PixelText.Create(row.rect, text, TextScale, TextIdle, 0f);
        Anchor(row.label.Rect, new Vector2(0f, 0.5f), new Vector2(28, 0));
        row.labelHome = row.label.Rect.anchoredPosition;

        row.value = PixelText.Create(row.rect, "", TextScale, TextIdle, 0.5f);
        Anchor(row.value.Rect, new Vector2(1f, 0.5f), new Vector2(ValueX, 0));
        row.valueHome = row.value.Rect.anchoredPosition;
        row.getValue = getValue;
        row.change = change;

        int index = rows.Count - 1;
        MakeArrow(row, "<", ArrowLeftX, () => { Select(index); ChangeValue(index, -1); }, index);
        MakeArrow(row, ">", ArrowRightX, () => { Select(index); ChangeValue(index, +1); }, index);
        y -= RowHeight + RowGap;
    }

    // Like an option, with a bar + percentage between the arrows
    private void AddVolume(List<Row> rows, Transform parent, string text, ref float y, Func<float> get, Action<float> set)
    {
        AddOption(rows, parent, text, ref y, () => Mathf.RoundToInt(get() * 100f) + "%", d => set(Mathf.Round((get() + d * 0.1f) * 10f) / 10f));
        Row row = rows[rows.Count - 1];
        Anchor(row.value.Rect, new Vector2(1f, 0.5f), new Vector2(-122, 0));
        row.valueHome = row.value.Rect.anchoredPosition;

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
        // A little breathing room above a header that follows other rows in the same column
        if (rows.Count > 0 && rows[rows.Count - 1].column == buildColumn) y -= 10;
        var row = new Row { isHeader = true, column = buildColumn, y = y };
        var holder = new GameObject("Header " + text, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        row.rect = (RectTransform)holder.transform;
        Anchor(row.rect, new Vector2(0.5f, 0.5f), new Vector2(columnX, y - 10), new Vector2(SettingsRowWidth, 34));
        row.label = PixelText.Create(row.rect, text, 2, Pink, 0f);
        Anchor(row.label.Rect, new Vector2(0f, 0.5f), new Vector2(8, 0));
        rows.Add(row);
        y -= 44;
    }

    private Row MakeRow(List<Row> rows, Transform parent, float y, float width)
    {
        var row = new Row { column = buildColumn, y = y };
        Image plate = MakeImage("Row", parent, PlateColor);
        Anchor(plate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(columnX, y - RowHeight / 2f), new Vector2(width, RowHeight));
        MakeWipe(plate);
        row.rect = plate.rectTransform;
        row.plate = plate;
        plate.enabled = false; // only shown on the selected row

        // Feedback pieces: white flash over the plate, hot pink edge on its left, ">" cursor
        row.flash = MakeImage("Flash", row.rect, new Color(1f, 1f, 1f, 0f));
        Stretch(row.flash.rectTransform);
        MakeWipe(row.flash);
        row.accent = MakeImage("Accent", row.rect, HotPink);
        row.accent.rectTransform.anchorMin = new Vector2(0f, 0f);
        row.accent.rectTransform.anchorMax = new Vector2(0f, 1f);
        row.accent.rectTransform.pivot = new Vector2(0f, 0.5f);
        row.accent.rectTransform.anchoredPosition = Vector2.zero;
        row.accent.rectTransform.sizeDelta = new Vector2(6f, 0f);
        row.accent.enabled = false;
        row.cursor = PixelText.Create(row.rect, ">", TextScale, Color.white, 0f);
        row.cursor.gameObject.SetActive(false);

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

    // Image that can wipe in from the left (Filled, horizontal)
    private static void MakeWipe(Image image)
    {
        image.sprite = WhiteSprite();
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    // Title: white pixel text with a hot pink underline (was a big gold word)
    private void MakeTitle(Transform parent, string text, float y)
    {
        PixelText title = PixelText.Create(parent, text, 5, TitleColor, 0.5f);
        Anchor(title.Rect, new Vector2(0.5f, 0.5f), new Vector2(0, y));
        float width = title.Rect.sizeDelta.x + 48f;
        Image line = MakeImage("Title Line", parent, HotPink);
        Anchor(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, y - title.Rect.sizeDelta.y / 2f - 12f), new Vector2(width, 4f));
        Image shade = MakeImage("Title Line Shade", parent, new Color(0.1f, 0.02f, 0.12f, 0.9f));
        Anchor(shade.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, y - title.Rect.sizeDelta.y / 2f - 16f), new Vector2(width, 4f));
    }

    private void MakeArrow(Row row, string glyph, float x, Action onClick, int index)
    {
        PixelText arrow = PixelText.Create(row.rect, glyph, TextScale, Pink, 0.5f);
        Anchor(arrow.Rect, new Vector2(1f, 0.5f), new Vector2(x, 0));
        if (glyph == "<") { row.arrowLeft = arrow; row.arrowLeftHome = arrow.Rect.anchoredPosition; }
        else { row.arrowRight = arrow; row.arrowRightHome = arrow.Rect.anchoredPosition; }
        // generous click area around the small glyph
        Image hit = MakeImage("ArrowHit", arrow.Rect, new Color(0, 0, 0, 0));
        Anchor(hit.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, RowHeight));
        hit.raycastTarget = true;
        var pointer = hit.gameObject.AddComponent<MenuPointer>();
        pointer.onEnter = () => Select(index);
        pointer.onClick = onClick;
    }

    private const string HintText = "{MOVE}: move   {OK}: OK   {BACK}: back";

    private void MakeHint(Transform parent, float y)
    {
        PixelText hint = PixelText.Create(parent, GameInput.Format(HintText), 2, new Color(1f, 1f, 1f, 0.55f), 0.5f);
        Anchor(hint.Rect, new Vector2(0.5f, 0.5f), new Vector2(0, y));
        hints.Add(hint);
    }

    // Hints and the Controls page's gamepad column follow the device (keyboard / Xbox / PlayStation / Switch)
    private void RefreshHints()
    {
        hintsVersion = GameInput.DeviceVersion;
        string hint = GameInput.Format(HintText);
        foreach (PixelText p in hints) if (p != null) p.SetText(hint);

        GameInput.Device pad = GameInput.LastGamepad;
        foreach (var (text, template) in padButtons)
            if (text != null) text.SetText(GameInput.Format(template, pad));
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
