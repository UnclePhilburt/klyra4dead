using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class SafeZone : MonoBehaviour
{
    [Header("Settings")]
    public bool killZombiesOnEnter = true;
    public bool stopCombatMusic = true;
    public bool pauseZombieSpawning = true;

    [Header("Spawn Point")]
    public bool unlockAsSpawnPoint = true;

    [Header("Optional Effects")]
    public bool healPlayers = false;
    public float healRate = 5f; // Health per second

    // Track players in zone
    private HashSet<PlayerHealth> playersInZone = new HashSet<PlayerHealth>();

    // Static list of all safe zones for Director to check
    public static List<SafeZone> AllSafeZones = new List<SafeZone>();

    void Awake()
    {
        // Make sure collider is trigger
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnEnable()
    {
        AllSafeZones.Add(this);
    }

    void OnDisable()
    {
        AllSafeZones.Remove(this);
    }

    void Update()
    {
        // Heal players in zone
        if (healPlayers)
        {
            foreach (var player in playersInZone)
            {
                if (player != null && !player.IsDead)
                {
                    player.Heal(healRate * Time.deltaTime);
                }
            }
        }

        // Check if any player is in safe zone - pause spawning
        if (pauseZombieSpawning && playersInZone.Count > 0)
        {
            if (ZombieDirector.Instance != null)
            {
                ZombieDirector.Instance.PausedSpawning(true);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check for zombie
        ZombieHealth zombie = other.GetComponent<ZombieHealth>();
        if (zombie == null) zombie = other.GetComponentInParent<ZombieHealth>();

        if (zombie != null && !zombie.IsDead)
        {
            if (killZombiesOnEnter)
            {
                // Kill zombie instantly
                zombie.TakeDamageLocal(9999f, Vector3.zero, zombie.transform.position);
                Debug.Log("[SafeZone] Zombie entered safe zone - eliminated!");
            }
            return;
        }

        // Check for player
        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player == null) player = other.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            playersInZone.Add(player);
            Debug.Log("[SafeZone] Player entered safe zone");

            // Stop combat music
            if (stopCombatMusic && CombatMusicManager.Instance != null)
            {
                CombatMusicManager.Instance.StopMusic();
            }

            // Unlock spawn point and complete quest
            if (unlockAsSpawnPoint)
            {
                if (SpawnManager.Instance != null)
                {
                    SpawnManager.Instance.UnlockSafeZoneSpawn();
                }

                TutorialQuestUI quest = FindFirstObjectByType<TutorialQuestUI>();
                if (quest != null)
                {
                    quest.CompleteQuest();
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Check for player leaving
        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player == null) player = other.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            playersInZone.Remove(player);
            Debug.Log("[SafeZone] Player left safe zone");

            // Resume spawning if no players in any safe zone
            if (pauseZombieSpawning && !AnyPlayerInSafeZone())
            {
                if (ZombieDirector.Instance != null)
                {
                    ZombieDirector.Instance.PausedSpawning(false);
                }
            }
        }
    }

    // Check if position is inside any safe zone
    public static bool IsPositionSafe(Vector3 position)
    {
        foreach (var zone in AllSafeZones)
        {
            if (zone == null) continue;

            Collider col = zone.GetComponent<Collider>();
            if (col != null && col.bounds.Contains(position))
            {
                return true;
            }
        }
        return false;
    }

    // Check if any player is in any safe zone
    public static bool AnyPlayerInSafeZone()
    {
        foreach (var zone in AllSafeZones)
        {
            if (zone != null && zone.playersInZone.Count > 0)
            {
                return true;
            }
        }
        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider box)
            {
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(sphere.center, sphere.radius);
            }
        }
    }
}
