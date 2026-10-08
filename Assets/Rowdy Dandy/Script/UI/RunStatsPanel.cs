using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// "Run stats" panel shown next to the level stats panel while the stats screen is open (C / gamepad L2).
// Reads RunStats, grouped by category in two columns: COMBAT | SURVIVAL, CATS | LOOT & WORLD. Also keeps the play time
// clock running. Created automatically.
// Juice: the panel pops in with an overshoot, the rows slide in one after another with a tick, numbers count up
// from zero, category headers flash in their colour; closing plays a soft whoosh.
public class RunStatsPanel : MonoBehaviour
{
    private const int TextScale = 2;
    private const float RowHeight = 26f;
    private const float HeaderHeight = 38f;
    private const float ColumnWidth = 470f;
    private const float ColumnGap = 26f;
    private const float Padding = 22f;
    private const int MaxValueChars = 19;

    private const float PopTime = 0.22f;
    private const float RowStagger = 0.022f;
    private const float RowSlideTime = 0.16f;
    private const float CountTime = 0.45f;

    private static readonly Color Gold = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
    private static readonly Color Pink = new Color32(0xFF, 0x9B, 0xE6, 0xFF);
    private static readonly Color Red = new Color32(0xFF, 0x6A, 0x6A, 0xFF);
    private static readonly Color Green = new Color32(0x7C, 0xF0, 0x8C, 0xFF);
    private static readonly Color Blue = new Color32(0x8C, 0xD2, 0xFF, 0xFF);
    private static readonly Color LabelColor = new Color(0.82f, 0.76f, 0.88f, 1f);

    private static RunStatsPanel instance;

    private class Row
    {
        public RectTransform rect;
        public PixelText label, value;
        public Func<string> get;
        public Color color;
        public bool header;
        public Image underline;
        public Vector2 home;
        public int order;      // reveal order
        public string shown = "";
    }

    private RectTransform panel;
    private CanvasGroup group;
    private readonly List<Row> rows = new List<Row>();
    private float refreshTimer;
    private float openedAt = -10f;
    private bool visible;
    private int ticked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("RunStatsPanel (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RunStatsPanel>();
    }

    public static void SetVisible(bool visible, RectTransform besides)
    {
        if (instance == null) Create();
        instance.Show(visible, besides);
    }

    public static void Refresh()
    {
        if (instance == null || instance.panel == null || !instance.panel.gameObject.activeSelf) return;
        instance.refreshTimer -= Time.unscaledDeltaTime;
        if (instance.refreshTimer > 0f) return;
        instance.refreshTimer = 0.25f;
        instance.UpdateValues(false);
    }

    private void Update()
    {
        if (!PauseMenu.IsPaused) RunStats.PlayTime += Time.unscaledDeltaTime;
        if (visible && panel != null) Animate();
    }

    private void Show(bool show, RectTransform besides)
    {
        if (panel == null) Build();
        if (show == visible) return;
        visible = show;
        panel.gameObject.SetActive(show);
        if (!show)
        {
            UISound.Play(UISound.Cue.Back);
            return;
        }

        // To the right of the level stats panel when it's anchored top-left like the HUD organizer places it
        Vector2 position = new Vector2(436f, -248f);
        if (besides != null && besides.anchorMin == new Vector2(0f, 1f) && besides.anchorMax == new Vector2(0f, 1f))
        {
            position = new Vector2(besides.anchoredPosition.x + besides.sizeDelta.x * (1f - besides.pivot.x) + 12f,
                                   besides.anchoredPosition.y + besides.sizeDelta.y * (1f - besides.pivot.y));
        }
        OverlayUI.Place(panel, new Vector2(0f, 1f), position);
        openedAt = Time.unscaledTime;
        ticked = 0;
        refreshTimer = 0f;
        foreach (Row r in rows) r.shown = "";
        UpdateValues(true);
        UISound.Play(UISound.Cue.Open);
        Animate();
    }

