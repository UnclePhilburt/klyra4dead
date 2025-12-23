#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class BuildZombieAnimator : MonoBehaviour
{
    [MenuItem("Tools/Build Zombie Animator")]
    static void BuildAnimator()
    {
        Debug.Log("=== Building Zombie Animator ===");

        // Fix animation import settings
        string[] fbxPaths = new string[]
        {
            "Assets/characters/zombie/Ch10_nonPBR@Zombie Idle.fbx",
            "Assets/characters/zombie/Ch10_nonPBR@Zombie Idle (1).fbx",
            "Assets/characters/zombie/Ch10_nonPBR@Zombie Running.fbx"
        };

        foreach (string path in fbxPaths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                importer.animationType = ModelImporterAnimationType.Human;

                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                ModelImporterClipAnimation[] newClips = new ModelImporterClipAnimation[clips.Length];

                for (int i = 0; i < clips.Length; i++)
                {
                    newClips[i] = clips[i];
                    newClips[i].loopTime = true;
                    newClips[i].loopPose = true;
                    newClips[i].wrapMode = WrapMode.Loop;
                    newClips[i].lockRootRotation = true;
                    newClips[i].keepOriginalOrientation = true;
                    newClips[i].lockRootHeightY = true;
                    newClips[i].keepOriginalPositionY = true;
                    newClips[i].lockRootPositionXZ = true;
                    newClips[i].keepOriginalPositionXZ = true;
                }

                importer.clipAnimations = newClips;
                importer.SaveAndReimport();
                Debug.Log($"Fixed: {path}");
            }
        }

        // Delete old controller if exists
        string controllerPath = "Assets/characters/zombie/ZombieAnimator.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
        {
            AssetDatabase.DeleteAsset(controllerPath);
        }

        // Create animator controller
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsChasing", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        // Load clips
        AnimationClip idleClip = LoadClipFromFBX("Assets/characters/zombie/Ch10_nonPBR@Zombie Idle.fbx");
        AnimationClip runClip = LoadClipFromFBX("Assets/characters/zombie/Ch10_nonPBR@Zombie Running.fbx");

        // Create blend tree for locomotion
        BlendTree blendTree;
        AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out blendTree);

        blendTree.blendType = BlendTreeType.Simple1D;
        blendTree.blendParameter = "Speed";
        blendTree.useAutomaticThresholds = false;

        if (idleClip != null)
            blendTree.AddChild(idleClip, 0f);
        if (runClip != null)
            blendTree.AddChild(runClip, 1f);

        rootStateMachine.defaultState = locomotionState;

        // Create Attack state (placeholder - uses idle until you add attack animation)
        AnimatorState attackState = rootStateMachine.AddState("Attack");
        attackState.motion = idleClip; // Replace with attack animation when you have one
        attackState.speed = 2f; // Speed up idle as placeholder

        // Transition: Locomotion -> Attack on trigger
        AnimatorStateTransition toAttack = locomotionState.AddTransition(attackState);
        toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        toAttack.hasExitTime = false;
        toAttack.duration = 0.1f;

        // Transition: Attack -> Locomotion after animation
        AnimatorStateTransition fromAttack = attackState.AddTransition(locomotionState);
        fromAttack.hasExitTime = true;
        fromAttack.exitTime = 0.8f;
        fromAttack.duration = 0.2f;

        // Create Die state (placeholder)
        AnimatorState dieState = rootStateMachine.AddState("Die");
        dieState.motion = idleClip; // Replace with death animation when you have one
        dieState.speed = 0.5f;

        // Transition: Any -> Die on trigger
        AnimatorStateTransition toDie = rootStateMachine.AddAnyStateTransition(dieState);
        toDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
        toDie.hasExitTime = false;
        toDie.duration = 0.1f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("=== Zombie Animator Built! ===");
        Debug.Log("NOTE: Attack and Die states use placeholder animations. Add proper animations later!");
    }

    static AnimationClip LoadClipFromFBX(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        AnimationClip bestClip = null;
        float longestLength = 0f;

        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
            {
                if (clip.length > longestLength)
                {
                    bestClip = clip;
                    longestLength = clip.length;
                }
            }
        }

        if (bestClip != null)
            Debug.Log($"Loaded: {bestClip.name} from {System.IO.Path.GetFileName(path)}");
        else
            Debug.LogWarning($"No clip found in: {path}");

        return bestClip;
    }
}
#endif
