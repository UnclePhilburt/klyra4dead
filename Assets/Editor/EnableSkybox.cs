using UnityEngine;
using UnityEditor;

public class EnableSkybox
{
    [MenuItem("Tools/Enable Skybox")]
    public static void Enable()
    {
        // Set camera to use skybox
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.Skybox;
            EditorUtility.SetDirty(Camera.main);
            Debug.Log("[Skybox] Camera set to use Skybox");
        }
        
        // Find skybox material in project
        string[] guids = AssetDatabase.FindAssets("t:Material skybox");
        if (guids.Length > 0)
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && mat.shader.name.Contains("Skybox"))
                {
                    RenderSettings.skybox = mat;
                    Debug.Log($"[Skybox] Using skybox: {path}");
                    break;
                }
            }
        }
        
        // Make sure ambient mode uses skybox
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        DynamicGI.UpdateEnvironment();
        
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            
        Debug.Log("[Skybox] Skybox enabled!");
    }
}
