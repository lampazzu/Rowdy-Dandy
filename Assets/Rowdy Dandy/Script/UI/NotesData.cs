using System;
using UnityEngine;

// Texts for the Weapons / Cats / Game Mechanics / Interactables tabs of Rowdy Notes (the enemy pages live in EnemyCatalog).
// Written from how things are set up in the project (attack speeds in WeaponManager, drop prefabs, effectors...);
// edit freely. Every page ends with a ROWDY SAYS line in his voice (a dandy guy... on a beach... baby).
// Icons: a sprite getter (null = show the glyph instead). Glyphs and texts use PixelFont characters
// (A-Z 0-9 . , : ! ? % / - + < > ( ) ') - no quotes, semicolons or ampersands.
public static class NotesData
{
    public class Topic
    {
        public string id, name, glyph;
        public Func<Sprite> icon;
        public string animatedPortrait;   // Resources/EnemyPortraits/<id>_anim (cats)
        public int[] ratings;             // weapons: 1-10 per RatingNames, drawn as jellyfish
        public (string header, string body)[] sections;
    }

    public const string Quip = "ROWDY SAYS";
    public static readonly string[] RatingNames = { "SPEED", "POWER", "QUIRKINESS", "DIFFICULTY", "VERTICALITY" };

    // ---------------------------------------------------------------- weapons (index = WeaponManager slot)
    public static readonly Topic[] Weapons =
    {
        new Topic
        {
            id = "rod", name = "Rod", glyph = "R", icon = () => WeaponIcon(0),
            ratings = new[] { 5, 3, 3, 1, 4 },
            sections = new[]
            {
                ("WHAT IT IS", "Rowdy's fishing rod. It never breaks and it's what he falls back to when anything else snaps."),
                ("ATTACKS", "Standing, ducking and jumping swings all at normal speed. Balanced, nothing fancy."),
                ("BEST FOR", "Saving durability: clear plants, statues and weak fodder with the rod and keep the good weapons for the big stuff."),
                ("WHERE", "Always in your hands."),
                (Quip, "Unbreakable. Just like my confidence. And my hair."),
            },
        },
        new Topic
        {
            id = "sword", name = "Sword", glyph = "S", icon = () => WeaponIcon(1),
            ratings = new[] { 9, 5, 3, 4, 5 },
            sections = new[]
            {
                ("WHAT IT IS", "A quick blade. 40 uses."),
                ("ATTACKS", "Standing slash 1.8x faster than the rod, ducking and jumping slashes 1.5x faster."),
                ("BEST FOR", "Counters. It's the fastest standing swing, so it's the easiest to time into an enemy's wind-up window."),
                ("WHERE", "Wereknights and Sharkwolves drop it (half the time). Walk over another one while holding a sword to repair yours."),
                (Quip, "Classic, pointy, and it matches my pompadour. A dandy accessory, baby."),
            },
        },
        new Topic
        {
            id = "naginata", name = "Naginata", glyph = "N", icon = () => WeaponIcon(2),
            ratings = new[] { 6, 6, 9, 7, 10 },
            sections = new[]
            {
                ("WHAT IT IS", "A long polearm. 30 uses - the most fragile."),
                ("ATTACKS", "Standing and ducking swings at normal speed. The jumping attack spins 3x faster than any other weapon."),
                ("BEST FOR", "Fighting from the air: jump over a crowd and shred it on the way down."),
                ("WHERE", "Horse Riders and Waterviva Riders drop it (half the time)."),
                (Quip, "A stick with a knife taped on top, but make it fashion. Spin to win."),
            },
        },
        new Topic
        {
            id = "cleaver", name = "Cleaver", glyph = "C", icon = () => WeaponIcon(3),
            ratings = new[] { 4, 9, 7, 6, 3 },
            sections = new[]
            {
                ("WHAT IT IS", "A heavy chopper. 50 uses - the toughest weapon."),
                ("ATTACKS", "Standing swing at normal speed, jumping chop 1.5x faster, ducking chop 1.8x faster."),
                ("BEST FOR", "Low enemies and ducking under attacks: the fast crouch chop hits Crabbies, Werefasts and legs while you stay small."),
                ("WHERE", "Gnoll Warriors drop it (half the time)."),
                (Quip, "For when one problem needs to become two smaller problems."),
            },
        },
    };

