using UnityEngine;
using UnityEditor;

public class FixAnimationLooping : MonoBehaviour
{
    [MenuItem("Tools/Fix SWAT Animation Looping")]
    static void FixLooping()
    {
        string[] fbxPaths = new string[]
        {
            "Assets/characters/swat/SwatRifle Idle.fbx",
            "Assets/characters/swat/Swat@Rifle Walk.fbx",
            "Assets/characters/swat/Swat@Rifle Run.fbx",
            "Assets/characters/swat/Swat@Backwards Rifle Walk.fbx",
            "Assets/characters/swat/Swat@Backwards Rifle Run.fbx"
        };

        foreach (string path in fbxPaths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                if (clips.Length == 0)
                    clips = importer.clipAnimations;

                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].loopTime = true;
                    clips[i].loopPose = true;
                    clips[i].wrapMode = WrapMode.Loop;
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                Debug.Log($"Fixed looping for: {path} ({clips.Length} clips)");
            }
            else
            {
                Debug.LogWarning($"Could not find: {path}");
            }
        }

        Debug.Log("Done! All SWAT animations now loop.");
    }
}
