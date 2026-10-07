using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PooledSpawner : MonoBehaviour
{
    public GameObject[] prefabsToSpawn; // Array of prefabs to spawn
    public Transform[] spawnPoints;
    public int initialPoolSize = 10;
    public float minSpawnRate = 1f;
    public float maxSpawnRate = 5f;
    public bool increasePoolSizeOverTime = false;
    public float poolIncreaseRate = 1f;
    public int maxSpawnedObjects = 5; // Maximum allowed spawned objects
    public Transform characterTransform;

    // Each pooled instance remembers which prefab it came from, so it is only reused for that prefab
    private class PooledObject
    {
        public GameObject prefab;
        public GameObject instance;
    }

    private readonly List<PooledObject> objectPool = new List<PooledObject>();
    // Active objects in the order they were spawned (oldest first), used for recycling
    private readonly List<PooledObject> activeObjects = new List<PooledObject>();

    private bool isValid = false;
    private Coroutine spawnRoutine;
    private Coroutine poolGrowthRoutine;

    private void Awake()
    {
        isValid = ValidateSetup();
        if (isValid)
        {
            InitializeObjectPool();
        }
    }

    private void OnEnable()
    {
        if (!isValid) return;

        // Only one spawn loop at a time (previously Start and OnEnable both started one)
        if (spawnRoutine == null)
        {
            spawnRoutine = StartCoroutine(SpawnObjects());
        }

        if (increasePoolSizeOverTime && poolGrowthRoutine == null)
        {
            poolGrowthRoutine = StartCoroutine(IncreasePoolSizeOverTime());
        }
    }

    private void OnDisable()
    {
        // Stop spawning when the script is disabled
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        if (poolGrowthRoutine != null)
        {
            StopCoroutine(poolGrowthRoutine);
            poolGrowthRoutine = null;
        }
    }

    private bool ValidateSetup()
    {
        if (prefabsToSpawn == null || prefabsToSpawn.Length == 0)
        {
            Debug.LogError($"PooledSpawner on '{name}': no prefabs assigned to 'Prefabs To Spawn'. Spawner disabled.", this);
            return false;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError($"PooledSpawner on '{name}': no 'Spawn Points' assigned. Spawner disabled.", this);
            return false;
        }

        return true;
    }

    private void InitializeObjectPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject prefab = GetRandomPrefab();
            if (prefab != null)
            {
                CreatePooledObject(prefab);
            }
        }
    }

    private IEnumerator SpawnObjects()
    {
        while (true)
        {
            float spawnInterval = Random.Range(minSpawnRate, maxSpawnRate);
            yield return new WaitForSeconds(spawnInterval);

            Transform spawnPoint = GetRandomSpawnPoint();
            GameObject prefabToSpawn = GetRandomPrefab();

            // Skip this tick if the Inspector has empty slots
            if (spawnPoint == null || prefabToSpawn == null) continue;

            CleanUpDestroyedObjects();

            // At the limit -> recycle the oldest active object to make room
            if (activeObjects.Count >= maxSpawnedObjects && activeObjects.Count > 0)
            {
                activeObjects[0].instance.SetActive(false);
                activeObjects.RemoveAt(0);
            }

            PooledObject pooled = GetPooledObject(prefabToSpawn);
            GameObject spawnedObject = pooled.instance;

            spawnedObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

            // Flip the spawned object based on the character's scale
            if (characterTransform != null)
            {
                spawnedObject.transform.localScale = new Vector3(characterTransform.localScale.x, 1, 1);
            }

            spawnedObject.SetActive(true);
            activeObjects.Add(pooled);
        }
    }

    // Reuses an inactive instance of the same prefab, or creates a new one if none is free
    private PooledObject GetPooledObject(GameObject prefabToSpawn)
    {
        for (int i = 0; i < objectPool.Count; i++)
        {
            PooledObject pooled = objectPool[i];
            if (pooled.prefab == prefabToSpawn && !pooled.instance.activeInHierarchy)
            {
                return pooled;
            }
        }

        return CreatePooledObject(prefabToSpawn);
    }

    // Removes pool entries whose objects were destroyed elsewhere, and forgets objects
    // that deactivated themselves (e.g. died / vanished) so they don't count toward the limit
    private void CleanUpDestroyedObjects()
    {
        objectPool.RemoveAll(p => p.instance == null);
        activeObjects.RemoveAll(p => p.instance == null || !p.instance.activeInHierarchy);
    }

    private PooledObject CreatePooledObject(GameObject prefabToSpawn)
    {
        GameObject newObj = Instantiate(prefabToSpawn, Vector3.zero, Quaternion.identity);
        newObj.SetActive(false);

        PooledObject pooled = new PooledObject { prefab = prefabToSpawn, instance = newObj };
        objectPool.Add(pooled);
        return pooled;
    }

    private GameObject GetRandomPrefab()
    {
        return prefabsToSpawn[Random.Range(0, prefabsToSpawn.Length)];
    }

    private Transform GetRandomSpawnPoint()
    {
        return spawnPoints[Random.Range(0, spawnPoints.Length)];
    }

    private IEnumerator IncreasePoolSizeOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(poolIncreaseRate);
            IncreasePoolSize();
        }
    }

    public void IncreasePoolSize()
    {
        if (!isValid) return;

        // Pre-create one more inactive instance of a random prefab
        GameObject prefabToSpawn = GetRandomPrefab();
        if (prefabToSpawn == null) return;

        CreatePooledObject(prefabToSpawn);
        Debug.Log($"Pool size increased to {objectPool.Count}");
    }
}
