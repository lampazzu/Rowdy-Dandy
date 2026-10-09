// The waves of the two colosseums (FrontierArena). 30 each, every one its own group.
// S(enemy, count, delay, every, flags):
//   delay = seconds after the wave starts, every = seconds between each one of the group (0 = all at once)
//   flags: E all elite   e the first one elite   r ranged (spawns at the edges)   f flies
//          < left side only   > right side only   s drops from the sky (on top of Rowdy, a ring warns where)
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
    private const string WF = "Enemy_Werefast", GW = "Enemy_GnollWarrior", GA = "Enemy_GnollArcher", GB = "Enemy_GnollBomber",
        BW = "Enemy_BigWerewolf", TW = "Enemy_TransformWolf", WK = "Enemy_WereKnight", HR = "Enemy_HorseRider",
        MC = "Enemy_MegaCreature", VR = "Enemy_VoltRat" /* the Red Jelly */, CR = "Crabby";

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
}
