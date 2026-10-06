using System.Collections;
using UnityEngine;

public class SpawnAndDestroy : MonoBehaviour
{
    public GameObject prefabToSpawn; // Prefab to spawn
    public float minSpawnTime = 1f; // Minimum spawn time (seconds)
    public float maxSpawnTime = 5f; // Maximum spawn time (seconds)
    public float timeBeforeDestruction = 1f; // Time before destruction (minutes)

    private Coroutine spawnRoutine; // Store coroutine reference

    private void OnEnable()
    {
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            float spawnTime = Random.Range(minSpawnTime, maxSpawnTime);
            yield return new WaitForSeconds(spawnTime);

            GameObject spawnedObject = Instantiate(prefabToSpawn, transform.position, Quaternion.identity);
            Destroy(spawnedObject, timeBeforeDestruction * 60f); // Convert minutes to seconds
        }
    }
}
