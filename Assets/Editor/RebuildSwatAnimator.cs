#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

public class RebuildSwatAnimator : MonoBehaviour
{
    [MenuItem("Tools/Rebuild SWAT Animator (All-in-One)")]
    static void RebuildAll()
    {
        Debug.Log("=== Rebuilding SWAT Animator ===");

        // Step 1: Fix animation looping
        FixLooping();

        // Step 2: Delete old animator controller
        string controllerPath = "Assets/characters/swat/SwatAnimator.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
        {
            AssetDatabase.DeleteAsset(controllerPath);
            Debug.Log("Deleted old SwatAnimator.controller");
        }

        // Step 3: Create new animator controller
        CreateAnimatorController();

        Debug.Log("=== SWAT Animator Rebuild Complete! ===");
    }

    static void FixLooping()
    {
        string[] fbxPaths = new string[]
        {
            "Assets/characters/swat/SwatRifle Idle.fbx",
            "Assets/characters/swat/Swat@Rifle Walk.fbx",
            "Assets/characters/swat/Swat@Rifle Run.fbx",
            "Assets/characters/swat/Swat@Backwards Rifle Walk.fbx",
            "Assets/characters/swat/Swat@Backwards Rifle Run.fbx",
            "Assets/characters/swat/Swat@Rifle Side StepLeft.fbx",
            "Assets/characters/swat/Swat@Rifle Side StepRight.fbx",
            "Assets/characters/swat/Swat@Sprint Left.fbx",
            "Assets/characters/swat/Swat@Sprint Right.fbx",
            "Assets/characters/swat/Swat@Walk Forward Left.fbx",
            "Assets/characters/swat/Swat@Walk Forward Right.fbx",
            "Assets/characters/swat/Swat@Sprint Forward Left.fbx",
            "Assets/characters/swat/Swat@Sprint Forward Right.fbx",
            "Assets/characters/swat/Swat@Walk Backward Left.fbx",
            "Assets/characters/swat/Swat@Walk Backward Right.fbx",
            "Assets/characters/swat/Swat@Run Backward Left.fbx",
            "Assets/characters/swat/Swat@Sprint Backward Right.fbx",
            "Assets/characters/swat/Swat@Rifle Aiming Idle.fbx",
            "Assets/characters/swat/Swat@Rifle Down To Aim.fbx",
            "Assets/characters/swat/Swat@Rifle Aim To Down.fbx",
            "Assets/characters/swat/Swat@Rifle Idle2.fbx",
            "Assets/characters/swat/Swat@Rifle Idle3.fbx",
            "Assets/characters/swat/Swat@Firing Rifle.fbx",
            "Assets/characters/swat/Swat@Reloadingidle.fbx",
            "Assets/characters/swat/Swat@Reloadwalking.fbx",
            "Assets/characters/swat/Swat@Reloadrunning.fbx",
            "Assets/characters/swat/Swat@Drop Kick.fbx"
        };

        foreach (string path in fbxPaths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                // Set to Humanoid rig
                importer.animationType = ModelImporterAnimationType.Human;

                // Must use defaultClipAnimations first to get the clip info, then assign to clipAnimations
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

                // Create new array with our settings
                // Transition animations, firing, reload, and kick should NOT loop
                bool shouldLoop = !path.Contains("To Aim") && !path.Contains("Aim To") && !path.Contains("Firing") && !path.Contains("Reload") && !path.Contains("Kick") && !path.Contains("Drop");

                ModelImporterClipAnimation[] newClips = new ModelImporterClipAnimation[clips.Length];
                for (int i = 0; i < clips.Length; i++)
                {
                    newClips[i] = clips[i];
                    newClips[i].loopTime = shouldLoop;
                    newClips[i].loopPose = shouldLoop;
                    newClips[i].wrapMode = shouldLoop ? WrapMode.Loop : WrapMode.Once;

                    // Bake rotation into pose
                    newClips[i].lockRootRotation = true;
                    newClips[i].keepOriginalOrientation = true;

                    // Bake position Y (vertical) into pose
                    newClips[i].lockRootHeightY = true;
                    newClips[i].keepOriginalPositionY = true;

                    // Bake XZ position too
                    newClips[i].lockRootPositionXZ = true;
                    newClips[i].keepOriginalPositionXZ = true;
                }

                importer.clipAnimations = newClips;
                importer.SaveAndReimport();
                Debug.Log($"Fixed rig + looping: {path} ({newClips.Length} clips)");
            }
        }
    }

    static void CreateAnimatorController()
    {
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(
            "Assets/characters/swat/SwatAnimator.controller");

        // Add parameters for 2D blend tree (horizontal and vertical movement)
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IdleVariant", AnimatorControllerParameterType.Int);
        controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsReloading", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Kick", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        // Load all animations
        AnimationClip idle = LoadClipFromFBX("Assets/characters/swat/SwatRifle Idle.fbx");
        AnimationClip walkForward = LoadClipFromFBX("Assets/characters/swat/Swat@Rifle Walk.fbx");
        AnimationClip runForward = LoadClipFromFBX("Assets/characters/swat/Swat@Rifle Run.fbx");
        AnimationClip walkBack = LoadClipFromFBX("Assets/characters/swat/Swat@Backwards Rifle Walk.fbx");
        AnimationClip runBack = LoadClipFromFBX("Assets/characters/swat/Swat@Backwards Rifle Run.fbx");
        AnimationClip stepLeft = LoadClipFromFBX("Assets/characters/swat/Swat@Rifle Side StepLeft.fbx");
        AnimationClip stepRight = LoadClipFromFBX("Assets/characters/swat/Swat@Rifle Side StepRight.fbx");
        AnimationClip sprintLeft = LoadClipFromFBX("Assets/characters/swat/Swat@Sprint Left.fbx");
        AnimationClip sprintRight = LoadClipFromFBX("Assets/characters/swat/Swat@Sprint Right.fbx");
        AnimationClip walkForwardLeft = LoadClipFromFBX("Assets/characters/swat/Swat@Walk Forward Left.fbx");
        AnimationClip walkForwardRight = LoadClipFromFBX("Assets/characters/swat/Swat@Walk Forward Right.fbx");
        AnimationClip sprintForwardLeft = LoadClipFromFBX("Assets/characters/swat/Swat@Sprint Forward Left.fbx");
        AnimationClip sprintForwardRight = LoadClipFromFBX("Assets/characters/swat/Swat@Sprint Forward Right.fbx");
        AnimationClip walkBackLeft = LoadClipFromFBX("Assets/characters/swat/Swat@Walk Backward Left.fbx");
        AnimationClip walkBackRight = LoadClipFromFBX("Assets/characters/swat/Swat@Walk Backward Right.fbx");
        AnimationClip runBackLeft = LoadClipFromFBX("Assets/characters/swat/Swat@Run Backward Left.fbx");
        AnimationClip sprintBackRight = LoadClipFromFBX("Assets/characters/swat/Swat@Sprint Backward Right.fbx");

        // Aiming animations
        AnimationClip aimingIdle = LoadClipFromFBX("Assets/characters/swat/Swat@Rifle Aiming Idle.fbx");
        AnimationClip downToAim = LoadClipFromFBX("Assets/characters/swat/Swat@Rifle Down To Aim.fbx");
        AnimationClip aimToDown = LoadClipFromFBX("Assets/characters/swat/Swat@Rifle Aim To Down.fbx");

        // Additional idle variations
        AnimationClip idle2 = LoadClipFromFBX("Assets/characters/swat/Swat@Rifle Idle2.fbx");
        AnimationClip idle3 = LoadClipFromFBX("Assets/characters/swat/Swat@Rifle Idle3.fbx");

        // Firing animation
        AnimationClip firing = LoadClipFromFBX("Assets/characters/swat/Swat@Firing Rifle.fbx");

        // Reload animations
        AnimationClip reloadIdle = LoadClipFromFBX("Assets/characters/swat/Swat@Reloadingidle.fbx");
        AnimationClip reloadWalk = LoadClipFromFBX("Assets/characters/swat/Swat@Reloadwalking.fbx");
        AnimationClip reloadRun = LoadClipFromFBX("Assets/characters/swat/Swat@Reloadrunning.fbx");

        // Kick animation
        AnimationClip kick = LoadClipFromFBX("Assets/characters/swat/Swat@Drop Kick.fbx");

        // Create 2D Blend Tree
        BlendTree blendTree;
        AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out blendTree);

        blendTree.blendType = BlendTreeType.FreeformCartesian2D;
        blendTree.blendParameter = "MoveX";   // X axis: -1 left, 1 right
        blendTree.blendParameterY = "MoveY";  // Y axis: -1 back, 1 forward

        // Add motions at positions: (MoveX, MoveY)
        // Center = Idle (main idle only in blend tree)
        if (idle != null)
            blendTree.AddChild(idle, new Vector2(0f, 0f));

        // Forward (walk at 0.5, run at 1.0)
        if (walkForward != null)
            blendTree.AddChild(walkForward, new Vector2(0f, 0.5f));
        if (runForward != null)
            blendTree.AddChild(runForward, new Vector2(0f, 1f));

        // Backward (walk at -0.5, run at -1.0)
        if (walkBack != null)
            blendTree.AddChild(walkBack, new Vector2(0f, -0.5f));
        if (runBack != null)
            blendTree.AddChild(runBack, new Vector2(0f, -1f));

        // Left strafe (step at -0.5, sprint at -1.0) - sped up to match movement
        if (stepLeft != null)
        {
            int idx = blendTree.children.Length;
            blendTree.AddChild(stepLeft, new Vector2(-0.5f, 0f));
            var children = blendTree.children;
            children[idx].timeScale = 1.8f;
            blendTree.children = children;
        }
        if (sprintLeft != null)
        {
            int idx = blendTree.children.Length;
            blendTree.AddChild(sprintLeft, new Vector2(-1f, 0f));
            var children = blendTree.children;
            children[idx].timeScale = 1.5f;
            blendTree.children = children;
        }

        // Right strafe (step at 0.5, sprint at 1.0) - sped up to match movement
        if (stepRight != null)
        {
            int idx = blendTree.children.Length;
            blendTree.AddChild(stepRight, new Vector2(0.5f, 0f));
            var children = blendTree.children;
            children[idx].timeScale = 1.8f;
            blendTree.children = children;
        }
        if (sprintRight != null)
        {
            int idx = blendTree.children.Length;
            blendTree.AddChild(sprintRight, new Vector2(1f, 0f));
            var children = blendTree.children;
            children[idx].timeScale = 1.5f;
            blendTree.children = children;
        }

        // Forward-left diagonal (walk at -0.5,0.5, run at -1,1)
        if (walkForwardLeft != null)
            blendTree.AddChild(walkForwardLeft, new Vector2(-0.5f, 0.5f));
        if (sprintForwardLeft != null)
            blendTree.AddChild(sprintForwardLeft, new Vector2(-1f, 1f));

        // Forward-right diagonal (walk at 0.5,0.5, run at 1,1)
        if (walkForwardRight != null)
            blendTree.AddChild(walkForwardRight, new Vector2(0.5f, 0.5f));
        if (sprintForwardRight != null)
            blendTree.AddChild(sprintForwardRight, new Vector2(1f, 1f));

        // Back-left diagonal (walk at -0.5,-0.5, run at -1,-1)
        if (walkBackLeft != null)
            blendTree.AddChild(walkBackLeft, new Vector2(-0.5f, -0.5f));
        if (runBackLeft != null)
            blendTree.AddChild(runBackLeft, new Vector2(-1f, -1f));

        // Back-right diagonal (walk at 0.5,-0.5, run at 1,-1)
        if (walkBackRight != null)
            blendTree.AddChild(walkBackRight, new Vector2(0.5f, -0.5f));
        if (sprintBackRight != null)
            blendTree.AddChild(sprintBackRight, new Vector2(1f, -1f));

        rootStateMachine.defaultState = locomotionState;

        // Create idle variation states
        AnimatorState idle2State = null;
        AnimatorState idle3State = null;

        if (idle2 != null)
        {
            idle2State = rootStateMachine.AddState("Idle2");
            idle2State.motion = idle2;
        }

        if (idle3 != null)
        {
            idle3State = rootStateMachine.AddState("Idle3");
            idle3State.motion = idle3;
        }

        // Transitions from Locomotion to idle variants (when IdleVariant changes and not moving)
        if (idle2State != null)
        {
            AnimatorStateTransition toIdle2 = locomotionState.AddTransition(idle2State);
            toIdle2.AddCondition(AnimatorConditionMode.Equals, 1, "IdleVariant");
            toIdle2.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveX");
            toIdle2.AddCondition(AnimatorConditionMode.Greater, -0.1f, "MoveX");
            toIdle2.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveY");
            toIdle2.AddCondition(AnimatorConditionMode.Greater, -0.1f, "MoveY");
            toIdle2.duration = 0.25f;
            toIdle2.hasExitTime = false;

            // Back to locomotion when moving or IdleVariant changes
            AnimatorStateTransition fromIdle2 = idle2State.AddTransition(locomotionState);
            fromIdle2.AddCondition(AnimatorConditionMode.NotEqual, 1, "IdleVariant");
            fromIdle2.duration = 0.25f;
            fromIdle2.hasExitTime = false;

            AnimatorStateTransition fromIdle2Move = idle2State.AddTransition(locomotionState);
            fromIdle2Move.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveY");
            fromIdle2Move.duration = 0.1f;
            fromIdle2Move.hasExitTime = false;
        }

        if (idle3State != null)
        {
            AnimatorStateTransition toIdle3 = locomotionState.AddTransition(idle3State);
            toIdle3.AddCondition(AnimatorConditionMode.Equals, 2, "IdleVariant");
            toIdle3.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveX");
            toIdle3.AddCondition(AnimatorConditionMode.Greater, -0.1f, "MoveX");
            toIdle3.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveY");
            toIdle3.AddCondition(AnimatorConditionMode.Greater, -0.1f, "MoveY");
            toIdle3.duration = 0.25f;
            toIdle3.hasExitTime = false;

            AnimatorStateTransition fromIdle3 = idle3State.AddTransition(locomotionState);
            fromIdle3.AddCondition(AnimatorConditionMode.NotEqual, 2, "IdleVariant");
            fromIdle3.duration = 0.25f;
            fromIdle3.hasExitTime = false;

            AnimatorStateTransition fromIdle3Move = idle3State.AddTransition(locomotionState);
            fromIdle3Move.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveY");
            fromIdle3Move.duration = 0.1f;
            fromIdle3Move.hasExitTime = false;
        }

        // Create Kick state (full body animation in base layer)
        if (kick != null)
        {
            AnimatorState kickState = rootStateMachine.AddState("Kick");
            kickState.motion = kick;

            // Any state -> Kick when Kick trigger
            AnimatorStateTransition toKick = rootStateMachine.AddAnyStateTransition(kickState);
            toKick.AddCondition(AnimatorConditionMode.If, 0, "Kick");
            toKick.duration = 0.1f;
            toKick.hasExitTime = false;

            // Kick -> Locomotion after animation finishes
            AnimatorStateTransition fromKick = kickState.AddTransition(locomotionState);
            fromKick.hasExitTime = true;
            fromKick.exitTime = 0.85f;
            fromKick.duration = 0.15f;
        }

        // Create upper body avatar mask for aiming layer
        string maskPath = "Assets/characters/swat/UpperBodyMask.mask";
        AvatarMask upperBodyMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (upperBodyMask == null)
        {
            upperBodyMask = new AvatarMask();

            // Enable upper body parts (humanoid)
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);  // Spine
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, false);
            upperBodyMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);

            AssetDatabase.CreateAsset(upperBodyMask, maskPath);
            Debug.Log("Created UpperBodyMask.mask");
        }

        // Add aiming layer (upper body only)
        AnimatorControllerLayer aimLayer = new AnimatorControllerLayer();
        aimLayer.name = "Aiming";
        aimLayer.defaultWeight = 1f;
        aimLayer.avatarMask = upperBodyMask;
        aimLayer.blendingMode = AnimatorLayerBlendingMode.Override;
        aimLayer.stateMachine = new AnimatorStateMachine();
        aimLayer.stateMachine.name = "Aiming";
        aimLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;

        // Need to save the state machine as sub-asset
        AssetDatabase.AddObjectToAsset(aimLayer.stateMachine, controller);

        controller.AddLayer(aimLayer);

        AnimatorStateMachine aimStateMachine = aimLayer.stateMachine;

        // Create empty state for when not aiming (lets base layer show through)
        AnimatorState notAimingState = aimStateMachine.AddState("NotAiming");
        notAimingState.motion = null;  // Empty - base layer plays
        aimStateMachine.defaultState = notAimingState;

        // Create aiming states
        AnimatorState downToAimState = null;
        AnimatorState aimingIdleState = null;
        AnimatorState aimToDownState = null;
        AnimatorState firingState = null;

        if (downToAim != null)
        {
            downToAimState = aimStateMachine.AddState("DownToAim");
            downToAimState.motion = downToAim;
        }

        if (aimingIdle != null)
        {
            aimingIdleState = aimStateMachine.AddState("AimingIdle");
            aimingIdleState.motion = aimingIdle;
        }

        if (aimToDown != null)
        {
            aimToDownState = aimStateMachine.AddState("AimToDown");
            aimToDownState.motion = aimToDown;
        }

        if (firing != null)
        {
            firingState = aimStateMachine.AddState("Firing");
            firingState.motion = firing;
        }

        // Create reload state (uses idle reload animation for upper body)
        AnimatorState reloadState = null;
        if (reloadIdle != null)
        {
            reloadState = aimStateMachine.AddState("Reloading");
            reloadState.motion = reloadIdle;
        }

        // Create transitions
        // NotAiming -> DownToAim when IsAiming = true
        if (downToAimState != null)
        {
            AnimatorStateTransition toAim = notAimingState.AddTransition(downToAimState);
            toAim.AddCondition(AnimatorConditionMode.If, 0, "IsAiming");
            toAim.duration = 0.1f;
            toAim.hasExitTime = false;
        }

        // DownToAim -> AimingIdle (after animation finishes)
        if (downToAimState != null && aimingIdleState != null)
        {
            AnimatorStateTransition toAimIdle = downToAimState.AddTransition(aimingIdleState);
            toAimIdle.hasExitTime = true;
            toAimIdle.exitTime = 0.9f;
            toAimIdle.duration = 0.1f;
        }

        // AimingIdle -> AimToDown when IsAiming = false
        if (aimingIdleState != null && aimToDownState != null)
        {
            AnimatorStateTransition fromAim = aimingIdleState.AddTransition(aimToDownState);
            fromAim.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAiming");
            fromAim.duration = 0.1f;
            fromAim.hasExitTime = false;
        }

        // AimToDown -> NotAiming (after animation finishes)
        if (aimToDownState != null)
        {
            AnimatorStateTransition toNotAiming = aimToDownState.AddTransition(notAimingState);
            toNotAiming.hasExitTime = true;
            toNotAiming.exitTime = 0.9f;
            toNotAiming.duration = 0.1f;
        }

        // AimingIdle -> Firing when Fire trigger
        if (aimingIdleState != null && firingState != null)
        {
            AnimatorStateTransition toFiring = aimingIdleState.AddTransition(firingState);
            toFiring.AddCondition(AnimatorConditionMode.If, 0, "Fire");
            toFiring.duration = 0.05f;
            toFiring.hasExitTime = false;
        }

        // Firing -> AimingIdle (after animation finishes)
        if (firingState != null && aimingIdleState != null)
        {
            AnimatorStateTransition fromFiring = firingState.AddTransition(aimingIdleState);
            fromFiring.hasExitTime = true;
            fromFiring.exitTime = 0.8f;
            fromFiring.duration = 0.1f;
        }

        // Reload transitions - Any state -> Reloading when Reload trigger
        if (reloadState != null)
        {
            AnimatorStateTransition toReload = aimStateMachine.AddAnyStateTransition(reloadState);
            toReload.AddCondition(AnimatorConditionMode.If, 0, "Reload");
            toReload.duration = 0.1f;
            toReload.hasExitTime = false;

            // Reloading -> NotAiming (after animation finishes, if not aiming)
            AnimatorStateTransition fromReloadToNot = reloadState.AddTransition(notAimingState);
            fromReloadToNot.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAiming");
            fromReloadToNot.hasExitTime = true;
            fromReloadToNot.exitTime = 0.95f;
            fromReloadToNot.duration = 0.1f;

            // Reloading -> AimingIdle (after animation finishes, if aiming)
            if (aimingIdleState != null)
            {
                AnimatorStateTransition fromReloadToAim = reloadState.AddTransition(aimingIdleState);
                fromReloadToAim.AddCondition(AnimatorConditionMode.If, 0, "IsAiming");
                fromReloadToAim.hasExitTime = true;
                fromReloadToAim.exitTime = 0.95f;
                fromReloadToAim.duration = 0.1f;
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("Created SwatAnimator.controller with locomotion blend tree and upper body aiming layer");
    }

    static AnimationClip LoadClipFromFBX(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        AnimationClip bestClip = null;
        float longestLength = 0f;

        // Simply pick the longest non-preview clip
        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
            {
                Debug.Log($"  Found: '{clip.name}' ({clip.length:F2}s) in {System.IO.Path.GetFileName(path)}");
                if (clip.length > longestLength)
                {
                    bestClip = clip;
                    longestLength = clip.length;
                }
            }
        }

        if (bestClip != null)
            Debug.Log($">>> Selected: '{bestClip.name}' ({bestClip.length:F2}s)");
        else
            Debug.LogWarning($"Could not find animation clip in: {path}");

        return bestClip;
    }
}
#endif
