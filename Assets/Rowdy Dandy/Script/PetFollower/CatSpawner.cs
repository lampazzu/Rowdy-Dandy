using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Puts the newer cats (Paprika, Mushidon, The Peak, Lallo, Tchogon) into every level that doesn't place them itself:
// one of each from Resources/Interactables/<name>.prefab, "home" next to a checkpoint spread along the map.
// CatRoster then hides them at random reachable spots like the level-placed cats. Drop a prefab into the scene
// by hand and this leaves that type alone.
public static class CatSpawner
{
    private static readonly (PetFollower.CatType type, string prefab)[] Cats =
    {
        (PetFollower.CatType.Paprika, "Interactables/Paprika"),
        (PetFollower.CatType.Mushidon, "Interactables/Mushidon"),
        (PetFollower.CatType.Peak, "Interactables/ThePeak"),
        (PetFollower.CatType.Lallo, "Interactables/Lallo"),
        (PetFollower.CatType.Tchogon, "Interactables/Tchogon"),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        Spawn();
        SceneManager.sceneLoaded += (s, m) => Spawn();
    }

    private static void Spawn()
    {
        // Only levels that have cats at all
        bool levelHasCats = false;
        var present = new HashSet<PetFollower.CatType>();
        foreach (PetFollower pet in PetFollower.Pets)
        {
            if (pet == null) continue;
            levelHasCats = true;
            present.Add(pet.Type);
        }
        if (!levelHasCats) return;

        var checkpoints = new List<Vector3>();
        foreach (RespawnTrigger r in Object.FindObjectsByType<RespawnTrigger>(FindObjectsSortMode.None)) checkpoints.Add(r.transform.position);
        if (checkpoints.Count == 0) return;
        checkpoints.Sort((a, b) => a.x.CompareTo(b.x));

        for (int i = 0; i < Cats.Length; i++)
        {
            if (present.Contains(Cats[i].type)) continue;
            GameObject prefab = Resources.Load<GameObject>(Cats[i].prefab);
            if (prefab == null) continue;
            // fixed home (it's the cat's identity across reloads): spread over the checkpoints, never the first one
            int index = Mathf.Clamp(Mathf.RoundToInt((i + 1) * (checkpoints.Count - 1) / (float)(Cats.Length + 1)) + 1, 0, checkpoints.Count - 1);
            Vector3 home = checkpoints[index] + new Vector3(1.6f, 0.7f, 0f);
            Object.Instantiate(prefab, new Vector3(home.x, home.y, 0f), Quaternion.identity).name = prefab.name;
        }
    }
}
