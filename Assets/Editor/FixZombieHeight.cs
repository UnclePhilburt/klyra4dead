using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

public class FixZombieHeight
{
    [MenuItem("Tools/Fix Zombie Height")]
    public static void Fix()
    {
        // Fix prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Zombie.prefab");
        if (prefab != null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            
            if (agent != null)
            {
                // Get the model height
                Renderer renderer = instance.GetComponentInChildren<Renderer>();
                float height = 1.8f; // Default human height
                
                if (renderer != null)
                {
                    height = renderer.bounds.size.y;
                }
                
                // Set base offset to half height (if pivot is at center)
                // Or 0 if pivot is at feet
                agent.baseOffset = 0f;
                
                // Also check if model needs to be moved up
                Transform model = instance.transform.GetChild(0);
                if (model != null && model.localPosition.y < 0)
                {
                    model.localPosition = new Vector3(model.localPosition.x, 0f, model.localPosition.z);
                }
                
                Debug.Log($"[Fix] Zombie height: {height}m, baseOffset set to: {agent.baseOffset}");
            }
            
            PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Resources/Zombie.prefab");
            Object.DestroyImmediate(instance);
            
            Debug.Log("[Fix] Zombie prefab updated!");
        }
        
        // Fix all zombies in scene
        foreach (var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
        {
            if (agent.gameObject.name.Contains("Zombie"))
            {
                agent.baseOffset = 0f;
                Debug.Log($"[Fix] Fixed: {agent.gameObject.name}");
            }
        }
    }
    
    [MenuItem("Tools/Raise Zombies (baseOffset +0.5)")]
    public static void RaiseZombies()
    {
        foreach (var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
        {
            if (agent.gameObject.name.Contains("Zombie"))
            {
                agent.baseOffset += 0.5f;
                Debug.Log($"[Fix] Raised: {agent.gameObject.name}, baseOffset now: {agent.baseOffset}");
            }
        }
    }
    
    [MenuItem("Tools/Lower Zombies (baseOffset -0.5)")]
    public static void LowerZombies()
    {
        foreach (var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
        {
            if (agent.gameObject.name.Contains("Zombie"))
            {
                agent.baseOffset -= 0.5f;
                Debug.Log($"[Fix] Lowered: {agent.gameObject.name}, baseOffset now: {agent.baseOffset}");
            }
        }
    }
}
