using UnityEngine;

// Art + sounds the boons use, in one place: Resources/BoonArt.asset. Filled automatically by Editor/BoonArtSetup.cs
// from "New Stuff you can use" and the Sound Effects folder (hags hand cards, status sounds...). Swap anything here.
// Patron portraits: drop a PNG at Resources/BoonPortraits/<Patron enum name>.png (e.g. Narcissism.png) and the boon
// picker shows it instead of the patron's emblem.
[CreateAssetMenu(menuName = "Rowdy Dandy/Boon Art")]
public class BoonArt : ScriptableObject
{
    [Header("Sheets (frames side by side)")]
    public Texture2D clawSlash;      // FX_CapeloboSlash, 6 x 64x64
    public Texture2D waterSonic;     // VFX_WaterSonic, 9 x 96x80
    public Texture2D charm;          // STS_Charm, 9 x 41x36
    public Texture2D fear;           // STS_Fear, 9 x 54x74
    public Texture2D slow;           // STS_Slow, 4 x 54x15
    public Texture2D charge;         // TBZG_VFX_Charge, 10 x 214x172
    public Texture2D magicalHit;     // TBZG_VFX_MagicalHit
    public Texture2D physicalHit;    // TBZG_VFX_PhysicalHit
    public Texture2D manaRecovery;   // TBZG_VFX_ManaRecovery
    public Texture2D ail;            // TBZG_VFX_Ail
    public Texture2D earthPillar;    // RDR_EarthPillar, 1 x 19x65
    public Texture2D matinta;        // TDF_Matinta@Projectile, 5 x 306x316
    public Texture2D gunImpact;      // PIV_GunImpact
    public Texture2D magicCircle;    // FX_MagicPlacement, 17 x 229x206 (Solar Flare sigil)
    public Texture2D sparkBurst;     // FX_DeckFix_Upgrade, 10 x 338x358
    public Texture2D groundPound;    // FX_GroundPound, 12 x 128x128 (Anvil Drop)
    public Texture2D pinkWave;       // Misc/Test/testWave.png, the pink wave (Beach Bod)
    public Texture2D flora;          // Scenery/Assets/RDR_Flowers.png, 3 x 32x32: purple flower, mushrooms, blue flower (FloraArt)

    [Header("Food (the Crazy Chef): ovo, pao, tomate, guejo, armondega, arface")]
    public Sprite[] food = new Sprite[6];

    [Header("Picker sounds")]
    public AudioClip[] rarity = new AudioClip[5];   // common, rare, epic, legendary, duo (MagicPlacement 01-05)
    public AudioClip[] rarityLayer = new AudioClip[5]; // TurretPlacement 01-05, layered under the card landing
    public AudioClip[] cardHover = new AudioClip[5];
    public AudioClip cardPick, reroll, upgrade, fanfare, cancel;
    public AudioClip open, whoosh, coin, swap, sparkle;

    [Header("Patron sounds (when you take one of their boons)")]
    public AudioClip narcissism, abyss, lycanthropy, rot, guild, chef, smith, sun;

    [Header("Effect sounds")]
    public AudioClip charmSfx, fearSfx, stunSfx;
    public AudioClip wolfTransform, wolfSpawn, wolfHowl, claw;
    public AudioClip glassBreak, zap, tornado, waveCrash, waterBoom, boto, splash;
    public AudioClip vines, sporePop, rockBreak;
    public AudioClip heal, hairFlip, pose, catPounce, catCall, nineLives, bigBoom, gel;
    public AudioClip clang, sizzle, sunBeam, chomp, squish, plop;

    private static BoonArt instance;
    private static bool loaded;

    public static BoonArt Get
    {
        get
        {
            if (!loaded) { loaded = true; instance = Resources.Load<BoonArt>("BoonArt"); }
            return instance;
        }
    }

    // Plays one of the clips through FXSound (null-safe)
    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip != null) FXSound.Play(clip, volume, pitch);
    }

    // ---------------------------------------------------------------- food sprites at the game's 64 px per unit
    public enum Food { Egg, Bread, Tomato, Cheese, Meatball, Lettuce }
    private static readonly Sprite[] foodSprites = new Sprite[6];

    public static Sprite FoodSprite(Food f)
    {
        int i = (int)f;
        if (foodSprites[i] != null) return foodSprites[i];
        BoonArt art = Get;
        if (art == null || art.food == null || i >= art.food.Length || art.food[i] == null) return null;
        Sprite s = art.food[i];
        s.texture.filterMode = FilterMode.Point;
        foodSprites[i] = Sprite.Create(s.texture, s.rect, new Vector2(0.5f, 0.5f), 64f);
        return foodSprites[i];
    }

    private static Sprite pinkWaveSprite;
    public static Sprite PinkWave
    {
        get
        {
            if (pinkWaveSprite != null) return pinkWaveSprite;
            BoonArt art = Get;
            if (art == null || art.pinkWave == null) return null;
            art.pinkWave.filterMode = FilterMode.Point;
            pinkWaveSprite = Sprite.Create(art.pinkWave, new Rect(0, 0, art.pinkWave.width, art.pinkWave.height), new Vector2(0.5f, 0.12f), 64f);
            return pinkWaveSprite;
        }
    }
}
