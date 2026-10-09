using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// EXPERIMENTAL style rank (Devil May Cry idea, Street Fighter V look): every hit Rowdy or his cats land fills a
// meter; fill it to climb D > C > B > A > S > SS > SSS. It drains when you stop fighting.
//   + variety: switching weapons / attack types (stand, duck, jump) pays; spamming the same swing pays less and less
//   + counters, crits, air hits and kills pay extra
//   + no-hit streak: every hit without taking damage multiplies what you earn (up to x2)
//   - getting hit: down two ranks, streak and combo gone
// Only with the MAIN CHARACTER boon (Narcissism): the rank is part of that boon (+damage per rank). Without it nothing is
// scored or shown. Pause > Preferences: Rank Size, Rank Position (screen corner).
// The panel is built at runtime on the overlay canvas: slanted SFV-style plate,
// huge rank letter that slams in, rank title, draining bar with a ghost trail, combo counter, sliding
// event tags (COUNTER / CRITICAL / AIR / VARIETY), shockwave + pixel burst + shake + sound on rank up,
// and the letter splitting in two on rank down. Hooks: EnemyHealth (hits / kills), Health (damage taken).
public class StyleRank : MonoBehaviour
{
    // ---------------------------------------------------------------- tuning
    private static readonly string[] Letters = { "D", "C", "B", "A", "S", "SS", "SSS" };
    private static readonly string[] Titles = { "DOPEY", "CHILL", "BODACIOUS", "AWESOME", "SUPERB", "TUBULAR", "ROWDY DANDY!" };
    private static readonly Color[] RankColors =
    {
        new Color(0.62f, 0.68f, 0.9f), new Color(0.35f, 0.9f, 1f), new Color(0.45f, 1f, 0.5f), new Color(1f, 0.9f, 0.3f),
        new Color(1f, 0.55f, 0.15f), new Color(1f, 0.25f, 0.75f), new Color(1f, 0.85f, 0.3f),
    };
    private const int MaxRank = 6;
    private const float ComboWindow = 2.5f;     // seconds between hits that still count as one combo
    private const float DecayDelay = 1.5f;      // seconds after the last gain before the meter drains

    private static readonly Color Ink = new Color32(0x1B, 0x08, 0x20, 0xFF);
    private static readonly Color Magenta = new Color32(0xFF, 0x39, 0xC0, 0xFF);
    private static readonly Color Plate = new Color32(0x2A, 0x0A, 0x2E, 0xF0);
    private static readonly Color ComboColor = new Color32(0xFF, 0xC9, 0x3C, 0xFF);

    // ---------------------------------------------------------------- state
    private static StyleRank instance;
    public static int Rank => instance != null ? instance.rank : 0;
    public static int BestRank { get; private set; }
    public static string BestRankLetter => Letters[BestRank];
    public static long Score => instance != null ? instance.score : 0;
    public static bool Enabled => Boons.Has("mainchar");

    // Main Character was just taken: show the panel and say hello
    public static void Announce()
    {
        StyleRank r = Get();
        r.shownSince = Time.unscaledTime + 2f;
        r.letterPunch = 0.6f;
        r.titleSlide = 1f;
        r.AddTag("YOU ARE THE MAIN CHARACTER", new Color32(0xFF, 0xD2, 0x4C, 0xFF));
        r.PlaySound("UISounds/UI_Confirm", 1f, 1.1f);
    }

    private int rank;
    private float points;            // 0..100 inside the current rank
    private int combo;
    private int noHitStreak;
    private long score;
    private float lastHitTime = -10f, lastGainTime = -10f, lastAirTag = -10f;
    private readonly List<string> recentMoves = new List<string>();

    private Transform rowdy;
    private Animator rowdyAnimator;
    private PlayerMovement rowdyMovement;
    private AudioSource audioSource;

    // ---------------------------------------------------------------- hooks
    public static void OnHit(EnemyHealth enemy, float damage, bool critical, bool counter, KillCredit credit)
    {
        if (credit == null || credit.kind == KillCredit.Kind.World || !Enabled) return;
        if (enemy != null && enemy.IsObject) return; // cutting plants isn't style
        if (!Application.isPlaying) return;
        Get().Hit(damage, critical, counter, credit);
    }

