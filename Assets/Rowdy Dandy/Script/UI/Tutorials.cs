using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// First-time popups: the first weapon, cat, legendary rat, checkpoint, level up and nightfall each get one small
// card that pauses the game. Few words, a bit of Rowdy. Dismiss with the face buttons / Space / Enter / E / Esc.
// Seen ones are saved (PlayerPrefs RD_Tut_*); Accessibility > Tutorial Popups turns them off. Texts are in Texts below
// (pixel font: A-Z 0-9 . , : ! ? % / - + < > ( ) only - no apostrophes or =).
public class Tutorials : MonoBehaviour
{
    public enum Topic { Weapon, Cat, Rat, Checkpoint, LevelUp, Night, Controls, Hurt, Notes, Water, Platform, WeaponBroke, Boons, Werewolf }

    // title, line 1, line 2, line 3 ({I} = the interact button of whatever was used last: E, Y, triangle, X)
    // Highlights: |WORD| = a mechanic (aqua), ^WORD^ = a warning / number (gold). Every important word gets one.
    private static (string title, string line1, string line2, string line3) Texts(Topic topic)
    {
        switch (topic)
        {
            case Topic.Weapon: return ("SHINY!", "EVERY SWING WEARS YOUR WEAPON DOWN.", "WATCH OUT FOR THE |DURABILITY BAR| (^DUR^).",
                                       "AT ZERO IT ^BREAKS^. THE |ROD| NEVER BREAKS. ENEMIES DROP MORE WEAPONS.");
            case Topic.Cat: return ("A CAT!", "|CATS| FIGHT FOR YOU. NO TRAINING NEEDED.", "YOUR FIRST CAT IS THE |LEADER|: ITS POWER COMES BACK ^FASTER^.",
                                    "THE |CAT PARTY| (PAUSE MENU) SETS THE ORDER. DIE AND ALL BUT THE |LEADER| ^WANDER OFF^.");
            case Topic.Rat: return ("LEGENDARY RAT!", "DROP IT AT A |CHECKPOINT| AS |BAIT|:", "A ^LOST CAT^ COMES RUNNING BACK.", "");
            case Topic.Checkpoint: return ("CHECKPOINT", "PRESS {I} HERE TO |REST|.", "^FULL HEALTH^, FRESH ENEMIES, MORNING SUN.", "");
            case Topic.LevelUp: return ("LEVEL UP!", "MORE ^DAMAGE^, ^CRIT CHANCE^, ^CRIT DAMAGE^", "AND ^+1^ |CAT SLOT|.", "");
            case Topic.Night: return ("NIGHTFALL", "MORE MONSTERS. GLOWING ONES ARE |ELITES|.", "|REST| AT A CHECKPOINT TO SKIP TO MORNING.", "");
            case Topic.Controls: return ("WELCOME, DANDY!", "{MOVE} MOVE, {JUMP} JUMP (HOLD IT), {ATTACK} ATTACK.", "{SURF} WHILE MOVING: |SURF DASH| ON YOUR BOARD.", "");
            case Topic.Hurt: return ("OUCH!", "HIT ENEMIES RIGHT AS THEY ATTACK", "FOR A |COUNTER|: ^HUGE DAMAGE^.", "");
            case Topic.Notes: return ("ROWDY NOTES", "EVERY ENEMY, CAT AND WEAPON YOU MEET GETS A PAGE.", "", "");
            case Topic.Water: return ("SURFS UP!", "ROWDY RIDES THE WATER. {JUMP} TO HOP OUT.", "{ATTACK} IN THE WATER LAUNCHES A |JUMP ATTACK|.", "");
            case Topic.Platform: return ("THIN PLATFORM", "JUMP UP THROUGH IT FROM BELOW.", "HOLD {DOWN} + {JUMP} TO |DROP THROUGH|.", "");
            case Topic.WeaponBroke: return ("IT BROKE!", "BROKEN WEAPONS ARE GONE. THE |ROD| NEVER BREAKS.", "", "");
            case Topic.Boons: return ("BOONS!", "A PATRON OFFERS YOU A |BOON|. BUILD YOUR STYLE.", "ONE PER |SLOT|. |PASSIVES| ^STACK^.", "");
            case Topic.Werewolf: return ("CALL OF THE MOON", "WHEN THE |MOON METER| IS FULL, PRESS {WOLF} TO GO |WEREWOLF|.", "FIGHTING FILLS IT. THE NIGHT FILLS IT ^TWICE^ AS FAST.", "");
        }
        return ("", "", "", "");
    }

