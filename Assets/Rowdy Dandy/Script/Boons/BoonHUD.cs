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
        bool hidden = !any || (PauseMenu.IsPaused && !StatsPause.IsOpen) || RowdyNotes.IsOpen || WorldMap.IsOpen || GameObject.FindGameObjectWithTag("Player") == null;
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
        // the Special slot's meter: Call of the Moon (werewolf) or Stephmoss (the poison patron's form)
        // ...or the Chef's Ingredient Rain / the Blacksmith's Armory (SpecialBoons)
        bool steph = Boons.Has("stephmoss");
        bool other = SpecialBoons.Owned;
        bool has = Boons.Has("moon") || steph || other;
        bool active = other ? SpecialBoons.Active : steph ? StephmossForm.Active : Werewolf.Active;
        bool readyNow = other ? SpecialBoons.Ready : steph ? StephmossForm.Ready : Werewolf.Ready;
        float charge = other ? SpecialBoons.Charge01 : steph ? StephmossForm.Charge01 : Werewolf.Charge01;
        float left = other ? SpecialBoons.TimeLeft01 : steph ? StephmossForm.TimeLeft01 : Werewolf.TimeLeft01;
        Color tone = other ? SpecialBoons.Tone : steph ? new Color(0.75f, 1f, 0.25f) : new Color(1f, 0.2f, 0.3f);
        moonFill.enabled = has;
        moonGlow.enabled = has;
        moonText.gameObject.SetActive(has);
        moonButton.enabled = has && readyNow && !active;
        if (slots == null || slots.Count < 5) return;
        Image special = slots[4].plate;
        Image specialIcon = slots[4].icon;
        if (!has) { special.rectTransform.localScale = Vector3.one; return; }

        float now = Time.unscaledTime;
        specialIcon.color = new Color(1f, 1f, 1f, active ? 1f : 0.35f + 0.65f * charge);
        moonFill.fillAmount = active ? left : charge;
        bool ready = readyNow && !active;
        float pulse = 0.5f + 0.5f * Mathf.Sin(now * 6f);
        moonFill.color = active ? new Color(tone.r, tone.g, tone.b, 0.75f) : ready ? new Color(tone.r, Mathf.Min(1f, tone.g + 0.3f * pulse), tone.b + 0.15f, 0.75f) : new Color(0.9f, 0.85f, 1f, 0.45f);
        moonFlash = Mathf.Max(0f, moonFlash - Time.unscaledDeltaTime * 1.5f);
        float glow = ready ? 0.35f + 0.35f * pulse : active ? 0.4f : 0f;
        moonGlow.color = new Color(tone.r, tone.g, tone.b, Mathf.Clamp01(glow + moonFlash));

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
        moonText.SetText(active ? (other ? SpecialBoons.ActiveLabel : steph ? "BUG!" : "WOLF!") : ready ? "FULL!" : Mathf.FloorToInt(charge * 100f) + "%");
        moonText.Color = active || ready ? new Color(tone.r, Mathf.Min(1f, tone.g * 0.6f + 0.3f * pulse + 0.2f), tone.b + 0.2f) : new Color(0.85f, 0.8f, 0.95f);
    }
    // ---------------------------------------------------------------- the BUILD panel (with the stats screen)
    private void UpdateBuildPanel(IReadOnlyList<string> owned)
    {
        bool statsUp = PlayerStats.Instance != null && PlayerStats.Instance.IsStatsOpen && owned.Count > 0 && (!PauseMenu.IsPaused || StatsPause.IsOpen);
        // {INTERACT} on the (paused) stats screen: zoom into the boons, every detail
        if (statsUp && StatsPause.IsOpen && Time.unscaledTime - StatsPause.OpenedAt > 0.15f && GameInput.Down(GameInput.Act.Interact))
        {
            StatsPause.Expanded = !StatsPause.Expanded;
            UISound.Play(StatsPause.Expanded ? UISound.Cue.Open : UISound.Cue.Back);
        }
        if (!statsUp) StatsPause.Expanded = false;
        UpdateExpanded(owned, statsUp && StatsPause.Expanded);
        bool want = statsUp && !StatsPause.Expanded;
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
        PixelText zoom = PixelText.Create(build, GameInput.Format("{INTERACT} ZOOM IN"), 2, new Color(1f, 0.85f, 0.4f), 0.5f);
        zoom.Rect.anchorMin = zoom.Rect.anchorMax = new Vector2(0.5f, 0f);
        zoom.Rect.anchoredPosition = new Vector2(0f, 24f);
        build.gameObject.SetActive(false);
        buildVisible = false;
    }

    // ---------------------------------------------------------------- the EXPANDED build (zoomed in, paused)
    // Big centred panel, two columns of boon cards with the FULL text, patron and rarity; {MOVE} scrolls.
    private RectTransform expanded, expandedContent;
    private CanvasGroup expandedGroup;
    private string expandedSignature = "";
    private float expandedOpenedAt, expandedScroll, expandedMaxScroll;
    private bool expandedVisible;
    private int expandedDevice = -1;

    private void UpdateExpanded(IReadOnlyList<string> owned, bool want)
    {
        string sig = shownSignature + Boons.Rerolls;
        if (want && (expanded == null || sig != expandedSignature || expandedDevice != GameInput.DeviceVersion)) BuildExpanded(owned, sig);
        if (expanded == null) return;
        if (want != expandedVisible)
        {
            expandedVisible = want;
            expanded.gameObject.SetActive(want);
            expandedOpenedAt = Time.unscaledTime;
            expandedScroll = 0f;
        }
        if (!want) return;
        float k = Mathf.Clamp01((Time.unscaledTime - expandedOpenedAt) / 0.2f);
        expanded.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, 1f - (1f - k) * (1f - k));
        expandedGroup.alpha = k;
        expandedScroll = Mathf.Clamp(expandedScroll - GameInput.MoveY * 900f * Time.unscaledDeltaTime, 0f, expandedMaxScroll);
        expandedContent.anchoredPosition = new Vector2(0f, Mathf.Round(expandedScroll));
    }

    private void BuildExpanded(IReadOnlyList<string> owned, string sig)
    {
        expandedSignature = sig;
        expandedDevice = GameInput.DeviceVersion;
        if (expanded != null) Destroy(expanded.gameObject);
        const float width = 1600f, height = 900f, colW = 740f, top = 130f, bottom = 70f;
        expanded = OverlayUI.MakeRect("Boon Build (expanded)", OverlayUI.Root);
        OverlayUI.Place(expanded, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
        expandedGroup = expanded.gameObject.AddComponent<CanvasGroup>();
        expandedGroup.blocksRaycasts = false;
        Image dim = OverlayUI.MakeImage("Dim", expanded, new Color(0.03f, 0.01f, 0.05f, 0.6f), OverlayUI.WhiteSprite);
        dim.rectTransform.anchorMin = dim.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dim.rectTransform.sizeDelta = new Vector2(4000f, 3000f);
        Image panel = OverlayUI.MakePanel("Panel", expanded);
        panel.rectTransform.anchorMin = Vector2.zero; panel.rectTransform.anchorMax = Vector2.one;
        panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;

        PixelText title = PixelText.Create(expanded, "YOUR BUILD", 6, new Color(1f, 0.82f, 0.3f), 0.5f);
        title.Rect.anchorMin = title.Rect.anchorMax = new Vector2(0.5f, 1f);
        title.Rect.anchoredPosition = new Vector2(0f, -52f);
        PixelText sub = PixelText.Create(expanded, owned.Count + " BOONS     HAIR GEL X" + Boons.Rerolls, 2, new Color(0.6f, 1f, 0.9f), 0.5f);
        sub.Rect.anchorMin = sub.Rect.anchorMax = new Vector2(0.5f, 1f);
        sub.Rect.anchoredPosition = new Vector2(0f, -98f);
        PixelText hint = PixelText.Create(expanded, GameInput.Format("{MOVE} SCROLL     {INTERACT} ZOOM OUT     {BACK} CLOSE"), 2, new Color(1f, 1f, 1f, 0.7f), 0.5f);
        hint.Rect.anchorMin = hint.Rect.anchorMax = new Vector2(0.5f, 0f);
        hint.Rect.anchoredPosition = new Vector2(0f, 34f);

        // scrolling viewport
        RectTransform view = OverlayUI.MakeRect("View", expanded);
        view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(30f, bottom); view.offsetMax = new Vector2(-30f, -top);
        view.gameObject.AddComponent<RectMask2D>();
        expandedContent = OverlayUI.MakeRect("Content", view);
        expandedContent.anchorMin = expandedContent.anchorMax = expandedContent.pivot = new Vector2(0.5f, 1f);
        expandedContent.sizeDelta = new Vector2(width - 60f, 10f);

        float[] colY = { -10f, -10f };
        int n = 0;
        foreach (string id in owned)
        {
            BoonDef d = BoonCatalog.Get(id);
            if (d == null) continue;
            int col = colY[1] > colY[0] ? 1 : 0; // the shorter column (left first)
            float x = col == 0 ? -colW - 10f : 10f;
            float y = colY[col];
            Rarity r = Boons.RarityOf(id);
            PatronInfo p = BoonCatalog.Of(d.patron);
            Color pc = d.IsDuo ? Color.Lerp(p.color, BoonCatalog.Of(d.partner.Value).color, 0.5f) : p.color;

            RectTransform card = OverlayUI.MakeRect("Boon " + d.name, expandedContent);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 1f);
            Image back = OverlayUI.MakeImage("Back", card, new Color(pc.r * 0.25f, pc.g * 0.2f, pc.b * 0.3f, 0.65f), OverlayUI.WhiteSprite);
            back.rectTransform.anchorMin = Vector2.zero; back.rectTransform.anchorMax = Vector2.one;
            back.rectTransform.offsetMin = back.rectTransform.offsetMax = Vector2.zero;
            Image stripe = OverlayUI.MakeImage("Stripe", card, BoonCatalog.RarityColors[(int)r], OverlayUI.WhiteSprite);
            stripe.rectTransform.anchorMin = new Vector2(0f, 0f); stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            stripe.rectTransform.sizeDelta = new Vector2(6f, 0f);

            Image plate = OverlayUI.MakeImage("Plate", card, pc, BoonIcons.Medallion);
            plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = plate.rectTransform.pivot = new Vector2(0f, 1f);
            plate.rectTransform.sizeDelta = new Vector2(84f, 84f);
            plate.rectTransform.anchoredPosition = new Vector2(20f, -14f);
            Image icon = OverlayUI.MakeImage("Icon", plate.rectTransform, d.iconTint, BoonIcons.Get(d));
            icon.preserveAspect = true;
            Fill(icon.rectTransform, 0.62f);

            PixelText name = PixelText.Create(card, d.name, 3, Color.white, 0f);
            name.Rect.anchorMin = name.Rect.anchorMax = new Vector2(0f, 1f);
            name.Rect.pivot = new Vector2(0f, 1f);
            name.Rect.anchoredPosition = new Vector2(122f, -16f);
            string who = d.IsDuo ? p.name + " + " + BoonCatalog.Of(d.partner.Value).name : p.name;
            PixelText tag = PixelText.Create(card, BoonCatalog.RarityNames[(int)r] + "  " + BoonCatalog.SlotNames[(int)d.slot] + "  -  " + who, 2, BoonCatalog.RarityColors[(int)r], 0f);
            tag.Rect.anchorMin = tag.Rect.anchorMax = new Vector2(0f, 1f);
            tag.Rect.pivot = new Vector2(0f, 1f);
            tag.Rect.anchoredPosition = new Vector2(122f, -50f);
            PixelText line = PixelText.Create(card, BoonPicker.Wrap(GameInput.Format(d.Describe(r)), 46), 2, new Color(0.9f, 0.86f, 0.96f), 0f);
            line.Rect.anchorMin = line.Rect.anchorMax = new Vector2(0f, 1f);
            line.Rect.pivot = new Vector2(0f, 1f);
            line.Rect.anchoredPosition = new Vector2(122f, -78f);

            float h = Mathf.Max(112f, 78f + line.Rect.sizeDelta.y + 16f);
            card.sizeDelta = new Vector2(colW, h);
            card.anchoredPosition = new Vector2(x + colW / 2f, y);
            colY[col] = y - h - 14f;
            n++;
        }
        float contentH = -Mathf.Min(colY[0], colY[1]);
        expandedContent.sizeDelta = new Vector2(width - 60f, contentH);
        expandedMaxScroll = Mathf.Max(0f, contentH - (height - top - bottom));
        expanded.gameObject.SetActive(false);
        expandedVisible = false;
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
        moonSprite = AIArt.Use("BoonHUD_MoonMeter", Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 64f));
        return moonSprite;
    }
}
