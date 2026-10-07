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

    // A stretch of the level with its own difficulty. Checked top to bottom; the first one containing Rowdy wins.
    [System.Serializable]
    public class Zone
    {
        public string name = "Zone";
        public float fromX = -100000f, toX = 100000f;
        public float fromY = -100000f, toY = 100000f;
        [Tooltip("Hardest enemy tier that can show up here (matches the pool's Difficulty).")]
        [Range(1, 10)] public int difficulty = 1;
        [Tooltip("x the wave size.")]
        public float countMultiplier = 1f;

        public Zone() { }
        public Zone(string name, float fromX, float toX, float fromY, float toY, int difficulty, float countMultiplier)
        {
            this.name = name; this.fromX = fromX; this.toX = toX; this.fromY = fromY; this.toY = toY;
            this.difficulty = difficulty; this.countMultiplier = countMultiplier;
        }

        public bool Contains(Vector2 p) => p.x >= fromX && p.x < toX && p.y >= fromY && p.y < toY;
    }

    [Header("Difficulty Zones")]
    [Tooltip("On: each wave is built when it starts, from the enemy pool, for the zone Rowdy is in (harder zones = tougher enemies and more of them). Off: the pre-made waves below are used as before.")]
    [SerializeField] private bool useZones = true;
    [Tooltip("Guessed borders - the Console logs 'Spawn zone: ...' at every wave, so walk around and adjust.")]
    [SerializeField] private List<Zone> zones = new List<Zone>
    {
        new Zone("Gloomy Forest (up top)", 5f, 60f, 7f, 100000f, 9, 1.3f),
        new Zone("Pelich", 205f, 100000f, -100000f, 100000f, 10, 1.4f),
        new Zone("Before Pelich", 160f, 205f, -100000f, 100000f, 6, 1.1f),
        new Zone("Jungle", 104f, 160f, -100000f, 100000f, 4, 1f),
        new Zone("Beach", -100000f, 104f, -100000f, 100000f, 2, 0.7f),
    };

    [Header("Ranged Enemies (Gnoll Archer / Bomber)")]
    [Tooltip("They don't move, so they only spawn on flat ground with a clear shot at Rowdy, facing him.")]
    [SerializeField] private bool smartRangedPlacement = true;
    [SerializeField] private float rangedMinDistance = 5f;
    [SerializeField] private float rangedMaxDistance = 11f;

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

    [Header("Cleanup (enemies left behind)")]
    [Tooltip("Spawned enemies further than this from Rowdy (and off screen) for a while are removed, so ones you run away from don't pile up.")]
    [SerializeField] private float despawnDistance = 30f;
    [Tooltip("Seconds an enemy has to stay that far away before it's removed.")]
    [SerializeField] private float despawnDelay = 4f;
    [Tooltip("Most spawned enemies alive at once. Spawning pauses while at the limit (0 = no limit).")]
    [SerializeField] private int maxAliveEnemies = 25;
    [Tooltip("Dead bodies of spawned enemies are cleaned up this long after dying.")]
    [SerializeField] private float corpseLifetime = 12f;

    [Header("Debug")]
    [SerializeField] private bool drawSpawnRadius = true;

    private class SpawnedEnemy
    {
        public GameObject root;
        public EnemyHealth health;
        public Renderer renderer;
        public float farTime;
        public float deadTime = -1f;
    }

    private Coroutine waveRoutine;
    private int currentWaveIndex = -1;
    private readonly List<SpawnedEnemy> spawned = new List<SpawnedEnemy>();
    private float cleanupTimer;

    private void Start()
    {
        FindPlayer();

        if (waves != null && waves.Count > 0)
        {
            waveRoutine = StartCoroutine(StartWaves());
        }
    }

    private int AliveCount
    {
        get
        {
            int alive = 0;
            foreach (SpawnedEnemy e in spawned) if (e.root != null && (e.health == null || !e.health.enemydead)) alive++;
            return alive;
        }
    }

    // Removes spawned enemies Rowdy left far behind (off screen), and old corpses. Checked a few times a second.
    private void Update()
    {
        cleanupTimer -= Time.deltaTime;
        if (cleanupTimer > 0f) return;
        float step = 0.25f - cleanupTimer;
        cleanupTimer = 0.25f;

        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            SpawnedEnemy e = spawned[i];
            if (e.root == null) { spawned.RemoveAt(i); continue; }

            bool dead = e.health != null && e.health.enemydead;
            if (dead)
            {
                if (e.deadTime < 0f) e.deadTime = Time.time;
                if (corpseLifetime > 0f && Time.time - e.deadTime > corpseLifetime && !IsVisible(e)) Despawn(i);
                continue;
            }

            if (player == null || despawnDistance <= 0f) continue;
            Vector3 position = e.health != null ? e.health.transform.position : e.root.transform.position;
            bool far = Vector2.Distance(position, player.position) > despawnDistance && !IsVisible(e);
            e.farTime = far ? e.farTime + step : 0f;
            if (e.farTime >= despawnDelay) Despawn(i);
        }
    }

    private static bool IsVisible(SpawnedEnemy e) => e.renderer != null && e.renderer.isVisible;

    private void Despawn(int index)
    {
        if (spawned[index].root != null) Destroy(spawned[index].root);
        spawned.RemoveAt(index);
    }

    private void Track(GameObject enemy)
    {
        var entry = new SpawnedEnemy { root = enemy, health = enemy.GetComponentInChildren<EnemyHealth>(true) };
        entry.renderer = entry.health != null ? entry.health.GetComponent<Renderer>() : null;
        if (entry.renderer == null) entry.renderer = enemy.GetComponentInChildren<SpriteRenderer>();
        spawned.Add(entry);
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

        // Zones: build this wave now, for wherever Rowdy is
        List<EnemyEntry> list = wave.enemies;
        if (useZones && player != null)
        {
            Zone zone = CurrentZone();
            if (zone != null)
            {
                list = BuildZoneWave(zone);
                Debug.Log($"Spawn zone: {zone.name} (difficulty {zone.difficulty}, {list.Count} enemies) at x {player.position.x:F0}, y {player.position.y:F0}");
            }
        }

        if (list == null || list.Count == 0)
        {
            Debug.LogWarning("WaveEnemySpawner: " + wave.waveName + " contains no enemies. Press RANDOMIZE ALL WAVES first.");
            yield break;
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (player == null)
            {
                FindPlayer();

                if (player == null)
                    yield break;
            }

            EnemyEntry entry = list[i];

            // At the limit: wait for some to die or be cleaned up before spawning more
            while (maxAliveEnemies > 0 && AliveCount >= maxAliveEnemies)
            {
                yield return new WaitForSeconds(0.5f);
            }

            if (entry != null && entry.prefab != null)
            {
                SpawnEnemy(entry);
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private Zone CurrentZone()
    {
        if (player == null || zones == null) return null;
        foreach (Zone zone in zones) if (zone != null && zone.Contains(player.position)) return zone;
        return null;
    }

    // Enemies allowed in the zone (pool Difficulty <= zone difficulty), favouring the ones near the zone's tier,
    // and a wave size that grows with the difficulty
    private List<EnemyEntry> BuildZoneWave(Zone zone)
    {
        var result = new List<EnemyEntry>();
        var allowed = new List<EnemyEntry>();
        foreach (EnemyEntry e in GetValidEnemies()) if (e.difficulty <= zone.difficulty) allowed.Add(e);
        if (allowed.Count == 0) return result;

        float progress = Mathf.InverseLerp(1f, 10f, zone.difficulty);
        int min = Mathf.Max(1, minimumEnemiesPerWave), max = Mathf.Max(min, maximumEnemiesPerWave);
        int count = Mathf.RoundToInt(Mathf.Lerp(min, max, progress * 0.8f) * Random.Range(0.85f, 1.15f) * Mathf.Max(0.1f, zone.countMultiplier));
        count = Mathf.Clamp(count, 1, max * 2);

        float total = 0f;
        var weights = new float[allowed.Count];
        for (int i = 0; i < allowed.Count; i++)
        {
            // close to the zone's tier = common; much easier ones still show up as fodder
            float gap = zone.difficulty - allowed[i].difficulty;
            weights[i] = Mathf.Max(1, allowed[i].spawnWeight) * Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(gap / 6f));
            total += weights[i];
        }
        for (int n = 0; n < count; n++)
        {
            float pick = Random.Range(0f, total);
            for (int i = 0; i < allowed.Count; i++)
            {
                pick -= weights[i];
                if (pick <= 0f || i == allowed.Count - 1) { result.Add(allowed[i]); break; }
            }
        }
        return result;
    }

    private static bool IsRanged(GameObject prefab)
    {
        EnemyCatalog.Entry entry = prefab != null ? EnemyCatalog.Match(prefab.name) : null;
        return entry != null && (entry.id == "gnollarcher" || entry.id == "gnollbomber");
    }

    private void SpawnEnemy(EnemyEntry entry)
    {
        Vector3 spawnPosition;
        bool ranged = smartRangedPlacement && entry.enemyType == EnemyType.Ground && IsRanged(entry.prefab);

        bool found = ranged ? GetRangedSpawnPosition(out spawnPosition) : GetSpawnPosition(entry.enemyType, out spawnPosition);
        if (!found)
        {
            Debug.LogWarning("WaveEnemySpawner: Could not find suitable surface position for " + entry.enemyType + " enemy.");
            return;
        }

        // 1. Instantiate enemy first
        GameObject enemy = Instantiate(entry.prefab, spawnPosition, Quaternion.identity);
        Track(enemy);

        // Archers / bombers stand still: turn them towards Rowdy (the art faces left at +x scale)
        if (ranged && player != null)
        {
            Vector3 s = enemy.transform.localScale;
            s.x = Mathf.Abs(s.x) * (player.position.x > spawnPosition.x ? -1f : 1f);
            enemy.transform.localScale = s;
        }

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

        // Pick a starting side; with randomized sides, alternate sides on each retry so a bad spot
        // on one side (e.g. water under a ground enemy) doesn't make every attempt fail
        float startDirection = randomizeSpawnSide ? (Random.value < 0.5f ? -1f : 1f) : 1f;

        // Try up to 8 times with slightly varied horizontal distance if a bad spot (like water for ground unit) is hit
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float distance = Random.Range(minimumSpawnDistance, maximumSpawnDistance);
            float direction = (randomizeSpawnSide && attempt % 2 == 1) ? -startDirection : startDirection;
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

            RaycastHit2D[] hits = SolidHits(rayStart, 40f);

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

            // Sea creatures only in open water: the water has to be the first thing from the sky down (not a pool
            // sealed under the ground), with air above it and some room to swim
            if (type == EnemyType.Water)
            {
                if (TryOpenWater(x, rayStart.y, out Vector2 surface))
                {
                    position = new Vector3(x, surface.y, player.position.z);
                    return true;
                }
                continue;
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

    // Down-ray hits on solid colliders only (triggers like attack boxes / pickups don't count), nearest first
    private static RaycastHit2D[] SolidHits(Vector2 from, float distance)
    {
        RaycastHit2D[] all = Physics2D.RaycastAll(from, Vector2.down, distance);
        var solid = new List<RaycastHit2D>(all.Length);
        foreach (RaycastHit2D h in all) if (h.collider != null && !h.collider.isTrigger) solid.Add(h);
        return solid.ToArray();
    }

    // First surface (ground or water) straight down from the top of the column
    private bool FirstSurface(float x, float fromY, out RaycastHit2D surface)
    {
        foreach (RaycastHit2D h in SolidHits(new Vector2(x, fromY), 40f))
        {
            if (IsGround(h.collider.gameObject) || IsWater(h.collider.gameObject)) { surface = h; return true; }
        }
        surface = default;
        return false;
    }

    private bool TryOpenWater(float x, float fromY, out Vector2 surface)
    {
        surface = default;
        if (!FirstSurface(x, fromY, out RaycastHit2D hit) || !IsWater(hit.collider.gameObject)) return false;
        if (Mathf.Abs(hit.point.y - player.position.y) > 8f) return false; // not some sea far below / above Rowdy

        // Room to swim: the water is also the first surface a bit to each side, at about the same height
        foreach (float side in new[] { -1.4f, 1.4f })
        {
            if (!FirstSurface(x + side, fromY, out RaycastHit2D near) || !IsWater(near.collider.gameObject)) return false;
            if (Mathf.Abs(near.point.y - hit.point.y) > 0.4f) return false;
        }

        // Air above the surface (no ground lid right on top of it)
        Collider2D lid = Physics2D.OverlapBox(hit.point + Vector2.up * 1f, new Vector2(2f, 1.4f), 0f, groundLayer);
        if (lid != null && !lid.isTrigger) return false;

        surface = hit.point;
        return true;
    }

    // Archers / bombers: flat ground, a clear line to Rowdy, not right in front of a wall or slope, at shooting range
    private bool GetRangedSpawnPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (player == null) return false;

        for (int attempt = 0; attempt < 16; attempt++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = player.position.x + side * Random.Range(rangedMinDistance, rangedMaxDistance);
            if (!FirstSurfaceNear(x, out RaycastHit2D ground)) continue;
            if (!IsGround(ground.collider.gameObject) || ground.normal.y < 0.95f) continue; // flat only

            // Flat for a body width either side (no slope / step right next to it)
            bool flat = true;
            foreach (float dx in new[] { -0.5f, 0.5f })
            {
                if (!FirstSurfaceNear(x + dx, out RaycastHit2D h) || Mathf.Abs(h.point.y - ground.point.y) > 0.12f) { flat = false; break; }
            }
            if (!flat) continue;
            if (Mathf.Abs(ground.point.y - player.position.y) > 3f) continue; // roughly Rowdy's level

            // Clear shot from chest height to Rowdy, and no wall within a few steps in the facing direction
            Vector2 chest = ground.point + Vector2.up * 0.6f;
            Vector2 target = (Vector2)player.position + Vector2.up * 0.3f;
            RaycastHit2D block = Physics2D.Linecast(chest, target, groundLayer);
            if (block.collider != null && !block.collider.isTrigger) continue;
            float facing = Mathf.Sign(player.position.x - x);
            RaycastHit2D wall = Physics2D.Raycast(chest, new Vector2(facing, 0f), 3f, groundLayer);
            if (wall.collider != null && !wall.collider.isTrigger) continue;
            if (Physics2D.Raycast(ground.point + Vector2.up * 0.1f, Vector2.up, ceilingCheckDistance, groundLayer).collider != null) continue;

            position = new Vector3(x, ground.point.y, player.position.z);
            return true;
        }

        // Nothing good nearby: a normal ground spot is better than no enemy
        return GetSpawnPosition(EnemyType.Ground, out position);
    }

    // The surface closest to Rowdy's height in this column (like GetSpawnPosition), ground or water
    private bool FirstSurfaceNear(float x, out RaycastHit2D best)
    {
        best = default;
        bool found = false;
        float bestDy = float.MaxValue;
        foreach (RaycastHit2D h in SolidHits(new Vector2(x, player.position.y + 6f), 14f))
        {
            if (!IsGround(h.collider.gameObject) && !IsWater(h.collider.gameObject)) continue;
            float dy = Mathf.Abs(h.point.y - player.position.y);
            if (dy < bestDy) { bestDy = dy; best = h; found = true; }
        }
        return found;
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