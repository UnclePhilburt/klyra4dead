using UnityEngine;

/// <summary>
/// Add this component to a zombie to automatically create a head hitbox collider.
/// The head collider allows headshots to be detected separately from body shots.
/// </summary>
public class ZombieHeadHitbox : MonoBehaviour
{
    [Header("Head Hitbox Settings")]
    public float headRadius = 0.15f;
    public Vector3 headOffset = new Vector3(0, 0.05f, 0);

    private GameObject headHitbox;

    void Start()
    {
        CreateHeadHitbox();
    }

    void CreateHeadHitbox()
    {
        // Find the head bone
        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning("[ZombieHeadHitbox] No humanoid animator found!");
            return;
        }

        Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        if (headBone == null)
        {
            Debug.LogWarning("[ZombieHeadHitbox] Head bone not found!");
            return;
        }

        // Create head hitbox object
        headHitbox = new GameObject("Head");
        headHitbox.transform.SetParent(headBone);
        headHitbox.transform.localPosition = headOffset;
        headHitbox.transform.localRotation = Quaternion.identity;

        // Add sphere collider
        SphereCollider sphereCol = headHitbox.AddComponent<SphereCollider>();
        sphereCol.radius = headRadius;
        sphereCol.isTrigger = false;

        Debug.Log("[ZombieHeadHitbox] Head hitbox created!");
    }

    void OnDestroy()
    {
        if (headHitbox != null)
        {
            Destroy(headHitbox);
        }
    }
}
