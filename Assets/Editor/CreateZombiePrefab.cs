#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

public class CreateZombiePrefab : MonoBehaviour
{
    [MenuItem("Tools/Create Zombie Prefab")]
    static void CreatePrefab()
    {
        // First make sure animator is built
        Debug.Log("Creating Zombie Prefab...");

        // Load the zombie model
        GameObject zombieModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/characters/zombie/Ch10_nonPBR@Zombie Idle.fbx");
        if (zombieModel == null)
        {
            Debug.LogError("Could not find zombie model!");
            return;
        }

        // Create root object
        GameObject zombieRoot = new GameObject("Zombie");

        // Add NavMeshAgent
        NavMeshAgent agent = zombieRoot.AddComponent<NavMeshAgent>();
        agent.speed = 1.5f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0.5f;
        agent.radius = 0.4f;
        agent.height = 2f;

        // Add capsule collider
        CapsuleCollider capsule = zombieRoot.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0, 1f, 0);
        capsule.radius = 0.4f;
        capsule.height = 2f;

        // Add Photon components
        Photon.Pun.PhotonView photonView = zombieRoot.AddComponent<Photon.Pun.PhotonView>();
        photonView.OwnershipTransfer = Photon.Pun.OwnershipOption.Takeover;

        // Add ZombieAI (implements IPunObservable)
        ZombieAI zombieAI = zombieRoot.AddComponent<ZombieAI>();

        // Add ZombieHealth
        zombieRoot.AddComponent<ZombieHealth>();

        // Add ZombieRagdoll
        zombieRoot.AddComponent<ZombieRagdoll>();

        // Set observed components
        photonView.ObservedComponents = new System.Collections.Generic.List<Component> { zombieAI };

        // Instantiate model as child
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(zombieModel);
        model.transform.SetParent(zombieRoot.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.name = "Model";

        // Get animator and assign controller
        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = model.GetComponentInChildren<Animator>();

        if (animator != null)
        {
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/characters/zombie/ZombieAnimator.controller");
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                Debug.Log("Assigned ZombieAnimator controller");
            }
            else
            {
                Debug.LogWarning("ZombieAnimator.controller not found - run Tools > Build Zombie Animator first!");
            }
        }

        // Save as prefab in Resources folder (required for Photon)
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        string prefabPath = "Assets/Resources/Zombie.prefab";

        // Remove old prefab if exists
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(zombieRoot, prefabPath);

        // Clean up scene object
        DestroyImmediate(zombieRoot);

        // Select the new prefab
        Selection.activeObject = prefab;

        Debug.Log($"Zombie prefab created at: {prefabPath}");
        Debug.Log("Add a ZombieSpawner to your scene - it will use PhotonNetwork.Instantiate to spawn zombies");
    }
}
#endif