    private void Build()
    {
        var columns = new[]
        {
            new (string title, Color color, (string label, Func<string> get, Color color)[] entries)[]
            {
                ("COMBAT", Red, new (string, Func<string>, Color)[]
                {
                    ("Enemies Killed", () => RunStats.Kills.ToString(), Gold),
                    ("Best Kill Streak", () => RunStats.BestStreak.ToString(), Gold),
                    ("Most Hunted", RunStats.FavoriteVictim, Color.white),
                    ("Max Damage", () => RunStats.MaxHit <= 0f ? "-" : Number(RunStats.MaxHit) + (string.IsNullOrEmpty(RunStats.MaxHitWith) ? "" : " (" + RunStats.MaxHitWith + ")"), Gold),
                    ("Total Damage", () => Number(RunStats.TotalDamage), Color.white),
                    ("Critical Hits", () => RunStats.CriticalHits.ToString(), Color.white),
                    ("Counters", () => RunStats.Counters.ToString(), Color.white),
                    ("Weapons Broken", () => RunStats.WeaponsBroken.ToString(), Color.white),
                }),
                ("SURVIVAL", Green, new (string, Func<string>, Color)[]
                {
                    ("Damage Taken", () => Number(RunStats.DamageTaken), Color.white),
                    ("Deaths", () => RunStats.Deaths.ToString(), Red),
                    ("HP Healed", () => Number(RunStats.Healed), Green),
                    ("Overheal Gained", () => Number(RunStats.OverhealGained), Gold),
                    ("Hits Blocked", () => RunStats.HitsBlocked.ToString(), Blue),
                    ("Checkpoints Lit", () => RunStats.Checkpoints.ToString(), Color.white),
                    ("Level Ups", () => RunStats.LevelUps.ToString(), Gold),
                }),
            },
            new (string title, Color color, (string label, Func<string> get, Color color)[] entries)[]
            {
                ("CATS", Pink, new (string, Func<string>, Color)[]
                {
                    ("Cat Capacity (MCC)", () => CatRoster.CatCount + " / " + CatRoster.Capacity, Pink),
                    ("Kills By Your Cats", () => RunStats.CatKills.ToString(), Pink),
                    ("Executions", () => RunStats.Executions.ToString(), Pink),
                    ("Enemies Poisoned", () => RunStats.EnemiesPoisoned.ToString(), Green),
                    ("Enemies Stunned", () => RunStats.EnemiesStunned.ToString(), Gold),
                    ("Decay Bursts", () => RunStats.DecayBursts.ToString(), new Color(0.8f, 0.55f, 1f)),
                    ("Cats Rescued", () => RunStats.CatsRescued.ToString(), Pink),
                    ("Cats Lost", () => RunStats.CatsLost.ToString(), Red),
                    ("Rats Registered", () => RunStats.RatsGrabbed.ToString(), Gold),
                }),
                ("LOOT & WORLD", Blue, new (string, Func<string>, Color)[]
                {
                    ("Weapons Picked Up", () => RunStats.WeaponsPickedUp.ToString(), Color.white),
                    ("Things Smashed", () => RunStats.ObjectsSmashed.ToString(), Color.white),
                    ("Statues Smashed", () => RunStats.StatuesSmashed.ToString(), Color.white),
                    ("Jelly Bounces", () => RunStats.JellyBounces.ToString(), Blue),
                    ("Drop Luck", () => "+" + Mathf.RoundToInt(DropLuck.Bonus * 100f) + "%", Gold),
                    ("Time Played", () => RunStats.FormatTime(RunStats.PlayTime), Color.white),
                }),
            },
        };

        // Height = the taller column
        float tallest = 0f;
        foreach (var column in columns)
        {
            float h = 0f;
            foreach (var cat in column) h += HeaderHeight + cat.entries.Length * RowHeight + 10f;
            tallest = Mathf.Max(tallest, h);
        }
        float width = Padding * 2f + ColumnWidth * 2f + ColumnGap;
        float height = Padding + 44f + tallest + Padding;

        Image background = OverlayUI.MakePanel("Run Stats", OverlayUI.Root);
        panel = background.rectTransform;
        panel.sizeDelta = new Vector2(width, height);
        group = panel.gameObject.AddComponent<CanvasGroup>();

        PixelText title = PixelText.Create(panel, "RUN STATS", 3, Gold, 0.5f);
        OverlayUI.Place(title.Rect, new Vector2(0.5f, 1f), new Vector2(0f, -Padding));

        int order = 0;
        for (int c = 0; c < columns.Length; c++)
        {
            float x = Padding + c * (ColumnWidth + ColumnGap);
            float y = -Padding - 44f;
            foreach (var cat in columns[c])
            {
                // Header: coloured title + a line under it
                var header = new Row { header = true, color = cat.color, order = order++ };
                header.rect = OverlayUI.MakeRect(cat.title, panel);
                OverlayUI.Place(header.rect, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(ColumnWidth, HeaderHeight));
                header.home = header.rect.anchoredPosition;
                header.label = PixelText.Create(header.rect, cat.title, TextScale, cat.color, 0f);
                header.label.Rect.anchorMin = header.label.Rect.anchorMax = new Vector2(0f, 0.5f);
                header.label.Rect.anchoredPosition = new Vector2(0f, 2f);
                header.underline = OverlayUI.MakeImage("Line", header.rect, cat.color, OverlayUI.WhiteSprite);
                OverlayUI.Place(header.underline.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 6f), new Vector2(ColumnWidth, 3f));
                rows.Add(header);
                y -= HeaderHeight;

                foreach (var entry in cat.entries)
                {
                    var row = new Row { get = entry.get, color = entry.color, order = order++ };
                    row.rect = OverlayUI.MakeRect(entry.label, panel);
                    OverlayUI.Place(row.rect, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(ColumnWidth, RowHeight));
                    row.home = row.rect.anchoredPosition;
                    row.label = PixelText.Create(row.rect, entry.label, TextScale, LabelColor, 0f);
                    row.label.Rect.anchorMin = row.label.Rect.anchorMax = new Vector2(0f, 0.5f);
                    row.label.Rect.anchoredPosition = new Vector2(8f, 0f);
                    row.value = PixelText.Create(row.rect, "", TextScale, entry.color, 1f);
                    row.value.Rect.anchorMin = row.value.Rect.anchorMax = new Vector2(1f, 0.5f);
                    row.value.Rect.anchoredPosition = new Vector2(-4f, 0f);
                    rows.Add(row);
                    y -= RowHeight;
                }
                y -= 10f;
            }
        }

