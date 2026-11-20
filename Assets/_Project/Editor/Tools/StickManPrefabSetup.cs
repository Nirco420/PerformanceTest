using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Creates the requested asset once after import. Existing prefabs are never overwritten.
[InitializeOnLoad]
public static class StickManPrefabSetup
{
    const string ModelPath = "Assets/_Project/Art/Models/Characters/StickManForMixamo.fbx";
    const string PrefabPath = "Assets/_Project/Prefabs/Characters/StickMan_DanceLoop.prefab";
    const string ControllerPath = "Assets/_Project/Animations/Characters/StickMan_DanceLoop.controller";

    static StickManPrefabSetup() { EditorApplication.delayCall += CreateIfMissing; }

    [MenuItem("Benchmark/Create StickMan Dance Loop Prefab")]
    public static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(PrefabPath)) return;
        GameObject root = null;
        try
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) throw new InvalidOperationException("StickMan model not imported yet.");
            var clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Single(x => x.name == "Armature|Dance");
            if (!clip.isLooping) throw new InvalidOperationException("Dance clip must have Loop Time enabled.");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!controller)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                var state = controller.layers[0].stateMachine.AddState("Dance Loop");
                state.motion = clip;
                state.speed = 1;
                controller.layers[0].stateMachine.defaultState = state;
            }
            // Wrapper preserves the model's imported axis correction when the benchmark
            // instantiates the prefab with Quaternion.identity.
            root = new GameObject("StickMan_DanceLoop");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root.transform, false);
            var animator = instance.GetComponent<Animator>();
            if (!animator) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("No skinned mesh found.");
            foreach (var renderer in renderers) renderer.updateWhenOffscreen = true;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var savedAnimator = saved.GetComponentInChildren<Animator>();
            if (!savedAnimator || savedAnimator.runtimeAnimatorController != controller || savedAnimator.applyRootMotion)
                throw new InvalidOperationException("Prefab validation failed.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/StickManPrefabValidation.txt",
                "PASS: prefab saved; Dance Loop default state; looping clip " + clip.name +
                "; duration=" + clip.length + "; root motion disabled; skinned meshes=" + renderers.Length);
            Debug.Log("Created and validated " + PrefabPath);
        }
        catch (Exception e) { Debug.LogException(e); }
        finally { if (root) UnityEngine.Object.DestroyImmediate(root); }
    }
}
