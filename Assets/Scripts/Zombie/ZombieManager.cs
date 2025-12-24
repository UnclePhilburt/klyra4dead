using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages zombie AI updates for performance optimization.
/// - Distance-based culling: Far zombies update less frequently
/// - Staggered updates: Not all zombies update on the same frame
/// - LOD-style AI: Simpler AI for distant zombies
/// </summary>
public class ZombieManager : MonoBehaviour
{
    public static ZombieManager Instance { get; private set; }

    [Header("Distance Culling")]
    [Tooltip("Zombies closer than this get full AI every frame")]
    public float nearDistance = 20f;
    [Tooltip("Zombies between near and mid get AI every 2 frames")]
    public float midDistance = 40f;
    [Tooltip("Zombies between mid and far get AI every 4 frames")]
    public float farDistance = 60f;
    [Tooltip("Zombies beyond far distance get AI every 8 frames")]
    public float veryFarDistance = 80f;

    [Header("Performance")]
    [Tooltip("Maximum zombies to update per frame")]
    public int maxUpdatesPerFrame = 30;
    [Tooltip("Completely disable AI for zombies beyond this distance")]
    public float cullingDistance = 100f;

    // Registered zombies
    private List<ZombieAI> allZombies = new List<ZombieAI>();
    private List<Transform> trackedPlayers = new List<Transform>();
    private Transform playerTransform;

    // Staggered update tracking
    private int frameCounter = 0;

    // Distance buckets - HashSets for O(1) lookup
    private HashSet<ZombieAI> nearZombies = new HashSet<ZombieAI>();
    private HashSet<ZombieAI> midZombies = new HashSet<ZombieAI>();
    private HashSet<ZombieAI> farZombies = new HashSet<ZombieAI>();
    private HashSet<ZombieAI> veryFarZombies = new HashSet<ZombieAI>();
    private HashSet<ZombieAI> culledZombies = new HashSet<ZombieAI>();
    private HashSet<ZombieAI> frozenZombies = new HashSet<ZombieAI>();

    private float nextBucketUpdate;
    private const float BUCKET_UPDATE_INTERVAL = 0.5f;

