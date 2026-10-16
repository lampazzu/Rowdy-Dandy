using UnityEngine;
using UnityEngine.UI;

// The day / night clock, top-right corner (the kill feed sits under it): a sun or a moon, DAY or NIGHT, and a bar
// with the time left until it flips. When it flips, the clock pops and - if Rowdy has boons that only work by day or
// by night - a popup says which ones just switched on.
// Built at runtime on the overlay canvas. Hidden in menus, the map, the notes and while nothing drives the cycle.
public class DayNightHUD : MonoBehaviour
{
    public const float Reserve = 74f; // UI pixels the kill feed leaves free above itself
    private const float Width = 250f, Height = 60f, Margin = 18f;

    private static DayNightHUD instance;
    private RectTransform root;
    private CanvasGroup group;
    private Image icon, barBack, barFill;
    private PixelText label, timer;
    private bool shownNight;
    private int shownSeconds = -1;
    private float pop;
    private bool started;

    private static readonly Color DayColor = new Color(1f, 0.82f, 0.3f), NightColor = new Color(0.62f, 0.72f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("DayNightHUD (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DayNightHUD>();
    }

    private void Start()
    {
        root = OverlayUI.MakeRect("Day Night Clock", OverlayUI.Root);
        OverlayUI.Place(root, new Vector2(1f, 1f), new Vector2(-Margin, -Margin), new Vector2(Width, Height));
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0f;
        Image panel = OverlayUI.MakePanel("Panel", root);
        panel.rectTransform.anchorMin = Vector2.zero; panel.rectTransform.anchorMax = Vector2.one;
        panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;

        icon = OverlayUI.MakeImage("Icon", root, Color.white, DayNightIcons.Sun);
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0f, 0.5f);
        icon.rectTransform.anchoredPosition = new Vector2(12f, 0f);
        icon.rectTransform.sizeDelta = new Vector2(40f, 40f);

        label = PixelText.Create(root, "DAY", 3, DayColor, 0f);
        label.Rect.anchorMin = label.Rect.anchorMax = new Vector2(0f, 0.5f);
        label.Rect.anchoredPosition = new Vector2(64f, 10f);
        timer = PixelText.Create(root, "", 2, new Color(0.92f, 0.9f, 1f), 1f);
        timer.Rect.anchorMin = timer.Rect.anchorMax = new Vector2(1f, 0.5f);
        timer.Rect.anchoredPosition = new Vector2(-12f, 10f);

        barBack = OverlayUI.MakeImage("Bar", root, new Color(0.08f, 0.02f, 0.1f, 0.9f), OverlayUI.WhiteSprite);
        barBack.rectTransform.anchorMin = barBack.rectTransform.anchorMax = barBack.rectTransform.pivot = new Vector2(0f, 0.5f);
        barBack.rectTransform.anchoredPosition = new Vector2(64f, -13f);
        barBack.rectTransform.sizeDelta = new Vector2(Width - 76f, 8f);
        barFill = OverlayUI.MakeImage("Fill", barBack.rectTransform, DayColor, OverlayUI.WhiteSprite);
        barFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        barFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        barFill.rectTransform.offsetMin = new Vector2(1f, 1f);
        barFill.rectTransform.offsetMax = new Vector2(-1f, -1f);
        shownNight = DayNight.IsNight;
        Show(shownNight);
    }

    private void Show(bool night)
    {
        bool eternal = DayNight.NightBanned;
        icon.sprite = night ? DayNightIcons.Moon : DayNightIcons.Sun;
        label.SetText(eternal ? "ETERNAL DAY" : night ? "NIGHT" : "DAY");
        label.Color = night ? NightColor : DayColor;
        barFill.color = night ? NightColor : DayColor;
        timer.gameObject.SetActive(!eternal);
        barBack.gameObject.SetActive(!eternal);
    }

