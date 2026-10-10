using UnityEditor;
using UnityEngine;

// Creates / fills Resources/BoonArt.asset (the art + sounds the level-up boons use) and gives the sheets pixel-art
// import settings. Sheets are also made readable: the boon icons are cropped to the drawn pixels at runtime.
// Runs by itself after compiling; re-run with Tools > Rowdy Dandy > Setup Boon Art.
[InitializeOnLoad]
public static class BoonArtSetup
{
    private const string AssetPath = "Assets/Resources/BoonArt.asset";
    private const string NewStuff = "Assets/Rowdy Dandy/New Stuff you can use/";
    private const string Sfx = "Assets/Rowdy Dandy/Sound Effects/";
    private const string Hags = Sfx + "Sounds from hags hand/";

    static BoonArtSetup() => EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Setup(false); };

    [MenuItem("Tools/Rowdy Dandy/Setup Boon Art")]
    private static void SetupMenu() => Setup(true);

    private static void Setup(bool verbose)
    {
        BoonArt art = AssetDatabase.LoadAssetAtPath<BoonArt>(AssetPath);
        bool created = false;
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<BoonArt>();
            AssetDatabase.CreateAsset(art, AssetPath);
            created = true;
        }

        bool changed = created;
        changed |= Fill(ref art.clawSlash, NewStuff + "FX_CapeloboSlash.png");
        changed |= Fill(ref art.waterSonic, NewStuff + "VFX_WaterSonic.png");
        changed |= Fill(ref art.charm, NewStuff + "STS_Charm.png");
        changed |= Fill(ref art.fear, NewStuff + "STS_Fear.png");
        changed |= Fill(ref art.slow, NewStuff + "STS_Slow.png");
        changed |= Fill(ref art.charge, NewStuff + "TBZG_VFX_Charge.png");
        changed |= Fill(ref art.magicalHit, NewStuff + "TBZG_VFX_MagicalHit.png");
        changed |= Fill(ref art.physicalHit, NewStuff + "TBZG_VFX_PhysicalHit.png");
        changed |= Fill(ref art.manaRecovery, NewStuff + "TBZG_VFX_ManaRecovery.png");
        changed |= Fill(ref art.ail, NewStuff + "TBZG_VFX_Ail.png");
        changed |= Fill(ref art.earthPillar, NewStuff + "RDR_EarthPillar.png");
        changed |= Fill(ref art.matinta, NewStuff + "TDF_Matinta@Projectile.png");
        changed |= Fill(ref art.gunImpact, NewStuff + "PIV_GunImpact.png");
        changed |= Fill(ref art.magicCircle, NewStuff + "FX_MagicPlacement.png");
        changed |= Fill(ref art.sparkBurst, NewStuff + "FX_DeckFix_Upgrade.png");
        changed |= Fill(ref art.groundPound, NewStuff + "FX_GroundPound.png");
        changed |= Fill(ref art.pinkWave, "Assets/Misc/Test/testWave.png");
        changed |= Fill(ref art.flora, "Assets/Scenery/Assets/RDR_Flowers.png");

        // the Crazy Chef's food (.aseprite files: the sprite inside each)
        string[] foods = { "ovo", "pao", "tomate", "guejo", "armondega", "arface" };
        if (art.food == null || art.food.Length != foods.Length) { art.food = new Sprite[foods.Length]; changed = true; }
        for (int i = 0; i < foods.Length; i++)
        {
            if (art.food[i] != null) continue;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(NewStuff + foods[i] + ".aseprite"))
                if (o is Sprite s) { art.food[i] = s; changed = true; break; }
            if (art.food[i] == null) Debug.LogWarning("Boon art: no sprite in " + foods[i] + ".aseprite");
        }

        if (art.rarity == null || art.rarity.Length != 5) { art.rarity = new AudioClip[5]; changed = true; }
        string[] rarityFiles = { "01_MagicPlacement_Commom", "02_MagicPlacement_Uncommom", "03_MagicPlacement_Rare", "04_MagicPlacement_Epic", "05_MagicPlacement_Legendary" };
        for (int i = 0; i < 5; i++) changed |= Fill(ref art.rarity[i], Hags + "MagicPlacement/" + rarityFiles[i] + ".wav");
        if (art.cardHover == null || art.cardHover.Length != 5) { art.cardHover = new AudioClip[5]; changed = true; }
        for (int i = 0; i < 5; i++) changed |= Fill(ref art.cardHover[i], Hags + "CardHover/CardHover" + (i + 1) + ".wav");
        if (art.rarityLayer == null || art.rarityLayer.Length != 5) { art.rarityLayer = new AudioClip[5]; changed = true; }
        string[] layerFiles = { "01_TurretPlacement_Commom", "02_TurretPlacement_Umcommom", "03_TurretPlacement_Rare", "04_TurretPlacement_Epic", "05_TurretPlacement_Legendary" };
        for (int i = 0; i < 5; i++) changed |= Fill(ref art.rarityLayer[i], Hags + "TurretPlacement/" + layerFiles[i] + ".wav");
        changed |= Fill(ref art.open, Hags + "WaveStart/WaveStart.mp3");
        changed |= Fill(ref art.whoosh, Sfx + "whoosh-cinematic-161021 1.mp3");
        changed |= Fill(ref art.coin, Hags + "BuyDrawCard/Buy XP.wav");
        changed |= Fill(ref art.swap, Hags + "DeckFix/DeckFixSwap.wav");
        changed |= Fill(ref art.sparkle, Sfx + "Misc_MissionComplete.mp3");
        changed |= Fill(ref art.cardPick, Hags + "UIClickCard/UIClickCard.wav");
        changed |= Fill(ref art.reroll, Hags + "BuyDrawCard/BuyDrawCard.wav");
        changed |= Fill(ref art.upgrade, Hags + "DeckFix/DeckFixUpgrade.wav");
        changed |= Fill(ref art.fanfare, Hags + "UpgradeLevel/UpgradeLevel.wav");
        changed |= Fill(ref art.cancel, Hags + "CardCancel/CardCancel.wav");

        changed |= Fill(ref art.narcissism, Hags + "Cards/Tower/MaeDOuro/MaeDouro Spawn.wav");
        changed |= Fill(ref art.abyss, Hags + "Cards/Tower/Iara/Iara Spawn.wav");
        changed |= Fill(ref art.lycanthropy, Hags + "Cards/Tower/Lobisomem/Werewolf Spawn.wav");
        changed |= Fill(ref art.rot, Hags + "Cards/Tower/Curupira/Curupira Spawn.wav");
        changed |= Fill(ref art.guild, Sfx + "RedVelvet Lines/Wig/Main Voice - Attack Build 1.wav");
        changed |= Fill(ref art.chef, Hags + "Cards/Tower/Saci/Saci Spawn.wav");
        changed |= Fill(ref art.smith, Hags + "Cards/Tower/Mapinguari/Mapinguari Spawn.wav");
        changed |= Fill(ref art.sun, Hags + "Cards/Magic/Boitata/Tatá Summon.wav");

        changed |= Fill(ref art.charmSfx, Hags + "Status/Enemy Charm Status.wav");
        changed |= Fill(ref art.fearSfx, Hags + "Status/Enemy Fear Status.wav");
        changed |= Fill(ref art.stunSfx, Hags + "Status/Enemy Stun Status.wav");
        changed |= Fill(ref art.wolfTransform, Sfx + "TransformWolfV2.mp3");
        changed |= Fill(ref art.wolfSpawn, Hags + "Cards/Tower/Lobisomem/Werewolf Spawn.wav");
        changed |= Fill(ref art.wolfHowl, Sfx + "Enemies/HowlingFX2.mp3");
        changed |= Fill(ref art.claw, Hags + "Cards/Tower/Capelobo/Capelobo Attack.wav");
        changed |= Fill(ref art.glassBreak, Sfx + "glass-breaking-93803.mp3");
        changed |= Fill(ref art.zap, Sfx + "electric-impact-37128.mp3");
        changed |= Fill(ref art.tornado, Hags + "Cards/Tower/Saci/Saci Tornado.wav");
        changed |= Fill(ref art.waveCrash, Hags + "Cards/Tower/Iara/Iara Attack.mp3");
        changed |= Fill(ref art.waterBoom, Hags + "Cards/Tower/Iara/Iara Explosion.mp3");
        changed |= Fill(ref art.boto, Hags + "Cards/Magic/Boto/Boto Attack.wav");
        changed |= Fill(ref art.splash, Hags + "Cards/Tower/Iara/Iara Explosion.mp3"); // (the beach ambience was silent for its first seconds)
        changed |= Fill(ref art.vines, Hags + "Cards/Tower/Curupira/Curupira Attack.mp3");
        changed |= Fill(ref art.sporePop, Hags + "Cards/Magic/Anhanga/Anhanga.wav");
        changed |= Fill(ref art.rockBreak, Sfx + "grab-gravel-2-36335.mp3");
        changed |= Fill(ref art.clang, Sfx + "Enemies/Player_Gauntlet_Hit_V2.wav");
        changed |= Fill(ref art.sizzle, Sfx + "fire-breath-6922.mp3");
        changed |= Fill(ref art.sunBeam, Sfx + "Enemies/Treeman_MeteorImpact.mp3");
        changed |= Fill(ref art.chomp, Sfx + "Imp_Death(crunch).wav");
        changed |= Fill(ref art.squish, Sfx + "Enemies/Regular_Blood_Hit.wav");
        changed |= Fill(ref art.plop, Sfx + "Cleric_Land.mp3");
        changed |= Fill(ref art.heal, Sfx + "PelicanHeartSFX.mp3");
        changed |= Fill(ref art.hairFlip, Sfx + "whoosh-cinematic-161021.mp3");
        changed |= Fill(ref art.pose, Hags + "Cards/Tower/MaeDOuro/MaeDouro Idle.wav");
        changed |= Fill(ref art.catPounce, Sfx + "RedVelvet Lines/Nick/Main Voice - Attack 1.wav");
        changed |= Fill(ref art.catCall, Sfx + "RedVelvet Lines/Wig/Main Voice - Attack Build 2.wav");
        changed |= Fill(ref art.nineLives, Sfx + "Enemies/Misc_Teleport.mp3");
        changed |= Fill(ref art.bigBoom, Sfx + "Curupira Explosion.mp3");
        changed |= Fill(ref art.gel, Hags + "DeckFix/DeckFixRepair.wav");

        foreach (Texture2D t in new[] { art.clawSlash, art.waterSonic, art.charm, art.fear, art.slow, art.charge, art.magicalHit, art.physicalHit,
                                        art.manaRecovery, art.ail, art.earthPillar, art.matinta, art.gunImpact, art.magicCircle, art.sparkBurst, art.groundPound, art.pinkWave })
            PixelImport(t, true);

        // The item-art sheets some boon icons are cut from need to be readable too
        ItemArt items = AssetDatabase.LoadAssetAtPath<ItemArt>("Assets/Resources/ItemArt.asset");
        if (items != null)
            foreach (Texture2D t in new[] { items.werewolf, items.vfxPoison, items.vfxHeal, items.vfxDecay, items.vfxBlock })
                PixelImport(t, true);

        if (changed)
        {
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
        }
        if (verbose || created) Debug.Log("Boon art: Resources/BoonArt.asset ready.");
    }

    private static bool Fill<T>(ref T slot, string path) where T : Object
    {
        if (slot != null) return false;
        slot = AssetDatabase.LoadAssetAtPath<T>(path);
        if (slot == null) Debug.LogWarning("Boon art: missing " + path);
        return slot != null;
    }

    private static void PixelImport(Texture2D tex, bool readable)
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
        if (readable && !importer.isReadable) { importer.isReadable = true; dirty = true; }
        if (dirty) importer.SaveAndReimport();
    }
}