    // Cached squared distances for fast comparison
    private float nearDistanceSqr;
    private float midDistanceSqr;
    private float farDistanceSqr;
    private float veryFarDistanceSqr;
    private float cullingDistanceSqr;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Pre-calculate squared distances to avoid sqrt in hot path
        CacheDistanceThresholds();
    }

    void CacheDistanceThresholds()
    {
        nearDistanceSqr = nearDistance * nearDistance;
        midDistanceSqr = midDistance * midDistance;
        farDistanceSqr = farDistance * farDistance;
        veryFarDistanceSqr = veryFarDistance * veryFarDistance;
        cullingDistanceSqr = cullingDistance * cullingDistance;
    }

    void Update()
    {
        frameCounter++;

        // Find player if not set
        if (playerTransform == null)
        {
            FindPlayer();
        }

        // Update distance buckets periodically
        if (Time.time >= nextBucketUpdate)
        {
            UpdateDistanceBuckets();
            nextBucketUpdate = Time.time + BUCKET_UPDATE_INTERVAL;
        }
    }

    void FindPlayer()
    {
        trackedPlayers.Clear();
        // Find all players
        var playerHealths = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var ph in playerHealths)
        {
            if (ph != null && !ph.IsDead)
                trackedPlayers.Add(ph.transform);
        }
        
        // Set local player transform
        foreach (var ph in playerHealths)
        {
            var pv = ph.GetComponent<Photon.Pun.PhotonView>();
            if (pv != null && pv.IsMine)
            {
                playerTransform = ph.transform;
                break;
            }
        }

        // Fallback to main camera
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }
    }

    void UpdateDistanceBuckets()
    {
        nearZombies.Clear();
        midZombies.Clear();
        farZombies.Clear();
        veryFarZombies.Clear();
        culledZombies.Clear();

        if (playerTransform == null) return;

        Vector3 playerPos = playerTransform.position;

        // Use for loop instead of foreach to avoid enumerator allocation
        for (int i = 0; i < allZombies.Count; i++)
        {
            var zombie = allZombies[i];
            if (zombie == null) continue;

            // Use squared distance to avoid expensive sqrt
            float distSqr = (zombie.transform.position - playerPos).sqrMagnitude;

            if (distSqr < nearDistanceSqr)
            {
                nearZombies.Add(zombie);
                UnfreezeZombie(zombie);
            }
            else if (distSqr < midDistanceSqr)
            {
                midZombies.Add(zombie);
                UnfreezeZombie(zombie);
            }
            else if (distSqr < farDistanceSqr)
            {
                farZombies.Add(zombie);
                UnfreezeZombie(zombie);
            }
            else if (distSqr < veryFarDistanceSqr)
            {
                veryFarZombies.Add(zombie);
                UnfreezeZombie(zombie);
            }
            else if (distSqr < cullingDistanceSqr)
            {
                culledZombies.Add(zombie);
                FreezeZombie(zombie); // Freeze AI completely
            }
            else
            {
                FreezeZombie(zombie); // Beyond culling - frozen
            }
        }
    }
    
    void FreezeZombie(ZombieAI zombie)
    {
        if (frozenZombies.Contains(zombie)) return;
        frozenZombies.Add(zombie);
        
        // Disable NavMeshAgent to save CPU
        var agent = zombie.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        
        // Reduce animator update
        var animator = zombie.GetComponentInChildren<Animator>();
        if (animator != null) animator.updateMode = AnimatorUpdateMode.Normal;
    }
    
    void UnfreezeZombie(ZombieAI zombie)
    {
        if (!frozenZombies.Contains(zombie)) return;
        frozenZombies.Remove(zombie);
        
        // Re-enable NavMeshAgent
        var agent = zombie.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = true;
    }

    /// <summary>
    /// Check if a zombie should update this frame based on distance.
    /// Called by ZombieAI in their Update().
    /// </summary>
    public bool ShouldUpdateThisFrame(ZombieAI zombie)
    {
        // Always update if chasing or attacking
        if (zombie.CurrentState == ZombieAI.ZombieState.Chasing ||
            zombie.CurrentState == ZombieAI.ZombieState.Attacking ||
            zombie.CurrentState == ZombieAI.ZombieState.FleeingLight)
        {
            return true;
        }

        // Check which bucket
        if (nearZombies.Contains(zombie))
            return true; // Every frame

        if (midZombies.Contains(zombie))
            return frameCounter % 2 == 0; // Every 2 frames

        if (farZombies.Contains(zombie))
            return frameCounter % 4 == 0; // Every 4 frames

        if (veryFarZombies.Contains(zombie))
            return frameCounter % 8 == 0; // Every 8 frames

        if (culledZombies.Contains(zombie))
            return frameCounter % 16 == 0; // Very infrequent

        // Beyond culling distance
        return false;
    }

    /// <summary>
    /// Get the update frequency multiplier for deltaTime compensation.
    /// </summary>
    public float GetDeltaTimeMultiplier(ZombieAI zombie)
    {
        if (nearZombies.Contains(zombie)) return 1f;
        if (midZombies.Contains(zombie)) return 2f;
        if (farZombies.Contains(zombie)) return 4f;
        if (veryFarZombies.Contains(zombie)) return 8f;
        return 16f;
    }

    /// <summary>
    /// Register a zombie with the manager.
    /// </summary>
    public void RegisterZombie(ZombieAI zombie)
    {
        if (!allZombies.Contains(zombie))
        {
            allZombies.Add(zombie);
        }
    }

    /// <summary>
    /// Unregister a zombie (call when destroyed).
    /// </summary>
    public void UnregisterZombie(ZombieAI zombie)
    {
        allZombies.Remove(zombie);
        nearZombies.Remove(zombie);
        midZombies.Remove(zombie);
        farZombies.Remove(zombie);
        veryFarZombies.Remove(zombie);
        culledZombies.Remove(zombie);
        frozenZombies.Remove(zombie);
    }

    /// <summary>
    /// Get all registered zombies (cached list - no allocation).
    /// </summary>
    public List<ZombieAI> GetAllZombies()
    {
        return allZombies;
    }

    /// <summary>
    /// Get list of tracked players for AI targeting.
    /// </summary>
    public List<Transform> GetTrackedPlayers()
    {
        return trackedPlayers;
    }

    public int GetActiveZombieCount()
    {
        return allZombies.Count;
    }

    /// <summary>
    /// Get count of zombies currently chasing.
    /// </summary>
    public int GetChasingCount()
    {
        int count = 0;
        foreach (var zombie in allZombies)
        {
            if (zombie != null &&
                (zombie.CurrentState == ZombieAI.ZombieState.Chasing ||
                 zombie.CurrentState == ZombieAI.ZombieState.Attacking))
            {
                count++;
            }
        }
        return count;
    }
}
