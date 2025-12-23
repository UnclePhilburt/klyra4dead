using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;
using System.Collections.Generic;

public class ZombieSpawner : MonoBehaviourPunCallbacks
{
    [Header("Spawn Settings")]
    [Tooltip("Name of zombie prefab in Resources folder")]
    public string zombiePrefabName = "Zombie";
    public int maxZombies = 5;
    public float spawnRadius = 5f;
    public float spawnInterval = 10f;
    public bool spawnOnStart = true;

    private List<GameObject> spawnedZombies = new List<GameObject>();
    private float spawnTimer;
    private GameObject zombiePrefab;

    bool CanSpawn => !PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient;

    void Start()
    {
        // Load prefab for offline mode
        zombiePrefab = Resources.Load<GameObject>(zombiePrefabName);

        if (!CanSpawn) return;

        if (spawnOnStart)
        {
            for (int i = 0; i < maxZombies; i++)
            {
                SpawnZombie();
            }
        }
    }

    void Update()
    {
        if (!CanSpawn) return;

        // Clean up destroyed zombies
        spawnedZombies.RemoveAll(z => z == null);

        // Respawn if below max
        if (spawnedZombies.Count < maxZombies)
        {
            spawnTimer += Time.deltaTime;
            if (spawnTimer >= spawnInterval)
            {
                SpawnZombie();
                spawnTimer = 0f;
            }
        }
    }

    void SpawnZombie()
    {
        Vector3 spawnPos = transform.position + Random.insideUnitSphere * spawnRadius;
        spawnPos.y = transform.position.y;

        // Find valid NavMesh position
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPos, out hit, spawnRadius * 2f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        GameObject zombie;
        if (PhotonNetwork.IsConnected)
        {
            // Online - use Photon
            zombie = PhotonNetwork.Instantiate(zombiePrefabName, spawnPos, rotation);
        }
        else
        {
            // Offline - regular instantiate
            zombie = Instantiate(zombiePrefab, spawnPos, rotation);
        }

        spawnedZombies.Add(zombie);
        Debug.Log($"[ZombieSpawner] Spawned zombie at {spawnPos}");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
