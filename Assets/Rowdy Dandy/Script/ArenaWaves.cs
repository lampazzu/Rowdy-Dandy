// The waves of the two colosseums (FrontierArena). 30 each, every one its own group.
// S(enemy, count, delay, every, flags):
//   delay = seconds after the wave starts, every = seconds between each one of the group (0 = all at once)
//   flags: E all elite   e the first one elite   r ranged (spawns at the edges)   f flies
//          < left side only   > right side only   s drops from the sky (on top of Rowdy, a ring warns where)
//          w comes out of the pool in the middle (The Purple Reign only: Shark Wolves)
// Every count is multiplied by the arena's CountScale (FrontierArena: x1.6 / x1.6 / x1.8).
// Elder = the Moonbound Elder (CursedElder). Waves 10 / 20 / 30 are the boss waves (and the seals).
public static class ArenaWaves
{
    public struct Spawn
    {
        public string prefab, flags;
        public int count;
        public float delay, every;
        public bool Has(char c) => flags != null && flags.IndexOf(c) >= 0;
    }

    public struct Round { public string title; public Spawn[] spawns; }

    public const string Elder = "ELDER";
    public const string Steph = "STEPHMOSS"; // the boss of the Purple Reign (StephmossBoss)
    private const string WF = "Enemy_Werefast", GW = "Enemy_GnollWarrior", GA = "Enemy_GnollArcher", GB = "Enemy_GnollBomber",
        BW = "Enemy_BigWerewolf", TW = "Enemy_TransformWolf", WK = "Enemy_WereKnight", HR = "Enemy_HorseRider",
        MC = "Enemy_MegaCreature", VR = "Enemy_VoltRat" /* the Red Jelly */, CR = "Crabby", SW = "SharkWolf", WV = "Enemy_WaterVivaRider";

    private static Spawn S(string prefab, int count, float delay = 0f, float every = 0f, string flags = "")
        => new Spawn { prefab = prefab, count = count, delay = delay, every = every, flags = flags };

    private static Round R(string title, params Spawn[] spawns) => new Round { title = title, spawns = spawns };

