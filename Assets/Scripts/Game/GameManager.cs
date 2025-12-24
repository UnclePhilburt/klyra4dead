using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Game Manager - Handles game state, waves (optional), and victory/defeat conditions.
/// Can work alongside ZombiePopulationManager for dynamic spawning, or use traditional waves.
/// </summary>
public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance { get; private set; }

    [Header("Game Mode")]
    [Tooltip("Use ZombiePopulationManager for dynamic spawning instead of waves")]
    public bool useDynamicPopulation = true;
    [Tooltip("If using waves, these settings apply")]
    public bool useTraditionalWaves = false;

    [Header("Wave Settings (if using traditional waves)")]
    public Transform[] zombieSpawnPoints;
    public GameObject[] zombiePrefabs;
    public int baseZombiesPerWave = 5;
    public int zombiesPerWave = 3;
    public float timeBetweenWaves = 30f;
    public int maxWaves = 0;

    [Header("Victory Conditions")]
    public bool survivalMode = true; // No victory, just survive
    public int wavesToWin = 10; // Only if !survivalMode && useTraditionalWaves

    [Header("References")]
    public AIDirector director;
    public ZombiePopulationManager populationManager;

    // State
    public int CurrentWave { get; private set; }
    public int ZombiesKilled { get; private set; }
    public int TotalZombiesKilled { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsVictory { get; private set; }
    public float SurvivalTime { get; private set; }

    // Events
    public event System.Action<int> OnWaveStart;
    public event System.Action<int> OnWaveComplete;
    public event System.Action OnGameOver;
    public event System.Action OnVictory;
    public event System.Action<int> OnZombieKilled;

    private bool initialized = false;

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
        StartCoroutine(Initialize());
    }

    IEnumerator Initialize()
    {
        yield return new WaitForSeconds(0.5f);

        // Find or create AI Director
        director = AIDirector.Instance;
        if (director == null)
        {
            GameObject dirObj = new GameObject("AIDirector");
            director = dirObj.AddComponent<AIDirector>();
        }

        // Find or create Population Manager if using dynamic mode
        if (useDynamicPopulation)
        {
            populationManager = ZombiePopulationManager.Instance;
            if (populationManager == null)
            {
                GameObject popObj = new GameObject("ZombiePopulationManager");
                populationManager = popObj.AddComponent<ZombiePopulationManager>();
                
                // Copy zombie prefabs
                if (zombiePrefabs != null && zombiePrefabs.Length > 0)
                {
                    populationManager.commonZombies = zombiePrefabs;
                }
            }

            // Subscribe to population manager events
            populationManager.OnHordeStarted += () => Debug.Log("[GameManager] HORDE STARTED!");
            populationManager.OnHordeEnded += () => Debug.Log("[GameManager] Horde ended, catching breath...");
        }

        // Start traditional waves if enabled
        if (useTraditionalWaves && !useDynamicPopulation)
        {
            if (PhotonNetwork.IsMasterClient || !PhotonNetwork.IsConnected)
            {
                StartCoroutine(WaveLoop());
            }
        }

        // Subscribe to zombie deaths for tracking
        ZombieHealth.OnAnyZombieDeath += HandleZombieDeath;

        initialized = true;
        Debug.Log($"[GameManager] Initialized. Dynamic: {useDynamicPopulation}, Waves: {useTraditionalWaves}");
    }

    void OnDestroy()
    {
        ZombieHealth.OnAnyZombieDeath -= HandleZombieDeath;
    }

    void Update()
    {
        if (!initialized || IsGameOver) return;

        SurvivalTime += Time.deltaTime;

        // Check for game over
        if (Time.frameCount % 60 == 0) // Check every second
        {
            CheckGameOver();
        }
    }

    void HandleZombieDeath()
    {
        ZombiesKilled++;
        TotalZombiesKilled++;
        OnZombieKilled?.Invoke(TotalZombiesKilled);
    }

    #region Traditional Wave System

    IEnumerator WaveLoop()
    {
        yield return new WaitForSeconds(5f); // Initial delay

        while (!IsGameOver)
        {
            // Wait for director to say it's a good time
            while (director != null && !director.IsGoodTimeForWave())
            {
                yield return new WaitForSeconds(1f);
            }

            StartWave();

            // Wait for wave to complete
            yield return new WaitUntil(() => GetAliveZombieCount() == 0 || IsGameOver);

            if (IsGameOver) break;

            WaveComplete();

            // Check victory
            if (!survivalMode && CurrentWave >= wavesToWin)
            {
                Victory();
                break;
            }

            // Cooldown between waves
            float cooldown = director != null ? director.GetWaveCooldown(timeBetweenWaves) : timeBetweenWaves;
            yield return new WaitForSeconds(cooldown);
        }
    }

    void StartWave()
    {
        CurrentWave++;
        ZombiesKilled = 0;

        int playerCount = PhotonNetwork.IsConnected ? PhotonNetwork.CurrentRoom.PlayerCount : 1;
        int zombieCount = baseZombiesPerWave + (zombiesPerWave * (CurrentWave - 1)) + (playerCount * 2);

        // Apply director modifier
        if (director != null)
        {
            zombieCount = Mathf.RoundToInt(zombieCount * director.GetZombieCountMultiplier());
        }

        Debug.Log($"[GameManager] Wave {CurrentWave}: {zombieCount} zombies");

        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_WaveStart", RpcTarget.All, CurrentWave, zombieCount);
        }
        else
        {
            OnWaveStart?.Invoke(CurrentWave);
        }

        StartCoroutine(SpawnWaveZombies(zombieCount));
    }

    IEnumerator SpawnWaveZombies(int count)
    {
        float interval = 0.5f;
        if (director != null)
        {
            interval = 0.5f / director.GetSpawnRateMultiplier();
        }

        for (int i = 0; i < count; i++)
        {
            SpawnZombie();
            yield return new WaitForSeconds(interval);
        }
    }

    void SpawnZombie()
    {
        if (zombiePrefabs == null || zombiePrefabs.Length == 0) return;
        if (zombieSpawnPoints == null || zombieSpawnPoints.Length == 0) return;

        Transform sp = zombieSpawnPoints[Random.Range(0, zombieSpawnPoints.Length)];
        GameObject prefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];

        Vector3 pos = sp.position + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Instantiate(prefab.name, pos, sp.rotation);
        }
        else
        {
            Instantiate(prefab, pos, sp.rotation);
        }
    }

    void WaveComplete()
    {
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_WaveComplete", RpcTarget.All, CurrentWave);
        }
        else
        {
            OnWaveComplete?.Invoke(CurrentWave);
        }

        Debug.Log($"[GameManager] Wave {CurrentWave} complete!");
    }

    [PunRPC]
    void RPC_WaveStart(int wave, int zombies)
    {
        CurrentWave = wave;
        OnWaveStart?.Invoke(wave);
    }

    [PunRPC]
    void RPC_WaveComplete(int wave)
    {
        OnWaveComplete?.Invoke(wave);
    }

    #endregion

    #region Game State

    int GetAliveZombieCount()
    {
        if (populationManager != null)
        {
            return populationManager.TotalZombiesAlive;
        }

        return FindObjectsByType<ZombieHealth>(FindObjectsSortMode.None)
            .Count(z => !z.IsDead);
    }

    public void CheckGameOver()
    {
        if (IsGameOver) return;
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        
        if (players.Length == 0) return;

        bool allDead = players.All(p => p.IsDead);

        if (allDead)
        {
            GameOver();
        }
    }

    void GameOver()
    {
        IsGameOver = true;

        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_GameOver", RpcTarget.All);
        }
        else
        {
            OnGameOver?.Invoke();
        }

        Debug.Log($"[GameManager] GAME OVER! Survived {SurvivalTime:F0}s, Killed {TotalZombiesKilled} zombies");
    }

    void Victory()
    {
        IsVictory = true;
        IsGameOver = true;

        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("RPC_Victory", RpcTarget.All);
        }
        else
        {
            OnVictory?.Invoke();
        }

        Debug.Log("[GameManager] VICTORY!");
    }

    [PunRPC]
    void RPC_GameOver()
    {
        IsGameOver = true;
        OnGameOver?.Invoke();
    }

    [PunRPC]
    void RPC_Victory()
    {
        IsVictory = true;
        IsGameOver = true;
        OnVictory?.Invoke();
    }

    #endregion

    #region Public Methods

    public void TriggerHorde()
    {
        if (director != null)
        {
            director.TriggerHorde();
        }
        else if (populationManager != null)
        {
            populationManager.TriggerHorde();
        }
    }

    public void TriggerStealth()
    {
        if (populationManager != null)
        {
            populationManager.TriggerStealth();
        }
    }

    public float GetItemDropMultiplier()
    {
        if (director != null)
        {
            return director.GetItemDropMultiplier();
        }
        return 1f;
    }

    public void RestartGame()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.LoadLevel(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    #endregion

    // Debug UI
    void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        GUILayout.BeginArea(new Rect(10, 380, 250, 100));
        GUILayout.BeginVertical("box");

        GUILayout.Label("<b>Game Manager</b>");
        GUILayout.Label($"Mode: {(useDynamicPopulation ? "Dynamic" : "Waves")}");
        GUILayout.Label($"Survival Time: {SurvivalTime:F0}s");
        GUILayout.Label($"Total Kills: {TotalZombiesKilled}");
        if (useTraditionalWaves) GUILayout.Label($"Wave: {CurrentWave}");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
