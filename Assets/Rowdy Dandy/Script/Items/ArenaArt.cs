using UnityEngine;

// Art the runtime colosseums dress themselves with (FrontierArena / GloomArena), as plain textures (cut / pivoted
// at runtime at 64 px per unit, never scaled). Resources/ArenaArt.asset - swap anything here.
//   Frontier (orange):  earth / sand piles, earth walls, pillars, the big orange cliff, a wolf skull
//   Purple Reign:       purple palms (the animated one: 22 frames), lake water (6 frames), the purple fog layers
//   Platforms:          the one-way sand tile (64 x 16), dyed per arena
[CreateAssetMenu(menuName = "Rowdy Dandy/Arena Art")]
public class ArenaArt : ScriptableObject
{
    [Header("The Frontier (orange)")]
    public Texture2D earthPile, sandPile, earthWall, earthWallHole, fatPillar, pillar, doublePillar, megaCliffA, megaCliffE, wolfSkull;
    [Header("The Purple Reign (purple)")]
    public Texture2D palm, bigPalm, lake, fog1, fog2, fog3, frontzada;
    [Header("Platforms")]
    public Texture2D passThrough;

    private static ArenaArt instance;
    private static bool loaded;
    public static ArenaArt Get { get { if (!loaded) { loaded = true; instance = Resources.Load<ArenaArt>("ArenaArt"); } return instance; } }

    // One sprite of a single-image texture, pivot at the bottom middle (stands on the ground)
    public static Sprite Standing(Texture2D t) => t == null ? null : ItemArt.Frames(t, 1, 1, new Vector2(0.5f, 0f), 64f)[0];
}