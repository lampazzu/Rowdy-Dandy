using System.Collections.Generic;
using UnityEngine;

// Animated portraits made by Tools > Rowdy Dandy > Generate Enemy Portraits: Resources/EnemyPortraits/<id>_anim.png
// (frames side by side) + <id>_anim.txt (seconds per frame). Null when there's none.
public static class AnimatedPortraits
{
    public class Clip
    {
        public Sprite[] frames;
        public float[] durations;
        public float length;

        public Sprite FrameAt(float time)
        {
            float t = Mathf.Repeat(time, length);
            for (int i = 0; i < frames.Length; i++)
            {
                if (t < durations[i]) return frames[i];
                t -= durations[i];
            }
            return frames[frames.Length - 1];
        }
    }

    private static readonly Dictionary<string, Clip> cache = new Dictionary<string, Clip>();

    // Portraits built at runtime (the Moonbound Elder's come from its sprite sheet)
    public static void Register(string id, Clip clip) => cache[id] = clip;

    public static Clip Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (cache.TryGetValue(id, out Clip clip)) return clip;
        clip = null;
        Texture2D strip = Resources.Load<Texture2D>("EnemyPortraits/" + id + "_anim");
        TextAsset timing = Resources.Load<TextAsset>("EnemyPortraits/" + id + "_anim");
        if (strip != null && timing != null)
        {
            string[] parts = timing.text.Trim().Split(',');
            int n = parts.Length;
            int w = strip.width / Mathf.Max(1, n);
            if (n >= 2 && w > 0)
            {
                clip = new Clip { frames = new Sprite[n], durations = new float[n] };
                for (int i = 0; i < n; i++)
                {
                    clip.frames[i] = Sprite.Create(strip, new Rect(i * w, 0, w, strip.height), new Vector2(0.5f, 0.5f), 64f);
                    float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d);
                    clip.durations[i] = Mathf.Max(0.02f, d);
                    clip.length += clip.durations[i];
                }
            }
        }
        cache[id] = clip;
        return clip;
    }
}

// Every enemy type the game knows about: display name (kill feed, Rowdy Notes), how to recognise it from object
// names, and its Rowdy Notes page. Edit the texts here freely - they're placeholders written from how the enemies
// are set up (attack clips, isParryTime windows, HP), so fix anything that plays differently.
// Portraits come from Resources/EnemyPortraits/<id>.png (made by Tools > Rowdy Dandy > Generate Enemy Portraits).
public static class EnemyCatalog
{
    public class Entry
    {
        public string id;
        public string name;
        public string[] keys;          // lowercase pieces of object names (spaces, _ and digits removed)
        public int killsToReveal;      // kills needed before the COUNTER section shows
        public string about;
        public string weakness;
        public string counter;
        public string drops = "EXP gems only.";  // weapon drops (from the prefabs' Weapon Drop settings)
        public string dropIds = "exp";           // Rowdy Notes DROPS icons: exp, sword, naginata, cleaver, heart, rubble (+ ":50%" label)
        public bool inNotes = true;    // false = only used for its kill feed name
        public string Quip => Quips.TryGetValue(id, out string q) ? q : null; // ROWDY SAYS line on its notes page
        public Sprite fallbackPortrait;

        private Sprite portrait;
        private bool portraitLoaded;
        public Sprite Portrait
        {
            get
            {
                if (!portraitLoaded)
                {
                    portraitLoaded = true;
                    portrait = Resources.Load<Sprite>("EnemyPortraits/" + id);
                }
                return portrait != null ? portrait : fallbackPortrait;
            }
        }
    }