    // ================================================================ THE FRONTIER (ramps up, learn every enemy)
    public static readonly Round[] Frontier =
    {
        R("THE WARM-UP", S(WF, 3)),
        R("CRAB RAVE", S(CR, 6, 0f, 0.6f), S(WF, 1, 3f)),
        R("JELLY SWARM", S(VR, 4, 0f, 0.4f, "f")),
        R("GNOLL PARTY", S(GW, 2), S(GA, 2, 1f, 0f, "r")),
        R("BOMB SQUAD", S(GB, 3, 0f, 0.5f, "r"), S(GW, 2, 3f)),
        R("HOWL AT THE SUN", S(TW, 2), S(WF, 2, 2f)),
        R("THE HEAVY", S(BW, 1, 0f, 0f, "E")),
        R("PINCER", S(WF, 3, 0f, 0.3f, "<"), S(WF, 3, 0.5f, 0.3f, ">")),
        R("JELLY RAIN", S(VR, 3, 0f, 0f, "f"), S(CR, 3, 2f, 0.5f, "s"), S(VR, 3, 4f, 0f, "f")),
        R("CHAMPION OF THE SHORE", S(WK, 1, 0f, 0f, "E"), S(GA, 2, 2f, 0f, "r"), S(WF, 2, 6f, 1f)),
        R("HORSEPLAY", S(HR, 2, 0f, 1f)),
        R("ARROW STORM", S(GA, 4, 0f, 0.4f, "r"), S(GW, 1, 2f, 0f, "E")),
        R("MEGA MASH", S(MC, 2, 0f, 1.5f)),
        R("THE PACK", S(TW, 2), S(WF, 4, 1f, 1.2f)),
        R("IRON AND FUR", S(WK, 2, 0f, 1f), S(BW, 1, 3f)),
        R("NO SAFE GROUND", S(VR, 4, 0f, 0.5f, "f"), S(GB, 2, 1f, 0f, "r")),
        R("STAMPEDE", S(HR, 3, 0f, 1.5f), S(CR, 4, 2f, 0.4f, "s")),
        R("BIG BAD DUO", S(BW, 1, 0f, 0f, "<"), S(BW, 1, 0.5f, 0f, ">")),
        R("MIXED BAG", S(GW, 1), S(GA, 1, 0.5f, 0f, "r"), S(GB, 1, 1f, 0f, "r"), S(WF, 1, 1.5f), S(TW, 1, 2f), S(MC, 1, 2.5f)),
        R("THE MOONBOUND ELDER", S(Elder, 1)),
        R("CRAB KINGS", S(CR, 6, 0f, 0.4f, "E")),
        R("KNIGHT SCHOOL", S(WK, 3, 0f, 1f, "e")),
        R("BLITZ", S(WF, 5, 0f, 0.6f, "es")),
        R("RED TIDE", S(VR, 5, 0f, 0.4f, "f"), S(VR, 3, 5f, 0.4f, "fE")),
        R("THE WALL", S(MC, 2, 0f, 1f, "E"), S(GA, 2, 2f, 0f, "r")),
        R("CAVALRY CHARGE", S(HR, 2, 0f, 1.5f, "E"), S(GW, 3, 2f, 0.8f)),
        R("WOLF MOON", S(TW, 3, 0f, 1f), S(BW, 2, 4f, 2f, "e")),
        R("BOMBARDMENT", S(GB, 3, 0f, 0.6f, "re"), S(GA, 3, 1f, 0.6f, "r"), S(VR, 2, 4f, 0.5f, "f")),
        R("THE GAUNTLET", S(WF, 3, 0f, 2f), S(GW, 2, 3f, 3f), S(MC, 1, 6f), S(WK, 1, 9f, 0f, "e"), S(VR, 3, 12f, 1f, "f")),
        R("THE CHAMPIONS", S(Elder, 1), S(WK, 2, 4f, 1.5f, "E"), S(VR, 4, 10f, 1f, "f")),
    };

