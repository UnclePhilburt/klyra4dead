#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateWeaponPrefabs : MonoBehaviour
{
    [MenuItem("Tools/Create Weapon Prefabs")]
    static void CreatePrefabs()
    {
        string gunsPath = "Assets/Low Poly Guns/Models/Guns";
        string outputPath = "Assets/Prefabs/Weapons";

        // Create output folder if needed
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(outputPath))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Weapons");
        }

        string[] gunFolders = new string[]
        {
            "assault1", "assault2", "assault3", "assault4",
            "pistol1", "pistol2", "pistol3", "pistol4",
            "shotgun1", "shotgun2",
            "smg1", "smg2",
            "sniper1", "sniper2"
        };

        int created = 0;

        foreach (string gun in gunFolders)
        {
            string fbxPath = $"{gunsPath}/{gun}/{gun}.fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

            if (model == null)
            {
                Debug.LogWarning($"Could not find: {fbxPath}");
                continue;
            }

            string prefabPath = $"{outputPath}/{gun}.prefab";

            // Delete old prefab if exists so we can recreate with materials
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            // Create instance
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = gun;

            // The gun models are usually too big, scale down
            instance.transform.localScale = Vector3.one * 0.18f;

            // Load the diffuse texture
            string diffusePath = $"{gunsPath}/{gun}/{gun}_diffuse.png";
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);

            Material matToUse = null;

            if (diffuse != null)
            {
                // Use the existing material from the gun folder and upgrade it
                string origMatPath = $"{gunsPath}/{gun}/{gun}.mat";
                Material origMat = AssetDatabase.LoadAssetAtPath<Material>(origMatPath);

                if (origMat != null)
                {
                    // Upgrade the existing material to URP
                    origMat.shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (origMat.shader == null || origMat.shader.name == "Hidden/InternalErrorShader")
                    {
                        origMat.shader = Shader.Find("Standard");
                    }

                    // Make sure texture is set on both properties
                    origMat.SetTexture("_BaseMap", diffuse);
                    origMat.SetTexture("_MainTex", diffuse);
                    origMat.mainTexture = diffuse;

                    // Check for normal map
                    string normalPath = $"{gunsPath}/{gun}/{gun}_normal.png";
                    Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                    if (normal != null)
                    {
                        origMat.SetTexture("_BumpMap", normal);
                        origMat.EnableKeyword("_NORMALMAP");
                    }

                    EditorUtility.SetDirty(origMat);
                    AssetDatabase.SaveAssets();

                    matToUse = origMat;
                    Debug.Log($"Updated existing material: {origMatPath}");
                }
                else
                {
                    Debug.LogWarning($"No material found at {origMatPath}");
                }
            }
            else
            {
                Debug.LogWarning($"No diffuse texture found for {gun} at {diffusePath}");
            }

            // Apply material to all renderers
            if (matToUse != null)
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = matToUse;
                    Debug.Log($"Applied material {matToUse.name} to: {renderer.name}");
                }
            }

            // Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            DestroyImmediate(instance);

            Debug.Log($"Created weapon prefab: {prefabPath}");
            created++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"Created {created} weapon prefabs in {outputPath}");
    }
}
#endif
