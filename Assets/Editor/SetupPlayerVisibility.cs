using UnityEngine;
using UnityEditor;

public class SetupPlayerVisibility
{
    [MenuItem("Tools/Add Player Visibility Component")]
    public static void Setup()
    {
        // Try to add to NetworkPlayer prefab
        GameObject prefab = Resources.Load<GameObject>("NetworkPlayer");
        if (prefab != null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            
            if (instance.GetComponent<PlayerVisibility>() == null)
            {
                PlayerVisibility visibility = instance.AddComponent<PlayerVisibility>();
                visibility.baseVisibility = 0.3f;
                visibility.maxVisibility = 1f;
                visibility.lightCheckRadius = 30f;
                
                PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Resources/NetworkPlayer.prefab");
                Debug.Log("[Visibility] Added PlayerVisibility to NetworkPlayer prefab");
            }
            else
            {
                Debug.Log("[Visibility] PlayerVisibility already exists on NetworkPlayer");
            }
            
            Object.DestroyImmediate(instance);
        }
        else
        {
            Debug.LogWarning("[Visibility] NetworkPlayer prefab not found in Resources!");
        }

        // Also add to any players in scene
        foreach (var health in Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
        {
            if (health.GetComponent<PlayerVisibility>() == null)
            {
                health.gameObject.AddComponent<PlayerVisibility>();
                EditorUtility.SetDirty(health.gameObject);
                Debug.Log($"[Visibility] Added PlayerVisibility to: {health.gameObject.name}");
            }
        }

        EditorUtility.DisplayDialog("Player Visibility Setup",
            "PlayerVisibility component added!\n\n" +
            "How it works:\n" +
            "- In darkness: Zombies see you from 6m\n" +
            "- In light: Zombies see you from 20m\n\n" +
            "Adjust 'visionRange' and 'maxVisionRange' on ZombieAI to tune difficulty.",
            "OK");
    }
}
