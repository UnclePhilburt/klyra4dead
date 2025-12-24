using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateMuzzleFlashPrefab
{
    [MenuItem("Tools/Create Muzzle Flash Prefab")]
    public static void Create()
    {
        // Ensure Resources folder exists
        string resourcesPath = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // Ensure Effects subfolder exists
        string effectsPath = "Assets/Resources/Effects";
        if (!AssetDatabase.IsValidFolder(effectsPath))
        {
            AssetDatabase.CreateFolder(resourcesPath, "Effects");
        }

        // Create the muzzle flash game object
        GameObject muzzleFlash = new GameObject("MuzzleFlash");
        MuzzleFlash flashScript = muzzleFlash.AddComponent<MuzzleFlash>();

        // Configure for horror style - orange/red flash, moderate intensity
        flashScript.flashColor = new Color(1f, 0.6f, 0.2f); // Orange
        flashScript.lightIntensity = 4f;
        flashScript.lightRange = 8f;
        flashScript.flashDuration = 0.04f;
        flashScript.totalLifetime = 0.12f;

        // Save as prefab
        string prefabPath = "Assets/Resources/Effects/MuzzleFlash.prefab";

        // Remove old prefab if exists
        if (File.Exists(prefabPath))
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        PrefabUtility.SaveAsPrefabAsset(muzzleFlash, prefabPath);
        Object.DestroyImmediate(muzzleFlash);

        Debug.Log("[MuzzleFlash] Prefab created at: " + prefabPath);
        Debug.Log("[MuzzleFlash] Now assign it to WeaponSystem.muzzleFlashPrefab");
        Debug.Log("[MuzzleFlash] Also create an empty child on your gun called 'FirePoint' at the barrel tip");

        // Select the created prefab
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        EditorUtility.DisplayDialog("Muzzle Flash Created",
            "Prefab saved to Resources/Effects/MuzzleFlash\n\n" +
            "Next steps:\n" +
            "1. Select your player/weapon\n" +
            "2. Create an empty child at the gun barrel tip named 'FirePoint'\n" +
            "3. Assign FirePoint to WeaponSystem -> Fire Point\n" +
            "4. Assign MuzzleFlash prefab to WeaponSystem -> Muzzle Flash Prefab",
            "OK");
    }

    [MenuItem("Tools/Setup Weapon Fire Point")]
    public static void SetupFirePoint()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select your weapon or player object first.", "OK");
            return;
        }

        // Find or create fire point
        Transform firePoint = selected.transform.Find("FirePoint");
        if (firePoint == null)
        {
            // Look for gun-related objects
            Transform gun = FindGunTransform(selected.transform);
            if (gun != null)
            {
                GameObject fp = new GameObject("FirePoint");
                fp.transform.SetParent(gun);
                fp.transform.localPosition = new Vector3(0, 0, 0.5f); // Forward from gun
                fp.transform.localRotation = Quaternion.identity;
                firePoint = fp.transform;
                Debug.Log("[FirePoint] Created at: " + gun.name + "/FirePoint");
            }
            else
            {
                GameObject fp = new GameObject("FirePoint");
                fp.transform.SetParent(selected.transform);
                fp.transform.localPosition = new Vector3(0, 1.5f, 1f); // Default chest height, forward
                fp.transform.localRotation = Quaternion.identity;
                firePoint = fp.transform;
                Debug.Log("[FirePoint] Created at: " + selected.name + "/FirePoint (adjust position manually)");
            }
        }

        // Try to assign to WeaponSystem
        WeaponSystem ws = selected.GetComponent<WeaponSystem>();
        if (ws == null)
        {
            ws = selected.GetComponentInChildren<WeaponSystem>();
        }

        if (ws != null)
        {
            ws.firePoint = firePoint;

            // Try to assign muzzle flash prefab
            GameObject flashPrefab = Resources.Load<GameObject>("Effects/MuzzleFlash");
            if (flashPrefab != null)
            {
                ws.muzzleFlashPrefab = flashPrefab;
                Debug.Log("[FirePoint] MuzzleFlash prefab auto-assigned!");
            }

            EditorUtility.SetDirty(ws);
            Debug.Log("[FirePoint] Assigned to WeaponSystem.firePoint");
        }

        Selection.activeGameObject = firePoint.gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();

        EditorUtility.DisplayDialog("Fire Point Created",
            "FirePoint created and selected.\n\n" +
            "Move it to the tip of your gun barrel in the Scene view.",
            "OK");
    }

    static Transform FindGunTransform(Transform root)
    {
        string[] gunNames = { "gun", "weapon", "rifle", "pistol", "shotgun", "smg", "m4", "ak", "barrel" };

        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            string nameLower = child.name.ToLower();
            foreach (string gunName in gunNames)
            {
                if (nameLower.Contains(gunName))
                {
                    return child;
                }
            }
        }

        return null;
    }
}
