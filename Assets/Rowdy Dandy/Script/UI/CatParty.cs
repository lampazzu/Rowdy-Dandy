using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// CAT PARTY: manage Rowdy's cats. Opens by itself when he finds a cat with a full party (or {INTERACT} on it), and
// from the pause menu. The game is paused while it's open.
//   {MOVE} pick a cat          {OK} pick it up and move it along the line (order = ring order, and who stays on death)
//   {INTERACT} make it LEADER  (the leader's power cools down much faster; Top Cat boon: more damage + a crown)
//   {ATTACK} x2 KICK it out    (it waits where Rowdy is standing, pick it up again any time)
//   {NOTES} Auto Pick Up on / off (off = cats only join with {INTERACT} on them)
//   new cat on the right: {OK} = TAKE it (full party: then choose who it replaces)
// Every cat has a few lines of its own; they talk when picked, kicked or crowned.
public class CatParty : MonoBehaviour
{
    private const float CardW = 168f, CardH = 268f, Gap = 18f, RowY = -40f;
    private const float AudioBoost = 2.2f;

    private static CatParty instance;
    public static bool IsOpen { get; private set; }
    private static int closedFrame = -10;
    public static bool BlocksPause => IsOpen || Time.frameCount <= closedFrame + 2;

    private enum Mode { Browse, Moving, ConfirmKick, ChooseSwap }
    private Mode mode;

    private class Card
    {
        public PetFollower pet;           // null = empty slot
        public bool candidate;
        public RectTransform root, body;
        public CanvasGroup group;
        public Image frame, glow, face, crown, shine;
        public PixelText name, power, cd, tag;
        public Vector2 home, pos;
        public float select, enterAt, rot, flyOut = -1f;
        public Vector2 flyVelocity;
        public float flySpin;
        public bool landed;
    }

    private RectTransform root, row, particles, bubble;
    private CanvasGroup rootGroup, bubbleGroup;
    private Image dim, wash, flash, bubbleBox;
    private PixelText header, subheader, hints, bubbleText, autoText;
    private readonly List<Card> cards = new List<Card>();
    private readonly List<Card> leaving = new List<Card>();
    private readonly List<(Image img, Vector2 vel, float life, float age, float spin)> bits = new List<(Image, Vector2, float, float, float)>();
    private int selected, heldDir, hintVersion = -1;
    private float navTimer, openedAt, kickArmedAt = -10f, shake;
    private PetFollower candidate;
    private string line = "", lineShown = "";
    private float lineAt, voiceAt = -10f;
    private int blips;
    private Color washColor = Color.clear;
    private bool pendingClose;
    private AudioSource[] sources;
    private int nextSource;

    // ---------------------------------------------------------------- what the cats say
    private static string[] Lines(PetFollower p, string when)
    {
        PetFollower.CatType t = p != null ? p.Type : PetFollower.CatType.Wig;
        switch (when)
        {
            case "kick":
                switch (t)
                {
                    case PetFollower.CatType.Wig: return new[] { "PLEASE DO NOT KICK ME!", "I WILL PEE IN YOUR BOOTS.", "WIG REMEMBERS. WIG ALWAYS REMEMBERS." };
                    case PetFollower.CatType.Samurai: return new[] { "A SAMURAI NEVER BEGS. ...PLEASE?", "YOU CUT ME DEEPER THAN ANY BLADE." };
                    case PetFollower.CatType.Paprika: return new[] { "YOU WILL MISS THE SPICE, BABY.", "FINE. BLAND LIFE. ENJOY." };
                    case PetFollower.CatType.Mushidon: return new[] { "I WILL SIT ON YOU. FOREVER.", "MUSHIDON IS TOO BIG TO KICK." };
                    case PetFollower.CatType.Peak: return new[] { "AND WHO PROTECTS YOU NOW?", "WALLS DO NOT GET KICKED, DANDY." };
                    case PetFollower.CatType.Lallo: return new[] { "I KNOW WHERE YOU SLEEP.", "HEE HEE. BAD IDEA." };
                    default: return new[] { "I WILL PULL MYSELF RIGHT BACK IN.", "YOU CANNOT ESCAPE MY GRAVITY." };
                }
            case "bye":
                return new[] { "FINE. WHATEVER. MEOW.", "BYE, LOSER.", "SEE YOU NEVER.", "I WAS GONNA LEAVE ANYWAY." };
            case "leader":
                switch (t)
                {
                    case PetFollower.CatType.Wig: return new[] { "FINALLY SOME RESPECT, BITCH." };
                    case PetFollower.CatType.Samurai: return new[] { "I ACCEPT THIS HONOR. SILENTLY." };
                    case PetFollower.CatType.Paprika: return new[] { "HOT HOT HOT! THE SPICE LEADS!" };
                    case PetFollower.CatType.Mushidon: return new[] { "BIG BOSS MUSHIDON. BIG. BOSS." };
                    case PetFollower.CatType.Peak: return new[] { "AS EXPECTED. I WAS ALWAYS IN CHARGE." };
                    case PetFollower.CatType.Lallo: return new[] { "THE ROT RISES. HEE HEE." };
                    default: return new[] { "EVERYTHING REVOLVES AROUND ME. LITERALLY." };
                }
            case "new":
                return new[] { "PICK ME! PICK ME! MEOW!", "I AM NEW. I AM SHINY. I AM HUNGRY.", "ROOM FOR ONE MORE, BOSS?" };
            case "move":
                return new[] { "WHEEE!", "PUT ME DOWN, BOI!", "WHERE ARE WE GOING?" };
            default:
                switch (t)
                {
                    case PetFollower.CatType.Wig: return new[] { "MEOW MEOW, BITCH.", "I HIT STUFF. SMALL STUFF. FAST.", "WIG IS MY NAME. HAIR IS MY GAME." };
                    case PetFollower.CatType.Samurai: return new[] { "IM FAST AS FUCK, BOI.", "ONE CUT. NO MORE.", "THE WEAK ONES ARE MINE." };
                    case PetFollower.CatType.Paprika: return new[] { "SPICY. VERY SPICY.", "TASTE THE PAPRIKA, LOSER.", "MY CLAWS ARE SEASONED." };
                    case PetFollower.CatType.Mushidon: return new[] { "STOMP. STOMP. NAP.", "I AM VERY HEAVY. IN A CUTE WAY.", "THE GROUND FEARS ME." };
                    case PetFollower.CatType.Peak: return new[] { "NOTHING GETS THROUGH ME.", "I AM THE WALL, BABY.", "HIT HIM? GO THROUGH ME FIRST." };
                    case PetFollower.CatType.Lallo: return new[] { "EVERYTHING DECAYS. EVEN YOU.", "HEE HEE. ROT.", "I SMELL A CHAIN REACTION." };
                    default: return new[] { "COME HERE. ALL OF YOU. CLOSER.", "SPIN TO WIN, BABY.", "YOU ARE IN MY ORBIT NOW." };
                }
        }
    }

