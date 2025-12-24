using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class FixZombieAnimatorController
{
    [MenuItem("Tools/Fix Zombie Animator")]
    public static void Fix()
    {
        string controllerPath = "Assets/characters/zombie/ZombieAnimator.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        
        if (controller == null)
        {
            Debug.LogError("[Fix] ZombieAnimator.controller not found at: " + controllerPath);
            return;
        }

        var rootStateMachine = controller.layers[0].stateMachine;

        // Make sure parameters exist
        AddParameterIfMissing(controller, "Speed", AnimatorControllerParameterType.Float);
        AddParameterIfMissing(controller, "IsChasing", AnimatorControllerParameterType.Bool);
        AddParameterIfMissing(controller, "IsSprinter", AnimatorControllerParameterType.Bool);

        // Fix all states
        foreach (var childState in rootStateMachine.states)
        {
            AnimatorState state = childState.state;
            
            // Make sure motion exists
            if (state.motion == null)
            {
                Debug.LogWarning($"[Fix] State '{state.name}' has no animation clip!");
                continue;
            }

            // Set speed parameter multiplier so animations respond to Speed
            state.speedParameterActive = false; // Don't tie playback speed to parameter
            
            Debug.Log($"[Fix] Checked state: {state.name}");
        }

        // Clear all transitions and rebuild them properly
        foreach (var childState in rootStateMachine.states)
        {
            AnimatorState state = childState.state;
            // Remove existing transitions
            for (int i = state.transitions.Length - 1; i >= 0; i--)
            {
                state.RemoveTransition(state.transitions[i]);
            }
        }

        // Find states
        AnimatorState idleState = FindState(rootStateMachine, "Idle");
        AnimatorState walkState = FindState(rootStateMachine, "Walk");
        AnimatorState runState = FindState(rootStateMachine, "Run");
        AnimatorState sprintState = FindState(rootStateMachine, "Sprint");

        if (idleState == null || walkState == null)
        {
            Debug.LogError("[Fix] Missing Idle or Walk state!");
            return;
        }

        // Idle -> Walk (Speed > 0.1)
        var t1 = idleState.AddTransition(walkState);
        t1.hasExitTime = false;
        t1.duration = 0.1f;
        t1.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        // Walk -> Idle (Speed < 0.1)
        var t2 = walkState.AddTransition(idleState);
        t2.hasExitTime = false;
        t2.duration = 0.1f;
        t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        if (runState != null)
        {
            // Walk -> Run (Speed > 2 AND IsChasing AND NOT IsSprinter)
            var t3 = walkState.AddTransition(runState);
            t3.hasExitTime = false;
            t3.duration = 0.1f;
            t3.AddCondition(AnimatorConditionMode.Greater, 2f, "Speed");
            t3.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinter");

            // Run -> Walk (Speed < 2)
            var t4 = runState.AddTransition(walkState);
            t4.hasExitTime = false;
            t4.duration = 0.1f;
            t4.AddCondition(AnimatorConditionMode.Less, 2f, "Speed");

            // Run -> Idle (Speed < 0.1)
            var t5 = runState.AddTransition(idleState);
            t5.hasExitTime = false;
            t5.duration = 0.1f;
            t5.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        if (sprintState != null)
        {
            // Walk -> Sprint (Speed > 2 AND IsSprinter)
            var t6 = walkState.AddTransition(sprintState);
            t6.hasExitTime = false;
            t6.duration = 0.1f;
            t6.AddCondition(AnimatorConditionMode.Greater, 2f, "Speed");
            t6.AddCondition(AnimatorConditionMode.If, 0, "IsSprinter");

            // Sprint -> Walk (Speed < 2)
            var t7 = sprintState.AddTransition(walkState);
            t7.hasExitTime = false;
            t7.duration = 0.1f;
            t7.AddCondition(AnimatorConditionMode.Less, 2f, "Speed");

            // Sprint -> Idle (Speed < 0.1)
            var t8 = sprintState.AddTransition(idleState);
            t8.hasExitTime = false;
            t8.duration = 0.1f;
            t8.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("[Fix] Zombie Animator Controller fixed!");
        Debug.Log("[Fix] Transitions rebuilt: Idle <-> Walk <-> Run/Sprint");
    }

    static void AddParameterIfMissing(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var param in controller.parameters)
            if (param.name == name) return;
        controller.AddParameter(name, type);
        Debug.Log($"[Fix] Added parameter: {name}");
    }

    static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (var state in sm.states)
            if (state.state.name == name)
                return state.state;
        return null;
    }
}