    // ---------------------------------------------------------------- cats (index = PetFollower.CatType)
    public static readonly Topic[] Cats =
    {
        new Topic
        {
            id = "Wig", name = "Wig", glyph = "W", icon = () => CatIcon("Interactables/Wig"), animatedPortrait = "cat_Wig",
            sections = new[]
            {
                ("WHAT HE DOES", "Swats everything around him: hurts enemies and knocks arrows out of the air. Clears fodder, staggers, blocks shots."),
                ("IGNORES", "Plants and statues. He has standards."),
                ("COOLDOWN", "His bar under the weapon slot fills back up after every swat."),
                (Quip, "Small, fluffy, violent. My kind of guy."),
            },
        },
        new Topic
        {
            id = "Samurai", name = "Nick", glyph = "N", icon = () => CatIcon("Interactables/SamuraiCat"), animatedPortrait = "cat_Samurai",
            sections = new[]
            {
                ("WHAT HE DOES", "A samurai cat. When an enemy is nearly dead (10% HP or 10 HP left) he dashes in and executes it, then chains to the next one - up to five."),
                ("HOW TO USE HIM", "Soften a crowd up and let him finish it. His cuts glow blue and freeze time for a blink so you can see them."),
                ("COOLDOWN", "Starts after the whole chain, so long chains don't waste it."),
                (Quip, "He bows before every kill. Show-off. I respect it."),
            },
        },
        new Topic
        {
            id = "Paprika", name = "Paprika", glyph = "P", icon = () => CatIcon("Interactables/Paprika"),
            sections = new[]
            {
                ("WHAT HE DOES", "When enemies are close he spices up Rowdy's weapon: for a few seconds every hit poisons. Poison eats HP over time (green numbers)."),
                ("HOW TO USE HIM", "Spread hits around the crowd while it's on - every enemy you tag keeps bleeding green."),
                ("COOLDOWN", "Starts when the poison blade runs out. It blinks right before it ends."),
                (Quip, "Hot, dangerous and a little bit toxic. Like me on a Friday."),
            },
        },
        new Topic
        {
            id = "Mushidon", name = "Mushidon", glyph = "M", icon = () => CatIcon("Interactables/Mushidon"),
            sections = new[]
            {
                ("WHAT HE DOES", "Jumps sky high and slams the ground. Every enemy standing on the ground nearby is stunned for a second."),
                ("WATCH OUT", "Flyers and anything mid-jump don't care. He only stomps when Rowdy is on the ground too."),
                ("COOLDOWN", "Short. He loves the sound."),
                (Quip, "Big feet, bigger ego. We get along great."),
            },
        },
        new Topic
        {
            id = "Peak", name = "The Peak", glyph = "K", icon = () => CatIcon("Interactables/ThePeak"),
            sections = new[]
            {
                ("WHAT HE DOES", "Gives Rowdy armor that blocks the next 3 hits. The shield over his head cracks a little with every block."),
                ("COOLDOWN", "Only starts once the armor breaks. Then he makes a new one."),
                (Quip, "Finally, someone who protects this face professionally."),
            },
        },
        new Topic
        {
            id = "Lallo", name = "Lallo", glyph = "L", icon = () => CatIcon("Interactables/Lallo"),
            sections = new[]
            {
                ("WHAT HE DOES", "While his bar is full, the next enemy Rowdy kills bursts in decay. Anything caught in it gets infected and bursts too - chain reaction."),
                ("HOW TO USE HIM", "Save the kill for the middle of a crowd. Purple shimmer on Rowdy = decay is ready."),
                ("COOLDOWN", "Starts after the burst."),
                (Quip, "He's not evil. He's just... thorough."),
            },
        },
    };

