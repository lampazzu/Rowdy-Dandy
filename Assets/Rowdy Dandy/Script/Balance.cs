using System.Collections.Generic;
using UnityEngine;

// The game's balance (formerly "Jarvis Balance"; the old Lamps numbers were retired 2026-10-08).
// Every tuned number lives in this file, so it's one place to tune. A "prefab" value passed in is what
// the prefab / scene / code had, used when nothing here overrides it.
public static class Balance
{    // ================================================================ Rowdy
    public const float RowdyMaxHealth = 120f;           // Lamp: 100

    // Base damage per hitbox (by object name). Lamp: ground 10, air 4, surf contact 1
    public static float RowdyBaseDamage(string hitbox, float lampValue)
    {
        if (hitbox == "DamageBoxAir") return 8f;
        return lampValue;
    }

    // Share of the level-up flat damage a hitbox gets (Lamp: all of it, so the surf contact box hit for 50 at level 10)
    public static float LevelBonusShare(string hitbox) => hitbox == "SurfDamage" ? 0.25f : 1f;

    // Per level up (index 0 = reaching level 2). Lamp: +1..+5 damage and +1..+25% crit to level 6, then +6 / +5% per level
    // (level 10: +39 damage, 76% crit, x2.4 crits). Jarvis: steady, ends at +30 damage, 18% crit, x1.95 crits.
    private static readonly float[] DamageGain = { 2, 2, 3, 3, 3, 4, 4, 4, 5 };
    public const float CritChanceGain = 2f;
    public const float CritMultiplierGain = 0.05f;
    public static float LevelDamageGain(int levelIndex) => DamageGain[Mathf.Clamp(levelIndex, 0, DamageGain.Length - 1)];

    // EXP to go from level N to N+1 (index 0 = level 1). Faster first boons, steeper end. Lamp: 100 x5, then 305..596.
    // 2026-10-09 (mega fix 8): level 3 after ~110 EXP and level 5 after ~330, so Rowdy meets Pelich around level 5.
    // 2026-10-09 (mega fix 9): was too fast. Level 3 is still quick (~110 EXP), then each level costs a lot more.
    private static readonly float[] ExpToNext = { 40, 70, 180, 300, 450, 620, 820, 1050, 1300 };
    public static float ExpRequired(int level) => ExpToNext[Mathf.Clamp(level - 1, 0, ExpToNext.Length - 1)];

    // EXP per gem picked up. The gem prefab says 0.2 (since the very first commit), which made levels crawl:
    // a Gnoll Warrior's 15 gems were worth 3 EXP. Every enemy's gem count stays as it is; each gem is worth this now.
    public const float GemExp = 2.5f;
    public static float GemValue(float prefabValue) => GemExp;

    // ================================================================ enemies (by EnemyCatalog id)
    private struct EnemyStats { public float hp, damage; public EnemyStats(float hp, float damage) { this.hp = hp; this.damage = damage; } }

    // hp = max health (0 = keep the prefab's), damage = x the prefab's hit values
    private static readonly Dictionary<string, EnemyStats> Enemies = new Dictionary<string, EnemyStats>
    {
        { "bigwolf",        new EnemyStats(30f,   0.8f) },  // Lamp 10 HP: died to a single swing
        { "transformwolf",  new EnemyStats(35f,   0.8f) },  // Lamp 25
        { "gnollarcher",    new EnemyStats(25f,   0.9f) },  // Lamp 10 - fragile, the arrows are the threat
        { "werefast",       new EnemyStats(40f,   0.9f) },  // Lamp 20
        { "gnollbomber",    new EnemyStats(35f,   1f)   },  // Lamp 10
        { "watervivarider", new EnemyStats(70f,   1f)   },  // Lamp 30
        { "gnollwarrior",   new EnemyStats(140f,  0.9f) },  // Lamp 200 - a wall for a difficulty 5 enemy
        { "wereknight",     new EnemyStats(110f,  1.4f) },  // Lamp 10 HP (!) for an armoured knight
        { "horserider",     new EnemyStats(120f,  1.2f) },  // Lamp 60
        { "megacreature",   new EnemyStats(220f,  1.5f) },  // Lamp 100 - the top tier should feel like it
        { "sharkwolf",      new EnemyStats(90f,   0.6f) },  // Lamp 50 HP, 30 per bite from out of the water
        { "pelich",         new EnemyStats(2000f, 1.3f) },  // Lamp 5000 (scene override; PelichBoss code says 600) - ~55 hits at level 8 here
        { "moonboundelder", new EnemyStats(900f,  1f)   },  // Lamp 450
        { "voltrat",        new EnemyStats(30f,   0.45f) }, // the Red Jelly (2026-10-10: x1 hit way too hard for something you can't parry)
    };

    // Deeper = tougher. Pelich's zone is his arena, bosses skip this.
    private static float ZoneHealth(Vector2 p)
    {
        if (p.x >= 205f) return 1.4f;
        if (p.x >= 5f && p.x < 60f && p.y >= 7f) return 1.4f; // Gloomy Forest up top
        if (p.x >= 160f) return 1.25f;                         // Gnoll Tower
        if (p.x >= 104f) return 1.1f;                          // Jungle
        return 1f;                                             // Beach
    }

    private static float ZoneDamage(Vector2 p)
    {
        if (p.x >= 205f || (p.x >= 5f && p.x < 60f && p.y >= 7f)) return 1.25f;
        if (p.x >= 160f) return 1.15f;
        if (p.x >= 104f) return 1.05f;
        return 1f;
    }

    private static bool IsBoss(string id) => id == "pelich" || id == "moonboundelder";

