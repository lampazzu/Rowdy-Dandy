using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One entry per collected cat: [purple slot with the cat's face]  NAME
//                                                                 [cooldown bar]
// Same pieces as the weapon slot and the HUD bars, drawn a bit smaller (pixelScale 3 vs the HUD's 4).
// Added by Tools > Rowdy Dandy > HUD - Add Cat Panel. Name and face come from each cat's PetFollower (HUD section).
// The face is dim while recharging and pops when the cat is ready again.
// A cat lost by dying keeps its slot for a few seconds after the respawn: grey face, blinking "GOT LOST!",
// then it slowly fades and slides away (plus a "[face] NICK GOT LOST!" popup over Rowdy).
public class CatHUD : MonoBehaviour
{
    [Header("Art (same pieces as the weapon slot and the bars)")]
    [SerializeField] private Sprite slotSprite;      // Panel_9Slice
    [SerializeField] private Sprite barBackSprite;   // SubBlueHealthUI
    [SerializeField] private Sprite barFillSprite;   // BarFill_Pink
    [SerializeField] private Sprite barFrameSprite;  // GradeHealthUI

    [Header("Layout (UI pixels at 1080p)")]
    [Tooltip("UI pixels per art pixel. The main HUD uses 4.")]
    [SerializeField] private int pixelScale = 3;
    [SerializeField] private float entryGap = 6f;
    [Tooltip("When this panel is open (C), the cats move below it")]
    [SerializeField] private RectTransform statsPanel;
    [SerializeField] private float statsGap = 12f;

    [Header("Feedback")]
    [SerializeField] private Color rechargingTint = new Color(0.45f, 0.4f, 0.5f, 1f);
    [SerializeField] private float readyPunch = 0.15f;
    [SerializeField] private float readyPunchTime = 0.2f;
    [Tooltip("Lost cat slot: seconds before it starts fading (counted from the respawn), then how long the fade takes")]
    [SerializeField] private float lostHold = 3.5f;
    [SerializeField] private float lostFade = 3f;
    [SerializeField] private Color lostColor = new Color(1f, 0.35f, 0.45f, 1f);

    private class Entry
    {
        public PetFollower pet;
        public RectTransform root;
        public Image icon;
        public PixelText label;
        public Image fill;
        public bool wasReady;
        public float punchTimer;
        // lost-cat ghost slot
        public bool lost;
        public float lostAge;
        public bool popupShown;
        public string lostName;
        public Sprite lostFace;
        public CanvasGroup group;
        public PixelText status;
        public RectTransform bar;
        public Image crown; // the Cat Party leader (gold) / sub-leader (silver)
        public Image sideCrown; // ...and one beside the name
        public Vector2 home; // its place in the layout
    }

    private readonly List<Entry> entries = new List<Entry>();

    // Legendary rats carried (bait for a lost cat, dropped at a checkpoint): [rat] x2 under the cats
    private RectTransform ratRow;
    private Image ratIcon;
    private PixelText ratText;
    private int shownRats = -1;
    private RectTransform rect;
    private float baseY;
    private float slicedMultiplier = 1f;

    private void Awake()
    {
        rect = (RectTransform)transform;
        baseY = rect.anchoredPosition.y;

        CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
        float referencePPU = scaler != null ? scaler.referencePixelsPerUnit : 100f;
        if (slotSprite != null) slicedMultiplier = referencePPU / (slotSprite.pixelsPerUnit * pixelScale);
    }

    // Free party places (Magical Cat Capacity - cats): an empty slot with "+2" under the cats
    private RectTransform freeSlot;
    private PixelText freeText;
    private int shownFree = -1;

    private int FreePlaces => CatRoster.Capacity > 1 ? Mathf.Max(0, CatRoster.Capacity - CatRoster.CatCount) : 0;

    private void UpdateFreeSlot()
    {
        int free = FreePlaces;
        if (free == shownFree) return;
        bool grew = shownFree >= 0 && free > shownFree;
        shownFree = free;
        if (freeSlot == null)
        {
            if (free <= 0) return;
            float slotSize = 22 * pixelScale;
            freeSlot = CreateUI("Free Places", transform);
            Place(freeSlot, 0f, 0f, slotSize, slotSize);
            Image slotImage = AddImage(freeSlot, slotSprite);
            slotImage.type = Image.Type.Sliced;
            slotImage.pixelsPerUnitMultiplier = slicedMultiplier;
            slotImage.color = new Color(1f, 1f, 1f, 0.45f);
            freeText = PixelText.Create(freeSlot, "", pixelScale, new Color(1f, 0.8f, 0.95f, 0.85f), 0.5f);
            freeText.Rect.anchorMin = freeText.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            freeText.Rect.anchoredPosition = Vector2.zero;
        }
        freeSlot.gameObject.SetActive(free > 0);
        freeText.SetText("+" + free);
        if (grew) freePunch = 0.3f; // level up opened a new place
        Layout();
    }

    private float freePunch;

    private void Update()
    {
        SyncEntries();
        foreach (Entry e in entries)
        {
            if (e.crown == null) continue;
            bool sub = CatRoster.IsSubLeader(e.pet), lead = CatRoster.IsLeader(e.pet);
            bool ranked = !e.lost && (lead || sub);
            e.crown.enabled = ranked;
            e.crown.color = sub ? SubColor : Color.white;
            if (e.sideCrown != null) { e.sideCrown.enabled = ranked; e.sideCrown.color = e.crown.color; }
            // the leader's cooldown bar is gold, the sub-leader's silver
            if (e.fill != null && !e.lost)
            {
                e.fill.sprite = ranked ? WhiteFill(barFillSprite) : barFillSprite; // same shape as the pink fill: stays inside the frame
                e.fill.color = lead ? LeaderColor : sub ? SubColor : Color.white;
            }
        }
        bool compact = GameSettings.CatHudCollapsed && entries.Exists(e => !e.lost);
        foreach (Entry e in entries) if (!e.lost && e.root.gameObject.activeSelf == compact) e.root.gameObject.SetActive(!compact);
        UpdateCompact(compact);
        UpdateFreeSlot();
        if (freeSlot != null)
        {
            freePunch = Mathf.Max(0f, freePunch - Time.unscaledDeltaTime);
            freeSlot.localScale = Vector3.one * (1f + freePunch * 0.6f);
        }
        AvoidStatsPanel();
        Layout(); // party order (leader / sub-leader) and the stats panel can change it any frame
        UpdateRats();

        float dt = Time.unscaledDeltaTime;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry entry = entries[i];
            if (!entry.lost) continue;
            if (!UpdateLost(entry, dt))
            {
                Destroy(entry.root.gameObject);
                entries.RemoveAt(i);
                Layout();
            }
        }

        foreach (Entry entry in entries)
        {
            if (entry.lost) continue;
            float fraction = entry.pet.CooldownFraction;
            entry.fill.fillAmount = fraction;

            bool ready = fraction >= 1f;
            entry.icon.color = ready ? entry.pet.FaceTint : rechargingTint * entry.pet.FaceTint;
            if (ready && !entry.wasReady) entry.punchTimer = readyPunchTime;
            entry.wasReady = ready;

            float punch = 0f;
            if (entry.punchTimer > 0f)
            {
                entry.punchTimer -= dt;
                punch = readyPunch * Mathf.Clamp01(entry.punchTimer / readyPunchTime);
            }
            entry.icon.rectTransform.localScale = Vector3.one * (1f + punch);
        }
    }

    // Add newly collected cats (in pickup order), drop destroyed ones
    private void SyncEntries()
    {
        bool changed = false;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].lost) continue;
            if (entries[i].pet == null || !entries[i].pet.IsCollected)
            {
                if (entries[i].root != null) Destroy(entries[i].root.gameObject);
                entries.RemoveAt(i);
                changed = true;
            }
        }

        foreach (PetFollower pet in PetFollower.Pets)
        {
            if (pet == null || !pet.IsCollected || entries.Exists(e => e.pet == pet && !e.lost)) continue;
            entries.Add(BuildEntry(pet));
            changed = true;
        }

        // The cat the last death cost: a ghost slot at the bottom
        int lostCount = CatRoster.LostNotices.Count;
        for (int n = 0; n < lostCount; n++)
        {
            CatRoster.LostCat notice = CatRoster.LostNotices[n];
            if (notice == null || notice.pet == null) continue;
            Entry ghost = BuildEntry(notice.pet);
            ghost.popupShown = n > 0; // one popup for the whole batch
            if (n == 0 && lostCount > 1) { notice = new CatRoster.LostCat { name = lostCount + " CATS", portrait = notice.portrait, pet = notice.pet }; }
            ghost.lost = true;
            ghost.lostName = notice.name;
            ghost.lostFace = notice.portrait;
            ghost.group = ghost.root.gameObject.AddComponent<CanvasGroup>();
            ghost.icon.color = rechargingTint;
            ghost.bar.gameObject.SetActive(false);
            ghost.status = PixelText.Create(ghost.root, "GOT LOST!", pixelScale, lostColor, 0f);
            RectTransform s = ghost.status.Rect;
            s.anchorMin = s.anchorMax = new Vector2(0f, 1f);
            s.anchoredPosition = new Vector2(ghost.bar.anchoredPosition.x, ghost.bar.anchoredPosition.y - ghost.bar.sizeDelta.y / 2f);
            entries.Add(ghost);
            changed = true;
        }
        CatRoster.LostNotices.Clear();

        if (changed) Layout();
    }

    // Collected cats first in PARTY order (leader 1st, sub-leader 2nd), then the free places, lost ghosts, the rats.
    // Never runs into the boon strip at the bottom-left: once the column would reach it, it wraps into a 2nd column.
    private const float BottomKeepOut = 200f; // canvas units above the bottom edge (boon strip + its moon button)
    private Vector2 ratSpot;

    private void Layout()
    {
        float step = 22 * pixelScale + entryGap;
        float columnStep = (22 + 2 + 76) * pixelScale + 16f;
        int maxRows = MaxRows(step);
        int slot = 0;
        Vector2 Next() { int col = slot / maxRows, row = slot % maxRows; slot++; return new Vector2(col * columnStep, -row * step); }

        List<PetFollower> party = CatRoster.Party;
        var live = entries.FindAll(e => !e.lost);
        live.Sort((a, b) => Order(party, a.pet).CompareTo(Order(party, b.pet)));
        if (compactRoot != null && compactRoot.gameObject.activeSelf) compactRoot.anchoredPosition = Next(); // one row for all
        else foreach (Entry e in live) { e.home = Next(); e.root.anchoredPosition = e.home; }
        if (freeSlot != null && freeSlot.gameObject.activeSelf) freeSlot.anchoredPosition = Next();
        foreach (Entry e in entries) if (e.lost) e.home = Next();
        ratSpot = Next();
    }

    private static int Order(List<PetFollower> party, PetFollower pet)
    {
        int i = party.IndexOf(pet);
        return i < 0 ? 999 : i;
    }

    private int MaxRows(float step)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return 99;
        var canvasRect = (RectTransform)canvas.rootCanvas.transform;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        float top = canvasRect.InverseTransformPoint(corners[1]).y;  // top-left corner of this panel
        float bottom = -canvasRect.rect.height * canvasRect.pivot.y + BottomKeepOut;
        return Mathf.Max(2, Mathf.FloorToInt((top - bottom + entryGap) / step));
    }

    // false = finished, remove it
    private bool UpdateLost(Entry entry, float dt)
    {
        entry.lostAge += dt;
        float t = entry.lostAge;

        // A readable popup once the respawn transition is over
        if (!entry.popupShown && t > 0.9f)
        {
            entry.popupShown = true;
            GameObject rowdy = GameObject.FindGameObjectWithTag("Player");
            if (rowdy != null)
                IconPopup.Show(rowdy.transform.position + Vector3.up * 0.9f, entry.lostFace, entry.lostName + " GOT LOST!", lostColor, 1f, 3.2f);
        }

        // Blinking status, shaking face for the first second
        entry.status.Color = new Color(lostColor.r, lostColor.g, lostColor.b, Mathf.Repeat(t, 0.5f) < 0.33f ? 1f : 0.35f);
        float shake = t < 1.4f && t > 0.9f ? Mathf.Round(Mathf.Sin(t * 70f) * 2f) : 0f;
        entry.icon.rectTransform.anchoredPosition = new Vector2(shake, 0f);

        float fade = Mathf.Clamp01((t - lostHold) / Mathf.Max(0.01f, lostFade));
        entry.group.alpha = 1f - fade;
        entry.root.anchoredPosition = new Vector2(entry.home.x - fade * fade * 60f, entry.home.y);
        return fade < 1f;
    }

    private void UpdateRats()
    {
        int rats = CatRoster.Rats;
        if (ratRow == null)
        {
            if (rats <= 0) return;
            ratRow = CreateUI("Rats", transform);
            ratIcon = AddImage(CreateUI("Rat", ratRow), null);
            ratIcon.preserveAspect = true;
            ratFrames = RatHudFrames();
            if (ratFrames != null) ratIcon.sprite = ratFrames[0];
            // The rat is only ~20 x 14 px of its 57 x 50 frame: cropped, it shows about as big as a cat's face
            Place(ratIcon.rectTransform, 1f * pixelScale, 4f * pixelScale, 20 * pixelScale, 15 * pixelScale);
            ratText = PixelText.Create(ratRow, "", pixelScale, new Color(0.75f, 0.92f, 1f), 0f);
            ratText.Rect.anchorMin = ratText.Rect.anchorMax = new Vector2(0f, 1f);
        }
        ratRow.gameObject.SetActive(rats > 0);
        int key = rats * 100 + CatRoster.CatCount;
        if (key != shownRats)
        {
            if (shownRats >= 0 && rats > shownRats / 100) ratPunch = 0.35f; // a new rat: punch
            shownRats = key;
            ratText.SetText("X" + rats + "  BAIT");
            ratText.Rect.anchoredPosition = new Vector2(24 * pixelScale, -11 * pixelScale);
        }
        // Flaps in the HUD too
        if (ratFrames != null) ratIcon.sprite = ratFrames[new[] { 0, 1, 2, 1 }[(int)(Time.unscaledTime * 8f) % 4]];
        ratPunch = Mathf.Max(0f, ratPunch - Time.unscaledDeltaTime);
        ratIcon.rectTransform.localScale = Vector3.one * (1f + ratPunch * 1.2f);
        ratIcon.color = ratPunch > 0f ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.3f), Mathf.Repeat(ratPunch * 12f, 1f)) : Color.white;
        Place(ratRow, ratSpot.x, -ratSpot.y, 260f, 22 * pixelScale);
    }

    private Sprite[] ratFrames;
    private float ratPunch;

    // The flying frames of PIV_Flying_Rat cropped tight around the rat (top row, frames 0-2)
    private static Sprite[] RatHudFrames()
    {
        ItemArt art = ItemArt.Get;
        if (art == null || art.flyingRat == null) return null;
        Texture2D tex = art.flyingRat;
        float fw = tex.width / 10f;
        float sx = tex.width / 575f, sy = tex.height / 99f; // in case the import shrank it
        var frames = new Sprite[3];
        for (int f = 0; f < 3; f++)
        {
            var r = new Rect(Mathf.Round(f * fw + 15f * sx), Mathf.Round(tex.height - 46f * sy), Mathf.Round(20f * sx), Mathf.Round(15f * sy));
            frames[f] = Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), 64f, 0, SpriteMeshType.FullRect);
        }
        return frames;
    }

    private void AvoidStatsPanel()
    {
        float y = baseY;
        if (statsPanel != null && statsPanel.gameObject.activeInHierarchy)
        {
            float statsBottom = statsPanel.anchoredPosition.y - statsPanel.sizeDelta.y - statsGap;
            y = Mathf.Min(baseY, statsBottom);
        }
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
    }

    private Entry BuildEntry(PetFollower pet)
    {
        int s = pixelScale;
        float slotSize = 22 * s;
        float textX = slotSize + 2 * s;

        var entry = new Entry { pet = pet, wasReady = true };
        entry.root = CreateUI(pet.CatName, transform);
        Place(entry.root, 0f, 0f, textX + 76 * s, slotSize);

        // Slot + face
        RectTransform slot = CreateUI("Slot", entry.root);
        Place(slot, 0f, 0f, slotSize, slotSize);
        Image slotImage = AddImage(slot, slotSprite);
        slotImage.type = Image.Type.Sliced;
        slotImage.pixelsPerUnitMultiplier = slicedMultiplier;

        RectTransform iconRect = CreateUI("Portrait", slot);
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(16 * s, 16 * s);
        entry.icon = AddImage(iconRect, pet.Portrait);
        entry.icon.preserveAspect = true;

        // leader (Cat Party): a little crown on top of the slot
        RectTransform crownRect = CreateUI("Leader Crown", slot);
        crownRect.anchorMin = crownRect.anchorMax = crownRect.pivot = new Vector2(0.5f, 0f);
        crownRect.anchoredPosition = new Vector2(0f, slotSize - 3 * s);
        crownRect.sizeDelta = new Vector2(9 * s, 5 * s);
        entry.crown = AddImage(crownRect, MoreSprites.Crown);
        entry.crown.enabled = false;

        // Name
        entry.label = PixelText.Create(entry.root, pet.CatName, s, Color.white, 0f);
        RectTransform labelRect = entry.label.Rect;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(textX, -(2 * s + labelRect.sizeDelta.y / 2f));

        // leader / sub-leader: a crown beside the name too
        RectTransform sideRect = CreateUI("Side Crown", entry.root);
        sideRect.anchorMin = sideRect.anchorMax = new Vector2(0f, 1f);
        sideRect.pivot = new Vector2(0f, 0.5f);
        sideRect.anchoredPosition = new Vector2(textX + labelRect.sizeDelta.x + 2 * s, labelRect.anchoredPosition.y + s);
        sideRect.sizeDelta = new Vector2(9 * s, 5 * s);
        entry.sideCrown = AddImage(sideRect, MoreSprites.Crown);
        entry.sideCrown.enabled = false;

        // Cooldown bar: dark back, pink fill, purple frame (like the HP / XP / DUR bars)
        RectTransform bar = CreateUI("CooldownBar", entry.root);
        entry.bar = bar;
        Place(bar, textX, slotSize - 10 * s, 76 * s, 8 * s);
        Stretch(AddImage(CreateUI("Back", bar), barBackSprite).rectTransform);
        entry.fill = AddImage(CreateUI("Fill", bar), barFillSprite);
        Stretch(entry.fill.rectTransform);
        entry.fill.type = Image.Type.Filled;
        entry.fill.fillMethod = Image.FillMethod.Horizontal;
        entry.fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        Stretch(AddImage(CreateUI("Frame", bar), barFrameSprite).rectTransform);

        return entry;
    }

    // The pink fill art turned white (keeping its shading and its exact shape), so gold / silver tints stay inside the
    // bar's frame like every other bar. A plain white square filled the whole bar rect and spilled over the frame.
    private static Sprite whiteFill;
    private static Sprite whiteFillSource;
    private static Sprite WhiteFill(Sprite src)
    {
        if (src == null) return OverlayUI.WhiteSprite;
        if (whiteFill != null && whiteFillSource == src) return whiteFill;
        Texture2D tex = src.texture;
        Rect r = src.textureRect;
        int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        Color32[] px;
        try
        {
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0); // (lower-left origin, like the sprite rect)
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            px = copy.GetPixels32();
            Destroy(copy);
        }
        catch { return OverlayUI.WhiteSprite; }
        float maxV = 0.01f;
        foreach (Color32 c in px) if (c.a > 0) maxV = Mathf.Max(maxV, Mathf.Max(c.r, Mathf.Max(c.g, c.b)) / 255f);
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a == 0) continue;
            float v = Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b)) / 255f / maxV; // brightest pixel = white
            byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(0.55f, 1f, v) * 255f);
            px[i] = new Color32(b, b, b, px[i].a);
        }
        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "CatBarFill_White" };
        outTex.SetPixels32(px);
        outTex.Apply(false, true);
        whiteFill = Sprite.Create(outTex, new Rect(0, 0, w, h), new Vector2(src.pivot.x / w, src.pivot.y / h), src.pixelsPerUnit, 0, SpriteMeshType.FullRect, src.border);
        whiteFillSource = src;
        return whiteFill;
    }

    // ---------------------------------------------------------------- compact view (Preferences / Cat Party)
    // All cats in one row: [leader face]  CAT PARTY
    //                                     [] [] [] []   <- one little square per cat, filling up as it recharges
    public static readonly Color LeaderColor = new Color(1f, 0.82f, 0.25f);
    public static readonly Color SubColor = new Color(0.75f, 0.85f, 1f);
    private static readonly Color SquareColor = new Color(1f, 0.45f, 0.8f);

    private RectTransform compactRoot;
    private Image compactFace, compactCrown;
    private readonly List<(Image back, Image fill, Image glow)> squares = new List<(Image, Image, Image)>();

    private void UpdateCompact(bool on)
    {
        if (!on) { if (compactRoot != null && compactRoot.gameObject.activeSelf) { compactRoot.gameObject.SetActive(false); Layout(); } return; }
        int s = pixelScale;
        float slotSize = 22 * s, textX = slotSize + 2 * s;
        if (compactRoot == null)
        {
            compactRoot = CreateUI("Cat Party (compact)", transform);
            Place(compactRoot, 0f, 0f, textX + 76 * s, slotSize);
            RectTransform slot = CreateUI("Slot", compactRoot);
            Place(slot, 0f, 0f, slotSize, slotSize);
            Image slotImage = AddImage(slot, slotSprite);
            slotImage.type = Image.Type.Sliced;
            slotImage.pixelsPerUnitMultiplier = slicedMultiplier;
            RectTransform faceRect = CreateUI("Face", slot);
            faceRect.anchorMin = faceRect.anchorMax = faceRect.pivot = new Vector2(0.5f, 0.5f);
            faceRect.sizeDelta = new Vector2(16 * s, 16 * s);
            compactFace = AddImage(faceRect, null);
            compactFace.preserveAspect = true;
            RectTransform crownRect = CreateUI("Crown", slot);
            crownRect.anchorMin = crownRect.anchorMax = crownRect.pivot = new Vector2(0.5f, 0f);
            crownRect.anchoredPosition = new Vector2(0f, slotSize - 3 * s);
            crownRect.sizeDelta = new Vector2(9 * s, 5 * s);
            compactCrown = AddImage(crownRect, MoreSprites.Crown);
            PixelText label = PixelText.Create(compactRoot, "CAT PARTY", s, Color.white, 0f);
            label.Rect.anchorMin = label.Rect.anchorMax = new Vector2(0f, 1f);
            label.Rect.anchoredPosition = new Vector2(textX, -(2 * s + label.Rect.sizeDelta.y / 2f));
        }
        if (!compactRoot.gameObject.activeSelf) { compactRoot.gameObject.SetActive(true); Layout(); }

        List<PetFollower> party = CatRoster.Party;
        PetFollower face = party.Count > 0 ? party[0] : null;
        compactFace.sprite = face != null ? face.Portrait : null;
        compactFace.enabled = face != null;
        if (face != null) compactFace.color = face.FaceTint;
        compactCrown.enabled = face != null && CatRoster.IsLeader(face);

        // one square per cat, in party order, where the cooldown bar would be
        while (squares.Count < party.Count)
        {
            RectTransform sq = CreateUI("Square", compactRoot);
            sq.anchorMin = sq.anchorMax = sq.pivot = new Vector2(0f, 1f);
            sq.sizeDelta = new Vector2(6 * s, 6 * s);
            Image back = AddImage(sq, OverlayUI.WhiteSprite);
            back.color = new Color(0.1f, 0.03f, 0.12f, 0.9f);
            Image fill = AddImage(CreateUI("Fill", sq), OverlayUI.WhiteSprite);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(s, s); fill.rectTransform.offsetMax = new Vector2(-s, -s);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            Image glow = AddImage(CreateUI("Ready", sq), OverlayUI.WhiteSprite);
            glow.rectTransform.anchorMin = Vector2.zero; glow.rectTransform.anchorMax = Vector2.one;
            glow.rectTransform.offsetMin = glow.rectTransform.offsetMax = Vector2.zero;
            squares.Add((back, fill, glow));
        }
        for (int i = 0; i < squares.Count; i++)
        {
            bool used = i < party.Count;
            squares[i].back.gameObject.SetActive(used);
            if (!used) continue;
            PetFollower p = party[i];
            RectTransform sq = squares[i].back.rectTransform;
            sq.anchoredPosition = new Vector2(textX + i * 8 * s, -(slotSize - 10 * s));
            float fr = p.CooldownFraction;
            Color c = CatRoster.IsLeader(p) ? LeaderColor : CatRoster.IsSubLeader(p) ? SubColor : SquareColor;
            squares[i].fill.fillAmount = fr;
            squares[i].fill.color = fr >= 1f ? c : new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 1f);
            // ready: a soft pulse over the square
            squares[i].glow.color = new Color(1f, 1f, 1f, fr >= 1f ? 0.15f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f + i) : 0f);
        }
    }

    private static RectTransform CreateUI(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image AddImage(RectTransform target, Sprite sprite)
    {
        Image image = target.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    // Top-left anchored, y measured downward (same as the HUD organizer)
    private static void Place(RectTransform target, float x, float y, float width, float height)
    {
        target.anchorMin = target.anchorMax = target.pivot = new Vector2(0f, 1f);
        target.anchoredPosition = new Vector2(x, -y);
        target.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform target)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.offsetMin = target.offsetMax = Vector2.zero;
    }
}
