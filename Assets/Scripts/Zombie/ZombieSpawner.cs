using UnityEngine;

// DEPRECATED: Use ZombiePopulationManager instead
// This script is disabled and kept only for reference
public class ZombieSpawner : MonoBehaviour
{
    void Awake()
    {
        Debug.Log("[ZombieSpawner] DEPRECATED - Use ZombiePopulationManager instead. Disabling.");
        enabled = false;
        gameObject.SetActive(false);
    }
}
