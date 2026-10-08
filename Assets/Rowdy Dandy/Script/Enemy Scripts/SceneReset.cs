using UnityEngine;
using UnityEngine.SceneManagement;

// 1 = reload the scene at the last checkpoint.
// 2 = reload the scene at the test checkpoint (the one next to PelichAnus at the end of the map).
// 3 = reload the scene at the first checkpoint ("Spawner") from the left = the start of the map.
public class SceneReset : MonoBehaviour
{
    [Tooltip("Checkpoint used by key 2. Leave empty to use the checkpoint closest to the object named below (or the right-most one).")]
    [SerializeField] private Transform testCheckpoint;
    [SerializeField] private string testCheckpointNearObject = "PelichAnus";
    [Tooltip("Spawn a bit above the checkpoint so Rowdy drops onto the ground instead of starting inside it.")]
    [SerializeField] private float testSpawnHeight = 1f;

    // Update is called once per frame
    void Update()
    {
        if (GameInput.KeyDown(KeyCode.Alpha1) || GameInput.KeyDown(KeyCode.Keypad1))
        {
            ResetScene();
        }
        else if (GameInput.KeyDown(KeyCode.Alpha2) || GameInput.KeyDown(KeyCode.Keypad2))
        {
            GoToTestCheckpoint();
        }
        else if (GameInput.KeyDown(KeyCode.Alpha3) || GameInput.KeyDown(KeyCode.Keypad3))
        {
            GoToCheckpoint(FindLeftmostCheckpoint());
        }
    }

    Transform FindLeftmostCheckpoint()
    {
        Transform best = null;
        foreach (RespawnTrigger checkpoint in FindObjectsByType<RespawnTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (best == null || checkpoint.transform.position.x < best.position.x) best = checkpoint.transform;
        }
        return best;
    }

    void ResetScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    void GoToTestCheckpoint()
    {
        GoToCheckpoint(testCheckpoint != null ? testCheckpoint : FindTestCheckpoint());
    }

    void GoToCheckpoint(Transform checkpoint)
    {
        if (checkpoint == null)
        {
            Debug.LogWarning("SceneReset: no checkpoint found, reloading at the last checkpoint instead.");
            ResetScene();
            return;
        }

        // Same keys RespawnTrigger saves and PlayerRespawn reads on load
        Vector2 spawnPoint = (Vector2)checkpoint.position + Vector2.up * testSpawnHeight;
        PlayerPrefs.SetFloat("RespawnX", spawnPoint.x);
        PlayerPrefs.SetFloat("RespawnY", spawnPoint.y);
        PlayerPrefs.Save();

        ResetScene();
    }

    Transform FindTestCheckpoint()
    {
        RespawnTrigger[] checkpoints = FindObjectsByType<RespawnTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (checkpoints.Length == 0) return null;

        Transform anchor = null;
        if (!string.IsNullOrEmpty(testCheckpointNearObject))
        {
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == testCheckpointNearObject) { anchor = t; break; }
            }
        }

        Transform best = null;
        float bestScore = float.MaxValue;
        foreach (RespawnTrigger checkpoint in checkpoints)
        {
            Transform t = checkpoint.transform;
            // Closest to the anchor, or right-most (end of the map) if there is no anchor
            float score = anchor != null ? Vector2.Distance(t.position, anchor.position) : -t.position.x;
            if (score < bestScore)
            {
                bestScore = score;
                best = t;
            }
        }
        return best;
    }
}
