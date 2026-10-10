using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// The Blacksmith's weapon boons come with their weapon:
//   Sword    - Split Edge, Dizzy Fighter, Grand Slash
//   Naginata - Skewer, Sky Spear, Onrush, Ground Breaker
//   Cleaver  - Butcher Block, Meat Shield, Five Slice
//   - taking the boon puts a fresh one in Rowdy's hands right away
//   - every respawn / load he has it again (a fresh one if it broke), in his hands
//   - enemies drop it more often: an extra roll on every kill (EnemyHealth -> ExtraDrop), one roll per weapon
// (Hook Line And Sinker / Just The Rod are the Rod: he always has that one.)
public static class WeaponBoons
{
    private static readonly (WeaponType type, string drop, string[] boons)[] Map =
    {
        (WeaponType.Sword, "Systems/WeaponDropSword", new[] { "splitedge", "dizzy", "swordcharge" }),
        (WeaponType.Naginata, "Systems/WeaponDropNaginata", new[] { "skewer", "skybeam", "onrush", "quake" }),
        (WeaponType.Cleaver, "Systems/WeaponDropCleaver", new[] { "butcher", "apron", "slices" }),
    };

    public const float FreshDurability = 50f;
    public const float ExtraDropPercent = 10f; // per kill, x drop luck

    private static bool Wants(string[] boons)
    {
        foreach (string b in boons) if (Boons.Has(b)) return true;
        return false;
    }

    public static void OnTaken(string id)
    {
        if (id == "justrod" && WeaponManager.Instance != null) { WeaponManager.Instance.SetWeaponToAxe(); return; }
        if (Boons.Has("justrod") || WeaponManager.Instance == null) return;
        foreach (var m in Map)
            if (System.Array.IndexOf(m.boons, id) >= 0) WeaponManager.Instance.PickupWeapon(m.type, FreshDurability);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        OnLoaded(default, LoadSceneMode.Single);
    }

    private static void OnLoaded(Scene s, LoadSceneMode m)
    {
        var go = new GameObject("WeaponBoons (respawn)");
        go.AddComponent<Runner>();
    }

    // Waits until the WeaponManager has loaded its save, then hands the boon weapons back
    private class Runner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            WeaponManager wm = WeaponManager.Instance;
            if (wm != null && !Boons.Has("justrod"))
            {
                int equip = -1;
                foreach (var m in Map)
                {
                    if (!Wants(m.boons)) continue;
                    if (!wm.IsUnlocked(m.type)) wm.PickupWeapon(m.type, FreshDurability);
                    equip = (int)m.type;
                }
                if (equip >= 0) wm.Equip((WeaponType)equip);
            }
            Destroy(gameObject);
        }
    }

    // One more roll on every kill for each boon weapon: drops it next to the usual loot
    public static void ExtraDrop(Vector3 at)
    {
        if (Boons.Has("justrod")) return;
        foreach (var m in Map)
        {
            if (!Wants(m.boons) || Random.Range(0f, 100f) > ExtraDropPercent * DropLuck.Multiplier) continue;
            GameObject prefab = Resources.Load<GameObject>(m.drop);
            if (prefab != null) Object.Instantiate(prefab, at + new Vector3(Random.Range(-0.3f, 0.3f), 0.2f, 0f), Quaternion.identity);
            return; // one extra weapon per kill at most
        }
    }
}
