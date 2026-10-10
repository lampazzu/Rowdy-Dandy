using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The level-up boon choice (Hades style): three cards from three patrons, dealt in one by one with a rarity sound.
// The highlighted card's patron talks in the box on top (typed out, in their colour). Pick with {OK} / click,
// reroll the hand with {INTERACT} if you have Hair Gel. The game is paused while it's open.
// Opens by itself when there are boon picks waiting (Boons.PendingPicks) and nothing else is on screen.
public class BoonPicker : MonoBehaviour
{
    private const float CardW = 440f, CardH = 540f, CardGap = 46f;
    private const float CardsY = -122f;              // card row centre (below the patron's box)
    private const float MedY = 118f, NameY = -4f, RowY = -48f, LineY = -70f, DescY = -88f;
    private const float InputDelay = 0.75f;
    private const float AudioBoost = 2.6f;

    private static BoonPicker instance;
    public static bool IsOpen { get; private set; }
    private static int closedFrame = -10;
    public static bool BlocksInput => IsOpen || Time.frameCount <= closedFrame + 2;

    private class Card
    {
        public Boons.Offer offer;
        public RectTransform root, body;
        public CanvasGroup group;
        public Image frame, glow, rays, medallion, icon, shine, gem;
        public PixelText patron, name, slot, rarity, desc, footer;
        public string line;
        public float select;      // 0..1 eased selection
        public float dealAt, landedAt = -1f;
        public float flip = 1f;   // scale x during a reroll
        public bool whooshed;
        public Vector2 home;
    }

    private RectTransform root, dialog, cardsRoot, particlesRoot;
    private Image dim, wash, emblem, emblemPlate, flash;
    private PixelText header, subheader, patronName, patronTitle, quote, hints, counter;
    private readonly List<Card> cards = new List<Card>();
    private readonly List<(Image img, Vector2 vel, float life, float age, float spin)> particles = new List<(Image, Vector2, float, float, float)>();
    private int selected;
    private float openedAt, navTimer;
    private int heldDir;
    private bool picking, rerolling;
    private string quoteFull = "";
    private int voiceBlips;
    private float quoteShownAt;
    private Color washColor = Color.clear;
    private AudioSource audioSource;
    private bool savedCursorVisible;
    private CursorLockMode savedLock;
    private int hintVersion = -1;
    private float mouseIdleUntil, mouseTravel;
    private Vector2 lastMouse;
    private int totalThisRun, indexThisRun;

    // ---------------------------------------------------------------- lifetime
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("BoonPicker (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<BoonPicker>();
        SceneManager.sceneLoaded += (s, m) => { if (instance != null && IsOpen) instance.CloseNow(); };
    }

    private void Update()
    {
        if (IsOpen) { Tick(); return; }
        if (Boons.PendingPicks <= 0 || Time.unscaledTime < Boons.OpenNotBefore) { totalThisRun = 0; return; }
        if (PauseMenu.IsPaused || RowdyNotes.IsOpen || WorldMap.IsOpen || Tutorials.IsOpen || CatParty.IsOpen || CheckpointRest.Resting || FirstDrop.Running) return;
        if (ArenaRun.HoldBoons) return; // mid colosseum trial: the picks wait for the next seal
        if (StephmossForm.Transforming) return;
        Health rowdy = FindFirstObjectByType<Health>();
        if (rowdy == null || rowdy.IsDead || Werewolf.Transforming) return;
        Open();
    }

    // ---------------------------------------------------------------- open / close
    private void Open()
    {
        if (root == null) Build();
        List<Boons.Offer> offer = Boons.MakeOffer();
        if (offer.Count == 0) { Boons.SkipPick(); return; } // everything owned at its best: nothing left to offer
        if (totalThisRun == 0) { totalThisRun = Boons.PendingPicks; indexThisRun = 0; }
        indexThisRun++;

        IsOpen = true;
        picking = rerolling = false;
        openedAt = Time.unscaledTime;
        PauseMenu.SetExternalPause(true);
        savedCursorVisible = Cursor.visible;
        savedLock = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        lastMouse = MousePos;
        mouseTravel = 0f;
        mouseIdleUntil = Time.unscaledTime + 0.5f;

        root.gameObject.SetActive(true);
        header.SetText(ArenaRun.InTrial ? "BOON TIME!" : "LEVEL UP!"); // colosseum boons come from the gong / every 5 waves, not levels
        counter.SetText(totalThisRun > 1 ? "BOON " + indexThisRun + " OF " + totalThisRun : "");
        Deal(offer, 0.35f);
        RefreshHints();
        Tick(); // lay everything out before the first frame is drawn
        BoonArt art = BoonArt.Get;
        if (art != null)
        {
            PlaySfx(art.fanfare, 0.7f);
            PlaySfx(art.open, 0.35f, 1.1f);
            PlaySfx(art.sparkle, 0.3f, 1.25f);
        }
        flash.color = new Color(1f, 1f, 1f, 0.35f);
    }

