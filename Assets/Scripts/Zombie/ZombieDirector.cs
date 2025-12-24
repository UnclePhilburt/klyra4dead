using UnityEngine;

// DEPRECATED: Use ZombiePopulationManager instead
public class ZombieDirector : MonoBehaviour
{
    public static ZombieDirector Instance { get; private set; }
    
    public int ActiveZombieCount => 0;
    public float CurrentIntensity => 0f;
    public bool InCalmPeriod => false;

    void Awake()
    {
        Instance = this;
        Debug.Log("[ZombieDirector] DEPRECATED - Use ZombiePopulationManager instead.");
        enabled = false;
    }

    // Stub for SafeZone compatibility - forwards to PopulationManager
    public void PausedSpawning(bool paused)
    {
        if (ZombiePopulationManager.Instance != null)
        {
            ZombiePopulationManager.Instance.SetSpawningPaused(paused);
        }
    }
}
