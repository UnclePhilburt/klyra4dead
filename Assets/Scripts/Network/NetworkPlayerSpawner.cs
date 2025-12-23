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

    private bool hasSpawned = false;

    void Start()
    {
        // Check if we're in a Photon room
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            // Disable local-only objects
            foreach (var obj in disableWhenConnected)
            {
                if (obj != null) obj.SetActive(false);
            }

            SpawnPlayer();
        }
        else
        {
            // Single player mode - keep local objects active
            Debug.Log("[NetworkPlayerSpawner] Not in Photon room - single player mode");
        }
    }

    public override void OnJoinedRoom()
    {
        // This is called if we join a room after the scene loads
        if (!hasSpawned)
        {
            foreach (var obj in disableWhenConnected)
            {
                if (obj != null) obj.SetActive(false);
            }

            SpawnPlayer();
        }
    }

    void SpawnPlayer()
    {
        if (hasSpawned) return;
        hasSpawned = true;

        // Determine spawn position
        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        // Use spawn point based on player number
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

        // Check SpawnManager for the correct spawn point
        if (SpawnManager.Instance != null)
        {
            Transform spawnPoint = SpawnManager.Instance.GetSpawnPoint();
            if (spawnPoint != null)
            {
                spawnPos = spawnPoint.position;
                spawnRot = spawnPoint.rotation;
            }
        }

        Debug.Log($"[NetworkPlayerSpawner] Spawning player '{playerPrefabName}' at {spawnPos}");

        // Spawn networked player
        GameObject player = PhotonNetwork.Instantiate(playerPrefabName, spawnPos, spawnRot);

        if (player != null)
        {
            Debug.Log($"[NetworkPlayerSpawner] Player spawned successfully! ActorNumber: {PhotonNetwork.LocalPlayer.ActorNumber}");
        }
        else
        {
            Debug.LogError($"[NetworkPlayerSpawner] Failed to spawn player! Make sure '{playerPrefabName}' exists in a Resources folder.");
        }
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        Debug.Log($"[NetworkPlayerSpawner] Player joined: {newPlayer.NickName} (Actor #{newPlayer.ActorNumber})");
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        Debug.Log($"[NetworkPlayerSpawner] Player left: {otherPlayer.NickName}");
    }
}
