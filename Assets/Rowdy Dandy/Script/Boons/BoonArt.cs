using UnityEngine;

// Art + sounds the boons use, in one place: Resources/BoonArt.asset. Filled automatically by Editor/BoonArtSetup.cs
// from "New Stuff you can use" and the Sound Effects folder (hags hand cards, status sounds...). Swap anything here.
// Patron portraits: drop a PNG at Resources/BoonPortraits/<PatronName>.png (e.g. Pompadour.png) and the boon
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

    [Header("Picker sounds")]
    public AudioClip[] rarity = new AudioClip[5];   // common, rare, epic, legendary, duo (MagicPlacement 01-05)
    public AudioClip[] cardHover = new AudioClip[5];
    public AudioClip cardPick, reroll, upgrade, fanfare, cancel;

    [Header("Patron sounds (when you take one of their boons)")]
    public AudioClip pompadour, riptide, howl, rot, disco, meow, hammock;

    [Header("Effect sounds")]
    public AudioClip charmSfx, fearSfx, stunSfx;
    public AudioClip wolfTransform, wolfSpawn, wolfHowl, claw;
    public AudioClip glassBreak, zap, tornado, waveCrash, waterBoom, boto, splash;
    public AudioClip vines, sporePop, mirrorBall, beam, zombieRise, discoFloor;
    public AudioClip heal, goldFist, hairFlip, pose, catPounce, catCall, nineLives, bigBoom, yawn, gel;

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
}
