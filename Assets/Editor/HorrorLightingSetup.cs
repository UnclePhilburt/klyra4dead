using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class HorrorLightingSetup : EditorWindow
{
    [MenuItem("Tools/Setup Horror Lighting")]
    public static void SetupLighting()
    {
        if (!EditorUtility.DisplayDialog("Setup Horror Lighting",
            "This will:\n" +
            "- Remove all directional/point/spot lights in scene\n" +
            "- Set dark ambient lighting\n" +
            "- Add thick fog\n" +
            "- Create dim moonlight\n\n" +
            "Continue?", "Yes", "Cancel"))
        {
            return;
        }

        // Remove existing lights
        int removed = 0;
        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light light in allLights)
        {
            // Keep lights that are children of player (flashlights)
            if (light.transform.root.GetComponent<PlayerHealth>() != null) continue;
            if (light.transform.root.name.Contains("Player")) continue;
            
            Undo.DestroyObjectImmediate(light.gameObject);
            removed++;
        }
        Debug.Log($"[Lighting] Removed {removed} lights");

        // Setup ambient lighting - very dark
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.04f); // Almost black with slight blue
        RenderSettings.ambientIntensity = 0.3f;

        // Setup fog
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.01f, 0.01f, 0.02f); // Very dark blue-black
        RenderSettings.fogDensity = 0.025f; // Thick fog, ~40m visibility

        // Skybox - try to make it dark or remove
        RenderSettings.skybox = null; // Remove skybox for solid color
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = new Color(0.005f, 0.005f, 0.015f); // Near black

        // Create dim moonlight
        GameObject moonlight = new GameObject("Moonlight");
        Light moon = moonlight.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.4f, 0.45f, 0.6f); // Cold blue-white
        moon.intensity = 0.15f; // Very dim
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.8f;
        moonlight.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        Undo.RegisterCreatedObjectUndo(moonlight, "Create Moonlight");

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[Lighting] ========================================");
        Debug.Log("[Lighting] HORROR LIGHTING SETUP COMPLETE!");
        Debug.Log("[Lighting] - Removed existing lights");
        Debug.Log("[Lighting] - Dark ambient (near black)");
        Debug.Log("[Lighting] - Thick fog enabled");
        Debug.Log("[Lighting] - Dim moonlight added");
        Debug.Log("[Lighting] ========================================");
        Debug.Log("[Lighting] TIP: Add Flashlight component to your player!");
    }

    [MenuItem("Tools/Adjust Fog Density")]
    public static void ShowFogWindow()
    {
        FogAdjuster.ShowWindow();
    }
}

public class FogAdjuster : EditorWindow
{
    float fogDensity = 0.025f;
    Color fogColor = new Color(0.01f, 0.01f, 0.02f);
    float ambientIntensity = 0.3f;

    public static void ShowWindow()
    {
        GetWindow<FogAdjuster>("Fog Settings");
    }

    void OnGUI()
    {
        GUILayout.Label("Horror Atmosphere Settings", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        
        fogDensity = EditorGUILayout.Slider("Fog Density", fogDensity, 0.001f, 0.1f);
        fogColor = EditorGUILayout.ColorField("Fog Color", fogColor);
        ambientIntensity = EditorGUILayout.Slider("Ambient Light", ambientIntensity, 0f, 1f);

        if (EditorGUI.EndChangeCheck())
        {
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;
            RenderSettings.ambientIntensity = ambientIntensity;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        GUILayout.Space(10);
        
        if (GUILayout.Button("Pitch Black (Flashlight Only)"))
        {
            fogDensity = 0.05f;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientIntensity = 0.05f;
            ambientIntensity = 0.05f;
        }
        
        if (GUILayout.Button("Creepy Night (Some Visibility)"))
        {
            fogDensity = 0.02f;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientIntensity = 0.3f;
            ambientIntensity = 0.3f;
        }
        
        if (GUILayout.Button("Foggy Dusk"))
        {
            fogDensity = 0.015f;
            fogColor = new Color(0.1f, 0.05f, 0.05f);
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;
            RenderSettings.ambientIntensity = 0.5f;
            ambientIntensity = 0.5f;
        }
    }
}
