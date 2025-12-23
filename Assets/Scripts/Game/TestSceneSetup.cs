using UnityEngine;

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

        // Ensure collider exists (CreatePrimitive adds one, but let's be safe)
        BoxCollider col = ground.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = ground.AddComponent<BoxCollider>();
        }

        // Give it a basic color
        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = new Color(0.3f, 0.4f, 0.3f); // Greenish ground
        }

        Debug.Log($"[TestSceneSetup] Ground created at Y=0 with collider. Size: {groundSize}");
    }

    void SpawnPlayer()
    {
        GameObject player;

        if (characterPrefab != null)
        {
            // Instantiate the provided character
            player = Instantiate(characterPrefab, spawnPosition, Quaternion.identity);
            player.name = "Player";
        }
        else
        {
            // Create a capsule placeholder if no character assigned
            Debug.LogWarning("[TestSceneSetup] No character prefab assigned! Creating capsule placeholder.");
            Debug.LogWarning("[TestSceneSetup] Drag 'Rifle Idle 1.fbx' from Assets/animations/swat into the Character Prefab slot.");

            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = spawnPosition;

            // Remove the collider (CharacterController will handle collision)
            Destroy(player.GetComponent<Collider>());
        }

        // Add the controller if not present
        SimpleThirdPersonController controller = player.GetComponent<SimpleThirdPersonController>();
        if (controller == null)
        {
            controller = player.AddComponent<SimpleThirdPersonController>();
        }

        // Setup the Animator with SwatAnimator controller
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // Try to load the SwatAnimator controller
            RuntimeAnimatorController swatController = Resources.Load<RuntimeAnimatorController>("SwatAnimator");
            if (swatController == null)
            {
                // Try loading from the animations folder path
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