    // Topics that never show any more (merged into another card / always-on-screen HUD stuff)
    private static bool Retired(Topic t) => t == Topic.WeaponBroke || t == Topic.Notes || t == Topic.Night; // nightfall lives in Rowdy Notes > Mechanics

    // No tips until Rowdy has landed and the player has tried to walk (per play session)
    private static bool ready;
    private static void CheckReady()
    {
        if (ready || FirstDrop.Running) return;
        Health h = FindFirstObjectByType<Health>();
        PlayerMovement pm = h != null ? h.GetComponent<PlayerMovement>() : null;
        if (pm != null && pm.IsGrounded && GameInput.MoveX != 0f) ready = true;
    }
    private const string Prefix = "RD_Tut_";

    private struct Pending { public Topic topic; public Sprite icon; public float showAt; }

    private static Tutorials instance;
    private static readonly List<Pending> queue = new List<Pending>();
    private static readonly HashSet<Topic> done = new HashSet<Topic>(); // seen, cached so per-frame Show() calls stay cheap
    private static int closedFrame = -10;

    public static bool IsOpen { get; private set; }
    public static bool BlocksPause => IsOpen || Time.frameCount <= closedFrame + 1;

    private RectTransform root, card;
    private Image iconImage;
    private PixelText title, hint;
    private RichLine line1, line2, line3;
    private Image arrow;
    private Topic showing;
    private float openedAt;
    private bool pendingClose;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { queue.Clear(); done.Clear(); IsOpen = false; ready = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("Tutorials (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Tutorials>();
        SceneManager.sceneLoaded += (s, m) => { if (instance != null && IsOpen) instance.Close(); };
    }

    public static bool Seen(Topic topic) => PlayerPrefs.GetInt(Prefix + topic, 0) == 1;

    // Seen already (cheap: safe to call every frame)
    public static bool Done(Topic topic)
    {
        if (done.Contains(topic)) return true;
        if (!Seen(topic)) return false;
        done.Add(topic);
        return true;
    }

    // Queue a popup (only the first time ever). delay = real seconds, so the pickup's own feedback plays first.
    public static void Show(Topic topic, Sprite icon = null, float delay = 0.5f)
    {
        if (!GameSettings.TutorialPopups || Done(topic) || Retired(topic)) return;
        foreach (Pending p in queue) if (p.topic == topic) return;
        queue.Add(new Pending { topic = topic, icon = icon, showAt = Time.unscaledTime + delay });
    }

    public static void ResetAll()
    {
        foreach (Topic t in System.Enum.GetValues(typeof(Topic))) PlayerPrefs.DeleteKey(Prefix + t);
        PlayerPrefs.DeleteKey(AskedKey);
        done.Clear();
        PlayerPrefs.Save();
    }

    // ---------------------------------------------------------------- the first question of a new game
    // "DO YOU WISH TO ACTIVATE TUTORIAL TIPS?" - asked once (RD_TutAsked), the answer is the Tutorial Popups setting
    // (Pause > Preferences can change it later).
    private const string AskedKey = "RD_TutAsked";
    private bool asking;
    private int choice; // 0 yes, 1 no
    private int heldDir;

    private void OpenAsk()
    {
        if (root == null) BuildUI();
        asking = true;
        choice = 0;
        title.SetText("HEY THERE, DANDY!");
        line1.SetText("DO YOU WISH TO ACTIVATE TUTORIAL TIPS?");
        line2.SetText("(HIGHLY RECOMMENDED)");
        line3.SetText("");
        iconImage.enabled = false;
        card.sizeDelta = new Vector2(Mathf.Max(760f, line1.Rect.sizeDelta.x + 120f), 330f);
        title.Rect.anchoredPosition = new Vector2(0f, 105f);
        line1.Rect.anchoredPosition = new Vector2(0f, 35f);
        line2.Rect.anchoredPosition = new Vector2(0f, -5f);
        hint.Rect.anchoredPosition = new Vector2(0f, -100f);
        RefreshAsk();
        IsOpen = true;
        openedAt = Time.unscaledTime;
        PauseMenu.SetExternalPause(true);
        root.gameObject.SetActive(true);
        UISound.Play(UISound.Cue.Unlock);
    }

    private void RefreshAsk()
    {
        hint.SetText((choice == 0 ? "> YES <" : "  YES  ") + "        " + (choice == 1 ? "> NO <" : "  NO  "));
    }

    private void HandleAsk()
    {
        if (pendingClose || Time.unscaledTime - openedAt < 0.4f) return;
        int dir = GameInput.MoveX > 0f || GameInput.MoveY < 0f ? 1 : GameInput.MoveX < 0f || GameInput.MoveY > 0f ? -1 : 0;
        if (dir != 0 && dir != heldDir) { choice = Mathf.Clamp(choice + dir, 0, 1); UISound.Play(UISound.Cue.Move); RefreshAsk(); }
        heldDir = dir;
        bool yes = GameInput.Down(GameInput.Act.Submit) || GameInput.Down(GameInput.Act.Jump) || GameInput.Down(GameInput.Act.Interact) || GameInput.MouseLeftDown;
        bool back = GameInput.PadDown(GameInput.Act.Back) || GameInput.Down(GameInput.Act.Pause);
        if (!yes && !back) return;
        bool on = yes ? choice == 0 : false;
        GameSettings.SetTutorialPopups(on);
        PlayerPrefs.SetInt(AskedKey, 1);
        PlayerPrefs.Save();
        UISound.Play(on ? UISound.Cue.Confirm : UISound.Cue.Back);
        asking = false;
        pendingClose = true;
    }

    private void Update()
    {
        if (IsOpen && asking) { HandleAsk(); Animate(); return; }
        if (IsOpen) { HandleInput(); Animate(); return; }
        CheckReady();
        if (!ready) return;
        if (PlayerPrefs.GetInt(AskedKey, 0) == 0)
        {
            if (!FirstDrop.Running && !PauseMenu.IsPaused && GameObject.FindGameObjectWithTag("Player") != null && Time.timeSinceLevelLoad > 0.6f) OpenAsk();
            return;
        }
        if (!FirstDrop.Running && !Done(Topic.Controls) && GameObject.FindGameObjectWithTag("Player") != null) Show(Topic.Controls, null, 1f);
        if (queue.Count == 0 || !GameSettings.TutorialPopups) { if (!GameSettings.TutorialPopups) queue.Clear(); return; }
        if (PauseMenu.IsPaused || RowdyNotes.IsOpen || WorldMap.IsOpen || CheckpointRest.Resting || FirstDrop.Running) return;
        Health rowdy = FindFirstObjectByType<Health>();
        if (rowdy != null && rowdy.IsDead) return;
        if (Time.unscaledTime < queue[0].showAt) return;

        Pending next = queue[0];
        queue.RemoveAt(0);
        if (Seen(next.topic)) return;
        Open(next);
    }

    private void LateUpdate()
    {
        if (pendingClose) { pendingClose = false; Close(); }
    }

    private void Open(Pending p)
    {
        if (root == null) BuildUI();
        PlayerPrefs.SetInt(Prefix + p.topic, 1);
        done.Add(p.topic);
        PlayerPrefs.Save();

        var text = Texts(p.topic);
        string button = Interact.ButtonName;
        title.SetText(text.title);
        line1.SetText(GameInput.Format(text.line1.Replace("{I}", button)));
        line2.SetText(GameInput.Format(text.line2.Replace("{I}", button)));
        line3.SetText(GameInput.Format(text.line3.Replace("{I}", button)));
        showing = p.topic;
        hint.SetText(GameInput.Icon(GameInput.Act.Jump) + " GOT IT");
        iconImage.sprite = p.icon;
        iconImage.enabled = p.icon != null;

        // Card fits the longest line
        bool three = !string.IsNullOrEmpty(text.line3);
        float w = Mathf.Max(title.Rect.sizeDelta.x, line1.Rect.sizeDelta.x, line2.Rect.sizeDelta.x, line3.Rect.sizeDelta.x) + 120f;
        float h = (p.icon != null ? 380f : 290f) + (three ? 40f : 0f);
        card.sizeDelta = new Vector2(Mathf.Max(620f, w), h);
        float top = h / 2f;
        float y = top - 60f;
        if (p.icon != null) { iconImage.rectTransform.anchoredPosition = new Vector2(0f, y - 40f); y -= 100f; }
        title.Rect.anchoredPosition = new Vector2(0f, y - 20f);
        line1.Rect.anchoredPosition = new Vector2(0f, y - 90f);
        line2.Rect.anchoredPosition = new Vector2(0f, y - 130f);
        line3.Rect.anchoredPosition = new Vector2(0f, y - 170f);
        hint.Rect.anchoredPosition = new Vector2(0f, -top + 36f);

        IsOpen = true;
        openedAt = Time.unscaledTime;
        PauseMenu.SetExternalPause(true);
        root.gameObject.SetActive(true);
        UISound.Play(UISound.Cue.Unlock);
    }

    private void Close()
    {
        IsOpen = false;
        closedFrame = Time.frameCount;
        if (arrow != null) arrow.enabled = false;
        if (root != null) root.gameObject.SetActive(false);
        PauseMenu.SetExternalPause(false);
    }

    private void HandleInput()
    {
        if (pendingClose || Time.unscaledTime - openedAt < 0.45f) return; // no accidental skips mid-combat
        bool dismiss = GameInput.Down(GameInput.Act.Submit) || GameInput.PadDown(GameInput.Act.Back) || GameInput.Down(GameInput.Act.Interact)
                    || GameInput.Down(GameInput.Act.Pause) || GameInput.MouseLeftDown;
        if (!dismiss) return;
        UISound.Play(UISound.Cue.Confirm);
        pendingClose = true;
    }

    // Pops in with an overshoot, the icon bobs, the hint blinks once it can be dismissed
    private void Animate()
    {
        float t = Time.unscaledTime - openedAt;
        float k = Mathf.Clamp01(t / 0.22f);
        float s = k < 1f ? Mathf.Lerp(0.6f, 1f, 1f - Mathf.Pow(1f - k, 3f)) + Mathf.Sin(k * Mathf.PI) * 0.08f : 1f;
        card.localScale = Vector3.one * s;
        if (iconImage.enabled) iconImage.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(t * 5f) * 0.05f);
        hint.Color = new Color(1f, 0.85f, 0.95f, t < 0.45f ? 0.25f : 0.55f + 0.45f * Mathf.Sin(t * 6f));
        PointAtDurability(t);
    }

