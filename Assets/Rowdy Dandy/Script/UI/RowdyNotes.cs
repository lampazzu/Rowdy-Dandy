using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// "Rowdy Notes": Select (gamepad) or Tab (keyboard) opens it. Four tabs - L1 / R1 (Q / E) switch them:
//   ENEMIES        bestiary. A page stays black until Rowdy has met that enemy (on screen, close by); its COUNTER
//                  section unlocks after killing EnemyCatalog.Entry.killsToReveal of them. Shows weapon drops.
//   WEAPONS        what each weapon does per attack; unlocks the first time you pick that weapon up.
//   GAME MECHANICS counters, surfing, durability, cats...
//   INTERACTABLES  plants, jellies, spawners, altars...
// Arrows / d-pad / stick flip pages. Progress is saved in PlayerPrefs (RD_Notes_*), like the player level.
// Texts: EnemyCatalog (enemies) and NotesData (the rest). Created automatically; builds its UI the first time it opens.
public class RowdyNotes : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    // Esc / B that closed the notes this frame must not also open the pause menu
    public static bool BlocksPause => IsOpen || Time.frameCount <= closedFrame + 1;

    private static RowdyNotes instance;
    private static int closedFrame = -10;

    // ---------------------------------------------------------------- tuning
    private const float SeenDistance = 13f;      // how close an enemy has to be (and on screen) to count as met
    private const int TextScale = 2;
    private const int WrapChars = 62;
    private const float LineHeight = 24f;

    private static readonly Color Pink = new Color32(0xFF, 0x9B, 0xE6, 0xFF);
    private static readonly Color HotPink = new Color32(0xFF, 0x39, 0xC0, 0xFF);
    private static readonly Color TitleColor = new Color32(0xF4, 0xEC, 0xFA, 0xFF);
    private static readonly Color TextColor = new Color(0.86f, 0.8f, 0.92f, 1f);
    private static readonly Color DimText = new Color(0.62f, 0.55f, 0.7f, 1f);
    private static readonly Color Counter = new Color32(0x5C, 0xE6, 0xFF, 0xFF); // same cyan as the COUNTER! popup
    private static readonly Color PlateDark = new Color(0.07f, 0.02f, 0.09f, 0.95f);
    private static readonly Color Silhouette = new Color(0.02f, 0f, 0.04f, 1f);

    // (enum values are saved in "read" flags, so new tabs go at the end; TabOrder is the order on screen)
    private enum Tab { Enemies, Weapons, Mechanics, Interactables, Cats }
    private static readonly Tab[] TabOrder = { Tab.Enemies, Tab.Cats, Tab.Weapons, Tab.Mechanics, Tab.Interactables };
    private static readonly string[] TabNames = { "ENEMIES", "CATS", "WEAPONS", "GAME MECHANICS", "INTERACTABLES" };
    private static readonly Color QuipColor = new Color32(0xFF, 0xD8, 0x6A, 0xFF);
    private static readonly Color JellyOn = new Color32(0x5C, 0xE6, 0xFF, 0xFF);
    private static readonly Color JellyOff = new Color(0.3f, 0.2f, 0.38f, 1f);

    // ---------------------------------------------------------------- saved progress
    private static string SeenKey(string id) => "RD_Notes_Seen_" + id;
    private static string KillsKey(string id) => "RD_Notes_Kills_" + id;
    private static string ViewedKey(string id) => "RD_Notes_Viewed_" + id; // enemies: 1 = page read, 2 = counter read
    private static string WeaponKey(int index) => "RD_Notes_Weapon_" + index;
    private static string ReadKey(Tab tab, string id) => "RD_Notes_Read_" + (int)tab + "_" + id;
    private static string CatKey(string id) => "RD_Notes_Cat_" + id;
    public static bool CatFound(string id) => PlayerPrefs.GetInt(CatKey(id), 0) == 1;

    // First time Rowdy picks up a cat of this type (PetFollower)
    public static void MarkCatFound(string id, string name, Sprite face)
    {
        if (CatFound(id)) return;
        PlayerPrefs.SetInt(CatKey(id), 1);
        PlayerPrefs.Save();
        Toast("NEW CAT NOTE", name, face);
    }

    public static bool IsSeen(EnemyCatalog.Entry e) => PlayerPrefs.GetInt(SeenKey(e.id), 0) == 1;
    public static int Kills(EnemyCatalog.Entry e) => PlayerPrefs.GetInt(KillsKey(e.id), 0);
    public static bool CounterRevealed(EnemyCatalog.Entry e) => Kills(e) >= Mathf.Max(1, e.killsToReveal);
    private static int Viewed(EnemyCatalog.Entry e) => PlayerPrefs.GetInt(ViewedKey(e.id), 0);
    private static bool HasNews(EnemyCatalog.Entry e) => IsSeen(e) && Viewed(e) < (CounterRevealed(e) ? 2 : 1);

    public static bool WeaponFound(int index) =>
        index == 0 || PlayerPrefs.GetInt(WeaponKey(index), 0) == 1 || PlayerPrefs.GetInt("WeaponUnlocked_" + index, 0) == 1;

    // Something unread anywhere (the HUD prompt shows NEW)
    public static bool AnyNews
    {
        get
        {
            foreach (EnemyCatalog.Entry e in EnemyCatalog.All) if (e.inNotes && HasNews(e)) return true;
            for (int i = 0; i < NotesData.Weapons.Length; i++)
                if (WeaponFound(i) && PlayerPrefs.GetInt(ReadKey(Tab.Weapons, NotesData.Weapons[i].id), 0) == 0) return true;
            foreach (NotesData.Topic c in NotesData.Cats)
                if (CatFound(c.id) && PlayerPrefs.GetInt(ReadKey(Tab.Cats, c.id), 0) == 0) return true;
            return false;
        }
    }

    public static void RecordKill(EnemyHealth enemy)
    {
        EnemyCatalog.Entry entry = EnemyCatalog.Identify(enemy);
        if (entry == null || !entry.inNotes) return;
        if (entry.fallbackPortrait == null && entry.Portrait == null) entry.fallbackPortrait = KillFeed.HeadCropOf(enemy);
        MarkSeen(entry);

        bool wasRevealed = CounterRevealed(entry);
        PlayerPrefs.SetInt(KillsKey(entry.id), Kills(entry) + 1);
        if (!wasRevealed && CounterRevealed(entry)) Toast("COUNTER REVEALED", entry.name, entry.Portrait);
    }

    public static void MarkSeen(EnemyCatalog.Entry entry)
    {
        if (entry == null || !entry.inNotes || IsSeen(entry)) return;
        PlayerPrefs.SetInt(SeenKey(entry.id), 1);
        PlayerPrefs.Save();
        Toast("NEW ROWDY NOTE", entry.name, entry.Portrait);
    }

    // First time Rowdy gets a pickup that has an Interactables page (flying rat, cat treat...): toast + NEW
    public static void MarkTopicNews(string id)
    {
        string key = "RD_Notes_Found_" + id;
        if (PlayerPrefs.GetInt(key, 0) == 1) return;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.DeleteKey(ReadKey(Tab.Interactables, id));
        PlayerPrefs.Save();
        foreach (NotesData.Topic t in NotesData.Interactables)
            if (t.id == id) { Sprite icon = null; try { icon = t.icon != null ? t.icon() : null; } catch { } Toast("NEW ROWDY NOTE", t.name, icon); break; }
    }

    public static void MarkWeaponFound(int index)
    {
        if (index <= 0 || index >= NotesData.Weapons.Length || PlayerPrefs.GetInt(WeaponKey(index), 0) == 1) return;
        PlayerPrefs.SetInt(WeaponKey(index), 1);
        PlayerPrefs.Save();
        NotesData.Topic w = NotesData.Weapons[index];
        Toast("NEW WEAPON NOTE", w.name, w.icon != null ? w.icon() : null);
    }

    // Dev reset (key 0)
    public static void ResetProgress()
    {
        foreach (EnemyCatalog.Entry e in EnemyCatalog.All)
        {
            PlayerPrefs.DeleteKey(SeenKey(e.id));
            PlayerPrefs.DeleteKey(KillsKey(e.id));
            PlayerPrefs.DeleteKey(ViewedKey(e.id));
        }
        for (int i = 0; i < NotesData.Weapons.Length; i++)
        {
            PlayerPrefs.DeleteKey(WeaponKey(i));
            PlayerPrefs.DeleteKey(ReadKey(Tab.Weapons, NotesData.Weapons[i].id));
        }
        foreach (NotesData.Topic t in NotesData.Mechanics) PlayerPrefs.DeleteKey(ReadKey(Tab.Mechanics, t.id));
        foreach (NotesData.Topic t in NotesData.Interactables) { PlayerPrefs.DeleteKey(ReadKey(Tab.Interactables, t.id)); PlayerPrefs.DeleteKey("RD_Notes_Found_" + t.id); }
        foreach (NotesData.Topic t in NotesData.Cats) { PlayerPrefs.DeleteKey(ReadKey(Tab.Cats, t.id)); PlayerPrefs.DeleteKey(CatKey(t.id)); }
    }

    // ---------------------------------------------------------------- lifetime
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("RowdyNotes (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RowdyNotes>();
    }

    private static void Toast(string headline, string name, Sprite face)
    {
        if (instance == null) Create();
        instance.toasts.Enqueue((headline, name, face));
    }

    // ---------------------------------------------------------------- page model
    private class Page
    {
        public string readKey;          // PlayerPrefs flag set when viewed (non-enemy tabs)
        public string title;
        public string subtitle = "";
        public Sprite icon;
        public string glyph = "?";
        public bool locked, news;
        public readonly List<(string header, string body, Color color)> sections = new List<(string, string, Color)>();
        public float progress = -1f;    // locked counter progress bar
        public string progressLabel;
        public EnemyCatalog.Entry enemy;
        public AnimatedPortraits.Clip anim;   // animated portrait (idle / walk frames), null = still icon
        public int[] ratings;                 // weapons: jellyfish rows
    }

    private List<Page> BuildPages(Tab tab)
    {
        var pages = new List<Page>();
        switch (tab)
        {
            case Tab.Enemies:
                foreach (EnemyCatalog.Entry e in EnemyCatalog.NotesEntries())
                {
                    bool seen = IsSeen(e), revealed = CounterRevealed(e);
                    int kills = Kills(e), need = Mathf.Max(1, e.killsToReveal);
                    var p = new Page { enemy = e, icon = e.Portrait, locked = !seen, news = HasNews(e), title = seen ? e.name : "???", anim = AnimatedPortraits.Get(e.id) };
                    if (!seen)
                    {
                        p.sections.Add(("NOTES", "Not met yet. Keep exploring - the page fills in once you see one up close.", DimText));
                    }
                    else
                    {
                        p.subtitle = "DEFEATED  " + kills;
                        p.sections.Add(("NOTES", e.about, TextColor));
                        p.sections.Add(("WEAKNESS", e.weakness, TextColor));
                        p.sections.Add(("DROPS", e.drops, TextColor));
                        if (revealed) p.sections.Add(("COUNTER", e.counter, Counter));
                        else
                        {
                            p.sections.Add(("COUNTER", "??? ???? ?? ??????? ???? ??? ????? ??", DimText));
                            p.progress = (float)kills / need;
                            p.progressLabel = $"DEFEAT {need - kills} MORE TO REVEAL";
                        }
                        if (!string.IsNullOrEmpty(e.Quip)) p.sections.Add((NotesData.Quip, e.Quip, QuipColor));
                    }
                    pages.Add(p);
                }
                break;

            case Tab.Weapons:
                for (int i = 0; i < NotesData.Weapons.Length; i++)
                {
                    NotesData.Topic w = NotesData.Weapons[i];
                    bool found = WeaponFound(i);
                    var p = TopicPage(Tab.Weapons, w);
                    p.locked = !found;
                    p.ratings = found ? w.ratings : null;
                    if (!found)
                    {
                        p.title = "???";
                        p.news = false;
                        p.sections.Clear();
                        p.sections.Add(("NOTES", "Not found yet. Pick one up to learn how it fights.", DimText));
                    }
                    pages.Add(p);
                }
                break;

            case Tab.Cats:
                foreach (NotesData.Topic c in NotesData.Cats)
                {
                    var p = TopicPage(Tab.Cats, c);
                    bool found = CatFound(c.id);
                    p.locked = !found;
                    p.news = found && PlayerPrefs.GetInt(p.readKey, 0) == 0;
                    if (!found)
                    {
                        p.title = "???";
                        p.sections.Clear();
                        p.sections.Add(("NOTES", "Not found yet. Somewhere out there a cat is glowing, waiting for you.", DimText));
                    }
                    pages.Add(p);
                }
                break;

            case Tab.Mechanics:
                foreach (NotesData.Topic t in NotesData.Mechanics) pages.Add(TopicPage(tab, t));
                break;

            default:
                foreach (NotesData.Topic t in NotesData.Interactables) pages.Add(TopicPage(tab, t));
                break;
        }
        return pages;
    }

    private static Page TopicPage(Tab tab, NotesData.Topic t)
    {
        var p = new Page { title = t.name, glyph = t.glyph, readKey = ReadKey(tab, t.id), anim = AnimatedPortraits.Get(t.animatedPortrait) };
        try { p.icon = t.icon != null ? t.icon() : null; } catch { p.icon = null; }
        p.news = PlayerPrefs.GetInt(p.readKey, 0) == 0 && (tab == Tab.Weapons || PlayerPrefs.GetInt("RD_Notes_Found_" + t.id, 0) == 1);
        foreach (var (header, body) in t.sections) p.sections.Add((header, body, header == NotesData.Quip ? QuipColor : TextColor));
        return p;
    }

    // ---------------------------------------------------------------- state
    private Tab tab;
    private readonly int[] pageOfTab = new int[5];
    private List<Page> pages = new List<Page>();
    private int page;
    private float pageChangedAt = -10f;
    private int pageDirection;
    private float openedAt;
    private bool pendingClose;

    private float navTimer;
    private int heldNav;

    private float scanTimer;
    private Transform rowdy;
    private EnemyHealth[] knownEnemies;
    private float enemyListAge = 99f;

    private readonly Queue<(string headline, string name, Sprite face)> toasts = new Queue<(string, string, Sprite)>();
    private RectTransform toastRect;
    private float toastStartedAt = -10f;
    private const float ToastIn = 0.22f, ToastHold = 2.6f, ToastOut = 0.22f;

    // UI
    private Canvas canvas;
    private GameObject root;
    private RectTransform panel;
    private CanvasGroup panelGroup;
    private RectTransform portraitHolder;
    private Image portraitImage, portraitFlash;
    private PixelText portraitGlyph;
    private PixelText nameText, subtitleText, indexText;
    private RectTransform textColumn;
    private CanvasGroup textGroup;
    private RectTransform stripRoot;
    private readonly List<GameObject> pageContent = new List<GameObject>();
    private readonly List<(Image frame, Image icon, PixelText glyph, Image news)> strip = new List<(Image, Image, PixelText, Image)>();
    private readonly List<(Image plate, PixelText label)> tabs = new List<(Image, PixelText)>();

    // ---------------------------------------------------------------- update
    private void Update()
    {
        if (IsOpen)
        {
            HandleOpenInput();
            Animate();
        }
        else
        {
            if (!PauseMenu.IsPaused && (PadInput.SelectDown || Input.GetKeyDown(KeyCode.Tab))) Open();
            ScanForEnemies();
        }
        UpdateToast();
    }

    private void LateUpdate()
    {
        if (pendingClose) { pendingClose = false; Close(); }
    }

    private void HandleOpenInput()
    {
        if (pendingClose) return;
        bool close = PadInput.SelectDown || Input.GetKeyDown(KeyCode.JoystickButton1) ||
                     Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Backspace) ||
                     Input.GetKeyDown(KeyCode.JoystickButton7);
        if (close && Time.unscaledTime - openedAt > 0.1f)
        {
            UISound.Play(UISound.Cue.Back);
            pendingClose = true;
            return;
        }

        // Tabs: L1 / R1 (Q / E)
        if (Input.GetKeyDown(KeyCode.JoystickButton4) || Input.GetKeyDown(KeyCode.Q)) { SwitchTab(-1); return; }
        if (Input.GetKeyDown(KeyCode.JoystickButton5) || Input.GetKeyDown(KeyCode.E)) { SwitchTab(+1); return; }

        float h = 0f;
        try { h = Input.GetAxisRaw("Horizontal"); } catch (System.ArgumentException) { }

        // Pages (the icons along the bottom): arrows / A-D / stick / d-pad, with key repeat
        int dir = 0;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) || h > 0.5f) dir = 1;
        else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) || h < -0.5f) dir = -1;
        if (Repeat(dir, ref heldNav, ref navTimer)) Flip(dir);
    }

    private static bool Repeat(int dir, ref int held, ref float timer)
    {
        if (dir == 0) { held = 0; return false; }
        if (dir != held) { held = dir; timer = 0.35f; return true; }
        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f) { timer = 0.14f; return true; }
        return false;
    }

    // ---------------------------------------------------------------- open / close
    public static void Open()
    {
        if (instance == null) Create();
        instance.DoOpen();
    }

    private void DoOpen()
    {
        if (IsOpen || PauseMenu.IsPaused) return;
        if (root == null) Build();

        // Start on something new to read, otherwise where we left off
        var enemyPages = BuildPages(Tab.Enemies);
        for (int i = 0; i < enemyPages.Count; i++)
            if (enemyPages[i].news) { tab = Tab.Enemies; pageOfTab[0] = i; break; }

        IsOpen = true;
        openedAt = Time.unscaledTime;
        PauseMenu.SetExternalPause(true);
        root.SetActive(true);
        UISound.Play(UISound.Cue.Open);
        pageDirection = 0;
        ShowTab(false);
    }

    private void Close()
    {
        IsOpen = false;
        closedFrame = Time.frameCount;
        if (root != null) root.SetActive(false);
        PauseMenu.SetExternalPause(false);
        PlayerPrefs.Save();
    }

    private void Flip(int direction)
    {
        if (pages.Count == 0) return;
        page = (page + direction + pages.Count) % pages.Count;
        pageOfTab[(int)tab] = page;
        pageDirection = direction;
        UISound.Play(UISound.Cue.Page);
        ShowPage();
    }

    private void SwitchTab(int direction)
    {
        int at = System.Array.IndexOf(TabOrder, tab);
        tab = TabOrder[(at + direction + TabOrder.Length) % TabOrder.Length];
        pageDirection = 0;
        UISound.Play(UISound.Cue.Change);
        ShowTab(true);
    }

    // ---------------------------------------------------------------- encounter tracking
    private void ScanForEnemies()
    {
        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer > 0f) return;
        scanTimer = 0.5f;

        if (rowdy == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            rowdy = player.transform;
        }

        bool anyUnseen = false;
        foreach (EnemyCatalog.Entry e in EnemyCatalog.All) if (e.inNotes && !IsSeen(e)) { anyUnseen = true; break; }
        if (!anyUnseen) return;

        enemyListAge += 0.5f;
        if (knownEnemies == null || enemyListAge > 2f)
        {
            knownEnemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
            enemyListAge = 0f;
        }

        Vector2 at = rowdy.position;
        foreach (EnemyHealth enemy in knownEnemies)
        {
            if (enemy == null || enemy.enemydead || !enemy.isActiveAndEnabled) continue;
            if (((Vector2)enemy.transform.position - at).sqrMagnitude > SeenDistance * SeenDistance) continue;
            if (!enemy.TryGetComponent(out Renderer r) || !r.isVisible) continue;

            EnemyCatalog.Entry entry = EnemyCatalog.Identify(enemy);
            if (entry == null || !entry.inNotes || IsSeen(entry)) continue;
            if (entry.fallbackPortrait == null && entry.Portrait == null) entry.fallbackPortrait = KillFeed.HeadCropOf(enemy);
            MarkSeen(entry);
        }
    }

    // ---------------------------------------------------------------- page content
    private void ShowTab(bool withAnimation)
    {
        pages = BuildPages(tab);
        page = Mathf.Clamp(pageOfTab[(int)tab], 0, Mathf.Max(0, pages.Count - 1));
        BuildStrip();
        for (int i = 0; i < tabs.Count; i++)
        {
            bool on = TabOrder[i] == tab;
            tabs[i].plate.color = on ? HotPink : new Color(0.25f, 0.1f, 0.3f, 1f);
            tabs[i].label.Color = on ? Color.white : DimText;
        }
        if (withAnimation) pageDirection = 0;
        ShowPage();
    }

    private void ShowPage()
    {
        if (pages.Count == 0) return;
        pageChangedAt = Time.unscaledTime;

        // Mark as read (and refresh this page's NEW state)
        Page current = pages[page];
        if (current.enemy != null)
        {
            EnemyCatalog.Entry e = current.enemy;
            if (IsSeen(e)) PlayerPrefs.SetInt(ViewedKey(e.id), Mathf.Max(Viewed(e), CounterRevealed(e) ? 2 : 1));
            current.news = false;
        }
        else if (!current.locked && current.readKey != null)
        {
            PlayerPrefs.SetInt(current.readKey, 1);
            current.news = false;
        }

        // Portrait: the face / icon, a black silhouette until unlocked, or a big glyph when there's no art
        Sprite face = current.icon;
        portraitImage.sprite = face;
        portraitImage.enabled = face != null;
        portraitImage.color = current.locked ? Silhouette : Color.white;
        portraitGlyph.SetText(current.locked && face == null ? "?" : current.glyph);
        portraitGlyph.gameObject.SetActive(face == null);

        nameText.SetText(current.title);
        nameText.Color = current.locked ? DimText : TitleColor;
        subtitleText.SetText(current.subtitle ?? "");
        indexText.SetText($"{page + 1:00} / {pages.Count:00}");

        // Right column
        foreach (GameObject go in pageContent) Destroy(go);
        pageContent.Clear();
        jellies.Clear();
        float y = 0f;
        if (current.ratings != null) AddRatings(current.ratings, ref y);
        foreach (var (header, body, color) in current.sections) AddSection(header, body, color, ref y);
        if (current.progress >= 0f)
        {
            y += 16f;
            AddLine(current.progressLabel, Pink, 0f, ref y);
            AddProgressBar(current.progress, ref y);
        }

        RefreshStrip();
    }

    private void AddSection(string header, string body, Color color, ref float y)
    {
        AddLine(header, Pink, 0f, ref y, 3);
        y -= 6f;
        foreach (string line in Wrap(body, WrapChars)) AddLine(line, color, 0f, ref y);
        y -= 16f;
    }

    private void AddLine(string text, Color color, float x, ref float y, int scale = TextScale)
    {
        PixelText line = PixelText.Create(textColumn, text, scale, color, 0f);
        line.Rect.anchorMin = line.Rect.anchorMax = new Vector2(0f, 1f);
        line.Rect.pivot = new Vector2(0f, 1f);
        line.Rect.anchoredPosition = new Vector2(x, Mathf.Round(y));
        pageContent.Add(line.gameObject);
        y -= scale == TextScale ? LineHeight : line.Rect.sizeDelta.y + 4f;
    }

    // Weapon stats as jellyfish (1-10), two columns:  SPEED  [jelly x10]   POWER  [jelly x10] ...
    private void AddRatings(int[] ratings, ref float y)
    {
        const float jelly = 18f, jellyGap = 3f, rowH = 30f, colW = 430f, labelW = 168f;
        int rows = (NotesData.RatingNames.Length + 1) / 2;
        for (int i = 0; i < NotesData.RatingNames.Length && i < ratings.Length; i++)
        {
            float x = (i % 2) * colW;
            float rowY = y - (i / 2) * rowH;
            PixelText label = PixelText.Create(textColumn, NotesData.RatingNames[i], 2, Pink, 0f);
            label.Rect.anchorMin = label.Rect.anchorMax = new Vector2(0f, 1f);
            label.Rect.pivot = new Vector2(0f, 0.5f);
            label.Rect.anchoredPosition = new Vector2(x, Mathf.Round(rowY - jelly / 2f));
            pageContent.Add(label.gameObject);
            for (int j = 0; j < 10; j++)
            {
                bool on = j < ratings[i];
                Image img = OverlayUI.MakeImage("Jelly", textColumn, on ? JellyOn : JellyOff, JellySprite);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 1f);
                img.rectTransform.pivot = new Vector2(0.5f, 1f);
                img.rectTransform.sizeDelta = new Vector2(jelly, jelly);
                img.rectTransform.anchoredPosition = new Vector2(x + labelW + j * (jelly + jellyGap) + jelly / 2f, Mathf.Round(rowY));
                pageContent.Add(img.gameObject);
                if (on) jellies.Add((img, j));
            }
        }
        y -= rows * rowH + 14f;
    }

    private readonly List<(Image img, int index)> jellies = new List<(Image, int)>();
    private static Sprite jellySprite;

    // 9x9 pixel jellyfish: dome, eyes, wavy tentacles (white, tinted by the Image)
    private static Sprite JellySprite
    {
        get
        {
            if (jellySprite == null)
                jellySprite = OverlayUI.PixelSprite(new[]
                {
                    "..WWWWW..",
                    ".WWWWWWW.",
                    "WWWWWWWWW",
                    "WW.WWW.WW",
                    "WWWWWWWWW",
                    ".W.W.W.W.",
                    ".W..W..W.",
                    "..W.W.W..",
                    "..W..W...",
                }, c => new Color32(255, 255, 255, 255), "NotesJelly");
            return jellySprite;
        }
    }

    private void AddProgressBar(float fill, ref float y)
    {
        y -= 6f;
        Image back = OverlayUI.MakeImage("Progress", textColumn, PlateDark);
        back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(0f, 1f);
        back.rectTransform.pivot = new Vector2(0f, 1f);
        back.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Round(y));
        back.rectTransform.sizeDelta = new Vector2(360f, 18f);
        Image bar = OverlayUI.MakeImage("Fill", back.rectTransform, HotPink, OverlayUI.WhiteSprite);
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillAmount = Mathf.Clamp01(fill);
        bar.rectTransform.anchorMin = Vector2.zero;
        bar.rectTransform.anchorMax = Vector2.one;
        bar.rectTransform.offsetMin = new Vector2(3f, 3f);
        bar.rectTransform.offsetMax = new Vector2(-3f, -3f);
        pageContent.Add(back.gameObject);
        y -= 30f;
    }

    private static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (string word in (text ?? "").Split(' '))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > width)
            {
                lines.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0) current.Append(' ');
            current.Append(word);
        }
        if (current.Length > 0) lines.Add(current.ToString());
        return lines;
    }

    // ---------------------------------------------------------------- bottom strip (one icon per page)
    private void BuildStrip()
    {
        foreach (Transform child in stripRoot) Destroy(child.gameObject);
        strip.Clear();

        const float icon = 60f;
        float gap = pages.Count > 14 ? 14f : 20f;
        float width = pages.Count * (icon + gap) - gap;
        Image back = OverlayUI.MakeImage("Strip Back", stripRoot, new Color(0f, 0f, 0f, 0.35f));
        Center(back.rectTransform, new Vector2(0f, 3f), new Vector2(width + 40f, icon + 34f));

        for (int i = 0; i < pages.Count; i++)
        {
            float x = -width / 2f + icon / 2f + i * (icon + gap);
            Image f = OverlayUI.MakeImage("Entry " + i, stripRoot, Color.white);
            Center(f.rectTransform, new Vector2(x, 0f), new Vector2(icon, icon));
            Image inner = OverlayUI.MakeImage("Back", f.rectTransform, PlateDark);
            Stretch(inner.rectTransform, 3f);
            Image ic = OverlayUI.MakeImage("Face", f.rectTransform, Color.white);
            ic.preserveAspect = true;
            Stretch(ic.rectTransform, 6f);
            PixelText g = PixelText.Create(f.rectTransform, "?", 3, new Color(0.7f, 0.45f, 0.75f, 1f), 0.5f);
            Center(g.Rect, Vector2.zero, null);
            Image news = OverlayUI.MakeImage("New", f.rectTransform, HotPink);
            news.rectTransform.anchorMin = news.rectTransform.anchorMax = new Vector2(1f, 1f);
            news.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            news.rectTransform.anchoredPosition = new Vector2(-4f, -4f);
            news.rectTransform.sizeDelta = new Vector2(12f, 12f);
            strip.Add((f, ic, g, news));
        }
    }

    private void RefreshStrip()
    {
        for (int i = 0; i < strip.Count && i < pages.Count; i++)
        {
            Page p = pages[i];
            var (frame, icon, glyph, news) = strip[i];
            icon.sprite = p.icon;
            icon.enabled = p.icon != null;
            icon.color = p.locked ? Silhouette : Color.white;
            glyph.SetText(p.locked && p.icon == null ? "?" : p.glyph);
            glyph.gameObject.SetActive(p.icon == null);
            frame.color = i == page ? HotPink : new Color(0.35f, 0.18f, 0.4f, 1f);
            news.enabled = p.news;
        }
    }

    // ---------------------------------------------------------------- animation
    private void Animate()
    {
        float now = Time.unscaledTime;

        float open = Ease(Mathf.Clamp01((now - openedAt) / 0.18f));
        panelGroup.alpha = open;
        panel.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, open);

        // Page flip: portrait slides in from the side you flipped towards, flashes, text fades up
        float t = Ease(Mathf.Clamp01((now - pageChangedAt) / 0.2f));
        portraitHolder.anchoredPosition = new Vector2(Mathf.Round(PortraitX + pageDirection * 70f * (1f - t)), PortraitY);
        portraitFlash.color = new Color(1f, 1f, 1f, 0.55f * (1f - Mathf.Clamp01((now - pageChangedAt) / 0.18f)));
        textGroup.alpha = t;
        textColumn.anchoredPosition = new Vector2(TextX, Mathf.Round(TextY - 12f * (1f - t)));

        // Animated portrait (idle / walk frames), and the jellyfish ratings bob in a wave
        if (pages.Count > 0 && page < pages.Count && pages[page].anim != null)
        {
            Sprite frame = pages[page].anim.FrameAt(now - pageChangedAt);
            portraitImage.sprite = frame;
            portraitImage.enabled = true;
            if (page < strip.Count) strip[page].icon.sprite = frame;
        }
        foreach (var (img, index) in jellies)
        {
            if (img == null) continue;
            float wave = Mathf.Sin(now * 5f - index * 0.6f);
            img.rectTransform.localScale = new Vector3(1f, 1f + 0.15f * Mathf.Max(0f, wave), 1f);
        }

        // Selected icon in the strip bobs
        for (int i = 0; i < strip.Count; i++)
        {
            float bob = i == page ? Mathf.Round(Mathf.Sin(now * 6f) * 3f) + 6f : 0f;
            RectTransform r = strip[i].frame.rectTransform;
            r.anchoredPosition = new Vector2(r.anchoredPosition.x, bob);
            r.localScale = Vector3.one * (i == page ? 1.12f : 1f);
            if (strip[i].news.enabled) strip[i].news.color = new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(now * 8f));
        }
    }

    private static float Ease(float t) => 1f - (1f - t) * (1f - t);

    // ---------------------------------------------------------------- build
    private const float PanelW = 1600f, PanelH = 880f;
    private const float PortraitX = -500f, PortraitY = 30f, PortraitSize = 380f;
    private const float TextX = -200f, TextY = 262f;
    private const float StripY = -360f;

    private void Build()
    {
        var canvasObject = new GameObject("Rowdy Notes Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450; // above the HUD / kill feed, below the pause menu
        canvas.pixelPerfect = true;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        float sliced = scaler.referencePixelsPerUnit / (64f * 4f);

        Image dim = OverlayUI.MakeImage("Dim", canvasObject.transform, new Color(0.04f, 0.01f, 0.07f, 0.8f));
        Stretch(dim.rectTransform);
        root = dim.gameObject;

        Image background = OverlayUI.MakeImage("Panel", root.transform, Color.white, OverlayUI.PanelSprite);
        background.type = Image.Type.Sliced;
        background.pixelsPerUnitMultiplier = sliced;
        panel = background.rectTransform;
        Center(panel, Vector2.zero, new Vector2(PanelW, PanelH));
        panelGroup = background.gameObject.AddComponent<CanvasGroup>();

        // Header: title with underline, page counter with L1 / R1, key hints
        PixelText title = PixelText.Create(panel, "ROWDY NOTES", 5, TitleColor, 0f);
        TopLeft(title.Rect, new Vector2(60f, -40f));
        Image underline = OverlayUI.MakeImage("Underline", panel, HotPink);
        TopLeft(underline.rectTransform, new Vector2(56f, -40f - title.Rect.sizeDelta.y - 10f));
        underline.rectTransform.sizeDelta = new Vector2(title.Rect.sizeDelta.x + 8f, 4f);

        PixelText prevHint = PixelText.Create(panel, "<", 3, Pink, 1f);
        indexText = PixelText.Create(panel, "01 / 16", 3, TitleColor, 0.5f);
        PixelText nextHint = PixelText.Create(panel, ">", 3, Pink, 0f);
        foreach (PixelText p in new[] { prevHint, indexText, nextHint }) p.Rect.anchorMin = p.Rect.anchorMax = new Vector2(1f, 1f);
        indexText.Rect.anchoredPosition = new Vector2(-230f, -62f);
        prevHint.Rect.anchoredPosition = new Vector2(-320f, -62f);
        nextHint.Rect.anchoredPosition = new Vector2(-140f, -62f);
        PixelText keys = PixelText.Create(panel, "TABS: L1 R1 / Q E     PAGES: ARROWS     CLOSE: SELECT / TAB", 2, new Color(1f, 1f, 1f, 0.5f), 1f);
        keys.Rect.anchorMin = keys.Rect.anchorMax = new Vector2(1f, 1f);
        keys.Rect.anchoredPosition = new Vector2(-60f, -104f);

        // Tab bar
        PixelText l1 = PixelText.Create(panel, "L1", 2, Pink, 0f);
        l1.Rect.anchorMin = l1.Rect.anchorMax = new Vector2(0f, 1f);
        l1.Rect.anchoredPosition = new Vector2(60f, -140f);
        float tx = 60f + l1.Rect.sizeDelta.x + 14f;
        for (int i = 0; i < TabNames.Length; i++)
        {
            PixelText label = PixelText.Create(panel, TabNames[i], 2, DimText, 0.5f);
            float w = label.Rect.sizeDelta.x + 28f;
            Image plate = OverlayUI.MakeImage("Tab " + TabNames[i], panel, Color.white);
            plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0f, 1f);
            plate.rectTransform.pivot = new Vector2(0f, 0.5f);
            plate.rectTransform.anchoredPosition = new Vector2(tx, -140f);
            plate.rectTransform.sizeDelta = new Vector2(w, 34f);
            label.transform.SetParent(plate.rectTransform, false);
            Center(label.Rect, new Vector2(0f, 1f), null);
            tabs.Add((plate, label));
            tx += w + 8f;
        }
        PixelText r1 = PixelText.Create(panel, "R1", 2, Pink, 0f);
        r1.Rect.anchorMin = r1.Rect.anchorMax = new Vector2(0f, 1f);
        r1.Rect.anchoredPosition = new Vector2(tx + 6f, -140f);

        // Portrait: dark plate, pink frame, the face, a white flash on page flips
        Image frame = OverlayUI.MakeImage("Portrait Frame", panel, HotPink);
        portraitHolder = frame.rectTransform;
        Center(portraitHolder, new Vector2(PortraitX, PortraitY), new Vector2(PortraitSize + 12f, PortraitSize + 12f));
        Image plateBack = OverlayUI.MakeImage("Plate", portraitHolder, PlateDark);
        Stretch(plateBack.rectTransform, 6f);
        Image glow = OverlayUI.MakeImage("Glow", plateBack.rectTransform, new Color(1f, 0.22f, 0.75f, 0.12f), OverlayUI.WhiteSprite);
        Stretch(glow.rectTransform, 0f);
        glow.rectTransform.offsetMax = new Vector2(0f, -PortraitSize * 0.55f);
        portraitImage = OverlayUI.MakeImage("Face", plateBack.rectTransform, Color.white);
        portraitImage.preserveAspect = true;
        Stretch(portraitImage.rectTransform, 24f);
        portraitGlyph = PixelText.Create(plateBack.rectTransform, "?", 24, new Color(0.55f, 0.25f, 0.6f, 1f), 0.5f);
        Center(portraitGlyph.Rect, Vector2.zero, null);
        portraitFlash = OverlayUI.MakeImage("Flash", portraitHolder, new Color(1f, 1f, 1f, 0f));
        Stretch(portraitFlash.rectTransform, 6f);

        nameText = PixelText.Create(panel, "", 4, TitleColor, 0.5f);
        Center(nameText.Rect, new Vector2(PortraitX, PortraitY - PortraitSize / 2f - 46f), null);
        subtitleText = PixelText.Create(panel, "", 2, Pink, 0.5f);
        Center(subtitleText.Rect, new Vector2(PortraitX, PortraitY - PortraitSize / 2f - 90f), null);

        // Right column (texts are rebuilt per page)
        textColumn = OverlayUI.MakeRect("Text", panel);
        textColumn.anchorMin = textColumn.anchorMax = new Vector2(0.5f, 0.5f);
        textColumn.pivot = new Vector2(0f, 1f);
        textColumn.sizeDelta = new Vector2(860f, 560f);
        textColumn.anchoredPosition = new Vector2(TextX, TextY);
        textGroup = textColumn.gameObject.AddComponent<CanvasGroup>();

        // Bottom strip (rebuilt per tab)
        stripRoot = OverlayUI.MakeRect("Strip", panel);
        Center(stripRoot, new Vector2(0f, StripY), new Vector2(PanelW - 80f, 100f));

        root.SetActive(false);
    }

    private static void TopLeft(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
    }

    private static void Center(RectTransform rect, Vector2 position, Vector2? size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
        if (size.HasValue) rect.sizeDelta = size.Value;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    // ---------------------------------------------------------------- toast ("NEW ROWDY NOTE: GNOLL ARCHER")
    private void UpdateToast()
    {
        float now = Time.unscaledTime;
        float age = now - toastStartedAt;
        float total = ToastIn + ToastHold + ToastOut;

        if (toastRect != null && age >= total)
        {
            Destroy(toastRect.gameObject);
            toastRect = null;
        }
        if (toastRect == null)
        {
            if (toasts.Count == 0 || IsOpen) return;
            var (headline, name, face) = toasts.Dequeue();
            BuildToast(headline, name, face);
            toastStartedAt = now;
            age = 0f;
            UISound.Play(UISound.Cue.Unlock);
        }

        float drop = age < ToastIn ? Ease(age / ToastIn) : age > ToastIn + ToastHold ? 1f - Ease((age - ToastIn - ToastHold) / ToastOut) : 1f;
        toastRect.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Lerp(120f, -24f, drop)));
    }

    private void BuildToast(string headline, string name, Sprite face)
    {
        Image back = OverlayUI.MakePanel("Notes Toast", OverlayUI.Root);
        toastRect = back.rectTransform;
        toastRect.anchorMin = toastRect.anchorMax = new Vector2(0.5f, 1f);
        toastRect.pivot = new Vector2(0.5f, 1f);

        float x = 18f;
        if (face != null)
        {
            Image icon = OverlayUI.MakeImage("Face", toastRect, Color.white, face);
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0f, 0.5f);
            icon.rectTransform.anchoredPosition = new Vector2(x, 0f);
            icon.rectTransform.sizeDelta = new Vector2(56f, 56f);
            x += 70f;
        }
        PixelText top = PixelText.Create(toastRect, headline, 2, headline.StartsWith("COUNTER") ? Counter : Pink, 0f);
        top.Rect.anchorMin = top.Rect.anchorMax = new Vector2(0f, 0.5f);
        top.Rect.anchoredPosition = new Vector2(x, 14f);
        PixelText title = PixelText.Create(toastRect, name, 3, TitleColor, 0f);
        title.Rect.anchorMin = title.Rect.anchorMax = new Vector2(0f, 0.5f);
        title.Rect.anchoredPosition = new Vector2(x, -14f);

        Image key = OverlayUI.MakeImage("Key", toastRect, Color.white, HudTag.Make(LastInputDevice.UsingGamepad ? "SELECT" : "TAB"));
        key.rectTransform.anchorMin = key.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        key.rectTransform.pivot = new Vector2(1f, 0.5f);
        key.rectTransform.sizeDelta = HudTag.UISize(key.sprite) * 0.75f;
        key.rectTransform.anchoredPosition = new Vector2(-18f, 0f);

        float width = Mathf.Max(x + Mathf.Max(top.Rect.sizeDelta.x, title.Rect.sizeDelta.x) + 40f + key.rectTransform.sizeDelta.x, 420f);
        toastRect.sizeDelta = new Vector2(width, 84f);
    }
}
