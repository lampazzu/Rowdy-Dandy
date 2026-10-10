using System.Collections.Generic;
using UnityEngine;

// Every patron and every boon: names, slots, numbers per rarity, icons and what the patrons say.
// Texts use the pixel font: A-Z 0-9 . , : ! ? % / - + < > ( ) only (no apostrophes).
//   {0} {1} in a description = the boon's values for the rarity shown (Common / Uncommon / Rare / Epic / Legendary)
//   Slots: one boon per Attack / Dash / Jump / Cats / Special slot (a new one replaces the old), Passives stack.
// Patron portraits: Resources/BoonPortraits/<enum name>.png (e.g. Narcissism.png, Chef.png, Sun.png).
public enum Patron { Narcissism, Abyss, Lycanthropy, Rot, Guild, Chef, Smith, Sun }
public enum BoonSlot { Attack, Dash, Jump, Cats, Special, Passive }
public enum Rarity { Common, Uncommon, Rare, Epic, Legendary, Duo } // saved as letters C U R E L D (Boons.Load reads the old numbers too)

public class PatronInfo
{
    public Patron id;
    public string name, title;
    public Color color, accent;
    public string emblem;            // BoonIcons drawn icon used until the patron has a portrait
    public string[] greet, pick;
}

public class BoonDef
{
    public string id, name;
    public Patron patron;
    public Patron? partner;          // duo boons: the second patron
    public BoonSlot slot;
    public string desc;
    public float[][] values;         // values[i] = { common, rare, epic } or { common, rare, epic, legendary } or all five
    public string icon;              // BoonIcons key
    public Color iconTint = Color.white;
    public string duoLine;           // what the two patrons say together
    public bool legendaryOnly;
    public string[] requires;        // duo: one of each list... see Boons.Eligible (ids of boons, any one per patron)
    public string[] excludes;        // can't be owned together: taking this one replaces those (and the other way round)
    public System.Func<bool> condition;

    public bool IsDuo => partner.HasValue;

    public float Value(int index, Rarity r)
    {
        if (values == null || index < 0 || index >= values.Length) return 0f;
        float[] v = values[index];
        // tuned numbers for this boon (Balance.BoonValues) win over the ones below
        if (Balance.BoonValues.TryGetValue(id, out float[][] tuned) && index < tuned.Length) v = tuned[index];
        if (r == Rarity.Duo || v.Length == 1) return v[0];
        if (v.Length >= 5) return v[Mathf.Min((int)r, v.Length - 1)];
        // 3 or 4 numbers = common / rare / epic (/ legendary): uncommon sits halfway, legendary = epic x1.25
        switch (r)
        {
            case Rarity.Common: return v[0];
            case Rarity.Uncommon: return Mathf.Lerp(v[0], v[1], 0.5f);
            case Rarity.Rare: return v[1];
            case Rarity.Epic: return v[Mathf.Min(2, v.Length - 1)];
            default: return v.Length >= 4 ? v[3] : v[v.Length - 1] * 1.25f;
        }
    }

    public string Describe(Rarity r)
    {
        string s = Balance.BoonDescription(id, desc);
        if (values != null)
            for (int i = 0; i < values.Length; i++)
                s = s.Replace("{" + i + "}", Format(Value(i, r)));
        return s;
    }

    public static string Format(float v) => Mathf.Abs(v - Mathf.Round(v)) < 0.01f || v >= 10f ? Mathf.RoundToInt(v).ToString() : v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
}

public static class BoonCatalog
{
    public static readonly Color[] RarityColors =
    {
        new Color(0.88f, 0.86f, 0.92f),   // common
        new Color(0.45f, 0.95f, 0.4f),    // uncommon
        new Color(0.35f, 0.7f, 1f),       // rare
        new Color(0.78f, 0.4f, 1f),       // epic
        new Color(1f, 0.6f, 0.15f),       // legendary
        new Color(0.4f, 1f, 0.88f),       // duo
    };
    public static readonly string[] RarityNames = { "COMMON", "UNCOMMON", "RARE", "EPIC", "LEGENDARY", "DUO" };
    public static readonly string[] SlotNames = { "ATTACK", "DASH", "JUMP", "CATS", "SPECIAL", "PASSIVE" };

    // Boons that were taken out of the game (DJ Fever, The Hammock, a few duos). A save that owned one gets the pick back.
    public static readonly HashSet<string> Retired = new HashSet<string>
    {
        "chainslap", "nightfever", "mirrorball", "funkyfeet", "naptime", "glassjaw", "thriller", "ravemold", "catscratch",
        "runway", "ink", "anvil", "sparks", // mega fix 10
    };

    public static readonly Dictionary<Patron, PatronInfo> Patrons = BuildPatrons();
    public static readonly List<BoonDef> All = BuildBoons();
    private static Dictionary<string, BoonDef> byId;

    public static PatronInfo Of(Patron p) => Patrons[p];

    public static BoonDef Get(string id)
    {
        if (byId == null)
        {
            byId = new Dictionary<string, BoonDef>();
            foreach (BoonDef b in All) byId[b.id] = b;
        }
        return id != null && byId.TryGetValue(id, out BoonDef d) ? d : null;
    }

    // Every boon id of a patron (duo requirement lists)
    private static string[] IdsOf(List<BoonDef> list, Patron p)
    {
        var ids = new List<string>();
        foreach (BoonDef b in list) if (!b.IsDuo && b.patron == p) ids.Add(b.id);
        return ids.ToArray();
    }

    private static float[] V(float c, float r, float e) => new[] { c, r, e };
    private static float[] V(float c, float r, float e, float l) => new[] { c, r, e, l };
    private static float[] F(float v) => new[] { v, v, v, v };

