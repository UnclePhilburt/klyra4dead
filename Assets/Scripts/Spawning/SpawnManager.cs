using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [Header("Spawn Points")]
    public Transform initialSpawnPoint;
    public Transform safeZoneSpawnPoint;

    [Header("Settings")]
    public float spawnDelay = 0.5f; // Wait for player to initialize

    [Header("Quest")]
    public bool hasReachedSafeZone = false;

    public static SpawnManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;

        // Check if player already reached safe zone (saved in PlayerPrefs)
        hasReachedSafeZone = PlayerPrefs.GetInt("ReachedSafeZone", 0) == 1;
    }

    void Start()
    {
        // Wait a moment for player to spawn, then move them
        Invoke(nameof(SpawnPlayer), spawnDelay);
    }

    void SpawnPlayer()
    {
        Transform spawnPoint = GetSpawnPoint();
        if (spawnPoint == null)
        {
            Debug.LogWarning("[SpawnManager] No spawn point set!");
            return;
        }

        // Find player
        ThirdPersonMotor player = FindFirstObjectByType<ThirdPersonMotor>();
        if (player != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();

            // Disable CharacterController to teleport
            if (cc != null) cc.enabled = false;

            player.transform.position = spawnPoint.position;
            player.transform.rotation = spawnPoint.rotation;

            // Re-enable CharacterController
            if (cc != null) cc.enabled = true;

            Debug.Log($"[SpawnManager] Player spawned at {spawnPoint.name}");
        }
        else
        {
            Debug.LogWarning("[SpawnManager] No player found!");
        }
    }

    public Transform GetSpawnPoint()
    {
        if (hasReachedSafeZone && safeZoneSpawnPoint != null)
        {
            return safeZoneSpawnPoint;
        }
        return initialSpawnPoint;
    }

    public void UnlockSafeZoneSpawn()
    {
        if (hasReachedSafeZone) return;

        hasReachedSafeZone = true;
        PlayerPrefs.SetInt("ReachedSafeZone", 1);
        PlayerPrefs.Save();

        Debug.Log("[SpawnManager] Safe zone spawn unlocked!");
    }

    // For testing - reset progress
    public void ResetProgress()
    {
        hasReachedSafeZone = false;
        PlayerPrefs.SetInt("ReachedSafeZone", 0);
        PlayerPrefs.Save();

        Debug.Log("[SpawnManager] Progress reset!");
    }

    // Reset with R key in editor for testing
    void Update()
    {
        #if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.P))
        {
            ResetProgress();
        }
        #endif
    }
}
