using UnityEngine;
using UnityEngine.SceneManagement;

// Dev tool: press 0 (top row or keypad) to wipe the game's progress and reload the level at the last checkpoint.
// Clears: level + EXP, weapons (unlocks, durability, equipped), Rowdy Notes progress (enemies met, kills,
// weapons found), run stats, collected cats. Keeps the Settings menu choices and the checkpoint.
// Like the other dev hotkeys (1/2, , . ;, 7-9) it's meant for testing - remove before release.
public class DevReset : MonoBehaviour
{
    private static DevReset instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var go = new GameObject("DevReset (auto)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DevReset>();
    }

    private void Update()
    {
        if (PauseMenu.IsPaused) return;
        if (GameInput.KeyDown(KeyCode.Alpha0) || GameInput.KeyDown(KeyCode.Keypad0)) ResetEverything();
    }

    public static void ResetEverything()
    {
        if (PlayerStats.Instance != null) PlayerStats.Instance.ResetLevelAndEXP(); // in case it outlives the reload
        PlayerPrefs.DeleteKey("PlayerLevel");
        PlayerPrefs.DeleteKey("PlayerEXP");
        PlayerPrefs.DeleteKey("SelectedWeapon");
        for (int i = 0; i < 4; i++)
        {
            PlayerPrefs.DeleteKey("WeaponUnlocked_" + i);
            PlayerPrefs.DeleteKey("WeaponCurDurability_" + i);
            PlayerPrefs.DeleteKey("WeaponMaxDurability_" + i);
        }
        RowdyNotes.ResetProgress();
        WorldMap.ResetProgress();
        DropLuck.Reset();
        OreNode.ForgetAllSaved();
        Tutorials.ResetAll();
        Boons.ClearAll();
        PlayerPrefs.Save();

        RunStats.ResetAll();
        CatRoster.ClearAll();

        Debug.Log("<color=orange>[DevReset]</color> All progress wiped (level, EXP, weapons, notes, stats, cats). Reloading.");
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
    }
}
