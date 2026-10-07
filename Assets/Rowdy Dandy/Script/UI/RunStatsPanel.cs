using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// "Run stats" panel shown next to the level stats panel while the stats screen is open (C / gamepad Select).
// Reads RunStats. Also keeps the play time clock running. Created automatically.
public class RunStatsPanel : MonoBehaviour
{
    private const int TextScale = 2;
    private const float RowHeight = 26f;
    private const float Width = 500f;
    private const float Padding = 22f;
    private const int MaxValueChars = 19;

    private static readonly Color Gold = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
    private static readonly Color Pink = new Color32(0xFF, 0x9B, 0xE6, 0xFF);
    private static readonly Color LabelColor = new Color(0.82f, 0.76f, 0.88f, 1f);

    private static RunStatsPanel instance;

    private RectTransform panel;
    private readonly List<(PixelText value, Func<string> get)> rows = new List<(PixelText, Func<string>)>();
    private float refreshTimer;

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
        instance.UpdateValues();
    }

    private void Update()
    {
        if (!PauseMenu.IsPaused) RunStats.PlayTime += Time.unscaledDeltaTime;
    }

    private void Show(bool visible, RectTransform besides)
    {
        if (panel == null) Build();
        panel.gameObject.SetActive(visible);
        if (!visible) return;

        // To the right of the level stats panel when it's anchored top-left like the HUD organizer places it
        Vector2 position = new Vector2(436f, -248f);
        if (besides != null && besides.anchorMin == new Vector2(0f, 1f) && besides.anchorMax == new Vector2(0f, 1f))
        {
            position = new Vector2(besides.anchoredPosition.x + besides.sizeDelta.x * (1f - besides.pivot.x) + 12f,
                                   besides.anchoredPosition.y + besides.sizeDelta.y * (1f - besides.pivot.y));
        }
        OverlayUI.Place(panel, new Vector2(0f, 1f), position);
        refreshTimer = 0f;
        UpdateValues();
    }

    private void Build()
    {
        var entries = new List<(string label, Func<string> get, Color color)>
        {
            ("Enemies Killed", () => RunStats.Kills.ToString(), Gold),
            ("By Your Cats", () => RunStats.CatKills.ToString(), Pink),
            ("Executions", () => RunStats.Executions.ToString(), Pink),
            ("Best Kill Streak", () => RunStats.BestStreak.ToString(), Gold),
            ("Most Hunted", RunStats.FavoriteVictim, Color.white),
            ("Max Damage", () => RunStats.MaxHit <= 0f ? "-" : Number(RunStats.MaxHit) + (string.IsNullOrEmpty(RunStats.MaxHitWith) ? "" : " (" + RunStats.MaxHitWith + ")"), Gold),
            ("Total Damage", () => Number(RunStats.TotalDamage), Color.white),
            ("Critical Hits", () => RunStats.CriticalHits.ToString(), Color.white),
            ("Counters", () => RunStats.Counters.ToString(), Color.white),
            ("Damage Taken", () => Number(RunStats.DamageTaken), Color.white),
            ("Deaths", () => RunStats.Deaths.ToString(), Color.white),
            ("Cats Rescued", () => RunStats.CatsRescued.ToString(), Pink),
            ("Cats Lost", () => RunStats.CatsLost.ToString(), Pink),
            ("Weapons Broken", () => RunStats.WeaponsBroken.ToString(), Color.white),
            ("Things Smashed", () => RunStats.ObjectsSmashed.ToString(), Color.white),
            ("Level Ups", () => RunStats.LevelUps.ToString(), Gold),
            ("Time Played", () => RunStats.FormatTime(RunStats.PlayTime), Color.white),
        };

        float height = Padding + 40f + entries.Count * RowHeight + Padding;
        Image background = OverlayUI.MakePanel("Run Stats", OverlayUI.Root);
        panel = background.rectTransform;
        panel.sizeDelta = new Vector2(Width, height);

        PixelText title = PixelText.Create(panel, "Run Stats", 3, Gold, 0.5f);
        OverlayUI.Place(title.Rect, new Vector2(0.5f, 1f), new Vector2(0f, -Padding));

        float y = -Padding - 40f - RowHeight / 2f;
        foreach (var entry in entries)
        {
            PixelText label = PixelText.Create(panel, entry.label, TextScale, LabelColor, 0f);
            label.Rect.anchorMin = label.Rect.anchorMax = new Vector2(0f, 1f);
            label.Rect.anchoredPosition = new Vector2(Padding, Mathf.Round(y));

            PixelText value = PixelText.Create(panel, "", TextScale, entry.color, 1f);
            value.Rect.anchorMin = value.Rect.anchorMax = new Vector2(1f, 1f);
            value.Rect.anchoredPosition = new Vector2(-Padding, Mathf.Round(y));

            rows.Add((value, entry.get));
            y -= RowHeight;
        }

        panel.gameObject.SetActive(false);
    }

    private void UpdateValues()
    {
        foreach (var row in rows)
        {
            string text = row.get() ?? "";
            if (text.Length > MaxValueChars) text = text.Substring(0, MaxValueChars - 1) + ".";
            row.value.SetText(text);
        }
    }

    private static string Number(float value) => Mathf.RoundToInt(value).ToString();
}
