using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Tracks how visible the player is based on surrounding light sources.
/// Zombies can see players from further away when they're in light.
/// </summary>
public class PlayerVisibility : MonoBehaviour
{
    public static PlayerVisibility LocalInstance { get; private set; }

    [Header("Visibility Settings")]
    [Tooltip("Base visibility when in complete darkness (0-1)")]
    public float baseVisibility = 0.3f;
    [Tooltip("Max visibility when fully lit (0-1)")]
    public float maxVisibility = 1f;
    [Tooltip("How quickly visibility updates")]
    public float updateInterval = 0.2f;

    [Header("Light Detection")]
    [Tooltip("Maximum distance to check for lights")]
    public float lightCheckRadius = 30f;
    [Tooltip("Layers that block light")]
    public LayerMask lightBlockingLayers;

    [Header("Debug")]
    public bool showDebug = false;

    // Current visibility (0 = dark, 1 = fully lit)
    public float CurrentVisibility { get; private set; }

    // Cached lights for performance
    private Light[] sceneLights;
    private float nextUpdateTime;
    private float nextLightCacheTime;
    private float lightCacheInterval = 2f;

    void Awake()
    {
        // Check if this is the local player
        var photonView = GetComponent<Photon.Pun.PhotonView>();
        if (photonView == null || photonView.IsMine)
        {
            LocalInstance = this;
        }
    }

    void Start()
    {
        CurrentVisibility = baseVisibility;
        CacheLights();
    }

    void Update()
    {
        // Only update for local player
        var photonView = GetComponent<Photon.Pun.PhotonView>();
        if (photonView != null && !photonView.IsMine) return;

        // Periodically refresh light cache (lights can be added/removed)
        if (Time.time >= nextLightCacheTime)
        {
            CacheLights();
            nextLightCacheTime = Time.time + lightCacheInterval;
        }

        // Update visibility calculation
        if (Time.time >= nextUpdateTime)
        {
            CalculateVisibility();
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    void CacheLights()
    {
        sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
    }

    void CalculateVisibility()
    {
        if (sceneLights == null || sceneLights.Length == 0)
        {
            CurrentVisibility = baseVisibility;
            return;
        }

        float totalLight = 0f;
        Vector3 playerPos = transform.position + Vector3.up; // Check from chest height

        foreach (Light light in sceneLights)
        {
            if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                continue;

            // Skip very dim lights
            if (light.intensity < 0.1f)
                continue;

            float contribution = CalculateLightContribution(light, playerPos);
            totalLight += contribution;
        }

        // Also check ambient light
        float ambientContribution = GetAmbientLightContribution();
        totalLight += ambientContribution;

        // Clamp and lerp to smooth changes
        float targetVisibility = Mathf.Lerp(baseVisibility, maxVisibility, Mathf.Clamp01(totalLight));
        CurrentVisibility = Mathf.Lerp(CurrentVisibility, targetVisibility, Time.deltaTime * 5f);

        if (showDebug)
        {
            Debug.Log($"[Visibility] Light: {totalLight:F2}, Visibility: {CurrentVisibility:F2}");
        }
    }

    float CalculateLightContribution(Light light, Vector3 playerPos)
    {
        Vector3 lightPos = light.transform.position;
        float distance = Vector3.Distance(lightPos, playerPos);

        // Check if within light range
        float range = light.range;
        if (light.type == LightType.Directional)
        {
            range = 1000f; // Directional lights affect everything
        }

        if (distance > range && light.type != LightType.Directional)
            return 0f;

        float contribution = 0f;

        switch (light.type)
        {
            case LightType.Point:
                contribution = CalculatePointLight(light, playerPos, distance);
                break;

            case LightType.Spot:
                contribution = CalculateSpotLight(light, playerPos, distance);
                break;

            case LightType.Directional:
                contribution = CalculateDirectionalLight(light, playerPos);
                break;
        }

        return contribution;
    }

    float CalculatePointLight(Light light, Vector3 playerPos, float distance)
    {
        // Check line of sight
        Vector3 direction = (light.transform.position - playerPos).normalized;
        if (Physics.Raycast(playerPos, direction, distance, lightBlockingLayers))
            return 0f; // Light is blocked

        // Inverse square falloff
        float attenuation = 1f - Mathf.Clamp01(distance / light.range);
        attenuation = attenuation * attenuation;

        return light.intensity * attenuation * 0.5f;
    }

    float CalculateSpotLight(Light light, Vector3 playerPos, float distance)
    {
        // Check if player is in the spotlight cone
        Vector3 toPlayer = (playerPos - light.transform.position).normalized;
        float angle = Vector3.Angle(light.transform.forward, toPlayer);

        if (angle > light.spotAngle / 2f)
            return 0f; // Outside cone

        // Check line of sight
        Vector3 direction = (light.transform.position - playerPos).normalized;
        if (Physics.Raycast(playerPos, direction, distance, lightBlockingLayers))
            return 0f; // Light is blocked

        // Distance attenuation
        float distanceAttenuation = 1f - Mathf.Clamp01(distance / light.range);
        distanceAttenuation = distanceAttenuation * distanceAttenuation;

        // Angle attenuation (brighter in center of cone)
        float angleAttenuation = 1f - Mathf.Clamp01(angle / (light.spotAngle / 2f));

        return light.intensity * distanceAttenuation * angleAttenuation * 0.7f;
    }

    float CalculateDirectionalLight(Light light, Vector3 playerPos)
    {
        // Check if player is in shadow (raycast toward light direction)
        Vector3 lightDir = -light.transform.forward;

        if (Physics.Raycast(playerPos, lightDir, 100f, lightBlockingLayers))
            return 0f; // In shadow

        // Directional lights provide consistent illumination
        return light.intensity * 0.3f;
    }

    float GetAmbientLightContribution()
    {
        // Check Unity's ambient light settings
        Color ambient = RenderSettings.ambientLight;
        float ambientIntensity = (ambient.r + ambient.g + ambient.b) / 3f;
        return ambientIntensity * RenderSettings.ambientIntensity * 0.2f;
    }

    /// <summary>
    /// Get the effective vision range multiplier for zombies.
    /// Returns 1.0 in full light, lower values in darkness.
    /// </summary>
    public float GetVisionMultiplier()
    {
        return CurrentVisibility;
    }

    /// <summary>
    /// Check if this player is visible from a specific position (for zombie checks)
    /// </summary>
    public float GetVisibilityFrom(Vector3 observerPosition)
    {
        // Base visibility from light
        float visibility = CurrentVisibility;

        // Could add additional factors here:
        // - Distance (further = harder to see)
        // - Movement (moving players are more visible)
        // - Crouching (if implemented)

        return visibility;
    }

    void OnDrawGizmosSelected()
    {
        if (!showDebug) return;

        // Draw visibility sphere (size represents visibility)
        Gizmos.color = Color.Lerp(Color.black, Color.yellow, CurrentVisibility);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, CurrentVisibility * 2f);
    }
}