    public static void OnKill(EnemyHealth enemy, KillCredit credit, KillCredit.Finish finish)
    {
        if (credit == null || credit.kind == KillCredit.Kind.World || !Enabled) return;
        if (enemy != null && enemy.IsObject) return;
        Get().Kill(finish, credit);
    }

    public static void OnPlayerHurt()
    {
        if (instance != null) instance.Hurt();
    }

    private static StyleRank Get()
    {
        if (instance == null)
        {
            var go = new GameObject("StyleRank (auto)");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<StyleRank>();
        }
        return instance;
    }

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        Build();
    }

    // ---------------------------------------------------------------- scoring
    private void Hit(float damage, bool critical, bool counter, KillCredit credit)
    {
        bool cat = credit.kind == KillCredit.Kind.Cat;
        string move = MoveKey(credit, out bool air);

        float gain = cat ? 5f : 9f + Mathf.Min(damage * 0.25f, 16f);
        int repeats = 0;
        foreach (string m in recentMoves) if (m == move) repeats++;
        gain /= 1f + 0.4f * repeats;
        bool variety = !cat && recentMoves.Count >= 2 && recentMoves[recentMoves.Count - 1] != move && repeats <= 1;
        recentMoves.Add(move);
        if (recentMoves.Count > 6) recentMoves.RemoveAt(0);

        if (counter) { gain = gain * 1.5f + 18f; AddTag("COUNTER!", new Color32(0x3C, 0xE8, 0xFF, 0xFF)); }
        if (critical) { gain = gain * 1.3f + 6f; AddTag("CRITICAL", new Color32(0xFF, 0x3A, 0x3A, 0xFF)); }
        if (air && !cat)
        {
            gain *= 1.25f;
            if (Time.time - lastAirTag > 1.2f) { lastAirTag = Time.time; AddTag("AIR", new Color(0.6f, 0.85f, 1f)); }
        }
        if (variety) { gain += 6f; AddTag("VARIETY", new Color(0.5f, 1f, 0.55f)); }
        if (cat) AddTag(credit.name.ToUpperInvariant(), new Color32(0xFF, 0x9B, 0xE6, 0xFF), small: true);

        if (Time.time - lastHitTime > ComboWindow) combo = 0;
        combo++;
        lastHitTime = Time.time;
        noHitStreak++;
        comboPop = 1f;

        AddStyle(gain);
    }

    private void Kill(KillCredit.Finish finish, KillCredit credit)
    {
        float gain = 20f;
        if (finish == KillCredit.Finish.Counter) gain += 15f;
        if (finish == KillCredit.Finish.Critical) gain += 8f;
        if (credit.execution) gain += 6f;
        AddStyle(gain);
    }

    private void AddStyle(float gain)
    {
        float streak = 1f + Mathf.Min(noHitStreak, 50) * 0.02f; // up to x2 for 50 clean hits
        gain *= streak / (1f + rank * 0.3f);                    // higher ranks are harder to fill
        score += Mathf.RoundToInt(gain * 10f * (rank + 1));
        points += gain;
        lastGainTime = Time.time;
        shownSince = Time.unscaledTime;

        while (points >= 100f && rank < MaxRank)
        {
            points -= 100f;
            rank++;
            OnRankUp();
        }
        if (rank == MaxRank) points = Mathf.Min(points, 100f);
    }

    private void Hurt()
    {
        noHitStreak = 0;
        bool hadSomething = rank > 0 || points > 1f || combo > 0;
        combo = 0;
        if (!hadSomething) return;

        int before = rank;
        rank = Mathf.Max(0, rank - 2);
        points = rank == before ? 0f : 30f;
        OnRankDown(before);
    }

    private string MoveKey(KillCredit credit, out bool air)
    {
        air = false;
        if (credit.kind == KillCredit.Kind.Cat) return "cat:" + credit.name;

        if (rowdy == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { rowdy = p.transform; rowdyAnimator = p.GetComponent<Animator>(); rowdyMovement = p.GetComponent<PlayerMovement>(); }
        }
        string attack = "swing";
        if (rowdyAnimator != null && rowdyAnimator.isActiveAndEnabled)
        {
            AnimatorStateInfo s = rowdyAnimator.GetCurrentAnimatorStateInfo(0);
            if (s.IsTag("JumpAttack")) attack = "jump";
            else if (s.IsTag("DuckingAttack")) attack = "duck";
            else if (s.IsTag("NeutralAttack")) attack = "stand";
        }
        air = rowdyMovement != null && !rowdyMovement.IsGrounded;
        if (credit.with == "Level Up") attack = "levelup";
        return credit.with + ":" + attack;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (Time.time - lastGainTime > DecayDelay && (rank > 0 || points > 0f))
        {
            points -= (5f + rank * 4f) * dt;
            if (points < 0f)
            {
                if (rank > 0) { rank--; points += 60f; letterPunch = 0.5f; titleSlide = 1f; }
                else points = 0f;
            }
        }
        if (combo > 0 && Time.time - lastHitTime > ComboWindow) combo = 0;
        BestRank = Mathf.Max(BestRank, rank);
    }

    // ---------------------------------------------------------------- UI
    private RectTransform root, plate, letterHolder, barRect;
    private CanvasGroup group;
    private PixelText letter, letterShadow, letterEcho, title, scoreText, comboNumber, comboLabel, streakText;
    private Image barFill, barGhost, flash, ring;
    private RawImage stripes;
    private float shownSince = -100f;
    private float letterPunch, flashAlpha, titleSlide, comboPop, ringAge = 99f, echoAge = 99f;
    private float ghostFill;
    private int shownRank = -1;
    private string shownTitle;

    private class Tag { public RectTransform rect; public PixelText text; public Image plate; public float age; public float y; }
    private readonly List<Tag> tags = new List<Tag>();

    private class Bit { public RectTransform rect; public Image image; public Vector2 velocity; public float age, life; public float spin; }
    private readonly List<Bit> bits = new List<Bit>();

    private const float PlateW = 420f, PlateH = 150f;

    private void Build()
    {
        root = OverlayUI.MakeRect("Style Rank", OverlayUI.Root);
        ApplyLayout();
        root.sizeDelta = new Vector2(PlateW, PlateH);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // Slanted plate: dark fill, magenta edge, ink outline, scrolling diagonal stripes
        Image back = OverlayUI.MakeImage("Plate", root, Color.white, Parallelogram(210, 75, 14, Plate, Magenta, Ink));
        plate = back.rectTransform;
        Stretch(plate);
        stripes = new GameObject("Stripes", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        stripes.transform.SetParent(plate, false);
        stripes.texture = StripeTexture();
        stripes.color = new Color(1f, 1f, 1f, 0.07f);
        stripes.raycastTarget = false;
        Stretch(stripes.rectTransform, 10f);
        stripes.rectTransform.offsetMin = new Vector2(40f, 10f);
        stripes.rectTransform.offsetMax = new Vector2(-14f, -10f);

        flash = OverlayUI.MakeImage("Flash", plate, new Color(1f, 1f, 1f, 0f), Parallelogram(210, 75, 14, Color.white, Color.white, Color.white));
        Stretch(flash.rectTransform);

        // Big letter (with an ink shadow and an echo that bursts out on rank up)
        letterHolder = OverlayUI.MakeRect("Letter", root);
        letterHolder.anchorMin = letterHolder.anchorMax = new Vector2(0f, 0.5f);
        letterHolder.anchoredPosition = new Vector2(110f, 6f);
        letterEcho = PixelText.Create(letterHolder, "D", 15, Color.white, 0.5f);
        letterShadow = PixelText.Create(letterHolder, "D", 15, Ink, 0.5f);
        letterShadow.Rect.anchoredPosition = new Vector2(7f, -7f);
        letter = PixelText.Create(letterHolder, "D", 15, Color.white, 0.5f);

        ring = OverlayUI.MakeImage("Ring", letterHolder, new Color(1f, 1f, 1f, 0f), RingSprite());
        ring.rectTransform.sizeDelta = new Vector2(120f, 120f);

        // Right side: title, bar, score
        title = PixelText.Create(root, "DOPEY", 3, Color.white, 0f);
        Anchor(title.Rect, new Vector2(212f, 34f));

        Image barBack = OverlayUI.MakeImage("Bar", root, Color.white, Parallelogram(92, 9, 4, new Color(0.08f, 0.02f, 0.1f, 1f), Ink, Ink));
        barRect = barBack.rectTransform;
        barRect.anchorMin = barRect.anchorMax = new Vector2(0f, 0.5f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.sizeDelta = new Vector2(184f, 18f);
        barRect.anchoredPosition = new Vector2(208f, 2f);
        Sprite fillShape = Parallelogram(90, 7, 4, Color.white, Color.white, Color.white);
        barGhost = OverlayUI.MakeImage("Ghost", barRect, new Color(1f, 1f, 1f, 0.75f), fillShape);
        MakeFilled(barGhost);
        barFill = OverlayUI.MakeImage("Fill", barRect, Color.white, fillShape);
        MakeFilled(barFill);

        scoreText = PixelText.Create(root, "STYLE 0", 2, new Color(1f, 1f, 1f, 0.75f), 0f);
        Anchor(scoreText.Rect, new Vector2(212f, -28f));
        streakText = PixelText.Create(root, "", 2, new Color(0.55f, 1f, 0.6f, 0.9f), 0f);
        Anchor(streakText.Rect, new Vector2(212f, -50f));

        // Combo counter above the plate:  12 HITS
        comboNumber = PixelText.Create(root, "0", 6, ComboColor, 0f);
        comboNumber.Rect.anchorMin = comboNumber.Rect.anchorMax = new Vector2(0f, 1f);
        comboNumber.Rect.pivot = new Vector2(0f, 0f);
        comboLabel = PixelText.Create(root, "HITS", 3, Color.white, 0f);
        comboLabel.Rect.anchorMin = comboLabel.Rect.anchorMax = new Vector2(0f, 1f);
        comboLabel.Rect.pivot = new Vector2(0f, 0f);
        ApplyLayout();
        GameSettings.Changed += ApplyLayout;
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;
        if (root == null) return;

        // Show while there's anything to show; slide in from the right, fade out a while after it's all gone
        bool active = rank > 0 || points > 0.5f || combo > 0;
        if (active) shownSince = Mathf.Max(shownSince, now - 0.01f);
        float target = (active || now - shownSince < 2f) && !PauseMenu.IsPaused && Enabled ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, dt * (target > group.alpha ? 6f : 1.5f));
        float slide = 1f - group.alpha;
        root.anchoredPosition = homePosition + slideFrom * (slide * slide * 160f);
        if (group.alpha <= 0f && !active) { ClearTransient(); return; }

        Color rc = RankColor(rank, now);

        // Letter + title
        if (shownRank != rank) SetRankVisuals();
        letterPunch = Mathf.MoveTowards(letterPunch, 0f, dt * 4f);
        float punch = EaseOutBack(1f - letterPunch);
        float scale = Mathf.Lerp(2.6f, 1f, punch);
        float bob = 1f + 0.035f * Mathf.Sin(now * 6f) * (rank / (float)MaxRank);
        letterHolder.localScale = Vector3.one * scale * bob;
        letterHolder.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-25f, 0f, punch) + Mathf.Sin(now * 2.3f) * 2f * (rank / (float)MaxRank));
        Vector2 jitter = rank >= 5 ? new Vector2(Mathf.Round(Random.Range(-1f, 1f) * (rank - 4) * 1.5f), Mathf.Round(Random.Range(-1f, 1f) * (rank - 4) * 1.5f)) : Vector2.zero;
        letter.Rect.anchoredPosition = jitter;
        letter.Color = Color.Lerp(rc, Color.white, letterPunch * 0.8f);

        // Echo: a big transparent copy flying out after a rank up
        echoAge += dt;
        float e = Mathf.Clamp01(echoAge / 0.45f);
        letterEcho.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 2.2f, e);
        letterEcho.Color = new Color(rc.r, rc.g, rc.b, 0.6f * (1f - e));

        ringAge += dt;
        float r = Mathf.Clamp01(ringAge / 0.4f);
        ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 3.4f, 1f - (1f - r) * (1f - r));
        ring.color = new Color(rc.r, rc.g, rc.b, 0.9f * (1f - r));

        titleSlide = Mathf.MoveTowards(titleSlide, 0f, dt * 5f);
        title.Rect.anchoredPosition = new Vector2(212f + titleSlide * titleSlide * 80f, 34f);
        title.Color = new Color(rc.r, rc.g, rc.b, 1f - titleSlide);

        // Bar: fill in the rank colour, white ghost catching up on drains, pulses when nearly full
        float fill = Mathf.Clamp01(points / 100f);
        barFill.fillAmount = fill;
        ghostFill = ghostFill > fill ? Mathf.MoveTowards(ghostFill, fill, dt * 0.6f) : fill;
        barGhost.fillAmount = ghostFill;
        float full = fill > 0.85f && rank < MaxRank ? 0.5f + 0.5f * Mathf.Sin(now * 18f) : 0f;
        barFill.color = Color.Lerp(rc, Color.white, full * 0.6f);

        // Flash + stripes
        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 3.5f);
        flash.color = new Color(1f, 1f, 1f, flashAlpha);
        Rect uv = stripes.uvRect;
        uv.x = Mathf.Repeat(now * (0.15f + rank * 0.12f), 1f);
        uv.width = 6f;
        uv.height = 2f;
        stripes.uvRect = uv;
        stripes.color = new Color(rc.r, rc.g, rc.b, 0.06f + 0.03f * rank);

        scoreText.SetText("STYLE " + score.ToString("000000"));
        streakText.SetText(noHitStreak >= 5 ? "NO HIT X" + (1f + Mathf.Min(noHitStreak, 50) * 0.02f).ToString("0.0") : "");

        // Combo
        bool showCombo = combo >= 2;
        comboNumber.gameObject.SetActive(showCombo);
        comboLabel.gameObject.SetActive(showCombo);
        if (showCombo)
        {
            comboNumber.SetText(combo.ToString());
            comboPop = Mathf.MoveTowards(comboPop, 0f, dt * 5f);
            comboNumber.Rect.localScale = Vector3.one * (1f + 0.5f * comboPop);
            Color cc = combo >= 25 ? new Color(1f, 0.3f, 0.3f) : combo >= 10 ? new Color(1f, 0.6f, 0.2f) : ComboColor;
            float fade = Mathf.Clamp01((ComboWindow - (Time.time - lastHitTime)) / 0.6f);
            comboNumber.Color = new Color(cc.r, cc.g, cc.b, fade);
            comboLabel.Color = new Color(1f, 1f, 1f, fade);
            comboLabel.Rect.anchoredPosition = new Vector2(34f + comboNumber.Rect.sizeDelta.x + 8f, comboBelow ? -10f : 10f);
        }

        UpdateTags(dt);
        UpdateBits(dt);
    }

    // ---------------------------------------------------------------- placement (settings)
    private Vector2 homePosition, slideFrom;
    private bool tagsOnRight, comboBelow;

    private void ApplyLayout()
    {
        if (root == null) return;
        int p = GameSettings.StyleRankPosition;          // Top Left, Top Center, Top Right, Bottom Left, Bottom Center, Bottom Right
        bool top = p <= 2;
        int column = p % 3;                              // 0 left, 1 center, 2 right
        float ax = column * 0.5f, ay = top ? 1f : 0f;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(ax, ay);
        root.localScale = Vector3.one * GameSettings.StyleRankSize;

        // Room for what's already there: Rowdy's HUD top left, kill feed top right, button prompts bottom left
        float x = column == 0 ? 24f : column == 2 ? -24f : 0f;
        float y = top ? (column == 0 ? -330f : column == 2 ? -270f : -16f) : (column == 0 ? 80f : 24f);
        float comboRoom = 70f * GameSettings.StyleRankSize; // the combo counter sits on the inner side
        if (!top) y += comboRoom;
        homePosition = new Vector2(x, y);
        slideFrom = column == 0 ? Vector2.left : column == 2 ? Vector2.right : (top ? Vector2.up : Vector2.down);
        tagsOnRight = column == 0;
        comboBelow = top;

        if (comboNumber != null)
        {
            Vector2 a = comboBelow ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
            Vector2 piv = comboBelow ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            comboNumber.Rect.anchorMin = comboNumber.Rect.anchorMax = a; comboNumber.Rect.pivot = piv;
            comboLabel.Rect.anchorMin = comboLabel.Rect.anchorMax = a; comboLabel.Rect.pivot = piv;
            comboNumber.Rect.anchoredPosition = new Vector2(34f, comboBelow ? -8f : 8f);
        }
    }

    private void OnDestroy() => GameSettings.Changed -= ApplyLayout;

    private void SetRankVisuals()
    {
        shownRank = rank;
        string l = Letters[rank];
        int s = l.Length == 1 ? 15 : l.Length == 2 ? 12 : 9;
        foreach (PixelText p in new[] { letter, letterShadow, letterEcho }) { p.Rect.localScale = Vector3.one; SetScale(p, l, s); }
        if (shownTitle != Titles[rank]) { shownTitle = Titles[rank]; title.SetText(shownTitle); }
    }

    private static void SetScale(PixelText p, string text, int scale)
    {
        p.SetText(text);
        p.SetScale(scale);
    }

    private void OnRankUp()
    {
        letterPunch = 1f;
        flashAlpha = 0.85f;
        titleSlide = 1f;
        ringAge = 0f;
        echoAge = 0f;
        shownRank = -1;
        SetRankVisuals();
        Burst(RankColors[rank], 10 + rank * 4);
        if (!Enabled) return; // no Main Character: no shake / rumble / sound either
        ScreenShake.Impulse(0.15f + rank * 0.07f);
        if (rank >= 4) GamepadRumble.Pulse(0.3f, 0.6f, 0.12f);
        PlaySound("UISounds/New UI sounds/Ui Confirm", 0.9f, 0.9f + rank * 0.08f);
        AddTag(Titles[rank], RankColors[rank]);
    }

    private void OnRankDown(int from)
    {
        flashAlpha = 0.6f;
        flash.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        SplitLetter(Letters[from], RankColors[from]);
        shownRank = -1;
        letterPunch = 0.6f;
        titleSlide = 1f;
        AddTag("RANK DOWN", new Color32(0xFF, 0x3A, 0x3A, 0xFF));
        PlaySound("UISounds/UI_Back", 1f, 0.7f);
    }

    private void PlaySound(string path, float volume, float pitch)
    {
        if (!Enabled) return;
        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip == null || audioSource == null) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }

    // ---------------------------------------------------------------- tags (slide in from the plate, stack, fade)
    private void AddTag(string text, Color color, bool small = false)
    {
        if (root == null) return;
        if (tags.Count >= 4) RemoveTag(0);
        PixelText label = PixelText.Create(root, text, small ? 2 : 3, Color.white, 1f);
        float w = label.Rect.sizeDelta.x + 30f, h = label.Rect.sizeDelta.y + 10f;
        Image back = OverlayUI.MakeImage("Tag", root, color, Parallelogram(Mathf.Max(8, Mathf.RoundToInt(w / 2f)), Mathf.Max(4, Mathf.RoundToInt(h / 2f)), 4, Color.white, Color.white, Ink));
        RectTransform rect = back.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(tagsOnRight ? 1f : 0f, 0.5f);
        rect.pivot = new Vector2(tagsOnRight ? 0f : 1f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        label.transform.SetParent(rect, false);
        label.Rect.anchorMin = label.Rect.anchorMax = new Vector2(0.5f, 0.5f);
        label.Rect.pivot = new Vector2(0.5f, 0.5f);
        label.Rect.anchoredPosition = new Vector2(2f, 1f);
        label.Color = Ink;
        tags.Add(new Tag { rect = rect, text = label, plate = back });
    }

    private void UpdateTags(float dt)
    {
        float y = 50f;
        for (int i = tags.Count - 1; i >= 0; i--)
        {
            Tag t = tags[i];
            t.age += dt;
            if (t.age > 1.4f) { RemoveTag(i); continue; }
            float inT = Mathf.Clamp01(t.age / 0.14f);
            float outT = Mathf.Clamp01((t.age - 1.0f) / 0.4f);
            t.y = Mathf.Lerp(t.y == 0f ? y : t.y, y, dt * 14f);
            float side = tagsOnRight ? 1f : -1f; // tags pop out of the plate towards the middle of the screen
            t.rect.anchoredPosition = new Vector2(Mathf.Round(side * (6f - (1f - EaseOutBack(inT)) * 120f - outT * 30f)), Mathf.Round(t.y));
            t.rect.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, inT), 1f);
            Color c = t.plate.color; c.a = 1f - outT; t.plate.color = c;
            t.text.Color = new Color(Ink.r, Ink.g, Ink.b, 1f - outT);
            y -= t.rect.sizeDelta.y + 6f;
        }
    }

    private void RemoveTag(int i)
    {
        if (tags[i].rect != null) Destroy(tags[i].rect.gameObject);
        tags.RemoveAt(i);
    }

    // ---------------------------------------------------------------- particles
    private void Burst(Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Image img = OverlayUI.MakeImage("Bit", letterHolder.parent, i % 3 == 0 ? Color.white : color, OverlayUI.WhiteSprite);
            float size = Random.Range(6f, 14f);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.rectTransform.anchoredPosition = letterHolder.anchoredPosition;
            float a = Random.Range(0f, Mathf.PI * 2f);
            bits.Add(new Bit { rect = img.rectTransform, image = img, velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(250f, 650f), life = Random.Range(0.4f, 0.8f), spin = Random.Range(-720f, 720f) });
        }
    }

    // The old letter splits down the middle and both halves fall away
    private void SplitLetter(string text, Color color)
    {
        for (int side = 0; side < 2; side++)
        {
            PixelText half = PixelText.Create(letterHolder.parent, text, 1, color, 0.5f);
            SetScale(half, text, text.Length == 1 ? 15 : text.Length == 2 ? 12 : 9);
            RawImage raw = half.GetComponent<RawImage>();
            raw.uvRect = new Rect(side * 0.5f, 0f, 0.5f, 1f);
            Vector2 size = half.Rect.sizeDelta;
            half.Rect.sizeDelta = new Vector2(size.x / 2f, size.y);
            half.Rect.anchorMin = half.Rect.anchorMax = new Vector2(0f, 0.5f);
            half.Rect.anchoredPosition = letterHolder.anchoredPosition + new Vector2((side - 0.5f) * size.x / 2f, 0f);
            bits.Add(new Bit { rect = half.Rect, image = null, velocity = new Vector2(side == 0 ? -160f : 160f, 220f), life = 0.9f, spin = side == 0 ? 160f : -160f });
            half.Color = new Color(1f, 0.35f, 0.35f, 1f);
        }
    }

    private void UpdateBits(float dt)
    {
        for (int i = bits.Count - 1; i >= 0; i--)
        {
            Bit b = bits[i];
            b.age += dt;
            if (b.rect == null || b.age >= b.life) { if (b.rect != null) Destroy(b.rect.gameObject); bits.RemoveAt(i); continue; }
            b.velocity += Vector2.down * 900f * dt;
            b.rect.anchoredPosition += b.velocity * dt;
            b.rect.localRotation = Quaternion.Euler(0f, 0f, b.rect.localEulerAngles.z + b.spin * dt);
            float alpha = 1f - b.age / b.life;
            if (b.image != null) { Color c = b.image.color; c.a = alpha; b.image.color = c; }
            else if (b.rect.TryGetComponent(out PixelText p)) { Color c = p.Color; c.a = alpha; p.Color = c; }
        }
    }

    private void ClearTransient()
    {
        for (int i = tags.Count - 1; i >= 0; i--) RemoveTag(i);
        foreach (Bit b in bits) if (b.rect != null) Destroy(b.rect.gameObject);
        bits.Clear();
    }

    // ---------------------------------------------------------------- helpers
    private static Color RankColor(int rank, float now)
    {
        if (rank < MaxRank) return RankColors[rank];
        return Color.HSVToRGB(Mathf.Repeat(now * 0.35f, 1f), 0.55f, 1f); // SSS: rainbow
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static void Anchor(RectTransform r, Vector2 pos)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = pos;
    }

    private static void Stretch(RectTransform r, float inset = 0f)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
    }

    private static void MakeFilled(Image img)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        Stretch(img.rectTransform, 2f);
    }

    // Slanted box (top edge shifted right by `slant` px): ink outline, 1px edge colour, fill
    private static Sprite Parallelogram(int w, int h, int slant, Color fill, Color edge, Color outline)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "SlantBox" };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float k = h > 1 ? y / (float)(h - 1) : 0f;
            int left = Mathf.RoundToInt(k * slant);
            int right = w - 1 - slant + left;
            for (int x = 0; x < w; x++)
            {
                Color c = new Color(0f, 0f, 0f, 0f);
                if (x >= left && x <= right)
                {
                    bool border = x == left || x == right || y == 0 || y == h - 1;
                    bool inner = x == left + 1 || x == right - 1 || y == 1 || y == h - 2;
                    c = border ? outline : inner ? edge : fill;
                }
                px[y * w + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private static Texture2D stripeTexture;
    private static Texture2D StripeTexture()
    {
        if (stripeTexture != null) return stripeTexture;
        const int s = 16;
        stripeTexture = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "StyleStripes" };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                px[y * s + x] = ((x + y) % s) < 5 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
        stripeTexture.SetPixels32(px);
        stripeTexture.Apply(false, true);
        return stripeTexture;
    }

    private static Sprite ringSprite;
    private static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int size = 40;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "StyleRing" };
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                px[y * size + x] = d > c - 2.5f && d <= c + 0.5f ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }
}