    // Checked top to bottom, so more specific keys come first ("riderwerewolf" before "werewolf")
    public static readonly List<Entry> All = new List<Entry>
    {
        new Entry
        {
            id = "crabby", name = "Crabby", keys = new[] { "crabby" }, killsToReveal = 5,
            about = "Kinda disgusting i don't know man.. them lil legs creeps me out.",
            weakness = "Paper thin once it's out of the shell - one clean hit. Strike as it walks, not when it's tucked in.",
            counter = "Nothing to counter: it never swings. Wait for the shell to open and tap it.",
        },
        new Entry
        {
            id = "gnollwarrior", name = "Gnoll Warrior", keys = new[] { "gnollwarrior" }, killsToReveal = 8,
            about = "Wish he would stop moving for once",
            dropIds = "exp,cleaver:50%",
            weakness = "It can be kicked. Knock it into deep water and it sinks like a rock.",
            counter = "Hit it while the blade is going up - the moment the swing starts, before it comes down. Counters stagger it and hurt a lot.",
            drops = "CLEAVER half the time, plus EXP gems.",
        },
        new Entry
        {
            id = "gnollarcher", name = "Gnoll Archer", keys = new[] { "gnollarcher" }, killsToReveal = 6,
            about = "I mean they can summon magic arrows, how cool is that?",
            weakness = "Only 10 HP. Close the distance between shots, or jump the arrows. Wig can swat arrows out of the air.",
            counter = "No counter window. Get inside its range and it's done.",
        },
        new Entry
        {
            id = "gnollbomber", name = "Gnoll Bomber", keys = new[] { "gnollbomber" }, killsToReveal = 6,
            about = "Why do this to the precious jellyfish? Nah man.. this just cruel",
            weakness = "Only 10 HP and slow to reload. Keep moving so the bombs land behind you, then rush it.",
            counter = "No counter window. Punish it right after a throw.",
        },
        new Entry
        {
            id = "horserider", name = "Horse Rider", keys = new[] { "horserider", "riderwerewolf", "werewolfrider", "wolfrider" }, killsToReveal = 6,
            about = "Poor horse.",
            dropIds = "exp,naginata:50%",
            weakness = "Turns slowly at the end of its patrol. Jump the charge and hit it from behind.",
            counter = "Right after it flinches from a hit there's a short window: a fast follow-up hit counts as a counter.",
            drops = "NAGINATA half the time.",
        },
        new Entry
        {
            id = "transformwolf", name = "Transform Wolf", keys = new[] { "transformwolf" }, killsToReveal = 8,
            about = "Puberty hits differnt for some people",
            weakness = "Only 25 HP. Trade hits early before it builds up speed.",
            counter = "Tight timing: hit it just as each claw comes down. Both of its swipes have a split-second window.",
        },
        new Entry
        {
            id = "sharkwolf", name = "Sharkwolf", keys = new[] { "sharkwolf", "wolfshark" }, killsToReveal = 6,
            about = "I want that shark man.",
            dropIds = "exp,sword:50%",
            drops = "SWORD half the time - it floats on the water where it died.",
            weakness = "It can't leave the water. Stay back from the shoreline and hit it when it lands from a leap. Kickable.",
            counter = "Hit it at the start of the bite, as the jaws open. The window is generous.",
        },
        new Entry
        {
            id = "statue", name = "Statue", keys = new[] { "wolfstatue" }, killsToReveal = 5,
            about = "Smash. Pass.",
            dropIds = "rubble",
            weakness = "Crumbles in one hit. Cats leave them alone - this one is on you.",
            counter = "Statues don't fight back.",
            drops = "Rubble.",
        },
        new Entry
        {
            id = "watervivarider", name = "Waterviva Rider", keys = new[] { "watervivarider" }, killsToReveal = 6,
            about = "jellyfish again? what's wrong with these people?",
            dropIds = "exp,naginata:50%",
            weakness = "Kickable. Bounce off the blue jellies to get above it and strike from the air.",
            counter = "Each of its three attacks has a window in the middle of the swing. Its flinch can also be countered.",
            drops = "NAGINATA half the time.",
        },
        new Entry
        {
            id = "wereknight", name = "Wereknight", keys = new[] { "wereknight" }, killsToReveal = 6,
            about = "Skips leg day",
            dropIds = "exp,sword:50%",
            weakness = "Fragile under the armor (10 HP). Get past the tip of the weapon.",
            counter = "Hit it halfway through the thrust, when the weapon is fully raised.",
            drops = "SWORD half the time.",
        },
        new Entry
        {
            id = "werefast", name = "Werefast", keys = new[] { "werefast" }, killsToReveal = 10,
            about = "Clingy guy.. I need some space, seriously, it's not you, it's me.",
            weakness = "Only 20 HP. A single crit usually drops it.",
            counter = "Its quick jab has two tiny windows. The long lunge stays counterable for most of the leap - swing into it.",
        },
        new Entry
        {
            id = "bigwolf", name = "Big Wolf", keys = new[] { "bigwerewolf", "bigwolf", "werewolf", "wwolf", "wolfprefab" }, killsToReveal = 8,
            about = "Good boy",
            weakness = "Slow to recover after a big leap. Punish the landing.",
            counter = "Strike as it rears back, right before the claws come down. Its double swipe has a second window on the follow-up.",
        },
        new Entry
        {
            id = "megacreature", name = "Mega Creature", keys = new[] { "megacreature", "dashcreature" }, killsToReveal = 5,
            about = "Kinda scary but I can manage it",
            weakness = "Its attack takes ages to start. Get behind it while it charges up.",
            counter = "The easiest counter in the jungle: hit it any time during the first second of its attack wind-up.",
        },
        new Entry
        {
            id = "pelican", name = "Pelican", keys = new[] { "pelican" }, killsToReveal = 3,
            about = "Love these guys. Good with tomato sauce.",
            dropIds = "heart",
            weakness = "One hit. Some of them carry a heart.",
            counter = "It's a bird. It doesn't fight.",
            drops = "Sometimes a heart (heals 10 HP).",
        },
        new Entry
        {
            id = "mantaray", name = "Manta Ray", keys = new[] { "mantaray" }, killsToReveal = 3,
            about = "I don't know... They seem cute but if you look closely it's kinda disgusting too... You know, they smell weird like fish, also they're gooey and slimey and, you know what? They don't even seem cute. Doesn't go with tomato sauce.",
            dropIds = "",
            weakness = "One hit. Kickable.",
            counter = "Nothing to counter.",
        },
        new Entry
        {
            id = "voltrat", name = "Red Jelly", keys = new[] { "voltrat", "redjelly" }, killsToReveal = 5,
            about = "You know what they say about jellyfish wounds.. Let's be careful... Don't anybody doing.. you know...",
            weakness = "Paper thin. One or two hits each.",
            counter = "Watch for the fast pulsing and the white flash, then jump or dash out of the line.",
            drops = "A couple of EXP gems.",
        },
        new Entry
        {
            id = "pelich", name = "Pelich Anus", keys = new[] { "pelichanus", "pelich" }, killsToReveal = 1,
            about = "I don't even know his real name, I know he is a pelichan and he has a big fat ass but so...",
            dropIds = "exp:X40",
            weakness = "It barely moves. Stay out of its stomp range and hit it while it's turning around.",
            counter = "No known counter. Patience and good spacing.",
            drops = "A pile of EXP gems.",
        },
        new Entry
        {
            id = "moonboundelder", name = "Moonbound Elder", keys = new[] { "moonboundelder" }, killsToReveal = 1,
            about = "Nah, I'd win",
            dropIds = "exp",
            weakness = "When he crouches and glows, the nova is coming - jump or run out of the ring. At half health he howls, turns red, calls two Werefasts and gets faster.",
            counter = "Same windows as a Big Wolf: strike as he rears back before the claws come down. His size makes the swipes reach further, so stay close.",
            drops = "A big pile of EXP gems.",
        },
        new Entry { id = "oldman", name = "Old Man", keys = new[] { "oldman" }, inNotes = false },
    };

