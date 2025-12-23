using UnityEngine;

public class PlayerKick : MonoBehaviour
{
    [Header("Kick Settings")]
    public float kickRange = 3f;   // AOE radius around player
    public float kickForce = 80f;  // Force applied to ragdoll
    public float kickCooldown = 1.5f;
    public float kickDelay = 1.0f; // When the kick actually connects (during jump)
    public float kickDuration = 1.3f; // How long the kick animation lasts (can't move during this)

    [Header("Audio")]
    public AudioClip kickSound;
    public AudioClip kickHitSound;

    private Animator animator;
    private AudioSource audioSource;
    private float nextKickTime;
    private bool kickPending;
    private float kickConnectTime;
    private float kickEndTime;

    // Public property for other scripts to check
    public bool IsKicking => Time.time < kickEndTime;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        Debug.Log($"[PlayerKick] Started. Animator found: {animator != null}");
    }

    void Update()
    {
        // Find animator if not set (character spawns dynamically)
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // F to kick
        if (Input.GetKeyDown(KeyCode.F))
        {
            Debug.Log($"[PlayerKick] F pressed! CanKick: {Time.time >= nextKickTime}, Animator: {animator != null}");
            if (Time.time >= nextKickTime)
            {
                StartKick();
            }
        }

        // Check if kick should connect
        if (kickPending && Time.time >= kickConnectTime)
        {
            kickPending = false;
            PerformKick();
        }
    }

    void StartKick()
    {
        nextKickTime = Time.time + kickCooldown;
        kickPending = true;
        kickConnectTime = Time.time + kickDelay;
        kickEndTime = Time.time + kickDuration;

        // Trigger animation
        if (animator != null)
        {
            // Check if parameter exists
            bool hasKick = false;
            foreach (var param in animator.parameters)
            {
                if (param.name == "Kick")
                {
                    hasKick = true;
                    break;
                }
            }
            Debug.Log($"[PlayerKick] Animator has Kick param: {hasKick}");

            animator.SetTrigger("Kick");
        }

        // Play kick sound
        if (kickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(kickSound);
        }

        Debug.Log("[Kick] Kick started!");
    }

    void PerformKick()
    {
        // Find all zombies in AOE radius
        Collider[] colliders = Physics.OverlapSphere(transform.position, kickRange);
        bool hitSomething = false;

        foreach (Collider col in colliders)
        {
            // Check if it's a zombie
            ZombieHealth zombieHealth = col.GetComponent<ZombieHealth>();
            if (zombieHealth == null)
                zombieHealth = col.GetComponentInParent<ZombieHealth>();

            if (zombieHealth == null || zombieHealth.IsDead)
                continue;

            // Direction from player to zombie (outward)
            Vector3 directionToZombie = (col.transform.position - transform.position).normalized;
            if (directionToZombie == Vector3.zero)
                directionToZombie = transform.forward;

            // Launch zombie outward!
            hitSomething = true;
            LaunchZombie(zombieHealth, directionToZombie);
        }

        if (hitSomething && kickHitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(kickHitSound);
        }
    }

    void LaunchZombie(ZombieHealth zombieHealth, Vector3 direction)
    {
        Transform zombie = zombieHealth.transform;

        // Disable AI (same as death)
        ZombieAI ai = zombie.GetComponent<ZombieAI>();
        if (ai != null) ai.enabled = false;

        // Disable main collider (same as death)
        CapsuleCollider mainCol = zombie.GetComponent<CapsuleCollider>();
        if (mainCol != null) mainCol.enabled = false;

        // Disable NavMeshAgent (same as death)
        UnityEngine.AI.NavMeshAgent agent = zombie.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        // Get ragdoll component
        ZombieRagdoll ragdoll = zombie.GetComponent<ZombieRagdoll>();

        if (ragdoll != null)
        {
            // Calculate force - outward from player and slightly up
            Vector3 forceDir = (direction + Vector3.up * 0.3f).normalized;
            Vector3 force = forceDir * kickForce;

            // Hit point is zombie's chest area
            Vector3 hitPoint = zombie.position + Vector3.up * 1f;

            // Enable ragdoll with force
            ragdoll.EnableRagdollWithForce(force, hitPoint);

            Debug.Log($"[Kick] Ragdolled zombie with force {kickForce}!");
        }

        // Destroy after a few seconds
        Destroy(zombie.gameObject, 5f);
    }

    void OnDrawGizmosSelected()
    {
        // Draw AOE kick range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, kickRange);
    }
}
