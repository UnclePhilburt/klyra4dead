using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

public class SetupZombieAnimations : EditorWindow
{
    [MenuItem("Tools/Setup Zombie Animations & Sounds")]
    public static void Setup()
    {
        string zombieFolder = "Assets/characters/zombie";
        string prefabPath = "Assets/Resources/Zombie.prefab";
        
        // Load the zombie prefab
        GameObject zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (zombiePrefab == null)
        {
            Debug.LogError("[Setup] Zombie prefab not found at: " + prefabPath);
            return;
        }

        // Get or create animator controller
        string controllerPath = zombieFolder + "/ZombieAnimator.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            Debug.Log("[Setup] Created new animator controller");
        }

        // Setup parameters
        AddParameterIfMissing(controller, "Speed", AnimatorControllerParameterType.Float);
        AddParameterIfMissing(controller, "IsChasing", AnimatorControllerParameterType.Bool);
        AddParameterIfMissing(controller, "IsSprinter", AnimatorControllerParameterType.Bool);
        AddParameterIfMissing(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameterIfMissing(controller, "Die", AnimatorControllerParameterType.Trigger);

        // Find animations
        AnimationClip idleClip = FindAnimation(zombieFolder, "Idle");
        AnimationClip walkClip = FindAnimation(zombieFolder, "walk");
        AnimationClip runClip = FindAnimation(zombieFolder, "Running");
        AnimationClip sprintClip = FindAnimation(zombieFolder, "Fast Run");
        
        if (idleClip == null) Debug.LogWarning("[Setup] Idle animation not found");
        if (walkClip == null) Debug.LogWarning("[Setup] Walk animation not found");
        if (runClip == null) Debug.LogWarning("[Setup] Run animation not found");
        if (sprintClip == null) Debug.LogWarning("[Setup] Sprint (Fast Run) animation not found");

        // Get the base layer
        var rootStateMachine = controller.layers[0].stateMachine;

        // Create states if they don't exist
        AnimatorState idleState = FindOrCreateState(rootStateMachine, "Idle", idleClip);
        AnimatorState walkState = FindOrCreateState(rootStateMachine, "Walk", walkClip);
        AnimatorState runState = FindOrCreateState(rootStateMachine, "Run", runClip);
        AnimatorState sprintState = FindOrCreateState(rootStateMachine, "Sprint", sprintClip);

        // Set default state
        rootStateMachine.defaultState = idleState;

        // Idle -> Walk (Speed > 0.1)
        AddTransitionIfMissing(idleState, walkState, "Speed", AnimatorConditionMode.Greater, 0.1f);
        
        // Walk -> Idle (Speed < 0.1)
        AddTransitionIfMissing(walkState, idleState, "Speed", AnimatorConditionMode.Less, 0.1f);
        
        // Walk -> Run (Speed > 2 && !IsSprinter)
        var walkToRun = FindOrAddTransition(walkState, runState);
        if (walkToRun != null && walkToRun.conditions.Length < 2)
        {
            ClearConditions(walkToRun);
            walkToRun.AddCondition(AnimatorConditionMode.Greater, 2f, "Speed");
            walkToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinter");
        }
        
        // Walk -> Sprint (Speed > 2 && IsSprinter)
        var walkToSprint = FindOrAddTransition(walkState, sprintState);
        if (walkToSprint != null && walkToSprint.conditions.Length < 2)
        {
            ClearConditions(walkToSprint);
            walkToSprint.AddCondition(AnimatorConditionMode.Greater, 2f, "Speed");
            walkToSprint.AddCondition(AnimatorConditionMode.If, 0, "IsSprinter");
        }
        
        // Run -> Walk (Speed < 2)
        AddTransitionIfMissing(runState, walkState, "Speed", AnimatorConditionMode.Less, 2f);
        
        // Sprint -> Walk (Speed < 2)
        AddTransitionIfMissing(sprintState, walkState, "Speed", AnimatorConditionMode.Less, 2f);
        
        // Idle -> Run (IsChasing && !IsSprinter)
        var idleToRun = FindOrAddTransition(idleState, runState);
        if (idleToRun != null && idleToRun.conditions.Length < 2)
        {
            ClearConditions(idleToRun);
            idleToRun.AddCondition(AnimatorConditionMode.If, 0, "IsChasing");
            idleToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinter");
        }
        
        // Idle -> Sprint (IsChasing && IsSprinter)
        var idleToSprint = FindOrAddTransition(idleState, sprintState);
        if (idleToSprint != null && idleToSprint.conditions.Length < 2)
        {
            ClearConditions(idleToSprint);
            idleToSprint.AddCondition(AnimatorConditionMode.If, 0, "IsChasing");
            idleToSprint.AddCondition(AnimatorConditionMode.If, 0, "IsSprinter");
        }
        
        // Run -> Idle (!IsChasing && Speed < 0.1)
        var runToIdle = FindOrAddTransition(runState, idleState);
        if (runToIdle != null && runToIdle.conditions.Length < 2)
        {
            ClearConditions(runToIdle);
            runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsChasing");
            runToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }
        
        // Sprint -> Idle (!IsChasing && Speed < 0.1)
        var sprintToIdle = FindOrAddTransition(sprintState, idleState);
        if (sprintToIdle != null && sprintToIdle.conditions.Length < 2)
        {
            ClearConditions(sprintToIdle);
            sprintToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsChasing");
            sprintToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        // Save the controller
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("[Setup] Animator controller updated with Sprint state!");

        // Now setup audio on the prefab
        SetupZombieAudio(zombiePrefab, zombieFolder);

        Debug.Log("[Setup] ========================================");
        Debug.Log("[Setup] ZOMBIE SETUP COMPLETE!");
        Debug.Log("[Setup] - Animator: Idle, Walk, Run, Sprint states");
        Debug.Log("[Setup] - Audio: Idle, Alert, Chase, Death sounds");
        Debug.Log("[Setup] ========================================");
    }

    static void ClearConditions(AnimatorStateTransition transition)
    {
        while (transition.conditions.Length > 0)
        {
            transition.RemoveCondition(transition.conditions[0]);
        }
    }

    static void SetupZombieAudio(GameObject prefab, string folder)
    {
        // Instantiate prefab to modify
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        ZombieAI ai = instance.GetComponent<ZombieAI>();

        if (ai == null)
        {
            Debug.LogWarning("[Setup] No ZombieAI component found on prefab");
            Object.DestroyImmediate(instance);
            return;
        }

        // Find audio clips
        var idleSounds = new System.Collections.Generic.List<AudioClip>();
        var alertSounds = new System.Collections.Generic.List<AudioClip>();
        var screamSounds = new System.Collections.Generic.List<AudioClip>();
        var chaseSounds = new System.Collections.Generic.List<AudioClip>();
        var deathSounds = new System.Collections.Generic.List<AudioClip>();
        var hurtSounds = new System.Collections.Generic.List<AudioClip>();

        string[] audioFiles = Directory.GetFiles(folder, "*.mp3");
        foreach (string file in audioFiles)
        {
            string assetPath = file.Replace("\\", "/");
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null) continue;

            string lower = clip.name.ToLower();

            if (lower.Contains("moan") || lower.Contains("choking") || lower.Contains("growl"))
            {
                idleSounds.Add(clip);
            }
            else if (lower.Contains("scream"))
            {
                screamSounds.Add(clip);
                alertSounds.Add(clip);
            }
            else if (lower.Contains("call") || lower.Contains("sound"))
            {
                chaseSounds.Add(clip);
            }
            else if (lower.Contains("dying") || lower.Contains("death"))
            {
                deathSounds.Add(clip);
            }
        }

        // Assign to AI
        if (idleSounds.Count > 0) ai.idleSounds = idleSounds.ToArray();
        if (alertSounds.Count > 0) ai.alertSounds = alertSounds.ToArray();
        if (chaseSounds.Count > 0) ai.chaseSounds = chaseSounds.ToArray();
        if (deathSounds.Count > 0) ai.deathSounds = deathSounds.ToArray();
        if (hurtSounds.Count > 0) ai.hurtSounds = hurtSounds.ToArray();
        
        // Use some sounds for hurt if we don't have specific ones
        if (ai.hurtSounds == null || ai.hurtSounds.Length == 0)
        {
            ai.hurtSounds = chaseSounds.ToArray();
        }

        Debug.Log($"[Setup] Audio assigned - Idle:{idleSounds.Count}, Alert:{alertSounds.Count}, Chase:{chaseSounds.Count}, Death:{deathSounds.Count}");

        // Save prefab
        PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Resources/Zombie.prefab");
        Object.DestroyImmediate(instance);

        Debug.Log("[Setup] Zombie prefab updated with audio!");
    }