    // ================================================================ THE GLOOMWOOD (way, way harder)
    public static readonly Round[] Gloom =
    {
        R("WHISPERS IN THE DARK", S(WF, 4, 0f, 0.5f, "e"), S(VR, 2, 2f, 0f, "f")),
        R("ROT CRABS", S(CR, 8, 0f, 0.4f, "e"), S(CR, 2, 4f, 0f, "E")),
        R("HOLLOW PACK", S(TW, 3, 0f, 0.6f), S(WF, 3, 2f, 0.5f)),
        R("THORN ARCHERS", S(GA, 4, 0f, 0.3f, "re"), S(GW, 2, 2f)),
        R("GRAVE KNIGHTS", S(WK, 3, 0f, 1.5f, "e")),
        R("NIGHT SWARM", S(VR, 7, 0f, 0.5f, "f"), S(VR, 3, 5f, 0.3f, "fE")),
        R("TWIN HEAVIES", S(BW, 2, 0f, 1f, "E")),
        R("GALLOWS RIDE", S(HR, 3, 0f, 1.2f, "e"), S(GA, 2, 3f, 0f, "r")),
        R("BOMB GARDEN", S(GB, 5, 0f, 0.8f, "r"), S(GW, 3, 2f, 0.6f, "e")),
        R("THE ELDER WAKES", S(Elder, 1), S(WF, 4, 6f, 1.5f)),
        R("BLACK STAMPEDE", S(HR, 4, 0f, 1f, "e"), S(HR, 1, 6f, 0f, "E")),
        R("MEGA ROT", S(MC, 3, 0f, 2f, "e")),
        R("CROSSFIRE", S(GA, 3, 0f, 0.3f, "r<"), S(GA, 3, 0f, 0.3f, "r>"), S(GW, 2, 2f, 0.5f, "E")),
        R("BLOOD PACK", S(TW, 4, 0f, 1f, "e"), S(TW, 1, 5f, 0f, "E")),
        R("FOG OF WAR", S(WF, 8, 0f, 0.7f, "s"), S(WF, 3, 6f, 0.5f, "E")),
        R("IRON STORM", S(WK, 3, 0f, 2f, "E"), S(VR, 6, 3f, 0.7f, "f")),
        R("THE CULL", S(BW, 3, 0f, 2.5f, "e")),
        R("SKY AND STEEL", S(VR, 8, 0f, 0.5f, "f"), S(MC, 2, 3f, 1f, "E")),
        R("GNOLL WARBAND", S(GW, 4, 0f, 0.5f, "e"), S(GA, 3, 1f, 0.4f, "r"), S(GB, 3, 3f, 0.6f, "re")),
        R("TWIN ELDERS", S(Elder, 2, 0f, 6f)),
        R("NO MERCY", S(GW, 1, 0f, 0f, "E"), S(GA, 1, 0.5f, 0f, "Er"), S(GB, 1, 1f, 0f, "Er"), S(WF, 1, 1.5f, 0f, "E"),
                      S(TW, 1, 2f, 0f, "E"), S(WK, 1, 2.5f, 0f, "E"), S(HR, 1, 3f, 0f, "E"), S(MC, 1, 3.5f, 0f, "E")),
        R("ROT TIDE", S(CR, 12, 0f, 0.3f, "se"), S(CR, 4, 5f, 0.3f, "E"), S(TW, 2, 7f, 1f, "E")),
        R("THE HORSEMEN", S(HR, 4, 0f, 2f, "E")),
        R("SWARM KING", S(VR, 14, 0f, 0.35f, "f"), S(VR, 2, 6f, 0.5f, "fE")),
        R("WALL OF FUR", S(BW, 3, 0f, 1.5f, "e"), S(WK, 2, 4f, 1f, "E")),
        R("GLOOM GAUNTLET", S(WF, 4, 0f, 1f, "E"), S(GW, 3, 4f, 1f, "E"), S(MC, 2, 8f, 2f, "E"), S(VR, 5, 12f, 0.6f, "fE")),
        R("HUNTERS", S(WF, 6, 0f, 0.8f, "Es")),
        R("SIEGE", S(GB, 4, 0f, 1f, "Er"), S(GA, 4, 1f, 1f, "Er"), S(MC, 2, 5f, 1f, "E")),
        R("LAST LIGHT", S(Elder, 1), S(BW, 2, 3f, 2f, "E"), S(VR, 6, 6f, 0.6f, "f")),
        R("THE GLOOM LORDS", S(Elder, 2, 0f, 8f), S(WK, 2, 3f, 1f, "E"), S(HR, 2, 10f, 1.5f, "E"), S(VR, 6, 14f, 0.8f, "fE")),
    };

