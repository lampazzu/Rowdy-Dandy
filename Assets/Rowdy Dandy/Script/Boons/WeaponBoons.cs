using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// The Blacksmith's weapon boons (Split Edge = Sword, Skewer = Naginata, Butcher Block = Cleaver) come with the weapon:
//   - taking the boon puts a fresh one in Rowdy's hands right away
//   - every respawn / load he has it again (a fresh one if it broke), in his hands
//   - enemies drop it more often: an extra roll on every kill (EnemyHealth -> ExtraDrop)
// (Hook Line And Sinker is the Rod: he always has that one.)
public static class WeaponBoons
{
    private static readonly (string boon, WeaponType type, string drop)[] Map =
    {
        ("splitedge", WeaponType.Sword, "Systems/WeaponDropSword"),
        ("skewer", WeaponType.Naginata, "Systems/WeaponDropNaginata"),
        ("butcher", WeaponType.Cleaver, "Systems/WeaponDropCleaver"),
    };

    public const float FreshDurability = 50f;
    public const float ExtraDropPercent = 10f; // per kill, x drop luck

    public static void OnTaken(string id)
    {
        foreach (var m in Map)
            if (m.boon == id && WeaponManager.Instance != null) WeaponManager.Instance.PickupWeapon(m.type, FreshDurability);
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
            if (wm != null)
            {
                int equip = -1;
                foreach (var m in Map)
                {
                    if (!Boons.Has(m.boon)) continue;
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
        foreach (var m in Map)
        {
            if (!Boons.Has(m.boon) || Random.Range(0f, 100f) > ExtraDropPercent * DropLuck.Multiplier) continue;
            GameObject prefab = Resources.Load<GameObject>(m.drop);
            if (prefab != null) Object.Instantiate(prefab, at + new Vector3(Random.Range(-0.3f, 0.3f), 0.2f, 0f), Quaternion.identity);
            return; // one extra weapon per kill at most
        }
    }
}