    // The weapon card points a bouncing arrow at the HUD durability bar
    private static readonly string[] ArrowRows = { "...WW...", "...WW...", "...WW...", "...WW...", "WWWWWWWW", ".WWWWWW.", "..WWWW..", "...WW..." };

    private void PointAtDurability(float t)
    {
        RectTransform bar = !asking && showing == Topic.Weapon && WeaponManager.Instance != null ? WeaponManager.Instance.DurabilityBar : null;
        if (bar == null || !bar.gameObject.activeInHierarchy) { if (arrow != null) arrow.enabled = false; return; }
        if (arrow == null)
        {
            arrow = OverlayUI.MakeImage("Dur Arrow", root, new Color(1f, 0.85f, 0.3f),
                OverlayUI.PixelSprite(ArrowRows, c => new Color32(255, 255, 255, 255), "TutorialArrow"));
            arrow.rectTransform.sizeDelta = new Vector2(48f, 48f);
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.rectTransform.pivot = new Vector2(0.5f, 0f);              // the tip
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f); // points left, at the end of the bar
        }
        Canvas c = bar.GetComponentInParent<Canvas>();
        Camera cam = c != null && c.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? c.rootCanvas.worldCamera : null;
        Vector3[] corners = new Vector3[4];
        bar.GetWorldCorners(corners);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, (corners[2] + corners[3]) / 2f); // right end of the bar
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local)) { arrow.enabled = false; return; }
        arrow.enabled = true;
        arrow.rectTransform.anchoredPosition = local + new Vector2(10f + Mathf.Round(Mathf.Abs(Mathf.Sin(t * 5f)) * 16f), 0f);
    }

    private void BuildUI()
    {
        root = OverlayUI.MakeRect("Tutorial", OverlayUI.Root);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        Image dim = OverlayUI.MakeImage("Dim", root, new Color(0.05f, 0f, 0.08f, 0.55f), OverlayUI.WhiteSprite);
        dim.rectTransform.anchorMin = Vector2.zero;
        dim.rectTransform.anchorMax = Vector2.one;
        dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;

        card = OverlayUI.MakePanel("Card", root).rectTransform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = new Vector2(0f, 40f);

        iconImage = OverlayUI.MakeImage("Icon", card, Color.white);
        iconImage.preserveAspect = true;
        iconImage.rectTransform.anchorMin = iconImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        iconImage.rectTransform.sizeDelta = new Vector2(96f, 96f);

        title = Text(6, new Color(1f, 0.82f, 0.3f));
        line1 = new RichLine(card, 3);
        line2 = new RichLine(card, 3);
        line3 = new RichLine(card, 3);
        hint = Text(2, new Color(1f, 0.85f, 0.95f, 0.8f));
        root.gameObject.SetActive(false);
    }

    private PixelText Text(int scale, Color color)
    {
        PixelText p = PixelText.Create(card, "", scale, color, 0.5f);
        p.Rect.anchorMin = p.Rect.anchorMax = new Vector2(0.5f, 0.5f);
        return p;
    }
}

