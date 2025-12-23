using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public static NetworkManager Instance { get; private set; }

    [Header("Settings")]
    public string gameVersion = "1.0";
    public int maxPlayersPerRoom = 4;
    public string gameSceneName = "Game";
    public string menuSceneName = "MainMenu";

    [Header("Player")]
    public GameObject playerPrefab;
    public Transform[] spawnPoints;

    // State
    public bool IsConnecting { get; private set; }
    public bool IsInRoom => PhotonNetwork.InRoom;
    public bool IsMasterClient => PhotonNetwork.IsMasterClient;
    public int PlayerCount => PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;

    // Events (renamed to avoid conflict with Photon callbacks)
    public event System.Action ConnectedToMaster;
    public event System.Action JoinedLobby;
    public event System.Action JoinedRoom;
    public event System.Action LeftRoom;
    public event System.Action<Player> PlayerJoined;
    public event System.Action<Player> PlayerLeft;
    public event System.Action<string> ConnectionError;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        PhotonNetwork.AutomaticallySyncScene = true;
    }

    void Start()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Connect();
        }
    }

    public void Connect()
    {
        IsConnecting = true;
        Debug.Log("[NetworkManager] Connecting to Photon...");

        PhotonNetwork.GameVersion = gameVersion;
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[NetworkManager] Connected to Master Server");
        IsConnecting = false;
        ConnectedToMaster?.Invoke();

        // Auto-join lobby
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[NetworkManager] Joined Lobby");
        JoinedLobby?.Invoke();
    }

    public void CreateRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning("[NetworkManager] Not connected to Photon");
            return;
        }

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = (byte)maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true
        };

        Debug.Log($"[NetworkManager] Creating room: {roomName}");
        PhotonNetwork.CreateRoom(roomName, options);
    }

    public void JoinRoom(string roomName)
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning("[NetworkManager] Not connected to Photon");
            return;
        }

        Debug.Log($"[NetworkManager] Joining room: {roomName}");
        PhotonNetwork.JoinRoom(roomName);
    }

    public void JoinRandomRoom()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning("[NetworkManager] Not connected to Photon");
            return;
        }

        Debug.Log("[NetworkManager] Joining random room...");
        PhotonNetwork.JoinRandomRoom();
    }

    public void QuickPlay()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning("[NetworkManager] Not connected to Photon");
            return;
        }

        Debug.Log("[NetworkManager] Quick Play - joining or creating room...");
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"[NetworkManager] No random room available, creating new room...");
        CreateRoom("Room_" + Random.Range(1000, 9999));
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[NetworkManager] Joined Room: {PhotonNetwork.CurrentRoom.Name}");
        Debug.Log($"[NetworkManager] Players in room: {PhotonNetwork.CurrentRoom.PlayerCount}");

        JoinedRoom?.Invoke();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[NetworkManager] Create room failed: {message}");
        ConnectionError?.Invoke($"Failed to create room: {message}");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[NetworkManager] Join room failed: {message}");
        ConnectionError?.Invoke($"Failed to join room: {message}");
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[NetworkManager] Left room");
        LeftRoom?.Invoke();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[NetworkManager] Player joined: {newPlayer.NickName}");
        PlayerJoined?.Invoke(newPlayer);
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[NetworkManager] Player left: {otherPlayer.NickName}");
        PlayerLeft?.Invoke(otherPlayer);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[NetworkManager] Disconnected: {cause}");
        IsConnecting = false;
        ConnectionError?.Invoke($"Disconnected: {cause}");
    }

    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
    }

    public void StartGame()
    {
        if (!IsMasterClient)
        {
            Debug.LogWarning("[NetworkManager] Only master client can start the game");
            return;
        }

        Debug.Log($"[NetworkManager] Starting game, loading scene: {gameSceneName}");
        PhotonNetwork.LoadLevel(gameSceneName);
    }

    public void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[NetworkManager] Player prefab not set!");
            return;
        }

        // Find spawn point
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int spawnIndex = PhotonNetwork.LocalPlayer.ActorNumber % spawnPoints.Length;
            Transform spawn = spawnPoints[spawnIndex];
            spawnPosition = spawn.position;
            spawnRotation = spawn.rotation;
        }

        Debug.Log($"[NetworkManager] Spawning player at {spawnPosition}");
        PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, spawnRotation);
    }

    public void SetPlayerName(string playerName)
    {
        PhotonNetwork.NickName = playerName;
        PlayerPrefs.SetString("PlayerName", playerName);
    }

    public string GetPlayerName()
    {
        return PhotonNetwork.NickName;
    }

    public List<RoomInfo> GetRoomList()
    {
        // This would need to be cached from OnRoomListUpdate
        return new List<RoomInfo>();
    }

    public Player[] GetPlayersInRoom()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return new Player[0];

        Player[] players = new Player[PhotonNetwork.CurrentRoom.PlayerCount];
        PhotonNetwork.CurrentRoom.Players.Values.CopyTo(players, 0);
        return players;
    }
}
