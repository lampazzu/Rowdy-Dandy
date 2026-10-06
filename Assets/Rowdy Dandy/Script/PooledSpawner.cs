using System.Collections;
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

    private GameObject[] objectPool;
    private int spawnedObjectCount = 0;
    private int currentSpawnIndex = 0;
    private bool isSpawning = false;


    private void OnEnable()
    {
        if (!isSpawning)
        {
            isSpawning = true;
            StartCoroutine(SpawnObjects());
        }
    }

    private void OnDisable()
    {
        // Stop spawning when the script is disabled
        isSpawning = false;
        StopCoroutine(SpawnObjects());
    }

    private void Update()
    {
        // Check if the script is active and respond accordingly
        if (isSpawning)
        {
            // Your update logic here (if needed)
        }
    }

    private void Start()
    {
        // Check if prefabsToSpawn array is empty
        if (prefabsToSpawn == null || prefabsToSpawn.Length == 0)
        {
            Debug.LogError("No prefabs assigned to spawn. Please assign prefabs to the 'Prefabs To Spawn' array in the inspector.");
            return; // Stop execution if no prefabs are assigned
        }

        // Initialize object pool
        InitializeObjectPool();

        // Start spawning
        StartCoroutine(SpawnObjects());

        // Optionally, start increasing the pool size over time
        if (increasePoolSizeOverTime)
        {
            StartCoroutine(IncreasePoolSizeOverTime());
        }
    }

    private void InitializeObjectPool()
    {
        objectPool = new GameObject[initialPoolSize];

        for (int i = 0; i < initialPoolSize; i++)
        {
            objectPool[i] = CreatePooledObject(prefabsToSpawn[0]); // Initialize with the first prefab
        }
    }

    private IEnumerator SpawnObjects()
    {
        while (true)
        {
            float spawnInterval = Random.Range(minSpawnRate, maxSpawnRate);
            yield return new WaitForSeconds(spawnInterval);

            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

            // Get a random prefab from the array
            GameObject prefabToSpawn = prefabsToSpawn[Random.Range(0, prefabsToSpawn.Length)];

            if (spawnedObjectCount < maxSpawnedObjects)
            {
                GameObject spawnedObject = GetPooledObject(prefabToSpawn);

                spawnedObject.transform.position = spawnPoint.position;
                spawnedObject.transform.rotation = spawnPoint.rotation;

                // Flip the spawned object based on the character's scale
                spawnedObject.transform.localScale = new Vector3(characterTransform.localScale.x, 1, 1);

                spawnedObject.SetActive(true);

                spawnedObjectCount++;

                
            }
            else
            {
                RecycleOldestObject(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
            }
        }
    }

    private GameObject GetPooledObject(GameObject prefabToSpawn)
    {
        for (int i = 0; i < objectPool.Length; i++)
        {
            if (!objectPool[i].activeInHierarchy)
            {
                // Re-initialize the object by replacing it with a new instance of the chosen prefab
                objectPool[i] = CreatePooledObject(prefabToSpawn);
                return objectPool[i];
            }
        }

        GameObject newObj = CreatePooledObject(prefabToSpawn);
        objectPool = AddObjectToArray(objectPool, newObj);

        return newObj;
    }

    private void RecycleOldestObject(GameObject prefabToSpawn, Vector3 position, Quaternion rotation)
    {
        // Recycle the oldest spawned object by replacing it with a new instance of the chosen prefab
        GameObject oldestObject = objectPool[currentSpawnIndex];
        oldestObject.SetActive(false);

        objectPool[currentSpawnIndex] = CreatePooledObject(prefabToSpawn);
        objectPool[currentSpawnIndex].transform.position = position;
        objectPool[currentSpawnIndex].transform.rotation = rotation;
        objectPool[currentSpawnIndex].SetActive(true);

        // Update the spawn index for the next recycle
        currentSpawnIndex = (currentSpawnIndex + 1) % objectPool.Length;
    }

    private GameObject CreatePooledObject(GameObject prefabToSpawn)
    {
        GameObject newObj = Instantiate(prefabToSpawn, Vector3.zero, Quaternion.identity);
        newObj.SetActive(false);
        return newObj;
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
        int newSize = objectPool.Length + 1;
        // Choose a random prefab from the array
        GameObject prefabToSpawn = prefabsToSpawn[Random.Range(0, prefabsToSpawn.Length)];
        GameObject newObj = CreatePooledObject(prefabToSpawn);
        objectPool = AddObjectToArray(objectPool, newObj);
        Debug.Log($"Pool size increased to {newSize}");
    }

    private GameObject[] AddObjectToArray(GameObject[] array, GameObject newObj)
    {
        GameObject[] newArray = new GameObject[array.Length + 1];
        array.CopyTo(newArray, 0);
        newArray[array.Length] = newObj;
        return newArray;
    }
}