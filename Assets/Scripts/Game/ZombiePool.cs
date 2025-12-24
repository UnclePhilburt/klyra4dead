using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Photon.Pun;

/// <summary>
/// Object pool for zombies to avoid expensive Instantiate/Destroy calls.
/// Critical for WebGL performance.
/// </summary>
public class ZombiePool : MonoBehaviour
{
    public static ZombiePool Instance { get; private set; }

    [Header("Pool Settings")]
    public int initialPoolSize = 30;
    public int maxPoolSize = 80;
    public GameObject zombiePrefab;

    [Header("Debug")]
    public int activeCount;
    public int pooledCount;

    private Queue<GameObject> pool = new Queue<GameObject>();
    private List<GameObject> allSpawned = new List<GameObject>();
    private Transform poolContainer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        poolContainer = new GameObject("ZombiePool").transform;
        poolContainer.SetParent(transform);
    }

    void Start()
    {
        // Load prefab if not set
        if (zombiePrefab == null)
        {
            zombiePrefab = Resources.Load<GameObject>("Zombie");
        }

        if (zombiePrefab == null)
        {
            Debug.LogError("[ZombiePool] No zombie prefab found!");
            return;
        }

        // Pre-warm pool (only in offline mode or as master client)
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
        {
            PrewarmPool();
        }
    }

    void PrewarmPool()
    {
        // Spread pre-warming across frames to avoid hitch
        StartCoroutine(PrewarmCoroutine());
    }

    System.Collections.IEnumerator PrewarmCoroutine()
    {
        int spawnedThisFrame = 0;
        int zombiesPerFrame = 5; // Spawn 5 per frame to avoid hitch

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledZombie();
            spawnedThisFrame++;

            if (spawnedThisFrame >= zombiesPerFrame)
            {
                spawnedThisFrame = 0;
                yield return null;
            }
        }

        Debug.Log($"[ZombiePool] Pre-warmed {initialPoolSize} zombies");
    }

    void CreatePooledZombie()
    {
        GameObject zombie = Instantiate(zombiePrefab, Vector3.zero, Quaternion.identity, poolContainer);
        zombie.SetActive(false);

        // Ensure it has required components
        var ai = zombie.GetComponent<ZombieAI>();
        if (ai != null)
        {
            ai.IsPooled = true;
        }

        pool.Enqueue(zombie);
        pooledCount = pool.Count;
    }

    /// <summary>
    /// Get a zombie from the pool (or create new if pool empty).
    /// </summary>
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        // For networked games, use PhotonNetwork.Instantiate
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            return PhotonNetwork.Instantiate(zombiePrefab.name, position, rotation);
        }

        GameObject zombie;

        if (pool.Count > 0)
        {
            zombie = pool.Dequeue();
        }
        else if (allSpawned.Count < maxPoolSize)
        {
            zombie = Instantiate(zombiePrefab, poolContainer);
            var ai = zombie.GetComponent<ZombieAI>();
            if (ai != null) ai.IsPooled = true;
        }
        else
        {
            // Pool exhausted - find furthest inactive zombie to recycle
            zombie = FindFurthestZombie();
            if (zombie == null)
            {
                Debug.LogWarning("[ZombiePool] Pool exhausted, cannot spawn more zombies");
                return null;
            }
            ReturnToPoolInternal(zombie);
            zombie = pool.Dequeue();
        }

        // Reset and activate
        zombie.transform.SetPositionAndRotation(position, rotation);
        zombie.transform.SetParent(null);
        ResetZombie(zombie);
        zombie.SetActive(true);

        allSpawned.Add(zombie);
        activeCount = allSpawned.Count - pool.Count;
        pooledCount = pool.Count;

        return zombie;
    }

    GameObject FindFurthestZombie()
    {
        if (Camera.main == null) return null;

        Vector3 camPos = Camera.main.transform.position;
        GameObject furthest = null;
        float maxDistSqr = 0f;

        for (int i = 0; i < allSpawned.Count; i++)
        {
            var z = allSpawned[i];
            if (z == null || !z.activeInHierarchy) continue;

            float distSqr = (z.transform.position - camPos).sqrMagnitude;
            if (distSqr > maxDistSqr)
            {
                maxDistSqr = distSqr;
                furthest = z;
            }
        }

        return furthest;
    }

    /// <summary>
    /// Return a zombie to the pool.
    /// </summary>
    public void Return(GameObject zombie)
    {
        if (zombie == null) return;

        // For networked games
        if (PhotonNetwork.IsConnected)
        {
            var pv = zombie.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                PhotonNetwork.Destroy(zombie);
            }
            return;
        }

        ReturnToPoolInternal(zombie);
    }

    void ReturnToPoolInternal(GameObject zombie)
    {
        zombie.SetActive(false);
        zombie.transform.SetParent(poolContainer);
        zombie.transform.position = Vector3.zero;

        // Disable expensive components
        var agent = zombie.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        var animator = zombie.GetComponentInChildren<Animator>();
        if (animator != null) animator.enabled = false;

        pool.Enqueue(zombie);
        activeCount = allSpawned.Count - pool.Count;
        pooledCount = pool.Count;
    }

    void ResetZombie(GameObject zombie)
    {
        // Reset health
        var health = zombie.GetComponent<ZombieHealth>();
        if (health != null)
        {
            health.ResetHealth();
        }

        // Reset AI
        var ai = zombie.GetComponent<ZombieAI>();
        if (ai != null)
        {
            ai.ResetAI();
        }

        // Re-enable NavMeshAgent
        var agent = zombie.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(zombie.transform.position);
        }

        // Re-enable animator
        var animator = zombie.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
        }
    }

    /// <summary>
    /// Clear all zombies (for scene cleanup).
    /// </summary>
    public void Clear()
    {
        foreach (var zombie in allSpawned)
        {
            if (zombie != null)
            {
                Destroy(zombie);
            }
        }
        allSpawned.Clear();
        pool.Clear();
        activeCount = 0;
        pooledCount = 0;
    }

    void OnDestroy()
    {
        Clear();
    }
}
