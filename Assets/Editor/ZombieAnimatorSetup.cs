using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class ZombieAnimatorSetup : EditorWindow
{
    [MenuItem("Tools/Setup Zombie Animator")]
    public static void SetupAnimator()
    {
        // Load the animator controller
        string path = "Assets/characters/zombie/ZombieAnimator.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

        if (controller == null)
        {
            Debug.LogError($"Could not find animator controller at: {path}");
            return;
        }

        // Add missing parameters
        AddParameterIfMissing(controller, "IsChasing", AnimatorControllerParameterType.Bool);
        AddParameterIfMissing(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameterIfMissing(controller, "Die", AnimatorControllerParameterType.Trigger);

        // Save changes
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("[ZombieAnimatorSetup] Added missing parameters: IsChasing (bool), Attack (trigger), Die (trigger)");
    }

    static void AddParameterIfMissing(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        // Check if parameter already exists
        foreach (var param in controller.parameters)
        {
            if (param.name == name)
            {
                Debug.Log($"[ZombieAnimatorSetup] Parameter '{name}' already exists, skipping");
                return;
            }
        }

        // Add the parameter
        controller.AddParameter(name, type);
        Debug.Log($"[ZombieAnimatorSetup] Added parameter: {name} ({type})");
    }
}
