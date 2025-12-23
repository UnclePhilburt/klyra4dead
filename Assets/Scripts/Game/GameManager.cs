using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance { get; private set; }

    [Header("Spawning")]
    public Transform[] playerSpawnPoints;
    public Transform[] zombieSpawnPoints;
    public GameObject[] zombiePrefabs;

    [Header("Wave Settings")]
    public int startingZombies = 5;
    public int zombiesPerWave = 5;
    public int zombiesPerPlayer = 3;
    public float timeBetweenWaves = 30f;
    public float spawnInterval = 1f;

    [Header("Game Settings")]
    public int maxWaves = 10; // 0 = infinite
    public float waveStartDelay = 5f;

    // State
    public int CurrentWave { get; private set; }
    public int ZombiesAlive { get; private set; }
    public int ZombiesKilled { get; private set; }
    public int TotalZombiesThisWave { get; private set; }
    public bool IsWaveActive { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsVictory { get; private set; }

    // Events
    public event System.Action<int> OnWaveStart;
    public event System.Action<int> OnWaveComplete;
    public event System.Action OnGameOver;
    public event System.Action OnVictory;
    public event System.Action<int, int> OnZombieCountChanged;

    private List<GameObject> activeZombies = new List<GameObject>();
    private int zombiesToSpawn;
    private bool isSpawning;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(StartFirstWave());
        }
    }

    IEnumerator StartFirstWave()
    {
        yield return new WaitForSeconds(waveStartDelay);
        StartWave();
    }

    [PunRPC]
    void RPC_StartWave(int wave, int zombieCount)
    {
        CurrentWave = wave;
        TotalZombiesThisWave = zombieCount;
        ZombiesAlive = 0;
        IsWaveActive = true;

        OnWaveStart?.Invoke(CurrentWave);
        Debug.Log($"[GameManager] Wave {CurrentWave} started! Zombies: {zombieCount}");
    }

    void StartWave()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        CurrentWave++;
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
        int zombieCount = startingZombies + (zombiesPerWave * (CurrentWave - 1)) + (zombiesPerPlayer * playerCount);

        TotalZombiesThisWave = zombieCount;
        zombiesToSpawn = zombieCount;
        ZombiesAlive = 0;
        IsWaveActive = true;

        // Sync to all clients
        photonView.RPC("RPC_StartWave", RpcTarget.All, CurrentWave, zombieCount);

        // Start spawning
        StartCoroutine(SpawnZombies());
    }

    IEnumerator SpawnZombies()
    {
        isSpawning = true;

        while (zombiesToSpawn > 0)
        {
            SpawnZombie();
            zombiesToSpawn--;
            yield return new WaitForSeconds(spawnInterval);
        }

        isSpawning = false;
    }

    void SpawnZombie()
    {
        if (zombieSpawnPoints == null || zombieSpawnPoints.Length == 0)
        {
            Debug.LogWarning("[GameManager] No zombie spawn points set!");
            return;
        }

        if (zombiePrefabs == null || zombiePrefabs.Length == 0)
        {
            Debug.LogWarning("[GameManager] No zombie prefabs set!");
            return;
        }

        // Random spawn point
        Transform spawnPoint = zombieSpawnPoints[Random.Range(0, zombieSpawnPoints.Length)];

        // Random zombie type
        GameObject zombiePrefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];

        // Spawn with offset
        Vector3 spawnPos = spawnPoint.position + new Vector3(
            Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));

        GameObject zombie = PhotonNetwork.Instantiate("Zombies/" + zombiePrefab.name,
            spawnPos, spawnPoint.rotation);

        ZombiesAlive++;
        photonView.RPC("RPC_UpdateZombieCount", RpcTarget.All, ZombiesAlive, ZombiesKilled);

        // Subscribe to death event
        ZombieHealth health = zombie.GetComponent<ZombieHealth>();
        if (health != null)
        {
            health.OnDeath += () => OnZombieDied(zombie);
        }

        activeZombies.Add(zombie);
    }

    void OnZombieDied(GameObject zombie)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        activeZombies.Remove(zombie);
        ZombiesAlive--;
        ZombiesKilled++;

        photonView.RPC("RPC_UpdateZombieCount", RpcTarget.All, ZombiesAlive, ZombiesKilled);

        // Check wave complete
        if (ZombiesAlive <= 0 && zombiesToSpawn <= 0 && !isSpawning)
        {
            WaveComplete();
        }
    }

    [PunRPC]
    void RPC_UpdateZombieCount(int alive, int killed)
    {
        ZombiesAlive = alive;
        ZombiesKilled = killed;
        OnZombieCountChanged?.Invoke(ZombiesAlive, ZombiesKilled);
    }

    void WaveComplete()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        IsWaveActive = false;
        photonView.RPC("RPC_WaveComplete", RpcTarget.All, CurrentWave);

        // Check victory
        if (maxWaves > 0 && CurrentWave >= maxWaves)
        {
            Victory();
            return;
        }

        // Start next wave after delay
        StartCoroutine(StartNextWaveAfterDelay());
    }

    [PunRPC]
    void RPC_WaveComplete(int wave)
    {
        IsWaveActive = false;
        OnWaveComplete?.Invoke(wave);
        Debug.Log($"[GameManager] Wave {wave} complete!");
    }

    IEnumerator StartNextWaveAfterDelay()
    {
        yield return new WaitForSeconds(timeBetweenWaves);
        StartWave();
    }

    void Victory()
    {
        IsVictory = true;
        IsGameOver = true;
        photonView.RPC("RPC_Victory", RpcTarget.All);
    }

    [PunRPC]
    void RPC_Victory()
    {
        IsVictory = true;
        IsGameOver = true;
        OnVictory?.Invoke();
        Debug.Log("[GameManager] Victory! All waves completed!");
    }

    public void CheckGameOver()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Check if all players are dead
        PlayerHealth[] players = FindObjectsOfType<PlayerHealth>();
        bool allDead = true;

        foreach (PlayerHealth player in players)
        {
            if (!player.IsDead)
            {
                allDead = false;
                break;
            }
        }

        if (allDead)
        {
            GameOver();
        }
    }

    void GameOver()
    {
        IsGameOver = true;
        photonView.RPC("RPC_GameOver", RpcTarget.All);
    }

    [PunRPC]
    void RPC_GameOver()
    {
        IsGameOver = true;
        OnGameOver?.Invoke();
        Debug.Log("[GameManager] Game Over! All players died.");
    }

    public void RestartGame()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Reset state
        CurrentWave = 0;
        ZombiesAlive = 0;
        ZombiesKilled = 0;
        IsGameOver = false;
        IsVictory = false;

        // Destroy all zombies
        foreach (GameObject zombie in activeZombies)
        {
            if (zombie != null)
            {
                PhotonNetwork.Destroy(zombie);
            }
        }
        activeZombies.Clear();

        // Reload scene
        PhotonNetwork.LoadLevel(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    public void ReturnToMenu()
    {
        PhotonNetwork.LeaveRoom();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