    // ---------------------------------------------------------------- open / close
    public static void Open(PetFollower newcomer)
    {
        if (IsOpen || PauseMenu.IsPaused || BoonPicker.IsOpen || RowdyNotes.IsOpen || WorldMap.IsOpen || Tutorials.IsOpen) return;
        if (instance == null)
        {
            var go = new GameObject("CatParty (auto)");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CatParty>();
            SceneManager.sceneLoaded += (s, m) => { if (instance != null && IsOpen) instance.Close(); };
        }
        instance.DoOpen(newcomer);
    }

    private void DoOpen(PetFollower newcomer)
    {
        if (root == null) Build();
        candidate = newcomer != null && !newcomer.IsCollected ? newcomer : null;
        IsOpen = true;
        pendingClose = false;
        openedAt = Time.unscaledTime;
        mode = Mode.Browse;
        PauseMenu.SetExternalPause(true);
        root.gameObject.SetActive(true);
        rootGroup.alpha = 1f;
        Rebuild(true);
        selected = candidate != null ? cards.Count - 1 : 0;
        Say(SelectedPet(), candidate != null ? "new" : "pick");
        RefreshHints();
        UISound.Play(UISound.Cue.Open);
        BoonArt art = BoonArt.Get;
        if (art != null) { Play(art.open, 0.3f, 1.2f); Play(art.whoosh, 0.25f, 1.3f); }
        flash.color = new Color(1f, 0.8f, 0.95f, 0.3f);
        header.Rect.localScale = Vector3.one * 2f;
    }

    private void Close()
    {
        IsOpen = false;
        closedFrame = Time.frameCount;
        foreach (Card c in cards) if (c.root != null) Destroy(c.root.gameObject);
        foreach (Card c in leaving) if (c.root != null) Destroy(c.root.gameObject);
        cards.Clear();
        leaving.Clear();
        foreach (var b in bits) if (b.img != null) Destroy(b.img.gameObject);
        bits.Clear();
        if (root != null) root.gameObject.SetActive(false);
        PauseMenu.SetExternalPause(false);
    }

