using UnityEngine;
using UnityEditor;

public class SetupAIDirectorSystem : EditorWindow
{
    [MenuItem("Tools/Setup AI Director System")]
    public static void Setup()
    {
        // Find zombie prefab
        GameObject zombiePrefab = Resources.Load<GameObject>("Zombie");
        
        // 1. Create or find NoiseManager (for stealth system)
        NoiseManager noiseManager = Object.FindFirstObjectByType<NoiseManager>();
        if (noiseManager == null)
        {
            GameObject noiseObj = new GameObject("NoiseManager");
            noiseManager = noiseObj.AddComponent<NoiseManager>();
            Debug.Log("[Setup] Created NoiseManager (stealth audio system)");
        }
        else
        {
            Debug.Log("[Setup] NoiseManager already exists");
        }

        // 2. Create or find AIDirector
        AIDirector director = Object.FindFirstObjectByType<AIDirector>();
        if (director == null)
        {
            GameObject directorObj = new GameObject("AIDirector");
            director = directorObj.AddComponent<AIDirector>();
            Debug.Log("[Setup] Created AIDirector");
        }
        else
        {
            Debug.Log("[Setup] AIDirector already exists");
        }

        // 3. Create or find ZombiePopulationManager
        ZombiePopulationManager popManager = Object.FindFirstObjectByType<ZombiePopulationManager>();
        if (popManager == null)
        {
            GameObject popObj = new GameObject("ZombiePopulationManager");
            popManager = popObj.AddComponent<ZombiePopulationManager>();
            
            // Set zombie prefab
            if (zombiePrefab != null)
            {
                popManager.commonZombies = new GameObject[] { zombiePrefab };
                Debug.Log("[Setup] Assigned Zombie prefab to PopulationManager");
            }
            else
            {
                Debug.LogWarning("[Setup] Zombie prefab not found in Resources folder! Please assign manually.");
            }
            
            Debug.Log("[Setup] Created ZombiePopulationManager");
        }
        else
        {
            Debug.Log("[Setup] ZombiePopulationManager already exists");
        }

        // 4. Create or find GameManager
        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gameManager = gmObj.AddComponent<GameManager>();
            
            // Add PhotonView for networking
            if (gmObj.GetComponent<Photon.Pun.PhotonView>() == null)
            {
                gmObj.AddComponent<Photon.Pun.PhotonView>();
            }
            
            Debug.Log("[Setup] Created GameManager");
        }
        
        // Configure GameManager
        gameManager.useDynamicPopulation = true;
        gameManager.useTraditionalWaves = false;
        gameManager.survivalMode = true;
        gameManager.director = director;
        gameManager.populationManager = popManager;
        
        // Set zombie prefabs on GameManager too
        if (zombiePrefab != null)
        {
            gameManager.zombiePrefabs = new GameObject[] { zombiePrefab };
        }
        
        Debug.Log("[Setup] Configured GameManager for dynamic population mode");

        // 5. Add PlayerStats to NetworkPlayer prefab
        AddPlayerStatsToPrefab();

        // 6. Mark scene dirty so changes are saved
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[Setup] ========================================");
        Debug.Log("[Setup] STEALTH ZOMBIE SYSTEM SETUP COMPLETE!");
        Debug.Log("[Setup] ========================================");
        Debug.Log("[Setup] - NoiseManager: Tracks player noise for stealth");
        Debug.Log("[Setup] - AIDirector: Controls pacing & intensity");
        Debug.Log("[Setup] - ZombiePopulationManager: Spawns zombie packs");
        Debug.Log("[Setup] - GameManager: Tracks game state");
        Debug.Log("[Setup] ========================================");
        Debug.Log("[Setup] HOW STEALTH WORKS:");
        Debug.Log("[Setup] - Zombies are ALMOST BLIND (6m vision)");
        Debug.Log("[Setup] - Zombies have GREAT HEARING");
        Debug.Log("[Setup] - Gunshots attract zombies from 60m!");
        Debug.Log("[Setup] - Running attracts from 12m");
        Debug.Log("[Setup] - Walking is quiet (5m)");
        Debug.Log("[Setup] - Stand still = invisible to zombies");
        Debug.Log("[Setup] ========================================");
        
        // Select the noise manager so user can see it
        Selection.activeGameObject = noiseManager.gameObject;
    }

    static void AddPlayerStatsToPrefab()
    {
        string path = "Assets/Resources/NetworkPlayer.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogWarning("[Setup] NetworkPlayer prefab not found at: " + path);
            return;
        }

        if (prefab.GetComponent<PlayerStats>() != null)
        {
            Debug.Log("[Setup] PlayerStats already on NetworkPlayer prefab");
            return;
        }

        // Instantiate, modify, save
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        instance.AddComponent<PlayerStats>();
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);

        Debug.Log("[Setup] Added PlayerStats to NetworkPlayer prefab");
    }

    [MenuItem("Tools/Trigger Test Horde")]
    public static void TriggerTestHorde()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Game must be running to trigger horde!");
            return;
        }

        if (ZombiePopulationManager.Instance != null)
        {
            ZombiePopulationManager.Instance.TriggerHorde();
            Debug.Log("[Test] Triggered HORDE!");
        }
        else if (AIDirector.Instance != null)
        {
            AIDirector.Instance.TriggerHorde();
            Debug.Log("[Test] Triggered horde via AIDirector");
        }
        else
        {
            Debug.LogWarning("No ZombiePopulationManager or AIDirector found!");
        }
    }

    [MenuItem("Tools/Trigger Stealth Mode")]
    public static void TriggerStealth()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Game must be running to trigger stealth!");
            return;
        }

        if (ZombiePopulationManager.Instance != null)
        {
            ZombiePopulationManager.Instance.TriggerStealth();
            Debug.Log("[Test] Triggered STEALTH mode - zombies will back off");
        }
        else
        {
            Debug.LogWarning("No ZombiePopulationManager found!");
        }
    }
}
