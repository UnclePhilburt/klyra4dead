using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// AI Director - Monitors player stress and dynamically adjusts game intensity.
/// Works with ZombiePopulationManager for dynamic spawning.
/// </summary>
public class AIDirector : MonoBehaviourPunCallbacks
{
    public static AIDirector Instance { get; private set; }

    [Header("Intensity Settings")]
    [Range(0f, 1f)] public float baseIntensity = 0.5f;
    public float intensityBuildRate = 0.08f;
    public float intensityDecayRate = 0.12f;

    [Header("Timing")]
    public float minPeakDuration = 30f;
    public float maxPeakDuration = 90f;
    public float minRelaxDuration = 25f;
    public float maxRelaxDuration = 60f;
    public float minStealthDuration = 45f;
    public float maxStealthDuration = 90f;

    [Header("Thresholds")]
    public float peakIntensityThreshold = 0.7f;
    public float relaxIntensityThreshold = 0.3f;
    public float criticalHealthPercent = 0.25f;
    public float lowAmmoPercent = 0.2f;
    public float healthyPercent = 0.7f;

    [Header("Spawn Modifiers")]
    public float minSpawnMultiplier = 0.3f;
    public float maxSpawnMultiplier = 1.5f;

    [Header("Pacing")]
    [Tooltip("Chance to trigger stealth after intense combat")]
    public float stealthChance = 0.35f;
    [Tooltip("Chance to trigger ambush after quiet period")]
    public float ambushChance = 0.25f;
    [Tooltip("Min time between horde events")]
    public float minTimeBetweenHordes = 120f;

    [Header("Debug")]
    public bool showDebugUI = true;

    // Current state
    public float CurrentIntensity { get; private set; }
    public DirectorState CurrentState { get; private set; }
    public float TeamStress { get; private set; }
    public float TeamHealth { get; private set; }
    public float TeamAmmo { get; private set; }
    public float TimeSinceLastHorde { get; private set; }

    public enum DirectorState
    {
        Stealth,    // Quiet, few zombies, exploration time
        BuildUp,    // Ramping up intensity
        Sustain,    // Maintaining moderate pressure
        Peak,       // High intensity combat
        PeakFade,   // Transitioning out of peak
        Relax,      // Recovery period
        Ambush      // Setting up a surprise attack
    }

    // Internal tracking
    private float stateTimer;
    private float stateDuration;
    private float lastHordeTime = -999f;
    private int recentPlayerDeaths;
    private float deathDecayTimer;
    private List<PlayerStats> trackedPlayers = new List<PlayerStats>();
    private int zombiesNearPlayers;
    private float nearbyZombieCheckTimer;

    // Population manager reference
    private ZombiePopulationManager populationManager;

    // Events
    public event System.Action<DirectorState> OnStateChanged;
    public event System.Action<float> OnIntensityChanged;
    public event System.Action OnHordeTriggered;
    public event System.Action OnStealthTriggered;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        CurrentState = DirectorState.BuildUp;
        stateDuration = Random.Range(15f, 25f);
        populationManager = ZombiePopulationManager.Instance;

