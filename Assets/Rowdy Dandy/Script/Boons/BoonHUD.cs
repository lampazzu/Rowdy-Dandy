using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Boon HUD (bottom-left, above the button prompts): the five slots (Attack, Dash, Jump, Cats, Special) as small
// medallions in their patron's colour, then the passives. Call of the Moon's slot is the MOON METER: it fills up,
// glows when full (with the button to press), drains while Rowdy is a werewolf.
// While the stats screen is open (C / L2), a BUILD panel lists every boon with its rarity and what it does.
public class BoonHUD : MonoBehaviour
{
    private const float SlotSize = 54f, PassiveSize = 38f, Gap = 6f;
    private const float Bottom = 102f, Left = 24f;

    private static BoonHUD instance;
    private static float moonShake, moonFlash;

    private RectTransform row;
    private CanvasGroup group;
    private readonly List<(Image plate, Image icon, BoonSlot slot)> slots = new List<(Image, Image, BoonSlot)>();
    private readonly List<(Image plate, Image icon)> passives = new List<(Image, Image)>();
    private Image moonFill, moonGlow, moonButton;
    private PixelText moonText;
    private string shownSignature = "";
    private int shownDevice = -1;

    // build panel
    private RectTransform build;
    private CanvasGroup buildGroup;
    private string buildSignature = "";
    private float buildOpenedAt;
    private bool buildVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("BoonHUD (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<BoonHUD>();
    }

    public static void ShakeMoon() => moonShake = 0.35f;
    public static void FlashMoon() => moonFlash = 1f;

    private void Start()
    {
        row = OverlayUI.MakeRect("Boon Slots", OverlayUI.Root);
        OverlayUI.Place(row, Vector2.zero, new Vector2(Left, Bottom), new Vector2(900f, SlotSize));
        group = row.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 0f;

        BoonSlot[] order = { BoonSlot.Attack, BoonSlot.Dash, BoonSlot.Jump, BoonSlot.Cats, BoonSlot.Special };
        float x = 0f;
        foreach (BoonSlot s in order)
        {
            Image plate = OverlayUI.MakeImage("Slot " + s, row, Color.white, BoonIcons.Medallion);
            Put(plate.rectTransform, x, SlotSize);
            Image icon = OverlayUI.MakeImage("Icon", plate.rectTransform, Color.white);
            icon.preserveAspect = true;
            Fill(icon.rectTransform, 0.62f);
            slots.Add((plate, icon, s));
            x += SlotSize + Gap;
        }

        // moon meter lives in the Special slot
        Image specialPlate = slots[4].plate;
        moonGlow = OverlayUI.MakeImage("Moon Glow", row, Color.clear, BoonIcons.Medallion); // behind the plate
        moonGlow.transform.SetSiblingIndex(specialPlate.transform.GetSiblingIndex());
        Put(moonGlow.rectTransform, 4 * (SlotSize + Gap) - SlotSize * 0.2f, SlotSize * 1.4f);
        moonGlow.rectTransform.anchoredPosition = new Vector2(4 * (SlotSize + Gap) - SlotSize * 0.2f, -SlotSize * 0.2f);
        moonFill = OverlayUI.MakeImage("Moon", specialPlate.rectTransform, new Color(1f, 0.25f, 0.35f, 0.55f), MoonSprite());
        moonFill.type = Image.Type.Filled;
        moonFill.fillMethod = Image.FillMethod.Vertical;
        moonFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        Fill(moonFill.rectTransform, 0.78f);
        moonButton = OverlayUI.MakeImage("Wolf Button", specialPlate.rectTransform, Color.white);
        moonButton.rectTransform.anchorMin = moonButton.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        moonButton.rectTransform.anchoredPosition = new Vector2(0f, 20f);
        moonText = PixelText.Create(specialPlate.rectTransform, "", 2, new Color(1f, 0.6f, 0.65f), 0.5f);
        moonText.Rect.anchorMin = moonText.Rect.anchorMax = new Vector2(0.5f, 0f);
        moonText.Rect.anchoredPosition = new Vector2(0f, -12f);

        for (int i = 0; i < 12; i++)
        {
            Image plate = OverlayUI.MakeImage("Passive", row, Color.white, BoonIcons.Medallion);
            Image icon = OverlayUI.MakeImage("Icon", plate.rectTransform, Color.white);
            icon.preserveAspect = true;
            Fill(icon.rectTransform, 0.62f);
            plate.gameObject.SetActive(false);
            passives.Add((plate, icon));
        }
    }

    private static void Put(RectTransform r, float x, float size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
        r.pivot = new Vector2(0f, 0f);
        r.sizeDelta = new Vector2(size, size);
        r.anchoredPosition = new Vector2(x, (SlotSize - size) * 0.5f);
    }