    // ---------------------------------------------------------------- cards
    private void Rebuild(bool dealIn)
    {
        var keep = new Dictionary<PetFollower, Card>();
        foreach (Card c in cards) if (c.pet != null && c.root != null) keep[c.pet] = c;
        var next = new List<Card>();
        List<PetFollower> party = CatRoster.Party;
        int capacity = CatRoster.Capacity;
        int slots = Mathf.Max(capacity, party.Count);
        for (int i = 0; i < slots; i++)
        {
            PetFollower p = i < party.Count ? party[i] : null;
            if (p != null && keep.TryGetValue(p, out Card existing)) { keep.Remove(p); existing.candidate = false; next.Add(existing); continue; }
            next.Add(MakeCard(p, false, dealIn ? 0.1f + i * 0.06f : 0f));
        }
        if (candidate != null && !candidate.IsCollected)
        {
            if (keep.TryGetValue(candidate, out Card c)) { keep.Remove(candidate); next.Add(c); }
            else next.Add(MakeCard(candidate, true, dealIn ? 0.25f + slots * 0.06f : 0f));
        }
        foreach (Card gone in keep.Values) Destroy(gone.root.gameObject);
        // empty slots are rebuilt every time (cheap)
        foreach (Card c in cards) if (c.pet == null && c.root != null && !next.Contains(c)) Destroy(c.root.gameObject);
        cards.Clear();
        cards.AddRange(next);

        // lay the row out: party slots, then a gap, then the newcomer
        float total = 0f;
        for (int i = 0; i < cards.Count; i++) total += CardW + (i > 0 ? Gap + (cards[i].candidate ? 60f : 0f) : 0f);
        float x = -total / 2f + CardW / 2f;
        for (int i = 0; i < cards.Count; i++)
        {
            if (i > 0) x += Gap + (cards[i].candidate ? 60f : 0f);
            cards[i].home = new Vector2(x, RowY);
            if (dealIn) cards[i].pos = cards[i].home + new Vector2(0f, -800f);
            x += CardW;
        }
        // a big party: shrink the whole row so it fits the screen
        rowScale = Mathf.Min(1f, 1760f / Mathf.Max(1f, total));
        row.localScale = Vector3.one * rowScale;
        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, cards.Count - 1));
        RefreshTexts();
    }

    private float rowScale = 1f;

    private Card MakeCard(PetFollower p, bool isCandidate, float delay)
    {
        var c = new Card { pet = p, candidate = isCandidate };
        c.root = OverlayUI.MakeRect(p != null ? "Card " + p.CatName : "Empty Slot", row);
        Center(c.root, Vector2.zero, new Vector2(CardW, CardH));
        c.group = c.root.gameObject.AddComponent<CanvasGroup>();
        c.enterAt = Time.unscaledTime + delay;
        c.pos = new Vector2(0f, -800f);

        c.glow = OverlayUI.MakeImage("Glow", c.root, Color.clear, GlowSprite());
        Center(c.glow.rectTransform, Vector2.zero, new Vector2(CardW + 80f, CardH + 80f));
        c.frame = OverlayUI.MakeImage("Frame", c.root, Color.white, CardSprite());
        c.frame.type = Image.Type.Sliced;
        c.frame.pixelsPerUnitMultiplier = OverlayUI.SlicedMultiplier * 0.75f;
        Stretch(c.frame.rectTransform);
        c.body = c.frame.rectTransform;

        if (p == null)
        {
            c.frame.color = new Color(0.35f, 0.3f, 0.42f, 0.55f);
            c.name = Text(c.root, "+", 6, new Color(1f, 1f, 1f, 0.35f));
            Center(c.name.Rect, new Vector2(0f, 20f));
            c.power = Text(c.root, "EMPTY", 2, new Color(1f, 1f, 1f, 0.35f));
            Center(c.power.Rect, new Vector2(0f, -60f));
            return c;
        }

        Color tint = p.Tint;
        c.frame.color = Color.Lerp(new Color(0.42f, 0.36f, 0.52f), tint, 0.35f);
        var clip = OverlayUI.MakeRect("Clip", c.root);
        Center(clip, Vector2.zero, new Vector2(CardW - 20f, CardH - 20f));
        clip.gameObject.AddComponent<RectMask2D>();
        c.shine = OverlayUI.MakeImage("Shine", clip, Color.clear, OverlayUI.WhiteSprite);
        Center(c.shine.rectTransform, Vector2.zero, new Vector2(46f, CardH * 1.6f));
        c.shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);

        Image plate = OverlayUI.MakeImage("Plate", c.root, new Color(0.1f, 0.05f, 0.14f, 0.85f), BoonIcons.Medallion);
        Center(plate.rectTransform, new Vector2(0f, 52f), new Vector2(124f, 124f));
        c.face = OverlayUI.MakeImage("Face", c.root, tint, p.Portrait);
        c.face.preserveAspect = true;
        Center(c.face.rectTransform, new Vector2(0f, 52f), new Vector2(96f, 96f));
        c.crown = OverlayUI.MakeImage("Crown", c.root, Color.white, MoreSprites.Crown);
        c.crown.preserveAspect = true;
        Center(c.crown.rectTransform, new Vector2(0f, CardH / 2f + 8f), new Vector2(54f, 30f));
        c.crown.enabled = CatRoster.IsLeader(p);

        c.name = Text(c.root, p.CatName, p.CatName.Length > 8 ? 2 : 3, Color.white);
        Center(c.name.Rect, new Vector2(0f, -36f));
        c.power = Text(c.root, p.PowerName, 2, Color.Lerp(tint, Color.white, 0.4f));
        Center(c.power.Rect, new Vector2(0f, -68f));
        c.cd = Text(c.root, "", 2, new Color(1f, 1f, 1f, 0.7f));
        Center(c.cd.Rect, new Vector2(0f, -94f));
        c.tag = Text(c.root, isCandidate ? "NEW!" : "", 3, new Color(1f, 0.85f, 0.3f));
        Center(c.tag.Rect, new Vector2(0f, -CardH / 2f - 26f));
        return c;
    }

    private void RefreshTexts()
    {
        int n = CatRoster.CatCount;
        PetFollower leader = CatRoster.Leader;
        subheader.SetText(n + " / " + CatRoster.Capacity + " CATS     LEADER: " + (leader != null ? leader.CatName : "NONE"));
        foreach (Card c in cards)
        {
            if (c.pet == null) continue;
            if (c.cd != null) c.cd.SetText("COOLDOWN " + Mathf.RoundToInt(c.pet.CooldownSeconds) + "S");
            if (c.crown != null) c.crown.enabled = CatRoster.IsLeader(c.pet);
            if (c.tag != null && !c.candidate) c.tag.SetText(CatRoster.IsLeader(c.pet) ? "LEADER" : "");
        }
        autoText.SetText(GameInput.Format("{NOTES} AUTO PICK UP: " + (CatRoster.AutoPickup ? "ON" : "OFF")));
        autoText.Color = CatRoster.AutoPickup ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.55f, 0.5f);
    }

    private void RefreshHints()
    {
        hintVersion = GameInput.DeviceVersion;
        Card c = Selected;
        string s;
        if (mode == Mode.Moving) s = "{MOVE} MOVE IT     {OK} PUT DOWN";
        else if (mode == Mode.ChooseSwap) s = "{MOVE} WHO LEAVES?     {OK} SWAP     {BACK} NEVER MIND";
        else if (mode == Mode.ConfirmKick) s = "{ATTACK} KICK FOR REAL     {MOVE} MERCY";
        else if (c != null && c.candidate) s = "{MOVE} SELECT     {OK} " + (CatRoster.HasRoom ? "TAKE" : "TAKE (SWAP)") + "     {BACK} CLOSE";
        else if (c != null && c.pet != null) s = "{MOVE} SELECT     {OK} MOVE     {INTERACT} LEADER     {ATTACK} KICK     {BACK} CLOSE";
        else s = "{MOVE} SELECT     {BACK} CLOSE";
        hints.SetText(GameInput.Format(s));
        RefreshTexts();
    }

    private Card Selected => selected >= 0 && selected < cards.Count ? cards[selected] : null;
    private PetFollower SelectedPet() => Selected != null ? Selected.pet : null;

    // ---------------------------------------------------------------- per frame
    private void Update()
    {
        if (!IsOpen) return;
        if (hintVersion != GameInput.DeviceVersion) RefreshHints();
        if (Time.unscaledTime - openedAt > 0.3f && !pendingClose) HandleInput();
        Animate();
    }

    private void LateUpdate()
    {
        if (pendingClose) { pendingClose = false; Close(); }
    }

    private void HandleInput()
    {
        bool back = GameInput.Down(GameInput.Act.Back) || GameInput.Down(GameInput.Act.Pause);
        if (back)
        {
            if (mode == Mode.ChooseSwap || mode == Mode.ConfirmKick || mode == Mode.Moving)
            {
                if (mode == Mode.ChooseSwap) SelectCard(cards.Count - 1);
                mode = Mode.Browse;
                UISound.Play(UISound.Cue.Back);
                RefreshHints();
                return;
            }
            UISound.Play(UISound.Cue.Back);
            pendingClose = true;
            return;
        }

        int dir = GameInput.MoveX > 0f ? 1 : GameInput.MoveX < 0f ? -1 : 0;
        if (Repeat(dir))
        {
            if (mode == Mode.Moving) MoveSelected(dir);
            else
            {
                if (mode == Mode.ConfirmKick) { mode = Mode.Browse; Say(SelectedPet(), "pick"); }
                int limit = mode == Mode.ChooseSwap ? CatRoster.CatCount - 1 : cards.Count - 1;
                int next = Mathf.Clamp(selected + dir, 0, Mathf.Max(0, limit));
                if (next != selected) SelectCard(next);
                else { shake = 0.15f; UISound.Play(UISound.Cue.Locked); }
            }
        }

        Card c = Selected;
        if (GameInput.Down(GameInput.Act.Submit) && c != null) Submit(c);
        else if (GameInput.Down(GameInput.Act.Interact) && c != null && c.pet != null && !c.candidate && mode == Mode.Browse) Crown(c);
        else if (GameInput.Down(GameInput.Act.Attack) && c != null && c.pet != null && !c.candidate && (mode == Mode.Browse || mode == Mode.ConfirmKick)) KickPressed(c);
        else if (GameInput.Down(GameInput.Act.Notes) && mode == Mode.Browse)
        {
            CatRoster.AutoPickup = !CatRoster.AutoPickup;
            UISound.Play(CatRoster.AutoPickup ? UISound.Cue.Unlock : UISound.Cue.Change);
            Burst(autoText.Rect.anchoredPosition, CatRoster.AutoPickup ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.55f, 0.5f), 14, 380f);
            RefreshTexts();
        }
    }

    private bool Repeat(int dir)
    {
        if (dir == 0) { heldDir = 0; return false; }
        if (dir != heldDir) { heldDir = dir; navTimer = 0.32f; return true; }
        navTimer -= Time.unscaledDeltaTime;
        if (navTimer <= 0f) { navTimer = 0.13f; return true; }
        return false;
    }

    private void SelectCard(int index)
    {
        selected = index;
        BoonArt art = BoonArt.Get;
        if (art != null && art.cardHover != null && art.cardHover.Length > 0) Play(art.cardHover[Random.Range(0, art.cardHover.Length)], 0.55f, 0.9f + 0.05f * index);
        else UISound.Play(UISound.Cue.Move);
        Card c = Selected;
        if (mode == Mode.ChooseSwap) Say(SelectedPet(), "kick");
        else Say(SelectedPet(), c != null && c.candidate ? "new" : "pick");
        if (c != null && c.pet != null && Time.unscaledTime - voiceAt > 0.7f) { voiceAt = Time.unscaledTime; c.pet.SpeakLine(); }
        RefreshHints();
    }

    private void Submit(Card c)
    {
        if (mode == Mode.Moving)
        {
            mode = Mode.Browse;
            UISound.Play(UISound.Cue.Confirm);
            Play(BoonArt.Get != null ? BoonArt.Get.plop : null, 0.4f, 1.3f);
            Burst(c.pos, c.pet != null ? c.pet.Tint : Color.white, 12, 300f);
            Say(c.pet, "pick");
            RefreshHints();
            return;
        }
        if (mode == Mode.ChooseSwap)
        {
            if (c.pet == null || c.candidate || candidate == null) return;
            PetFollower newcomer = candidate;
            Card incoming = cards.Find(k => k.candidate);
            FlyOut(c, "bye");
            CatRoster.Kick(c.pet, KickSpot());
            newcomer.Collect(BoonRunner.Rowdy, true);
            CatRoster.Reindex();
            candidate = null;
            mode = Mode.Browse;
            if (incoming != null) { incoming.candidate = false; if (incoming.tag != null) incoming.tag.SetText(""); }
            Rebuild(false);
            selected = Mathf.Max(0, cards.FindIndex(k => k.pet == newcomer));
            Celebrate(Selected, "new");
            return;
        }
        if (c.candidate)
        {
            if (candidate == null) return;
            if (CatRoster.HasRoom)
            {
                PetFollower newcomer = candidate;
                newcomer.Collect(BoonRunner.Rowdy, false);
                CatRoster.Reindex();
                candidate = null;
                c.candidate = false;
                if (c.tag != null) c.tag.SetText("");
                Rebuild(false);
                selected = Mathf.Max(0, cards.FindIndex(k => k.pet == newcomer));
                Celebrate(Selected, "new");
            }
            else
            {
                mode = Mode.ChooseSwap;
                UISound.Play(UISound.Cue.Change);
                Play(BoonArt.Get != null ? BoonArt.Get.swap : null, 0.5f, 1f);
                SelectCard(Mathf.Clamp(selected, 0, CatRoster.CatCount - 1));
            }
            return;
        }
        if (c.pet == null) return;
        mode = Mode.Moving;
        UISound.Play(UISound.Cue.Change);
        Play(BoonArt.Get != null ? BoonArt.Get.whoosh : null, 0.25f, 1.6f);
        Say(c.pet, "move");
        RefreshHints();
    }

    private void MoveSelected(int dir)
    {
        Card c = Selected;
        if (c == null || c.pet == null) return;
        int target = selected + dir;
        if (target < 0 || target >= CatRoster.CatCount) { shake = 0.15f; UISound.Play(UISound.Cue.Locked); return; }
        CatRoster.Move(c.pet, dir);
        (cards[selected], cards[target]) = (cards[target], cards[selected]);
        (cards[selected].home, cards[target].home) = (cards[target].home, cards[selected].home);
        selected = target;
        Play(BoonArt.Get != null ? BoonArt.Get.swap : null, 0.35f, 1.3f + 0.05f * target);
        UISound.Play(UISound.Cue.Move);
    }

    private void Crown(Card c)
    {
        if (CatRoster.IsLeader(c.pet))
        {
            CatRoster.SetLeader(null);
            UISound.Play(UISound.Cue.Back);
            Say(c.pet, "pick");
            RefreshTexts();
            return;
        }
        CatRoster.SetLeader(c.pet);
        foreach (Card k in cards) if (k.crown != null) { k.crown.enabled = k == c; }
        crownDrop = 0f;
        crownCard = c;
        Celebrate(c, "leader");
        BoonArt art = BoonArt.Get;
        if (art != null) Play(art.coin, 0.5f, 1.2f);
        UISound.Play(UISound.Cue.Unlock);
        RefreshTexts();
    }

    private void KickPressed(Card c)
    {
        if (mode != Mode.ConfirmKick || Time.unscaledTime - kickArmedAt > 2.5f)
        {
            mode = Mode.ConfirmKick;
            kickArmedAt = Time.unscaledTime;
            shake = 0.3f;
            UISound.Play(UISound.Cue.Locked);
            Say(c.pet, "kick");
            c.pet.SpeakLine();
            RefreshHints();
            return;
        }
        // KICK
        FlyOut(c, "bye");
        CatRoster.Kick(c.pet, KickSpot());
        mode = Mode.Browse;
        Rebuild(false);
        selected = Mathf.Clamp(selected, 0, cards.Count - 1);
        RefreshHints();
    }

    private static Vector3 KickSpot()
    {
        Vector3 at = BoonRunner.Rowdy != null ? BoonRunner.RowdyCenter : Vector3.zero;
        return at + new Vector3(Random.Range(-0.9f, 0.9f), 0.2f, 0f);
    }

    // the card spins away off screen, the cat says one last thing
    private void FlyOut(Card c, string what)
    {
        cards.Remove(c);
        leaving.Add(c);
        c.flyOut = Time.unscaledTime;
        c.flyVelocity = new Vector2(Random.Range(-500f, 500f), 1500f);
        c.flySpin = Random.Range(-720f, 720f);
        if (c.tag != null) { c.tag.SetText("BYE!"); c.tag.Color = new Color(1f, 0.5f, 0.5f); }
        Burst(c.pos, c.pet != null ? c.pet.Tint : Color.white, 24, 700f);
        BoonArt art = BoonArt.Get;
        if (art != null) { Play(art.whoosh, 0.45f, 1.2f); Play(art.cancel, 0.5f, 0.9f); }
        if (c.pet != null) c.pet.SpeakLine();
        string[] l = Lines(c.pet, what);
        Say(null, null, l[Random.Range(0, l.Length)]);
        shake = 0.35f;
        flash.color = new Color(1f, 0.4f, 0.5f, 0.25f);
    }

    private void Celebrate(Card c, string what)
    {
        if (c == null) return;
        BoonArt art = BoonArt.Get;
        if (art != null) { Play(art.sparkle, 0.45f, 1.15f); Play(art.rarity != null && art.rarity.Length > 3 ? art.rarity[3] : null, 0.4f, 1.2f); }
        Burst(c.pos, Color.Lerp(c.pet != null ? c.pet.Tint : Color.white, Color.white, 0.3f), 34, 800f);
        Burst(c.pos, new Color(1f, 0.85f, 0.3f), 16, 500f);
        flash.color = new Color(1f, 0.9f, 0.6f, 0.3f);
        c.select = 1.4f;
        Say(c.pet, what);
        if (c.pet != null) c.pet.SpeakLine();
        RefreshHints();
    }

    private void Say(PetFollower p, string when, string exact = null)
    {
        if (exact == null)
        {
            if (when == null) return;
            string[] l = Lines(p, when);
            exact = l[Random.Range(0, l.Length)];
        }
        line = exact;
        lineAt = Time.unscaledTime;
        blips = 0;
        bubble.localScale = Vector3.one * 0.8f;
    }

    // ---------------------------------------------------------------- animation
    private Card crownCard;
    private float crownDrop = 9f;

    private void Animate()
    {
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        float open = Mathf.Clamp01((now - openedAt) / 0.2f);
        dim.color = new Color(0.04f, 0.01f, 0.07f, 0.78f * open);
        Card sel = Selected;
        Color target = sel != null && sel.pet != null ? sel.pet.Tint : new Color(0.8f, 0.6f, 1f);
        washColor = Color.Lerp(washColor, target, dt * 6f);
        wash.color = new Color(washColor.r, washColor.g, washColor.b, 0.35f * open);
        flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, Mathf.MoveTowards(flash.color.a, 0f, dt * 1.5f));
        header.Rect.localScale = Vector3.one * Mathf.Lerp(header.Rect.localScale.x, 1f + 0.025f * Mathf.Sin(now * 4f), dt * 14f);
        shake = Mathf.Max(0f, shake - dt);
        row.anchoredPosition = new Vector2(shake > 0f ? Mathf.Sin(now * 90f) * 14f * shake : 0f, 0f);

        for (int i = 0; i < cards.Count; i++)
        {
            Card c = cards[i];
            float since = now - c.enterAt;
            if (since < 0f) { c.group.alpha = 0f; continue; }
            if (!c.landed && since > 0.28f)
            {
                c.landed = true;
                Play(BoonArt.Get != null ? BoonArt.Get.plop : null, 0.25f, 1.2f + 0.06f * i);
                if (c.candidate) Burst(c.home, new Color(1f, 0.85f, 0.3f), 18, 500f);
            }
            bool isSel = i == selected;
            float s0 = isSel ? 1f : 0f;
            c.select = Mathf.MoveTowards(c.select, s0, dt * 6f);
            float s = Mathf.Clamp01(c.select);
            float s2 = s * s * (3f - 2f * s);
            float lift = mode == Mode.Moving && isSel ? 70f : 26f * s2;
            float wiggle = mode == Mode.Moving && isSel ? Mathf.Sin(now * 30f) * 4f : Mathf.Sin(now * 3f + i) * 2f * s2;
            if (mode == Mode.ConfirmKick && isSel) wiggle = Mathf.Sin(now * 60f) * 6f;
            if (mode == Mode.ChooseSwap && isSel) wiggle = Mathf.Sin(now * 45f) * 4f;
            Vector2 goal = c.home + new Vector2(0f, lift + (c.candidate ? 10f * Mathf.Sin(now * 2.5f) : 0f));
            c.pos = Vector2.Lerp(c.pos, goal, dt * (since < 0.4f ? 9f : 14f));
            c.root.anchoredPosition = c.pos;
            c.root.localRotation = Quaternion.Euler(0f, 0f, wiggle);
            float pop = c.select > 1f ? 1f + (c.select - 1f) * 0.5f : 1f;
            c.root.localScale = Vector3.one * (1f + 0.08f * s2) * pop;
            c.group.alpha = Mathf.Clamp01(since / 0.15f) * (mode == Mode.ChooseSwap && c.candidate ? 0.55f : Mathf.Lerp(0.75f, 1f, s2));

            Color glowC = c.pet != null ? c.pet.Tint : Color.white;
            if (mode == Mode.ConfirmKick && isSel) glowC = new Color(1f, 0.25f, 0.3f);
            c.glow.color = new Color(glowC.r, glowC.g, glowC.b, (0.1f + 0.5f * s2) * (0.75f + 0.25f * Mathf.Sin(now * 6f)));
            if (c.shine != null)
            {
                float sweep = Mathf.Repeat(now * 0.6f + i * 0.17f, 1f);
                c.shine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-CardW, CardW, sweep * 2.2f), 0f);
                c.shine.color = new Color(1f, 1f, 1f, 0.18f * s2);
            }
            if (c.face != null) c.face.rectTransform.anchoredPosition = new Vector2(0f, 52f + Mathf.Round(Mathf.Sin(now * 5f + i) * (2f + 3f * s2)));
            if (c.crown != null && c.crown.enabled)
            {
                float drop = c == crownCard ? Mathf.Clamp01(crownDrop) : 1f;
                float bounce = drop < 1f ? (1f - drop) * 260f - Mathf.Sin(drop * Mathf.PI) * 30f : Mathf.Abs(Mathf.Sin(now * 3f)) * 4f;
                c.crown.rectTransform.anchoredPosition = new Vector2(0f, CardH / 2f + 8f + bounce);
                c.crown.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(now * 2f) * 6f);
            }
            if (isSel && s > 0.9f && c.pet != null && Random.value < 0.25f)
                Spark(c.pos + new Vector2(Random.Range(-CardW / 2f, CardW / 2f), Random.Range(-CardH / 2f, CardH / 2f)), Color.Lerp(c.pet.Tint, Color.white, 0.4f), new Vector2(Random.Range(-20f, 20f), Random.Range(40f, 120f)), 0.7f, 6f);
        }
        if (crownDrop < 1f)
        {
            crownDrop += dt / 0.35f;
            if (crownDrop >= 1f && crownCard != null) { Burst(crownCard.pos + new Vector2(0f, CardH / 2f), new Color(1f, 0.85f, 0.3f), 22, 500f); Play(BoonArt.Get != null ? BoonArt.Get.clang : null, 0.3f, 1.8f); }
        }

        // kicked cards fly away
        for (int i = leaving.Count - 1; i >= 0; i--)
        {
            Card c = leaving[i];
            float a = now - c.flyOut;
            c.flyVelocity += new Vector2(0f, -2600f) * dt;
            c.pos += c.flyVelocity * dt;
            c.root.anchoredPosition = c.pos;
            c.root.localRotation = Quaternion.Euler(0f, 0f, c.root.localEulerAngles.z + c.flySpin * dt);
            c.group.alpha = Mathf.Clamp01(1.2f - a);
            if (a > 1.3f) { Destroy(c.root.gameObject); leaving.RemoveAt(i); }
        }

        // the speech bubble over the selected card, typed out with little blips
        if (sel != null)
        {
            Vector2 at = sel.pos * rowScale + new Vector2(0f, (CardH / 2f) * rowScale + 92f);
            bubble.anchoredPosition = Vector2.Lerp(bubble.anchoredPosition, at, dt * 16f);
        }
        bubble.localScale = Vector3.one * Mathf.Lerp(bubble.localScale.x, 1f, dt * 14f);
        int chars = Mathf.Clamp((int)((now - lineAt) * 55f), 0, line.Length);
        string shown = line.Substring(0, chars);
        if (shown != lineShown)
        {
            lineShown = shown;
            bubbleText.SetText(BoonPicker.Wrap(shown, 24));
            RectTransform r = bubbleText.Rect;
            bubbleBox.rectTransform.sizeDelta = new Vector2(Mathf.Max(160f, r.sizeDelta.x + 44f), Mathf.Max(56f, r.sizeDelta.y + 30f));
            if (chars / 3 > blips && chars < line.Length)
            {
                blips = chars / 3;
                BoonArt art = BoonArt.Get;
                PetFollower talker = SelectedPet();
                float pitch = talker == null ? 1.6f : 1.4f + 0.1f * (int)talker.Type;
                if (art != null && art.cardHover != null && art.cardHover.Length > 0) Play(art.cardHover[blips % art.cardHover.Length], 0.12f, pitch * Random.Range(0.95f, 1.1f));
            }
        }
        bubbleGroup.alpha = string.IsNullOrEmpty(line) ? 0f : 1f;

        // ambient motes in the selected cat's colour
        if (Random.value < 0.45f)
            Spark(new Vector2(Random.Range(-960f, 960f), -560f), Color.Lerp(washColor, Color.white, Random.value * 0.4f), new Vector2(Random.Range(-15f, 15f), Random.Range(60f, 160f)), Random.Range(2.5f, 5f), Random.Range(4f, 9f));
        for (int i = bits.Count - 1; i >= 0; i--)
        {
            var b = bits[i];
            b.age += dt;
            if (b.img == null || b.age >= b.life) { if (b.img != null) Destroy(b.img.gameObject); bits.RemoveAt(i); continue; }
            b.vel *= 1f - 1.2f * dt * (b.life < 2f ? 1f : 0f);
            b.img.rectTransform.anchoredPosition += b.vel * dt;
            b.img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, b.age * b.spin);
            Color col = b.img.color;
            col.a = 1f - b.age / b.life;
            b.img.color = col;
            bits[i] = b;
        }
    }

    // ---------------------------------------------------------------- particles + sound
    private void Spark(Vector2 at, Color color, Vector2 velocity, float life, float size)
    {
        Image img = OverlayUI.MakeImage("Mote", particles, color, OverlayUI.WhiteSprite);
        img.rectTransform.sizeDelta = new Vector2(size, size);
        img.rectTransform.anchoredPosition = at;
        bits.Add((img, velocity, life, 0f, Random.Range(-90f, 90f)));
    }

    private void Burst(Vector2 at, Color color, int count, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 d = Random.insideUnitCircle.normalized;
            Spark(at + d * Random.Range(0f, 30f), Color.Lerp(color, Color.white, Random.value * 0.35f), d * Random.Range(speed * 0.3f, speed), Random.Range(0.4f, 0.9f), Random.Range(6f, 13f));
        }
    }

    private void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;
        if (sources == null)
        {
            sources = new AudioSource[5];
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f; s.ignoreListenerPause = true; s.priority = 20;
                sources[i] = s;
            }
        }
        float v = volume * AudioBoost * GameSettings.SfxVolume;
        if (!AudioGuard.Safe(clip, ref v, ref pitch)) return;
        AudioSource src = sources[nextSource];
        nextSource = (nextSource + 1) % sources.Length;
        src.pitch = pitch;
        src.PlayOneShot(clip, v);
    }

    // ---------------------------------------------------------------- building
    private void Build()
    {
        root = OverlayUI.MakeRect("Cat Party", OverlayUI.Root);
        Stretch(root);
        rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = false;
        dim = OverlayUI.MakeImage("Dim", root, Color.clear, OverlayUI.WhiteSprite);
        Stretch(dim.rectTransform);
        wash = OverlayUI.MakeImage("Wash", root, Color.clear, WashSprite());
        Stretch(wash.rectTransform);
        particles = OverlayUI.MakeRect("Motes", root);
        Center(particles, Vector2.zero, Vector2.zero);

        header = Text(root, "CAT PARTY", 7, new Color(1f, 0.82f, 0.3f));
        Center(header.Rect, new Vector2(0f, 440f));
        subheader = Text(root, "", 3, Color.white);
        Center(subheader.Rect, new Vector2(0f, 384f));

        row = OverlayUI.MakeRect("Row", root);
        Center(row, Vector2.zero, Vector2.zero);

        bubble = OverlayUI.MakeRect("Bubble", root);
        Center(bubble, new Vector2(0f, 200f), Vector2.zero);
        bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
        bubbleBox = OverlayUI.MakeImage("Box", bubble, new Color(1f, 0.97f, 1f, 0.97f), BubbleSprite());
        bubbleBox.type = Image.Type.Sliced;
        bubbleBox.pixelsPerUnitMultiplier = OverlayUI.SlicedMultiplier * 0.75f;
        Center(bubbleBox.rectTransform, Vector2.zero, new Vector2(200f, 60f));
        Image tail = OverlayUI.MakeImage("Tail", bubble, new Color(1f, 0.97f, 1f, 0.97f), TailSprite());
        Center(tail.rectTransform, new Vector2(0f, -40f), new Vector2(24f, 18f));
        bubbleText = Text(bubble, "", 2, new Color(0.15f, 0.06f, 0.2f));
        Center(bubbleText.Rect, Vector2.zero);

        autoText = Text(root, "", 3, Color.white);
        Center(autoText.Rect, new Vector2(0f, -330f));
        hints = Text(root, "", 3, new Color(0.95f, 0.9f, 1f, 0.9f));
        hints.Rect.anchorMin = hints.Rect.anchorMax = new Vector2(0.5f, 0f);
        hints.Rect.anchoredPosition = new Vector2(0f, 46f);

        flash = OverlayUI.MakeImage("Flash", root, Color.clear, OverlayUI.WhiteSprite);
        Stretch(flash.rectTransform);
        root.gameObject.SetActive(false);
    }

    private static PixelText Text(Transform parent, string s, int scale, Color color)
    {
        PixelText t = PixelText.Create(parent, s, scale, color, 0.5f);
        t.Rect.anchorMin = t.Rect.anchorMax = new Vector2(0.5f, 0.5f);
        return t;
    }

    private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    private static void Center(RectTransform r, Vector2 pos, Vector2? size = null)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        if (size.HasValue) r.sizeDelta = size.Value;
    }

    private static Sprite cardSprite, glowSprite, washSprite, bubbleSprite, tailSprite;

    private static Sprite CardSprite()
    {
        if (cardSprite != null) return cardSprite;
        var tex = new Texture2D(12, 12, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "CatCard" };
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
        cardSprite = Sprite.Create(tex, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        return cardSprite;
    }

    private static Sprite BubbleSprite()
    {
        if (bubbleSprite != null) return bubbleSprite;
        var tex = new Texture2D(10, 10, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "CatBubble" };
        Color32 ink = new Color32(0x1B, 0x08, 0x20, 0xFF), fill = new Color32(255, 255, 255, 255), clear = new Color32(0, 0, 0, 0);
        var px = new Color32[100];
        for (int y = 0; y < 10; y++)
            for (int x = 0; x < 10; x++)
            {
                int d = Mathf.Min(Mathf.Min(x, 9 - x), Mathf.Min(y, 9 - y));
                bool corner = (x <= 1 || x >= 8) && (y <= 1 || y >= 8) && d == 0 || (x == 0 || x == 9) && (y == 1 || y == 8) || (y == 0 || y == 9) && (x == 1 || x == 8);
                px[y * 10 + x] = corner ? clear : d == 0 ? ink : fill;
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        bubbleSprite = Sprite.Create(tex, new Rect(0, 0, 10, 10), new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
        return bubbleSprite;
    }

    private static Sprite TailSprite()
    {
        if (tailSprite != null) return tailSprite;
        tailSprite = BoonFX.FromRows("CatBubbleTail", new[] { "kwwwwwwk", ".kwwwwk.", "..kwwk..", "...kk..." },
            ch => ch == 'k' ? new Color32(0x1B, 0x08, 0x20, 0xFF) : new Color32(255, 255, 255, 255), new Vector2(0.5f, 0.5f), 16f);
        return tailSprite;
    }

    private static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 48;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "CatGlow" };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / n * 2f - 1f), dy = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                float a = Mathf.Clamp01(1f - (Mathf.Pow(dx, 4f) + Mathf.Pow(dy, 4f)));
                px[y * n + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        return glowSprite;
    }

    private static Sprite WashSprite()
    {
        if (washSprite != null) return washSprite;
        const int w = 64, h = 36;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "CatWash" };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((Mathf.Sqrt(dx * dx * 0.7f + dy * dy) - 0.35f) / 0.8f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        washSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        return washSprite;
    }
}
