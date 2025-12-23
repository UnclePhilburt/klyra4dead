#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class SetupZombieRagdoll : MonoBehaviour
{
    [MenuItem("Tools/Setup Zombie Ragdoll")]
    static void SetupRagdoll()
    {
        // Find the zombie prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Zombie.prefab");
        if (prefab == null)
        {
            Debug.LogError("Zombie prefab not found! Run 'Create Zombie Prefab' first.");
            return;
        }

        // Instantiate to edit
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Animator animator = instance.GetComponentInChildren<Animator>();

        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("Zombie must have a Humanoid animator!");
            DestroyImmediate(instance);
            return;
        }

        // Clear any existing ragdoll components first
        foreach (var joint in instance.GetComponentsInChildren<CharacterJoint>())
            DestroyImmediate(joint);
        foreach (var rb in instance.GetComponentsInChildren<Rigidbody>())
            if (rb.gameObject != instance) DestroyImmediate(rb);
        foreach (var col in instance.GetComponentsInChildren<Collider>())
            if (col.gameObject != instance) DestroyImmediate(col);

        // Get bone transforms
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

        Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

        Transform leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Transform rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);

        Debug.Log($"Bones found - Hips:{hips != null} Spine:{spine != null} Chest:{chest != null} Head:{head != null}");

        // Add rigidbodies (no colliders on bones - use simple box colliders)
        float totalMass = 50f;

        // Core body
        Rigidbody hipsRb = AddRigidbody(hips, totalMass * 0.25f);
        Rigidbody spineRb = AddRigidbody(spine, totalMass * 0.15f);
        Rigidbody chestRb = AddRigidbody(chest ?? upperChest, totalMass * 0.15f);
        Rigidbody headRb = AddRigidbody(head, totalMass * 0.08f);

        // Arms
        Rigidbody leftUpperArmRb = AddRigidbody(leftUpperArm, totalMass * 0.03f);
        Rigidbody leftLowerArmRb = AddRigidbody(leftLowerArm, totalMass * 0.02f);
        Rigidbody rightUpperArmRb = AddRigidbody(rightUpperArm, totalMass * 0.03f);
        Rigidbody rightLowerArmRb = AddRigidbody(rightLowerArm, totalMass * 0.02f);

        // Legs
        Rigidbody leftUpperLegRb = AddRigidbody(leftUpperLeg, totalMass * 0.08f);
        Rigidbody leftLowerLegRb = AddRigidbody(leftLowerLeg, totalMass * 0.05f);
        Rigidbody rightUpperLegRb = AddRigidbody(rightUpperLeg, totalMass * 0.08f);
        Rigidbody rightLowerLegRb = AddRigidbody(rightLowerLeg, totalMass * 0.05f);

        // Add simple colliders
        AddBoxCollider(hips, new Vector3(0.3f, 0.2f, 0.2f));
        AddBoxCollider(spine, new Vector3(0.25f, 0.2f, 0.15f));
        if (chest != null || upperChest != null) AddBoxCollider(chest ?? upperChest, new Vector3(0.3f, 0.25f, 0.2f));
        AddSphereCollider(head, 0.1f);

        AddCapsuleCollider(leftUpperArm, 0.04f, 0.25f);
        AddCapsuleCollider(leftLowerArm, 0.035f, 0.25f);
        AddCapsuleCollider(rightUpperArm, 0.04f, 0.25f);
        AddCapsuleCollider(rightLowerArm, 0.035f, 0.25f);

        AddCapsuleCollider(leftUpperLeg, 0.06f, 0.4f);
        AddCapsuleCollider(leftLowerLeg, 0.05f, 0.35f);
        AddCapsuleCollider(rightUpperLeg, 0.06f, 0.4f);
        AddCapsuleCollider(rightLowerLeg, 0.05f, 0.35f);

        // Add joints - connect child to parent
        AddJoint(spineRb, hipsRb, 40f);
        if (chestRb != null) AddJoint(chestRb, spineRb, 30f);
        AddJoint(headRb, chestRb ?? spineRb, 50f);

        AddJoint(leftUpperArmRb, chestRb ?? spineRb, 80f);
        AddJoint(leftLowerArmRb, leftUpperArmRb, 120f);
        AddJoint(rightUpperArmRb, chestRb ?? spineRb, 80f);
        AddJoint(rightLowerArmRb, rightUpperArmRb, 120f);

        AddJoint(leftUpperLegRb, hipsRb, 60f);
        AddJoint(leftLowerLegRb, leftUpperLegRb, 100f);
        AddJoint(rightUpperLegRb, hipsRb, 60f);
        AddJoint(rightLowerLegRb, rightUpperLegRb, 100f);

        // Save prefab
        PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Resources/Zombie.prefab");
        DestroyImmediate(instance);

        Debug.Log("Zombie ragdoll setup complete!");
    }

    static Rigidbody AddRigidbody(Transform bone, float mass)
    {
        if (bone == null) return null;

        Rigidbody rb = bone.GetComponent<Rigidbody>();
        if (rb == null) rb = bone.gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        return rb;
    }

    static void AddBoxCollider(Transform bone, Vector3 size)
    {
        if (bone == null) return;
        BoxCollider col = bone.gameObject.AddComponent<BoxCollider>();
        col.size = size;
        col.enabled = false;
    }

    static void AddSphereCollider(Transform bone, float radius)
    {
        if (bone == null) return;
        SphereCollider col = bone.gameObject.AddComponent<SphereCollider>();
        col.radius = radius;
        col.enabled = false;
    }

    static void AddCapsuleCollider(Transform bone, float radius, float height)
    {
        if (bone == null) return;
        CapsuleCollider col = bone.gameObject.AddComponent<CapsuleCollider>();
        col.radius = radius;
        col.height = height;
        col.direction = 0; // X axis for limbs
        col.enabled = false;
    }

    static void AddJoint(Rigidbody rb, Rigidbody connectedBody, float swingLimit)
    {
        if (rb == null || connectedBody == null) return;

        CharacterJoint joint = rb.gameObject.AddComponent<CharacterJoint>();
        joint.connectedBody = connectedBody;
        joint.enablePreprocessing = false;

        SoftJointLimit limit = new SoftJointLimit();
        limit.limit = swingLimit;
        joint.swing1Limit = limit;
        joint.swing2Limit = limit;

        SoftJointLimit twistLimit = new SoftJointLimit();
        twistLimit.limit = swingLimit * 0.5f;
        joint.lowTwistLimit = twistLimit;

        twistLimit.limit = swingLimit * 0.5f;
        joint.highTwistLimit = twistLimit;
    }
}
#endif
