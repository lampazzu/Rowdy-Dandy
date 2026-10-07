using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One entry per collected cat: [purple slot with the cat's face]  NAME
//                                                                 [cooldown bar]
// Same pieces as the weapon slot and the HUD bars, drawn a bit smaller (pixelScale 3 vs the HUD's 4).
// Added by Tools > Rowdy Dandy > HUD - Add Cat Panel. Name and face come from each cat's PetFollower (HUD section).
// The face is dim while recharging and pops when the cat is ready again.
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

    private class Entry
    {
        public PetFollower pet;
        public RectTransform root;
        public Image icon;
        public PixelText label;
        public Image fill;
        public bool wasReady;
        public float punchTimer;
    }

    private readonly List<Entry> entries = new List<Entry>();
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

    private void Update()
    {
        SyncEntries();
        AvoidStatsPanel();

        float dt = Time.unscaledDeltaTime;
        foreach (Entry entry in entries)
        {
            float fraction = entry.pet.CooldownFraction;
            entry.fill.fillAmount = fraction;

            bool ready = fraction >= 1f;
            entry.icon.color = ready ? Color.white : rechargingTint;
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
            if (entries[i].pet == null || !entries[i].pet.IsCollected)
            {
                if (entries[i].root != null) Destroy(entries[i].root.gameObject);
                entries.RemoveAt(i);
                changed = true;
            }
        }

        foreach (PetFollower pet in PetFollower.Pets)
        {
            if (pet == null || !pet.IsCollected || entries.Exists(e => e.pet == pet)) continue;
            entries.Add(BuildEntry(pet));
            changed = true;
        }

        if (changed)
        {
            float step = 22 * pixelScale + entryGap;
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].root.anchoredPosition = new Vector2(0f, -i * step);
            }
        }
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

        // Name
        entry.label = PixelText.Create(entry.root, pet.CatName, s, Color.white, 0f);
        RectTransform labelRect = entry.label.Rect;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(textX, -(2 * s + labelRect.sizeDelta.y / 2f));

        // Cooldown bar: dark back, pink fill, purple frame (like the HP / XP / DUR bars)
        RectTransform bar = CreateUI("CooldownBar", entry.root);
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
