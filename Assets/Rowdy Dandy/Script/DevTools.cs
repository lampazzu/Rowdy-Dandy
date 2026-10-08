using UnityEngine;
using UnityEngine.SceneManagement;

// What the Dev Tools page of the pause menu does (hold START on the gamepad, or F1 / `). Testing only.
// The old hotkeys (0 reset, 1/2/3 checkpoints, , . ; levels, 7-9 weapons) still work too.
public static class DevTools
{
    public static bool GodMode { get; set; }

    private static Transform Rowdy
    {
        get
        {
            Health h = Object.FindFirstObjectByType<Health>(); // Rowdy's body (other objects carry the Player tag too)
            if (h != null) return h.transform;
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            return p != null ? p.transform : null;
        }
    }

    private static Vector3 InFrontOfRowdy(float distance = 1.2f, float up = 0.6f)
    {
        Transform r = Rowdy;
        if (r == null) return Vector3.zero;
        float facing = r.localScale.x >= 0f ? 1f : -1f;
        return r.position + new Vector3(facing * distance, up, 0f);
    }

    // ---------------------------------------------------------------- progress
    public static void ResetGame() => DevReset.ResetEverything();
    public static void LevelUp() { if (PlayerStats.Instance != null) PlayerStats.Instance.LevelUp(); }
    public static void LevelDown() { if (PlayerStats.Instance != null) PlayerStats.Instance.LevelDown(); }
    public static void ResetLevel() { if (PlayerStats.Instance != null) PlayerStats.Instance.ResetLevelAndEXP(); }

    public static void GiveAllWeapons()
    {
        WeaponManager wm = WeaponManager.Instance;
        if (wm == null) return;
        wm.PickupWeapon(WeaponType.Cleaver, 50f);
        wm.PickupWeapon(WeaponType.Naginata, 50f);
        wm.PickupWeapon(WeaponType.Sword, 50f);
    }

    // ---------------------------------------------------------------- travel (reload the level at a checkpoint)
    public static void GoToFirstCheckpoint() => GoTo(FindCheckpoint(true));
    public static void GoToLastCheckpoint() => GoTo(FindCheckpoint(false));
    public static void GoToSavedCheckpoint() => Reload();

    private static Transform FindCheckpoint(bool leftMost)
    {
        Transform best = null;
        foreach (RespawnTrigger c in Object.FindObjectsByType<RespawnTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            float x = c.transform.position.x;
            if (best == null || (leftMost ? x < best.position.x : x > best.position.x)) best = c.transform;
        }
        return best;
    }

    private static void GoTo(Transform checkpoint)
    {
        if (checkpoint != null)
        {
            // same keys RespawnTrigger saves and PlayerRespawn reads
            PlayerPrefs.SetFloat("RespawnX", checkpoint.position.x);
            PlayerPrefs.SetFloat("RespawnY", checkpoint.position.y + 1f);
            PlayerPrefs.Save();
        }
        Reload();
    }

    private static void Reload()
    {
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
    }

    // ---------------------------------------------------------------- spawns
    public static void SpawnRat() => FlyingRat.Spawn(InFrontOfRowdy(1.6f, 0.4f));
    public static void SpawnFish() => CatTreat.Spawn(InFrontOfRowdy(1f, 0.6f));
    public static void SpawnStatue() => StatueScatter.SpawnNear(InFrontOfRowdy(2.2f, 0.5f));

    public static void CollectAllCats()
    {
        Transform r = Rowdy;
        if (r == null) return;
        foreach (PetFollower pet in PetFollower.Pets)
            if (pet != null && !pet.IsCollected) pet.Collect(r);
    }

    // ---------------------------------------------------------------- cheats
    public static void Heal()
    {
        Transform r = Rowdy;
        if (r != null && r.TryGetComponent(out Health h)) h.AddHealth(50f, true);
    }

    public static void KillNearby()
    {
        Transform r = Rowdy;
        if (r == null) return;
        foreach (EnemyHealth e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (e == null || e.enemydead || e.IsObject) continue;
            if (Vector2.Distance(e.transform.position, r.position) > 12f) continue;
            EnemyHealth.CreditNextHit(KillCredit.Rowdy());
            e.TakeDamageEnemy(e.currentenemyHealth + 1f);
        }
    }
}
