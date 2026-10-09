using System.Collections.Generic;
using UnityEngine;

// Every patron and every boon: names, slots, numbers per rarity, icons and what the patrons say.
// Texts use the pixel font: A-Z 0-9 . , : ! ? % / - + < > ( ) ' only.
//   {0} {1} in a description = the boon's values for the rarity shown (Common / Rare / Epic / Legendary)
//   Slots: one boon per Attack / Dash / Jump / Cats / Special slot (a new one replaces the old), Passives stack.
public enum Patron { Pompadour, Riptide, Howl, Rot, Disco, Meow, Hammock }
public enum BoonSlot { Attack, Dash, Jump, Cats, Special, Passive }
public enum Rarity { Common, Rare, Epic, Legendary, Duo }

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
    public float[][] values;         // values[i] = { common, rare, epic, legendary }
    public string icon;              // BoonIcons key
    public Color iconTint = Color.white;
    public string duoLine;           // what the two patrons say together
    public bool legendaryOnly;
    public string[] requires;        // duo: one of each list... see Boons.Eligible (ids of boons, any one per patron)
    public System.Func<bool> condition;

    public bool IsDuo => partner.HasValue;

    public float Value(int index, Rarity r)
    {
        if (values == null || index < 0 || index >= values.Length) return 0f;
        float[] v = values[index];
        // Jarvis balance: its own numbers for this boon (Balance.BoonValues), Lamp: the ones below
        if (Balance.Jarvis && Balance.BoonValues.TryGetValue(id, out float[][] jarvis) && index < jarvis.Length) v = jarvis[index];
        int i = r == Rarity.Duo ? 0 : Mathf.Min((int)r, v.Length - 1);
        if ((int)r == 3 && v.Length < 4) return v[v.Length - 1] * 1.25f; // legendary of a 3-value boon
        return v[i];
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
        new Color(0.35f, 0.7f, 1f),       // rare
        new Color(0.78f, 0.4f, 1f),       // epic
        new Color(1f, 0.6f, 0.15f),       // legendary
        new Color(0.6f, 1f, 0.45f),       // duo
    };
    public static readonly string[] RarityNames = { "COMMON", "RARE", "EPIC", "LEGENDARY", "DUO" };
    public static readonly string[] SlotNames = { "ATTACK", "DASH", "JUMP", "CATS", "SPECIAL", "PASSIVE" };

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

    private static float[] V(float c, float r, float e) => new[] { c, r, e };
    private static float[] V(float c, float r, float e, float l) => new[] { c, r, e, l };
    private static float[] F(float v) => new[] { v, v, v, v };

    private static Dictionary<Patron, PatronInfo> BuildPatrons()
    {
        var d = new Dictionary<Patron, PatronInfo>();
        void Add(Patron id, string name, string title, Color color, Color accent, string emblem, string[] greet, string[] pick)
            => d[id] = new PatronInfo { id = id, name = name, title = title, color = color, accent = accent, emblem = emblem, greet = greet, pick = pick };

        Add(Patron.Pompadour, "THE POMPADOUR", "PATRON OF GORGEOUS", new Color(1f, 0.37f, 0.78f), new Color(1f, 0.82f, 0.3f), "draw:hairflip",
            new[] { "OH. MY. GOD. LOOK AT YOU, GORGEOUS. NOW LOOK AT ME.", "A LITTLE VOLUME NEVER HURT ANYBODY, DARLING.",
                    "YOU HAVE THE HAIR. I HAVE THE POWER. LET US BE ICONIC.", "BEAUTY IS PAIN, BABY. MOSTLY THEIR PAIN.",
                    "THAT FACE? WASTED ON MONSTERS. LET US FIX THAT." },
            new[] { "SERVE IT. SERVE IT ALL.", "MWAH! NOW GO MAKE THEM JEALOUS.", "FLAWLESS. AS EXPECTED." });

        Add(Patron.Riptide, "RIPTIDE", "PATRON OF THE GNARLY", new Color(0.25f, 0.9f, 1f), new Color(0.85f, 1f, 1f), "draw:wave",
            new[] { "YO BRAH. THE OCEAN TOLD ME YOU NEED A LITTLE PUSH.", "SURF IS UP, DUDE. SO ARE THE BODIES. HEH.",
                    "JUST GO WITH THE FLOW, MAN. THE FLOW GOES THROUGH THEM.", "TOTALLY TUBULAR SWING YOU GOT THERE, BRO.",
                    "SALT IN THE HAIR, SAND IN THE TEETH. LIVING THE DREAM, DUDE." },
            new[] { "COWABUNGA, DANDY!", "RIDE IT OUT, BRAH.", "THE WAVE PROVIDES, MAN." });

        Add(Patron.Howl, "HOWL", "PATRON OF THE HUNGRY MOON", new Color(0.92f, 0.18f, 0.26f), new Color(0.86f, 0.86f, 0.95f), "draw:moonrage",
            new[] { "THE MOON IS WATCHING YOU, LITTLE MAN. BE WORTH IT.", "I SMELL FEAR. NOT YOURS. GOOD.",
                    "TEETH. CLAWS. HUNGER. PICK ONE. OR ALL OF THEM.", "AWOOOO! SORRY. IT JUST COMES OUT.",
                    "THE WOLVES OUT THERE ARE MY CHILDREN. EAT THEM ANYWAY." },
            new[] { "FEED. GROW. HOWL.", "NOW YOU HUNT WITH ME.", "GRRRR. YES." });

        Add(Patron.Rot, "MAMA ROT", "PATRON OF ROT AND BLOOM", new Color(0.55f, 1f, 0.25f), new Color(0.72f, 0.38f, 1f), "draw:sporestep",
            new[] { "HELLO DEARIE. EVERYTHING ROTS. THAT IS WHAT MAKES THE FLOWERS SO PRETTY.", "COME, SWEETIE. MAMA HAS SOMETHING SPECIAL GROWING FOR YOU.",
                    "SUCH A HEALTHY BOY. WE CAN FIX THAT. FOR THEM, I MEAN.", "MY GARDEN NEEDS FERTILIZER, DEARIE. YOU KNOW WHERE IT IS.",
                    "EAT YOUR GREENS. THEN MAKE THEM GREEN." },
            new[] { "THERE YOU GO. NOW GO MAKE MAMA SOME COMPOST.", "GROW BIG AND MEAN, DEARIE.", "SO PROUD OF YOU, SWEETIE." });

        Add(Patron.Disco, "DJ FEVER", "PATRON OF THE GROOVE", new Color(1f, 0.9f, 0.22f), new Color(0.6f, 0.45f, 1f), "draw:mirrorball",
            new[] { "CAN YOU DIG IT?! THE DANCE FLOOR IS CALLING YOUR NAME, BABY!", "LET ME HEAR YOU SAY YEAH! ...I SAID LET ME HEAR YOU!",
                    "TURN IT UP! TURN THEM INTO CONFETTI!", "OH YEAH, THAT IS THE FUNK. I CAN FEEL IT IN MY PLATFORMS.",
                    "EVERY FIGHT IS A PARTY IF YOU ARE THE DJ!" },
            new[] { "OUTTA SIGHT!", "NOW GET DOWN TONIGHT, DANDY!", "DROP THE BASS! AND THEM." });

        Add(Patron.Meow, "MADAME MEOW", "PATRON OF THE CATS", new Color(0.8f, 0.62f, 1f), new Color(1f, 0.72f, 0.56f), "draw:catface",
            new[] { "MY BABIES TELL ME YOU FEED THEM WELL. MRRROW. GOOD BOY.", "WHO IS A GOOD ROWDY? YOU ARE! NOW HUSH, MOMMY IS TALKING.",
                    "NINE LIVES, DARLING. MY BABIES COUNT THEM FOR YOU.", "PSPSPSPSPS. OH, SORRY. FORCE OF HABIT.",
                    "A MAN WITH TEN CATS IS NEVER ALONE. OR SAFE." },
            new[] { "TAKE CARE OF MY BABIES. OR ELSE. MRRP.", "PURRFECT CHOICE.", "THE KITTIES APPROVE." });

        Add(Patron.Hammock, "THE HAMMOCK", "PATRON OF DOING LESS", new Color(0.55f, 0.66f, 0.9f), new Color(0.96f, 0.86f, 0.6f), "draw:naptime",
            new[] { "YAAAWN... OH. HEY. YOU WANT POWER? ...IT IS A LOT OF WORK.", "WHY SWING TWICE WHEN YOU CAN SWING HARDER AND NAP AFTER?",
                    "EVERY DEAL HAS A CATCH, MAN. MINE IS... ZZZ...", "I WAS GONNA HELP YOU EARLIER. THEN I DID NOT.",
                    "GOOD STUFF, BAD STUFF. SAME HAMMOCK, DUDE." },
            new[] { "COOL. COOL COOL COOL. ZZZ.", "WAKE ME WHEN IT IS OVER.", "NO REFUNDS, DUDE." });
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
            var req = new List<string>();
            req.Add(string.Join(",", needA));
            req.Add(string.Join(",", needB));
            d.requires = req.ToArray();
            list.Add(d);
            return d;
        }

        // ---------------------------------------------------------------- THE POMPADOUR
        Add("hairflip", "HAIR FLIP", Patron.Pompadour, BoonSlot.Attack,
            "EVERY 3RD SWING FLIPS YOUR HAIR: A PINK CRESCENT FLIES OUT FOR {0} DAMAGE.", "draw:hairflip", V(18, 26, 36));
        Add("decoy", "TOO PRETTY TO HIT", Patron.Pompadour, BoonSlot.Dash,
            "SURF DASH LEAVES A GORGEOUS DECOY. ENEMIES STARE AT IT FOR {0}S, THEN IT SHATTERS FOR 20.", "sheet:charm:9:4", V(2f, 2.8f, 3.6f));
        Add("admire", "ADMIRE YOURSELF", Patron.Pompadour, BoonSlot.Passive,
            "STAND STILL 1.5S TO STRIKE A POSE. YOUR NEXT HIT DEALS X{0} DAMAGE.", "draw:mirror", V(2.5f, 3f, 3.5f));
        Add("mainchar", "MAIN CHARACTER", Patron.Pompadour, BoonSlot.Passive,
            "+{0}% DAMAGE FOR EVERY STYLE RANK. AT SSS YOU SHINE LIKE THE SUN.", "draw:crown", V(6, 8, 10));

        // ---------------------------------------------------------------- RIPTIDE
        Add("wipeout", "WIPEOUT", Patron.Riptide, BoonSlot.Dash,
            "SURF DASH SENDS A WAVE THAT CARRIES ENEMIES AWAY: {0} DAMAGE, MORE IF THEY HIT A WALL.", "sheet:waterSonic:9:2", V(20, 28, 38));
        Add("undertow", "UNDERTOW", Patron.Riptide, BoonSlot.Attack,
            "HITS DRAG ENEMIES TOWARD YOU AND SLOW THEM BY {0}% FOR 2S.", "draw:spiral", V(30, 40, 50));
        Add("hangten", "HANG TEN", Patron.Riptide, BoonSlot.Jump,
            "LANDING FROM A BIG JUMP SPLASHES A FOAM RING: {0} DAMAGE AND KNOCKBACK.", "draw:board", V(16, 23, 32));
        Add("saltwater", "SALT WATER BLOOD", Patron.Riptide, BoonSlot.Passive,
            "IN THE WATER YOU HEAL {0} HP PER SECOND. LEAVING IT: +20% DAMAGE FOR 4S.", "sheet:manaRecovery:10:6", V(3, 4.5f, 6));

        // ---------------------------------------------------------------- HOWL
        Add("feral", "FERAL SWIPE", Patron.Howl, BoonSlot.Attack,
            "EVERY HIT RAKES A SECOND TIME WITH SPECTRAL CLAWS FOR {0}% OF ITS DAMAGE.", "sheet:clawSlash:6:0", V(35, 50, 65));
        Add("moonrage", "MOONRAGE", Patron.Howl, BoonSlot.Passive,
            "AT NIGHT: +{0}% DAMAGE, +15% SPEED AND GLOWING RED EYES.", "draw:moonrage", V(25, 35, 45));
        Add("bloodthirst", "BLOODTHIRST", Patron.Howl, BoonSlot.Passive,
            "EVERY KILL HEALS {0} HP. PAST FULL HEALTH IT BECOMES OVERHEAL.", "draw:fang", V(4, 6, 9));
        Add("packleader", "PACK LEADER", Patron.Howl, BoonSlot.Cats,
            "YOUR CATS GROW A BLOOD RED AURA: +{0}% CAT DAMAGE. NICK EXECUTES AT 20% HEALTH.", "draw:paw", V(60, 90, 120));
        BoonDef moon = Add("moon", "CALL OF THE MOON", Patron.Howl, BoonSlot.Special,
            "PRESS {WOLF} WHEN THE MOON IS FULL: BECOME A WEREWOLF FOR {0}S. CLAWS, SPEED, LIFESTEAL.", "draw:moonwolf", F(12));
        moon.legendaryOnly = true;

        // ---------------------------------------------------------------- MAMA ROT
        Add("sporestep", "SPORE STEP", Patron.Rot, BoonSlot.Jump,
            "EVERY LANDING PUFFS A MUSHROOM CLOUD: {0} POISON DAMAGE PER SECOND FOR 3S.", "draw:sporestep", V(8, 12, 16));
        Add("rottenedge", "ROTTEN EDGE", Patron.Rot, BoonSlot.Attack,
            "ALL YOUR HITS POISON: {0} DAMAGE PER SECOND FOR 4S.", "item:vfxPoison:10:4", V(6, 9, 13));
        Add("overgrowth", "OVERGROWTH", Patron.Rot, BoonSlot.Passive,
            "POISONED ENEMIES THAT DIE MAKE THE EARTH ERUPT, ROOTING EVERYONE NEARBY FOR {0}S.", "sheet:earthPillar:1:0", V(1.5f, 2f, 2.5f));
        Add("compost", "COMPOST", Patron.Rot, BoonSlot.Cats,
            "EVERY TIME A CAT ATTACKS OR USES ITS POWER, YOU HEAL {0} HP.", "item:vfxHeal:10:4", V(2, 3, 4));

        // ---------------------------------------------------------------- DJ FEVER
        Add("chainslap", "CHAIN SLAP", Patron.Disco, BoonSlot.Attack,
            "HITS ARC LIGHTNING TO {1} MORE ENEMIES FOR {0} DAMAGE EACH.", "sheet:magicalHit:10:3", V(10, 15, 21), V(2, 2, 3));
        Add("nightfever", "NIGHT FEVER", Patron.Disco, BoonSlot.Dash,
            "SURF DASH LAYS A FLASHING DANCE FLOOR. ENEMIES ON IT GET STUNNED FOR {0}S.", "draw:floor", V(1f, 1.4f, 1.8f));
        Add("mirrorball", "MIRROR BALL", Patron.Disco, BoonSlot.Passive,
            "10 KILLS WITHOUT GETTING HIT DROPS A DISCO BALL. ITS BEAMS DEAL {0} DAMAGE FOR 6S.", "draw:mirrorball", V(8, 12, 16));
        Add("funkyfeet", "FUNKY FEET", Patron.Disco, BoonSlot.Jump,
            "EVERY JUMP SHOOTS A SPARK RING: {0} DAMAGE TO ENEMIES AROUND YOU.", "draw:shoe", V(10, 14, 19));

        // ---------------------------------------------------------------- MADAME MEOW
        Add("catnip", "CATNIP FRENZY", Patron.Meow, BoonSlot.Cats,
            "YOUR CATS COOL DOWN {0}% FASTER AND LEAVE SPARKLY TRAILS.", "draw:leaf", V(25, 35, 45));
        Add("ninelives", "NINE LIVES", Patron.Meow, BoonSlot.Passive,
            "ONCE PER LIFE, A DEADLY HIT LEAVES YOU AT {0}% HEALTH INSTEAD. THE CATS SAVE YOU.", "draw:ninelives", V(20, 35, 50));
        Add("furcoat", "FUR COAT", Patron.Meow, BoonSlot.Passive,
            "-{0}% DAMAGE TAKEN FOR EVERY CAT WITH YOU (UP TO 8).", "draw:fur", V(3, 4, 5));
        Add("felinefury", "FELINE FURY", Patron.Meow, BoonSlot.Attack,
            "EVERY 4TH HIT, A GHOST CAT POUNCES ON THE ENEMY FOR {0} DAMAGE.", "draw:catface", V(22, 32, 44));
        Add("catcall", "CAT CALL", Patron.Meow, BoonSlot.Dash,
            "SURF DASH WAKES YOUR CATS: ALL THEIR POWERS ARE READY AGAIN. ONCE EVERY {0}S.", "draw:bell", V(9, 7, 5));

        // ---------------------------------------------------------------- THE HAMMOCK (good stuff with a catch)
        Add("naptime", "NAP TIME", Patron.Hammock, BoonSlot.Passive,
            "AFTER A KILL YOU NAP FOR 1.2S (NO ATTACKING) AND HEAL {0} HP.", "draw:naptime", V(6, 9, 12));
        Add("glassjaw", "GLASS JAW, GOLDEN FIST", Patron.Hammock, BoonSlot.Passive,
            "YOU DEAL DOUBLE DAMAGE. YOU TAKE X{0} DAMAGE.", "sheet:physicalHit:10:3", V(2f, 1.8f, 1.6f));
        Add("weaponsnob", "WEAPON SNOB", Patron.Hammock, BoonSlot.Passive,
            "WEAPONS WEAR OUT TWICE AS FAST. WHEN ONE BREAKS IT EXPLODES FOR {0}.", "draw:brokensword", V(40, 60, 80));

        // ---------------------------------------------------------------- DUOS
        Duo("beachbod", "BEACH BOD", Patron.Pompadour, Patron.Riptide,
            "YOUR WIPEOUT WAVE IS A GIANT PINK TSUNAMI: DOUBLE SIZE, DOUBLE DAMAGE, AND IT CHARMS.", "sheet:waterSonic:9:4",
            "POMPADOUR: SHOW THEM THE BOD, BABY! RIPTIDE: ...AND THE WAVE, BRO.",
            new[] { "hairflip", "decoy", "admire", "mainchar" }, new[] { "wipeout" }).iconTint = new Color(1f, 0.55f, 0.85f);
        Duo("thriller", "THRILLER", Patron.Howl, Patron.Disco,
            "AT NIGHT, YOUR KILLS GET BACK UP AS DANCING ZOMBIES THAT FIGHT FOR YOU FOR 8S.", "draw:hand",
            "HOWL: THE DEAD RISE AT NIGHT. DJ FEVER: AND THEY CAN DANCE!",
            new[] { "feral", "moonrage", "bloodthirst", "packleader", "moon" }, new[] { "chainslap", "nightfever", "mirrorball", "funkyfeet" });
        Duo("redtide", "RED TIDE", Patron.Rot, Patron.Riptide,
            "YOUR WAVES AND SPLASHES ARE TOXIC: THEY POISON FOR 8 DAMAGE PER SECOND.", "sheet:waterSonic:9:6",
            "MAMA ROT: A LITTLE SOMETHING IN THE WATER, DEARIE. RIPTIDE: GNARLY. AND GROSS.",
            new[] { "sporestep", "rottenedge", "overgrowth", "compost" }, new[] { "wipeout", "hangten", "saltwater" }).iconTint = new Color(0.55f, 1f, 0.35f);
        Duo("wolfwhistle", "WOLF WHISTLE", Patron.Pompadour, Patron.Howl,
            "EVERY FEW SECONDS, ENEMIES NEAR YOU MAY FREEZE TO STARE AT YOU (CHARMED 1.5S).", "sheet:charm:9:5",
            "POMPADOUR: THEY CANNOT LOOK AWAY. HOWL: NEITHER CAN THEIR PREDATOR.",
            new[] { "hairflip", "decoy", "admire", "mainchar" }, new[] { "feral", "moonrage", "bloodthirst", "packleader", "moon" });
        Duo("ravemold", "RAVE MOLD", Patron.Disco, Patron.Rot,
            "YOUR POISON CLOUDS PULSE WITH LIGHTNING, ZAPPING EVERYTHING INSIDE.", "sheet:magicalHit:10:2",
            "DJ FEVER: THE SPORES ARE DANCING! MAMA ROT: THEY ALWAYS DID, SWEETIE.",
            new[] { "chainslap", "nightfever", "mirrorball", "funkyfeet" }, new[] { "sporestep" }).iconTint = new Color(0.6f, 1f, 0.4f);
        Duo("wolfpack", "WOLF PACK", Patron.Howl, Patron.Meow,
            "AS A WEREWOLF OR AT NIGHT, YOUR CATS TURN FERAL: DOUBLE DAMAGE, HALF COOLDOWNS.", "sheet:fear:9:5",
            "HOWL: THE KITTENS HAVE TEETH. MADAME MEOW: THEY ALWAYS HAD, MUTT.",
            new[] { "feral", "moonrage", "bloodthirst", "packleader", "moon" }, new[] { "catnip", "ninelives", "furcoat", "felinefury", "catcall" });
        Duo("glamourpuss", "GLAMOUR PUSS", Patron.Pompadour, Patron.Meow,
            "+3% CRIT CHANCE FOR EVERY CAT WITH YOU. CRITS MAKE THEM SPARKLE.", "draw:catface",
            "POMPADOUR: THE CATS NEED A MAKEOVER. MADAME MEOW: THEY ARE ALREADY PERFECT, DARLING.",
            new[] { "hairflip", "decoy", "admire", "mainchar" }, new[] { "catnip", "ninelives", "furcoat", "felinefury", "catcall" }).iconTint = new Color(1f, 0.85f, 0.4f);
        Duo("catscratch", "CAT SCRATCH FEVER", Patron.Disco, Patron.Meow,
            "WHEN YOUR CATS HIT OR USE A POWER, LIGHTNING JUMPS TO 2 ENEMIES FOR 10 DAMAGE.", "sheet:clawSlash:6:1",
            "DJ FEVER: THE KITTIES GOT THE FEVER! MADAME MEOW: MRROW. ELECTRIC.",
            new[] { "chainslap", "nightfever", "mirrorball", "funkyfeet" }, new[] { "catnip", "ninelives", "furcoat", "felinefury", "catcall" }).iconTint = new Color(1f, 0.95f, 0.4f);

        return list;
    }
}
