using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// One colosseum trial = one run, Hades style (FrontierArena):
//   - ringing the gong for a fresh trial resets the build: level 1, no boons, the cat party leaves (back to their
//     hiding spots, first in line for a rat - drop one at the gong to lure them back). Hair gel and rats stay.
//   - over the 30 waves Rowdy climbs from level 1 to 10: LevelPlan says the least level he should have after each
//     wave (topped up if the kills fell short) and LevelCap keeps him from running more than a level ahead.
//   - boons don't interrupt the fight: picks bank up (HoldBoons): 3 at the gong, 1 every 5 waves (9 in all).
//   - a SEAL (waves 10 / 20) saves the build; dying after it, the next trial starts there with that build.
public static class ArenaRun
{
    public static int LevelCap;        // 0 = no cap (outside a trial)
    public static bool HoldBoons;      // BoonPicker waits while this is on
    public static bool InTrial;        // level ups grant no boons: the trial hands them out (3 at the gong, 1 every 5 waves)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        LevelCap = 0;
        HoldBoons = false;
        InTrial = false;
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
    }

    // a death mid-trial reloads the scene: the trial is over
    private static void OnLoaded(Scene s, LoadSceneMode m) { LevelCap = 0; HoldBoons = false; InTrial = false; }

    public const int Waves = 30;

    // The least level after clearing wave w (1-based): level 10 by wave 28
    public static int MinLevelAfter(int w) => Mathf.Clamp(1 + Mathf.FloorToInt(9f * w / 28f), 1, PlayerStats.MaxLevel);

    // How far ahead the kills may carry him while fighting wave w (1-based)
    public static int CapDuring(int w) => Mathf.Clamp(MinLevelAfter(w) + 1, 1, PlayerStats.MaxLevel);

    public static void FreshStart()
    {
        PlayerStats stats = PlayerStats.Instance;
        if (stats != null) stats.SetLevel(1);
        Boons.ResetBuild();
        List<string> gone = CatRoster.SendAllAway();
        CatRoster.EnforceCapacity();
        if (gone.Count > 0 && BoonRunner.Rowdy != null)
            IconPopup.Show(BoonRunner.RowdyCenter + Vector3.up * 1.4f, null, gone.Count == 1 ? "YOUR CAT WAITS OUTSIDE" : "YOUR CATS WAIT OUTSIDE", new Color(1f, 0.75f, 0.9f), 0.8f, 2.4f);
        HealFull();
    }

    // ---------------------------------------------------------------- seals
    private static string SealKey(string arenaKey) => arenaKey + "SealBuild";

    public static void SaveSeal(string arenaKey)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(PlayerStats.Level).Append('#').Append(Boons.Export()).Append('#').Append(string.Join(",", CatRoster.PartyKeys));
        PlayerPrefs.SetString(SealKey(arenaKey), sb.ToString());
        PlayerPrefs.Save();
    }

    public static bool RestoreSeal(string arenaKey)
    {
        string s = PlayerPrefs.GetString(SealKey(arenaKey), "");
        if (string.IsNullOrEmpty(s)) return false;
        string[] p = s.Split('#');
        if (p.Length < 2 || !int.TryParse(p[0], out int level)) return false;
        PlayerStats stats = PlayerStats.Instance;
        if (stats != null) stats.SetLevel(level);
        Boons.Import(p[1]);
        CatRoster.SendAllAway();
        if (p.Length > 2 && p[2].Length > 0) CatRoster.Recall(p[2].Split(','), BoonRunner.Rowdy);
        HealFull();
        return true;
    }

    public static void ClearSeal(string arenaKey) => PlayerPrefs.DeleteKey(SealKey(arenaKey));

    private static void HealFull()
    {
        if (BoonRunner.Rowdy != null && BoonRunner.Rowdy.TryGetComponent(out Health h)) h.AddHealth(h.startingHealth, false);
    }
}