    // ---------------------------------------------------------------- game mechanics
    public static readonly Topic[] Mechanics =
    {
        new Topic
        {
            id = "counter", name = "Counters", glyph = "!",
            sections = new[]
            {
                ("HOW", "Hit an enemy while it's starting an attack - the wind-up, before the blow lands - and you get a COUNTER! It flashes, the pad rumbles and it hurts more."),
                ("WHERE TO LEARN THEM", "Every enemy's window is different. Kill enough of one kind and its page reveals exactly when to swing."),
                ("KILL FEED", "The arrow in the kill log tells how it died: white = normal, red = critical, cyan = counter, gold = Nick."),
                (Quip, "Timing is everything. Ask any comedian. Or me, a comedian."),
            },
        },
        new Topic
        {
            id = "rank", name = "Style Rank", glyph = "S",
            sections = new[]
            {
                ("HOW", "Every hit fills the style meter. Fill it to climb from D up to SSS. It drains when you stop fighting."),
                ("WHAT COUNTS", "Variety (switch weapons and attacks - stand, duck, jump), counters, crits, air hits and kills. Spamming the same swing gives less and less."),
                ("DON'T GET HIT", "Taking damage drops you two ranks and resets the no-hit streak, which multiplies everything you earn."),
                ("SETTINGS", "Pause > Settings: turn it off, change its size, or move it to another spot on the screen."),
                (Quip, "Style is not a number. Except here. Here it's a letter."),
            },
        },
        new Topic
        {
            id = "surf", name = "Surfing", glyph = "S",
            sections = new[]
            {
                ("HOW", "{SURF}: Rowdy hops on his board and dashes."),
                ("ON LAND TOO", "You can surf on solid ground, not just water. The ground slows the board down if you stay on it."),
                ("ENDLESS SURF", "Jump while surfing and the surf carries on when you land. Keep hopping and you can surf as long as you like."),
                (Quip, "Surfing on sand is technically illegal on most beaches. Good thing nobody here can catch me."),
            },
        },
        new Topic
        {
            id = "durability", name = "Weapon Durability", glyph = "D", icon = () => WeaponIcon(1),
            sections = new[]
            {
                ("HOW IT WORKS", "Every swing you start costs one use, whether it hits or not. The DUR bar under the weapon name shows what's left."),
                ("BREAKING", "At zero the weapon shatters and you're back to the Rod. A broken weapon is gone until another one drops."),
                ("REPAIRING", "Walk over a dropped copy of the weapon you're holding and it's picked up by itself: full durability again."),
                ("SWITCHING", "{SWITCH} swaps between the weapons you're carrying. The Rod never breaks."),
                (Quip, "Nothing lasts forever. Except the rod. And my good looks."),
            },
        },
        new Topic
        {
            id = "levels", name = "Levels And Crits", glyph = "+", icon = () => PrefabIcon("Systems/EXPgem"),
            sections = new[]
            {
                ("EXP", "Enemies drop EXP gems that fly to you. Fill the XP bar to level up."),
                ("LEVEL UP", "Full heal, more damage, more critical chance, stronger crits - and a golden shockwave that deals 10 damage to everything around you."),
                ("CRITICALS", "Random hits land as CRITICAL! for extra damage. The chance grows with your level."),
                (Quip, "Leveling up is like a haircut. You feel it immediately."),
            },
        },
        new Topic
        {
            id = "water", name = "Water And Drowning", glyph = "W", icon = () => KillFeed.WaterIcon,
            sections = new[]
            {
                ("ENEMIES", "Most land enemies can't swim: knock them into deep water and it finishes them. The kill still counts as yours."),
                ("KICKABLE", "Some enemies are light enough to be knocked around easily - their notes say so."),
                ("SHARKWOLVES", "They live in the water and can't leave it. Stay back from the shore line."),
                (Quip, "Rule one of the beach: if it can't swim, it can't win."),
            },
        },
        new Topic
        {
            id = "crowds", name = "Crowds", glyph = ">",
            sections = new[]
            {
                ("TAKING TURNS", "Big Wolves, Transform Wolves and Werefasts stop at striking distance and only a few press you at once."),
                ("SETTING", "Pause > Settings > Crowd Limit sets how many (default 4)."),
                ("DANGER ZONES", "Some areas spawn much tougher crowds: the Gloomy Forest up top and the shore before Pelich. Pelich's arena itself is his alone."),
                (Quip, "Form a line, fellas. One autograph at a time."),
            },
        },
        new Topic
        {
            id = "checkpoints", name = "Checkpoints", glyph = "S", icon = () => PrefabIcon("Interactables/Spawner"),
            sections = new[]
            {
                ("HOW", "Touch a spawner to set it as your respawn point."),
                ("RESTING", "While you stand on a spawner no new enemy waves arrive. Catch your breath. The start of the beach is quiet too."),
                ("DYING", "You come back at the last spawner you touched. Level, weapons and notes are kept - but ALL your cats get lost, except one for every flying rat you registered."),
                (Quip, "Nap spot. Officially. I'm putting it on my resume."),
            },
        },
    };