    private static void Fill(RectTransform r, float fraction)
    {
        r.anchorMin = new Vector2(0.5f - fraction / 2f, 0.5f - fraction / 2f);
        r.anchorMax = new Vector2(0.5f + fraction / 2f, 0.5f + fraction / 2f);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private void Update()
    {
        if (row == null) return;
        IReadOnlyList<string> owned = Boons.OwnedIds;
        bool any = owned.Count > 0;
        bool hidden = !any || PauseMenu.IsPaused || RowdyNotes.IsOpen || WorldMap.IsOpen || GameObject.FindGameObjectWithTag("Player") == null;
        group.alpha = Mathf.MoveTowards(group.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime * 6f);

        string signature = string.Join(",", owned);
        foreach (string id in owned) signature += Boons.RarityOf(id);
        if (signature != shownSignature) Rebuild(owned, signature);

        UpdateMoon();
        UpdateBuildPanel(owned);
    }

    private void Rebuild(IReadOnlyList<string> owned, string signature)
    {
        shownSignature = signature;
        foreach (var (plate, icon, slot) in slots)
        {
            BoonDef d = Boons.InSlot(slot);
            if (d != null)
            {
                plate.color = BoonCatalog.Of(d.patron).color;
                icon.sprite = BoonIcons.Get(d);
                icon.color = d.iconTint;
                icon.enabled = true;
            }
            else
            {
                plate.color = new Color(0.4f, 0.36f, 0.45f, 0.55f);
                icon.enabled = false;
            }
        }
        int p = 0;
        float x = 5 * (SlotSize + Gap) + 10f;
        foreach (string id in owned)
        {
            BoonDef d = BoonCatalog.Get(id);
            if (d == null || d.slot != BoonSlot.Passive || p >= passives.Count) continue;
            var (plate, icon) = passives[p++];
            plate.gameObject.SetActive(true);
            plate.color = d.IsDuo ? Color.Lerp(BoonCatalog.Of(d.patron).color, BoonCatalog.Of(d.partner.Value).color, 0.5f) : BoonCatalog.Of(d.patron).color;
            icon.sprite = BoonIcons.Get(d);
            icon.color = d.iconTint;
            Put(plate.rectTransform, x, PassiveSize);
            x += PassiveSize + Gap;
        }
        for (; p < passives.Count; p++) passives[p].plate.gameObject.SetActive(false);
    }

    private void UpdateMoon()
    {
        bool has = Boons.Has("moon");
        moonFill.enabled = has;
        moonGlow.enabled = has;
        moonText.gameObject.SetActive(has);
        moonButton.enabled = has && Werewolf.Ready && !Werewolf.Active;
        Image special = slots[4].plate;
        Image specialIcon = slots[4].icon;
        if (!has) { special.rectTransform.localScale = Vector3.one; return; }

        float now = Time.unscaledTime;
        specialIcon.color = new Color(1f, 1f, 1f, Werewolf.Active ? 1f : 0.35f + 0.65f * Werewolf.Charge01);
        moonFill.fillAmount = Werewolf.Active ? Werewolf.TimeLeft01 : Werewolf.Charge01;
        bool ready = Werewolf.Ready && !Werewolf.Active;
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * 6f);
        moonFill.color = Werewolf.Active ? new Color(1f, 0.2f, 0.3f, 0.75f) : ready ? new Color(1f, 0.35f + 0.3f * pulse, 0.45f, 0.75f) : new Color(0.9f, 0.85f, 1f, 0.45f);
        moonFlash = Mathf.Max(0f, moonFlash - Time.unscaledDeltaTime * 1.5f);
        float glow = ready ? 0.35f + 0.35f * pulse : Werewolf.Active ? 0.4f : 0f;
        moonGlow.color = new Color(1f, 0.2f, 0.3f, Mathf.Clamp01(glow + moonFlash));

        moonShake = Mathf.Max(0f, moonShake - Time.unscaledDeltaTime);
        float shake = moonShake > 0f ? Mathf.Sin(now * 70f) * 6f * moonShake / 0.35f : 0f;
        special.rectTransform.anchoredPosition = new Vector2(4 * (SlotSize + Gap) + shake, 0f);
        special.rectTransform.localScale = Vector3.one * (1f + (ready ? 0.06f * pulse : 0f) + moonFlash * 0.25f);

        if (GameInput.DeviceVersion != shownDevice)
        {
            shownDevice = GameInput.DeviceVersion;
            moonButton.sprite = ButtonIcons.Get(GameInput.IconId(GameInput.Act.Werewolf));
            moonButton.rectTransform.sizeDelta = ButtonIcons.UISize(moonButton.sprite, 2);
        }
        moonText.SetText(Werewolf.Active ? "WOLF!" : ready ? "FULL!" : Mathf.FloorToInt(Werewolf.Charge01 * 100f) + "%");
        moonText.Color = Werewolf.Active || ready ? new Color(1f, 0.45f + 0.3f * pulse, 0.5f) : new Color(0.85f, 0.8f, 0.95f);
    }