    private void Deal(List<Boons.Offer> offer, float delay)
    {
        foreach (Card c in cards) if (c.root != null) Destroy(c.root.gameObject);
        cards.Clear();
        float total = offer.Count * CardW + (offer.Count - 1) * CardGap;
        for (int i = 0; i < offer.Count; i++)
        {
            Card c = BuildCard(offer[i]);
            c.home = new Vector2(-total / 2f + CardW / 2f + i * (CardW + CardGap), CardsY);
            c.dealAt = Time.unscaledTime + delay + i * 0.14f;
            c.whooshed = false;
            c.root.anchoredPosition = c.home + new Vector2(0f, -900f);
            c.group.alpha = 0f;
            cards.Add(c);
        }
        selected = Mathf.Clamp(selected, 0, cards.Count - 1);
        if (Time.unscaledTime - openedAt < 0.1f) selected = cards.Count > 1 ? 1 : 0;
        ShowQuote(cards.Count > 0 ? cards[selected] : null);
    }

    private void Finish(Card chosen)
    {
        StartCoroutine(PickRoutine(chosen));
    }

    private IEnumerator PickRoutine(Card chosen)
    {
        picking = true;
        PatronInfo p = BoonCatalog.Of(chosen.offer.def.patron);
        BoonArt art = BoonArt.Get;
        PlaySfx(art != null ? art.cardPick : null, 1f);
        PlaySfx(art != null ? (chosen.offer.upgrade ? art.upgrade : PatronSound(chosen.offer.def.patron)) : null, 0.8f);
        PlaySfx(art != null ? art.coin : null, 0.45f, 1.05f);
        PlaySfx(art != null ? art.swap : null, 0.35f, 1.2f);
        if (chosen.offer.def.IsDuo && art != null) PlaySfx(PatronSound(chosen.offer.def.partner.Value), 0.6f, 1.05f);
        StartCoroutine(PickSparkle(art, (int)chosen.offer.rarity));
        UISound.Play(UISound.Cue.Confirm);
        string line = chosen.offer.def.IsDuo ? chosen.offer.def.duoLine : p.pick[Random.Range(0, p.pick.Length)];
        SetQuote(chosen, line);
        flash.color = new Color(p.color.r, p.color.g, p.color.b, 0.45f);
        Burst(chosen.root.anchoredPosition + cardsRoot.anchoredPosition, p.color, 40, 900f);
        Burst(chosen.root.anchoredPosition + cardsRoot.anchoredPosition, Color.white, 16, 600f);

        float t = 0f;
        while (t < 1.05f)
        {
            t += Time.unscaledDeltaTime;
            foreach (Card c in cards)
            {
                if (c == chosen)
                {
                    float pop = t < 0.12f ? Mathf.Lerp(1.06f, 1.18f, t / 0.12f) : Mathf.Lerp(1.18f, 1.1f, Mathf.Clamp01((t - 0.12f) / 0.3f));
                    c.root.localScale = Vector3.one * pop;
                    c.root.anchoredPosition = Vector2.Lerp(c.root.anchoredPosition, new Vector2(0f, CardsY + 20f), Time.unscaledDeltaTime * 8f);
                    c.glow.color = new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(t * 20f));
                }
                else
                {
                    c.root.anchoredPosition += new Vector2(0f, -1600f * Time.unscaledDeltaTime * Mathf.Clamp01(t * 3f));
                    c.root.localRotation = Quaternion.Euler(0f, 0f, (c.home.x < 0 ? 1f : -1f) * t * 30f);
                    c.group.alpha = Mathf.Max(0f, 1f - t * 2.5f);
                }
            }
            AnimateCommon();
            yield return null;
        }