    // By name, without EnemyCatalog.Identify's per-object cache (spawned enemies can be renamed after Awake)
    private static EnemyCatalog.Entry Kind(Component c)
    {
        Transform t = c != null ? c.transform : null;
        for (int depth = 0; depth < 4 && t != null; depth++, t = t.parent)
        {
            EnemyCatalog.Entry found = EnemyCatalog.Match(t.name);
            if (found != null) return found;
        }
        return null;
    }

    // EnemyHealth.Awake: max health for this enemy (or the prefab's)
    public static float EnemyHealth(EnemyHealth e, float lampValue)
    {
        if (e == null || e.IsObject) return lampValue;
        if (lampValue <= 1f) return lampValue; // one-hit set pieces placed in the scene (e.g. the archer by Pelich) stay that way
        EnemyCatalog.Entry kind = Kind(e);
        if (kind == null || !Enemies.TryGetValue(kind.id, out EnemyStats s) || s.hp <= 0f) return lampValue;
        float hp = s.hp;
        if (!IsBoss(kind.id))
        {
            hp *= ZoneHealth(e.transform.position);
            if (Encore.BloodMoon) hp *= Encore.BloodMoonHealth;
        }
        return Mathf.Round(hp);
    }

    // Spike (enemy hitboxes, arrows, bombs): damage for a hit worth lampValue
    public static float EnemyDamage(Component source, EnemyHealth owner, float lampValue)
    {
        if (lampValue <= 0f || lampValue >= 500f) return lampValue; // kill boxes stay kill boxes
        float m = 1f;
        EnemyCatalog.Entry kind = owner != null ? Kind(owner) : null;
        if (kind != null && Enemies.TryGetValue(kind.id, out EnemyStats s)) m = s.damage;
        Vector3 at = source != null ? source.transform.position : Vector3.zero;
        if (kind == null || !IsBoss(kind.id)) m *= ZoneDamage(at);
        return Mathf.Round(lampValue * m);
    }

    // PelichBoss stomp. Lamp 18
    public static float PelichStomp(float lampValue) => 24f;

    // ================================================================ wave spawner (overrides, applied at Start)
    public const float SpawnTimeBetweenWaves = 8f;   // Lamp 10
    public const float SpawnInterval = 0.35f;        // Lamp 0.2 - a portal every 0.2s read as one blob
    public const int SpawnMinPerWave = 4;            // Lamp 5
    public const int SpawnMaxPerWave = 8;            // Lamp 10
    public const int SpawnMaxAlive = 18;             // Lamp 25
    public const float SpawnNightEliteChance = 0.2f; // Lamp 0.15

    // ================================================================ cats
    // Every cat's cooldown x this (2026-10-08 nerf: cats act a bit less often). <1 = faster cats.
    public const float CatCooldownScale = 1.35f;
    // The party leader (Cat Party menu) cools down this much faster than the others
    public const float LeaderCooldownScale = 0.6f;
    // The sub-leader (unlocked at level 10, instead of a 10th cat slot)
    public const float SubLeaderCooldownScale = 0.8f;

    // ================================================================ boons
    // Wipeout: how long the wave carries enemies and the cooldown. Lamp: 0.55s at speed 12 (6.6 units: always off screen), 1s
    public static float WipeoutTravel => 0.3f;
    public static float WipeoutCooldown => 3f;
    public static float WipeoutWallBonus => 1.4f;
    public static Vector2 WipeoutFling => new Vector2(1.5f, 2f);

    // Numbers per boon: { values[0] per rarity, values[1]... } - missing = the BoonCatalog values
    public static readonly Dictionary<string, float[][]> BoonValues = new Dictionary<string, float[][]>
    {
        { "hairflip",    new[] { new float[] { 8, 12, 16 } } },        // Lamp 18 / 26 / 36
        { "admire",      new[] { new float[] { 2f, 2.4f, 2.8f } } },   // Lamp x2.5 / 3 / 3.5
        { "mainchar",    new[] { new float[] { 3, 4, 5 } } },          // Lamp 6 / 8 / 10% per rank (60% at SSS)
        { "wipeout",     new[] { new float[] { 10, 14, 18 } } },       // Lamp 20 / 28 / 38
        { "hangten",     new[] { new float[] { 12, 17, 23 } } },       // Lamp 16 / 23 / 32
        { "saltwater",   new[] { new float[] { 2, 3, 4 } } },          // Lamp 3 / 4.5 / 6
        { "feral",       new[] { new float[] { 15, 22, 30 } } },       // Lamp 35 / 50 / 65%
        { "moonrage",    new[] { new float[] { 15, 22, 30 } } },       // Lamp 25 / 35 / 45%
        { "bloodthirst", new[] { new float[] { 2, 3, 5 } } },          // Lamp 4 / 6 / 9
        { "packleader",  new[] { new float[] { 40, 60, 80 } } },       // Lamp 60 / 90 / 120%
        { "sporestep",   new[] { new float[] { 5, 7, 10 } } },         // Lamp 8 / 12 / 16
        { "rottenedge",  new[] { new float[] { 4, 6, 9 } } },          // Lamp 6 / 9 / 13
        { "furcoat",     new[] { new float[] { 2, 3, 4 } } },          // Lamp 3 / 4 / 5% per cat (40% at 8 cats)
        { "felinefury",  new[] { new float[] { 14, 20, 28 } } },       // Lamp 22 / 32 / 44
        { "weaponsnob",  new[] { new float[] { 30, 45, 60 } } },       // Lamp 40 / 60 / 80
    };

    // Descriptions that changed meaning with the rebalance
    public static string BoonDescription(string id, string lampDesc)
    {
        return lampDesc;
    }
}