// One line of tutorial text with highlighted words: |MECHANIC| in aqua, ^WARNING^ in gold, the rest white.
// Each run is its own PixelText, laid out left to right inside one centered rect.
public class RichLine
{
    public static readonly Color Plain = Color.white;
    public static readonly Color Mechanic = new Color(0.45f, 1f, 0.85f);
    public static readonly Color Warning = new Color(1f, 0.8f, 0.25f);

    private readonly RectTransform rect;
    private readonly int scale;
    private readonly System.Collections.Generic.List<PixelText> runs = new System.Collections.Generic.List<PixelText>();

    public RectTransform Rect => rect;

    public RichLine(Transform parent, int scale)
    {
        this.scale = scale;
        rect = OverlayUI.MakeRect("Rich Line", parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
    }

    public void SetText(string text)
    {
        foreach (PixelText r in runs) if (r != null) Object.Destroy(r.gameObject);
        runs.Clear();
        text = text ?? "";
        var parts = new System.Collections.Generic.List<(string s, Color c)>();
        var sb = new System.Text.StringBuilder();
        char mode = ' ';
        foreach (char ch in text)
        {
            if (ch == '|' || ch == '^')
            {
                if (sb.Length > 0) parts.Add((sb.ToString(), mode == '|' ? Mechanic : mode == '^' ? Warning : Plain));
                sb.Clear();
                mode = mode == ch ? ' ' : ch;
                continue;
            }
            sb.Append(ch);
        }
        if (sb.Length > 0) parts.Add((sb.ToString(), mode == '|' ? Mechanic : mode == '^' ? Warning : Plain));

        float x = 0f, h = 0f;
        foreach (var (s, c) in parts)
        {
            PixelText p = PixelText.Create(rect, s, scale, c, 0f);
            p.Rect.anchorMin = p.Rect.anchorMax = new Vector2(0f, 0.5f);
            p.Rect.anchoredPosition = new Vector2(x, 0f);
            x += p.Rect.sizeDelta.x;
            h = Mathf.Max(h, p.Rect.sizeDelta.y);
            runs.Add(p);
        }
        rect.sizeDelta = new Vector2(x, h);
    }
}