    // ---------------------------------------------------------------- the BUILD panel (with the stats screen)
    private void UpdateBuildPanel(IReadOnlyList<string> owned)
    {
        bool want = PlayerStats.Instance != null && PlayerStats.Instance.IsStatsOpen && owned.Count > 0 && !PauseMenu.IsPaused;
        string sig = shownSignature + Boons.Rerolls;
        if (want && (build == null || sig != buildSignature)) BuildPanel(owned, sig);
        if (build == null) return;
        if (want != buildVisible)
        {
            buildVisible = want;
            build.gameObject.SetActive(want);
            buildOpenedAt = Time.unscaledTime;
        }
        if (!want) return;
        float t = Time.unscaledTime - buildOpenedAt;
        float k = Mathf.Clamp01(t / 0.22f);
        build.localScale = Vector3.one * (k < 1f ? Mathf.Lerp(0.8f, 1f, 1f - (1f - k) * (1f - k)) + Mathf.Sin(k * Mathf.PI) * 0.04f : 1f);
        buildGroup.alpha = k;
    }

    private void BuildPanel(IReadOnlyList<string> owned, string sig)
    {
        buildSignature = sig;
        if (build != null) Destroy(build.gameObject);
        const float width = 440f, rowH = 84f; // narrow: the run stats panel sits to its left
        float height = 110f + owned.Count * rowH + 40f;
        build = OverlayUI.MakeRect("Boon Build", OverlayUI.Root);
        OverlayUI.Place(build, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(width, height));
        build.pivot = new Vector2(1f, 0.5f);
        buildGroup = build.gameObject.AddComponent<CanvasGroup>();
        buildGroup.blocksRaycasts = false;
        Image panel = OverlayUI.MakePanel("Panel", build);
        panel.rectTransform.anchorMin = Vector2.zero; panel.rectTransform.anchorMax = Vector2.one;
        panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;

        PixelText title = PixelText.Create(build, "YOUR BUILD", 4, new Color(1f, 0.82f, 0.3f), 0.5f);
        title.Rect.anchorMin = title.Rect.anchorMax = new Vector2(0.5f, 1f);
        title.Rect.anchoredPosition = new Vector2(0f, -40f);
        PixelText gel = PixelText.Create(build, "HAIR GEL X" + Boons.Rerolls, 2, new Color(0.6f, 1f, 0.9f), 0.5f);
        gel.Rect.anchorMin = gel.Rect.anchorMax = new Vector2(0.5f, 1f);
        gel.Rect.anchoredPosition = new Vector2(0f, -74f);

        float y = -110f;
        foreach (string id in owned)
        {
            BoonDef d = BoonCatalog.Get(id);
            if (d == null) continue;
            Rarity r = Boons.RarityOf(id);
            PatronInfo p = BoonCatalog.Of(d.patron);
            Image plate = OverlayUI.MakeImage("Plate", build, p.color, BoonIcons.Medallion);
            plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0f, 1f);
            plate.rectTransform.pivot = new Vector2(0f, 1f);
            plate.rectTransform.sizeDelta = new Vector2(52f, 52f);
            plate.rectTransform.anchoredPosition = new Vector2(24f, y);
            Image icon = OverlayUI.MakeImage("Icon", plate.rectTransform, d.iconTint, BoonIcons.Get(d));
            icon.preserveAspect = true;
            Fill(icon.rectTransform, 0.62f);

            PixelText name = PixelText.Create(build, d.name, 2, Color.white, 0f);
            name.Rect.anchorMin = name.Rect.anchorMax = new Vector2(0f, 1f);
            name.Rect.anchoredPosition = new Vector2(88f, y - 12f);
            PixelText tag = PixelText.Create(build, BoonCatalog.RarityNames[(int)r] + " " + BoonCatalog.SlotNames[(int)d.slot], 1, BoonCatalog.RarityColors[(int)r], 0f);
            tag.SetScale(2);
            tag.Rect.anchorMin = tag.Rect.anchorMax = new Vector2(0f, 1f);
            tag.Rect.anchoredPosition = new Vector2(88f, y - 30f);
            string desc = GameInput.Format(d.Describe(r));
            string wrapped = BoonPicker.Wrap(desc, 26);
            string[] lines = wrapped.Split('\n');
            if (lines.Length > 2) wrapped = lines[0] + "\n" + lines[1] + "..";
            PixelText line = PixelText.Create(build, wrapped, 2, new Color(0.85f, 0.8f, 0.92f), 0f);
            line.Rect.anchorMin = line.Rect.anchorMax = new Vector2(0f, 1f);
            line.Rect.pivot = new Vector2(0f, 1f);
            line.Rect.anchoredPosition = new Vector2(88f, y - 42f);
            y -= rowH;
        }
        build.gameObject.SetActive(false);
        buildVisible = false;
    }

    // ---------------------------------------------------------------- art
    private static Sprite moonSprite;
    private static Sprite MoonSprite()
    {
        if (moonSprite != null) return moonSprite;
        const int n = 24;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "MoonMeter" };
        var px = new Color32[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                bool crater = (Mathf.Abs(x - 8) < 2 && Mathf.Abs(y - 14) < 2) || (Mathf.Abs(x - 15) < 3 && Mathf.Abs(y - 8) < 2);
                px[y * n + x] = d <= 11f ? (crater ? new Color32(200, 200, 210, 255) : new Color32(255, 255, 255, 255)) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        moonSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f);
        return moonSprite;
    }
}
