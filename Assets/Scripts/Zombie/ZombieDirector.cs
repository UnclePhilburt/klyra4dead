using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Photon.Pun;

public class ZombieDirector : MonoBehaviourPunCallbacks
{
    [Header("Multiplayer")]
    public string zombiePrefabName = "Zombie"; // Name in Resources folder
    [Header("Spawn Settings")]
    public GameObject zombiePrefab;
    public int maxZombies = 100;
    public float spawnInterval = 0.3f;    // Spawn check frequency
    public int spawnBatchSize = 10;       // How many to spawn per cycle
    public float minSpawnDistance = 10f;  // Min distance from any player
    public float maxSpawnDistance = 35f;  // Max distance from nearest player
    public float despawnDistance = 70f;   // Despawn if farther than this from ALL players

    [Header("Intensity")]
    [Range(0f, 1f)]
    public float baseIntensity = 0.5f;    // 0 = calm, 1 = intense
    public float intensityBuildRate = 0.1f;
    public float intensityDecayRate = 0.05f;
    public float calmPeriodDuration = 5f;     // Short breather
    public float intensePeriodDuration = 45f; // Long chaos

    [Header("Ambush")]
    public float ambushChance = 0.2f;     // Chance per spawn cycle to trigger ambush
    public int ambushSize = 15;           // How many zombies in an ambush
    public float ambushSpawnRadius = 12f; // Radius around ambush point

    [Header("Debug")]
    public bool showDebug = true;

    // Runtime
    private List<Transform> players = new List<Transform>();
    private List<GameObject> activeZombies = new List<GameObject>();
    private float nextSpawnTime;
    private float currentIntensity;
    private bool inCalmPeriod;
    private float periodEndTime;

    public static ZombieDirector Instance { get; private set; }

