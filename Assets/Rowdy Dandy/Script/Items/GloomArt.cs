using UnityEngine;

// The Gloomy Forest sprites (Assets/Scenery/Gloomy Forest) the runtime-built Gloomwood arena decorates itself with:
// Resources/GloomArt.asset. Swap or add sprites here to change how the arena looks.
[CreateAssetMenu(menuName = "Rowdy Dandy/Gloom Art")]
public class GloomArt : ScriptableObject
{
    [Tooltip("Big background strip (RDR_GloomyTrees)")]
    public Sprite backdrop;
    public Sprite[] trees;          // TreeA-F
    public Sprite[] horrificTrees;  // HorrificTreeA/B
    public Sprite[] bushes;         // BushA-C
    public Sprite[] crystals;       // CrystalFlower A-D, CrystalVegetation
    public Sprite[] crystalPatches; // CrystalPatch A-C, CrystalVine
    public Sprite[] flowers;        // FlowerRed A-E
    public Sprite[] grass;          // GrassPatch A-D
    public Sprite[] rocks;          // RockPack, TreeStump A-C

    private static GloomArt instance;
    private static bool loaded;

    public static GloomArt Get
    {
        get
        {
            if (!loaded) { loaded = true; instance = Resources.Load<GloomArt>("GloomArt"); }
            return instance;
        }
    }
}