    private void Update()
    {
        if (root == null) return;
        bool player = BoonRunner.Rowdy != null;
        bool hidden = !player || !DayNight.Running || (PauseMenu.IsPaused && !StatsPause.IsOpen) || RowdyNotes.IsOpen || WorldMap.IsOpen
                      || BoonPicker.IsOpen || CatParty.IsOpen;
        group.alpha = Mathf.MoveTowards(group.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime * 6f);
        if (!DayNight.Running) return;

        bool night = DayNight.IsNight;
        if (night != shownNight)
        {
            shownNight = night;
            Show(night);
            if (started) Flipped(night);
        }
        started = true;

        // time left until the flip, and the bar emptying toward it
        float left = Mathf.Max(0f, DayNight.SecondsToFlip);
        int secs = Mathf.CeilToInt(left);
        if (secs != shownSeconds)
        {
            shownSeconds = secs;
            timer.SetText((night ? "DAY IN " : "NIGHT IN ") + secs / 60 + ":" + (secs % 60).ToString("00"));
        }
        float k = DayNight.PhaseLength > 0.01f ? Mathf.Clamp01(left / DayNight.PhaseLength) : 0f;
        barFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.001f, k), 1f);

        // the last 10 seconds: the icon pulses (it's about to turn)
        float now = Time.unscaledTime;
        bool soon = left < 10f && !DayNight.NightBanned;
        pop = Mathf.MoveTowards(pop, 0f, Time.unscaledDeltaTime * 2f);
        float s = 1f + 0.35f * pop * pop + (soon ? 0.08f * Mathf.Abs(Mathf.Sin(now * 6f)) : 0f);
        icon.rectTransform.localScale = Vector3.one * s;
        label.Rect.localScale = Vector3.one * (1f + 0.2f * pop * pop);
    }

    // The flip: the clock pops, and (only if Rowdy owns any) the day or night boons that just switched on are named
    private void Flipped(bool night)
    {
        pop = 1f;
        UISound.Play(UISound.Cue.Change);
        if (BoonRunner.Rowdy == null) return;
        int on = 0;
        foreach (string id in night ? Boons.NightIds : Boons.DayIds) if (Boons.Has(id) && id != "eclipse") on++;
        if (on == 0 || Boons.Has("eclipse")) return;
        Color c = night ? NightColor : DayColor;
        IconPopup.Show(BoonRunner.RowdyHead + Vector3.up * 0.5f, night ? DayNightIcons.Moon : DayNightIcons.Sun,
            (night ? "NIGHT BOONS ON" : "DAY BOONS ON") + " (" + on + ")", c, 0.8f, 2f);
    }
}

// The sun and the moon (HUD clock, boon HUD badges). Pixel art, made here until redrawn (AI Placeholders).
public static class DayNightIcons
{
    private static Sprite sun, moon;

    public static Sprite Sun
    {
        get
        {
            if (sun != null) return sun;
            sun = OverlayUI.PixelSprite(new[]
            {
                "......y.......",
                "..y...y...y...",
                "...y.....y....",
                ".....kkk......",
                "....kooook....",
                "...koyyyyok...",
                "yy.koyywyok.yy",
                "...koyyyyok...",
                "...koyyyyok...",
                "....kooook....",
                ".....kkk......",
                "...y.....y....",
                "..y...y...y...",
                "......y.......",
            }, c => c == 'y' ? new Color32(255, 220, 90, 255) : c == 'o' ? new Color32(245, 150, 40, 255) : c == 'w' ? new Color32(255, 250, 220, 255) : new Color32(0x1B, 0x08, 0x20, 0xFF), "HUD_Sun");
            return sun;
        }
    }

    public static Sprite Moon
    {
        get
        {
            if (moon != null) return moon;
            moon = OverlayUI.PixelSprite(new[]
            {
                "....kkkk......",
                "..kkmmmk......",
                ".kmmwmk.......",
                ".kmwmk........",
                "kmmwk.........",
                "kmmmk.........",
                "kmmmk.........",
                "kmmmk.........",
                "kmmmmk........",
                ".kmmmmk.......",
                ".kmmmmmkk.....",
                "..kkmmmmmkk...",
                "....kkkkkk....",
                "..............",
            }, c => c == 'm' ? new Color32(205, 215, 255, 255) : c == 'w' ? new Color32(255, 255, 255, 255) : new Color32(0x1B, 0x08, 0x20, 0xFF), "HUD_Moon");
            return moon;
        }
    }
}
