using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Who dealt a hit (set right before EnemyHealth.TakeDamageEnemy, see EnemyHealth.CreditNextHit)
public class KillCredit
{
    public enum Kind { Rowdy, Cat, World }

    public Kind kind;
    public string name;      // "ROWDY", "WIG", "NICK" ("" for world kills)
    public Sprite icon;      // weapon icon / cat face / water...
    public string with;      // weapon or cat name, for the stats screen
    public bool execution;   // Nick's finishing cut
    public enum Finish { Normal, Critical, Counter }

    public static KillCredit Rowdy()
    {
        WeaponManager weapons = WeaponManager.Instance;
        return new KillCredit
        {
            kind = Kind.Rowdy,
            name = "Rowdy",
            icon = weapons != null ? weapons.ActiveProfile : null,
            with = weapons != null ? weapons.ActiveName : "Rowdy",
        };
    }

    public static KillCredit Cat(PetFollower cat, bool execution = false)
    {
        return new KillCredit { kind = Kind.Cat, name = cat.CatName, icon = cat.Portrait, with = cat.CatName, execution = execution };
    }

    public static KillCredit Drowning() => new KillCredit { kind = Kind.World, name = "", icon = KillFeed.WaterIcon, with = "Water" };
}

// Counter-Strike style kill log in the top-right corner:  ROWDY [naginata] > [gnoll face] GNOLL WARRIOR
// The arrow says how it died: white = normal hit, red = critical, cyan = counter, gold = Nick's execution.
// Built at runtime on the overlay canvas the first time something dies.
public class KillFeed : MonoBehaviour
{
    private const int MaxRows = 5;
    private const float RowLifetime = 6f;
    private const float FadeTime = 0.6f;
    private const float RowHeight = 40f;
    private const float RowGap = 6f;
    private const float Margin = 18f;
    private const float IconSize = 32f;
    private const int TextScale = 2;

    private static readonly Color RowdyColor = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
    private static readonly Color CatColor = new Color32(0xFF, 0x9B, 0xE6, 0xFF);
    private static readonly Color VictimColor = new Color(0.92f, 0.9f, 0.95f, 1f);
    private static readonly Color PlateColor = new Color(0.08f, 0.02f, 0.1f, 0.72f);

    private class Row
    {
        public RectTransform rect;
        public CanvasGroup group;
        public float age;
    }

    private static KillFeed instance;
    private static Sprite waterIcon, skullIcon, arrowIcon;
    private static readonly Dictionary<string, (string name, Sprite portrait)> identities = new Dictionary<string, (string, Sprite)>();

    private RectTransform area;
    private readonly List<Row> rows = new List<Row>();

    // ---------------------------------------------------------------- reporting
    public static void Report(KillCredit killer, string victimName, Sprite victimPortrait, KillCredit.Finish finish = KillCredit.Finish.Normal)
    {
        if (!GameSettings.KillFeedOn) return;
        Get().AddRow(killer, victimName, victimPortrait, finish);
    }

    public static readonly Color NormalArrow = new Color(0.95f, 0.92f, 0.98f, 1f);
    public static readonly Color CriticalArrow = new Color32(0xFF, 0x3A, 0x3A, 0xFF);   // same red as CRITICAL!
    public static readonly Color CounterArrow = new Color32(0x3C, 0xE8, 0xFF, 0xFF);    // same cyan as COUNTER!
    public static readonly Color ExecutionArrow = new Color32(0xFF, 0xC9, 0x3C, 0xFF);

    private static KillFeed Get()
    {
        if (instance == null)
        {
            var go = new GameObject("KillFeed (auto)");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<KillFeed>();
        }
        return instance;
    }

    private void Awake()
    {
        area = OverlayUI.MakeRect("Kill Feed", OverlayUI.Root);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = area.offsetMax = Vector2.zero;
    }