    private static readonly Dictionary<string, string> Quips = new Dictionary<string, string>
    {
        ["crabby"] = "Hides in its shell every time I show up. Relatable. I do that with my landlord.",
        ["gnollwarrior"] = "Big sword, bigger ego. Compensating for something? Not me. The hair is natural, baby.",
        ["gnollarcher"] = "Shoots from far away because up close it would have to admit how dandy I look.",
        ["gnollbomber"] = "Explosive personality. Literally. I have dated worse.",
        ["horserider"] = "A wolf riding a horse. The horse is clearly the brains of this operation.",
        ["transformwolf"] = "Puberty hits different when you are cursed.",
        ["sharkwolf"] = "Shark plus wolf. Whoever designed this guy owes me an apology and a sandwich.",
        ["statue"] = "Art critic Rowdy rates it: one star. Very smashable.",
        ["watervivarider"] = "Rides a jellyfish like a surfboard. Copycat. Counter it and watch the jelly go boom.",
        ["wereknight"] = "Full armor on 10 HP. That is a helmet on a jelly bean.",
        ["werefast"] = "Hyperactive little furball. Somebody switch this guy to decaf.",
        ["bigwolf"] = "Tall, dark and hairy. Fine, he has the height. I have the hair.",
        ["megacreature"] = "Takes a full second to start its attack. Same, buddy. Same. Every morning.",
        ["pelican"] = "Some carry hearts. I carry charm. We are not the same.",
        ["mantaray"] = "A flying pancake. I would register it for cash if it was not so peaceful.",
        ["pelich"] = "Lord of the far shore? I am lord of the whole beach. Also, that name is a crime.",
        ["moonboundelder"] = "Grandpa had ONE bad day. Lesson learned: never poke old men who live in tents.",
    };