    private static Dictionary<Patron, PatronInfo> BuildPatrons()
    {
        var d = new Dictionary<Patron, PatronInfo>();
        void Add(Patron id, string name, string title, Color color, Color accent, string emblem, string[] greet, string[] pick)
            => d[id] = new PatronInfo { id = id, name = name, title = title, color = color, accent = accent, emblem = emblem, greet = greet, pick = pick };

        Add(Patron.Narcissism, "NARCISSISM", "PATRON OF GORGEOUS", new Color(1f, 0.37f, 0.78f), new Color(1f, 0.82f, 0.3f), "draw:hairflip",
            new[] { "OH. MY. GOD. LOOK AT YOU, GORGEOUS. NOW LOOK AT ME.", "A LITTLE VOLUME NEVER HURT ANYBODY, DARLING.",
                    "YOU HAVE THE HAIR. I HAVE THE POWER. LET US BE ICONIC.", "BEAUTY IS PAIN, BABY. MOSTLY THEIR PAIN.",
                    "THAT FACE? WASTED ON MONSTERS. LET US FIX THAT.", "MIRROR, MIRROR... NEVER MIND, IT IS ME. IT IS ALWAYS ME." },
            new[] { "SERVE IT. SERVE IT ALL.", "MWAH! NOW GO MAKE THEM JEALOUS.", "FLAWLESS. AS EXPECTED." });

        Add(Patron.Abyss, "SEA ABYSS", "PATRON OF THE DEEP", new Color(0.18f, 0.6f, 1f), new Color(0.45f, 1f, 0.85f), "draw:tentacle",
            new[] { "...DOWN HERE, LITTLE SURFER, NOBODY HEARS THEM SCREAM.", "THE DEEP IS PATIENT. THE DEEP IS HUNGRY. THE DEEP LIKES YOUR HAIR.",
                    "EVERY WAVE YOU RIDE IS ONE OF MY FINGERS.", "COME CLOSER. THE PRESSURE IS... RELAXING.",
                    "WHAT SINKS, STAYS. WHAT STAYS, IS MINE.", "THE LIGHT NEVER REACHES ME. YOU WILL DO." },
            new[] { "SINK THEM ALL.", "THE TIDE RISES WITH YOU.", "...GLUB." });

        Add(Patron.Lycanthropy, "LYCANTHROPY", "PATRON OF THE HUNGRY MOON", new Color(0.92f, 0.18f, 0.26f), new Color(0.86f, 0.86f, 0.95f), "draw:moonrage",
            new[] { "THE MOON IS WATCHING YOU, LITTLE MAN. BE WORTH IT.", "I SMELL FEAR. NOT YOURS. GOOD.",
                    "TEETH. CLAWS. HUNGER. PICK ONE. OR ALL OF THEM.", "AWOOOO! SORRY. IT JUST COMES OUT.",
                    "THE WOLVES OUT THERE ARE MY CHILDREN. EAT THEM ANYWAY.", "THE SUN? A BIG BRIGHT SHOW-OFF. IGNORE HIM." },
            new[] { "FEED. GROW. HOWL.", "NOW YOU HUNT WITH ME.", "GRRRR. YES." });

        // The poison patron is STEPHMOSS, LORD OF SEVERED WOODS (the user's boss from Graft). Enum stays Rot (saves).
        Add(Patron.Rot, "STEPHMOSS", "LORD OF SEVERED WOODS", new Color(0.72f, 1f, 0.25f), new Color(0.95f, 0.8f, 0.25f), "draw:sporestep",
            new[] { "...SNIP. SNIP. THE WOODS WERE QUIET BEFORE YOU, SURFER.", "EVERYTHING WITH A STEM GETS CUT. EVERYTHING WITH A HEART GETS POISON.",
                    "MY SWARM LIKES YOUR HAIR. IT WANTS TO NEST IN IT.", "THE GROVE BLEEDS GREEN. LET THEM BLEED GREEN TOO.",
                    "I HAVE SIX LEGS AND TWO BLADES. YOU HAVE A FISHING ROD. ADORABLE.", "BUZZ... DO NOT SWAT. TAKE THE GIFT." },
            new[] { "GO. ROT THEM FROM THE INSIDE.", "THE SWARM GOES WITH YOU.", "SNIP SNIP, LITTLE DANDY." });
        Add(Patron.Guild, "STINKY BOIS GUILD", "PATRON OF THE STRAYS", new Color(0.8f, 0.62f, 1f), new Color(1f, 0.72f, 0.56f), "draw:catface",
            new[] { "WE ARE THE GUILD. WE SMELL. WE RULE THE ALLEYS.", "OI, DANDY. YOU FEED OUR BOYS, WE WATCH YOUR BACK. DEAL?",
                    "NINE LIVES EACH, BOSS. THAT IS A LOT OF LIVES.", "THE BOSS SAYS HI. THE BOSS IS ASLEEP IN A BOX.",
                    "WE KNOCKED YOUR STUFF OFF THE TABLE. NOW WE HELP. FAIR.", "NOBODY BATHES IN THE GUILD. THAT IS RULE NUMBER ONE." },
            new[] { "THE GUILD REMEMBERS.", "MRRP. PLEASURE DOING BUSINESS.", "STAY STINKY, DANDY." });

        Add(Patron.Chef, "CRAZY CHEF", "PATRON OF THE SANDWICH", new Color(1f, 0.45f, 0.2f), new Color(1f, 0.9f, 0.55f), "draw:sandwich",
            new[] { "MAMMA MIA! LOOK AT THOSE ARMS! THEY NEED A SANDWICH!", "BREAD! LETTUCE! TOMATO! CHEESE! MEATBALL! BREAD! THAT IS POETRY, BABY!",
                    "YOU FIGHT ON AN EMPTY STOMACH? CRAZY! I AM THE CRAZY ONE HERE!", "EVERY ENEMY IS JUST AN INGREDIENT YOU HAVE NOT MET YET!",
                    "SANDWICH. SANDWICH! SANDWIIIICH!", "SALT? PEPPER? VIOLENCE? A PINCH OF EACH!" },
            new[] { "BUON APPETITO!", "CHEF KISS! MWAH!", "NOW GO MAKE THEM INTO SALAD!" });

        Add(Patron.Smith, "THE BLACKSMITH", "PATRON OF STEEL", new Color(0.66f, 0.72f, 0.84f), new Color(1f, 0.52f, 0.16f), "draw:anvil",
            new[] { "HMPH. YOU SWING THAT THING LIKE A FISHING ROD. OH. IT IS A FISHING ROD.", "GOOD STEEL DOES NOT ASK QUESTIONS. NEITHER DO I.",
                    "HIT THINGS. BREAK THINGS. BRING ME THE PIECES.", "I HAVE BEEN HAMMERING SINCE BEFORE YOUR HAIR WAS INVENTED.",
                    "A WEAPON IS A PROMISE. KEEP IT SHARP." },
            new[] { "CLANG. DONE.", "TREAT HER RIGHT.", "NOW THAT IS A WEAPON." });

        Add(Patron.Sun, "THE SUN GOD", "PATRON OF DAYLIGHT", new Color(1f, 0.76f, 0.18f), new Color(1f, 0.98f, 0.8f), "draw:sun",
            new[] { "BEHOLD! IT IS I! THE REASON YOU CAN SEE!", "YOU ARE WELCOME FOR THE TAN, MORTAL.",
                    "THE MOON? THAT SHINY ROCK STEALS MY LIGHT! DO NOT TRUST IT!", "RISE AND SHINE! MOSTLY SHINE! MOSTLY ME!",
                    "WEAR SUNSCREEN. THEY WILL NOT." },
            new[] { "BASK IN IT!", "LET THERE BE LIGHT! ON THEM! PAINFULLY!", "RADIANT CHOICE." });
        return d;
    }

