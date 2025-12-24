using UnityEngine;

/// <summary>
/// Utility class to check light levels at any position.
/// Used by zombies to avoid lit areas.
/// </summary>
public static class LightDetection
{
    private static Light[] cachedLights;
    private static float lastCacheTime;
    private static float cacheInterval = 0.5f; // Refresh more often

    /// <summary>
    /// Get light intensity at a world position (0 = dark, higher = brighter)
    /// </summary>
    public static float GetLightLevelAt(Vector3 position, LayerMask blockingLayers = default)
    {
        RefreshLightCache();

        if (cachedLights == null || cachedLights.Length == 0)
            return GetAmbientLight();

        float totalLight = GetAmbientLight();

        foreach (Light light in cachedLights)
        {
            if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                continue;

            if (light.intensity < 0.05f)
                continue;

            float contribution = GetLightContribution(light, position, blockingLayers);
            totalLight += contribution;
        }

        return totalLight;
    }

    static void RefreshLightCache()
    {
        if (cachedLights == null || Time.time - lastCacheTime > cacheInterval)
        {
            cachedLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            lastCacheTime = Time.time;
        }
    }

    /// <summary>
    /// Force refresh the light cache (call when lights are added/removed)
    /// </summary>
    public static void RefreshCache()
    {
        cachedLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        lastCacheTime = Time.time;
    }

    static float GetLightContribution(Light light, Vector3 position, LayerMask blockingLayers)
    {
        Vector3 lightPos = light.transform.position;
        float distance = Vector3.Distance(lightPos, position);

        switch (light.type)
        {
            case LightType.Point:
                if (distance > light.range) return 0f;
                
                // Check line of sight (only if mask specified)
                if (blockingLayers.value != 0)
                {
                    Vector3 dir = (lightPos - position).normalized;
                    if (Physics.Raycast(position + Vector3.up * 0.5f, dir, distance - 0.5f, blockingLayers))
                        return 0f;
                }
                
                // Quadratic falloff
                float normalizedDist = distance / light.range;
                float attenuation = Mathf.Clamp01(1f - normalizedDist * normalizedDist);
                return light.intensity * attenuation;

            case LightType.Spot:
                if (distance > light.range) return 0f;
                
                Vector3 toPos = (position - lightPos).normalized;
                float angle = Vector3.Angle(light.transform.forward, toPos);
                if (angle > light.spotAngle / 2f) return 0f;
                
                // Check line of sight
                if (blockingLayers.value != 0)
                {
                    Vector3 dir = (lightPos - position).normalized;
                    if (Physics.Raycast(position + Vector3.up * 0.5f, dir, distance - 0.5f, blockingLayers))
                        return 0f;
                }
                
                float distAtten = Mathf.Clamp01(1f - (distance / light.range));
                distAtten = distAtten * distAtten;
                float angleAtten = 1f - Mathf.Clamp01(angle / (light.spotAngle / 2f));
                return light.intensity * distAtten * angleAtten;

            case LightType.Directional:
                // Check if in shadow
                if (blockingLayers.value != 0)
                {
                    if (Physics.Raycast(position + Vector3.up * 0.5f, -light.transform.forward, 100f, blockingLayers))
                        return 0f;
                }
                return light.intensity * 0.5f;

            default:
                return 0f;
        }
    }

    static float GetAmbientLight()
    {
        Color ambient = RenderSettings.ambientLight;
        float intensity = (ambient.r + ambient.g + ambient.b) / 3f;
        return intensity * RenderSettings.ambientIntensity * 0.3f;
    }

    /// <summary>
    /// Check if a position is considered "in light"
    /// </summary>
    public static bool IsInLight(Vector3 position, float threshold = 0.3f, LayerMask blockingLayers = default)
    {
        return GetLightLevelAt(position, blockingLayers) > threshold;
    }

    /// <summary>
    /// Find the darkest nearby position
    /// </summary>
    public static Vector3 FindDarkestNearby(Vector3 center, float radius, int samples = 8, LayerMask blockingLayers = default)
    {
        Vector3 darkest = center;
        float lowestLight = GetLightLevelAt(center, blockingLayers);

        for (int i = 0; i < samples; i++)
        {
            float angle = (360f / samples) * i;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 testPos = center + dir * radius;

            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(testPos, out hit, radius, UnityEngine.AI.NavMesh.AllAreas))
            {
                float light = GetLightLevelAt(hit.position + Vector3.up, blockingLayers);
                if (light < lowestLight)
                {
                    lowestLight = light;
                    darkest = hit.position;
                }
            }
        }

        return darkest;
    }

    /// <summary>
    /// Debug visualization - call from OnDrawGizmos
    /// </summary>
    public static void DrawLightDebug(Vector3 position)
    {
        float light = GetLightLevelAt(position + Vector3.up);
        Gizmos.color = Color.Lerp(Color.black, Color.yellow, Mathf.Clamp01(light));
        Gizmos.DrawSphere(position + Vector3.up * 2f, 0.3f);
    }

    /// <summary>
    /// Get distance to the nearest active light source
    /// </summary>
    public static float GetDistanceToNearestLight(Vector3 position)
    {
        RefreshLightCache();

        if (cachedLights == null || cachedLights.Length == 0)
            return float.MaxValue;

        float nearestDist = float.MaxValue;

        foreach (Light light in cachedLights)
        {
            if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                continue;

            if (light.intensity < 0.1f)
                continue;

            // Skip directional lights for distance calculation
            if (light.type == LightType.Directional)
                continue;

            float dist = Vector3.Distance(position, light.transform.position);
            
            // Only consider lights within their range
            if (dist < light.range && dist < nearestDist)
            {
                nearestDist = dist;
            }
        }

        return nearestDist;
    }

    /// <summary>
    /// Get direction to flee from nearby lights
    /// </summary>
    public static Vector3 GetFleeDirection(Vector3 position, LayerMask blockingLayers = default)
    {
        RefreshLightCache();

        if (cachedLights == null || cachedLights.Length == 0)
            return Vector3.zero;

        Vector3 fleeDir = Vector3.zero;
        int lightCount = 0;

        foreach (Light light in cachedLights)
        {
            if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                continue;

            if (light.intensity < 0.1f)
                continue;

            // Skip directional lights
            if (light.type == LightType.Directional)
                continue;

            float dist = Vector3.Distance(position, light.transform.position);
            
            // Only flee from lights we're within range of
            if (dist < light.range)
            {
                // Direction away from this light, weighted by proximity
                Vector3 awayFromLight = (position - light.transform.position).normalized;
                float weight = 1f - (dist / light.range); // Closer = more weight
                fleeDir += awayFromLight * weight * light.intensity;
                lightCount++;
            }
        }

        if (lightCount > 0)
        {
            fleeDir.y = 0; // Keep horizontal
            return fleeDir.normalized;
        }

        return Vector3.zero;
    }

}