    // ================================================================ THE PURPLE REIGN (a pool in the middle, everything at once)
    public static readonly Round[] Violet =
    {
        R("ROYAL WELCOME", S(WF, 4, 0f, 0.4f, "e"), S(VR, 3, 1f, 0.3f, "f"), S(SW, 1, 2f, 0f, "w")),
        R("COURT JESTERS", S(CR, 8, 0f, 0.3f, "e"), S(WV, 2, 2f, 1f), S(VR, 2, 3f, 0f, "fE")),
        R("THE KINGS GUARD", S(WK, 3, 0f, 1f, "E"), S(GA, 2, 1f, 0f, "re")),
        R("SHARK SEASON", S(SW, 2, 0f, 1.5f, "w"), S(WF, 4, 1f, 0.5f, "e")),
        R("PURPLE RAIN", S(VR, 8, 0f, 0.3f, "f"), S(CR, 4, 2f, 0.4f, "sE")),
        R("BLOOD AND VELVET", S(TW, 4, 0f, 0.8f, "E"), S(GB, 2, 2f, 0f, "re")),
        R("THE HEAVY CROWN", S(BW, 2, 0f, 1f, "E"), S(MC, 1, 3f, 0f, "E")),
        R("JOUSTING", S(HR, 3, 0f, 1f, "E"), S(GA, 3, 2f, 0.4f, "r")),
        R("FROM THE DEEP", S(SW, 3, 0f, 1f, "w"), S(WV, 3, 1f, 0.6f, "e"), S(VR, 4, 3f, 0.4f, "f")),
        R("THE MOONBOUND REGENT", S(Elder, 1), S(SW, 2, 3f, 2f, "w"), S(WK, 2, 6f, 1f, "E")),
        R("A MILLION CRABS", S(CR, 16, 0f, 0.2f, "e"), S(CR, 4, 4f, 0.3f, "sE")),
        R("ROYAL ARTILLERY", S(GB, 4, 0f, 0.6f, "Er"), S(GA, 4, 0.5f, 0.6f, "Er"), S(VR, 4, 3f, 0.4f, "f")),
        R("THE PACK OF KINGS", S(TW, 4, 0f, 1f, "E"), S(BW, 2, 3f, 1.5f, "E")),
        R("TIDAL KNIGHTS", S(WK, 4, 0f, 1f, "E"), S(SW, 2, 2f, 1f, "w")),
        R("NO SAFE THRONE", S(VR, 10, 0f, 0.3f, "fe"), S(HR, 2, 3f, 1f, "E")),
        R("GOLIATHS", S(MC, 3, 0f, 1.5f, "E")),
        R("THE MOAT", S(SW, 4, 0f, 0.8f, "w"), S(WF, 6, 1f, 0.4f, "Es")),
        R("WARBAND ROYALE", S(GW, 4, 0f, 0.6f, "E"), S(GA, 3, 1f, 0.4f, "Er"), S(GB, 3, 2f, 0.6f, "Er")),
        R("JELLY CROWN", S(VR, 12, 0f, 0.25f, "fE"), S(WV, 3, 3f, 0.6f, "E")),
        R("TWO MOONS", S(Elder, 2, 0f, 5f), S(SW, 2, 4f, 2f, "w")),
        R("CAVALRY OF NIGHT", S(HR, 5, 0f, 1f, "E"), S(VR, 4, 3f, 0.4f, "fE")),
        R("THE GREAT HUNT", S(WF, 8, 0f, 0.5f, "Es"), S(TW, 3, 3f, 1f, "E")),
        R("SIEGE OF VELVET", S(GB, 5, 0f, 0.8f, "Er"), S(MC, 2, 2f, 1.5f, "E"), S(GA, 4, 3f, 0.5f, "Er")),
        R("SHARK TANK", S(SW, 6, 0f, 0.6f, "w"), S(WK, 2, 3f, 1f, "E")),
        R("EVERYONE IS INVITED", S(GW, 1, 0f, 0f, "E"), S(GA, 1, 0.3f, 0f, "Er"), S(GB, 1, 0.6f, 0f, "Er"), S(WF, 1, 0.9f, 0f, "E"),
                                S(TW, 1, 1.2f, 0f, "E"), S(WK, 1, 1.5f, 0f, "E"), S(HR, 1, 1.8f, 0f, "E"), S(MC, 1, 2.1f, 0f, "E"),
                                S(BW, 1, 2.4f, 0f, "E"), S(VR, 3, 2.7f, 0.3f, "fE"), S(SW, 1, 3f, 0f, "w"), S(CR, 4, 3.3f, 0.2f, "E")),
        R("THE ARMORY", S(WK, 5, 0f, 0.8f, "E"), S(GW, 3, 3f, 0.8f, "E")),
        R("SKY AND SEA", S(VR, 14, 0f, 0.3f, "fE"), S(SW, 4, 2f, 1f, "w")),
        R("THE KINGSLAYERS", S(BW, 3, 0f, 1f, "E"), S(MC, 2, 2f, 1.5f, "E"), S(HR, 2, 4f, 1f, "E")),
        R("LAST DANCE", S(Elder, 1), S(WF, 8, 1f, 0.5f, "Es"), S(VR, 8, 4f, 0.3f, "fE"), S(SW, 3, 6f, 1f, "w")),
        R("STEPHMOSS, LORD OF SEVERED WOODS", S(Steph, 1), S(VR, 4, 20f, 0.5f, "f"), S(SW, 2, 35f, 3f, "w")),
    };
}