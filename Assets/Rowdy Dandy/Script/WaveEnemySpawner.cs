using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WaveEnemySpawner : MonoBehaviour
{
    public enum EnemyType
    {
        Ground,
        Water,
        Flying
    }

    [System.Serializable]
    public class EnemyEntry
    {
        public GameObject prefab;
        public EnemyType enemyType = EnemyType.Ground;

        [Header("Generation")]
        [Range(1, 10)]
        public int difficulty = 1;

        [Tooltip("Higher values make this enemy more likely to appear.")]
        [Range(1, 10)]
        public int spawnWeight = 5;
    }

    [System.Serializable]
    public class Wave
    {
        public string waveName = "Wave";

        [Tooltip("Maximum enemy difficulty allowed in this wave.")]
        [Range(1, 10)]
        public int difficulty = 1;

        [Tooltip("Generated automatically when you press RANDOMIZE ALL WAVES.")]
        public List<EnemyEntry> enemies = new List<EnemyEntry>();
    }

    [Header("Player")]
    [SerializeField] private Transform player;

    [Tooltip("Automatically finds an object tagged Player if Player is empty.")]
    [SerializeField] private bool findPlayerAutomatically = true;

    [Header("Enemy Pool")]
    [SerializeField] private List<EnemyEntry> enemyPool = new List<EnemyEntry>();

    [Header("Waves")]
    [SerializeField] private List<Wave> waves = new List<Wave>();

    [Header("Wave Timing")]
    [Tooltip("Time before the first wave starts.")]
    [SerializeField] private float initialDelay = 2f;

    [Tooltip("Time between waves.")]
    [SerializeField] private float timeBetweenWaves = 5f;

    [Tooltip("Time between individual enemy spawns.")]
    [SerializeField] private float spawnInterval = 0.5f;

    [SerializeField] private bool loopWaves = false;

    [Header("Wave Generation")]
    [Tooltip("Minimum number of enemies generated in a wave.")]
    [SerializeField] private int minimumEnemiesPerWave = 10;

    [Tooltip("Maximum number of enemies generated in a wave.")]
    [SerializeField] private int maximumEnemiesPerWave = 50;

    [Tooltip("Higher difficulty enemies become less common automatically.")]
    [Range(0.1f, 3f)]
    [SerializeField] private float difficultyRarity = 1f;

    [Header("Spawn Position")]
    [SerializeField] private float minimumSpawnDistance = 8f;

    [SerializeField] private float maximumSpawnDistance = 14f;

    [SerializeField] private bool randomizeSpawnSide = true;

    [Tooltip("How far above the detected ground/water the enemy is spawned.")]
    [SerializeField] private float groundSpawnOffset = 0.2f;

    [Header("Ground Settings")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("Also accepts objects tagged 'Ground' even if they are not in Ground Layer.")]
    [SerializeField] private bool useGroundTag = true;
    [Tooltip("Distance above spawn point to check for a low ceiling. If hit, spawn location is rejected.")]
    [SerializeField] private float ceilingCheckDistance = 1.5f;

    [Header("Water Settings")]
    [SerializeField] private LayerMask waterLayer;
    [Tooltip("Also accepts objects tagged 'Water' even if they are not in Water Layer.")]
    [SerializeField] private bool useWaterTag = true;

    [Header("Flying Settings")]
    [Tooltip("Minimum height above player where flying enemies spawn.")]
    [SerializeField] private float minFlyingHeightOffset = 2f;
    [Tooltip("Maximum height above player where flying enemies spawn.")]
    [SerializeField] private float maxFlyingHeightOffset = 6f;

    [Header("Spawn VFX")]
    [Tooltip("Portal VFX spawned at the exact same position as each enemy.")]
    [SerializeField] private GameObject spawnPortalPrefab;

    [Header("Debug")]
    [SerializeField] private bool drawSpawnRadius = true;

    private Coroutine waveRoutine;
    private int currentWaveIndex = -1;

    private void Start()
    {
        FindPlayer();

        if (waves != null && waves.Count > 0)
        {
            waveRoutine = StartCoroutine(StartWaves());
        }
    }

    private void FindPlayer()
    {
        if (player != null)
            return;

        if (!findPlayerAutomatically)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogWarning("WaveEnemySpawner: No object with the 'Player' tag was found.");
        }
    }

    private IEnumerator StartWaves()
    {
        yield return new WaitForSeconds(initialDelay);

        do
        {
            for (int i = 0; i < waves.Count; i++)
            {
                if (waves[i] == null)
                    continue;

                currentWaveIndex = i;

                Debug.Log("WaveEnemySpawner: Starting " + waves[i].waveName);

                yield return StartCoroutine(SpawnWave(waves[i]));

                yield return new WaitForSeconds(timeBetweenWaves);
            }

        } while (loopWaves);

        currentWaveIndex = -1;
    }

    private IEnumerator SpawnWave(Wave wave)
    {
        if (wave == null)
            yield break;

        if (wave.enemies == null || wave.enemies.Count == 0)
        {
            Debug.LogWarning("WaveEnemySpawner: " + wave.waveName + " contains no enemies. Press RANDOMIZE ALL WAVES first.");
            yield break;
        }

        for (int i = 0; i < wave.enemies.Count; i++)
        {
            if (player == null)
            {
                FindPlayer();

                if (player == null)
                    yield break;
            }

            EnemyEntry entry = wave.enemies[i];

            if (entry != null && entry.prefab != null)
            {
                SpawnEnemy(entry);
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnEnemy(EnemyEntry entry)
    {
        Vector3 spawnPosition;

        if (!GetSpawnPosition(entry.enemyType, out spawnPosition))
        {
            Debug.LogWarning("WaveEnemySpawner: Could not find suitable surface position for " + entry.enemyType + " enemy.");
            return;
        }

        // 1. Instantiate enemy first
        GameObject enemy = Instantiate(entry.prefab, spawnPosition, Quaternion.identity);

        if (entry.enemyType == EnemyType.Flying)
        {
            if (spawnPortalPrefab != null)
            {
                Instantiate(spawnPortalPrefab, spawnPosition, Quaternion.identity);
            }
            return;
        }

        // 2. Align bottom of collider to ground/water surface
        Collider2D enemyCollider = enemy.GetComponentInChildren<Collider2D>();

        if (enemyCollider != null)
        {
            Physics2D.SyncTransforms();

            float lowestPoint = enemyCollider.bounds.min.y;
            float pivotY = enemy.transform.position.y;
            float bottomOffset = pivotY - lowestPoint;

            Vector3 adjustedPos = enemy.transform.position;
            adjustedPos.y = spawnPosition.y + bottomOffset + groundSpawnOffset;
            enemy.transform.position = adjustedPos;

            // 3. Anti-stuck routine: push upward if still overlapping terrain colliders
            LayerMask targetMask = (entry.enemyType == EnemyType.Ground) ? groundLayer : waterLayer;
            int maxUnstickAttempts = 10;
            float stepDistance = 0.2f;

            for (int i = 0; i < maxUnstickAttempts; i++)
            {
                Physics2D.SyncTransforms();

                Collider2D overlap = Physics2D.OverlapBox(
                    enemyCollider.bounds.center,
                    enemyCollider.bounds.size * 0.9f,
                    0f,
                    targetMask
                );

                if (overlap != null)
                {
                    enemy.transform.position += Vector3.up * stepDistance;
                }
                else
                {
                    break;
                }
            }
        }

        // 4. Spawn portal VFX at finalized enemy position
        if (spawnPortalPrefab != null)
        {
            Instantiate(spawnPortalPrefab, enemy.transform.position, Quaternion.identity);
        }
    }

    private bool GetSpawnPosition(EnemyType type, out Vector3 position)
    {
        position = Vector3.zero;

        if (player == null)
            return false;

        // Try up to 5 times with slightly varied horizontal distance if a bad spot (like water for ground unit) is hit
        for (int attempt = 0; attempt < 5; attempt++)
        {
            float distance = Random.Range(minimumSpawnDistance, maximumSpawnDistance);
            float direction = randomizeSpawnSide ? (Random.value < 0.5f ? -1f : 1f) : 1f;
            float x = player.position.x + (distance + attempt * 0.5f) * direction;

            // Flying enemies spawn in mid-air near player height
            if (type == EnemyType.Flying)
            {
                float y = player.position.y + Random.Range(minFlyingHeightOffset, maxFlyingHeightOffset);
                position = new Vector3(x, y, player.position.z);
                return true;
            }

            // Raycast down for Ground or Water
            float rayStartY = player.position.y + 15f;
            Vector2 rayStart = new Vector2(x, rayStartY);

            RaycastHit2D[] hits = Physics2D.RaycastAll(rayStart, Vector2.down, 40f);

            bool columnHasWater = false;
            float highestWaterY = float.MinValue;

            // First pass: Check if this column contains water
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null) continue;

                if (IsWater(hit.collider.gameObject))
                {
                    columnHasWater = true;
                    if (hit.point.y > highestWaterY)
                    {
                        highestWaterY = hit.point.y;
                    }
                }
            }

            // If spawning a Ground enemy in a column that has water above/on it, reject this position attempt
            if (type == EnemyType.Ground && columnHasWater)
            {
                continue;
            }

            // If spawning a Water enemy in a column with no water, reject this position attempt
            if (type == EnemyType.Water && !columnHasWater)
            {
                continue;
            }

            RaycastHit2D bestHit = default;
            bool foundTarget = false;
            float bestVerticalDistance = float.MaxValue;

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null)
                    continue;

                bool isValidSurface = (type == EnemyType.Ground)
                    ? IsGround(hit.collider.gameObject)
                    : IsWater(hit.collider.gameObject);

                if (!isValidSurface)
                    continue;

                float verticalDistance = Mathf.Abs(hit.point.y - player.position.y);

                if (!foundTarget || verticalDistance < bestVerticalDistance)
                {
                    bestHit = hit;
                    bestVerticalDistance = verticalDistance;
                    foundTarget = true;
                }
            }

            if (foundTarget)
            {
                // Ceiling check: raycast upwards from surface point to make sure there is no ceiling too close overhead
                RaycastHit2D ceilingHit = Physics2D.Raycast(bestHit.point + Vector2.up * 0.1f, Vector2.up, ceilingCheckDistance, groundLayer);
                if (ceilingHit.collider != null)
                {
                    continue; // Skip this position if a ceiling is detected close above
                }

                position = new Vector3(x, bestHit.point.y, player.position.z);
                return true;
            }
        }

        return false;
    }

    private bool IsGround(GameObject obj)
    {
        if (groundLayer.value != 0 && ((groundLayer.value & (1 << obj.layer)) != 0))
            return true;

        if (useGroundTag)
        {
            try
            {
                if (obj.CompareTag("Ground"))
                    return true;
            }
            catch { }
        }

        return false;
    }

    private bool IsWater(GameObject obj)
    {
        if (waterLayer.value != 0 && ((waterLayer.value & (1 << obj.layer)) != 0))
            return true;

        if (useWaterTag)
        {
            try
            {
                if (obj.CompareTag("Water"))
                    return true;
            }
            catch { }
        }

        return false;
    }

    public void RandomizeWaves()
    {
        if (enemyPool == null || enemyPool.Count == 0)
        {
            Debug.LogWarning("WaveEnemySpawner: Enemy Pool is empty.");
            return;
        }

        List<EnemyEntry> validEnemies = GetValidEnemies();

        if (validEnemies.Count == 0)
        {
            Debug.LogWarning("WaveEnemySpawner: No enemy prefabs are assigned.");
            return;
        }

        if (waves == null || waves.Count == 0)
        {
            Debug.LogWarning("WaveEnemySpawner: No waves have been added.");
            return;
        }

        foreach (Wave wave in waves)
        {
            if (wave == null)
                continue;

            wave.enemies.Clear();
            GenerateWave(wave, validEnemies);
        }

        Debug.Log("WaveEnemySpawner: Randomized " + waves.Count + " waves.");
    }

    private void GenerateWave(Wave wave, List<EnemyEntry> validEnemies)
    {
        int maximumDifficulty = Mathf.Max(1, wave.difficulty);
        List<EnemyEntry> availableEnemies = new List<EnemyEntry>();

        foreach (EnemyEntry enemy in validEnemies)
        {
            if (enemy.difficulty <= maximumDifficulty)
            {
                availableEnemies.Add(enemy);
            }
        }

        if (availableEnemies.Count == 0)
            return;

        int minimumCount = Mathf.Max(1, minimumEnemiesPerWave);
        int maximumCount = Mathf.Max(minimumCount, maximumEnemiesPerWave);

        float difficultyProgress = Mathf.InverseLerp(1f, 10f, maximumDifficulty);

        int waveMinimum = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(minimumCount, minimumCount + (maximumCount - minimumCount) * 0.6f, difficultyProgress)),
            1, maximumCount);

        int waveMaximum = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(minimumCount + (maximumCount - minimumCount) * 0.5f, maximumCount, difficultyProgress)),
            waveMinimum, maximumCount);

        int enemyCount = Random.Range(waveMinimum, waveMaximum + 1);

        for (int i = 0; i < enemyCount; i++)
        {
            EnemyEntry selectedEnemy = SelectRandomEnemy(availableEnemies, maximumDifficulty);

            if (selectedEnemy != null)
            {
                wave.enemies.Add(selectedEnemy);
            }
        }

        ShuffleWave(wave);
    }

    private EnemyEntry SelectRandomEnemy(List<EnemyEntry> availableEnemies, int maximumDifficulty)
    {
        if (availableEnemies.Count == 0)
            return null;

        float totalWeight = 0f;

        foreach (EnemyEntry enemy in availableEnemies)
        {
            int difficulty = Mathf.Max(1, enemy.difficulty);
            float difficultyMultiplier = 1f / Mathf.Pow(difficulty, difficultyRarity);
            float weight = Mathf.Max(1, enemy.spawnWeight) * difficultyMultiplier;

            if (difficulty == maximumDifficulty)
            {
                weight *= 1.5f;
            }

            totalWeight += weight;
        }

        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (EnemyEntry enemy in availableEnemies)
        {
            int difficulty = Mathf.Max(1, enemy.difficulty);
            float difficultyMultiplier = 1f / Mathf.Pow(difficulty, difficultyRarity);
            float weight = Mathf.Max(1, enemy.spawnWeight) * difficultyMultiplier;

            if (difficulty == maximumDifficulty)
            {
                weight *= 1.5f;
            }

            currentWeight += weight;

            if (randomValue <= currentWeight)
            {
                return enemy;
            }
        }

        return availableEnemies[Random.Range(0, availableEnemies.Count)];
    }

    private List<EnemyEntry> GetValidEnemies()
    {
        List<EnemyEntry> valid = new List<EnemyEntry>();

        foreach (EnemyEntry enemy in enemyPool)
        {
            if (enemy != null && enemy.prefab != null)
            {
                valid.Add(enemy);
            }
        }

        return valid;
    }

    private void ShuffleWave(Wave wave)
    {
        for (int i = wave.enemies.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            EnemyEntry temp = wave.enemies[i];
            wave.enemies[i] = wave.enemies[randomIndex];
            wave.enemies[randomIndex] = temp;
        }
    }

    public void StartWavesManually()
    {
        if (waveRoutine != null)
        {
            StopCoroutine(waveRoutine);
        }

        waveRoutine = StartCoroutine(StartWaves());
    }

    public void StopWaves()
    {
        if (waveRoutine != null)
        {
            StopCoroutine(waveRoutine);
            waveRoutine = null;
        }

        currentWaveIndex = -1;
    }

    public void SpawnCurrentWave()
    {
        if (currentWaveIndex >= 0 && currentWaveIndex < waves.Count)
        {
            StartCoroutine(SpawnWave(waves[currentWaveIndex]));
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSpawnRadius)
            return;

        if (player == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(player.position, minimumSpawnDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(player.position, maximumSpawnDistance);
    }
}

#if UNITY_EDITOR

[CustomEditor(typeof(WaveEnemySpawner))]
public class WaveEnemySpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "m_Script");

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(12);

        WaveEnemySpawner spawner = (WaveEnemySpawner)target;

        GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);

        if (GUILayout.Button("RANDOMIZE ALL WAVES", GUILayout.Height(40)))
        {
            Undo.RecordObject(spawner, "Randomize Enemy Waves");
            spawner.RandomizeWaves();
            EditorUtility.SetDirty(spawner);
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "Wave Difficulty controls the maximum enemy tier allowed. " +
            "Higher difficulty also allows larger waves. " +
            "Randomize to generate the composition.",
            MessageType.Info
        );
    }
}

#endif