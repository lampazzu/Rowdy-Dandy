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
        public Image crown; // the Cat Party leader
    }

    private readonly List<Entry> entries = new List<Entry>();

    // Registered flying rats (each one keeps a cat from getting lost): [rat] x2 under the cats
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
        foreach (Entry e in entries) if (e.crown != null) e.crown.enabled = !e.lost && CatRoster.IsLeader(e.pet);
        UpdateFreeSlot();
        if (freeSlot != null)
        {
            freePunch = Mathf.Max(0f, freePunch - Time.unscaledDeltaTime);
            freeSlot.localScale = Vector3.one * (1f + freePunch * 0.6f);
        }
        AvoidStatsPanel();
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
            entry.icon.color = ready ? entry.pet.Tint : rechargingTint * entry.pet.Tint;
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

    // Collected cats first (pickup order), lost ghosts under them
    private void Layout()
    {
        float step = 22 * pixelScale + entryGap;
        int row = 0;
        foreach (Entry e in entries) if (!e.lost) e.root.anchoredPosition = new Vector2(0f, -(row++) * step);
        if (freeSlot != null && freeSlot.gameObject.activeSelf) freeSlot.anchoredPosition = new Vector2(0f, -(row++) * step);
        foreach (Entry e in entries) if (e.lost) e.root.anchoredPosition = new Vector2(e.root.anchoredPosition.x, -(row++) * step);
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
        Vector2 p = entry.root.anchoredPosition;
        entry.root.anchoredPosition = new Vector2(-fade * fade * 60f, p.y);
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
            bool safe = rats >= CatRoster.CatCount && CatRoster.CatCount > 0;
            ratText.SetText("X" + rats + (safe ? "  CATS SAFE" : ""));
            ratText.Rect.anchoredPosition = new Vector2(24 * pixelScale, -11 * pixelScale);
        }
        // Flaps in the HUD too
        if (ratFrames != null) ratIcon.sprite = ratFrames[new[] { 0, 1, 2, 1 }[(int)(Time.unscaledTime * 8f) % 4]];
        ratPunch = Mathf.Max(0f, ratPunch - Time.unscaledDeltaTime);
        ratIcon.rectTransform.localScale = Vector3.one * (1f + ratPunch * 1.2f);
        ratIcon.color = ratPunch > 0f ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.3f), Mathf.Repeat(ratPunch * 12f, 1f)) : Color.white;
        int rows = entries.Count + (freeSlot != null && freeSlot.gameObject.activeSelf ? 1 : 0);
        Place(ratRow, 0f, rows * (22 * pixelScale + entryGap), 260f, 22 * pixelScale);
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