        // out
        float o = 0f;
        while (o < 0.22f)
        {
            o += Time.unscaledDeltaTime;
            float k = 1f - o / 0.22f;
            rootGroup.alpha = k;
            chosen.root.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.1f, k);
            yield return null;
        }
        Boons.Take(chosen.offer);
        CloseNow();
    }

    // a bright little ta-da a beat after the pick, higher for better cards
    private IEnumerator PickSparkle(BoonArt art, int rarity)
    {
        float t = 0f;
        while (t < 0.22f) { t += Time.unscaledDeltaTime; yield return null; }
        if (art != null) PlaySfx(art.sparkle, 0.45f, 1f + 0.08f * Mathf.Min(rarity, 3));
        if (art != null && art.rarity != null && art.rarity.Length == 5) PlaySfx(art.rarity[Mathf.Clamp(rarity, 0, 4)], 0.4f, 1.25f);
    }

    private void CloseNow()
    {
        StopAllCoroutines();
        IsOpen = false;
        picking = rerolling = false;
        closedFrame = Time.frameCount;
        if (root != null) root.gameObject.SetActive(false);
        if (rootGroup != null) rootGroup.alpha = 1f;
        foreach (var part in particles) if (part.img != null) Destroy(part.img.gameObject);
        particles.Clear();
        Cursor.visible = savedCursorVisible;
        Cursor.lockState = savedLock;
        PauseMenu.SetExternalPause(false);
        if (Boons.PendingPicks <= 0) totalThisRun = 0;
    }

    private void Reroll()
    {
        if (!IsOpen || rerolling || picking) return;
        if (!Boons.SpendReroll())
        {
            PlaySfx(BoonArt.Get != null ? BoonArt.Get.cancel : null, 0.8f);
            UISound.Play(UISound.Cue.Locked);
            hintShake = 0.3f;
            return;
        }
        StartCoroutine(RerollRoutine());
    }

    private IEnumerator RerollRoutine()
    {
        rerolling = true;
        PlaySfx(BoonArt.Get != null ? BoonArt.Get.reroll : null, 1f);
        PlaySfx(BoonArt.Get != null ? BoonArt.Get.gel : null, 0.6f);
        var previous = new List<Boons.Offer>();
        foreach (Card c in cards) previous.Add(c.offer);
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.unscaledDeltaTime;
            foreach (Card c in cards) { c.flip = 1f - t / 0.18f; c.root.localScale = new Vector3(Mathf.Max(0.01f, c.flip), 1f, 1f); }
            yield return null;
        }
        List<Boons.Offer> next = Boons.MakeOffer(previous);
        Deal(next.Count > 0 ? next : previous, 0f);
        foreach (Card c in cards) { c.root.anchoredPosition = c.home; c.dealAt = Time.unscaledTime - 1f; c.landedAt = Time.unscaledTime; c.group.alpha = 1f; c.flip = 0f; }
        t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            foreach (Card c in cards) c.flip = t / 0.2f;
            yield return null;
        }
        foreach (Card c in cards) c.flip = 1f;
        Burst(new Vector2(0f, CardsY), new Color(0.6f, 1f, 0.9f), 30, 700f);
        RefreshHints();
        rerolling = false;
    }

    // ---------------------------------------------------------------- per frame
    private float hintShake;

    private void Tick()
    {
        float now = Time.unscaledTime;
        if (hintVersion != GameInput.DeviceVersion) RefreshHints();

        if (!picking && !rerolling && now - openedAt > InputDelay) HandleInput();

        // deal-in animation + selection
        for (int i = 0; i < cards.Count; i++)
        {
            Card c = cards[i];
            if (picking) break;
            float since = now - c.dealAt;
            if (since < 0f) { c.group.alpha = 0f; continue; }
            if (!c.whooshed) { c.whooshed = true; PlaySfx(BoonArt.Get != null ? BoonArt.Get.whoosh : null, 0.18f, 1.5f + i * 0.15f); }
            if (c.landedAt < 0f && since >= 0.32f) Land(c);
            float k = Mathf.Clamp01(since / 0.32f);
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            float target = i == selected ? 1f : 0f;
            c.select = Mathf.MoveTowards(c.select, target, Time.unscaledDeltaTime * 7f);
            float s = c.select * c.select * (3f - 2f * c.select);
            Vector2 pos = c.home + new Vector2(0f, 26f * s);
            c.root.anchoredPosition = Vector2.Lerp(c.home + new Vector2(0f, -900f), pos, ease);
            c.root.localRotation = Quaternion.Euler(0f, 0f, (1f - ease) * (i - 1) * -12f);
            float landPop = c.landedAt > 0f ? Mathf.Max(0f, 1f - (now - c.landedAt) / 0.2f) * 0.06f : 0f;
            float flipX = c.landedAt < 0f ? Mathf.Clamp01(since / 0.18f) : c.flip;
            float scale = 1f + 0.06f * s + landPop;
            c.root.localScale = new Vector3(scale * Mathf.Max(0.01f, flipX), scale, 1f);
            c.group.alpha = Mathf.Clamp01(since / 0.12f) * Mathf.Lerp(0.72f, 1f, s);
            AnimateCard(c, s, now);
        }
        if (!picking) AnimateCommon(); // the pick animation drives it itself
    }

    private void Land(Card c)
    {
        c.landedAt = Time.unscaledTime;
        BoonArt art = BoonArt.Get;
        int r = (int)c.offer.rarity;
        if (art != null && art.rarity != null && art.rarity.Length == 5) PlaySfx(art.rarity[Mathf.Clamp(r, 0, 4)], 0.75f);
        if (art != null && art.rarityLayer != null && art.rarityLayer.Length == 5) PlaySfx(art.rarityLayer[Mathf.Clamp(r, 0, 4)], 0.35f, 1.15f);
        if (art != null && r >= (int)Rarity.Epic) PlaySfx(art.sparkle, 0.25f, 1.1f + 0.1f * r);
        Color rc = BoonCatalog.RarityColors[r];
        if (r >= 1) Burst(c.home + cardsRoot.anchoredPosition, rc, 8 + r * 8, 400f + r * 150f);
    }

    private void AnimateCard(Card c, float s, float now)
    {
        PatronInfo p = BoonCatalog.Of(c.offer.def.patron);
        Color rc = BoonCatalog.RarityColors[(int)c.offer.rarity];
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * 5f + c.home.x);
        c.glow.color = new Color(rc.r, rc.g, rc.b, (0.15f + 0.35f * s) * (0.7f + 0.3f * pulse));
        c.frame.color = Color.Lerp(p.color * 0.75f + new Color(0, 0, 0, 0.25f), p.color, s);
        if (c.rays.enabled)
        {
            c.rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, now * (12f + 18f * s));
            c.rays.color = new Color(rc.r, rc.g, rc.b, 0.25f + 0.35f * s);
        }
        c.icon.rectTransform.anchoredPosition = new Vector2(0f, MedY + Mathf.Sin(now * 3f + c.home.x) * (2f + 4f * s));
        c.medallion.rectTransform.anchoredPosition = new Vector2(0f, MedY);
        float gemPulse = 1f + 0.12f * Mathf.Sin(now * 6f) * s;
        c.gem.rectTransform.localScale = Vector3.one * gemPulse;

        // the shine sweeps across the selected card every couple of seconds
        float sweep = Mathf.Repeat(now * 0.55f + c.home.x * 0.001f, 1f);
        c.shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-CardW, CardW, sweep * 2.2f), 0f);
        c.shine.color = new Color(1f, 1f, 1f, 0.16f * s);

        if (c.offer.upgrade || c.offer.def.IsDuo || c.offer.rarity >= Rarity.Epic)
            c.footer.Color = new Color(c.footer.Color.r, c.footer.Color.g, c.footer.Color.b, 0.75f + 0.25f * pulse);

        // sparkles drifting off rare and better cards while selected
        if (s > 0.9f && c.offer.rarity >= Rarity.Rare && Random.value < 0.25f + 0.1f * (int)c.offer.rarity)
            Spark(c.root.anchoredPosition + cardsRoot.anchoredPosition + new Vector2(Random.Range(-CardW / 2f, CardW / 2f), Random.Range(-CardH / 2f, CardH / 2f)), Color.Lerp(rc, Color.white, 0.3f), new Vector2(Random.Range(-20f, 20f), Random.Range(40f, 120f)), 0.8f);
    }

    private void AnimateCommon()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        float open = Mathf.Clamp01((now - openedAt) / 0.25f);
        dim.color = new Color(0.04f, 0.01f, 0.07f, 0.72f * open);

        // the room takes the colour of the patron you're looking at
        if (cards.Count > 0 && selected < cards.Count)
        {
            Color target = BoonCatalog.Of(cards[selected].offer.def.patron).color;
            washColor = Color.Lerp(washColor, target, dt * 6f);
        }
        wash.color = new Color(washColor.r, washColor.g, washColor.b, 0.42f * open);
        flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, Mathf.MoveTowards(flash.color.a, 0f, dt * 1.6f));

        // header slam
        float hk = Mathf.Clamp01((now - openedAt) / 0.2f);
        header.Rect.localScale = Vector3.one * (hk < 1f ? Mathf.Lerp(2.2f, 1f, hk * hk) : 1f + 0.03f * Mathf.Sin(now * 4f));
        header.Color = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.3f), Mathf.Clamp01((now - openedAt) / 0.35f));
        subheader.Color = new Color(1f, 1f, 1f, Mathf.Clamp01((now - openedAt - 0.15f) / 0.3f));

        // dialog: typed out
        if (cards.Count > 0 && selected < cards.Count)
        {
            Card c = cards[Mathf.Clamp(selected, 0, cards.Count - 1)];
            PatronInfo p = BoonCatalog.Of(c.offer.def.patron);
            int chars = Mathf.Clamp((int)((now - quoteShownAt) * 70f), 0, quoteFull.Length);
            // the patron's voice: a soft blip every few letters while the line types out
            if (chars / 5 > voiceBlips && chars < quoteFull.Length && !picking)
            {
                voiceBlips = chars / 5;
                BoonArt blipArt = BoonArt.Get;
                if (blipArt != null && blipArt.cardHover != null && blipArt.cardHover.Length > 0)
                    PlaySfx(blipArt.cardHover[voiceBlips % blipArt.cardHover.Length], 0.12f, PatronVoice(c.offer.def.patron) * Random.Range(1.6f, 1.8f));
            }
            quote.SetText(Wrap(quoteFull.Substring(0, chars), 46));
            emblemPlate.color = p.color;
            float bob = Mathf.Sin(now * 2.5f) * 4f;
            emblem.rectTransform.anchoredPosition = new Vector2(0f, bob);
            dialog.localScale = Vector3.one * Mathf.Lerp(dialog.localScale.x, 1f, dt * 12f);
        }
        dialog.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01((now - openedAt - 0.1f) / 0.25f);

        hintShake = Mathf.Max(0f, hintShake - dt);
        hints.Rect.anchoredPosition = new Vector2(hintShake > 0f ? Mathf.Sin(now * 80f) * 10f * hintShake / 0.3f : 0f, 46f);

        // ambient motes rising
        if (Random.value < 0.5f)
        {
            Color mc = Color.Lerp(washColor, Color.white, Random.value * 0.4f);
            Spark(new Vector2(Random.Range(-960f, 960f), -560f), mc, new Vector2(Random.Range(-15f, 15f), Random.Range(60f, 160f)), Random.Range(2.5f, 5f), Random.Range(4f, 9f));
        }
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var part = particles[i];
            part.age += dt;
            if (part.img == null || part.age >= part.life)
            {
                if (part.img != null) Destroy(part.img.gameObject);
                particles.RemoveAt(i);
                continue;
            }
            part.vel *= 1f - 1.2f * dt * (part.life < 2f ? 1f : 0f);
            part.img.rectTransform.anchoredPosition += part.vel * dt;
            part.img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, part.age * part.spin);
            Color col = part.img.color;
            col.a = 1f - part.age / part.life;
            part.img.color = col;
            particles[i] = part;
        }
    }

    // ---------------------------------------------------------------- input
    private Vector2 MousePos
    {
        get
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null ? m.position.ReadValue() : Vector2.zero;
        }
    }

    private void HandleInput()
    {
        if (cards.Count == 0) return;
        int dir = GameInput.MoveX > 0f ? 1 : GameInput.MoveX < 0f ? -1 : 0;
        if (dir == 0) heldDir = 0;
        else if (dir != heldDir) { heldDir = dir; navTimer = 0.32f; Move(dir); }
        else
        {
            navTimer -= Time.unscaledDeltaTime;
            if (navTimer <= 0f) { navTimer = 0.14f; Move(dir); }
        }

        // mouse: hover selects, click takes. Only once the mouse has really been moved since the picker opened,
        // so a cursor resting over a card doesn't fight the pad / keys.
        Vector2 mouse = MousePos;
        bool mouseMoved = (mouse - lastMouse).sqrMagnitude > 9f;
        mouseTravel += (mouse - lastMouse).magnitude;
        lastMouse = mouse;
        if (Time.unscaledTime > mouseIdleUntil && mouseTravel > 40f && GameInput.Current == GameInput.Device.Keyboard && (mouseMoved || GameInput.MouseLeftDown))
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(cards[i].body, mouse, null)) continue;
                if (i != selected) { selected = i; OnSelectionChanged(); }
                if (GameInput.MouseLeftDown) { Finish(cards[i]); return; }
            }
        }

        if (GameInput.Down(GameInput.Act.Submit)) { Finish(cards[selected]); return; }
        if (GameInput.Down(GameInput.Act.Interact)) Reroll();
    }

    private void Move(int dir)
    {
        int next = Mathf.Clamp(selected + dir, 0, cards.Count - 1);
        if (next == selected) return;
        selected = next;
        OnSelectionChanged();
    }

    private void OnSelectionChanged()
    {
        BoonArt art = BoonArt.Get;
        if (art != null && art.cardHover != null && art.cardHover.Length > 0) PlaySfx(art.cardHover[Random.Range(0, art.cardHover.Length)], 0.7f, 0.92f + 0.08f * selected);
        else UISound.Play(UISound.Cue.Move);
        Card hovered = cards[selected];
        if (art != null && (hovered.offer.rarity >= Rarity.Epic || hovered.offer.def.IsDuo)) PlaySfx(art.coin, 0.18f, 1.3f);
        voiceBlips = 0;
        ShowQuote(cards[selected]);
        dialog.localScale = Vector3.one * 1.04f;
    }

    private void ShowQuote(Card c)
    {
        if (c == null) return;
        SetQuote(c, c.line);
    }

    private void SetQuote(Card c, string line)
    {
        BoonDef d = c.offer.def;
        PatronInfo p = BoonCatalog.Of(d.patron);
        if (d.IsDuo)
        {
            PatronInfo q = BoonCatalog.Of(d.partner.Value);
            patronName.SetText(p.name + " + " + q.name);
            patronTitle.SetText("DUO BOON");
        }
        else
        {
            patronName.SetText(p.name);
            patronTitle.SetText(p.title);
        }
        patronName.Color = p.color;
        patronTitle.Color = d.IsDuo ? BoonCatalog.RarityColors[(int)Rarity.Duo] : p.accent;
        emblem.sprite = BoonIcons.Emblem(d.patron);
        quoteFull = line ?? "";
        quoteShownAt = Time.unscaledTime;
        voiceBlips = 0;
        quote.SetText("");
    }

    private void RefreshHints()
    {
        hintVersion = GameInput.DeviceVersion;
        string reroll = Boons.Rerolls > 0 ? "{INTERACT} REROLL (HAIR GEL X" + Boons.Rerolls + ")" : "NO HAIR GEL TO REROLL";
        hints.SetText(GameInput.Format("{OK} TAKE IT       " + reroll + "       " + (GameInput.UsingGamepad ? "{MOVE}" : "< >") + " CHOOSE"));
    }

    // ---------------------------------------------------------------- particles
    private void Spark(Vector2 at, Color color, Vector2 velocity, float life, float size = 6f)
    {
        Image img = OverlayUI.MakeImage("Mote", particlesRoot, color, OverlayUI.WhiteSprite);
        img.rectTransform.sizeDelta = new Vector2(size, size);
        img.rectTransform.anchoredPosition = at;
        particles.Add((img, velocity, life, 0f, Random.Range(-90f, 90f)));
    }

    private void Burst(Vector2 at, Color color, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Spark(at + dir * Random.Range(0f, 40f), Color.Lerp(color, Color.white, Random.value * 0.35f), dir * Random.Range(speed * 0.3f, speed), Random.Range(0.4f, 0.9f), Random.Range(6f, 14f));
        }
    }

    // ---------------------------------------------------------------- audio (plays while the game is paused)
    // A few sources so pitched sounds (deal whooshes, hover steps, dialogue blips) don't retune each other
    private AudioSource[] sources;
    private int nextSource;

    private void PlaySfx(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;
        if (sources == null)
        {
            sources = new AudioSource[6];
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.ignoreListenerPause = true;
                s.priority = 20;
                sources[i] = s;
            }
            audioSource = sources[0];
        }
        float v = volume * AudioBoost * GameSettings.SfxVolume;
        if (!AudioGuard.Safe(clip, ref v, ref pitch)) return;
        AudioSource src = sources[nextSource];
        nextSource = (nextSource + 1) % sources.Length;
        src.pitch = pitch;
        src.PlayOneShot(clip, v);
    }

    private static AudioClip PatronSound(Patron p)
    {
        BoonArt art = BoonArt.Get;
        if (art == null) return null;
        switch (p)
        {
            case Patron.Narcissism: return art.narcissism;
            case Patron.Abyss: return art.abyss;
            case Patron.Lycanthropy: return art.lycanthropy;
            case Patron.Rot: return art.rot;
            case Patron.Guild: return art.guild;
            case Patron.Chef: return art.chef;
            case Patron.Smith: return art.smith;
            default: return art.sun;
        }
    }

    // Each patron talks at their own pitch (dialogue blips)
    private static float PatronVoice(Patron p)
    {
        switch (p)
        {
            case Patron.Narcissism: return 1.35f;
            case Patron.Abyss: return 0.6f;
            case Patron.Lycanthropy: return 0.75f;
            case Patron.Rot: return 1.1f;
            case Patron.Guild: return 1.5f;
            case Patron.Chef: return 1.25f;
            case Patron.Smith: return 0.7f;
            default: return 1.0f;
        }
    }

    // ---------------------------------------------------------------- building the UI
    private CanvasGroup rootGroup;

    private void Build()
    {
        root = OverlayUI.MakeRect("Boon Picker", OverlayUI.Root);
        Stretch(root);
        rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = false;

        dim = OverlayUI.MakeImage("Dim", root, Color.clear, OverlayUI.WhiteSprite);
        Stretch(dim.rectTransform);
        wash = OverlayUI.MakeImage("Wash", root, Color.clear, SoftEdgeSprite());
        Stretch(wash.rectTransform);
        particlesRoot = OverlayUI.MakeRect("Motes", root);
        Center(particlesRoot, Vector2.zero, Vector2.zero);

        header = Text(root, "LEVEL UP!", 7, new Color(1f, 0.82f, 0.3f));
        Center(header.Rect, new Vector2(0f, 470f));
        subheader = Text(root, "CHOOSE YOUR BOON", 3, Color.white);
        Center(subheader.Rect, new Vector2(0f, 412f));
        counter = Text(root, "", 2, new Color(1f, 0.85f, 0.95f, 0.8f));
        Center(counter.Rect, new Vector2(0f, 382f));

        // the patron's box
        dialog = OverlayUI.MakeRect("Dialog", root);
        Center(dialog, new Vector2(0f, 284f), new Vector2(1240f, 140f));
        dialog.gameObject.AddComponent<CanvasGroup>();
        Image box = OverlayUI.MakeImage("Box", dialog, new Color(1f, 1f, 1f, 0.95f), CardSprite());
        box.type = Image.Type.Sliced;
        box.pixelsPerUnitMultiplier = OverlayUI.SlicedMultiplier;
        Stretch(box.rectTransform);
        box.color = new Color(0.55f, 0.5f, 0.62f, 0.95f);
        emblemPlate = OverlayUI.MakeImage("Emblem Plate", dialog, Color.white, BoonIcons.Medallion);
        Center(emblemPlate.rectTransform, new Vector2(-540f, 0f), new Vector2(120f, 120f));
        emblem = OverlayUI.MakeImage("Emblem", emblemPlate.rectTransform, Color.white);
        emblem.preserveAspect = true;
        Center(emblem.rectTransform, Vector2.zero, new Vector2(80f, 80f));
        // name, title and the line, centred in the space right of the emblem
        patronName = Text(dialog, "", 4, Color.white);
        Center(patronName.Rect, new Vector2(70f, 42f));
        patronTitle = Text(dialog, "", 2, Color.white);
        Center(patronTitle.Rect, new Vector2(70f, 12f));
        quote = Text(dialog, "", 3, new Color(0.97f, 0.95f, 1f));
        Center(quote.Rect, new Vector2(70f, -32f));

        cardsRoot = OverlayUI.MakeRect("Cards", root);
        Center(cardsRoot, Vector2.zero, Vector2.zero);

        hints = Text(root, "", 3, new Color(0.95f, 0.9f, 1f, 0.9f));
        hints.Rect.anchorMin = hints.Rect.anchorMax = new Vector2(0.5f, 0f);
        hints.Rect.pivot = new Vector2(0.5f, 0.5f);
        hints.Rect.anchoredPosition = new Vector2(0f, 46f);

        flash = OverlayUI.MakeImage("Flash", root, Color.clear, OverlayUI.WhiteSprite);
        Stretch(flash.rectTransform);
        root.gameObject.SetActive(false);
    }

    private Card BuildCard(Boons.Offer offer)
    {
        BoonDef d = offer.def;
        PatronInfo p = BoonCatalog.Of(d.patron);
        Color rc = BoonCatalog.RarityColors[(int)offer.rarity];
        var c = new Card { offer = offer };
        c.root = OverlayUI.MakeRect("Card " + d.id, cardsRoot);
        Center(c.root, Vector2.zero, new Vector2(CardW, CardH));
        c.group = c.root.gameObject.AddComponent<CanvasGroup>();

        c.glow = OverlayUI.MakeImage("Glow", c.root, Color.clear, SoftGlowSprite());
        Center(c.glow.rectTransform, Vector2.zero, new Vector2(CardW + 120f, CardH + 120f));

        c.frame = OverlayUI.MakeImage("Frame", c.root, p.color, CardSprite());
        c.frame.type = Image.Type.Sliced;
        c.frame.pixelsPerUnitMultiplier = OverlayUI.SlicedMultiplier * 0.75f;
        Stretch(c.frame.rectTransform);
        c.body = c.frame.rectTransform;

        // shine, clipped to the card
        var clip = OverlayUI.MakeRect("Clip", c.root);
        Center(clip, Vector2.zero, new Vector2(CardW - 24f, CardH - 24f));
        clip.gameObject.AddComponent<RectMask2D>();
        c.shine = OverlayUI.MakeImage("Shine", clip, Color.clear, OverlayUI.WhiteSprite);
        Center(c.shine.rectTransform, Vector2.zero, new Vector2(70f, CardH * 1.6f));
        c.shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);

        // duo cards: second patron's colour stripe
        if (d.IsDuo)
        {
            Color q = BoonCatalog.Of(d.partner.Value).color;
            Image stripe = OverlayUI.MakeImage("Duo Stripe", c.root, q, OverlayUI.WhiteSprite);
            Center(stripe.rectTransform, new Vector2(0f, CardH / 2f - 16f), new Vector2(CardW - 40f, 6f));
            Image stripe2 = OverlayUI.MakeImage("Duo Stripe 2", c.root, q, OverlayUI.WhiteSprite);
            Center(stripe2.rectTransform, new Vector2(0f, -CardH / 2f + 16f), new Vector2(CardW - 40f, 6f));
        }

        c.patron = Text(c.root, d.IsDuo ? p.name + " + " + BoonCatalog.Of(d.partner.Value).name : p.name, 2, Color.Lerp(p.color, Color.white, 0.25f));
        Center(c.patron.Rect, new Vector2(0f, CardH / 2f - 34f));

        c.rays = OverlayUI.MakeImage("Rays", c.root, Color.clear, RaysSprite());
        Center(c.rays.rectTransform, new Vector2(0f, MedY), new Vector2(240f, 240f));
        c.rays.enabled = offer.rarity >= Rarity.Rare;

        c.medallion = OverlayUI.MakeImage("Medallion", c.root, p.color, BoonIcons.Medallion);
        Center(c.medallion.rectTransform, new Vector2(0f, MedY), new Vector2(160f, 160f));
        c.icon = OverlayUI.MakeImage("Icon", c.root, d.iconTint, BoonIcons.Get(d));
        c.icon.preserveAspect = true;
        Center(c.icon.rectTransform, new Vector2(0f, MedY), new Vector2(104f, 104f));

        int nameScale = d.name.Length > 18 ? 3 : 4;
        c.name = Text(c.root, d.name, nameScale, Color.white);
        Center(c.name.Rect, new Vector2(0f, NameY));

        c.gem = OverlayUI.MakeImage("Gem", c.root, rc, GemSprite());
        Center(c.gem.rectTransform, new Vector2(-150f, RowY), new Vector2(24f, 24f));
        c.rarity = Text(c.root, BoonCatalog.RarityNames[(int)offer.rarity], 2, rc, 0f);
        Anchor(c.rarity.Rect, new Vector2(0.5f, 0.5f), new Vector2(-130f, RowY));
        c.rarity.Rect.pivot = new Vector2(0f, 0.5f);
        c.slot = Text(c.root, BoonCatalog.SlotNames[(int)d.slot], 2, new Color(0.85f, 0.8f, 0.95f), 1f);
        Anchor(c.slot.Rect, new Vector2(0.5f, 0.5f), new Vector2(170f, RowY));
        c.slot.Rect.pivot = new Vector2(1f, 0.5f);

        Image line = OverlayUI.MakeImage("Line", c.root, new Color(p.color.r, p.color.g, p.color.b, 0.6f), OverlayUI.WhiteSprite);
        Center(line.rectTransform, new Vector2(0f, LineY), new Vector2(CardW - 80f, 3f));

        c.desc = Text(c.root, Wrap(GameInput.Format(d.Describe(offer.rarity)), 30), 2, new Color(0.95f, 0.93f, 1f));
        c.desc.Rect.anchorMin = c.desc.Rect.anchorMax = new Vector2(0.5f, 0.5f);
        c.desc.Rect.pivot = new Vector2(0.5f, 1f);
        c.desc.Rect.anchoredPosition = new Vector2(0f, DescY);

        string footer;
        Color footerColor;
        if (offer.upgrade) { footer = "UPGRADE: " + BoonCatalog.RarityNames[(int)offer.oldRarity] + " > " + BoonCatalog.RarityNames[(int)offer.rarity]; footerColor = new Color(0.5f, 1f, 0.55f); }
        else if (offer.replaces != null) { footer = "REPLACES " + offer.replaces.name; footerColor = new Color(1f, 0.55f, 0.5f); }
        else if (d.IsDuo) { footer = "TWO PATRONS, ONE BOON!"; footerColor = BoonCatalog.RarityColors[(int)Rarity.Duo]; }
        else if (d.legendaryOnly) { footer = "SIGNATURE MOVE!"; footerColor = BoonCatalog.RarityColors[(int)Rarity.Legendary]; }
        else { footer = "NEW!"; footerColor = new Color(1f, 0.85f, 0.4f); }
        if (footer.Length > 30) footer = footer.Substring(0, 30);
        c.footer = Text(c.root, footer, 2, footerColor);
        Center(c.footer.Rect, new Vector2(0f, -CardH / 2f + 44f));

        // which greeting this patron says for this card
        c.line = d.IsDuo ? d.duoLine : p.greet[Random.Range(0, p.greet.Length)];
        return c;
    }

    // ---------------------------------------------------------------- small helpers
    private static PixelText Text(Transform parent, string s, int scale, Color color, float pivotX = 0.5f)
    {
        PixelText t = PixelText.Create(parent, s, scale, color, pivotX);
        t.Rect.anchorMin = t.Rect.anchorMax = new Vector2(0.5f, 0.5f);
        return t;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform r, Vector2 pos, Vector2? size = null)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        if (size.HasValue) r.sizeDelta = size.Value;
    }

    private static void Anchor(RectTransform r, Vector2 anchor, Vector2 pos)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.anchoredPosition = pos;
    }

    // Word wrap for the pixel font (button icons count as several letters)
    public static string Wrap(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new System.Text.StringBuilder();
        int lineLen = 0;
        foreach (string word in text.Split(' '))
        {
            int len = PixelFont.CharLength(word);
            if (lineLen > 0 && lineLen + 1 + len > maxChars) { sb.Append('\n'); lineLen = 0; }
            else if (lineLen > 0) { sb.Append(' '); lineLen++; }
            sb.Append(word);
            lineLen += len;
        }
        return sb.ToString();
    }

    // Neutral 9-slice card (tinted per patron): dark outline, light border, inner line, dark fill
    private static Sprite cardSprite, raysSprite, glowSprite, edgeSprite, gemSprite;

    private static Sprite CardSprite()
    {
        if (cardSprite != null) return cardSprite;
        var tex = new Texture2D(12, 12, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "BoonCard" };
        Color32 outline = new Color32(0x1B, 0x08, 0x20, 0xFF), hi = new Color32(255, 255, 255, 255), border = new Color32(205, 205, 205, 255),
                fill = new Color32(58, 50, 66, 240), clear = new Color32(0, 0, 0, 0);
        var px = new Color32[144];
        for (int y = 0; y < 12; y++)
            for (int x = 0; x < 12; x++)
            {
                int top = 11 - y;
                int d = Mathf.Min(Mathf.Min(x, 11 - x), Mathf.Min(top, 11 - top));
                bool corner = (x == 0 || x == 11) && (top == 0 || top == 11);
                px[y * 12 + x] = corner ? clear : d == 0 ? outline : d == 1 ? ((top <= 1 || x <= 1) ? hi : border) : d == 2 ? outline : fill;
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        cardSprite = AIArt.Use("BoonPicker_Card9Slice", Sprite.Create(tex, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4)));
        return cardSprite;
    }

    private static Sprite RaysSprite()
    {
        if (raysSprite != null) return raysSprite;
        const int n = 96;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "BoonRays" };
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - c, dy = y - c, d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                float ang = Mathf.Atan2(dy, dx);
                float ray = Mathf.Abs(Mathf.Sin(ang * 6f));
                float a = d > 1f || d < 0.25f ? 0f : (ray > 0.75f ? 1f : 0f) * (1f - d);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Round(a * 4f) / 4f * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        raysSprite = AIArt.Use("BoonPicker_Rays", Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f)));
        return raysSprite;
    }

    private static Sprite SoftGlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 48;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "BoonGlow" };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / n * 2f - 1f), dy = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                float d = Mathf.Max(Mathf.Pow(dx, 4f) + Mathf.Pow(dy, 4f), 0f);
                float a = Mathf.Clamp01(1f - d) ;
                px[y * n + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        glowSprite = AIArt.Use("BoonPicker_Glow", Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f)));
        return glowSprite;
    }

    private static Sprite SoftEdgeSprite()
    {
        if (edgeSprite != null) return edgeSprite;
        const int w = 64, h = 36;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "BoonWash" };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx * 0.7f + dy * dy);
                px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((d - 0.35f) / 0.8f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        edgeSprite = AIArt.Use("BoonPicker_Edge", Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f)));
        return edgeSprite;
    }

    private static Sprite GemSprite()
    {
        if (gemSprite != null) return gemSprite;
        gemSprite = BoonFX.FromRows("BoonGem", new[] { "...k...", "..kwk..", ".kwaak.", "kwaaaak", ".kaaak.", "..kak..", "...k..." },
            ch => ch == 'k' ? new Color32(27, 8, 32, 255) : ch == 'w' ? new Color32(255, 255, 255, 255) : new Color32(230, 230, 230, 255), new Vector2(0.5f, 0.5f), 16f);
        return gemSprite;
    }
}
