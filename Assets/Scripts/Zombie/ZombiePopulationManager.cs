using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AI;
public class ZombiePopulationManager : MonoBehaviourPunCallbacks
{
    public static ZombiePopulationManager Instance { get; private set; }
    [Header("Zombie Prefabs")]
    public GameObject[] commonZombies;
    
    [Header("Rare Zombies (Parasite)")]
    public GameObject[] rareZombies;
    [Range(0f, 1f)]
    public float rareSpawnChance = 0.02f; // 2% chance per spawn cycle
    public float rareSpawnInterval = 60f; // Check every 60 seconds
    public int maxRareZombies = 2;
    [Header("Population Limits")]
    public int baseMaxZombies = 60;
    public int zombiesPerPlayer = 30;
    public int maxClusters = 5;
    public int maxStragglers = 2;
    
    public int maxTotalZombies => baseMaxZombies + (playerPositions.Count - 1) * zombiesPerPlayer;
    [Header("Cluster Settings (Tight Groups)")]
    public int minClusterSize = 20;
    public int maxClusterSize = 40;
    public float clusterRadius = 6f;
    [Header("Zombie Type Distribution")]
    [Range(0f, 1f)]
    public float walkerChance = 0.5f;
    [Range(0f, 1f)]
    public float sprinterChance = 0.05f;
    [Header("Spawn Distances")]
    public float minSpawnDistance = 25f;
    public float maxSpawnDistance = 50f;
    public float despawnDistance = 120f;
    public float minClusterSpacing = 20f;
    [Header("Timing")]
    public float initialGracePeriod = 5f;
    public float clusterSpawnInterval = 15f;
    public float stragglerSpawnInterval = 25f;
    public int TotalZombiesAlive { get; private set; }
    public bool IsReady { get; private set; }
    public bool SpawningPaused { get; private set; }
    private List<GameObject> allZombies = new List<GameObject>();
    private List<GameObject> rareZombiesList = new List<GameObject>();
    private List<ZombieCluster> clusters = new List<ZombieCluster>();
    private List<Transform> playerPositions = new List<Transform>();
    
    private float gameStartTime;
    private float lastClusterSpawn;
    private float lastStragglerSpawn;
    private float lastRareSpawnCheck;
    private bool prefabsLoaded = false;
    private int walkerCount, regularCount, sprinterCount;

    // Cached lists to avoid GC allocation
    private List<GameObject> despawnCache = new List<GameObject>();
    private float despawnDistanceSqr;

    private class ZombieCluster
    {
        public Vector3 position;
        public List<GameObject> zombies = new List<GameObject>();
    }

