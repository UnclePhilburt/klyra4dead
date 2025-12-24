using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class FixShadows : EditorWindow
{
    [MenuItem("Tools/Fix Shadows")]
    public static void Fix()
    {
        // Enable shadows on all directional lights
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        int fixed_count = 0;
        
        foreach (Light light in lights)
        {
            if (light.type == LightType.Directional)
            {
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 1f;
                light.shadowBias = 0.05f;
                light.shadowNormalBias = 0.4f;
                light.shadowNearPlane = 0.2f;
                EditorUtility.SetDirty(light);
                fixed_count++;
                Debug.Log($"[Shadows] Enabled soft shadows on: {light.gameObject.name}");
            }
        }

        // Fix quality settings for shadows
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.shadowDistance = 150f;
        QualitySettings.shadowCascades = 4;
        
        Debug.Log($"[Shadows] Fixed {fixed_count} lights");
        Debug.Log("[Shadows] Quality Settings updated:");
        Debug.Log("  - Shadow Quality: All");
        Debug.Log("  - Shadow Resolution: High");
        Debug.Log("  - Shadow Distance: 150");
        Debug.Log("  - Shadow Cascades: 4");
        
        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