    // Public stats
    public int ActiveZombieCount => activeZombies.Count;
    public float CurrentIntensity => currentIntensity;
    public bool InCalmPeriod => inCalmPeriod;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        currentIntensity = baseIntensity;
        StartCalmPeriod();
    }

    void Update()
    {
        // Only master client controls spawning in multiplayer
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            return;

        RefreshPlayerList();
        CleanupDeadZombies();
        DespawnFarZombies();
        UpdateIntensity();

        // Spawning
        if (Time.time >= nextSpawnTime && !inCalmPeriod)
        {
            TrySpawnZombies();
            nextSpawnTime = Time.time + spawnInterval / currentIntensity;
        }

        // Period transitions
        if (Time.time >= periodEndTime)
        {
            if (inCalmPeriod)
                StartIntensePeriod();
            else
                StartCalmPeriod();
        }
    }

    void RefreshPlayerList()
    {
        players.Clear();

        // Find all players by ThirdPersonMotor
        ThirdPersonMotor[] motors = FindObjectsByType<ThirdPersonMotor>(FindObjectsSortMode.None);
        foreach (var motor in motors)
        {
            players.Add(motor.transform);
        }

        // Also find by PlayerHealth as backup
        PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var health in healths)
        {
            if (!health.IsDead && !players.Contains(health.transform))
            {
                players.Add(health.transform);
            }
        }
    }

    void CleanupDeadZombies()
    {
        activeZombies.RemoveAll(z => z == null);
    }

    void DespawnFarZombies()
    {
        if (players.Count == 0) return;

        for (int i = activeZombies.Count - 1; i >= 0; i--)
        {
            GameObject zombie = activeZombies[i];
            if (zombie == null) continue;

            // Check distance to nearest player
            float nearestDist = GetDistanceToNearestPlayer(zombie.transform.position);

            if (nearestDist > despawnDistance)
            {
                // Check if zombie is chasing - don't despawn active threats
                ZombieAI ai = zombie.GetComponent<ZombieAI>();
                if (ai != null && (ai.CurrentState == ZombieAI.ZombieState.Chasing ||
                                   ai.CurrentState == ZombieAI.ZombieState.Attacking))
                {
                    continue; // Don't despawn active zombies
                }

                if (showDebug) Debug.Log($"[Director] Despawning far zombie at distance {nearestDist:F0}");
                activeZombies.RemoveAt(i);

                if (PhotonNetwork.IsConnected)
                {
                    PhotonNetwork.Destroy(zombie);
                }
                else
                {
                    Destroy(zombie);
                }
            }
        }
    }

    void UpdateIntensity()
    {
        // Count nearby threats
        int nearbyThreats = 0;
        foreach (var zombie in activeZombies)
        {
            if (zombie == null) continue;
            ZombieAI ai = zombie.GetComponent<ZombieAI>();
            if (ai != null && (ai.CurrentState == ZombieAI.ZombieState.Chasing ||
                               ai.CurrentState == ZombieAI.ZombieState.Attacking))
            {
                nearbyThreats++;
            }
        }

        // Adjust intensity based on active threats
        float targetIntensity = baseIntensity + (nearbyThreats * 0.1f);
        targetIntensity = Mathf.Clamp01(targetIntensity);

        if (currentIntensity < targetIntensity)
            currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, intensityBuildRate * Time.deltaTime);
        else
            currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, intensityDecayRate * Time.deltaTime);
    }

    void TrySpawnZombies()
    {
        if (zombiePrefab == null)
        {
            Debug.LogWarning("[Director] No zombie prefab assigned!");
            return;
        }
        if (players.Count == 0)
        {
            Debug.LogWarning("[Director] No players found!");
            return;
        }
        if (activeZombies.Count >= maxZombies) return;

        // Check for ambush
        if (Random.value < ambushChance)
        {
            TriggerAmbush();
            return;
        }

        // Normal spawn - spawn a batch of zombies
        int toSpawn = Mathf.Min(spawnBatchSize, maxZombies - activeZombies.Count);
        int spawned = 0;

        for (int i = 0; i < toSpawn; i++)
        {
            Vector3? spawnPos = FindSpawnPosition();
            if (spawnPos.HasValue)
            {
                SpawnZombie(spawnPos.Value);
                spawned++;
            }
        }

        if (showDebug && spawned > 0)
        {
            Debug.Log($"[Director] Spawned batch of {spawned} zombies. Total: {activeZombies.Count}");
        }
    }

    Vector3? FindSpawnPosition()
    {
        // Try multiple times to find valid spot
        for (int attempt = 0; attempt < 10; attempt++)
        {
            // Pick random player to spawn near
            Transform targetPlayer = players[Random.Range(0, players.Count)];

            // Random angle around player
            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(minSpawnDistance, maxSpawnDistance);

            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
            Vector3 testPos = targetPlayer.position + offset;

            // Find valid NavMesh position
            NavMeshHit hit;
            if (NavMesh.SamplePosition(testPos, out hit, 5f, NavMesh.AllAreas))
            {
                Vector3 spawnPos = hit.position;

                // Check not too close to any player
                if (GetDistanceToNearestPlayer(spawnPos) < minSpawnDistance)
                    continue;

                // Check not visible to any player (simple line of sight)
                if (IsVisibleToAnyPlayer(spawnPos))
                    continue;

                return spawnPos;
            }
        }

        return null;
    }

    bool IsVisibleToAnyPlayer(Vector3 position)
    {
        foreach (Transform player in players)
        {
            Vector3 dirToSpawn = position - player.position;
            float dist = dirToSpawn.magnitude;

            // If close enough, do visibility check
            if (dist < maxSpawnDistance)
            {
                // Check if player is roughly facing this direction
                float angle = Vector3.Angle(player.forward, dirToSpawn);
                if (angle < 70f) // Within player's view cone
                {
                    // Raycast to check line of sight
                    if (!Physics.Raycast(player.position + Vector3.up * 1.5f, dirToSpawn.normalized, dist))
                    {
                        return true; // Clear line of sight - visible
                    }
                }
            }
        }
        return false;
    }

    float GetDistanceToNearestPlayer(Vector3 position)
    {
        float nearest = float.MaxValue;
        foreach (Transform player in players)
        {
            float dist = Vector3.Distance(position, player.position);
            if (dist < nearest) nearest = dist;
        }
        return nearest;
    }

    void SpawnZombie(Vector3 position)
    {
        GameObject zombie;
        Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        if (PhotonNetwork.IsConnected)
        {
            // Multiplayer - spawn networked zombie
            zombie = PhotonNetwork.Instantiate(zombiePrefabName, position, rotation);
        }
        else
        {
            // Single player - spawn local zombie
            zombie = Instantiate(zombiePrefab, position, rotation);
        }

        activeZombies.Add(zombie);
    }

    void TriggerAmbush()
    {
        if (players.Count == 0) return;

        // Pick random player
        Transform targetPlayer = players[Random.Range(0, players.Count)];

        // Find ambush position behind or to side of player
        float angle = Random.Range(90f, 270f); // Behind or to sides
        float distance = Random.Range(minSpawnDistance, minSpawnDistance + 10f);

        Vector3 offset = Quaternion.Euler(0, targetPlayer.eulerAngles.y + angle, 0) * Vector3.forward * distance;
        Vector3 ambushCenter = targetPlayer.position + offset;

        // Find valid NavMesh position
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(ambushCenter, out hit, 10f, NavMesh.AllAreas))
            return;

        ambushCenter = hit.position;

        if (showDebug) Debug.Log($"[Director] AMBUSH! Spawning {ambushSize} zombies!");

        // Spawn multiple zombies around ambush point
        int spawned = 0;
        for (int i = 0; i < ambushSize && activeZombies.Count < maxZombies; i++)
        {
            Vector3 spawnOffset = Random.insideUnitSphere * ambushSpawnRadius;
            spawnOffset.y = 0;

            if (NavMesh.SamplePosition(ambushCenter + spawnOffset, out hit, 3f, NavMesh.AllAreas))
            {
                SpawnZombie(hit.position);
                spawned++;
            }
        }

        // Bump intensity
        currentIntensity = Mathf.Min(1f, currentIntensity + 0.3f);
    }

    void StartCalmPeriod()
    {
        inCalmPeriod = true;
        periodEndTime = Time.time + calmPeriodDuration;
        if (showDebug) Debug.Log("[Director] Calm period started");
    }

    void StartIntensePeriod()
    {
        inCalmPeriod = false;
        periodEndTime = Time.time + intensePeriodDuration;
        if (showDebug) Debug.Log("[Director] Intense period started!");
    }

    // Public methods for external control
    public void ForceAmbush()
    {
        TriggerAmbush();
    }

    public void SetIntensity(float intensity)
    {
        currentIntensity = Mathf.Clamp01(intensity);
    }

    public void PausedSpawning(bool paused)
    {
        inCalmPeriod = paused;
        if (paused)
            periodEndTime = float.MaxValue;
    }

    void OnDrawGizmosSelected()
    {
        if (players == null) return;

        foreach (Transform player in players)
        {
            if (player == null) continue;

            // Min spawn distance
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(player.position, minSpawnDistance);

            // Max spawn distance
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.position, maxSpawnDistance);

            // Despawn distance
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(player.position, despawnDistance);
        }
    }
}
