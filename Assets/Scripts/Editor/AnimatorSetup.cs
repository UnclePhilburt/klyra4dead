#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class AnimatorSetup : MonoBehaviour
{
    [MenuItem("Tools/Create SWAT Animator Controller")]
    static void CreateAnimatorController()
    {
        // Create the controller in the correct folder
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(
            "Assets/characters/swat/SwatAnimator.controller");

        // Add parameters for 2D blend tree
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Direction", AnimatorControllerParameterType.Float);

        // Get the root state machine
        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        // Load animations from FBX files
        AnimationClip idleClip = LoadClipFromFBX("Assets/characters/swat/SwatRifle Idle.fbx");
        AnimationClip walkClip = LoadClipFromFBX("Assets/characters/swat/Swat@Rifle Walk.fbx");
        AnimationClip runClip = LoadClipFromFBX("Assets/characters/swat/Swat@Rifle Run.fbx");
        AnimationClip backWalkClip = LoadClipFromFBX("Assets/characters/swat/Swat@Backwards Rifle Walk.fbx");
        AnimationClip backRunClip = LoadClipFromFBX("Assets/characters/swat/Swat@Backwards Rifle Run.fbx");

        // Log what we found
        Debug.Log($"Idle: {(idleClip != null ? idleClip.name : "NOT FOUND")}");
        Debug.Log($"Walk: {(walkClip != null ? walkClip.name : "NOT FOUND")}");
        Debug.Log($"Run: {(runClip != null ? runClip.name : "NOT FOUND")}");
        Debug.Log($"Back Walk: {(backWalkClip != null ? backWalkClip.name : "NOT FOUND")}");
        Debug.Log($"Back Run: {(backRunClip != null ? backRunClip.name : "NOT FOUND")}");

        // Create 2D Blend Tree
        BlendTree blendTree;
        AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out blendTree);

        blendTree.blendType = BlendTreeType.FreeformCartesian2D;
        blendTree.blendParameter = "Direction";  // X axis: -1 backward, 1 forward
        blendTree.blendParameterY = "Speed";     // Y axis: 0 idle, 1 run

        // Add motions to blend tree
        // Position format: (Direction, Speed)
        // Direction: -1 = backward, 0 = neutral, 1 = forward
        // Speed: 0 = idle/walk speed, 1 = run speed

        // Center - Idle (no movement)
        if (idleClip != null)
        {
            blendTree.AddChild(idleClip, new Vector2(0f, 0f));
        }

        // Forward movement
        if (walkClip != null)
        {
            blendTree.AddChild(walkClip, new Vector2(1f, 0.3f));  // Forward walk
        }
        if (runClip != null)
        {
            blendTree.AddChild(runClip, new Vector2(1f, 1f));     // Forward run
        }

        // Backward movement
        if (backWalkClip != null)
        {
            blendTree.AddChild(backWalkClip, new Vector2(-1f, 0.3f));  // Backward walk
        }
        if (backRunClip != null)
        {
            blendTree.AddChild(backRunClip, new Vector2(-1f, 1f));     // Backward run
        }

        // Set as default state
        rootStateMachine.defaultState = locomotionState;

        // Save
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("Created SwatAnimator.controller with 2D blend tree at Assets/characters/swat/");
        Debug.Log("Parameters: Speed (0-1), Direction (-1 backward to 1 forward)");
    }

    static AnimationClip LoadClipFromFBX(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
            {
                return clip;
            }
        }
        Debug.LogWarning($"Could not find animation clip in: {path}");
        return null;
    }
}
#endif
