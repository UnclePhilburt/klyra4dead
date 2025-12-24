using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// WebGL-specific optimizations that maintain gameplay feel while improving performance.
/// Automatically adjusts settings based on platform and measured FPS.
/// </summary>
public class WebGLOptimizer : MonoBehaviour
{
    public static WebGLOptimizer Instance { get; private set; }
    public static bool IsWebGL { get; private set; }

    [Header("Performance Monitoring")]
    public float targetFPS = 30f;
    public float criticalFPS = 20f;

    [Header("Current Performance")]
    [SerializeField] private float currentFPS;
    [SerializeField] private PerformanceLevel currentLevel = PerformanceLevel.High;

    public enum PerformanceLevel { High, Medium, Low, Critical }
    public PerformanceLevel CurrentLevel => currentLevel;

    // Cached squared distances for fast comparisons
    public float NearDistanceSqr { get; private set; }
    public float MidDistanceSqr { get; private set; }
    public float FarDistanceSqr { get; private set; }
    public float CullDistanceSqr { get; private set; }

    // Dynamic settings based on performance
    public int MaxActiveAI { get; private set; } = 20;
    public int MaxVisibleZombies { get; private set; } = 40;
    public float AIUpdateInterval { get; private set; } = 0.1f;
    public float VisionCheckInterval { get; private set; } = 0.2f;
    public float PathRecalcInterval { get; private set; } = 0.5f;
    public bool EnableLightAvoidance { get; private set; } = true;

    // FPS tracking
    private float[] fpsBuffer = new float[30];
    private int fpsBufferIndex;
    private float fpsUpdateTimer;
    private const float FPS_UPDATE_INTERVAL = 0.5f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        #if UNITY_WEBGL && !UNITY_EDITOR
        IsWebGL = true;
        #else
        IsWebGL = false;
        #endif

        // Pre-calculate squared distances
        UpdateDistanceThresholds(20f, 40f, 60f, 100f);

        // Apply initial WebGL settings
        if (IsWebGL)
        {
            ApplyWebGLSettings();
        }
    }

    void ApplyWebGLSettings()
    {
        // Reduce physics overhead
        Physics.defaultSolverIterations = 3;
        Physics.defaultSolverVelocityIterations = 1;
        Physics.autoSyncTransforms = false; // Manual sync only

        // Reduce audio overhead
        AudioSettings.SetDSPBufferSize(1024, 4);

        // Target 30 FPS to be stable
        Application.targetFrameRate = 30;

        // Aggressive quality reduction for WebGL
        QualitySettings.shadowDistance = 20f;
        QualitySettings.shadowCascades = 1;
        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.vSyncCount = 0;
        QualitySettings.skinWeights = SkinWeights.TwoBones; // Cheaper animation
        QualitySettings.lodBias = 0.5f; // More aggressive LOD

        // Limit zombie population for WebGL
        if (ZombiePopulationManager.Instance != null)
        {
            ZombiePopulationManager.Instance.baseMaxZombies = 25;
            ZombiePopulationManager.Instance.zombiesPerPlayer = 10;
        }

        // Start with medium settings
        SetPerformanceLevel(PerformanceLevel.Medium);

        Debug.Log("[WebGLOptimizer] Applied WebGL-specific settings - max 25 zombies");
    }

    void Update()
    {
        // Track FPS
        fpsBuffer[fpsBufferIndex] = 1f / Time.unscaledDeltaTime;
        fpsBufferIndex = (fpsBufferIndex + 1) % fpsBuffer.Length;

        fpsUpdateTimer += Time.unscaledDeltaTime;
        if (fpsUpdateTimer >= FPS_UPDATE_INTERVAL)
        {
            fpsUpdateTimer = 0f;
            UpdatePerformanceLevel();
        }
    }

    void UpdatePerformanceLevel()
    {
        // Calculate average FPS
        float sum = 0f;
        for (int i = 0; i < fpsBuffer.Length; i++)
            sum += fpsBuffer[i];
        currentFPS = sum / fpsBuffer.Length;

        // Adjust performance level based on FPS
        PerformanceLevel newLevel = currentLevel;

        if (currentFPS < criticalFPS)
            newLevel = PerformanceLevel.Critical;
        else if (currentFPS < targetFPS * 0.8f)
            newLevel = PerformanceLevel.Low;
        else if (currentFPS < targetFPS)
            newLevel = PerformanceLevel.Medium;
        else if (currentFPS > targetFPS * 1.2f && currentLevel != PerformanceLevel.High)
            newLevel = PerformanceLevel.High;

        if (newLevel != currentLevel)
        {
            SetPerformanceLevel(newLevel);
        }
    }

    void SetPerformanceLevel(PerformanceLevel level)
    {
        currentLevel = level;

        switch (level)
        {
            case PerformanceLevel.High:
                MaxActiveAI = 25;
                MaxVisibleZombies = 60;
                AIUpdateInterval = 0.05f;
                VisionCheckInterval = 0.15f;
                PathRecalcInterval = 0.3f;
                EnableLightAvoidance = true;
                UpdateDistanceThresholds(25f, 45f, 70f, 120f);
                break;

            case PerformanceLevel.Medium:
                MaxActiveAI = 18;
                MaxVisibleZombies = 45;
                AIUpdateInterval = 0.1f;
                VisionCheckInterval = 0.25f;
                PathRecalcInterval = 0.5f;
                EnableLightAvoidance = true;
                UpdateDistanceThresholds(20f, 35f, 55f, 100f);
                break;

            case PerformanceLevel.Low:
                MaxActiveAI = 12;
                MaxVisibleZombies = 30;
                AIUpdateInterval = 0.15f;
                VisionCheckInterval = 0.35f;
                PathRecalcInterval = 0.75f;
                EnableLightAvoidance = false;
                UpdateDistanceThresholds(15f, 28f, 45f, 80f);
                break;

            case PerformanceLevel.Critical:
                MaxActiveAI = 8;
                MaxVisibleZombies = 20;
                AIUpdateInterval = 0.25f;
                VisionCheckInterval = 0.5f;
                PathRecalcInterval = 1.0f;
                EnableLightAvoidance = false;
                UpdateDistanceThresholds(12f, 22f, 35f, 60f);
                break;
        }

        Debug.Log($"[WebGLOptimizer] Performance level: {level} (FPS: {currentFPS:F1})");
    }

    void UpdateDistanceThresholds(float near, float mid, float far, float cull)
    {
        NearDistanceSqr = near * near;
        MidDistanceSqr = mid * mid;
        FarDistanceSqr = far * far;
        CullDistanceSqr = cull * cull;
    }

    /// <summary>
    /// Fast squared distance check - use instead of Vector3.Distance
    /// </summary>
    public static float SqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        float dz = a.z - b.z;
        return dx * dx + dy * dy + dz * dz;
    }

    /// <summary>
    /// Check if distance is less than threshold (using squared values)
    /// </summary>
    public static bool IsWithinDistance(Vector3 a, Vector3 b, float distanceSqr)
    {
        return SqrDistance(a, b) < distanceSqr;
    }

    /// <summary>
    /// Get performance multiplier for staggered updates (0-1 based on zombie ID)
    /// </summary>
    public bool ShouldUpdateThisFrame(int zombieId, float baseInterval)
    {
        // Stagger updates based on zombie ID to spread load
        float offset = (zombieId % 10) * 0.1f * baseInterval;
        return Time.time % baseInterval < Time.deltaTime + offset;
    }
}
