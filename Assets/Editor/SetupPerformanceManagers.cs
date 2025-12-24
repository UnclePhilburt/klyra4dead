using UnityEngine;
using UnityEditor;

public class SetupPerformanceManagers
{
    [MenuItem("Tools/Setup Performance Managers")]
    public static void Setup()
    {
        // Create or find GameManagers object
        GameObject managers = GameObject.Find("GameManagers");
        if (managers == null)
        {
            managers = new GameObject("GameManagers");
            Debug.Log("[Performance] Created GameManagers object");
        }

        // Add LightManager
        if (Object.FindObjectOfType<LightManager>() == null)
        {
            managers.AddComponent<LightManager>();
            Debug.Log("[Performance] Added LightManager - caches lights for fast queries");
        }
        else
        {
            Debug.Log("[Performance] LightManager already exists");
        }

        // Add ZombieManager
        if (Object.FindObjectOfType<ZombieManager>() == null)
        {
            managers.AddComponent<ZombieManager>();
            Debug.Log("[Performance] Added ZombieManager - handles distance culling");
        }
        else
        {
            Debug.Log("[Performance] ZombieManager already exists");
        }

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[Performance] Setup complete! Performance managers ready.");
        Debug.Log("[Performance] Tips:");
        Debug.Log("  - LightManager: Caches all lights, updates every 2s");
        Debug.Log("  - ZombieManager: Culls distant zombie AI updates");
        Debug.Log("  - Far zombies update less frequently (saves CPU)");
    }
}
