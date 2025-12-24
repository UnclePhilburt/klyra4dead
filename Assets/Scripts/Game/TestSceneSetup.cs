using UnityEngine;
using Photon.Pun;

/// <summary>
/// Simple test scene setup for single-player testing in browser.
/// Attach this to an empty GameObject in your scene.
/// Drag your character FBX into the characterPrefab slot in the Inspector.
/// </summary>
public class TestSceneSetup : MonoBehaviour
{
    [Header("Player Setup")]
    [Tooltip("Drag your character FBX (e.g., 'Rifle Idle 1') here")]
    public GameObject characterPrefab;

    [Tooltip("Where to spawn the player - default is FPS controller's working position")]
    public Vector3 spawnPosition = new Vector3(142f, 46f, -155f);

    [Header("Ground")]
    [Tooltip("Create a simple ground plane (disable for existing maps like Flooded_Grounds)")]
    public bool createGround = false;
    public Vector3 groundSize = new Vector3(200, 1, 200);

    void Start()
    {
        // Skip if NetworkPlayerSpawner will handle player spawning
        if (FindFirstObjectByType<NetworkPlayerSpawner>() != null)
        {
            Debug.Log("[TestSceneSetup] NetworkPlayerSpawner found - disabling single player setup");
            enabled = false;
            return;
        }

        // Skip if connected to Photon (multiplayer mode)
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("[TestSceneSetup] Photon connected - disabling single player setup");
            enabled = false;
            return;
        }

        if (createGround)
        {
            CreateSimpleGround();
        }

        SpawnPlayer();
    }

    void CreateSimpleGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -0.5f, 0);
        ground.transform.localScale = groundSize;
        ground.layer = LayerMask.NameToLayer("Default");
        ground.isStatic = true;

        BoxCollider col = ground.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = ground.AddComponent<BoxCollider>();
        }

        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = new Color(0.3f, 0.4f, 0.3f);
        }

        Debug.Log($"[TestSceneSetup] Ground created at Y=0 with collider. Size: {groundSize}");
    }

    void SpawnPlayer()
    {
        GameObject player;

        if (characterPrefab != null)
        {
            player = Instantiate(characterPrefab, spawnPosition, Quaternion.identity);
            player.name = "Player";
        }
        else
        {
            Debug.LogWarning("[TestSceneSetup] No character prefab assigned! Creating capsule placeholder.");
            Debug.LogWarning("[TestSceneSetup] Drag 'Rifle Idle 1.fbx' from Assets/animations/swat into the Character Prefab slot.");

            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = spawnPosition;
            Destroy(player.GetComponent<Collider>());
        }

        SimpleThirdPersonController controller = player.GetComponent<SimpleThirdPersonController>();
        if (controller == null)
        {
            controller = player.AddComponent<SimpleThirdPersonController>();
        }

        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            RuntimeAnimatorController swatController = Resources.Load<RuntimeAnimatorController>("SwatAnimator");
            if (swatController == null)
            {
                Debug.Log("[TestSceneSetup] Animator found. Make sure SwatAnimator.controller is assigned in the Animator component.");
            }
        }
        else if (characterPrefab != null)
        {
            Debug.LogWarning("[TestSceneSetup] No Animator found on character. Animations won't play.");
        }

        Debug.Log("[TestSceneSetup] Player spawned! Use WASD to move, Mouse to look, Shift to run, Space to jump.");
    }
}
