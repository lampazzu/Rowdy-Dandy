using UnityEngine;

// The ability cats (Paprika, Mushidon, The Peak, Lallo, Tchogon) run on Wig's animator; their own art lives in
// Resources/CatSkins/RDR_<name>.png (copied from "New Cat skin files": 12 frames of 78 x 86, the same layout as
// RDR_Wig). After the animator picks Wig's frame N, this draws frame N of the cat's own sheet instead, at the same
// pivot. CatSkins/CatPortrait_<name>.png is its HUD face (16 x 16: the 8 x 8 head of frame 0 at x2, like Wig / Nick).
public class CatSkin : MonoBehaviour
{
    private const string WigPrefix = "RDR_Wig_";
    private SpriteRenderer sr;
    private Texture2D sheet;
    private Sprite[] frames;
    private Sprite lastSource;

    public static string SkinName(PetFollower.CatType type)
    {
        switch (type)
        {
            case PetFollower.CatType.Paprika: return "Paprika";
            case PetFollower.CatType.Mushidon: return "Mushidon";
            case PetFollower.CatType.Peak: return "ThePeak";
            case PetFollower.CatType.Lallo: return "Lallo";
            case PetFollower.CatType.Tchogon: return "Tchogon";
        }
        return null;
    }

    public static bool Attach(SpriteRenderer sr, string skin)
    {
        if (sr == null || string.IsNullOrEmpty(skin)) return false;
        Texture2D t = Resources.Load<Texture2D>("CatSkins/RDR_" + skin);
        if (t == null) return false;
        var s = sr.gameObject.AddComponent<CatSkin>();
        s.sr = sr;
        s.sheet = t;
        return true;
    }

    public static Sprite Portrait(string skin)
    {
        Texture2D t = string.IsNullOrEmpty(skin) ? null : Resources.Load<Texture2D>("CatSkins/CatPortrait_" + skin);
        return t == null ? null : ItemArt.Frames(t, 1, 1, new Vector2(0.5f, 0.5f), 64f)[0];
    }

    private void LateUpdate()
    {
        if (sr == null || sheet == null) return;
        Sprite source = sr.sprite;
        if (source == null || source == lastSource) return;
        if (!source.name.StartsWith(WigPrefix) || !int.TryParse(source.name.Substring(WigPrefix.Length), out int i)) return;
        if (frames == null)
        {
            Vector2 pivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
            frames = ItemArt.Frames(sheet, Mathf.Max(1, sheet.width / Mathf.RoundToInt(source.rect.width)), 1, pivot, source.pixelsPerUnit);
        }
        Sprite skin = frames[Mathf.Clamp(i, 0, frames.Length - 1)];
        sr.sprite = skin;
        lastSource = skin; // the animator writes Wig's frame again next time it changes
    }
}
