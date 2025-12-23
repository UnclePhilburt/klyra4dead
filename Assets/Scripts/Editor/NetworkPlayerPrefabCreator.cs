using UnityEngine;
using UnityEditor;
using Photon.Pun;
using System.IO;

public class NetworkPlayerPrefabCreator : EditorWindow
{
    private GameObject sourcePlayer;
    private string prefabName = "NetworkPlayer";

    [MenuItem("Tools/Klyra4Dead/Create Network Player Prefab")]
    public static void ShowWindow()
    {
        GetWindow<NetworkPlayerPrefabCreator>("Network Player Creator");
    }

    [MenuItem("Tools/Klyra4Dead/Auto Setup Network Player")]
    public static void AutoSetup()
    {
        // Find player in scene
        GameObject player = FindPlayerInScene();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find player in scene. Make sure you have a player with ThirdPersonMotor component.", "OK");
            return;
        }

        CreateNetworkPlayerPrefab(player, "NetworkPlayer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Network Player Prefab Creator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        sourcePlayer = (GameObject)EditorGUILayout.ObjectField("Source Player", sourcePlayer, typeof(GameObject), true);
        prefabName = EditorGUILayout.TextField("Prefab Name", prefabName);

        GUILayout.Space(10);

        if (GUILayout.Button("Find Player in Scene"))
        {
            sourcePlayer = FindPlayerInScene();
            if (sourcePlayer == null)
            {
                EditorUtility.DisplayDialog("Not Found", "Could not find player in scene.", "OK");
            }
        }

        GUILayout.Space(10);

        GUI.enabled = sourcePlayer != null;
        if (GUILayout.Button("Create Network Player Prefab"))
        {
            CreateNetworkPlayerPrefab(sourcePlayer, prefabName);
        }
        GUI.enabled = true;

        GUILayout.Space(20);
        GUILayout.Label("Instructions:", EditorStyles.boldLabel);
        GUILayout.Label("1. Drag your player object or click 'Find Player'");
        GUILayout.Label("2. Click 'Create Network Player Prefab'");
        GUILayout.Label("3. Add NetworkPlayerSpawner to scene");
    }

    private static GameObject FindPlayerInScene()
    {
        // Try to find by ThirdPersonMotor component
        ThirdPersonMotor motor = FindObjectOfType<ThirdPersonMotor>();
        if (motor != null)
        {
            return motor.gameObject;
        }

        // Try to find by tag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            return player;
        }

        // Try to find by name
        string[] playerNames = { "Player", "PlayerCharacter", "Character", "FPSController" };
        foreach (string name in playerNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                return obj;
            }
        }

        return null;
    }

    private static void CreateNetworkPlayerPrefab(GameObject source, string prefabName)
    {
        // Ensure Resources folder exists
        string resourcesPath = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // Create a copy of the player
        GameObject playerCopy = Instantiate(source);
        playerCopy.name = prefabName;

        // Remove any missing scripts from the copy (and all children)
        RemoveMissingScripts(playerCopy);

        // Add or get PhotonView
        PhotonView photonView = playerCopy.GetComponent<PhotonView>();
        if (photonView == null)
        {
            photonView = playerCopy.AddComponent<PhotonView>();
        }

        // Setup observed components
        var observedComponents = new System.Collections.Generic.List<Component>();

        // Add ThirdPersonMotor if it implements IPunObservable
        ThirdPersonMotor motor = playerCopy.GetComponent<ThirdPersonMotor>();
        if (motor != null)
        {
            observedComponents.Add(motor);
        }

        // Add PlayerHealth if present
        PlayerHealth health = playerCopy.GetComponent<PlayerHealth>();
        if (health != null)
        {
            observedComponents.Add(health);
        }

        // Add PhotonTransformView for position sync backup
        PhotonTransformView transformView = playerCopy.GetComponent<PhotonTransformView>();
        if (transformView == null)
        {
            transformView = playerCopy.AddComponent<PhotonTransformView>();
            transformView.m_SynchronizePosition = true;
            transformView.m_SynchronizeRotation = true;
            transformView.m_SynchronizeScale = false;
        }
        observedComponents.Add(transformView);

        // Set observed components on PhotonView
        photonView.ObservedComponents = observedComponents;
        photonView.Synchronization = ViewSynchronization.UnreliableOnChange;

        // Add PhotonAnimatorView if there's an Animator
        Animator animator = playerCopy.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            PhotonAnimatorView animView = playerCopy.GetComponent<PhotonAnimatorView>();
            if (animView == null)
            {
                animView = playerCopy.AddComponent<PhotonAnimatorView>();
            }
            // PhotonAnimatorView will auto-detect parameters
        }

        // Save as prefab
        string prefabPath = $"{resourcesPath}/{prefabName}.prefab";

        // Remove old prefab if exists
        if (File.Exists(prefabPath))
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        // Create prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(playerCopy, prefabPath);

        // Destroy the temporary copy
        DestroyImmediate(playerCopy);

        // Select the new prefab
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log($"[NetworkPlayerPrefabCreator] Created prefab at {prefabPath}");
        Debug.Log($"[NetworkPlayerPrefabCreator] Observed components: {observedComponents.Count}");

        EditorUtility.DisplayDialog("Success",
            $"NetworkPlayer prefab created at:\n{prefabPath}\n\nNow add NetworkPlayerSpawner to your scene!",
            "OK");

        // Create NetworkPlayerSpawner in scene if not present
        if (FindObjectOfType<NetworkPlayerSpawner>() == null)
        {
            if (EditorUtility.DisplayDialog("Add Spawner?",
                "Would you like to add NetworkPlayerSpawner to the scene?",
                "Yes", "No"))
            {
                GameObject spawnerObj = new GameObject("NetworkPlayerSpawner");
                NetworkPlayerSpawner spawner = spawnerObj.AddComponent<NetworkPlayerSpawner>();
                spawner.playerPrefabName = prefabName;

                // Find spawn points
                var spawnManager = FindObjectOfType<SpawnManager>();
                if (spawnManager != null)
                {
                    Debug.Log("[NetworkPlayerPrefabCreator] SpawnManager found - spawner will use it automatically");
                }

                Selection.activeObject = spawnerObj;
                Debug.Log("[NetworkPlayerPrefabCreator] Added NetworkPlayerSpawner to scene");
            }
        }
    }

    private static void RemoveMissingScripts(GameObject go)
    {
        // Remove missing scripts from this object
        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        if (removed > 0)
        {
            Debug.Log($"[NetworkPlayerPrefabCreator] Removed {removed} missing script(s) from {go.name}");
        }

        // Recursively remove from all children
        foreach (Transform child in go.transform)
        {
            RemoveMissingScripts(child.gameObject);
        }
    }
}