    static AnimationClip FindAnimation(string folder, string nameContains)
    {
        string[] files = Directory.GetFiles(folder, "*.fbx");
        foreach (string file in files)
        {
            if (file.ToLower().Contains(nameContains.ToLower()))
            {
                string assetPath = file.Replace("\\", "/");
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (Object asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                    {
                        Debug.Log($"[Setup] Found animation: {clip.name} in {Path.GetFileName(file)}");
                        return clip;
                    }
                }
            }
        }
        return null;
    }

    static void AddParameterIfMissing(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var param in controller.parameters)
        {
            if (param.name == name) return;
        }
        controller.AddParameter(name, type);
        Debug.Log($"[Setup] Added parameter: {name}");
    }

    static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string name, AnimationClip clip)
    {
        foreach (var state in sm.states)
        {
            if (state.state.name == name)
            {
                if (clip != null) state.state.motion = clip;
                return state.state;
            }
        }
        
        AnimatorState newState = sm.AddState(name);
        if (clip != null) newState.motion = clip;
        Debug.Log($"[Setup] Created state: {name}");
        return newState;
    }

    static void AddTransitionIfMissing(AnimatorState from, AnimatorState to, string param, AnimatorConditionMode mode, float threshold)
    {
        foreach (var trans in from.transitions)
        {
            if (trans.destinationState == to) return; // Already exists
        }
        
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.AddCondition(mode, threshold, param);
    }

    static AnimatorStateTransition FindOrAddTransition(AnimatorState from, AnimatorState to)
    {
        foreach (var trans in from.transitions)
        {
            if (trans.destinationState == to) return trans;
        }
        
        var transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        return transition;
    }
}