    bool CanSpawn => (!PhotonNetwork.IsConnected || (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)) 
                     && prefabsLoaded 
                     && Time.time > gameStartTime + initialGracePeriod 
                     && !SpawningPaused;
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        gameStartTime = Time.time;
        despawnDistanceSqr = despawnDistance * despawnDistance;
        LoadPrefabs();
        StartCoroutine(WaitAndStart());
    }

    void LoadPrefabs()
    {
        if (commonZombies == null || commonZombies.Length == 0)
        {
            GameObject zombiePrefab = Resources.Load<GameObject>("Zombie");
            if (zombiePrefab != null)
                commonZombies = new GameObject[] { zombiePrefab };
            else
            {
                Debug.LogError("[Population] No Zombie prefab found in Resources!");
                return;
            }
        }
        
        // Load rare zombies if not set
        if (rareZombies == null || rareZombies.Length == 0)
        {
            GameObject parasitePrefab = Resources.Load<GameObject>("ParasiteZombie");
            if (parasitePrefab != null)
                rareZombies = new GameObject[] { parasitePrefab };
        }
        
        prefabsLoaded = true;
        Debug.Log($"[Population] Loaded {commonZombies.Length} common, {(rareZombies?.Length ?? 0)} rare zombie prefabs");
    }

    IEnumerator WaitAndStart()
    {
        yield return new WaitForSeconds(2f);
        float timeout = 30f;
        while (PhotonNetwork.IsConnected && !PhotonNetwork.InRoom && timeout > 0)
        {
            yield return new WaitForSeconds(0.5f);
            timeout -= 0.5f;
        }
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
            yield break;
        IsReady = true;
        StartCoroutine(PlayerTrackingLoop());
        StartCoroutine(MainLoop());
        Debug.Log("[Population] Started spawning system");
    }

    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.IsMasterClient && !IsReady)
        {
            gameStartTime = Time.time;
            IsReady = true;
            StartCoroutine(PlayerTrackingLoop());
            StartCoroutine(MainLoop());
        }
    }

    IEnumerator PlayerTrackingLoop()
    {
        while (true)
        {
            playerPositions.Clear();
            foreach (var p in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
                if (!p.IsDead) playerPositions.Add(p.transform);
            
            if (playerPositions.Count == 0)
                foreach (var m in FindObjectsByType<ThirdPersonMotor>(FindObjectsSortMode.None))
                    playerPositions.Add(m.transform);
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator MainLoop()
    {
        yield return new WaitForSeconds(initialGracePeriod);
        while (true)
        {
            allZombies.RemoveAll(z => z == null);
            rareZombiesList.RemoveAll(z => z == null);
            TotalZombiesAlive = allZombies.Count;
            CleanupClusters();
            DespawnFarZombies();
            UpdateStats();
            Debug.Log($"[Population] Check: CanSpawn={CanSpawn}, Players={playerPositions.Count}, Zombies={TotalZombiesAlive}, Clusters={clusters.Count}, TimeSinceCluster={(Time.time - lastClusterSpawn):F0}s");
            if (CanSpawn && playerPositions.Count > 0)
            {
                // Spawn clusters (hordes) - they stand still
                if (clusters.Count < maxClusters && 
                    Time.time > lastClusterSpawn + clusterSpawnInterval &&
                    TotalZombiesAlive < maxTotalZombies - minClusterSize)
                {
                    SpawnCluster();
                    lastClusterSpawn = Time.time;
                }
                // Spawn stragglers (few wandering zombies)
                int currentStragglers = CountStragglers();
                if (currentStragglers < maxStragglers && 
                    Time.time > lastStragglerSpawn + stragglerSpawnInterval &&
                    TotalZombiesAlive < maxTotalZombies)
                {
                    SpawnStraggler();
                    lastStragglerSpawn = Time.time;
                }
                
                // Rare zombie spawn check (Parasite)
                if (rareZombies != null && rareZombies.Length > 0 &&
                    Time.time > lastRareSpawnCheck + rareSpawnInterval &&
                    rareZombiesList.Count < maxRareZombies)
                {
                    lastRareSpawnCheck = Time.time;
                    if (Random.value < rareSpawnChance)
                    {
                        SpawnRareZombie();
                    }
                }
            }
            yield return new WaitForSeconds(3f);
        }
    }

    ZombieAI.ZombieSpeed RollZombieType()
    {
        float roll = Random.value;
        if (roll < sprinterChance)
            return ZombieAI.ZombieSpeed.Sprinter;
        else if (roll < sprinterChance + walkerChance)
            return ZombieAI.ZombieSpeed.Walker;
        else
            return ZombieAI.ZombieSpeed.Regular;
    }

    void SpawnCluster()
    {
        Vector3 clusterPos = GetClusterSpawnPosition();
        if (clusterPos == Vector3.zero)
        {
            Debug.LogWarning("[Population] Failed to find valid cluster spawn position");
            return;
        }
        int size = Random.Range(minClusterSize, maxClusterSize + 1);
        int available = maxTotalZombies - TotalZombiesAlive;
        Debug.Log($"[Population] Horde size: rolled {size}, available space {available}, max total {maxTotalZombies}");
        size = Mathf.Min(size, maxTotalZombies - TotalZombiesAlive);
        ZombieCluster cluster = new ZombieCluster { position = clusterPos };
        clusters.Add(cluster);
        StartCoroutine(SpawnClusterZombies(cluster, size));
        Debug.Log($"[Population] Spawning HORDE of {size} zombies at {clusterPos}");
    }

    IEnumerator SpawnClusterZombies(ZombieCluster cluster, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // Tight penguin-like clustering
            Vector2 offset2D = Random.insideUnitCircle * clusterRadius;
            Vector3 spawnPos = cluster.position + new Vector3(offset2D.x, 0, offset2D.y);
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                spawnPos = hit.position;
            var zombieType = RollZombieType();
            // Horde zombies stand still (isStationary = true)
            GameObject zombie = SpawnZombieAt(spawnPos, cluster.position, false, zombieType, true);
            if (zombie != null)
                cluster.zombies.Add(zombie);
            yield return new WaitForSeconds(0.03f);
        }
    }

    void SpawnStraggler()
    {
        Vector3 pos = GetSpawnPosition();
        if (pos == Vector3.zero) return;
        var zombieType = Random.value < 0.7f ? ZombieAI.ZombieSpeed.Walker : RollZombieType();
        // Stragglers wander around
        SpawnZombieAt(pos, Vector3.zero, true, zombieType, false);
    }

    void SpawnRareZombie()
    {
        Vector3 pos = GetSpawnPosition();
        if (pos == Vector3.zero) return;
        
        if (rareZombies == null || rareZombies.Length == 0) return;
        
        GameObject prefab = rareZombies[Random.Range(0, rareZombies.Length)];
        if (prefab == null) return;
        GameObject zombie;
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
            zombie = PhotonNetwork.Instantiate(prefab.name, pos, Quaternion.Euler(0, Random.Range(0, 360), 0));
        else
            zombie = Instantiate(prefab, pos, Quaternion.Euler(0, Random.Range(0, 360), 0));
        if (zombie == null) return;
        rareZombiesList.Add(zombie);
        allZombies.Add(zombie);
        TotalZombiesAlive = allZombies.Count;
        var health = zombie.GetComponent<ZombieHealth>();
        if (health != null)
            health.OnDeath += () => { 
                allZombies.Remove(zombie); 
                rareZombiesList.Remove(zombie);
                TotalZombiesAlive = allZombies.Count; 
            };
        Debug.Log($"[Population] RARE ZOMBIE spawned! ({rareZombiesList.Count}/{maxRareZombies})");
    }

    GameObject SpawnZombieAt(Vector3 position, Vector3 homePosition, bool isWanderer, ZombieAI.ZombieSpeed speedType, bool isStationary)
    {
        if (commonZombies == null || commonZombies.Length == 0) return null;
        GameObject prefab = commonZombies[Random.Range(0, commonZombies.Length)];
        if (prefab == null) return null;
        GameObject zombie;
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
            zombie = PhotonNetwork.Instantiate(prefab.name, position, Quaternion.Euler(0, Random.Range(0, 360), 0));
        else
            zombie = Instantiate(prefab, position, Quaternion.Euler(0, Random.Range(0, 360), 0));
        if (zombie == null) return null;
        ZombieAI ai = zombie.GetComponent<ZombieAI>();
        if (ai != null)
        {
            ai.SetSpeedType(speedType);
            
            if (isStationary)
            {
                // Horde zombies stand still until they detect a player
                ai.SetStationary(homePosition);
            }
            else if (isWanderer)
            {
                ai.SetWanderer();
            }
            else
            {
                ai.SetHomePosition(homePosition);
            }
        }
        allZombies.Add(zombie);
        TotalZombiesAlive = allZombies.Count;
        var health = zombie.GetComponent<ZombieHealth>();
        if (health != null)
            health.OnDeath += () => { allZombies.Remove(zombie); TotalZombiesAlive = allZombies.Count; };
        return zombie;
    }

    Vector3 GetClusterSpawnPosition()
    {
        if (playerPositions.Count == 0) return Vector3.zero;
        Transform targetPlayer = GetPlayerWithFewestNearbyZombies();
        if (targetPlayer == null)
            targetPlayer = playerPositions[Random.Range(0, playerPositions.Count)];
        for (int attempt = 0; attempt < 25; attempt++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 pos = targetPlayer.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * dist;
            bool tooClose = false;
            foreach (var p in playerPositions)
            {
                if (p != null && Vector3.Distance(pos, p.position) < minSpawnDistance * 0.8f)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;
            bool tooCloseToCluster = false;
            foreach (var c in clusters)
            {
                if (Vector3.Distance(pos, c.position) < minClusterSpacing)
                {
                    tooCloseToCluster = true;
                    break;
                }
            }
            if (tooCloseToCluster) continue;
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f))
                pos = hit.point;
            if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, 15f, NavMesh.AllAreas))
                return navHit.position;
        }
        return Vector3.zero;
    }

    Transform GetPlayerWithFewestNearbyZombies()
    {
        if (playerPositions.Count == 0) return null;
        if (playerPositions.Count == 1) return playerPositions[0];
        
        Transform best = null;
        int fewestZombies = int.MaxValue;
        
        foreach (var player in playerPositions)
        {
            if (player == null) continue;
            
            int nearbyCount = 0;
            foreach (var zombie in allZombies)
            {
                if (zombie != null && Vector3.Distance(zombie.transform.position, player.position) < maxSpawnDistance)
                    nearbyCount++;
            }
            
            if (nearbyCount < fewestZombies)
            {
                fewestZombies = nearbyCount;
                best = player;
            }
        }
        
        return best;
    }

    Vector3 GetSpawnPosition()
    {
        if (playerPositions.Count == 0) return Vector3.zero;
        for (int i = 0; i < 15; i++)
        {
            Transform player = playerPositions[Random.Range(0, playerPositions.Count)];
            if (player == null) continue;
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 pos = player.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * dist;
            bool tooClose = false;
            foreach (var p in playerPositions)
            {
                if (p != null && Vector3.Distance(pos, p.position) < minSpawnDistance * 0.7f)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f))
                pos = hit.point;
            if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, 10f, NavMesh.AllAreas))
                return navHit.position;
        }
        return Vector3.zero;
    }

    void CleanupClusters()
    {
        foreach (var cluster in clusters)
            cluster.zombies.RemoveAll(z => z == null);
        clusters.RemoveAll(c => c.zombies.Count == 0);
    }

    int CountStragglers()
    {
        int clusterZombies = 0;
        foreach (var c in clusters)
            clusterZombies += c.zombies.Count;
        return TotalZombiesAlive - clusterZombies - rareZombiesList.Count;
    }

    void UpdateStats()
    {
        walkerCount = regularCount = sprinterCount = 0;
        foreach (var z in allZombies)
        {
            if (z == null) continue;
            var ai = z.GetComponent<ZombieAI>();
            if (ai == null) continue;
            
            switch (ai.SpeedType)
            {
                case ZombieAI.ZombieSpeed.Walker: walkerCount++; break;
                case ZombieAI.ZombieSpeed.Regular: regularCount++; break;
                case ZombieAI.ZombieSpeed.Sprinter: sprinterCount++; break;
            }
        }
    }

    void DespawnFarZombies()
    {
        if (playerPositions.Count == 0) return;

        // Reuse cached list to avoid GC allocation
        despawnCache.Clear();

        for (int i = 0; i < allZombies.Count; i++)
        {
            var z = allZombies[i];
            if (z == null) continue;

            bool farFromAll = true;
            Vector3 zPos = z.transform.position;

            for (int j = 0; j < playerPositions.Count; j++)
            {
                var p = playerPositions[j];
                if (p == null) continue;

                // Use squared distance to avoid sqrt
                float distSqr = (zPos - p.position).sqrMagnitude;
                if (distSqr <= despawnDistanceSqr)
                {
                    farFromAll = false;
                    break;
                }
            }
            if (farFromAll) despawnCache.Add(z);
        }

        for (int i = 0; i < despawnCache.Count; i++)
        {
            var z = despawnCache[i];
            allZombies.Remove(z);
            rareZombiesList.Remove(z);
            var pv = z.GetComponent<PhotonView>();
            if (PhotonNetwork.IsConnected && pv != null && pv.IsMine)
                PhotonNetwork.Destroy(z);
            else if (!PhotonNetwork.IsConnected)
                Destroy(z);
        }
        TotalZombiesAlive = allZombies.Count;
    }

    public void SetSpawningPaused(bool paused)
    {
        SpawningPaused = paused;
        Debug.Log($"[Population] Spawning {(paused ? "PAUSED" : "RESUMED")}");
    }

    // Compatibility stubs
    public void TriggerHorde() { }
    public void TriggerStealth() { }
    public enum PopulationMode { Normal, Horde, Aftermath }
    public PopulationMode CurrentMode => PopulationMode.Normal;
    public void ForceMode(PopulationMode mode) { }
    public event System.Action<PopulationMode> OnModeChanged;
    public event System.Action OnHordeStarted;
    public event System.Action OnHordeEnded;
    #if UNITY_EDITOR
    void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;
        GUILayout.BeginArea(new Rect(10, 220, 280, 200));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("<b>Zombie Population</b>");
        GUILayout.Label($"Total: {TotalZombiesAlive} / {maxTotalZombies}");
        GUILayout.Label($"Clusters: {clusters.Count} ({clusters.Sum(c => c.zombies.Count)} zombies)");
        GUILayout.Label($"Stragglers: {CountStragglers()} / {maxStragglers}");
        GUILayout.Label($"<color=red>RARE: {rareZombiesList.Count} / {maxRareZombies}</color>");
        GUILayout.Space(5);
        GUILayout.Label($"Walkers: {walkerCount}  Regular: {regularCount}  Sprinters: {sprinterCount}");
        GUILayout.Label($"Chasing: {ZombieAI.GetChasingCount()}");
        
        float graceRemaining = (gameStartTime + initialGracePeriod) - Time.time;
        if (graceRemaining > 0)
            GUILayout.Label($"Grace: {graceRemaining:F0}s");
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
#endif
}
