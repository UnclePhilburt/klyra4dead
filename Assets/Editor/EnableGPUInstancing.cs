using UnityEngine;
using UnityEditor;

public class EnableGPUInstancing : EditorWindow
{
    [MenuItem("Tools/Performance/Enable GPU Instancing on All Materials")]
    public static void EnableOnAllMaterials()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material");
        int count = 0;

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat != null && mat.shader != null)
            {
                if (!mat.enableInstancing)
                {
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                    count++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[GPU Instancing] Enabled on {count} materials");
        EditorUtility.DisplayDialog("GPU Instancing", $"Enabled GPU Instancing on {count} materials.", "OK");
    }
}
