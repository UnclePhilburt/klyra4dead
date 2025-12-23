using UnityEngine;

public class ZombieRagdoll : MonoBehaviour
{
    private Rigidbody[] ragdollBodies;
    private Collider[] ragdollColliders;
    private Animator animator;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();

        // Get all rigidbodies in children (ragdoll bones)
        ragdollBodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        // Disable ragdoll at start
        SetRagdollEnabled(false);
    }

    public void SetRagdollEnabled(bool enabled)
    {
        foreach (Rigidbody rb in ragdollBodies)
        {
            rb.isKinematic = !enabled;
            rb.useGravity = enabled;
        }

        foreach (Collider col in ragdollColliders)
        {
            // Skip the main capsule collider on root
            if (col.gameObject == gameObject) continue;
            col.enabled = enabled;
        }

        if (animator != null)
        {
            animator.enabled = !enabled;
        }
    }

    public void EnableRagdoll()
    {
        SetRagdollEnabled(true);
    }

    public void EnableRagdollWithForce(Vector3 force, Vector3 hitPoint)
    {
        SetRagdollEnabled(true);

        // Find the hit rigidbody and apply force there
        Rigidbody hitRb = null;
        float nearestDist = float.MaxValue;

        foreach (Rigidbody rb in ragdollBodies)
        {
            float dist = Vector3.Distance(rb.position, hitPoint);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                hitRb = rb;
            }
        }

        if (hitRb != null)
        {
            hitRb.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
        }
    }

    // Launch the entire ragdoll with force (for kicks, explosions, etc.)
    public void LaunchRagdoll(Vector3 force)
    {
        SetRagdollEnabled(true);

        // Apply force to all rigidbodies for a big launch
        foreach (Rigidbody rb in ragdollBodies)
        {
            rb.AddForce(force, ForceMode.Impulse);
        }
    }
}
