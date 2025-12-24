using UnityEngine;
using UnityEditor;

public class FixZombieAnimations
{
    [MenuItem("Tools/Fix Zombie Animation Sinking")]
    public static void Fix()
    {
        // Fix prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Zombie.prefab");
        if (prefab != null)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Animator animator = instance.GetComponentInChildren<Animator>();
            
            if (animator != null)
            {
                // Disable root motion - this often causes sinking
                animator.applyRootMotion = false;
                Debug.Log("[Fix] Disabled root motion on Animator");
            }
            
            PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Resources/Zombie.prefab");
            Object.DestroyImmediate(instance);
            
            Debug.Log("[Fix] Zombie prefab saved!");
        }
        
        // Fix all zombies in scene
        foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            if (animator.gameObject.name.Contains("Zombie") || 
                animator.transform.root.name.Contains("Zombie"))
            {
                animator.applyRootMotion = false;
                EditorUtility.SetDirty(animator);
                Debug.Log($"[Fix] Disabled root motion on: {animator.gameObject.name}");
            }
        }
        
        Debug.Log("[Fix] Done! Root motion disabled on all zombies.");
    }
}
