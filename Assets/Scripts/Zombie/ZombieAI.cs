using UnityEngine;
using Photon.Pun;
using UnityEngine.AI;

/// <summary>
/// Zombie AI with sensory system: Almost blind but excellent hearing.
/// Players can sneak past zombies if they're quiet.
/// </summary>
public class ZombieAI : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Movement")]
    public float walkSpeed = 1.5f;
    public float investigateSpeed = 2.5f;
    public float chaseSpeed = 4.5f;
    public float rotationSpeed = 5f;

    [Header("Vision (Very Limited)")]
    [Tooltip("Zombies can only SEE players within this short range")]
    public float visionRange = 6f;
    [Tooltip("Field of view in degrees (frontal cone)")]
    public float visionAngle = 90f;
    [Tooltip("Layer mask for line of sight checks")]
    public LayerMask obstacleMask;

    [Header("Hearing (Primary Sense)")]
    [Tooltip("Zombies remember sounds for this long before giving up")]
    public float soundMemoryDuration = 8f;
    [Tooltip("How long to investigate a sound location")]
    public float investigateDuration = 5f;

    [Header("Attack")]
    public float attackRange = 2f;
    public float attackDamage = 20f;
    public float attackCooldown = 1.5f;
    public float loseTargetRange = 25f;

    [Header("Audio - Ambient")]
    public AudioClip[] idleSounds;
    public float idleSoundInterval = 8f;
    [Range(0f, 1f)] public float idleVolume = 0.4f;

    [Header("Audio - Alert/Chase")]
    public AudioClip[] alertSounds;
    public AudioClip[] chaseSounds;
    public float chaseSoundInterval = 3f;
    [Range(0f, 1f)] public float chaseVolume = 0.8f;

    [Header("Audio - Combat")]
    public AudioClip[] attackSounds;
    public AudioClip[] hitSounds;
    [Range(0f, 1f)] public float attackVolume = 1f;

    [Header("Audio - Damage/Death")]
    public AudioClip[] hurtSounds;
    public AudioClip[] deathSounds;
    [Range(0f, 1f)] public float hurtVolume = 0.8f;

    [Header("Audio - Light Pain")]
    public AudioClip[] lightPainSounds;
    [Range(0f, 1f)] public float lightPainVolume = 0.9f;

    [Header("Light Avoidance - #1 PRIORITY")]
    [Tooltip("If true, this zombie ignores flashlights and light sources")]
    public bool fearless = false;
    [Tooltip("Light level that makes zombie uncomfortable (start retreating)")]
    public float lightDiscomfortThreshold = 0.3f;
    [Tooltip("Light level that causes pain/fleeing")]
    public float lightPainThreshold = 0.6f;
    [Tooltip("How far from light sources zombies try to stay")]
    public float lightSafeDistance = 8f;
    [Tooltip("Extra buffer distance - zombies stay THIS far from light edge")]
    public float lightBufferDistance = 4f;
    [Tooltip("Speed when fleeing light (faster than normal)")]
    public float lightFleeSpeed = 6f;
    [Tooltip("How often to check light level")]
    public float lightCheckInterval = 0.2f;

    // State
    public enum ZombieState { Idle, Wandering, Investigating, Chasing, Attacking, FleeingLight, Stalking }
    
    private ZombieState _currentState;
    public ZombieState CurrentState 
    { 
        get => _currentState;
        private set
        {
            bool wasChasing = _currentState == ZombieState.Chasing || _currentState == ZombieState.Attacking;
            bool willChase = value == ZombieState.Chasing || value == ZombieState.Attacking;
            
            if (wasChasing && !willChase) chasingCount--;
            else if (!wasChasing && willChase) chasingCount++;
            
            _currentState = value;
        }
    }

    // Speed types for zombie variants
    public enum ZombieSpeed { Walker, Regular, Sprinter }
    public ZombieSpeed SpeedType { get; private set; } = ZombieSpeed.Regular;

    // Static tracking for combat music
    private static int chasingCount = 0;
    public static int GetChasingCount() => chasingCount;

    public Transform CurrentTarget { get; private set; }

    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource audioSource;
    private ZombieHealth health;

    // Investigation (heard a noise)
    private Vector3 investigatePosition;
    private float investigateTimer;
    private float lastHeardTime;
    private Transform lastHeardSource;

    // Timers
    private float nextAttackTime;
    private float nextIdleSoundTime;
    private float nextChaseSoundTime;
    private float wanderTimer;
    private bool hasPlayedAlertSound;
    
    // Path recalculation throttling (performance)
    private float nextPathRecalcTime;
    private const float PATH_RECALC_INTERVAL = 0.4f;
    private Vector3 lastPathTarget;

    // Network sync
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private float networkSpeed;

    // Flanking
    private float flankAngle;
    private float flankDistance = 2f;
    private float nextFlankRecalcTime;

    // Light avoidance
    private float currentLightLevel;
    private float distanceToNearestLight;
    private float nextLightCheckTime;
    private ZombieState stateBeforeLight;
    private Transform targetBeforeLight;
    private Vector3 fleeDestination;
    private bool isFleeing;
    private bool isStationary = false;
    private Vector3 homePosition;

    // Object pooling support
    public bool IsPooled { get; set; }

    // Cached collider array for NonAlloc physics (prevents GC)
    private static Collider[] nearbyCollidersCache = new Collider[20];

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
        flankAngle = Random.Range(0f, 360f);

        // Stagger idle sound timing so they don't all groan at once
        nextIdleSoundTime = Time.time + Random.Range(2f, idleSoundInterval);

        // Stagger light check timing per zombie
        nextLightCheckTime = Time.time + Random.Range(0f, lightCheckInterval);

        // Register with manager for culling
        if (ZombieManager.Instance != null)
        {
            ZombieManager.Instance.RegisterZombie(this);
        }
    }

    void OnDestroy()
    {
        // Decrement chasing count if we were chasing
        if (_currentState == ZombieState.Chasing || _currentState == ZombieState.Attacking)
        {
            chasingCount--;
        }
        
        // Unregister from manager
        if (ZombieManager.Instance != null)
        {
            ZombieManager.Instance.UnregisterZombie(this);
        }
    }

    void Update()
    {
        if (health != null && health.IsDead)
        {
            if (agent != null) agent.enabled = false;
            return;
        }

        bool isLocal = !PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;

        if (isLocal)
        {
            // Distance-based culling: check if we should update this frame
            bool shouldUpdate = ZombieManager.Instance == null ||
                                ZombieManager.Instance.ShouldUpdateThisFrame(this);

            if (shouldUpdate)
            {
                // Skip light avoidance on WebGL low performance modes
                bool checkLight = WebGLOptimizer.Instance == null ||
                                  WebGLOptimizer.Instance.EnableLightAvoidance;

                // #1 PRIORITY: Check light FIRST - before any other AI
                if (checkLight)
                {
                    CheckLightStatus();
                }

                // If fleeing light, only do that - nothing else matters
                if (CurrentState == ZombieState.FleeingLight)
                {
                    UpdateFleeingLight();
                }
                else
                {
                    UpdateAI();
                }
            }

            // LOD for animator - skip updates for very distant zombies
            bool updateAnimator = true;
            if (ZombieManager.Instance != null)
            {
                // Very far zombies skip animator updates every other frame
                float deltaMult = ZombieManager.Instance.GetDeltaTimeMultiplier(this);
                if (deltaMult >= 8f && Time.frameCount % 2 != 0)
                {
                    updateAnimator = false;
                }
            }

            if (updateAnimator)
            {
                UpdateAnimator();
            }

            // Ambient sounds can be less frequent
            if (shouldUpdate)
            {
                PlayAmbientSounds();
            }
        }
        else
        {
            // Interpolate network position for remote clients
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * 10f);
            UpdateRemoteAnimator();
        }
    }

    void UpdateAI()
    {
        // Always check for VISUAL detection (very short range)
        CheckVision();

        // State machine
        switch (CurrentState)
        {
            case ZombieState.Idle:
                UpdateIdle();
                break;
            case ZombieState.Wandering:
                UpdateWandering();
                break;
            case ZombieState.Investigating:
                UpdateInvestigating();
                break;
            case ZombieState.Chasing:
                UpdateChasing();
                break;
            case ZombieState.Attacking:
                UpdateAttacking();
                break;
        }
    }

    #region Sensory System

    /// <summary>
    /// Called by NoiseManager when this zombie hears a sound
    /// </summary>
    public void HearNoise(Vector3 noisePosition, Transform source = null)
    {
        // Already chasing someone? Only update if this is our target or closer
        if (CurrentState == ZombieState.Chasing && CurrentTarget != null)
        {
            if (source != null && source == CurrentTarget)
            {
                // Update target position (they made noise)
                return;
            }
            // Ignore other noises while chasing
            return;
        }

        // Already attacking? Ignore
        if (CurrentState == ZombieState.Attacking) return;

        // Remember this noise
        investigatePosition = noisePosition;
        lastHeardTime = Time.time;
        lastHeardSource = source;

        // If idle or wandering, go investigate
        if (CurrentState == ZombieState.Idle || CurrentState == ZombieState.Wandering)
        {
            StartInvestigating(noisePosition);
        }
        else if (CurrentState == ZombieState.Investigating)
        {
            // Update investigation target to new noise
            investigatePosition = noisePosition;
            investigateTimer = investigateDuration;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (agent.isOnNavMesh) agent.SetDestination(noisePosition);
            }
        }
    }

    void StartInvestigating(Vector3 position)
    {
        CurrentState = ZombieState.Investigating;
        investigatePosition = position;
        investigateTimer = investigateDuration;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = investigateSpeed;
            if (agent.isOnNavMesh) agent.SetDestination(position);
        }

        // Play alert sound (quieter than full chase)
        if (alertSounds.Length > 0 && Random.value < 0.5f)
        {
            PlaySound(alertSounds, idleVolume);
        }

        #if UNITY_EDITOR
        Debug.Log($"[ZombieAI] Investigating noise at {position}");
        #endif
    }

    /// <summary>
    /// Check for visual detection - very short range, requires line of sight
    /// Uses NonAlloc physics for zero garbage collection.
    /// </summary>
    void CheckVision()
    {
        // Don't check vision if already have a confirmed target
        if (CurrentTarget != null && CurrentState == ZombieState.Chasing) return;

        // Find potential targets using NonAlloc (no garbage!)
        int numColliders = Physics.OverlapSphereNonAlloc(transform.position, visionRange, nearbyCollidersCache);

        for (int i = 0; i < numColliders; i++)
        {
            Collider col = nearbyCollidersCache[i];
            if (col == null) continue;

            PlayerHealth playerHealth = col.GetComponent<PlayerHealth>();
            if (playerHealth == null) playerHealth = col.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null && !playerHealth.IsDead && !playerHealth.IsDowned)
            {
                // Check if in field of view
                Vector3 dirToPlayer = (playerHealth.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, dirToPlayer);

                if (angle < visionAngle / 2f)
                {
                    // Check line of sight
                    if (!Physics.Linecast(transform.position + Vector3.up, playerHealth.transform.position + Vector3.up, obstacleMask))
                    {
                        // SPOTTED!
                        SpotPlayer(playerHealth.transform);
                        return;
                    }
                }
            }
        }
    }

    void SpotPlayer(Transform player)
    {
        CurrentTarget = player;
        CurrentState = ZombieState.Chasing;

        if (!hasPlayedAlertSound)
        {
            PlayAlertSound();
            hasPlayedAlertSound = true;
        }

        // Trigger combat music
        if (CombatMusicManager.Instance != null)
            CombatMusicManager.Instance.TriggerCombat();

        #if UNITY_EDITOR
        Debug.Log($"[ZombieAI] SPOTTED player at close range!");
        #endif
    }

    #endregion

    #region Light Avoidance - TOP PRIORITY

    void CheckLightStatus()
    {
        // Fearless zombies ignore light completely
        if (fearless) return;


        // Throttle light checks for performance
        if (Time.time < nextLightCheckTime) return;
        nextLightCheckTime = Time.time + lightCheckInterval;

        // Update current light level and distance to nearest light
        currentLightLevel = LightDetection.GetLightLevelAt(transform.position, obstacleMask);
        distanceToNearestLight = LightDetection.GetDistanceToNearestLight(transform.position);

        // Calculate effective danger zone (light range + our buffer)
        bool tooCloseToLight = distanceToNearestLight < (lightSafeDistance + lightBufferDistance);
        bool inPainfulLight = currentLightLevel >= lightPainThreshold;
        bool inUncomfortableLight = currentLightLevel >= lightDiscomfortThreshold;

        // FLEE if in painful light OR too close to any light source
        if ((inPainfulLight || tooCloseToLight) && CurrentState != ZombieState.FleeingLight)
        {
            StartFleeingLight();
        }
        // If just uncomfortable (dim light at edge), back away slowly
        else if (inUncomfortableLight && CurrentState != ZombieState.FleeingLight && CurrentState != ZombieState.Chasing)
        {
            StartFleeingLight();
        }
    }

    void StartFleeingLight()
    {
        // Save current state to resume later
        if (CurrentState != ZombieState.FleeingLight)
        {
            stateBeforeLight = CurrentState;
            targetBeforeLight = CurrentTarget;
        }

        CurrentState = ZombieState.FleeingLight;
        isFleeing = true;

        // Play pain sound
        if (lightPainSounds != null && lightPainSounds.Length > 0)
        {
            PlaySound(lightPainSounds, lightPainVolume);
        }
        else
        {
            PlaySound(hurtSounds, hurtVolume);
        }

        // Find escape destination - go AWAY from light, with extra buffer
        FindDarkEscapePoint();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = lightFleeSpeed;
            if (agent.isOnNavMesh) agent.SetDestination(fleeDestination);
        }

        #if UNITY_EDITOR
        Debug.Log($"[ZombieAI] FLEEING LIGHT! Level: {currentLightLevel:F2}, Dist to light: {distanceToNearestLight:F1}m");
        #endif
    }

    void FindDarkEscapePoint()
    {
        // Get direction away from nearest lights
        Vector3 fleeDirection = LightDetection.GetFleeDirection(transform.position, obstacleMask);

        if (fleeDirection == Vector3.zero)
        {
            // No clear direction, pick random direction away
            fleeDirection = Random.insideUnitSphere;
            fleeDirection.y = 0;
            fleeDirection.Normalize();
        }

        // Try to find a dark spot far from light - go EXTRA far with the buffer
        float escapeDistance = lightSafeDistance + lightBufferDistance + 5f;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 testPos = transform.position + fleeDirection * escapeDistance;

            // Add some randomness to spread zombies out
            testPos += Random.insideUnitSphere * 3f;
            testPos.y = transform.position.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(testPos, out hit, 5f, NavMesh.AllAreas))
            {
                // Check if this spot is actually dark and far from lights
                float testLight = LightDetection.GetLightLevelAt(hit.position, obstacleMask);
                float testDist = LightDetection.GetDistanceToNearestLight(hit.position);

                if (testLight < lightDiscomfortThreshold && testDist > lightSafeDistance + lightBufferDistance)
                {
                    fleeDestination = hit.position;
                    return;
                }
            }

            // Try a different angle
            fleeDirection = Quaternion.Euler(0, 45f, 0) * fleeDirection;
        }

        // Fallback: just go in the flee direction as far as possible
        NavMeshHit fallbackHit;
        Vector3 fallbackPos = transform.position + fleeDirection * (escapeDistance + 10f);
        if (NavMesh.SamplePosition(fallbackPos, out fallbackHit, 10f, NavMesh.AllAreas))
        {
            fleeDestination = fallbackHit.position;
        }
        else
        {
            fleeDestination = transform.position + fleeDirection * escapeDistance;
        }
    }

    void UpdateFleeingLight()
    {
        if (agent == null) return;

        // Re-check light level
        currentLightLevel = LightDetection.GetLightLevelAt(transform.position, obstacleMask);
        distanceToNearestLight = LightDetection.GetDistanceToNearestLight(transform.position);

        bool stillInDanger = currentLightLevel >= lightDiscomfortThreshold ||
                             distanceToNearestLight < (lightSafeDistance + lightBufferDistance);

        // If still too close to light, keep fleeing
        if (stillInDanger)
        {
            // If we've reached our destination but still in light, find new escape point
            if (agent.isOnNavMesh && agent.remainingDistance < 1f)
            {
                FindDarkEscapePoint();
                if (agent.enabled && agent.isOnNavMesh)
                {
                    if (agent.isOnNavMesh) agent.SetDestination(fleeDestination);
                }
            }

            // Make sure we're moving fast
            agent.speed = lightFleeSpeed;
        }
        else
        {
            // We're safe! Resume previous behavior
            isFleeing = false;
            CurrentState = stateBeforeLight;
            CurrentTarget = targetBeforeLight;

            // Reset speed
            if (agent != null)
            {
                switch (CurrentState)
                {
                    case ZombieState.Chasing:
                        agent.speed = chaseSpeed;
                        break;
                    case ZombieState.Investigating:
                        agent.speed = investigateSpeed;
                        break;
                    default:
                        agent.speed = walkSpeed;
                        break;
                }
            }

            #if UNITY_EDITOR
            Debug.Log($"[ZombieAI] Escaped light! Resuming {CurrentState} state. Dist to light: {distanceToNearestLight:F1}m");
            #endif
        }
    }

    /// <summary>
    /// Check if a position is safe (dark and far from lights)
    /// </summary>
    bool IsPositionSafe(Vector3 position)
    {
        float lightLevel = LightDetection.GetLightLevelAt(position, obstacleMask);
        float distToLight = LightDetection.GetDistanceToNearestLight(position);
        return lightLevel < lightDiscomfortThreshold && distToLight > (lightSafeDistance + lightBufferDistance);
    }

    #endregion

    #region State Updates

    void UpdateIdle()
    {
        hasPlayedAlertSound = false;

        // Small chance to wander
        if (Random.value < 0.005f)
        {
            StartWandering();
        }
    }

    void UpdateWandering()
    {
        hasPlayedAlertSound = false;

        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0 || (agent != null && agent.isOnNavMesh && agent.remainingDistance < 0.5f))
        {
            CurrentState = ZombieState.Idle;
            if (agent != null) agent.speed = walkSpeed;
        }
    }

    void UpdateInvestigating()
    {
        investigateTimer -= Time.deltaTime;

        // Check if we've reached the investigation point
        if (agent != null && agent.isOnNavMesh && agent.remainingDistance < 2f)
        {
            // Look around
            transform.Rotate(0, Random.Range(-30f, 30f) * Time.deltaTime, 0);
        }

        // Give up after timer expires
        if (investigateTimer <= 0)
        {
            // No one here... go back to idle
            CurrentState = ZombieState.Idle;
            if (agent != null) agent.speed = walkSpeed;
            #if UNITY_EDITOR
            Debug.Log("[ZombieAI] Nothing here... returning to idle");
            #endif
        }

        // If we hear another noise, it gets updated via HearNoise()
    }

    void UpdateChasing()
    {
        if (CurrentTarget == null || !IsValidTarget(CurrentTarget))
        {
            // Lost target
            CurrentTarget = null;

            // If we remember where we last heard them, investigate there
            if (Time.time - lastHeardTime < soundMemoryDuration && lastHeardSource != null)
            {
                StartInvestigating(lastHeardSource.position);
            }
            else
            {
                CurrentState = ZombieState.Idle;
            }
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, CurrentTarget.position);

        // Lost them? (too far)
        if (distanceToTarget > loseTargetRange)
        {
            // Lost visual, but remember where they were
            StartInvestigating(CurrentTarget.position);
            CurrentTarget = null;
            return;
        }

        // In attack range?
        if (distanceToTarget <= attackRange)
        {
            CurrentState = ZombieState.Attacking;
            if (agent != null && agent.isOnNavMesh) agent.SetDestination(transform.position);
            return;
        }

        // Chase with flanking behavior
        UpdateChaseMovement();
        PlayChaseSounds();
    }

    void UpdateChaseMovement()
    {
        if (CurrentTarget == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, CurrentTarget.position);

        // Recalculate flank angle occasionally
        if (Time.time >= nextFlankRecalcTime)
        {
            flankAngle += Random.Range(-30f, 30f);
            nextFlankRecalcTime = Time.time + Random.Range(2f, 4f);
        }

        Vector3 targetPos = CurrentTarget.position;

        // Apply flanking when close
        if (distanceToTarget < 8f && distanceToTarget > attackRange * 2)
        {
            Vector3 offset = Quaternion.Euler(0, flankAngle, 0) * Vector3.forward * flankDistance;
            targetPos += offset;
        }

        // Throttle path recalculation for performance
        bool shouldRecalcPath = Time.time >= nextPathRecalcTime || 
                                 Vector3.Distance(targetPos, lastPathTarget) > 3f;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = chaseSpeed;
            if (shouldRecalcPath)
            {
                if (agent.isOnNavMesh) agent.SetDestination(targetPos);
                lastPathTarget = targetPos;
                nextPathRecalcTime = Time.time + PATH_RECALC_INTERVAL;
            }
        }

        // Face target
        Vector3 direction = (CurrentTarget.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
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

    #endregion

    #region Actions

    void StartWandering()
    {
        CurrentState = ZombieState.Wandering;
        wanderTimer = Random.Range(5f, 12f);

        // Try to find a dark wander destination
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * 8f;
            randomDirection += transform.position;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, 10f, NavMesh.AllAreas))
            {
                // Only wander to dark areas far from lights
                if (IsPositionSafe(hit.position))
                {
                    if (agent != null && agent.enabled && agent.isOnNavMesh)
                    {
                        agent.speed = walkSpeed;
                        if (agent.isOnNavMesh) agent.SetDestination(hit.position);
                    }
                    return;
                }
            }
        }

        // Fallback: stay in place if no safe spot found
        CurrentState = ZombieState.Idle;
    }

    void Attack()
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        PlaySound(attackSounds, attackVolume);

        // Make noise when attacking (alerts other zombies)
        if (NoiseManager.Instance != null)
        {
            NoiseManager.Instance.MakeNoise(transform.position, NoiseManager.NoiseType.ZombieDeath);
        }

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
                playerHealth.Damage(attackDamage, photonView != null ? photonView.ViewID : -1);
                PlaySound(hitSounds, attackVolume);
            }
        }
    }

    #endregion

    #region Audio

    void PlayAmbientSounds()
    {
        // Only play idle sounds when not actively chasing/attacking
        if (CurrentState == ZombieState.Chasing || CurrentState == ZombieState.Attacking) return;

        if (Time.time >= nextIdleSoundTime && idleSounds.Length > 0)
        {
            PlaySound(idleSounds, idleVolume);
            nextIdleSoundTime = Time.time + idleSoundInterval + Random.Range(-2f, 3f);
        }
    }

    void PlayAlertSound()
    {
        if (alertSounds.Length > 0)
        {
            PlaySound(alertSounds, chaseVolume);
        }
        else if (chaseSounds.Length > 0)
        {
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

    public void PlayHurtSound() { PlaySound(hurtSounds, hurtVolume); }
    public void PlayDeathSound() { PlaySound(deathSounds, hurtVolume); }

    void PlaySound(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0 || audioSource == null) return;
        audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
    }

    #endregion

    #region Utility

    bool IsValidTarget(Transform target)
    {
        if (target == null) return false;
        PlayerHealth ph = target.GetComponent<PlayerHealth>();
        return ph == null || (!ph.IsDead && !ph.IsDowned);
    }

    public void SetTarget(Transform target)
    {
        CurrentTarget = target;
        if (target != null)
        {
            CurrentState = ZombieState.Chasing;
            if (!hasPlayedAlertSound)
            {
                PlayAlertSound();
                hasPlayedAlertSound = true;
            }
        }
    }

    public void SetSpeedType(ZombieSpeed speedType)
    {
        SpeedType = speedType;
        switch (speedType)
        {
            case ZombieSpeed.Walker:
                walkSpeed = 1.0f;
                investigateSpeed = 1.5f;
                chaseSpeed = 2.5f;
                break;
            case ZombieSpeed.Regular:
                walkSpeed = 1.5f;
                investigateSpeed = 2.5f;
                chaseSpeed = 4.5f;
                break;
            case ZombieSpeed.Sprinter:
                walkSpeed = 2.0f;
                investigateSpeed = 3.5f;
                chaseSpeed = 7.0f;
                break;
        }
        if (agent != null) agent.speed = walkSpeed;
    }

    public void SetWanderer()
    {
        CurrentState = ZombieState.Wandering;
        wanderTimer = Random.Range(10f, 20f);
    }

    public void SetStationary(Vector3 homePos)
    {
        homePosition = homePos;
        isStationary = true;
        CurrentState = ZombieState.Idle;
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = true;
    }

    public void SetHomePosition(Vector3 position)
    {
        investigatePosition = position;
    }

    void UpdateAnimator()
    {
        if (animator == null) return;
        float speed = agent != null && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
        animator.SetFloat("Speed", speed);
        animator.SetBool("IsChasing", CurrentState == ZombieState.Chasing || CurrentState == ZombieState.Attacking);
    }

    void UpdateRemoteAnimator()
    {
        if (animator == null) return;
        animator.SetFloat("Speed", networkSpeed);
        animator.SetBool("IsChasing", CurrentState == ZombieState.Chasing || CurrentState == ZombieState.Attacking);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext((int)CurrentState);
            stream.SendNext(agent != null && agent.isOnNavMesh ? agent.velocity.magnitude : 0f);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            CurrentState = (ZombieState)(int)stream.ReceiveNext();
            networkSpeed = (float)stream.ReceiveNext();
        }
    }

    #endregion

    /// <summary>
    /// Reset AI state for object pooling.
    /// </summary>
    public void ResetAI()
    {
        // Reset state
        CurrentState = ZombieState.Idle;
        CurrentTarget = null;
        SpeedType = ZombieSpeed.Regular;

        // Reset timers
        investigateTimer = 0f;
        lastHeardTime = 0f;
        nextAttackTime = 0f;
        nextIdleSoundTime = Time.time + Random.Range(2f, idleSoundInterval);
        nextChaseSoundTime = 0f;
        wanderTimer = 0f;
        hasPlayedAlertSound = false;
        nextPathRecalcTime = 0f;
        nextLightCheckTime = 0f;

        // Reset light fleeing
        currentLightLevel = 0f;
        isFleeing = false;
        isStationary = false;

        // Reset components
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = walkSpeed;
            agent.isStopped = false;
        }

        // Register with manager
        if (ZombieManager.Instance != null)
        {
            ZombieManager.Instance.RegisterZombie(this);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Vision range (small, yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        // Vision cone
        Vector3 leftDir = Quaternion.Euler(0, -visionAngle / 2f, 0) * transform.forward * visionRange;
        Vector3 rightDir = Quaternion.Euler(0, visionAngle / 2f, 0) * transform.forward * visionRange;
        Gizmos.DrawLine(transform.position, transform.position + leftDir);
        Gizmos.DrawLine(transform.position, transform.position + rightDir);

        // Attack range (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Investigation point (cyan)
        if (CurrentState == ZombieState.Investigating)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(investigatePosition, 1f);
            Gizmos.DrawLine(transform.position, investigatePosition);
        }

        // Light flee destination (orange)
        if (CurrentState == ZombieState.FleeingLight)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
            Gizmos.DrawSphere(fleeDestination, 0.5f);
            Gizmos.DrawLine(transform.position, fleeDestination);
        }

        // Light danger indicator
        if (Application.isPlaying)
        {
            float lightLevel = LightDetection.GetLightLevelAt(transform.position, obstacleMask);
            if (lightLevel >= lightPainThreshold)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
            }
            else if (lightLevel >= lightDiscomfortThreshold)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.3f);
            }
        }
    }
}
