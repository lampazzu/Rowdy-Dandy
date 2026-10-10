// The waves of the two colosseums (FrontierArena). 30 each, every one its own group.
// S(enemy, count, delay, every, flags):
//   delay = seconds after the wave starts, every = seconds between each one of the group (0 = all at once)
//   flags: E all elite   e the first one elite   r ranged (spawns at the edges)   f flies
//          < left side only   > right side only   s drops from the sky (on top of Rowdy, a ring warns where)
//          w comes out of the pool in the middle (The Purple Reign only: Shark Wolves)
// Every count is multiplied by the arena's CountScale (FrontierArena: x1.6 / x1.6 / x1.8).
// Elder = the Moonbound Elder (CursedElder), Pelich = Pelich Anus. Waves 10 / 20 / 30 are the boss waves (and the seals).
// Manta Rays fly low and hurt on touch, Pelicans are harmless (some carry a heart), Waterviva Riders are in all three.
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
    public const string Pelich = "PELICH";   // Pelich Anus, a copy of the beach boss (FrontierArena keeps a template)
    private const string WF = "Enemy_Werefast", GW = "Enemy_GnollWarrior", GA = "Enemy_GnollArcher", GB = "Enemy_GnollBomber",
        BW = "Enemy_BigWerewolf", TW = "Enemy_TransformWolf", WK = "Enemy_WereKnight", HR = "Enemy_HorseRider",
        MC = "Enemy_MegaCreature", VR = "Enemy_VoltRat" /* the Red Jelly */, CR = "Crabby", SW = "SharkWolf", WV = "Enemy_WaterVivaRider",
        MR = "Neutral_MantaRay" /* flies low, hurts on touch */, PE = "Neutral_Pelican" /* harmless, some carry a heart */;

    private static Spawn S(string prefab, int count, float delay = 0f, float every = 0f, string flags = "")
        => new Spawn { prefab = prefab, count = count, delay = delay, every = every, flags = flags };

    private static Round R(string title, params Spawn[] spawns) => new Round { title = title, spawns = spawns };

    // ================================================================ THE FRONTIER (ramps up, learn every enemy)
    public static readonly Round[] Frontier =
    {
        R("THE WARM-UP", S(WF, 3)),
        R("CRAB RAVE", S(CR, 6, 0f, 0.6f), S(WF, 1, 3f)),
        R("JELLY SWARM", S(VR, 4, 0f, 0.4f, "f"), S(MR, 2, 2f, 0.6f, "f")),
        R("GNOLL PARTY", S(GW, 2), S(GA, 2, 1f, 0f, "r")),
        R("BOMB SQUAD", S(GB, 3, 0f, 0.5f, "r"), S(GW, 2, 3f)),
        R("HOWL AT THE SUN", S(TW, 2), S(WF, 2, 2f)),
        R("THE HEAVY", S(BW, 1, 0f, 0f, "E")),
        R("PINCER", S(WF, 3, 0f, 0.3f, "<"), S(WF, 3, 0.5f, 0.3f, ">")),
        R("JELLY RAIN", S(VR, 3, 0f, 0f, "f"), S(CR, 3, 2f, 0.5f, "s"), S(VR, 3, 4f, 0f, "f"), S(PE, 2, 5f, 1f, "f")),
        R("CHAMPION OF THE SHORE", S(Pelich, 1), S(WK, 1, 3f), S(GA, 2, 6f, 0f, "r"), S(WF, 2, 10f, 1f)),
        R("HORSEPLAY", S(HR, 2, 0f, 1f)),
        R("ARROW STORM", S(GA, 4, 0f, 0.4f, "r"), S(GW, 1, 2f, 0f, "E")),
        R("MEGA MASH", S(MC, 2, 0f, 1.5f), S(WV, 2, 3f, 1f)),
        R("THE PACK", S(TW, 2), S(WF, 4, 1f, 1.2f)),
        R("IRON AND FUR", S(WK, 2, 0f, 1f), S(BW, 1, 3f)),
        R("NO SAFE GROUND", S(VR, 4, 0f, 0.5f, "f"), S(GB, 2, 1f, 0f, "r")),
        R("STAMPEDE", S(HR, 3, 0f, 1.5f), S(CR, 4, 2f, 0.4f, "s"), S(MR, 3, 4f, 0.4f, "f")),
        R("BIG BAD DUO", S(BW, 1, 0f, 0f, "<"), S(BW, 1, 0.5f, 0f, ">")),
        R("MIXED BAG", S(GW, 1), S(GA, 1, 0.5f, 0f, "r"), S(GB, 1, 1f, 0f, "r"), S(WF, 1, 1.5f), S(TW, 1, 2f), S(MC, 1, 2.5f)),
        R("THE MOONBOUND ELDER", S(Elder, 1)),
        R("CRAB KINGS", S(CR, 6, 0f, 0.4f, "E")),
        R("KNIGHT SCHOOL", S(WK, 3, 0f, 1f, "e")),
        R("BLITZ", S(WF, 5, 0f, 0.6f, "es")),
        R("RED TIDE", S(VR, 5, 0f, 0.4f, "f"), S(VR, 3, 5f, 0.4f, "fE"), S(WV, 2, 3f, 1f, "e")),
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
        R("ROT CRABS", S(CR, 8, 0f, 0.4f, "e"), S(CR, 2, 4f, 0f, "E"), S(MR, 3, 3f, 0.5f, "f")),
        R("HOLLOW PACK", S(TW, 3, 0f, 0.6f), S(WF, 3, 2f, 0.5f)),
        R("THORN ARCHERS", S(GA, 4, 0f, 0.3f, "re"), S(GW, 2, 2f)),
        R("GRAVE KNIGHTS", S(WK, 3, 0f, 1.5f, "e")),
        R("NIGHT SWARM", S(VR, 7, 0f, 0.5f, "f"), S(VR, 3, 5f, 0.3f, "fE"), S(PE, 2, 4f, 1f, "f")),
        R("TWIN HEAVIES", S(BW, 2, 0f, 1f, "E")),
        R("GALLOWS RIDE", S(HR, 3, 0f, 1.2f, "e"), S(GA, 2, 3f, 0f, "r"), S(WV, 2, 2f, 1f, "e")),
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
        R("BEAK AND CLAW", S(Elder, 1), S(Pelich, 1, 6f)),
        R("NO MERCY", S(GW, 1, 0f, 0f, "E"), S(GA, 1, 0.5f, 0f, "Er"), S(GB, 1, 1f, 0f, "Er"), S(WF, 1, 1.5f, 0f, "E"),
                      S(TW, 1, 2f, 0f, "E"), S(WK, 1, 2.5f, 0f, "E"), S(HR, 1, 3f, 0f, "E"), S(MC, 1, 3.5f, 0f, "E")),
        R("ROT TIDE", S(CR, 12, 0f, 0.3f, "se"), S(CR, 4, 5f, 0.3f, "E"), S(TW, 2, 7f, 1f, "E")),
        R("THE HORSEMEN", S(HR, 4, 0f, 2f, "E")),
        R("SWARM KING", S(VR, 14, 0f, 0.35f, "f"), S(VR, 2, 6f, 0.5f, "fE"), S(MR, 4, 3f, 0.4f, "f")),
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
        R("COURT JESTERS", S(CR, 8, 0f, 0.3f, "e"), S(WV, 2, 2f, 1f), S(VR, 2, 3f, 0f, "fE"), S(PE, 2, 4f, 1f, "f")),
        R("THE KINGS GUARD", S(WK, 3, 0f, 1f, "E"), S(GA, 2, 1f, 0f, "re")),
        R("SHARK SEASON", S(SW, 2, 0f, 1.5f, "w"), S(WF, 4, 1f, 0.5f, "e")),
        R("PURPLE RAIN", S(VR, 8, 0f, 0.3f, "f"), S(CR, 4, 2f, 0.4f, "sE"), S(MR, 4, 3f, 0.4f, "f")),
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
        R("THE MOON AND THE BEAK", S(Elder, 1), S(Pelich, 1, 5f, 0f, "E"), S(SW, 2, 4f, 2f, "w")),
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

    // ================================================================ wave templates (variety on a replay)
    // Every wave slot of every colosseum has Variants templates: the hand-made wave above + generated ones with the
    // SAME threat (enemy weights x elites, within ~8%), the same enemies unlocked so far (the ramp that teaches each
    // enemy stays) and about the same length. Boss slots keep their boss and only change the escort.
    // 30 slots x 6 = 180 templates per colosseum. Each trial draws one per slot at random (ForRun).
    // Generated once per session from fixed seeds, so the templates are always the same 180.
    public const int Variants = 6;
    private static readonly Round[][][] templates = new Round[3][][];

    public static Round[] Hand(int tier) => tier == 2 ? Violet : tier == 1 ? Gloom : Frontier;

    public static Round[] ForRun(int tier)
    {
        Round[][] t = Templates(tier);
        var run = new Round[t.Length];
        for (int w = 0; w < t.Length; w++) run[w] = t[w][UnityEngine.Random.Range(0, t[w].Length)];
        return run;
    }

    public static int TemplateCount(int tier) { int n = 0; foreach (Round[] slot in Templates(tier)) n += slot.Length; return n; }

    public static Round[][] Templates(int tier)
    {
        tier = UnityEngine.Mathf.Clamp(tier, 0, 2);
        if (templates[tier] != null) return templates[tier];
        Round[] hand = Hand(tier);
        // when each enemy first shows up in the hand-made list: generated waves only use what's been met
        var firstSeen = new System.Collections.Generic.Dictionary<string, int>();
        for (int w = 0; w < hand.Length; w++)
            foreach (Spawn s in hand[w].spawns)
                if (!firstSeen.ContainsKey(s.prefab)) firstSeen[s.prefab] = w;
        var all = new Round[hand.Length][];
        for (int w = 0; w < hand.Length; w++)
        {
            all[w] = new Round[Variants];
            all[w][0] = hand[w];
            for (int v = 1; v < Variants; v++)
                all[w][v] = Generate(tier, w, hand[w], firstSeen, new System.Random(7919 * (tier + 1) + 131 * w + v));
        }
        templates[tier] = all;
        return all;
    }

    // ---------------------------------------------------------------- threat (how hard a group is)
    private static float Weight(string prefab)
    {
        switch (prefab)
        {
            case CR: return 0.5f;
            case PE: return 0.4f;
            case MR: return 0.8f;
            case WF: case VR: return 1f;
            case GA: return 1.2f;
            case GW: return 1.3f;
            case GB: return 1.5f;
            case TW: return 1.6f;
            case WV: return 1.8f;
            case SW: return 2f;
            case HR: return 2.3f;
            case WK: return 2.5f;
            case BW: case MC: return 3f;
            case Elder: return 8f;
            case Pelich: return 10f;
            case Steph: return 30f;
        }
        return 1.5f;
    }

    private const float EliteWeight = 2.2f;

    private static float Threat(Spawn s)
    {
        float w = Weight(s.prefab);
        if (s.Has('E')) return s.count * w * EliteWeight;
        if (s.Has('e')) return w * EliteWeight + (s.count - 1) * w;
        return s.count * w;
    }

    public static float Threat(Round r) { float t = 0f; foreach (Spawn s in r.spawns) t += Threat(s); return t; }

    private static float Length(Round r)
    {
        float end = 0f;
        foreach (Spawn s in r.spawns) end = System.Math.Max(end, s.delay + s.count * s.every);
        return end;
    }

    private static float EliteShare(Round r)
    {
        float total = Threat(r), elite = 0f;
        foreach (Spawn s in r.spawns)
        {
            float w = Weight(s.prefab);
            if (s.Has('E')) elite += s.count * w * EliteWeight;
            else if (s.Has('e')) elite += w * EliteWeight;
        }
        return total > 0f ? elite / total : 0f;
    }

    // ---------------------------------------------------------------- roles
    private static readonly string[] Cheap = { CR, WF, VR, MR };
    private static readonly string[] Melee = { WF, GW, TW, CR };
    private static readonly string[] Ranged = { GA, GB };
    private static readonly string[] Heavy = { BW, MC, WK, HR, WV };
    private static readonly string[] Flyers = { VR, MR };
    private static bool Flies(string p) => p == VR || p == MR || p == PE;
    private static bool IsBoss(string p) => p == Elder || p == Steph || p == Pelich;
    private static bool IsRanged(string p) => p == GA || p == GB;
    private static bool CanDrop(string p) => p == WF || p == CR || p == GW || p == TW; // falls from the sky ('s')

    private enum Shape { Swarm, Pincer, HeavyEscort, RangedLine, SkyDrop, AirRaid, MixedBag, Waves, FromTheDeep }

    private static readonly string[][] Names =
    {
        new[] { "THE SWARM", "TOO MANY TEETH", "STAMPEDE", "FLOOD", "HORDE", "NO ROOM TO SURF" },                 // Swarm
        new[] { "PINCER", "BOTH SIDES", "SANDWICHED", "CLOSING IN", "NO WAY OUT", "THE SQUEEZE" },               // Pincer
        new[] { "THE BIG ONE", "BODYGUARDS", "HEAVY ESCORT", "BRUTE FORCE", "BIG AND FRIENDS", "THE TANK" },     // HeavyEscort
        new[] { "ARROW STORM", "FIRING LINE", "SHOOTING GALLERY", "KEEP YOUR HEAD DOWN", "CROSSFIRE", "VOLLEY" }, // RangedLine
        new[] { "FROM ABOVE", "SKYFALL", "LOOK UP", "RAINING WOLVES", "DROP ZONE", "HEADS UP" },                 // SkyDrop
        new[] { "AIR RAID", "JELLY RAIN", "BUZZING", "STING OPERATION", "THE CLOUD", "ZAP ZAP" },                // AirRaid
        new[] { "MIXED BAG", "A BIT OF EVERYTHING", "THE BUFFET", "GRAB BAG", "ODD COUPLES", "THE PARADE" },     // MixedBag
        new[] { "WAVE AFTER WAVE", "THEY KEEP COMING", "SECOND HELPING", "ROUND AND ROUND", "MORE", "ENCORE" },   // Waves
        new[] { "FROM THE DEEP", "SHARK BAIT", "THE MOAT", "SOMETHING IN THE WATER", "FINS UP", "HIGH TIDE" },  // FromTheDeep
    };

    private static string Pick(System.Random rng, string[] options) => options[rng.Next(options.Length)];

    private static string PickFrom(System.Random rng, string[] options, System.Collections.Generic.List<string> roster)
    {
        var ok = new System.Collections.Generic.List<string>();
        foreach (string o in options) if (roster.Contains(o)) ok.Add(o);
        return ok.Count > 0 ? ok[rng.Next(ok.Count)] : roster[rng.Next(roster.Count)];
    }

    // ---------------------------------------------------------------- one generated wave
    private static Round Generate(int tier, int w, Round hand, System.Collections.Generic.Dictionary<string, int> firstSeen, System.Random rng)
    {
        var roster = new System.Collections.Generic.List<string>();
        foreach (var kv in firstSeen)
            if (kv.Value <= w && !IsBoss(kv.Key) && kv.Key != PE && (kv.Key != SW || tier == 2)) roster.Add(kv.Key);
        bool pelicans = firstSeen.TryGetValue(PE, out int peSeen) && peSeen <= w; // harmless: only ever a small side group
        if (roster.Count == 0) return hand;

        // boss slots keep their boss: only the escort is new
        var spawns = new System.Collections.Generic.List<Spawn>();
        float budget = Threat(hand);
        foreach (Spawn s in hand.spawns)
            if (IsBoss(s.prefab))
            {
                budget -= Threat(s);
                Spawn b = s;
                // an Elder slot is sometimes Pelich Anus instead (and the other way round)
                if (b.prefab == Elder && rng.NextDouble() < 0.3) b.prefab = Pelich;
                else if (b.prefab == Pelich && rng.NextDouble() < 0.3) b.prefab = Elder;
                spawns.Add(b);
            }
        bool boss = spawns.Count > 0;
        float span = System.Math.Max(3f, Length(hand)) * (boss ? 1f : 0.9f + 0.2f * (float)rng.NextDouble());
        float eliteShare = EliteShare(hand);
        float eliteChance = (float)System.Math.Min(0.85, eliteShare * 1.3 + 0.03 * w / 3.0);

        var shapes = new System.Collections.Generic.List<Shape> { Shape.Swarm, Shape.Pincer, Shape.MixedBag, Shape.Waves };
        if (roster.Exists(p => System.Array.IndexOf(Flyers, p) >= 0)) shapes.Add(Shape.AirRaid);
        if (roster.Exists(IsRanged)) shapes.Add(Shape.RangedLine);
        if (roster.Exists(CanDrop)) shapes.Add(Shape.SkyDrop);
        if (roster.Exists(p => System.Array.IndexOf(Heavy, p) >= 0)) shapes.Add(Shape.HeavyEscort);
        if (roster.Contains(SW)) { shapes.Add(Shape.FromTheDeep); shapes.Add(Shape.FromTheDeep); }
        Shape shape = shapes[rng.Next(shapes.Count)];
        string title = boss ? hand.title : Pick(rng, Names[(int)shape]);

        // the groups this shape is made of: (prefab, share of the budget, start, flags)
        var plan = new System.Collections.Generic.List<(string prefab, float share, float start, string flags)>();
        switch (shape)
        {
            case Shape.Swarm:
                plan.Add((PickFrom(rng, Cheap, roster), 0.75f, 0f, ""));
                plan.Add((PickFrom(rng, Melee, roster), 0.25f, span * 0.5f, ""));
                break;
            case Shape.Pincer:
            {
                string p = PickFrom(rng, Melee, roster);
                plan.Add((p, 0.5f, 0f, "<"));
                plan.Add((p, 0.5f, 0.4f, ">"));
                break;
            }
            case Shape.HeavyEscort:
                plan.Add((PickFrom(rng, Heavy, roster), 0.55f, 0f, ""));
                plan.Add((PickFrom(rng, Cheap, roster), 0.45f, span * 0.3f, ""));
                break;
            case Shape.RangedLine:
                plan.Add((PickFrom(rng, Ranged, roster), 0.5f, 0f, "r"));
                plan.Add((PickFrom(rng, Melee, roster), 0.5f, 1f, ""));
                break;
            case Shape.SkyDrop:
            {
                var droppers = roster.FindAll(CanDrop);
                plan.Add((droppers[rng.Next(droppers.Count)], 0.65f, 0f, "s"));
                plan.Add((PickFrom(rng, Melee, roster), 0.35f, span * 0.4f, ""));
                break;
            }
            case Shape.AirRaid:
                plan.Add((PickFrom(rng, Flyers, roster), 0.7f, 0f, "f"));
                plan.Add((PickFrom(rng, Ranged.Length > 0 ? Ranged : Melee, roster), 0.3f, span * 0.35f, ""));
                break;
            case Shape.MixedBag:
            {
                int kinds = System.Math.Min(roster.Count, 3 + rng.Next(3));
                var bag = new System.Collections.Generic.List<string>(roster);
                for (int i = 0; i < kinds; i++)
                {
                    string p = bag[rng.Next(bag.Count)];
                    bag.Remove(p);
                    plan.Add((p, 1f / kinds, span * i / kinds, ""));
                    if (bag.Count == 0) break;
                }
                break;
            }
            case Shape.Waves:
            {
                int n = 2 + rng.Next(2);
                for (int i = 0; i < n; i++) plan.Add((roster[rng.Next(roster.Count)], 1f / n, span * i / n, ""));
                break;
            }
            case Shape.FromTheDeep:
                plan.Add((SW, 0.55f, 0f, "w"));
                plan.Add((PickFrom(rng, Melee, roster), 0.45f, 1f, ""));
                break;
        }

        if (pelicans && !boss && rng.NextDouble() < 0.3) plan.Add((PE, 0.06f, span * 0.5f, "f")); // a couple of heart carriers

        // fill the budget: counts from each group's share, elites where the hand-made wave had them
        float target = System.Math.Max(0f, budget);
        float spent = 0f;
        var made = new System.Collections.Generic.List<Spawn>();
        foreach (var g in plan)
        {
            string flags = g.flags;
            if (Flies(g.prefab) && flags.IndexOf('f') < 0) flags += "f";
            if (IsRanged(g.prefab) && flags.IndexOf('r') < 0) flags += "r";
            if (g.prefab == SW && flags.IndexOf('w') < 0) flags += "w";
            if (flags.IndexOf('s') >= 0 && !CanDrop(g.prefab)) flags = flags.Replace("s", "");
            double roll = rng.NextDouble();
            float groupBudget = target * g.share;
            // elites only where the group can afford them (an elite heavy is a lot of threat on its own)
            if (roll < eliteChance * 0.45 && groupBudget >= Weight(g.prefab) * EliteWeight * 1.5f) flags += "E";
            else if (roll < eliteChance && groupBudget >= Weight(g.prefab) * (EliteWeight + 1f)) flags += "e";
            float unit = Weight(g.prefab) * (flags.IndexOf('E') >= 0 ? EliteWeight : 1f);
            int count = (int)System.Math.Round(groupBudget / unit);
            if (count < 1 && made.Count > 0 && groupBudget < unit * 0.5f) continue; // too small to be worth a group
            count = System.Math.Max(1, System.Math.Min(16, count));
            float every = count <= 1 ? 0f : (float)System.Math.Round(System.Math.Max(0.25, System.Math.Min(2.5, span * 0.6 / count)), 2);
            var s = S(g.prefab, count, (float)System.Math.Round(g.start, 1), every, flags);
            made.Add(s);
            spent += Threat(s);
        }
        // tune until it's within ~8% of the hand-made wave: one enemy at a time on the cheapest group that can move;
        // too much and nothing left to take = elites calm down, then the priciest extra group goes
        for (int guard = 0; guard < 80 && target > 0f && made.Count > 0; guard++)
        {
            float diff = target - spent;
            if (System.Math.Abs(diff) <= target * 0.08f) break;
            int pick = -1;
            for (int i = 0; i < made.Count; i++)
            {
                bool movable = (diff > 0f ? made[i].count < 20 : made[i].count > 1) && made[i].prefab != PE; // pelicans never pad a wave
                if (movable && (pick < 0 || Weight(made[i].prefab) < Weight(made[pick].prefab))) pick = i;
            }
            if (pick >= 0)
            {
                Spawn s = made[pick];
                float before = Threat(s);
                s.count += diff > 0f ? 1 : -1;
                made[pick] = s;
                spent += Threat(s) - before;
                continue;
            }
            if (diff > 0f) break;
            int elite = made.FindIndex(x => x.Has('E') || x.Has('e'));
            if (elite >= 0)
            {
                Spawn s = made[elite];
                float before = Threat(s);
                s.flags = s.Has('E') ? s.flags.Replace("E", "e") : s.flags.Replace("e", "");
                made[elite] = s;
                spent += Threat(s) - before;
                continue;
            }
            if (made.Count < 2) break;
            int priciest = 0;
            for (int i = 1; i < made.Count; i++) if (Threat(made[i]) > Threat(made[priciest])) priciest = i;
            spent -= Threat(made[priciest]);
            made.RemoveAt(priciest);
        }
        spawns.AddRange(made);
        return R(title, spawns.ToArray());
    }
}