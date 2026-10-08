using UnityEditor;
using UnityEngine;

// Creates / fills Resources/ItemArt.asset (art for the rat, ores, cat treat and the Moonbound Elder) and gives
// those textures pixel-art import settings: point filter, no compression, big enough max size that the
// 2340 px werewolf sheet isn't shrunk. Runs by itself after compiling; re-run with Tools > Rowdy Dandy > Setup Item Art.
[InitializeOnLoad]
public static class ItemArtSetup
{
    private const string AssetPath = "Assets/Resources/ItemArt.asset";
    private const string NewStuff = "Assets/Rowdy Dandy/New Stuff you can use/";

    static ItemArtSetup() => EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Setup(false); };

    [MenuItem("Tools/Rowdy Dandy/Setup Item Art")]
    private static void SetupMenu() => Setup(true);

    private static void Setup(bool verbose)
    {
        ItemArt art = AssetDatabase.LoadAssetAtPath<ItemArt>(AssetPath);
        bool created = false;
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<ItemArt>();
            AssetDatabase.CreateAsset(art, AssetPath);
            created = true;
        }

        bool changed = created;
        changed |= Fill(ref art.werewolf, NewStuff + "TDF_SPW_WerewolfuNIT.png");
        changed |= Fill(ref art.flyingRat, NewStuff + "PIV_Flying_Rat.png");
        changed |= Fill(ref art.oreBreak, NewStuff + "DMG_OreBreak.png");
        changed |= Fill(ref art.emerald, NewStuff + "DMG_Ore@Emerald.png");
        changed |= Fill(ref art.sapphire, NewStuff + "DMG_Ore@Sapphire.png");
        changed |= Fill(ref art.ruby, NewStuff + "DMG_Ore@Ruby.png");
        changed |= Fill(ref art.crystals, NewStuff + "DMG_Ore@Crystals.png");
        changed |= Fill(ref art.fish, "Assets/Resources/Pickups/CatTreat_Fish.png");

        changed |= Fill(ref art.vfxHeal, NewStuff + "TBZG_VFX_Heal.png");
        changed |= Fill(ref art.vfxPoison, NewStuff + "TBZG_VFX_Poison.png");
        changed |= Fill(ref art.vfxDecay, NewStuff + "TBZG_VFX_Decay.png");
        changed |= Fill(ref art.vfxBlock, NewStuff + "TBZG_VFX_Block.png");
        changed |= Fill(ref art.stsStun, NewStuff + "STS_Stun.png");
        changed |= Fill(ref art.stsShield, NewStuff + "STS_Shield.png");
        changed |= Fill(ref art.groundPound, NewStuff + "FX_GroundPound.png");
        changed |= Fill(ref art.pointer, NewStuff + "TBZG_Pointer.png");
        if (art.blood == null || art.blood.Length == 0)
        {
            art.blood = new Texture2D[3];
            Fill(ref art.blood[0], NewStuff + "VFX_BloodA.png");
            Fill(ref art.blood[1], NewStuff + "VFX_BloodB.png");
            Fill(ref art.blood[2], NewStuff + "VFX_BloodC.png");
            changed = true;
        }

        foreach (Texture2D t in new[] { art.werewolf, art.flyingRat, art.oreBreak, art.emerald, art.sapphire, art.ruby, art.crystals, art.fish,
                                        art.vfxHeal, art.vfxPoison, art.vfxDecay, art.vfxBlock, art.stsStun, art.stsShield, art.groundPound, art.pointer })
            PixelImport(t);
        if (art.blood != null) foreach (Texture2D t in art.blood) PixelImport(t);

        if (changed)
        {
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
        }
        if (verbose || created) Debug.Log("Item art: Resources/ItemArt.asset ready.");
    }

    private static bool Fill(ref Texture2D slot, string path)
    {
        if (slot != null) return false;
        slot = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (slot == null) Debug.LogWarning("Item art: missing " + path);
        return slot != null;
    }

    private static void PixelImport(Texture2D tex)
    {
        if (tex == null) return;
        string path = AssetDatabase.GetAssetPath(tex);
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
        if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
        if (importer.maxTextureSize < 4096) { importer.maxTextureSize = 4096; dirty = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
        if (dirty) importer.SaveAndReimport();
    }
}