        panel.gameObject.SetActive(false);
    }

    private void Animate()
    {
        float t = Time.unscaledTime - openedAt;

        // Panel: pop with overshoot + fade
        float p = Mathf.Clamp01(t / PopTime);
        float overshoot = 1f + Mathf.Sin(p * Mathf.PI) * 0.06f;
        panel.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, 1f - (1f - p) * (1f - p)) * overshoot;
        group.alpha = Mathf.Clamp01(t / (PopTime * 0.6f));

        foreach (Row row in rows)
        {
            float rt = t - PopTime * 0.5f - row.order * RowStagger;
            float k = Mathf.Clamp01(rt / RowSlideTime);
            float ease = 1f - (1f - k) * (1f - k);
            row.rect.anchoredPosition = row.home + new Vector2(Mathf.Round((1f - ease) * 40f), 0f);
            Color lc = row.header ? row.color : LabelColor;
            // a white flash as each row lands
            float flash = rt > 0f && rt < RowSlideTime + 0.12f ? 1f - Mathf.Clamp01((rt - RowSlideTime * 0.5f) / 0.15f) : 0f;
            row.label.Color = new Color(Mathf.Lerp(lc.r, 1f, flash), Mathf.Lerp(lc.g, 1f, flash), Mathf.Lerp(lc.b, 1f, flash), ease);
            if (row.underline != null)
            {
                row.underline.rectTransform.sizeDelta = new Vector2(Mathf.Round(ColumnWidth * ease), 3f);
                row.underline.color = new Color(row.color.r, row.color.g, row.color.b, 0.8f * ease);
            }
            if (row.value != null)
            {
                row.value.Color = new Color(row.color.r, row.color.g, row.color.b, ease);
                // Count up
                string target = row.get() ?? "";
                float c = Mathf.Clamp01((rt - 0.05f) / CountTime);
                string show = CountUp(target, 1f - (1f - c) * (1f - c) * (1f - c));
                if (show.Length > MaxValueChars) show = show.Substring(0, MaxValueChars - 1) + ".";
                if (show != row.shown) { row.value.SetText(show); row.shown = show; }
                float pop = c > 0f && c < 1f ? 1f : Mathf.Clamp01(1f - (rt - 0.05f - CountTime) / 0.12f);
                row.value.Rect.localScale = Vector3.one * (1f + 0.15f * Mathf.Max(0f, pop) * (c >= 1f ? 1f : 0f));
            }

            // a tick every few rows as they arrive
            if (rt >= 0f && row.order >= ticked)
            {
                if (row.header || row.order % 3 == 0) UISound.Play(row.header ? UISound.Cue.Change : UISound.Cue.Move);
                ticked = row.order + 1;
            }
        }
    }

    // "1234" at 40% = "494"; "1234 (Sword)" counts the number and keeps the rest; text types itself in
    private static string CountUp(string target, float k)
    {
        if (k >= 1f || string.IsNullOrEmpty(target)) return target;
        int digits = 0;
        bool plus = target.StartsWith("+");
        int start = plus ? 1 : 0;
        while (start + digits < target.Length && char.IsDigit(target[start + digits])) digits++;
        if (digits > 0 && !target.Contains(":"))
        {
            long value = long.Parse(target.Substring(start, digits));
            return (plus ? "+" : "") + Mathf.RoundToInt(value * k) + target.Substring(start + digits);
        }
        return target.Substring(0, Mathf.Clamp(Mathf.CeilToInt(target.Length * k), 0, target.Length));
    }

    private void UpdateValues(bool instant)
    {
        if (!instant) return; // Animate() reads the values live every frame
        foreach (Row row in rows) if (row.value != null) { row.value.SetText(""); row.shown = ""; }
    }

    private static string Number(float value) => Mathf.RoundToInt(value).ToString();
}