    private void Update()
    {
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            Row row = rows[i];
            row.age += Time.unscaledDeltaTime;
            float left = RowLifetime - row.age;
            row.group.alpha = Mathf.Clamp01(left / FadeTime);
            if (left <= 0f) RemoveRow(i);
        }
    }

    private void AddRow(KillCredit killer, string victimName, Sprite portrait, KillCredit.Finish finish)
    {
        if (rows.Count >= MaxRows) RemoveRow(0);

        var row = new Row { rect = OverlayUI.MakeRect("Kill", area) };
        row.group = row.rect.gameObject.AddComponent<CanvasGroup>();
        row.group.blocksRaycasts = false;

        bool world = killer == null || killer.kind == KillCredit.Kind.World || string.IsNullOrEmpty(killer.name);
        bool cat = killer != null && killer.kind == KillCredit.Kind.Cat;

        // Plate (framed for Rowdy's / his cats' kills, like CS highlights your own)
        Image plate = world ? OverlayUI.MakeImage("Plate", row.rect, PlateColor) : OverlayUI.MakePanel("Plate", row.rect);
        if (!world)
        {
            plate.pixelsPerUnitMultiplier = OverlayUI.SlicedMultiplier * 2f; // thinner frame than the HUD panels
            plate.color = new Color(1f, 1f, 1f, 0.92f);
        }

        // Pieces left to right: [killer] [icon] [portrait] [victim]
        var pieces = new List<RectTransform>();
        if (!world)
        {
            PixelText killerText = PixelText.Create(row.rect, killer.name, TextScale, cat ? CatColor : RowdyColor, 0f);
            pieces.Add(killerText.Rect);
        }

        Sprite icon = killer != null && killer.icon != null ? killer.icon : (world ? SkullIcon : null);
        if (icon != null) pieces.Add(MakeIcon(row.rect, icon, killer != null && killer.execution));
        if (!world)
        {
            Color arrowColor = killer.execution ? ExecutionArrow : finish == KillCredit.Finish.Counter ? CounterArrow
                             : finish == KillCredit.Finish.Critical ? CriticalArrow : NormalArrow;
            Image arrow = OverlayUI.MakeImage("Arrow", row.rect, arrowColor, ArrowIcon);
            arrow.rectTransform.sizeDelta = new Vector2(IconSize * 0.75f, IconSize * 0.75f);
            pieces.Add(arrow.rectTransform);
        }
        if (portrait != null) pieces.Add(MakeIcon(row.rect, portrait, false));

        PixelText victimText = PixelText.Create(row.rect, victimName, TextScale, VictimColor, 0f);
        pieces.Add(victimText.Rect);

        // Lay them out
        const float pad = 12f, gap = 8f;
        float x = pad;
        foreach (RectTransform piece in pieces)
        {
            piece.anchorMin = piece.anchorMax = new Vector2(0f, 0.5f);
            piece.pivot = new Vector2(0f, 0.5f);
            piece.anchoredPosition = new Vector2(Mathf.Round(x), 0f);
            x += piece.sizeDelta.x + gap;
        }
        float width = x - gap + pad;

        OverlayUI.Place(plate.rectTransform, new Vector2(0f, 0f), Vector2.zero, new Vector2(width, RowHeight));
        row.rect.sizeDelta = new Vector2(width, RowHeight);
        row.rect.SetAsLastSibling();

        rows.Add(row);
        Relayout();
    }

    private static RectTransform MakeIcon(RectTransform parent, Sprite sprite, bool gold)
    {
        Image image = OverlayUI.MakeImage("Icon", parent, gold ? new Color(1f, 0.85f, 0.4f) : Color.white, sprite);
        image.preserveAspect = true;
        image.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);
        return image.rectTransform;
    }

    private void RemoveRow(int index)
    {
        if (rows[index].rect != null) Destroy(rows[index].rect.gameObject);
        rows.RemoveAt(index);
        Relayout();
    }

    // Oldest on top, newest at the bottom, all right-aligned under the top-right corner
    private void Relayout()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            RectTransform rect = rows[i].rect;
            OverlayUI.Place(rect, new Vector2(1f, 1f), new Vector2(-Margin, -Margin - i * (RowHeight + RowGap)));
        }
    }

    // ---------------------------------------------------------------- who died
    // Name + face: the enemy's (unused) EnemyHUD 'Name' / 'Portrait' if it has one, otherwise its name and a
    // head crop of its sprite. Cached per enemy type.
    public static void Describe(EnemyHealth enemy, out string name, out Sprite portrait)
    {
        // Known enemy types: catalog name + generated head portrait (Resources/EnemyPortraits)
        EnemyCatalog.Entry entry = EnemyCatalog.Identify(enemy);
        if (entry != null)
        {
            name = entry.name;
            if (entry.fallbackPortrait == null && entry.Portrait == null) entry.fallbackPortrait = HeadCropOf(enemy);
            portrait = entry.Portrait;
            return;
        }

        string key = CleanObjectName(enemy.gameObject.name);
        if (identities.TryGetValue(key, out var known) && (known.portrait != null || !string.IsNullOrEmpty(known.name)))
        {
            name = known.name;
            portrait = known.portrait;
            return;
        }

        name = null;
        portrait = null;
        // The enemy itself, or its prefab's container (e.g. the horse rider's patrol-point holder) if it holds only this enemy
        Transform search = enemy.transform;
        Transform parent = enemy.transform.parent;
        if (parent != null && parent.GetComponentsInChildren<EnemyHealth>(true).Length == 1) search = parent;
        foreach (TMP_Text text in search.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name != "Name") continue;
            string first = text.text != null ? text.text.Split('\n')[0].Trim() : "";
            if (first.Length > 0) { name = first; break; }
        }
        foreach (Image image in search.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name == "Portrait" && image.sprite != null) { portrait = image.sprite; break; }
        }

        if (string.IsNullOrEmpty(name)) name = EnemyCatalog.PrettyName(enemy.gameObject.name);
        if (portrait == null) portrait = HeadCropOf(enemy);

        identities[key] = (name, portrait);
    }

    // Fallback face when there's no generated portrait yet
    public static Sprite HeadCropOf(EnemyHealth enemy)
    {
        return enemy != null && enemy.TryGetComponent(out SpriteRenderer sr) && sr.sprite != null ? HeadCrop(sr.sprite) : null;
    }

    private static string CleanObjectName(string objectName)
    {
        string n = objectName.Replace("(Clone)", "").Trim();
        int paren = n.IndexOf(" (");
        if (paren > 0) n = n.Substring(0, paren);
        if (n.StartsWith("Enemy_")) n = n.Substring(6);
        return n;
    }

    // Square crop around the top of the sprite's visible pixels (its head)
    private static Sprite HeadCrop(Sprite sprite)
    {
        try
        {
            Texture2D source = sprite.texture;
            Rect r = sprite.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            if (w <= 0 || h <= 0) return null;

            // Copy through a RenderTexture so it works on non-readable textures
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active; // (before Blit, which makes rt the active one)
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
            read.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color32[] px = read.GetPixels32();
            int top = -1, minX = w, maxX = -1;
            for (int y = h - 1; y >= 0 && top < 0; y--)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 40) { top = y; break; }
            if (top < 0) { Object.Destroy(read); return null; }

            int size = Mathf.Clamp(Mathf.RoundToInt(h * 0.38f), 8, 28);
            int bottom = Mathf.Max(0, top - size + 1);
            for (int y = bottom; y <= top; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 40) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
            int centerX = (minX + maxX) / 2;
            int left = Mathf.Clamp(centerX - size / 2, 0, Mathf.Max(0, w - size));
            int cw = Mathf.Min(size, w), ch = top - bottom + 1;

            var crop = new Texture2D(cw, ch, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "HeadCrop" };
            crop.SetPixels32(0, 0, cw, ch, Slice(px, w, left, bottom, cw, ch));
            crop.Apply(false, true);
            Object.Destroy(read);
            return Sprite.Create(crop, new Rect(0, 0, cw, ch), new Vector2(0.5f, 0.5f), 16f);
        }
        catch (System.Exception)
        {
            return null; // tightly packed atlas sprite etc.: just no portrait
        }
    }

    private static Color32[] Slice(Color32[] px, int w, int x0, int y0, int cw, int ch)
    {
        var result = new Color32[cw * ch];
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
                result[y * cw + x] = px[(y0 + y) * w + x0 + x];
        return result;
    }

    // ---------------------------------------------------------------- generated icons
    public static Sprite WaterIcon
    {
        get
        {
            if (waterIcon == null)
            {
                waterIcon = OverlayUI.PixelSprite(new[]
                {
                    "................",
                    "................",
                    "................",
                    "..LL......LL....",
                    ".LWWL....LWWL...",
                    "LW..WL..LW..WL..",
                    "......LL......LL",
                    "................",
                    "..LL......LL....",
                    ".LWWL....LWWL...",
                    "LW..WL..LW..WL..",
                    "......LL......LL",
                    "................",
                    "................",
                    "................",
                    "................",
                }, c => c == 'W' ? new Color32(0xC8, 0xF4, 0xFF, 0xFF) : new Color32(0x3C, 0xA8, 0xE8, 0xFF), "WaterIcon");
            }
            return waterIcon;
        }
    }

    // White arrow, tinted per kill type
    private static Sprite ArrowIcon
    {
        get
        {
            if (arrowIcon == null)
            {
                arrowIcon = OverlayUI.PixelSprite(new[]
                {
                    "............",
                    "......OO....",
                    "......OWO...",
                    "OOOOOOOWWO..",
                    "OWWWWWWWWWO.",
                    "OWWWWWWWWWWO",
                    "OWWWWWWWWWO.",
                    "OOOOOOOWWO..",
                    "......OWO...",
                    "......OO....",
                    "............",
                    "............",
                }, c => c == 'W' ? new Color32(0xFF, 0xFF, 0xFF, 0xFF) : new Color32(0x3A, 0x30, 0x40, 0xFF), "KillArrow");
            }
            return arrowIcon;
        }
    }

    private static Sprite SkullIcon
    {
        get
        {
            if (skullIcon == null)
            {
                skullIcon = OverlayUI.PixelSprite(new[]
                {
                    "................",
                    "................",
                    "....OOOOOOOO....",
                    "...OWWWWWWWWO...",
                    "..OWWWWWWWWWWO..",
                    "..OWWWWWWWWWWO..",
                    "..OWOOWWWWOOWO..",
                    "..OWOOWWWWOOWO..",
                    "..OWWWWOOWWWWO..",
                    "...OWWWWWWWWO...",
                    "....OWOWOWOWO...",
                    "....OOOOOOOOO...",
                    "................",
                    "................",
                    "................",
                    "................",
                }, c => c == 'W' ? new Color32(0xEE, 0xE6, 0xF2, 0xFF) : new Color32(0x1B, 0x08, 0x20, 0xFF), "SkullIcon");
            }
            return skullIcon;
        }
    }
}
