using UnityEngine;
using Photon.Pun;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Movement")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float rotationSpeed = 5f;

    [Header("Detection")]
    public float detectionRange = 20f;
    public float attackRange = 2f;
    public float loseTargetRange = 30f;
    public LayerMask playerLayer;

    [Header("Attack")]
    public float attackDamage = 20f;
    public float attackCooldown = 1.5f;

    [Header("Audio - Ambient")]
    public AudioClip[] idleSounds;         // Groans, breathing while idle/wandering
    public float idleSoundInterval = 5f;
    [Range(0f, 1f)] public float idleVolume = 0.6f;

    [Header("Audio - Alert/Chase")]
    public AudioClip[] alertSounds;        // Screams when first spotting player
    public AudioClip[] chaseSounds;        // Growls/screams while chasing
    public float chaseSoundInterval = 3f;
    [Range(0f, 1f)] public float chaseVolume = 0.8f;

    [Header("Audio - Combat")]
    public AudioClip[] attackSounds;       // Swing/lunge sounds
    public AudioClip[] hitSounds;          // When hitting player
    [Range(0f, 1f)] public float attackVolume = 1f;

    [Header("Audio - Damage/Death")]
    public AudioClip[] hurtSounds;         // When taking damage
    public AudioClip[] deathSounds;        // Death screams
    [Range(0f, 1f)] public float hurtVolume = 0.8f;

    // State
    public enum ZombieState { Idle, Wandering, Chasing, Attacking }
    public ZombieState CurrentState { get; private set; }
    public Transform CurrentTarget { get; private set; }

    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource audioSource;
    private ZombieHealth health;

    private float nextAttackTime;
    private float nextIdleSoundTime;
    private float nextChaseSoundTime;
    private float wanderTimer;
    private Vector3 wanderDestination;
    private bool hasPlayedAlertSound;

    // Network sync
    private Vector3 networkPosition;
    private Quaternion networkRotation;

    // Flanking behavior
    private float flankAngle;
    private float flankDistance = 2f;
    private float nextFlankRecalcTime;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        audioSource = GetComponent<AudioSource>();
        health = GetComponent<ZombieHealth>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.maxDistance = 20f;
        }

        if (agent != null)
        {
            agent.speed = walkSpeed;
        }

        CurrentState = ZombieState.Idle;
        networkPosition = transform.position;
        networkRotation = transform.rotation;

        // Each zombie gets a random flank angle to approach from
        flankAngle = Random.Range(0f, 360f);

        Debug.Log($"[ZombieAI] Zombie spawned. Detection range: {detectionRange}. IsLocal: {!PhotonNetwork.IsConnected || photonView.IsMine}");
    }

    void Update()
    {
        if (health != null && health.IsDead)
        {
            if (agent != null) agent.enabled = false;
            return;
        }

        bool isLocal = !PhotonNetwork.IsConnected || photonView.IsMine;

        if (isLocal)
        {
            UpdateAI();
            UpdateAnimator();
            PlayIdleSounds();
        }
        else
        {
            // Interpolate network position
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * 10f);
        }
    }

    void UpdateAI()
    {
        // Find nearest player if no target
        if (CurrentTarget == null || !IsValidTarget(CurrentTarget))
        {
            FindNearestPlayer();
        }

        // Check if target is too far
        if (CurrentTarget != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, CurrentTarget.position);
            if (distanceToTarget > loseTargetRange)
            {
                CurrentTarget = null;
            }
        }

        // State machine
        switch (CurrentState)
        {
            case ZombieState.Idle:
                UpdateIdle();
                break;
            case ZombieState.Wandering:
                UpdateWandering();
                break;
            case ZombieState.Chasing:
                UpdateChasing();
                break;
            case ZombieState.Attacking:
                UpdateAttacking();
                break;
        }
    }

    void UpdateIdle()
    {
        hasPlayedAlertSound = false; // Reset for next chase

        if (CurrentTarget != null)
        {
            Debug.Log($"[ZombieAI] Starting chase! Target: {CurrentTarget.name}");
            CurrentState = ZombieState.Chasing;
            PlayAlertSound();

            // Trigger combat music
            if (CombatMusicManager.Instance != null)
                CombatMusicManager.Instance.TriggerCombat();

            return;
        }

        // Randomly start wandering
        if (Random.value < 0.01f)
        {
            StartWandering();
        }
    }

    void UpdateWandering()
    {
        hasPlayedAlertSound = false; // Reset for next chase

        if (CurrentTarget != null)
        {
            CurrentState = ZombieState.Chasing;
            PlayAlertSound();

            // Trigger combat music
            if (CombatMusicManager.Instance != null)
                CombatMusicManager.Instance.TriggerCombat();

            return;
        }

        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0 || agent.remainingDistance < 0.5f)
        {
            CurrentState = ZombieState.Idle;
        }
    }

    void UpdateChasing()
    {
        if (CurrentTarget == null)
        {
            CurrentState = ZombieState.Idle;
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, CurrentTarget.position);

        if (distanceToTarget <= attackRange)
        {
            CurrentState = ZombieState.Attacking;
            if (agent != null) agent.SetDestination(transform.position);
            return;
        }

        // Recalculate flank angle occasionally to keep them moving
        if (Time.time >= nextFlankRecalcTime)
        {
            // Shift angle slightly to create dynamic movement
            flankAngle += Random.Range(-30f, 30f);
            nextFlankRecalcTime = Time.time + Random.Range(2f, 4f);
        }

        // Calculate flanking position around the player
        Vector3 targetPos = CurrentTarget.position;

        // When far away, go more direct. When close, spread out more
        float currentFlankDist = distanceToTarget > 8f ? 0f : flankDistance;

        if (currentFlankDist > 0)
        {
            // Get offset position around player based on this zombie's flank angle
            Vector3 offset = Quaternion.Euler(0, flankAngle, 0) * Vector3.forward * currentFlankDist;
            targetPos += offset;
        }

        // Chase the target (with flanking offset)
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = runSpeed;
            agent.SetDestination(targetPos);
        }
        else if (agent != null && !agent.isOnNavMesh)
        {
            Debug.LogWarning("[ZombieAI] Not on NavMesh! Make sure to bake NavMesh (add NavMeshSurface component)");
        }

        // Face target (always face the actual player, not the flank position)
        Vector3 direction = (CurrentTarget.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Play chase sounds periodically
        PlayChaseSounds();
    }

    void UpdateAttacking()
    {
        if (CurrentTarget == null)
        {
            CurrentState = ZombieState.Idle;
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, CurrentTarget.position);

        if (distanceToTarget > attackRange * 1.5f)
        {
            CurrentState = ZombieState.Chasing;
            return;
        }

        // Face target
        Vector3 direction = (CurrentTarget.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * 2f * Time.deltaTime);
        }

        // Attack
        if (Time.time >= nextAttackTime)
        {
            Attack();
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    void Attack()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        // Play attack sound
        PlaySound(attackSounds, attackVolume);

        // Deal damage after a short delay (sync with animation)
        Invoke(nameof(DealDamage), 0.5f);
    }

    void DealDamage()
    {
        if (CurrentTarget == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, CurrentTarget.position);
        if (distanceToTarget <= attackRange * 1.5f)
        {
            PlayerHealth playerHealth = CurrentTarget.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.Damage(attackDamage, photonView.ViewID);
                Debug.Log($"[ZombieAI] Attacked player for {attackDamage} damage");

                // Play hit sound
                PlaySound(hitSounds, attackVolume);
            }
        }
    }

    void StartWandering()
    {
        CurrentState = ZombieState.Wandering;
        wanderTimer = Random.Range(3f, 8f);

        // Find random point on NavMesh
        Vector3 randomDirection = Random.insideUnitSphere * 10f;
        randomDirection += transform.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, 10f, NavMesh.AllAreas))
        {
            wanderDestination = hit.position;
            if (agent != null && agent.enabled)
            {
                agent.speed = walkSpeed;
                agent.SetDestination(wanderDestination);
            }
        }
    }

    void FindNearestPlayer()
    {
        // Find all colliders in range (use layer if set, otherwise check all)
        Collider[] colliders;
        if (playerLayer.value != 0)
        {
            colliders = Physics.OverlapSphere(transform.position, detectionRange, playerLayer);
        }
        else
        {
            colliders = Physics.OverlapSphere(transform.position, detectionRange);
        }

        float nearestDistance = float.MaxValue;
        Transform nearestPlayer = null;

        // Debug - uncomment to see detection status
        // Debug.Log($"[ZombieAI] Searching... found {colliders.Length} colliders in range {detectionRange}");

        foreach (Collider col in colliders)
        {
            // Check for PlayerHealth component (works regardless of layer)
            PlayerHealth playerHealth = col.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = col.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null && !playerHealth.IsDead && !playerHealth.IsDowned)
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPlayer = playerHealth.transform;
                }
            }
        }

        // Also try to find by tag as fallback
        if (nearestPlayer == null)
        {
            try
            {
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                foreach (GameObject player in players)
                {
                    float distance = Vector3.Distance(transform.position, player.transform.position);
                    if (distance < detectionRange && distance < nearestDistance)
                    {
                        PlayerHealth ph = player.GetComponent<PlayerHealth>();
                        if (ph == null || (!ph.IsDead && !ph.IsDowned))
                        {
                            nearestDistance = distance;
                            nearestPlayer = player.transform;
                        }
                    }
                }
            }
            catch (UnityException)
            {
                // Tag doesn't exist
            }
        }

        // Last resort - find ThirdPersonMotor or CharacterController
        if (nearestPlayer == null)
        {
            ThirdPersonMotor[] motors = FindObjectsByType<ThirdPersonMotor>(FindObjectsSortMode.None);
            foreach (var motor in motors)
            {
                float distance = Vector3.Distance(transform.position, motor.transform.position);
                if (distance < detectionRange && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPlayer = motor.transform;
                }
            }
        }

        if (nearestPlayer != null)
        {
            CurrentTarget = nearestPlayer;
            Debug.Log($"[ZombieAI] Found target: {nearestPlayer.name} at distance {nearestDistance:F1}");
        }
        else
        {
            // Only log occasionally to avoid spam
            if (Random.value < 0.01f)
            {
                ThirdPersonMotor[] motors = FindObjectsByType<ThirdPersonMotor>(FindObjectsSortMode.None);
                Debug.Log($"[ZombieAI] No target found. ThirdPersonMotors in scene: {motors.Length}");
            }
        }
    }

    bool IsValidTarget(Transform target)
    {
        if (target == null) return false;

        PlayerHealth health = target.GetComponent<PlayerHealth>();
        if (health == null) return true; // No health component = always valid

        return !health.IsDead;
    }

    public void SetTarget(Transform target)
    {
        CurrentTarget = target;
        if (CurrentState == ZombieState.Idle || CurrentState == ZombieState.Wandering)
        {
            CurrentState = ZombieState.Chasing;
        }
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        float speed = agent != null ? agent.velocity.magnitude : 0f;
        animator.SetFloat("Speed", speed);
        animator.SetBool("IsChasing", CurrentState == ZombieState.Chasing);
    }

    void PlayIdleSounds()
    {
        // Only play idle sounds when not chasing/attacking
        if (CurrentState == ZombieState.Chasing || CurrentState == ZombieState.Attacking) return;

        if (Time.time >= nextIdleSoundTime && idleSounds.Length > 0 && audioSource != null)
        {
            PlaySound(idleSounds, idleVolume);
            nextIdleSoundTime = Time.time + idleSoundInterval + Random.Range(-1f, 2f);
        }
    }

    void PlayAlertSound()
    {
        if (hasPlayedAlertSound) return;
        hasPlayedAlertSound = true;

        if (alertSounds.Length > 0)
        {
            PlaySound(alertSounds, chaseVolume);
        }
        else if (chaseSounds.Length > 0)
        {
            // Fallback to chase sound if no alert sounds
            PlaySound(chaseSounds, chaseVolume);
        }

        nextChaseSoundTime = Time.time + chaseSoundInterval;
    }

    void PlayChaseSounds()
    {
        if (Time.time >= nextChaseSoundTime && chaseSounds.Length > 0)
        {
            PlaySound(chaseSounds, chaseVolume);
            nextChaseSoundTime = Time.time + chaseSoundInterval + Random.Range(-0.5f, 1f);
        }
    }

    // Public methods for ZombieHealth to call
    public void PlayHurtSound()
    {
        PlaySound(hurtSounds, hurtVolume);
    }

    public void PlayDeathSound()
    {
        PlaySound(deathSounds, hurtVolume);
    }

    // Helper to play random sound from array
    void PlaySound(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0 || audioSource == null) return;
        audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext((int)CurrentState);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            CurrentState = (ZombieState)(int)stream.ReceiveNext();
        }
    }

    void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