        InvokeRepeating(nameof(RefreshPlayerList), 1f, 2f);
    }

    void Update()
    {
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        UpdatePlayerMetrics();
        UpdateZombieProximity();
        UpdateIntensity();
        UpdateState();
        DecayDeathPenalty();

        TimeSinceLastHorde += Time.deltaTime;
    }

    void RefreshPlayerList()
    {
        trackedPlayers.Clear();
        PlayerStats[] stats = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        trackedPlayers.AddRange(stats);
        
        // If no population manager, try to find it
        if (populationManager == null)
        {
            populationManager = ZombiePopulationManager.Instance;
        }
    }

    void UpdatePlayerMetrics()
    {
        if (trackedPlayers.Count == 0)
        {
            PlayerHealth[] healths = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
            // Manual average to avoid LINQ allocation
            if (healths.Length > 0)
            {
                float sum = 0f;
                for (int i = 0; i < healths.Length; i++)
                    sum += healths[i].HealthPercent;
                TeamHealth = sum / healths.Length;
            }
            else
            {
                TeamHealth = 1f;
            }
            TeamAmmo = 0.5f;
            TeamStress = CalculateStressFromHealth(TeamHealth);
            return;
        }

        float totalHealth = 0f;
        float totalAmmo = 0f;
        float totalStress = 0f;
        int aliveCount = 0;

        foreach (var player in trackedPlayers)
        {
            if (player == null || player.IsDead) continue;

            aliveCount++;
            totalHealth += player.HealthPercent;
            totalAmmo += player.AmmoPercent;
            totalStress += player.StressLevel;
        }

        if (aliveCount > 0)
        {
            TeamHealth = totalHealth / aliveCount;
            TeamAmmo = totalAmmo / aliveCount;
            TeamStress = totalStress / aliveCount;
        }
        else
        {
            TeamHealth = 0f;
            TeamAmmo = 0f;
            TeamStress = 1f;
        }

        TeamStress = Mathf.Clamp01(TeamStress + (recentPlayerDeaths * 0.2f));
    }

    float CalculateStressFromHealth(float healthPercent)
    {
        if (healthPercent > healthyPercent) return 0.2f;
        if (healthPercent > 0.5f) return 0.4f;
        if (healthPercent > criticalHealthPercent) return 0.7f;
        return 1f;
    }

    void UpdateZombieProximity()
    {
        nearbyZombieCheckTimer -= Time.deltaTime;
        if (nearbyZombieCheckTimer > 0) return;
        nearbyZombieCheckTimer = 1f;

        zombiesNearPlayers = 0;
        float dangerRadius = 15f;

        foreach (var player in trackedPlayers)
        {
            if (player == null || player.IsDead) continue;

            Collider[] nearby = Physics.OverlapSphere(player.transform.position, dangerRadius);
            foreach (var col in nearby)
            {
                if (col.GetComponent<ZombieHealth>() != null)
                {
                    zombiesNearPlayers++;
                }
            }
        }
    }

    void UpdateIntensity()
    {
        float targetIntensityNow = CalculateTargetIntensity();

        if (CurrentIntensity < targetIntensityNow)
        {
            CurrentIntensity += intensityBuildRate * Time.deltaTime;
        }
        else
        {
            CurrentIntensity -= intensityDecayRate * Time.deltaTime;
        }

        CurrentIntensity = Mathf.Clamp01(CurrentIntensity);
        OnIntensityChanged?.Invoke(CurrentIntensity);
    }

    float CalculateTargetIntensity()
    {
        float intensity = 0f;

        switch (CurrentState)
        {
            case DirectorState.Stealth:
                intensity = 0.15f;
                break;
            case DirectorState.BuildUp:
                intensity = 0.3f + (stateTimer / stateDuration) * 0.3f;
                break;
            case DirectorState.Sustain:
                intensity = 0.5f;
                break;
            case DirectorState.Peak:
                intensity = 0.85f;
                break;
            case DirectorState.PeakFade:
                intensity = 0.5f;
                break;
            case DirectorState.Relax:
                intensity = 0.2f;
                break;
            case DirectorState.Ambush:
                intensity = 0.25f;
                break;
        }

        // Combat modifier
        float combatModifier = Mathf.Clamp01(zombiesNearPlayers / 10f) * 0.25f;
        intensity += combatModifier;

        // Reduce if struggling
        if (TeamHealth < criticalHealthPercent)
        {
            intensity *= 0.4f;
        }
        else if (TeamHealth < 0.5f)
        {
            intensity *= 0.7f;
        }

        if (TeamAmmo < lowAmmoPercent)
        {
            intensity *= 0.6f;
        }

        return Mathf.Clamp01(intensity);
    }

    void UpdateState()
    {
        stateTimer += Time.deltaTime;

        switch (CurrentState)
        {
            case DirectorState.Stealth:
                if (stateTimer >= stateDuration)
                {
                    // End stealth - maybe ambush?
                    if (TeamHealth > healthyPercent && Random.value < ambushChance && 
                        TimeSinceLastHorde > minTimeBetweenHordes)
                    {
                        TransitionTo(DirectorState.Ambush);
                    }
                    else
                    {
                        TransitionTo(DirectorState.BuildUp);
                    }
                }
                break;

            case DirectorState.BuildUp:
                if (stateTimer >= stateDuration || CurrentIntensity >= 0.6f)
                {
                    // Decide: sustain, peak, or more buildup
                    if (TeamHealth > 0.6f && CurrentIntensity > 0.5f)
                    {
                        if (Random.value < 0.4f && TimeSinceLastHorde > minTimeBetweenHordes)
                            TransitionTo(DirectorState.Peak);
                        else
                            TransitionTo(DirectorState.Sustain);
                    }
                    else
                    {
                        TransitionTo(DirectorState.Sustain);
                    }
                }
                break;

            case DirectorState.Sustain:
                if (stateTimer >= stateDuration)
                {
                    if (TeamHealth > 0.5f && TeamStress < 0.6f && Random.value < 0.3f && 
                        TimeSinceLastHorde > minTimeBetweenHordes)
                    {
                        TransitionTo(DirectorState.Peak);
                    }
                    else if (TeamStress > 0.7f || TeamHealth < 0.4f)
                    {
                        TransitionTo(DirectorState.Relax);
                    }
                    else
                    {
                        // Continue sustain or slight build
                        TransitionTo(Random.value < 0.5f ? DirectorState.Sustain : DirectorState.BuildUp);
                    }
                }
                break;

            case DirectorState.Peak:
                bool teamStruggling = TeamHealth < criticalHealthPercent || TeamStress > 0.85f;
                if (stateTimer >= stateDuration || teamStruggling)
                {
                    TransitionTo(DirectorState.PeakFade);
                }
                break;

            case DirectorState.PeakFade:
                if (stateTimer >= 10f || CurrentIntensity < 0.4f)
                {
                    TransitionTo(DirectorState.Relax);
                }
                break;

            case DirectorState.Relax:
                if (stateTimer >= stateDuration)
                {
                    // After relax, chance for stealth or back to buildup
                    if (Random.value < stealthChance)
                    {
                        TransitionTo(DirectorState.Stealth);
                    }
                    else
                    {
                        TransitionTo(DirectorState.BuildUp);
                    }
                }
                break;

            case DirectorState.Ambush:
                if (stateTimer >= 5f)
                {
                    TransitionTo(DirectorState.Peak);
                    TriggerHorde();
                }
                break;
        }
    }

    void TransitionTo(DirectorState newState)
    {
        DirectorState oldState = CurrentState;
        CurrentState = newState;
        stateTimer = 0f;

        switch (newState)
        {
            case DirectorState.Stealth:
                stateDuration = Random.Range(minStealthDuration, maxStealthDuration);
                OnStealthTriggered?.Invoke();
                TriggerPopulationStealth();
                break;
            case DirectorState.BuildUp:
                stateDuration = Random.Range(20f, 40f);
                TriggerPopulationRoaming();
                break;
            case DirectorState.Sustain:
                stateDuration = Random.Range(30f, 60f);
                break;
            case DirectorState.Peak:
                stateDuration = Random.Range(minPeakDuration, maxPeakDuration);
                lastHordeTime = Time.time;
                TimeSinceLastHorde = 0f;
                OnHordeTriggered?.Invoke();
                TriggerPopulationHorde();
                break;
            case DirectorState.PeakFade:
                stateDuration = 10f;
                break;
            case DirectorState.Relax:
                stateDuration = Random.Range(minRelaxDuration, maxRelaxDuration);
                if (TeamHealth < 0.5f) stateDuration *= 1.5f;
                break;
            case DirectorState.Ambush:
                stateDuration = 5f;
                break;
        }

        Debug.Log($"[AIDirector] {oldState} -> {newState} (Duration: {stateDuration:F1}s, Intensity: {CurrentIntensity:F2})");
        OnStateChanged?.Invoke(newState);
    }

    void TriggerPopulationStealth()
    {
        if (populationManager != null)
        {
            populationManager.TriggerStealth();
        }
    }

    void TriggerPopulationRoaming()
    {
        if (populationManager != null)
        {
            populationManager.ForceMode(ZombiePopulationManager.PopulationMode.Normal);
        }
    }

    void TriggerPopulationHorde()
    {
        if (populationManager != null)
        {
            populationManager.TriggerHorde();
        }
    }

    public void TriggerHorde()
    {
        if (CurrentState != DirectorState.Peak)
        {
            TransitionTo(DirectorState.Peak);
        }
    }

    void DecayDeathPenalty()
    {
        if (recentPlayerDeaths > 0)
        {
            deathDecayTimer += Time.deltaTime;
            if (deathDecayTimer >= 30f)
            {
                recentPlayerDeaths--;
                deathDecayTimer = 0f;
            }
        }
    }

    public void ReportPlayerDeath()
    {
        recentPlayerDeaths++;
        deathDecayTimer = 0f;
        CurrentIntensity = Mathf.Max(0.2f, CurrentIntensity - 0.3f);

        if (CurrentState == DirectorState.Peak)
        {
            TransitionTo(DirectorState.PeakFade);
        }
    }

    public void ReportPlayerRevive()
    {
        CurrentIntensity = Mathf.Max(0.3f, CurrentIntensity - 0.1f);
    }

    #region Public Getters

    public float GetSpawnRateMultiplier()
    {
        float multiplier = Mathf.Lerp(minSpawnMultiplier, maxSpawnMultiplier, CurrentIntensity);

        if (TeamHealth < criticalHealthPercent) multiplier *= 0.3f;
        else if (TeamHealth < 0.5f) multiplier *= 0.6f;

        if (CurrentState == DirectorState.Relax || CurrentState == DirectorState.Stealth)
            multiplier *= 0.4f;

        return Mathf.Clamp(multiplier, minSpawnMultiplier, maxSpawnMultiplier);
    }

    public float GetZombieCountMultiplier()
    {
        float multiplier = 1f;

        switch (CurrentState)
        {
            case DirectorState.Stealth:
                multiplier = 0.3f;
                break;
            case DirectorState.Relax:
                multiplier = 0.5f;
                break;
            case DirectorState.BuildUp:
                multiplier = 0.6f + (stateTimer / stateDuration) * 0.3f;
                break;
            case DirectorState.Sustain:
                multiplier = 0.8f;
                break;
            case DirectorState.Peak:
            case DirectorState.Ambush:
                multiplier = 1.3f;
                break;
        }

        if (TeamHealth < criticalHealthPercent) multiplier *= 0.5f;
        else if (TeamAmmo < lowAmmoPercent) multiplier *= 0.7f;

        return Mathf.Clamp(multiplier, 0.2f, 1.5f);
    }

    public float GetWaveCooldown(float baseCooldown)
    {
        float cooldown = baseCooldown;

        if (TeamHealth < criticalHealthPercent) cooldown *= 2f;
        else if (TeamHealth < 0.5f) cooldown *= 1.5f;
        if (TeamAmmo < lowAmmoPercent) cooldown *= 1.3f;
        if (TeamHealth > healthyPercent && TeamAmmo > 0.5f) cooldown *= 0.7f;

        if (CurrentState == DirectorState.Relax || CurrentState == DirectorState.Stealth)
            cooldown *= 1.5f;
        else if (CurrentState == DirectorState.Ambush)
            cooldown *= 0.3f;

        return Mathf.Clamp(cooldown, 10f, 120f);
    }

    public bool ShouldSpawnSpecial()
    {
        if (TeamHealth < 0.4f || TeamAmmo < lowAmmoPercent) return false;
        if (CurrentState == DirectorState.Stealth) return false;

        float chance = CurrentState == DirectorState.Peak ? 0.15f : 0.05f;
        if (CurrentIntensity > 0.8f) chance *= 0.5f;

        return Random.value < chance;
    }

    public float GetItemDropMultiplier()
    {
        float multiplier = 1f;

        if (TeamHealth < criticalHealthPercent) multiplier += 1.2f;
        else if (TeamHealth < 0.5f) multiplier += 0.5f;

        if (TeamAmmo < lowAmmoPercent) multiplier += 1f;
        else if (TeamAmmo < 0.4f) multiplier += 0.4f;

        if (TeamHealth > healthyPercent && TeamAmmo > 0.6f) multiplier *= 0.5f;

        // More drops during stealth (reward exploration)
        if (CurrentState == DirectorState.Stealth) multiplier += 0.3f;

        return Mathf.Clamp(multiplier, 0.3f, 3f);
    }

    public bool IsGoodTimeForWave()
    {
        if (CurrentState == DirectorState.Stealth) return false;
        if (CurrentState == DirectorState.Relax && stateTimer < stateDuration * 0.8f) return false;

        return CurrentState == DirectorState.BuildUp || 
               CurrentState == DirectorState.Sustain ||
               CurrentState == DirectorState.Peak || 
               CurrentState == DirectorState.Ambush;
    }

    public bool IsInStealthMode()
    {
        return CurrentState == DirectorState.Stealth;
    }

    public bool IsInCombatMode()
    {
        return CurrentState == DirectorState.Peak || CurrentState == DirectorState.Sustain;
    }

    #endregion

    void OnGUI()
    {
        if (!showDebugUI) return;
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        GUILayout.BeginArea(new Rect(10, 10, 250, 220));
        GUILayout.BeginVertical("box");

        string stateColor = CurrentState switch
        {
            DirectorState.Stealth => "cyan",
            DirectorState.Peak => "red",
            DirectorState.Ambush => "magenta",
            DirectorState.Relax => "green",
            _ => "white"
        };

        GUILayout.Label("<b>AI Director</b>");
        GUILayout.Label($"State: <color={stateColor}>{CurrentState}</color>");
        GUILayout.Label($"Intensity: {CurrentIntensity:P0}");
        GUILayout.Label($"Team Health: {TeamHealth:P0}");
        GUILayout.Label($"Team Ammo: {TeamAmmo:P0}");
        GUILayout.Label($"Team Stress: {TeamStress:P0}");
        GUILayout.Label($"Nearby Zombies: {zombiesNearPlayers}");
        GUILayout.Label($"State Timer: {stateTimer:F1}s / {stateDuration:F1}s");
        GUILayout.Label($"Time Since Horde: {TimeSinceLastHorde:F0}s");
        GUILayout.Label($"Spawn Mult: {GetSpawnRateMultiplier():F2}x");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}
