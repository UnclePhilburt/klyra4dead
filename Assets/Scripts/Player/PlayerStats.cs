using UnityEngine;
using Photon.Pun;

/// <summary>
/// Tracks individual player statistics for the AI Director.
/// Attach to the player prefab alongside PlayerHealth.
/// </summary>
public class PlayerStats : MonoBehaviourPunCallbacks
{
    [Header("References")]
    private PlayerHealth playerHealth;
    private PlayerShooting playerShooting;
    private WeaponSystem weaponSystem;

    [Header("Stress Tracking")]
    public float stressDecayRate = 0.1f;
    public float damageStressMultiplier = 0.02f;
    public float nearbyZombieStress = 0.05f;
    public float combatStressDecay = 0.05f;

    // Current stats
    public float HealthPercent { get; private set; } = 1f;
    public float AmmoPercent { get; private set; } = 1f;
    public float StressLevel { get; private set; } = 0f;
    public bool IsDead => playerHealth != null && playerHealth.IsDead;
    public bool IsDowned => playerHealth != null && playerHealth.IsDowned;

    // Tracking
    private float lastHealth;
    private float recentDamageTaken;
    private float damageDecayTimer;
    private float combatTimer;
    private float nearbyZombieCheckTimer;
    private int nearbyZombies;

    // Events
    public event System.Action OnDeath;
    public event System.Action OnRevive;

    void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerShooting = GetComponent<PlayerShooting>();
        weaponSystem = GetComponent<WeaponSystem>();

        if (playerHealth != null)
        {
            lastHealth = playerHealth.currentHealth;
            playerHealth.OnDeath += HandleDeath;
            playerHealth.OnRevived += HandleRevive;
        }
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandleDeath;
            playerHealth.OnRevived -= HandleRevive;
        }
    }

    void Update()
    {
        // Only track local player
        if (PhotonNetwork.IsConnected && photonView != null && !photonView.IsMine) return;

        UpdateHealthStats();
        UpdateAmmoStats();
        UpdateStress();
        CheckNearbyZombies();
    }

    void UpdateHealthStats()
    {
        if (playerHealth == null) return;

        HealthPercent = playerHealth.HealthPercent;

        // Track damage taken
        float currentHealth = playerHealth.currentHealth;
        if (currentHealth < lastHealth)
        {
            float damage = lastHealth - currentHealth;
            recentDamageTaken += damage;
            StressLevel += damage * damageStressMultiplier;
            combatTimer = 5f; // 5 seconds of "in combat" status
        }
        lastHealth = currentHealth;

        // Decay recent damage tracking
        damageDecayTimer += Time.deltaTime;
        if (damageDecayTimer >= 3f)
        {
            recentDamageTaken *= 0.8f;
            damageDecayTimer = 0f;
        }
    }

    void UpdateAmmoStats()
    {
        // Try PlayerShooting first
        if (playerShooting != null)
        {
            int current = playerShooting.CurrentAmmo;
            int max = playerShooting.MagazineSize;
            int reserve = playerShooting.ReserveAmmo;

            // Total ammo as percent of what we could have
            int totalAmmo = current + reserve;
            int maxTotal = max + 90; // Assume 90 reserve max
            AmmoPercent = Mathf.Clamp01((float)totalAmmo / maxTotal);
            return;
        }

        // Try WeaponSystem
        if (weaponSystem != null)
        {
            AmmoPercent = weaponSystem.GetAmmoPercent();
            return;
        }

        // Default to assuming moderate ammo
        AmmoPercent = 0.5f;
    }

    void UpdateStress()
    {
        // Combat timer decay
        if (combatTimer > 0)
        {
            combatTimer -= Time.deltaTime;
        }

        // Base stress decay when not in combat
        if (combatTimer <= 0)
        {
            StressLevel -= stressDecayRate * Time.deltaTime;
        }
        else
        {
            // Slower decay during combat
            StressLevel -= combatStressDecay * Time.deltaTime;
        }

        // Add stress from nearby zombies
        StressLevel += nearbyZombies * nearbyZombieStress * Time.deltaTime;

        // Add stress from low health
        if (HealthPercent < 0.3f)
        {
            StressLevel += 0.1f * Time.deltaTime;
        }

        // Add stress from low ammo
        if (AmmoPercent < 0.2f)
        {
            StressLevel += 0.05f * Time.deltaTime;
        }

        // Add stress if downed
        if (IsDowned)
        {
            StressLevel += 0.2f * Time.deltaTime;
        }

        StressLevel = Mathf.Clamp01(StressLevel);
    }

    void CheckNearbyZombies()
    {
        nearbyZombieCheckTimer -= Time.deltaTime;
        if (nearbyZombieCheckTimer > 0) return;
        nearbyZombieCheckTimer = 0.5f;

        nearbyZombies = 0;
        float checkRadius = 10f;

        Collider[] nearby = Physics.OverlapSphere(transform.position, checkRadius);
        foreach (var col in nearby)
        {
            ZombieHealth zh = col.GetComponent<ZombieHealth>();
            if (zh != null && !zh.IsDead)
            {
                nearbyZombies++;
            }
        }
    }

    void HandleDeath()
    {
        StressLevel = 1f;
        OnDeath?.Invoke();

        // Report to AI Director
        if (AIDirector.Instance != null)
        {
            AIDirector.Instance.ReportPlayerDeath();
        }
    }

    void HandleRevive()
    {
        StressLevel = 0.5f; // Start at moderate stress after revive
        OnRevive?.Invoke();

        // Report to AI Director
        if (AIDirector.Instance != null)
        {
            AIDirector.Instance.ReportPlayerRevive();
        }
    }

    // Called by external systems to add stress
    public void AddStress(float amount)
    {
        StressLevel = Mathf.Clamp01(StressLevel + amount);
    }

    // Get a danger score for this player (used for prioritizing who needs help)
    public float GetDangerScore()
    {
        float danger = 0f;

        // Low health = danger
        danger += (1f - HealthPercent) * 0.4f;

        // Low ammo = danger
        danger += (1f - AmmoPercent) * 0.2f;

        // High stress = danger
        danger += StressLevel * 0.2f;

        // Nearby zombies = danger
        danger += Mathf.Clamp01(nearbyZombies / 5f) * 0.2f;

        // Downed = max danger
        if (IsDowned) danger = 1f;

        return Mathf.Clamp01(danger);
    }
}