    private static readonly Dictionary<int, Entry> byObject = new Dictionary<int, Entry>();

    public static int NotesCount
    {
        get { int n = 0; foreach (Entry e in All) if (e.inNotes) n++; return n; }
    }

    public static List<Entry> NotesEntries()
    {
        var list = new List<Entry>();
        foreach (Entry e in All) if (e.inNotes) list.Add(e);
        return list;
    }

    // Which entry this enemy is (its own name first, then up to 3 parents). Cached per object.
    public static Entry Identify(EnemyHealth enemy)
    {
        if (enemy == null) return null;
        int key = enemy.GetInstanceID();
        if (byObject.TryGetValue(key, out Entry cached)) return cached;

        Entry found = null;
        Transform t = enemy.transform;
        for (int depth = 0; depth < 4 && t != null && found == null; depth++, t = t.parent)
            found = Match(t.name);

        byObject[key] = found;
        return found;
    }

    public static Entry Match(string objectName)
    {
        string n = Normalize(objectName);
        if (n.Length == 0) return null;
        foreach (Entry entry in All)
            foreach (string k in entry.keys)
                if (n.Contains(k)) return entry;
        return null;
    }

    public static Entry Get(string id)
    {
        foreach (Entry e in All) if (e.id == id) return e;
        return null;
    }

    private static string Normalize(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s.ToLowerInvariant())
            if (c >= 'a' && c <= 'z') sb.Append(c);
        return sb.ToString();
    }

    // "RDR_SomeThing_0 (3)" -> "Some Thing", for anything not in the catalog
    public static string PrettyName(string objectName)
    {
        string n = objectName.Replace("(Clone)", "").Trim();
        int paren = n.IndexOf(" (");
        if (paren > 0) n = n.Substring(0, paren);
        foreach (string prefix in new[] { "Enemy_", "Neutral_", "RDR_", "RDRl_", "RD_" })
            if (n.StartsWith(prefix)) n = n.Substring(prefix.Length);
        while (n.Length > 0 && (char.IsDigit(n[n.Length - 1]) || n[n.Length - 1] == '_')) n = n.Substring(0, n.Length - 1);

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < n.Length; i++)
        {
            char c = n[i];
            if (c == '_') { sb.Append(' '); continue; }
            if (i > 0 && char.IsUpper(c) && char.IsLower(n[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        string result = sb.ToString().Trim();
        return result.Length > 0 ? result : objectName;
    }
}
