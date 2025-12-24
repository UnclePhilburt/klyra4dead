using UnityEngine;
using UnityEditor;

public class AddPlayerStats : EditorWindow
{
    [MenuItem("Tools/Add PlayerStats to NetworkPlayer")]
    public static void AddStats()
    {
        // Load the prefab
        string path = "Assets/Resources/NetworkPlayer.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogError($"Could not find prefab at: {path}");
            return;
        }

        // Check if already has PlayerStats
        if (prefab.GetComponent<PlayerStats>() != null)
        {
            Debug.Log("[AddPlayerStats] PlayerStats already exists on NetworkPlayer");
            return;
        }

        // Add component to prefab
        GameObject prefabInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        
        if (prefabInstance.GetComponent<PlayerStats>() == null)
        {
            prefabInstance.AddComponent<PlayerStats>();
            Debug.Log("[AddPlayerStats] Added PlayerStats component");
        }

        // Save back to prefab
        PrefabUtility.SaveAsPrefabAsset(prefabInstance, path);
        DestroyImmediate(prefabInstance);

        Debug.Log("[AddPlayerStats] NetworkPlayer prefab updated with PlayerStats!");
    }
}
