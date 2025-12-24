using UnityEngine;

public class WebGLPerformance : MonoBehaviour
{
    [Header("WebGL Optimizations")]
    public bool disableShadows = true;
    public int maxActiveLights = 2;
    public float particleMultiplier = 0.3f;

    [Header("General")]
    public int targetFrameRate = 60;

    void Awake()
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = 0;

#if UNITY_WEBGL
        ApplyWebGLOptimizations();
#endif
    }

    void ApplyWebGLOptimizations()
    {
        QualitySettings.SetQualityLevel(1, true);

        if (disableShadows)
        {
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowDistance = 0f;

            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                light.shadows = LightShadows.None;
            }
        }

        QualitySettings.pixelLightCount = maxActiveLights;

        foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            var main = ps.main;
            main.maxParticles = Mathf.Max(1, (int)(main.maxParticles * particleMultiplier));
        }

        QualitySettings.softParticles = false;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.lodBias = 0.5f;
        QualitySettings.skinWeights = SkinWeights.TwoBones;
        QualitySettings.globalTextureMipmapLimit = 1;
    }
}
