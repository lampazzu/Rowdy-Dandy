using System.Collections.Generic;
using UnityEngine;

// The art the runtime-built pickups / the Moonbound Elder use, in one place: Resources/ItemArt.asset.
// Filled in automatically by Editor/ItemArtSetup.cs from "New Stuff you can use" (and the cat treat fish in
// Resources/Pickups) - swap a texture here to change the art. Sheets are cut into frames at runtime on a grid.
[CreateAssetMenu(menuName = "Rowdy Dandy/Item Art")]
public class ItemArt : ScriptableObject
{
    [Header("Moonbound Elder (TDF_SPW_WerewolfUnit: 15 frames of 156 x 108)")]
    public Texture2D werewolf;

    [Header("Flying Rat (PIV_Flying_Rat: 10 x 2 grid - top row flying / zap, bottom row the 'registered' burst)")]
    public Texture2D flyingRat;

    [Header("Ores")]
    [Tooltip("DMG_OreBreak: 10 frames side by side; frame 0 is the intact ore")]
    public Texture2D oreBreak;
    public Texture2D emerald, sapphire, ruby, crystals;
    [Tooltip("Ores are scattered on the ground of every level at fixed (seeded) spots; broken ones stay broken (saved).")]
    public bool scatterOres = true;
    public int oresPerLevel = 14;

    [Header("Cat treat fish (Resources/AI Placeholders/Pickups/CatTreat_Fish.png, frames side by side)")]
    public Texture2D fish;
    public int fishFrames = 2;

    [Header("Effects (TBZG_VFX_*: 10 frames of 214 x 172)")]
    public Texture2D vfxHeal;
    public Texture2D vfxPoison;
    public Texture2D vfxDecay;
    public Texture2D vfxBlock;

    [Header("Status icons (STS_Stun: 18 frames of 19 x 21, STS_Shield: 6 frames of 24 x 22, last 3 = cracking)")]
    public Texture2D stsStun;
    public Texture2D stsShield;

    [Header("Ground pound (FX_GroundPound: 12 frames of 128 x 128) - Mushidon's stomp")]
    public Texture2D groundPound;

    [Header("Blood spatter (VFX_BloodA/B/C: 4 frames of 156 x 94)")]
    public Texture2D[] blood;

    [Header("Pointer arrow (TBZG_Pointer: 6 frames of 48 x 64) - Pelich's weak spot")]
    public Texture2D pointer;

    // ---------------------------------------------------------------- runtime access
    private static ItemArt instance;
    private static bool loaded;

    public static ItemArt Get
    {
        get
        {
            if (!loaded) { loaded = true; instance = Resources.Load<ItemArt>("ItemArt"); }
            return instance;
        }
    }

    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

    // Cuts a sheet into columns x rows frames (row 0 = top row), all with the same pivot. Cached.
    public static Sprite[] Frames(Texture2D tex, int columns, int rows, Vector2 pivot, float pixelsPerUnit)
    {
        if (tex == null) return null;
        string key = tex.GetInstanceID() + "_" + columns + "x" + rows + "_" + pivot + "_" + pixelsPerUnit;
        if (cache.TryGetValue(key, out Sprite[] s)) return s;
        tex.filterMode = FilterMode.Point;
        float w = tex.width / (float)columns, h = tex.height / (float)rows;
        s = new Sprite[columns * rows];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
            {
                float x0 = Mathf.Round(c * w), x1 = Mathf.Round((c + 1) * w);
                float y0 = Mathf.Round(tex.height - (r + 1) * h), y1 = Mathf.Round(tex.height - r * h);
                s[r * columns + c] = Sprite.Create(tex, new Rect(x0, y0, x1 - x0, y1 - y0), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
        cache[key] = s;
        return s;
    }

    public static Sprite Single(Texture2D tex, float pixelsPerUnit = 64f) => tex == null ? null : Frames(tex, 1, 1, new Vector2(0.5f, 0.5f), pixelsPerUnit)[0];

    private static Material unlit;
    public static Material Unlit
    {
        get
        {
            if (unlit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) unlit = new Material(shader) { name = "Item (Unlit)" };
            }
            return unlit;
        }
    }

    // Lit like the level (pickups lying in the world)
    private static Material lit;
    public static Material Lit
    {
        get
        {
            if (lit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) lit = new Material(shader) { name = "Item (Lit)" };
            }
            return lit;
        }
    }
}
