using UnityEngine;
using UnityEditor;

public class SkyboxPicker
{
    static void ApplySkybox(string path)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
        {
            RenderSettings.skybox = mat;
            
            if (Camera.main != null)
                Camera.main.clearFlags = CameraClearFlags.Skybox;
            
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();
            
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            
            Debug.Log("[Skybox] Applied: " + path);
        }
        else
        {
            Debug.LogError("[Skybox] Could not load: " + path);
        }
    }

    [MenuItem("Tools/Skybox/Cold Night (Horror)")]
    static void ColdNight() => ApplySkybox("Assets/AllSkyFree/Cold Night/Cold Night.mat");

    [MenuItem("Tools/Skybox/Night Moon Burst")]
    static void NightMoon() => ApplySkybox("Assets/AllSkyFree/Night MoonBurst/Night Moon Burst.mat");

    [MenuItem("Tools/Skybox/Deep Dusk")]
    static void DeepDusk() => ApplySkybox("Assets/AllSkyFree/Deep Dusk/Deep Dusk.mat");

    [MenuItem("Tools/Skybox/Cold Sunset")]
    static void ColdSunset() => ApplySkybox("Assets/AllSkyFree/Cold Sunset/Cold Sunset.mat");

    [MenuItem("Tools/Skybox/Epic Blue Sunset")]
    static void BlueSunset() => ApplySkybox("Assets/AllSkyFree/Epic_BlueSunset/Epic_BlueSunset.mat");

    [MenuItem("Tools/Skybox/Overcast (Gloomy)")]
    static void Overcast() => ApplySkybox("Assets/AllSkyFree/Overcast Low/AllSky_Overcast4_Low.mat");

    [MenuItem("Tools/Skybox/Cartoon Day")]
    static void CartoonDay() => ApplySkybox("Assets/AllSkyFree/Cartoon Base BlueSky/Day_BlueSky_Nothing.mat");

    [MenuItem("Tools/Skybox/Cartoon Night")]
    static void CartoonNight() => ApplySkybox("Assets/AllSkyFree/Cartoon Base NightSky/Cartoon Base NightSky.mat");

    [MenuItem("Tools/Skybox/Space")]
    static void Space() => ApplySkybox("Assets/AllSkyFree/Space_AnotherPlanet/AllSky_Space_AnotherPlanet.mat");
}
