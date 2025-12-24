using UnityEngine;
using UnityEditor;

public class SetupHorrorSkybox
{
    [MenuItem("Tools/Setup Dark Horror Skybox")]
    public static void Setup()
    {
        // Apply Cold Night skybox (darkest)
        Material nightSkybox = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/AllSkyFree/Cold Night/Cold Night.mat");
        
        if (nightSkybox != null)
        {
            RenderSettings.skybox = nightSkybox;
            Debug.Log("[Skybox] Applied Cold Night skybox");
        }
        else
        {
            Debug.LogError("[Skybox] Cold Night.mat not found!");
        }

        // Set camera to skybox
        if (Camera.main != null)
            Camera.main.clearFlags = CameraClearFlags.Skybox;

        // Dark ambient lighting
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.04f);
        RenderSettings.ambientIntensity = 0.2f;

        // Dark fog
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.01f, 0.01f, 0.02f);
        RenderSettings.fogDensity = 0.025f;

        DynamicGI.UpdateEnvironment();
        
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[Skybox] Dark horror atmosphere applied!");
    }
}
