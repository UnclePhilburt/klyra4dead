using UnityEngine;
using Photon.Pun;

/// <summary>
/// Spawns the local player when the scene loads in multiplayer.
/// Add this to an empty GameObject in your game scene.
/// </summary>
public class NetworkPlayerSpawner : MonoBehaviourPunCallbacks
{
    [Header("Player Prefab")]
    [Tooltip("Name of player prefab in Resources folder (e.g., 'NetworkPlayer')")]
    public string playerPrefabName = "NetworkPlayer";

    [Header("Spawn Points")]
    public Transform[] spawnPoints;
    public Transform defaultSpawnPoint;

    [Header("Scene Objects to Disable in Multiplayer")]
    [Tooltip("Objects to disable when connected (e.g., local-only player)")]
    public GameObject[] disableWhenConnected;

    [Header("Auto-Disable")]
    [Tooltip("Automatically find and disable existing players in scene")]
    public bool autoDisableScenePlayers = true;

    private bool hasSpawned = false;
    private static bool playerSpawnedThisSession = false;
    private static GameObject spawnedLocalPlayer = null;

    void Awake()
    {
        // Reset static flags when scene loads
        playerSpawnedThisSession = false;
        spawnedLocalPlayer = null;
    }

    void Start()
    {
        // Check if we're already in a Photon room
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            #if UNITY_EDITOR
            Debug.Log("[NetworkPlayerSpawner] Already in room, spawning immediately");
            #endif
            SetupMultiplayer();
        }
        else
        {
            // Ensure NetworkManager exists and is connecting
            EnsureNetworkManager();

            // Wait for room connection - OnJoinedRoom() will handle spawning
            #if UNITY_EDITOR
            Debug.Log("[NetworkPlayerSpawner] Waiting for Photon room connection...");
            #endif
            StartCoroutine(WaitForRoom());
        }
    }

    void EnsureNetworkManager()
    {
        // Create NetworkManager if it doesn't exist
        if (NetworkManager.Instance == null)
        {
            #if UNITY_EDITOR
            Debug.Log("[NetworkPlayerSpawner] NetworkManager not found - creating one...");
            #endif
            GameObject nmObj = new GameObject("NetworkManager");
            nmObj.AddComponent<NetworkManager>();
        }
        else if (!PhotonNetwork.IsConnected && !NetworkManager.Instance.IsConnecting)
        {
            #if UNITY_EDITOR
            Debug.Log("[NetworkPlayerSpawner] Triggering Photon connection via NetworkManager...");
            #endif
            NetworkManager.Instance.Connect();
        }
    }

    System.Collections.IEnumerator WaitForRoom()
    {
        float timeout = 15f;
        float elapsed = 0f;

        while (!PhotonNetwork.InRoom && timeout > 0)
        {
            timeout -= 1f;
            elapsed += 1f;

            if (elapsed % 3 == 0)
            {
                #if UNITY_EDITOR
                Debug.Log($"[NetworkPlayerSpawner] Connection status - Connected: {PhotonNetwork.IsConnected}, InLobby: {PhotonNetwork.InLobby}, InRoom: {PhotonNetwork.InRoom}");
                #endif
            }

            yield return new WaitForSeconds(1f);
        }

        if (PhotonNetwork.InRoom && !hasSpawned && !playerSpawnedThisSession)
        {
            #if UNITY_EDITOR
            Debug.Log("[NetworkPlayerSpawner] Room joined via coroutine, spawning player");
            #endif
            SetupMultiplayer();
        }
        else if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning($"[NetworkPlayerSpawner] Timeout! Connected: {PhotonNetwork.IsConnected}, InLobby: {PhotonNetwork.InLobby}, InRoom: {PhotonNetwork.InRoom}");
            #if UNITY_EDITOR
            Debug.Log("[NetworkPlayerSpawner] Running in single player mode");
            #endif
        }
    }

    void SetupMultiplayer()
    {
        // Disable manually specified objects
        foreach (var obj in disableWhenConnected)
        {
            if (obj != null)
            {
                #if UNITY_EDITOR
                Debug.Log($"[NetworkPlayerSpawner] Disabling: {obj.name}");
                #endif
                obj.SetActive(false);
            }
        }

        // Auto-find and disable any existing players in the scene
        if (autoDisableScenePlayers)
        {
            DisableExistingScenePlayers();
        }

        SpawnPlayer();
    }

    void DisableExistingScenePlayers()
    {
        // Find all ThirdPersonMotor objects (scene players)
        ThirdPersonMotor[] motors = FindObjectsByType<ThirdPersonMotor>(FindObjectsSortMode.None);
        foreach (var motor in motors)
        {
            PhotonView pv = motor.GetComponent<PhotonView>();
            if (pv != null && pv.InstantiationId != 0) continue;

            #if UNITY_EDITOR
            Debug.Log($"[NetworkPlayerSpawner] Auto-disabling scene player (Motor): {motor.gameObject.name}");
            #endif
            motor.gameObject.SetActive(false);
        }

        // Find all ThirdPersonController objects (newer player setup)
        ThirdPersonController[] controllers = FindObjectsByType<ThirdPersonController>(FindObjectsSortMode.None);
        foreach (var controller in controllers)
        {
            PhotonView pv = controller.GetComponent<PhotonView>();
            if (pv != null && pv.InstantiationId != 0) continue;

            #if UNITY_EDITOR
            Debug.Log($"[NetworkPlayerSpawner] Auto-disabling scene player (Controller): {controller.gameObject.name}");
            #endif
            controller.gameObject.SetActive(false);
        }

        // Also disable any SimpleThirdPersonController (older player setup)
        SimpleThirdPersonController[] simpleControllers = FindObjectsByType<SimpleThirdPersonController>(FindObjectsSortMode.None);
        foreach (var controller in simpleControllers)
        {
            PhotonView pv = controller.GetComponent<PhotonView>();
            if (pv != null && pv.InstantiationId != 0) continue;

            #if UNITY_EDITOR
            Debug.Log($"[NetworkPlayerSpawner] Auto-disabling scene player (Simple): {controller.gameObject.name}");
            #endif
            controller.gameObject.SetActive(false);
        }
    }

    public override void OnJoinedRoom()
    {
        #if UNITY_EDITOR
        Debug.Log("[NetworkPlayerSpawner] OnJoinedRoom callback triggered");
        #endif
        StartCoroutine(DelayedJoinedRoom());
    }

    System.Collections.IEnumerator DelayedJoinedRoom()
    {
        yield return null;
        if (!hasSpawned && !playerSpawnedThisSession)
        {
            SetupMultiplayer();
        }
    }

    void SpawnPlayer()
    {
        if (hasSpawned || playerSpawnedThisSession || spawnedLocalPlayer != null)
        {
            #if UNITY_EDITOR
            Debug.Log("[NetworkPlayerSpawner] Player already spawned (flag/static), skipping");
            #endif
            return;
        }

        // Check for existing local player
        foreach (var pv in FindObjectsByType<PhotonView>(FindObjectsSortMode.None))
        {
            if (pv.IsMine && pv.GetComponent<PlayerHealth>() != null)
            {
                #if UNITY_EDITOR
                Debug.Log("[NetworkPlayerSpawner] Local player already exists, skipping spawn");
                #endif
                hasSpawned = true;
                playerSpawnedThisSession = true;
                spawnedLocalPlayer = pv.gameObject;
                return;
            }
        }

        hasSpawned = true;
        playerSpawnedThisSession = true;

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int spawnIndex = (PhotonNetwork.LocalPlayer.ActorNumber - 1) % spawnPoints.Length;
            if (spawnPoints[spawnIndex] != null)
            {
                spawnPos = spawnPoints[spawnIndex].position;
                spawnRot = spawnPoints[spawnIndex].rotation;
            }
        }
        else if (defaultSpawnPoint != null)
        {
            spawnPos = defaultSpawnPoint.position;
            spawnRot = defaultSpawnPoint.rotation;
        }

        if (SpawnManager.Instance != null)
        {
            Transform spawnPoint = SpawnManager.Instance.GetSpawnPoint();
            if (spawnPoint != null)
            {
                spawnPos = spawnPoint.position;
                spawnRot = spawnPoint.rotation;
            }
        }

        #if UNITY_EDITOR
        Debug.Log($"[NetworkPlayerSpawner] Spawning player '{playerPrefabName}' at {spawnPos}");
        #endif

        GameObject player = PhotonNetwork.Instantiate(playerPrefabName, spawnPos, spawnRot);
        spawnedLocalPlayer = player;

        if (player != null)
        {
            #if UNITY_EDITOR
            Debug.Log($"[NetworkPlayerSpawner] Player spawned successfully! ActorNumber: {PhotonNetwork.LocalPlayer.ActorNumber}");
            #endif
        }
        else
        {
            Debug.LogError($"[NetworkPlayerSpawner] Failed to spawn player! Make sure '{playerPrefabName}' exists in a Resources folder.");
        }
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        #if UNITY_EDITOR
        Debug.Log($"[NetworkPlayerSpawner] Player joined: {newPlayer.NickName} (Actor #{newPlayer.ActorNumber})");
        #endif
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        #if UNITY_EDITOR
        Debug.Log($"[NetworkPlayerSpawner] Player left: {otherPlayer.NickName}");
        #endif
    }
}