    // ---------------------------------------------------------------- interactables
    public static readonly Topic[] Interactables =
    {
        new Topic
        {
            id = "blueplant", name = "Blue Plant", glyph = "B", icon = () => PrefabIcon("Interactables/BluePlant "),
            sections = new[]
            {
                ("WHAT IT DOES", "Launches Rowdy straight up. Step into it to reach high ledges."),
                ("CAREFUL", "It can be cut down - don't slash the one you need to climb with."),
                (Quip, "Nature's elevator. No music, no small talk. Perfect."),
            },
        },
        new Topic
        {
            id = "orangeplant", name = "Orange Plant", glyph = "O", icon = () => PrefabIcon("Interactables/OrangePlant"),
            sections = new[]
            {
                ("WHAT IT DOES", "Nothing. Absolutely nothing. It's just really satisfying to cut."),
                (Quip, "Some things exist only to be cut by a handsome man. This is one of them."),
            },
        },
        new Topic
        {
            id = "purpleplant", name = "Purple Plant", glyph = "P", icon = () => PrefabIcon("Interactables/PurplePlant"),
            sections = new[]
            {
                ("WHAT IT DOES", "Also nothing. Cut it anyway. Swish."),
                (Quip, "Purple. Pointless. Pretty. Like my ex's band."),
            },
        },
        new Topic
        {
            id = "jelly", name = "Jellyfish", glyph = "J", icon = () => PrefabIcon("Interactables/Jelly"),
            sections = new[]
            {
                ("WHAT IT DOES", "Land on a jelly and it bounces you up, glowing for a moment. Big jellies throw you higher."),
                ("TIP", "Chain bounces to cross water, or bounce over a Waterviva Rider and strike from above."),
                (Quip, "Ten out of ten jellyfish. That's the highest rating there is. Ask the weapon notes."),
            },
        },
        new Topic
        {
            id = "spawner", name = "Spawner", glyph = "S", icon = () => PrefabIcon("Interactables/Spawner"),
            sections = new[]
            {
                ("WHAT IT DOES", "A checkpoint. Touch it and it becomes your respawn point. Standing on it pauses the enemy waves."),
                (Quip, "Respawning: the only thing I'm better at than surfing."),
            },
        },
        new Topic
        {
            id = "altars", name = "Shaman Altars", glyph = "A",
            sections = new[]
            {
                ("WHAT THEY DO", "Old shaman altars and pillars. Break both altars and the pillars light up - something wakes up when they do."),
                ("TIP", "Look for them in pairs; the camera shows you what you opened."),
                (Quip, "Break ancient religious artifacts? On a Tuesday? Sure, why not."),
            },
        },
        new Topic
        {
            id = "statues", name = "Wolf Statues", glyph = "W",
            sections = new[]
            {
                ("WHAT THEY DO", "Breakable wolf idols. One hit smashes them. Cats ignore them, so they're up to you."),
                (Quip, "I'm not a vandal. I'm an art critic with a fishing rod."),
            },
        },
        new Topic
        {
            id = "weapondrop", name = "Weapon Drops", glyph = "!", icon = () => WeaponIcon(3),
            sections = new[]
            {
                ("WHAT THEY ARE", "A dropped weapon floats in a beam of light. Dropped in mid-air, it falls to the ground first."),
                ("PICKING UP", "Walk up and press {INTERACT}. It comes with full durability and is equipped right away."),
                ("REPAIR", "Same weapon as the one in your hands? Just walk over it - REPAIRED."),
                (Quip, "Free stuff in a beam of light. Either a gift from the gods or a trap. Worth it."),
            },
        },
        new Topic
        {
            id = "gems", name = "EXP Gems", glyph = "+", icon = () => PrefabIcon("Systems/EXPgem"),
            sections = new[]
            {
                ("WHAT THEY DO", "Burst out of defeated enemies and swim through the air to Rowdy when he's near. Each one is EXP."),
                (Quip, "Shiny things that make me stronger. Like compliments."),
            },
        },
        new Topic
        {
            id = "heart", name = "Hearts", glyph = "+",
            sections = new[]
            {
                ("WHAT THEY DO", "Some pelicans carry a heart. Knock it loose and grab it to heal 10 HP."),
                ("NOTE", "You can't pick one up at full health - it waits in its pink beam for later."),
                (Quip, "Taking a heart from a pelican. Romantic, if you don't think about it."),
            },
        },
        new Topic
        {
            id = "flyingrat", name = "Flying Rat", glyph = "R", icon = () => SheetIcon(ItemArt.Get != null ? ItemArt.Get.flyingRat : null, 10, 2, 0),
            sections = new[]
            {
                ("WHAT IT IS", "A very rare rat with wings. Now and then one bursts out of a defeated enemy, flutters around and escapes after a while."),
                ("REGISTER IT", "Walk up to it and press {INTERACT} to grab it. Every registered rat is bait: when you die, one cat per rat stays with you and the rest get lost. 10 cats and 3 rats = you come back with 3 cats."),
                ("LUCK", "Ore gems make it show up more often."),
                (Quip, "Cats love rats. I love cats. The rats are fine with it. Probably."),
            },
        },
        new Topic
        {
            id = "cattreat", name = "Cat Treat", glyph = "F", icon = () => SheetIcon(ItemArt.Get != null ? ItemArt.Get.fish : null, ItemArt.Get != null ? Mathf.Max(1, ItemArt.Get.fishFrames) : 1, 1, 0),
            sections = new[]
            {
                ("WHAT IT IS", "A tiny fish, a very rare drop. It flops around on the ground until you grab it."),
                ("WHAT IT DOES", "Feeds every cat you have: their cooldowns refill at once and stay halved for a while. No cats? It's a snack: +5 HP."),
                (Quip, "Raw fish off the floor. Gourmet, if you're a cat."),
            },
        },
        new Topic
        {
            id = "ores", name = "Ore Rocks", glyph = "O", icon = () => SheetIcon(ItemArt.Get != null ? ItemArt.Get.oreBreak : null, 10, 1, 0),
            sections = new[]
            {
                ("WHAT THEY ARE", "Dark rocks with gems inside, scattered around the level. Three hits break one into a fountain of gems."),
                ("GEMS", "Emerald, sapphire, ruby and the rare crystal. Each one you pick up adds a little drop luck (+0.5% to +1.5%)."),
                ("DROP LUCK", "Raises the chance of weapon drops, flying rats and cat treats. A broken rock stays broken, so the luck out there is limited."),
                (Quip, "Smashing rocks for jewelry. My grandma would be proud. Or worried."),
            },
        },
        new Topic
        {
            id = "catpickup", name = "Lost Cats", glyph = "C", icon = () => CatIcon("Interactables/SamuraiCat"),
            sections = new[]
            {
                ("WHAT THEY ARE", "Cats hiding around the map, glowing so you can spot them. Touch one and it joins the party. See the Cats tab."),
                ("MAGICAL CAT CAPACITY", "You can have as many cats as your level (level 1 = 1 cat, up to 10). Party full? Press {INTERACT} on a new cat to swap: the cat that's been with you longest waits there."),
                ("GETTING LOST", "Die and every cat gets lost again somewhere you've been - except one per registered flying rat."),
                (Quip, "If you love something, let it go. Then go find it. It's glowing. Easy."),
            },
        },
        new Topic
        {
            id = "oldman", name = "The Old Man", glyph = "?",
            sections = new[]
            {
                ("WHO", "An old man living in a tent by the mountain. He looks harmless."),
                ("CAREFUL", "Hurt him and the curse wakes up: the Moonbound Elder, a huge shadow werewolf. You were warned."),
                (Quip, "Note to self: do not poke old men who live alone in tents. Ever."),
            },
        },
    };

    // ---------------------------------------------------------------- icon helpers
    private static Sprite WeaponIcon(int index)
    {
        WeaponManager weapons = WeaponManager.Instance;
        return weapons != null ? weapons.GetProfileByIndex(index) : null;
    }

    private static Sprite SheetIcon(Texture2D sheet, int columns, int rows, int frame)
    {
        Sprite[] frames = ItemArt.Frames(sheet, columns, rows, new Vector2(0.5f, 0.5f), 64f);
        return frames != null && frame < frames.Length ? frames[frame] : null;
    }

    private static Sprite PrefabIcon(string resourcePath)
    {
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null) return null;
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>(true);
        return sr != null ? sr.sprite : null;
    }

    private static Sprite CatIcon(string resourcePath)
    {
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        PetFollower cat = prefab != null ? prefab.GetComponent<PetFollower>() : null;
        return cat != null && cat.Portrait != null ? cat.Portrait : PrefabIcon(resourcePath);
    }
}
