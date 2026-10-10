using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Which cats are out there is reshuffled on EVERY load (death, rest, reset):
//   - the hiding spots ("homes") = where the level placed its cats + one next to some checkpoints
//   - the cats Rowdy had (CatRoster.KeptKeys) keep their own home and type: they rejoin him (after a death only the
//     leader / sub-leader do; the others wander off as "GOT LOST" cats a rat can lure back)
//   - every other home gets a cat from an even shuffle of all 7 types (the level used to place 6 Wigs + 4 Nicks and
//     only 1 of each other cat, so Wig and Nick turned up far more often)
// CatRoster then hides them at random reachable spots as before. Cat identity = scene / type / home (RosterKey).
public static class CatSpawner
{
    private static readonly (PetFollower.CatType type, string prefab)[] Cats =
    {
        (PetFollower.CatType.Wig, "Interactables/Wig"),
        (PetFollower.CatType.Samurai, "Interactables/SamuraiCat"),
        (PetFollower.CatType.Paprika, "Interactables/Paprika"),
        (PetFollower.CatType.Mushidon, "Interactables/Mushidon"),
        (PetFollower.CatType.Peak, "Interactables/ThePeak"),
        (PetFollower.CatType.Lallo, "Interactables/Lallo"),
        (PetFollower.CatType.Tchogon, "Interactables/Tchogon"),
    };

    private const int CheckpointHomes = 5; // extra homes next to checkpoints (spread along the map, never the first)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        Spawn();
        SceneManager.sceneLoaded += (s, m) => Spawn();
    }

    private static void Spawn()
    {
        // Only levels that have cats at all
        var placed = new List<PetFollower>();
        foreach (PetFollower pet in PetFollower.Pets) if (pet != null && !pet.IsCollected) placed.Add(pet);
        if (placed.Count == 0) return;
        string scene = placed[0].gameObject.scene.name;

        // every home: the level's cat spots + a few checkpoints
        var homes = new List<Vector3>();
        foreach (PetFollower pet in placed) homes.Add(pet.HomePosition);
        var checkpoints = new List<Vector3>();
        foreach (RespawnTrigger r in Object.FindObjectsByType<RespawnTrigger>(FindObjectsSortMode.None)) checkpoints.Add(r.transform.position);
        checkpoints.Sort((a, b) => a.x.CompareTo(b.x));
        for (int i = 0; i < CheckpointHomes && checkpoints.Count > 1; i++)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt((i + 1) * (checkpoints.Count - 1) / (float)(CheckpointHomes + 1)) + 1, 0, checkpoints.Count - 1);
            homes.Add(checkpoints[index] + new Vector3(1.6f, 0.7f, 0f));
        }

        // the cats he keeps: their own type at their own home
        var kept = new HashSet<string>(CatRoster.KeptKeys());
        var keptHomes = new List<Vector3>();
        foreach (PetFollower pet in placed)
        {
            if (kept.Contains(pet.RosterKey)) { kept.Remove(pet.RosterKey); keptHomes.Add(pet.HomePosition); }
            else pet.Retire();
        }
        foreach (string key in kept) // kept cats that came from a reshuffled home last time
        {
            if (!ParseKey(key, scene, out PetFollower.CatType type, out Vector3 home)) continue;
            if (Make(type, home) != null) keptHomes.Add(home);
        }

        // everything else: an even shuffle of all the types
        var free = homes.FindAll(h => !keptHomes.Exists(k => Mathf.Abs(k.x - h.x) < 0.2f && Mathf.Abs(k.y - h.y) < 0.2f));
        var bag = new List<PetFollower.CatType>();
        while (bag.Count < free.Count)
        {
            var round = new List<PetFollower.CatType>();
            foreach (var c in Cats) round.Add(c.type);
            for (int i = round.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (round[i], round[j]) = (round[j], round[i]); }
            bag.AddRange(round);
        }
        for (int i = 0; i < free.Count; i++) Make(bag[i], free[i]);
    }

    private static PetFollower Make(PetFollower.CatType type, Vector3 home)
    {
        foreach (var c in Cats)
        {
            if (c.type != type) continue;
            GameObject prefab = Resources.Load<GameObject>(c.prefab);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, new Vector3(home.x, home.y, 0f), Quaternion.identity);
            go.name = prefab.name;
            return go.GetComponent<PetFollower>();
        }
        return null;
    }

    // RosterKey = "<scene>/<type>/<x*10>,<y*10>"
    private static bool ParseKey(string key, string scene, out PetFollower.CatType type, out Vector3 home)
    {
        type = PetFollower.CatType.Wig;
        home = Vector3.zero;
        string[] p = key.Split('/');
        if (p.Length != 3 || p[0] != scene || !System.Enum.TryParse(p[1], out type)) return false;
        string[] xy = p[2].Split(',');
        if (xy.Length != 2 || !int.TryParse(xy[0], out int x) || !int.TryParse(xy[1], out int y)) return false;
        home = new Vector3(x / 10f, y / 10f, 0f);
        return true;
    }
}
