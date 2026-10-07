using UnityEngine;
using UnityEngine.UI;

// Button prompts in the bottom-left corner, drawn with the HUD's pink tags:
//   [L2] NOTES   [SELECT] STATS      (keyboard: [TAB] NOTES   [C] STATS)
// NOTES gets a blinking NEW tag when there's an unread Rowdy Notes page. Hidden while a menu is open.
// Created automatically on the shared overlay canvas.
public class HudPrompts : MonoBehaviour
{
    private const float Margin = 24f;
    private const float Gap = 10f;
    private const int TextScale = 3;

    private static readonly Color TextColor = new Color(0.95f, 0.92f, 0.98f, 1f);
    private static readonly Color ActiveColor = new Color32(0xFF, 0x9B, 0xE6, 0xFF);

    private static HudPrompts instance;

    private RectTransform row;
    private CanvasGroup group;
    private Image notesKey, statsKey, newTag;
    private PixelText notesText, statsText;
    private bool showingGamepad;
    private float newsTimer;
    private bool news;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("HudPrompts (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<HudPrompts>();
    }

    private void Start()
    {
        row = OverlayUI.MakeRect("Button Prompts", OverlayUI.Root);
        OverlayUI.Place(row, Vector2.zero, new Vector2(Margin, Margin), new Vector2(600f, HudTag.Height * HudTag.ArtScale));
        group = row.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        notesKey = OverlayUI.MakeImage("Notes Key", row, Color.white);
        notesText = PixelText.Create(row, "NOTES", TextScale, TextColor, 0f);
        newTag = OverlayUI.MakeImage("New", row, Color.white, HudTag.Make("NEW"));
        statsKey = OverlayUI.MakeImage("Stats Key", row, Color.white);
        statsText = PixelText.Create(row, "STATS", TextScale, TextColor, 0f);
        SetKeys(LastInputDevice.UsingGamepad);
    }

    private void SetKeys(bool gamepad)
    {
        showingGamepad = gamepad;
        notesKey.sprite = HudTag.Make(gamepad ? "L2" : "TAB");
        statsKey.sprite = HudTag.Make(gamepad ? "SELECT" : "C");
        Layout();
    }

    // Left to right along the bottom edge: key, word, (NEW), gap, key, word
    private void Layout()
    {
        float x = 0f;
        x = Put(notesKey.rectTransform, HudTag.UISize(notesKey.sprite), x) + 8f;
        x = Put(notesText.Rect, notesText.Rect.sizeDelta, x) + 8f;
        x = Put(newTag.rectTransform, HudTag.UISize(newTag.sprite) * 0.75f, x) + Gap * 3f;
        x = Put(statsKey.rectTransform, HudTag.UISize(statsKey.sprite), x) + 8f;
        Put(statsText.Rect, statsText.Rect.sizeDelta, x);
    }

    private static float Put(RectTransform rect, Vector2 size, float x)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(Mathf.Round(x), 0f);
        return x + size.x;
    }

    private void Update()
    {
        if (row == null) return;

        bool hidden = PauseMenu.IsPaused || RowdyNotes.IsOpen || GameObject.FindGameObjectWithTag("Player") == null;
        group.alpha = Mathf.MoveTowards(group.alpha, hidden ? 0f : 0.9f, Time.unscaledDeltaTime * 6f);
        if (hidden) return;

        bool gamepad = LastInputDevice.UsingGamepad;
        if (gamepad != showingGamepad) SetKeys(gamepad);

        newsTimer -= Time.unscaledDeltaTime;
        if (newsTimer <= 0f)
        {
            newsTimer = 0.5f;
            news = RowdyNotes.AnyNews;
        }
        newTag.enabled = news;
        if (news) newTag.color = new Color(1f, 1f, 1f, Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.55f ? 1f : 0.35f);

        bool statsOpen = PlayerStats.Instance != null && PlayerStats.Instance.IsStatsOpen;
        statsText.Color = statsOpen ? ActiveColor : TextColor;
    }
}
