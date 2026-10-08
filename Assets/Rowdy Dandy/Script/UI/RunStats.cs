using System.Collections.Generic;
using UnityEngine;

// Numbers for the stats screen (C / gamepad Select). Kept for the whole play session, so they survive
// deaths and checkpoint reloads; they start over when the game is restarted.
public static class RunStats
{
    public static int Kills;
    public static int ObjectsSmashed;
    public static int Deaths;
    public static int CriticalHits;
    public static int Counters;
    public static int Executions;   // Nick's finishing cuts
    public static int CatKills;     // kills by any cat (executions included)
    public static int CatsRescued;
    public static int CatsLost;
    public static int WeaponsBroken;
    public static int LevelUps;
    public static int CurrentStreak; // kills since Rowdy last got hurt
    public static int BestStreak;
    public static float MaxHit;
    public static string MaxHitWith = "";
    public static float TotalDamage;
    public static float DamageTaken;
    public static float PlayTime;    // seconds, paused time not counted
    // mega list 4 additions
    public static float Healed;
    public static float OverhealGained;
    public static int HitsBlocked;     // The Peak's armor
    public static int EnemiesPoisoned; // Paprika
    public static int EnemiesStunned;  // Mushidon
    public static int DecayBursts;     // Lallo
    public static int Checkpoints;
    public static int RatsGrabbed;
    public static int JellyBounces;
    public static int WeaponsPickedUp;
    public static int StatuesSmashed;
    public static readonly Dictionary<string, int> KillsByEnemy = new Dictionary<string, int>();

    public static void ResetAll() => ResetSession(); // dev reset (key 0)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Kills = ObjectsSmashed = Deaths = CriticalHits = Counters = Executions = CatKills = 0;
        CatsRescued = CatsLost = WeaponsBroken = LevelUps = CurrentStreak = BestStreak = 0;
        MaxHit = TotalDamage = DamageTaken = PlayTime = Healed = OverhealGained = 0f;
        HitsBlocked = EnemiesPoisoned = EnemiesStunned = DecayBursts = Checkpoints = RatsGrabbed = JellyBounces = WeaponsPickedUp = StatuesSmashed = 0;
        MaxHitWith = "";
        KillsByEnemy.Clear();
    }

    public static void RecordHit(float damage, string with)
    {
        if (damage <= 0f) return;
        TotalDamage += damage;
        if (damage > MaxHit)
        {
            MaxHit = damage;
            MaxHitWith = with ?? "";
        }
    }

    public static void RecordKill(string victim, bool byCat)
    {
        Kills++;
        if (byCat) CatKills++;
        CurrentStreak++;
        if (CurrentStreak > BestStreak) BestStreak = CurrentStreak;
        if (string.IsNullOrEmpty(victim)) victim = "?";
        KillsByEnemy.TryGetValue(victim, out int count);
        KillsByEnemy[victim] = count + 1;
    }

    public static void RecordDamageTaken(float damage)
    {
        DamageTaken += Mathf.Max(0f, damage);
        CurrentStreak = 0;
    }

    // Most killed enemy type, e.g. "GNOLL WARRIOR X12"
    public static string FavoriteVictim()
    {
        string best = null;
        int bestCount = 0;
        foreach (var pair in KillsByEnemy)
        {
            if (pair.Value > bestCount) { best = pair.Key; bestCount = pair.Value; }
        }
        return best == null ? "-" : best + " X" + bestCount;
    }

    public static string FormatTime(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
    }
}