    private static List<BoonDef> BuildBoons()
    {
        var list = new List<BoonDef>();
        BoonDef Add(string id, string name, Patron p, BoonSlot slot, string desc, string icon, params float[][] values)
        {
            var b = new BoonDef { id = id, name = name, patron = p, slot = slot, desc = desc, icon = icon, values = values };
            list.Add(b);
            return b;
        }
        BoonDef Duo(string id, string name, Patron a, Patron b, string desc, string icon, string line, string[] needA, string[] needB, params float[][] values)
        {
            var d = new BoonDef { id = id, name = name, patron = a, partner = b, slot = BoonSlot.Passive, desc = desc, icon = icon, values = values, duoLine = line };
            d.requires = new[] { string.Join(",", needA), string.Join(",", needB) };
            list.Add(d);
            return d;
        }

        // ---------------------------------------------------------------- NARCISSISM
        Add("hairflip", "HAIR FLIP", Patron.Narcissism, BoonSlot.Attack,
            "EVERY 3RD SWING FLIPS YOUR HAIR: A PINK CRESCENT FLIES OUT FOR {0} DAMAGE.", "draw:hairflip", V(18, 26, 36));
        Add("decoy", "TOO PRETTY TO HIT", Patron.Narcissism, BoonSlot.Dash,
            "SURF DASH LEAVES A GORGEOUS DECOY. ENEMIES STARE AT IT FOR {0}S, THEN IT SHATTERS FOR 20.", "sheet:charm:9:4", V(2f, 2.8f, 3.6f));
        Add("admire", "ADMIRE YOURSELF", Patron.Narcissism, BoonSlot.Passive,
            "STAND STILL 1.5S TO STRIKE A POSE. YOUR NEXT HIT DEALS X{0} DAMAGE.", "draw:mirror", V(2.5f, 3f, 3.5f));
        Add("mainchar", "MAIN CHARACTER", Patron.Narcissism, BoonSlot.Passive,
            "THE STYLE RANK FOLLOWS YOU NOW (D TO SSS). +{0}% DAMAGE FOR EVERY RANK. AT SSS YOU SHINE LIKE THE SUN.", "draw:crown", V(6, 8, 10));
        Add("kiss", "BLOW A KISS", Patron.Narcissism, BoonSlot.Jump,
            "EVERY JUMP BLOWS A KISS: A HEART FLIES TO THE NEAREST ENEMY. {0} DAMAGE AND CHARMED 1S.", "draw:kiss", V(8, 12, 16));
        Add("spotlight", "SPOTLIGHT", Patron.Narcissism, BoonSlot.Passive,
            "EVERY 10S THE SPOTLIGHT FINDS YOU FOR 4S: +{0}% DAMAGE AND +15% CRIT CHANCE WHILE IT SHINES.", "draw:spotlight", V(25, 35, 50));
        Add("jealousy", "JEALOUSY", Patron.Narcissism, BoonSlot.Passive,
            "CHARMED ENEMIES GET JEALOUS OF EACH OTHER AND SLAP THE CLOSEST ONE FOR {0} DAMAGE EVERY SECOND.", "draw:brokenheart", V(6, 9, 12));
        Add("flawless", "FLAWLESS", Patron.Narcissism, BoonSlot.Passive,
            "AT FULL HEALTH YOU DEAL +{0}% DAMAGE. A SCRATCH? THAT IS JUST CHARACTER.", "draw:diamond", V(20, 30, 40));
        Add("hairspray", "HAIR SPRAY", Patron.Narcissism, BoonSlot.Attack,
            "HITS LEAVE A STICKY GLOSS FOR 3S: ENEMIES ARE SLOWED 30% AND TAKE +{0}% DAMAGE FROM YOU.", "draw:spraycan", V(15, 22, 30));

        // ---------------------------------------------------------------- SEA ABYSS
        Add("wipeout", "WIPEOUT", Patron.Abyss, BoonSlot.Dash,
            "WHEN YOU SURF, YOU BECOME THE WAVE: ENEMIES IN YOUR WAY ARE SWEPT ALONG AND SLAMMED FOR {0}, MORE INTO A WALL.", "sheet:waterSonic:9:2", V(20, 28, 38));
        Add("undertow", "UNDERTOW", Patron.Abyss, BoonSlot.Attack,
            "HITS DRAG ENEMIES DOWN TOWARD YOU AND SLOW THEM BY {0}% FOR 2S.", "draw:spiral", V(30, 40, 50));
        Add("hangten", "DEPTH CHARGE", Patron.Abyss, BoonSlot.Jump,
            "LANDING FROM A BIG JUMP BURSTS A RING OF BLACK WATER: {0} DAMAGE AND KNOCKBACK.", "draw:board", V(16, 23, 32));
        Add("saltwater", "SALT WATER BLOOD", Patron.Abyss, BoonSlot.Passive,
            "IN THE WATER YOU HEAL {0} HP PER SECOND. LEAVING IT: +20% DAMAGE FOR 4S.", "sheet:manaRecovery:10:6", V(3, 4.5f, 6));
        Add("pressure", "CRUSHING DEPTHS", Patron.Abyss, BoonSlot.Attack,
            "EVERY 5TH HIT, THE WEIGHT OF THE DEEP CRUSHES THE TARGET AND EVERYONE NEAR IT FOR {0}.", "draw:pressure", V(20, 30, 42));
        Add("angler", "ANGLER LURE", Patron.Abyss, BoonSlot.Passive,
            "A GLOWING LURE DANGLES OVER YOUR HEAD. EVERY 2.5S IT ZAPS THE NEAREST ENEMY FOR {0}.", "draw:lure", V(6, 9, 13));
        Add("endlesssurf", "ENDLESS SWELL", Patron.Abyss, BoonSlot.Dash,
            "HOLD {SURF} AND THE WAVE NEVER ENDS: YOU SURF FOR AS LONG AS YOU HOLD IT, ON LAND OR SEA. ENEMIES YOU RIDE INTO TAKE {0}.", "sheet:waterSonic:9:4", V(6, 9, 12));
        Add("ripcurrent", "RIP CURRENT", Patron.Abyss, BoonSlot.Jump,
            "EVERY LANDING PULLS ENEMIES WITHIN 3 TOWARD YOU AND SLOWS THEM {0}% FOR 2S.", "draw:whirl", V(30, 40, 50));
        Add("leviathan", "LEVIATHAN", Patron.Abyss, BoonSlot.Passive,
            "BELOW 30% HEALTH, TENTACLES BURST OUT OF YOUR BODY AND SMASH EVERYTHING AROUND YOU: {0} DAMAGE PER SMASH. NO COOLDOWN: THEY STAY OUT UNTIL YOU HEAL.", "draw:tentacle", V(12, 18, 25));

        // ---------------------------------------------------------------- LYCANTHROPY
        Add("feral", "FERAL SWIPE", Patron.Lycanthropy, BoonSlot.Attack,
            "EVERY HIT RAKES A SECOND TIME WITH SPECTRAL CLAWS FOR {0}% OF ITS DAMAGE.", "sheet:clawSlash:6:0", V(35, 50, 65));
        Add("moonrage", "MOONRAGE", Patron.Lycanthropy, BoonSlot.Passive,
            "AT NIGHT: +{0}% DAMAGE, +15% SPEED AND GLOWING RED EYES.", "draw:moonrage", V(25, 35, 45));
        Add("bloodthirst", "BLOODTHIRST", Patron.Lycanthropy, BoonSlot.Passive,
            "EVERY KILL HEALS {0} HP. PAST FULL HEALTH IT BECOMES OVERHEAL.", "draw:fang", V(4, 6, 9));
        Add("packleader", "PACK LEADER", Patron.Lycanthropy, BoonSlot.Cats,
            "YOUR CATS GROW A BLOOD RED AURA: +{0}% CAT DAMAGE. NICK EXECUTES AT 20% HEALTH.", "draw:paw", V(60, 90, 120));
        BoonDef moon = Add("moon", "CALL OF THE MOON", Patron.Lycanthropy, BoonSlot.Special,
            "PRESS {WOLF} WHEN THE MOON IS FULL: BECOME A WEREWOLF FOR {0}S. CLAWS, SPEED, LIFESTEAL.", "draw:moonwolf", F(12));
        moon.legendaryOnly = true;
        Add("huntmark", "HUNTERS MARK", Patron.Lycanthropy, BoonSlot.Passive,
            "THE BIGGEST PREY IN SIGHT IS MARKED. YOU DEAL +{0}% DAMAGE TO IT, AND ITS DEATH HEALS 10.", "draw:mark", V(30, 45, 60));
        Add("pounce", "POUNCE", Patron.Lycanthropy, BoonSlot.Jump,
            "LANDING NEXT TO ENEMIES RAKES THEM WITH CLAWS: {0} DAMAGE AND THEY BLEED.", "sheet:clawSlash:6:2", V(12, 18, 25));
        Add("openwounds", "OPEN WOUNDS", Patron.Lycanthropy, BoonSlot.Attack,
            "+10% CRIT CHANCE. CRITICAL HITS MAKE ENEMIES BLEED {0} DAMAGE PER SECOND FOR 4S.", "draw:drop", V(6, 9, 13));
        Add("alpharoar", "ALPHA ROAR", Patron.Lycanthropy, BoonSlot.Dash,
            "SURF DASH ROARS: ENEMIES NEAR YOU ARE TERRIFIED FOR {0}S.", "sheet:fear:9:5", V(1.2f, 1.6f, 2.1f));
        Add("feast", "MOON FEAST", Patron.Lycanthropy, BoonSlot.Passive,
            "THE MOON METER FILLS {0}% FASTER. AS A WEREWOLF, EVERY KILL HEALS 3.", "draw:moonbite", V(40, 60, 80)).condition = () => Boons.Has("moon");
        Add("silverfur", "SILVER FUR", Patron.Lycanthropy, BoonSlot.Passive,
            "AT NIGHT YOU TAKE {0}% LESS DAMAGE.", "draw:fur", V(15, 22, 30));

        // ---------------------------------------------------------------- STEPHMOSS (was Mama Rot)
        Add("sporestep", "SPORE STEP", Patron.Rot, BoonSlot.Jump,
            "EVERY LANDING PUFFS A MUSHROOM CLOUD: {0} POISON DAMAGE PER SECOND FOR 3S.", "flora:1:toxic", V(8, 12, 16));
        Add("rottenedge", "ROTTEN EDGE", Patron.Rot, BoonSlot.Attack,
            "ALL YOUR HITS POISON: {0} DAMAGE PER SECOND FOR 4S.", "item:vfxPoison:10:4", V(6, 9, 13));
        Add("overgrowth", "EARTH ERUPT", Patron.Rot, BoonSlot.Passive,
            "POISONED ENEMIES THAT DIE MAKE THE EARTH ERUPT, ROOTING EVERYONE NEARBY FOR {0}S.", "sheet:earthPillar:1:0", V(1.5f, 2f, 2.5f));
        Add("compost", "COMPOST", Patron.Rot, BoonSlot.Cats,
            "EVERY TIME A CAT ATTACKS OR USES ITS POWER, YOU HEAL {0} HP.", "item:vfxHeal:10:4", V(2, 3, 4));
        Add("fester", "FESTER", Patron.Rot, BoonSlot.Passive,
            "POISONED ENEMIES TAKE +{0}% DAMAGE FROM YOUR HITS.", "draw:fester", V(20, 30, 40));
        Add("plague", "PLAGUE", Patron.Rot, BoonSlot.Passive,
            "WHEN A POISONED ENEMY DIES, ITS POISON JUMPS TO {0} ENEMIES NEARBY.", "draw:plague", V(2, 3, 4));
        Add("thornskin", "THORN SKIN", Patron.Rot, BoonSlot.Passive,
            "ENEMIES THAT HIT YOU TAKE {0} THORN DAMAGE AND GET POISONED.", "draw:thorn", V(10, 16, 22));
        Add("rootsnare", "ROOT SNARE", Patron.Rot, BoonSlot.Dash,
            "SURF DASH LEAVES GRASPING ROOTS WHERE YOU STARTED. ENEMIES THAT STEP IN ARE ROOTED {0}S AND POISONED.", "draw:roots", V(1.5f, 2f, 2.5f));
        BoonDef steph = Add("stephmoss", "BECOME STEPHMOSS", Patron.Rot, BoonSlot.Special,
            "PRESS {WOLF} WHEN THE SWARM IS READY: BECOME STEPHMOSS FOR {0}S. WALK TO ROLL A WHIRLWIND, DASH TO TELEPORT, ATTACK FOR A CROSS BLADE, DOWN + ATTACK FOR EXPLODING MOSQUITOES.", "draw:sporestep", F(12));
        steph.legendaryOnly = true;
        steph.iconTint = new Color(0.85f, 1f, 0.4f);
        Add("bloom", "BLOOM", Patron.Rot, BoonSlot.Passive,
            "EVERY 5TH POISON KILL GROWS A FLOWER. TOUCH IT TO HEAL {0} HP.", "flora:0", V(10, 15, 20));
        Add("sporelob", "SPORE LOB", Patron.Rot, BoonSlot.Attack,
            "EVERY 3RD SWING LOBS A SPORE POD THAT BURSTS INTO A POISON CLOUD: {0} DAMAGE PER SECOND.", "draw:pod", V(5, 8, 11));

        // ---------------------------------------------------------------- STINKY BOIS GUILD
        Add("catnip", "CATNIP FRENZY", Patron.Guild, BoonSlot.Cats,
            "YOUR CATS COOL DOWN {0}% FASTER AND LEAVE SPARKLY TRAILS.", "draw:leaf", V(25, 35, 45));
        Add("ninelives", "NINE LIVES", Patron.Guild, BoonSlot.Passive,
            "ONCE PER LIFE, A DEADLY HIT LEAVES YOU AT {0}% HEALTH INSTEAD. THE GUILD SAVES YOU.", "draw:ninelives", V(20, 35, 50));
        Add("furcoat", "FUR COAT", Patron.Guild, BoonSlot.Passive,
            "-{0}% DAMAGE TAKEN FOR EVERY CAT WITH YOU (UP TO 8). IT SMELLS, BUT IT WORKS.", "draw:fur", V(3, 4, 5));
        Add("felinefury", "FELINE FURY", Patron.Guild, BoonSlot.Attack,
            "EVERY 4TH HIT, A GHOST CAT POUNCES ON THE ENEMY FOR {0} DAMAGE.", "draw:catface", V(22, 32, 44));
        Add("catcall", "CAT CALL", Patron.Guild, BoonSlot.Dash,
            "SURF DASH WAKES YOUR LEADER AND SUB-LEADER: THEIR POWERS ARE READY AGAIN. EVERY OTHER CAT GETS {0}% OF ITS COOLDOWN CUT.", "draw:bell", V(10, 12, 15));
        Add("straytax", "STRAY TAX", Patron.Guild, BoonSlot.Passive,
            "KILLS HAVE A {0}% CHANCE TO DROP A FISH TREAT. GRAB IT: HEAL 8 AND YOUR CATS COOL DOWN.", "draw:fish", V(8, 12, 16));
        Add("hairball", "HAIRBALL", Patron.Guild, BoonSlot.Jump,
            "WHEN YOU JUMP, ONE OF YOUR CATS HACKS A HAIRBALL AT THE NEAREST ENEMY: {0} DAMAGE, SLOWED 40%.", "draw:hairball", V(10, 15, 20)).condition = () => Boons.CatsWithRowdy > 0;
        Add("ambush", "ALLEY AMBUSH", Patron.Guild, BoonSlot.Dash,
            "SURF DASH SENDS YOUR CATS LEAPING AT THE NEAREST ENEMIES: {0} DAMAGE EACH.", "draw:ambush", V(12, 18, 25)).condition = () => Boons.CatsWithRowdy > 0;
        Add("stench", "STENCH", Patron.Guild, BoonSlot.Cats,
            "YOUR CATS REEK. ENEMIES NEAR THEM TAKE {0} DAMAGE PER SECOND AND MOVE 25% SLOWER.", "draw:stench", V(3, 5, 7));
        Add("topcat", "TOP CAT", Patron.Guild, BoonSlot.Cats,
            "YOUR PARTY LEADER DEALS +{0}% DAMAGE AND WEARS A TINY CROWN. (PICK THE LEADER IN THE CAT PARTY.)", "draw:topcat", V(50, 75, 100));
        Add("catloyalty", "CAT LOYALTY", Patron.Guild, BoonSlot.Passive,
            "YOUR CAT PARTY IS ALWAYS FULL: EVERY FREE SLOT CALLS A CAT TO YOU, NEW SLOTS TOO. YOU NEVER LOSE A CAT, NOT EVEN WHEN YOU DIE. CATS DEAL +{0}% DAMAGE.", "draw:ninelives", V(5, 10, 15));

        // ---------------------------------------------------------------- CRAZY CHEF
        Add("sandwich", "SANDWICH TIME", Patron.Chef, BoonSlot.Passive,
            "EVERY {0} KILLS A GIANT SANDWICH FALLS FROM THE SKY. EAT IT: HEAL 25 AND +20% DAMAGE FOR 8S.", "draw:sandwich", V(14, 11, 8));
        Add("meatball", "MEATBALL MORTAR", Patron.Chef, BoonSlot.Attack,
            "EVERY 3RD SWING LOBS A MEATBALL THAT EXPLODES IN SAUCE: {0} DAMAGE AROUND IT.", "food:armondega", V(17, 24, 33));
        Add("eggs", "EGG TOSS", Patron.Chef, BoonSlot.Dash,
            "SURF DASH THROWS AN EGG AT EVERY ENEMY ON SCREEN. THEY CRACK: {0} DAMAGE AND YOLK IN THE EYES (STUNNED 0.8S).", "food:ovo", V(8, 12, 16));
        Add("tomato", "TOMATO SPLAT", Patron.Chef, BoonSlot.Jump,
            "LANDING SQUASHES A BAG OF TOMATOES: {0} DAMAGE AROUND YOU, AND ENEMIES SLIP (SLOWED 40%).", "food:tomate", V(10, 15, 21));
        Add("cheese", "GRATE EXPECTATIONS", Patron.Chef, BoonSlot.Passive,
            "+{0}% CRIT CHANCE AND +20% CRIT DAMAGE. CRITICAL HITS SPRAY CHEESE.", "food:guejo", V(8, 12, 16));
        Add("secretsauce", "SECRET SAUCE", Patron.Chef, BoonSlot.Passive,
            "ALL YOUR HEALING IS {0}% STRONGER, AND EVERY BIG HEAL SPLASHES HOT SAUCE ON ENEMIES NEAR YOU FOR 10.", "draw:sauce", V(25, 40, 55));
        Add("foodfight", "FOOD FIGHT", Patron.Chef, BoonSlot.Cats,
            "WHEN A CAT ATTACKS, IT ALSO THROWS FOOD AT A NEARBY ENEMY: {0} DAMAGE.", "food:pao", V(9, 13, 18));
        Add("foodstock", "FOOD STOCK", Patron.Chef, BoonSlot.Passive,
            "EVERY FOOD YOUR BOONS MAKE COMES X3: THREE SANDWICHES, THREE EGGS PER ENEMY, NINE TOMATOES, THREE MEATBALLS... FOOD DEALS +{0}% DAMAGE.", "food:pao", V(5, 10, 15))
            .condition = () => Boons.Has("sandwich") || Boons.Has("eggs") || Boons.Has("tomato") || Boons.Has("meatball") || Boons.Has("foodfight") || Boons.Has("sandwichrain");
        Add("satisfied", "SATISFIED", Patron.Chef, BoonSlot.Passive,
            "YOU CAN OVERHEAL TWICE AS MUCH: A NEW BAR GROWS OVER THE GOLDEN ONE. ALL YOUR HEALING IS {0}% STRONGER.", "draw:sauce", V(5, 10, 15)).iconTint = new Color(1f, 0.6f, 0.9f);
        BoonDef rain = Add("sandwichrain", "INGREDIENT RAIN", Patron.Chef, BoonSlot.Special,
            "PRESS {WOLF} WHEN THE KITCHEN IS READY: FOR 6S EVERY SANDWICH INGREDIENT POURS FROM THE SKY. FOOD THAT HITS AN ENEMY DEALS DAMAGE, FOOD YOU CATCH HEALS YOU. LONG COOLDOWN.", "draw:sandwich", F(1));
        rain.legendaryOnly = true;
        rain.iconTint = new Color(1f, 0.85f, 0.5f);

        // ---------------------------------------------------------------- THE BLACKSMITH
        Add("weaponsnob", "WEAPON SNOB", Patron.Smith, BoonSlot.Passive,
            "WEAPONS WEAR OUT TWICE AS FAST. WHEN ONE BREAKS IT EXPLODES FOR {0}.", "draw:brokensword", V(40, 60, 80)).excludes = new[] { "gearedup" };
        Add("whetstone", "WHETSTONE", Patron.Smith, BoonSlot.Passive,
            "YOU ATTACK {0}% FASTER.", "draw:whetstone", V(12, 18, 25));
        Add("tempered", "TEMPERED STEEL", Patron.Smith, BoonSlot.Passive,
            "SWINGS HAVE A {0}% CHANCE TO COST NO DURABILITY. WEAPONS YOU PICK UP GET +25% DURABILITY.", "draw:ingot", V(25, 35, 50));
        Add("hookline", "HOOK LINE AND SINKER", Patron.Smith, BoonSlot.Passive,
            "ROD: YOUR ROD DEALS DOUBLE DAMAGE. HITS REEL ENEMIES IN, AND EVERY 3RD ROD HIT ON THE SAME ENEMY STUNS IT AND DEALS {0} MORE.", "draw:hook", V(15, 22, 30));
        Add("splitedge", "SPLIT EDGE", Patron.Smith, BoonSlot.Passive,
            "SWORD: EVERY SWING ALSO FIRES A STEEL WAVE THAT CUTS THROUGH ENEMIES FOR {0} DAMAGE.", "draw:steelwave", V(8, 12, 16));
        Add("skewer", "SKEWER", Patron.Smith, BoonSlot.Passive,
            "NAGINATA: HITS PIERCE THROUGH, STRIKING UP TO 3 ENEMIES BEHIND THE TARGET FOR {0}% DAMAGE.", "draw:spear", V(60, 80, 100)).excludes = new[] { "onrush" };
        Add("butcher", "BUTCHER BLOCK", Patron.Smith, BoonSlot.Passive,
            "CLEAVER: EVERY ATTACK CHOPS 5 TIMES INSTEAD OF 3, AND HITS ON ENEMIES BELOW 40% HEALTH DEAL X{0} DAMAGE.", "draw:cleaver", V(1.6f, 2f, 2.5f));
        Add("gearedup", "GEARED UP", Patron.Smith, BoonSlot.Passive,
            "YOUR WEAPONS NEVER LOSE DURABILITY, AND THEY HIT {0}% HARDER.", "draw:ingot", V(5, 10, 15)).excludes = new[] { "weaponsnob", "justrod" };
        Add("justrod", "JUST THE ROD, PLEASE.", Patron.Smith, BoonSlot.Passive,
            "THE ROD DEALS X{0} DAMAGE. BUT YOU CAN NOT PICK UP ANY OTHER WEAPON.", "draw:hook", V(5, 5, 5, 5)).excludes = new[] { "gearedup" };
        Add("sharp", "SHARP WEAPONS", Patron.Smith, BoonSlot.Passive,
            "A WEAPON PICKED UP PAST FULL DURABILITY GETS SHARPENED: +1% DAMAGE FOR EVERY 1% OF SHARPENING, UP TO +{0}%. IT WEARS OFF FAST, LIKE OVERHEAL.", "draw:whetstone", V(50, 75, 100));
        Add("dizzy", "DIZZY FIGHTER", Patron.Smith, BoonSlot.Passive,
            "SWORD: HOLD {ATTACK} IN THE AIR AND YOU NEVER STOP SPINNING, SLASHING {0} TIMES A SECOND.", "draw:steelwave", V(6, 8, 10));
        Add("swordcharge", "GRAND SLASH", Patron.Smith, BoonSlot.Passive,
            "SWORD: AFTER DOWN + ATTACK, KEEP HOLDING {ATTACK} TO CHARGE UP TO 3 TICKS. EVERY TICK MAKES THE SLASH BIGGER: UP TO {0} DAMAGE.", "draw:steelwave", V(40, 60, 80));
        Add("skybeam", "SKY SPEAR", Patron.Smith, BoonSlot.Passive,
            "NAGINATA: DOWN + ATTACK SUMMONS 5 MAGIC SPEARS THAT BURST UP OUT OF THE GROUND ONE AFTER ANOTHER, BEHIND YOU, UNDER YOU AND IN FRONT OF YOU: {0} DAMAGE EACH, AND THEY LAUNCH ENEMIES UP.", "draw:spear", V(14, 20, 28));
        Add("onrush", "ONRUSH", Patron.Smith, BoonSlot.Passive,
            "NAGINATA: HOLD {ATTACK} ON THE GROUND TO DRILL NONSTOP: A HIT EVERY 0.08S FOR {0}% DAMAGE. IT DOES NOT PIERCE. DRILLING WEARS THE NAGINATA DOWN: 2 DURABILITY PER SECOND WHILE YOU HOLD IT.", "draw:spear", V(40, 50, 60)).excludes = new[] { "skewer" };
        Add("quake", "GROUND BREAKER", Patron.Smith, BoonSlot.Passive,
            "NAGINATA: A JUMP ATTACK THAT HITS THE GROUND CAUSES AN EARTHQUAKE: {0} DAMAGE, AND ENEMIES NEARBY ARE STUNNED 1.5S.", "draw:pressure", V(20, 30, 40));
        Add("apron", "MEAT SHIELD", Patron.Smith, BoonSlot.Passive,
            "CLEAVER: DOWN + ATTACK RAISES A SHIELD THAT BLOCKS THE NEXT HIT YOU TAKE. WHILE IT IS UP, ARROWS AND BOMBS THAT HIT IT BOUNCE BACK AT THE ENEMIES FOR 500% DAMAGE (THAT DOES NOT BREAK IT). READY AGAIN AFTER {0}S.", "draw:ricochet", V(8, 6, 4));
        Add("slices", "FIVE SLICE", Patron.Smith, BoonSlot.Passive,
            "CLEAVER: LAND A JUMP ATTACK AND 1S LATER THE ENEMY IS SLICED 5 TIMES FOR {0}% DAMAGE EACH. THE SLICES COUNT AS ATTACKS.", "draw:cleaver", V(40, 55, 70));
        Add("armory", "THE ARMORY", Patron.Smith, BoonSlot.Passive,
            "{0} ENCHANTED SWORDS FLOAT AROUND YOU AND SLASH THE NEAREST ENEMIES BY THEMSELVES, EVERY 1.4S EACH.", "draw:swords",
            new float[] { 1, 1, 2, 2, 3 }).iconTint = new Color(1f, 0.8f, 0.55f);
        BoonDef bankai = Add("senbonzakura", "SENBONZAKURA KAGEYOSHI", Patron.Smith, BoonSlot.Special,
            "PRESS {WOLF} WHEN THE PETALS ARE READY. BANKAI: YOUR BLADE SCATTERS INTO A THOUSAND TINY BLADES LIKE CHERRY PETALS. FOR {0}S THEY STREAM THROUGH YOUR ENEMIES, CUTTING EVERYTHING THEY TOUCH, THEN CLOSE IN ON THE BIGGEST CROWD AND CRUSH IT. LONG COOLDOWN.", "draw:sakura", F(9));
        bankai.legendaryOnly = true;
        bankai.iconTint = new Color(1f, 0.75f, 0.9f);

        // ---------------------------------------------------------------- THE SUN GOD
        Add("solarflare", "SOLAR FLARE", Patron.Sun, BoonSlot.Attack,
            "EVERY 4TH HIT CALLS A PILLAR OF SUNLIGHT ON THE TARGET: {0} DAMAGE TO EVERYTHING IN IT.", "draw:sunbeam", V(20, 30, 42));
        Add("sunburn", "SUNBURN", Patron.Sun, BoonSlot.Attack,
            "HITS SET ENEMIES ABLAZE: {0} DAMAGE PER SECOND FOR 3S.", "draw:flame", V(5, 8, 11));
        Add("daybreak", "DAYBREAK", Patron.Sun, BoonSlot.Passive,
            "IN DAYLIGHT: +{0}% DAMAGE AND YOU HEAL 1 HP EVERY 2 SECONDS.", "draw:sun", V(15, 22, 30));
        Add("halo", "HALO", Patron.Sun, BoonSlot.Passive,
            "THREE LITTLE SUNS CIRCLE YOU, BURNING ENEMIES THEY TOUCH FOR {0}.", "draw:halo", V(6, 9, 12));
        Add("blinding", "BLINDING DASH", Patron.Sun, BoonSlot.Dash,
            "SURF DASH FLASHES LIKE HIGH NOON: ENEMIES NEAR YOU ARE BLINDED (STUNNED) FOR {0}S.", "draw:flash", V(1f, 1.4f, 1.8f));
        Add("sunspot", "SUNSPOT", Patron.Sun, BoonSlot.Jump,
            "LANDING LEAVES A POOL OF SUNLIGHT FOR 3S: ENEMIES IN IT BURN FOR {0} PER SECOND, YOU HEAL 2 PER SECOND.", "draw:sunspot", V(6, 9, 12));
        BoonDef light = Add("cometothelight", "COME TO THE LIGHT", Patron.Sun, BoonSlot.Passive,
            "THE NIGHT NEVER COMES AGAIN. YOU GIVE UP EVERY NIGHT BOON YOU HAVE, AND FOR EACH ONE THE SUN GOD HANDS YOU ONE OF HIS OWN, AT THE SAME RARITY. NO MORE NIGHT ELITES EITHER.", "draw:sun", F(1));
        light.legendaryOnly = true;
        light.iconTint = new Color(1f, 0.95f, 0.6f);
        light.condition = () => Boons.NightBoonCount >= 2;

        // ---------------------------------------------------------------- DUOS
        string[] abyss = IdsOf(list, Patron.Abyss);
        string[] narc = IdsOf(list, Patron.Narcissism), lyc = IdsOf(list, Patron.Lycanthropy), rot = IdsOf(list, Patron.Rot),
                 guild = IdsOf(list, Patron.Guild), chef = IdsOf(list, Patron.Chef), smith = IdsOf(list, Patron.Smith), sun = IdsOf(list, Patron.Sun);

        Duo("beachbod", "BEACH BOD", Patron.Narcissism, Patron.Abyss,
            "THE WAVE YOU BECOME IS A GORGEOUS PINK WAVE: +50% DAMAGE, AND EVERYONE IT SWEEPS IS CHARMED.", "draw:pinkwave",
            "NARCISSISM: SHOW THEM THE BOD, BABY! SEA ABYSS: ...EVEN THE DEEP IS BLUSHING.",
            narc, new[] { "wipeout" }).iconTint = new Color(1f, 0.55f, 0.85f);
        Duo("redtide", "RED TIDE", Patron.Rot, Patron.Abyss,
            "YOUR WAVES AND WATER BURSTS ARE TOXIC: THEY POISON FOR 8 DAMAGE PER SECOND.", "sheet:waterSonic:9:6",
            "STEPHMOSS: A LITTLE VENOM IN THE TIDE. SEA ABYSS: THE DEEP APPROVES.",
            rot, new[] { "wipeout", "hangten", "saltwater", "pressure", "ripcurrent" }).iconTint = new Color(0.55f, 1f, 0.35f);
        Duo("wolfwhistle", "WOLF WHISTLE", Patron.Narcissism, Patron.Lycanthropy,
            "EVERY FEW SECONDS, ENEMIES NEAR YOU MAY FREEZE TO STARE AT YOU (CHARMED 1.5S).", "sheet:charm:9:5",
            "NARCISSISM: THEY CANNOT LOOK AWAY. LYCANTHROPY: NEITHER CAN THEIR PREDATOR.", narc, lyc);
        Duo("wolfpack", "WOLF PACK", Patron.Lycanthropy, Patron.Guild,
            "AS A WEREWOLF OR AT NIGHT, YOUR CATS TURN FERAL: DOUBLE DAMAGE, HALF COOLDOWNS.", "sheet:fear:9:4",
            "LYCANTHROPY: THE KITTENS HAVE TEETH. THE GUILD: WE ALWAYS HAD, MUTT.", lyc, guild);
        Duo("glamourpuss", "GLAMOUR PUSS", Patron.Narcissism, Patron.Guild,
            "+3% CRIT CHANCE FOR EVERY CAT WITH YOU. CRITS MAKE THEM SPARKLE.", "draw:catface",
            "NARCISSISM: THE CATS NEED A MAKEOVER. THE GUILD: WE ARE NOT TAKING A BATH.", narc, guild).iconTint = new Color(1f, 0.85f, 0.4f);
        Duo("eclipse", "ECLIPSE", Patron.Sun, Patron.Lycanthropy,
            "DAY AND NIGHT AT ONCE: YOUR DAY BOONS AND YOUR NIGHT BOONS ALWAYS WORK, AND THE MOON METER ALWAYS FILLS AT NIGHT SPEED.", "draw:eclipse",
            "SUN GOD: ...FINE. WE SHARE THE SKY. LYCANTHROPY: JUST THIS ONCE, SHOW-OFF.",
            new[] { "daybreak", "halo", "solarflare", "sunburn", "blinding", "sunspot" }, new[] { "moonrage", "silverfur", "moon", "feast" });
        Duo("forgefire", "FORGED IN SUNLIGHT", Patron.Smith, Patron.Sun,
            "YOUR WEAPON GLOWS WHITE HOT: EVERY HIT BURNS FOR 6 PER SECOND, AND WEAPONS LOSE DURABILITY 25% SLOWER.", "draw:hotblade",
            "THE BLACKSMITH: FINALLY, A DECENT FURNACE. SUN GOD: I AM NOT A FURNACE! ...OK, A LITTLE.", smith, sun);
        Duo("catfood", "GOURMET KIBBLE", Patron.Chef, Patron.Guild,
            "YOUR CATS EAT LIKE KINGS: +40% CAT DAMAGE, AND EVERY SANDWICH OR FISH TREAT READIES ALL THEIR POWERS.", "draw:fish",
            "CRAZY CHEF: FOR THE KITTIES, ONLY THE BEST! THE GUILD: WE WOULD HAVE EATEN THE BOX. BUT THANKS.", chef, guild).iconTint = new Color(1f, 0.8f, 0.5f);
        Duo("fermented", "FERMENTATION", Patron.Chef, Patron.Rot,
            "YOUR FOOD IS... AGED. MEATBALLS, EGGS AND TOMATOES POISON FOR 8 DAMAGE PER SECOND.", "draw:fester",
            "CRAZY CHEF: IT IS NOT ROTTEN, IT IS AGED! STEPHMOSS: AGED, ROTTEN, POISONED. ALL DELICIOUS.", chef, rot).iconTint = new Color(0.8f, 1f, 0.5f);
        Duo("silverclaws", "SILVER CLAWS", Patron.Smith, Patron.Lycanthropy,
            "THE BLACKSMITH FORGES YOUR CLAWS: FERAL SWIPE, POUNCE AND WEREWOLF CLAWS DEAL DOUBLE DAMAGE.", "sheet:clawSlash:6:3",
            "THE BLACKSMITH: SILVER. ON A WEREWOLF. I KNOW. LYCANTHROPY: IT TICKLES.", smith, new[] { "feral", "pounce", "moon" }).iconTint = new Color(0.85f, 0.9f, 1f);

        // ---- mega fix 10
        string[] rotPoison = { "rottenedge", "sporestep", "sporelob", "thornskin", "rootsnare" };
        Duo("jellypals", "JELLY BUDDIES", Patron.Guild, Patron.Abyss,
            "FOR EVERY CAT YOU HAVE, A LITTLE BLUE JELLYFISH FIGHTS BY YOUR SIDE: IT CIRCLES YOU, CHARGES UP AND JETS THROUGH ENEMIES.", "draw:tentacle",
            "THE GUILD: THEY ARE WET CATS. WE ALLOW IT. SEA ABYSS: ...THEY ARE NOT CATS.", guild, abyss).iconTint = new Color(0.5f, 0.8f, 1f);
        Duo("peakplate", "TRIPLE PLATED", Patron.Smith, Patron.Guild,
            "THE PEAK GIVES YOU 3 ARMORS AT ONCE: 9 BLOCKED HITS INSTEAD OF 3.", "draw:ingot",
            "THE BLACKSMITH: I MADE IT A LITTLE HELMET. AND TWO SPARES. THE GUILD: HE WEARS THEM ALL AT ONCE.", smith, guild).iconTint = new Color(0.7f, 0.9f, 1f);
        Duo("scavenger", "SCAVENGER", Patron.Smith, Patron.Guild,
            "THE CATS GO THROUGH THE POCKETS: EVERY KILL HAS A 5% CHANCE TO DROP EVERY WEAPON AT ONCE.", "draw:brokensword",
            "THE GUILD: FINDERS KEEPERS. THE BLACKSMITH: ...AT LEAST CLEAN THEM FIRST.", smith, guild);
        Duo("clawsharpener", "CLAW SHARPENER", Patron.Guild, Patron.Smith,
            "CATS SHARPEN YOUR WEAPON: EVERY CAT ATTACK RESTORES 1 DURABILITY, AND WIG SCRATCHES CAN CRIT.", "draw:whetstone",
            "THE BLACKSMITH: WHO TAUGHT THE CATS TO SHARPEN? THE GUILD: YOU DID. WE WATCHED.", guild, smith);
        Duo("toxicbeauty", "TOXIC BEAUTY", Patron.Narcissism, Patron.Rot,
            "CHARMED ENEMIES ARE POISONED TOO, AND POISONED ENEMIES STAY CHARMED 50% LONGER.", "sheet:charm:9:3",
            "NARCISSISM: GORGEOUS AND DEADLY. STEPHMOSS: MOSTLY DEADLY.", narc, rotPoison).iconTint = new Color(0.7f, 1f, 0.5f);
        Duo("foodcritic", "FOOD CRITIC", Patron.Narcissism, Patron.Chef,
            "EATING A SANDWICH OR A FISH TREAT PUTS YOU IN THE SPOTLIGHT FOR 5S: +30% DAMAGE, AND ENEMIES NEARBY ARE CHARMED WHILE THEY WATCH YOU EAT.", "draw:spotlight",
            "CRAZY CHEF: MANGIA, BELLO! NARCISSISM: EVERYONE, WATCH HIM CHEW.", narc, chef);
        Duo("fishingcats", "FISHING CATS", Patron.Abyss, Patron.Guild,
            "CATS THAT ATTACK ENEMIES STANDING IN WATER DRAG THEM UNDER: DOUBLE CAT DAMAGE, AND EVERY WATER KILL DROPS A FISH TREAT.", "draw:fish",
            "THE GUILD: FISH. FISH! SEA ABYSS: ...I REGRET THIS.", abyss, guild).iconTint = new Color(0.55f, 0.85f, 1f);
        Duo("rabid", "RABID", Patron.Lycanthropy, Patron.Rot,
            "YOUR CLAW HITS POISON, AND EVERY POISONED ENEMY THAT DIES FILLS THE MOON METER BY 5%.", "sheet:clawSlash:6:1",
            "LYCANTHROPY: FOAM AT THE MOUTH, BABY. STEPHMOSS: DISGUSTING. APPROVED.", new[] { "feral", "pounce" }, rotPoison).iconTint = new Color(0.75f, 1f, 0.45f);
        Duo("moldcats", "MOLD CATS", Patron.Rot, Patron.Guild,
            "CAT ATTACKS POISON, AND STENCH SPREADS POISON TOO.", "draw:stench",
            "THE GUILD: WE WERE ALWAYS THIS STINKY. STEPHMOSS: NOW IT IS MEDICAL.", rot, guild).iconTint = new Color(0.7f, 1f, 0.45f);
        Duo("rustededge", "RUSTED EDGE", Patron.Rot, Patron.Smith,
            "YOUR WEAPON IS RUSTY: -10% DURABILITY COST, EVERY HIT POISONS, AND BROKEN WEAPONS LEAVE A POISON CLOUD.", "draw:brokensword",
            "THE BLACKSMITH: IT IS NOT RUST, IT IS PATINA. STEPHMOSS: IT IS ROT. AND IT IS BEAUTIFUL.", rot, smith).iconTint = new Color(0.8f, 0.9f, 0.45f);
        Duo("photosynthesis", "PHOTOSYNTHESIS", Patron.Rot, Patron.Sun,
            "IN DAYLIGHT YOUR MUSHROOMS AND FLOWERS GROW TWICE AS FAST, AND YOUR BLOOM FLOWERS ALSO LEAVE A SUNSPOT.", "flora:2",
            "SUN GOD: GROW, LITTLE MUSHROOMS. STEPHMOSS: THEY DO NOT NEED THE SUN. ...THEY DO LIKE IT.", new[] { "bloom", "sporestep" }, new[] { "daybreak", "halo", "sunspot" });
        Duo("sunbathing", "SUNBATHING", Patron.Guild, Patron.Sun,
            "IN DAYLIGHT, CATS WITH THEIR POWER READY GLOW AND HEAL YOU 1 HP EVERY 3S, AND CAT COOLDOWNS ARE 20% SHORTER.", "draw:sun",
            "THE GUILD: FIVE MORE MINUTES IN THE SUN. SUN GOD: TAKE TEN.", guild, sun).iconTint = new Color(1f, 0.85f, 0.5f);
        Duo("midnightsnack", "MIDNIGHT SNACK", Patron.Lycanthropy, Patron.Chef,
            "FOOD EATEN AS A WEREWOLF MAKES THE WEREWOLF LAST LONGER. FOOD EATEN AS ROWDY FILLS THE MOON METER.", "draw:moonbite",
            "LYCANTHROPY: MEAT. MORE MEAT. CRAZY CHEF: FINALLY, SOMEBODY WHO FINISHES THE PLATE!", new[] { "moon", "feast" }, chef);

        return list;
    }
}
