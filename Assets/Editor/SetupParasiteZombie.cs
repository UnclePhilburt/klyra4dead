using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

public class SetupParasiteZombie : EditorWindow
{
    [MenuItem("Tools/Zombies/Setup Parasite Zombie Prefab")]
    public static void Setup()
    {
        // Find the FBX
        string fbxPath = "Assets/characters/parasitezombie/character.fbx";
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        
        if (fbx == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find character.fbx at:\n" + fbxPath, "OK");
            return;
        }

        // Create instance
        GameObject zombie = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        zombie.name = "ParasiteZombie";

        // Add NavMeshAgent
        NavMeshAgent agent = zombie.GetComponent<NavMeshAgent>();
        if (agent == null) agent = zombie.AddComponent<NavMeshAgent>();
        agent.speed = 3.5f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 1.5f;
        agent.radius = 0.5f;
        agent.height = 2f;

        // Add CapsuleCollider
        CapsuleCollider col = zombie.GetComponent<CapsuleCollider>();
        if (col == null) col = zombie.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 1f, 0);
        col.radius = 0.4f;
        col.height = 2f;

        // Add Rigidbody (kinematic for NavMesh)
        Rigidbody rb = zombie.GetComponent<Rigidbody>();
        if (rb == null) rb = zombie.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        // Add ZombieAI
        ZombieAI ai = zombie.GetComponent<ZombieAI>();
        if (ai == null) ai = zombie.AddComponent<ZombieAI>();
        
        // FEARLESS - ignores flashlight!
        ai.fearless = true;
        
        // Slower but relentless
        ai.walkSpeed = 1.2f;
        ai.chaseSpeed = 4f;
        ai.attackRange = 2f;
        ai.attackDamage = 25f;
        ai.visionRange = 20f;

        // Add ZombieHealth - tanky!
        ZombieHealth health = zombie.GetComponent<ZombieHealth>();
        if (health == null) health = zombie.AddComponent<ZombieHealth>();
        health.maxHealth = 200f;

        // Set layer
        int zombieLayer = LayerMask.NameToLayer("Zombie");
        if (zombieLayer >= 0) zombie.layer = zombieLayer;

        // Find or create Animator
        Animator animator = zombie.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // Try to find zombie animator controller
            RuntimeAnimatorController zombieController = Resources.Load<RuntimeAnimatorController>("ZombieAnimator");
            if (zombieController == null)
                zombieController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/characters/zombie/ZombieAnimator.controller");
            
            if (zombieController != null)
                animator.runtimeAnimatorController = zombieController;
        }

        // Ensure Resources folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // Save as prefab
        string prefabPath = "Assets/Resources/ParasiteZombie.prefab";
        
        // Remove old prefab if exists
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            AssetDatabase.DeleteAsset(prefabPath);

        // Create prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(zombie, prefabPath);
        
        // Destroy scene instance
        DestroyImmediate(zombie);

        // Select the new prefab
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        // Try to add to ZombiePopulationManager
        ZombiePopulationManager popManager = FindObjectOfType<ZombiePopulationManager>();
        if (popManager != null)
        {
            SerializedObject so = new SerializedObject(popManager);
            SerializedProperty zombiesArray = so.FindProperty("rareZombies");
            
            // Check if already in array
            bool alreadyAdded = false;
            for (int i = 0; i < zombiesArray.arraySize; i++)
            {
                GameObject existing = zombiesArray.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (existing != null && existing.name == "ParasiteZombie")
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                zombiesArray.arraySize++;
                zombiesArray.GetArrayElementAtIndex(zombiesArray.arraySize - 1).objectReferenceValue = prefab;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(popManager);
                Debug.Log("[SetupParasiteZombie] Added to ZombiePopulationManager!");
            }
        }

        EditorUtility.DisplayDialog("Success!", 
            "ParasiteZombie prefab created!\n\n" +
            "Stats:\n" +
            "- FEARLESS (ignores flashlight)\n" +
            "- Health: 200 (tanky)\n" +
            "- Walk: 1.2, Chase: 4.0\n" +
            "- Attack Damage: 25\n\n" +
            "Saved to: Assets/Resources/ParasiteZombie.prefab", "OK");

        Debug.Log("[SetupParasiteZombie] Prefab created at " + prefabPath);
    }
}
