using System;
using UnityEngine;

// Texts for the Weapons / Game Mechanics / Interactables tabs of Rowdy Notes (the enemy pages live in EnemyCatalog).
// Written from how things are set up in the project (attack speeds in WeaponManager, drop prefabs, effectors...);
// edit freely. Icons: a sprite getter (null = show the glyph instead). Glyphs use PixelFont characters
// (A-Z 0-9 . , : ! ? % / - + < > ( ) ').
public static class NotesData
{
    public class Topic
    {
        public string id, name, glyph;
        public Func<Sprite> icon;
        public (string header, string body)[] sections;
    }

    // ---------------------------------------------------------------- weapons (index = WeaponManager slot)
    public static readonly Topic[] Weapons =
    {
        new Topic
        {
            id = "rod", name = "Rod", glyph = "R", icon = () => WeaponIcon(0),
            sections = new[]
            {
                ("WHAT IT IS", "Rowdy's fishing rod. It never breaks and it's what he falls back to when anything else snaps."),
                ("ATTACKS", "Standing, ducking and jumping swings all at normal speed. Balanced, nothing fancy."),
                ("BEST FOR", "Saving durability: clear plants, statues and weak fodder with the rod and keep the good weapons for the big stuff."),
                ("NUANCES", "Every breakable weapon drops you back to the rod when it breaks, so you're never unarmed."),
                ("WHERE", "Always in your hands."),
            },
        },
        new Topic
        {
            id = "sword", name = "Sword", glyph = "S", icon = () => WeaponIcon(1),
            sections = new[]
            {
                ("WHAT IT IS", "A quick blade. 40 uses."),
                ("ATTACKS", "Standing slash 1.8x faster than the rod, ducking and jumping slashes 1.5x faster."),
                ("BEST FOR", "Counters. It's the fastest standing swing, so it's the easiest to time into an enemy's wind-up window."),
                ("NUANCES", "Fast swings burn uses fast too - every swing costs one, hit or miss. Don't mash."),
                ("WHERE", "Wereknights drop it (half the time)."),
            },
        },
        new Topic
        {
            id = "naginata", name = "Naginata", glyph = "N", icon = () => WeaponIcon(2),
            sections = new[]
            {
                ("WHAT IT IS", "A long polearm. 30 uses - the most fragile."),
                ("ATTACKS", "Standing and ducking swings at normal speed. The jumping attack spins 3x faster than any other weapon."),
                ("BEST FOR", "Fighting from the air: jump over a crowd and shred it on the way down. Great against things that stay on the ground."),
                ("NUANCES", "Only 30 swings, and the air spin is so fast it eats them quickly. Save it for crowds."),
                ("WHERE", "Horse Riders and Waterviva Riders drop it (half the time)."),
            },
        },
        new Topic
        {
            id = "cleaver", name = "Cleaver", glyph = "C", icon = () => WeaponIcon(3),
            sections = new[]
            {
                ("WHAT IT IS", "A heavy chopper. 50 uses - the toughest weapon."),
                ("ATTACKS", "Standing swing at normal speed, jumping chop 1.5x faster, ducking chop 1.8x faster."),
                ("BEST FOR", "Low enemies and ducking under attacks: the fast crouch chop hits Crabbies, Werefasts and legs while you stay small."),
                ("NUANCES", "Most uses of any weapon, so it's the one to keep for long fights and bosses."),
                ("WHERE", "Gnoll Warriors drop it (half the time)."),
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
                ("HOW", "Hit an enemy while it's starting an attack - the wind-up, before the blow lands - and you get a COUNTER! It flashes, the pad rumbles and it hurts more than a normal hit."),
                ("WHERE TO LEARN THEM", "Every enemy's window is different. Kill enough of one kind and its page in the enemy notes reveals exactly when to swing."),
                ("TIP", "Fast weapons (the Sword) make counters much easier to time."),
            },
        },
        new Topic
        {
            id = "surf", name = "Surfing", glyph = "S",
            sections = new[]
            {
                ("HOW", "Shift (or L) / RB: Rowdy hops on his board and dashes."),
                ("ON LAND TOO", "You can surf on solid ground, not just water. The ground slows the board down if you stay on it."),
                ("ENDLESS SURF", "Jump while surfing and the surf carries on when you land. Keep hopping - only staying on the ground ends it - and you can surf as long as you like."),
                ("TIP", "Hop-surfing is the fastest way to cross the map and to get away from a crowd."),
            },
        },
        new Topic
        {
            id = "durability", name = "Weapon Durability", glyph = "D", icon = () => WeaponIcon(1),
            sections = new[]
            {
                ("HOW IT WORKS", "Every swing you start costs one use, whether it hits or not. The DUR bar under the weapon name shows what's left."),
                ("BREAKING", "At zero the weapon shatters and you're back to the Rod. A broken weapon is gone until another one drops."),
                ("SWITCHING", "L1 / Q swaps between the weapons you're carrying (the badge on the weapon slot shows when you can). The Rod never breaks."),
                ("PICKING UP", "Some enemies drop weapons in a beam of light. Walk to it and press Y / E."),
            },
        },
        new Topic
        {
            id = "cats", name = "Cats", glyph = "C", icon = () => CatIcon("Interactables/Wig"),
            sections = new[]
            {
                ("FINDING THEM", "Cats hide around the level, in a new spot every time you load. Touch one and it follows you."),
                ("WIG", "Swats everything around him: hurts enemies and knocks arrows out of the air. He leaves plants and statues alone."),
                ("NICK", "A samurai cat. When an enemy is nearly dead (10% HP or 10 HP left) he dashes in and executes it, chaining to the next one."),
                ("LOSING THEM", "Each cat has a cooldown bar under the weapon slot. If Rowdy dies, the last cat he found runs off."),
            },
        },
        new Topic
        {
            id = "levels", name = "Levels And Crits", glyph = "+", icon = () => PrefabIcon("Systems/EXPgem"),
            sections = new[]
            {
                ("EXP", "Enemies drop EXP gems that fly to you. Fill the XP bar to level up."),
                ("LEVEL UP", "Full heal, more damage, more critical chance and stronger crits."),
                ("CRITICALS", "Random hits land as CRITICAL! for extra damage. The chance grows with your level."),
            },
        },
        new Topic
        {
            id = "water", name = "Water And Drowning", glyph = "W", icon = () => KillFeed.WaterIcon,
            sections = new[]
            {
                ("ENEMIES", "Most land enemies can't swim: knock them into deep water and it finishes them. The kill still counts as yours."),
                ("KICKABLE", "Some enemies are light enough to be knocked around easily - their notes say so. Perfect for pushing into the sea."),
                ("SHARKWOLVES", "They live in the water and can't leave it. Stay back from the shore line."),
            },
        },
        new Topic
        {
            id = "crowds", name = "Crowds", glyph = ">",
            sections = new[]
            {
                ("TAKING TURNS", "Big Wolves, Transform Wolves and Werefasts stop at striking distance instead of running into you, and only a few press you at once - the rest wait their turn."),
                ("SETTING", "Pause > Settings > Crowd Limit sets how many (default 4)."),
                ("DANGER ZONES", "Some areas spawn much tougher crowds: the Gloomy Forest up top and the shore near Pelich are the worst."),
            },
        },
        new Topic
        {
            id = "checkpoints", name = "Checkpoints", glyph = "S", icon = () => PrefabIcon("Interactables/Spawner"),
            sections = new[]
            {
                ("HOW", "Touch a spawner to set it as your respawn point."),
                ("DYING", "You come back at the last spawner you touched. Your level, weapons and notes are kept; the most recent cat runs off."),
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
            },
        },
        new Topic
        {
            id = "orangeplant", name = "Orange Plant", glyph = "O", icon = () => PrefabIcon("Interactables/OrangePlant"),
            sections = new[]
            {
                ("WHAT IT DOES", "Hit it and it bursts into a cloud that pulls everything nearby towards it."),
                ("TIP", "Burst it next to a group of enemies to bunch them up for one big swing."),
            },
        },
        new Topic
        {
            id = "purpleplant", name = "Purple Plant", glyph = "P", icon = () => PrefabIcon("Interactables/PurplePlant"),
            sections = new[]
            {
                ("WHAT IT DOES", "Like the orange plant: a hit bursts it into a pulling cloud."),
                ("TIP", "Good for dragging enemies off ledges or away from you."),
            },
        },
        new Topic
        {
            id = "jelly", name = "Jellyfish", glyph = "J", icon = () => PrefabIcon("Interactables/Jelly"),
            sections = new[]
            {
                ("WHAT IT DOES", "Land on a jelly and it bounces you up, glowing for a moment. Big jellies throw you higher."),
                ("TIP", "Chain bounces to cross water, or bounce over a Waterviva Rider and strike from above."),
            },
        },
        new Topic
        {
            id = "spawner", name = "Spawner", glyph = "S", icon = () => PrefabIcon("Interactables/Spawner"),
            sections = new[]
            {
                ("WHAT IT DOES", "A checkpoint. Touch it and it becomes your respawn point (Respawn point activated!)."),
            },
        },
        new Topic
        {
            id = "altars", name = "Shaman Altars", glyph = "A",
            sections = new[]
            {
                ("WHAT THEY DO", "Old shaman altars and pillars. Break both altars and the pillars light up - something wakes up when they do."),
                ("TIP", "Look for them in pairs; the camera shows you what you opened."),
            },
        },
        new Topic
        {
            id = "statues", name = "Wolf Statues", glyph = "W",
            sections = new[]
            {
                ("WHAT THEY DO", "Breakable wolf idols. One hit smashes them. Cats ignore them, so they're up to you."),
            },
        },
        new Topic
        {
            id = "weapondrop", name = "Weapon Drops", glyph = "!", icon = () => WeaponIcon(3),
            sections = new[]
            {
                ("WHAT THEY ARE", "A dropped weapon floats in a beam of light with sparkles, showing its icon."),
                ("PICKING UP", "Walk up and press Y / E. It comes with full durability and is equipped right away."),
            },
        },
        new Topic
        {
            id = "gems", name = "EXP Gems", glyph = "+", icon = () => PrefabIcon("Systems/EXPgem"),
            sections = new[]
            {
                ("WHAT THEY DO", "Burst out of defeated enemies and swim through the air to Rowdy when he's near. Each one is EXP."),
            },
        },
        new Topic
        {
            id = "heart", name = "Hearts", glyph = "+",
            sections = new[]
            {
                ("WHAT THEY DO", "Some pelicans carry a heart. Knock it loose and grab it to heal 10 HP."),
                ("NOTE", "You can't pick one up at full health - it stays there for later."),
            },
        },
        new Topic
        {
            id = "catpickup", name = "Lost Cats", glyph = "C", icon = () => CatIcon("Interactables/SamuraiCat"),
            sections = new[]
            {
                ("WHAT THEY ARE", "Cats hiding around the map. Touch one to bring it along. See Game Mechanics > Cats."),
            },
        },
        new Topic
        {
            id = "oldman", name = "The Old Man", glyph = "?",
            sections = new[]
            {
                ("WHO", "An old man living in a tent by the mountain. He looks harmless."),
                ("CAREFUL", "Hurt him and the curse takes over: he becomes a Transform Wolf."),
            },
        },
    };

    // ---------------------------------------------------------------- icon helpers
    private static Sprite WeaponIcon(int index)
    {
        WeaponManager weapons = WeaponManager.Instance;
        return weapons != null ? weapons.GetProfileByIndex(index) : null;
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
