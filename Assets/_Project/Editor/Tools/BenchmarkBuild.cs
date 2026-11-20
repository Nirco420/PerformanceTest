using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BenchmarkBuild
{
    [MenuItem("Benchmark/Build Android APK")]
    public static void Android()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new InvalidOperationException("Zuerst in Build Profiles auf Android wechseln.");
        Validate();
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "de.weissenhorn.mobilebenchmark");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.enableFrameTimingStats = true;
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.allowDebugging = false;
        Directory.CreateDirectory("Builds/Android");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/_Project/Tests/TestScenes/Benchmark_Mesh.unity" },
            locationPathName = "Builds/Android/WeissenhornBenchmark.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Android-Build fehlgeschlagen: " + report.summary.result);
        Debug.Log("APK erstellt: " + Path.GetFullPath("Builds/Android/WeissenhornBenchmark.apk"));
    }

    [MenuItem("Benchmark/Validate scene and statistics")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Pruefung nur ausserhalb des Play Mode starten.");
        var scene = SceneManager.GetSceneByPath("Assets/_Project/Tests/TestScenes/Benchmark_Mesh.unity");
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/_Project/Tests/TestScenes/Benchmark_Mesh.unity", OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("Benchmark-Szene vor dem Build speichern.");
            var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<BenchmarkController>()).Single();
            if (!controller.testPrefab || !controller.camera || !controller.camera.orthographic ||
                !controller.benchmarkMonitor || !controller.hardwareControler || !controller.shareCSVButton)
                throw new Exception("Fehlende Szenenreferenz.");
            if (!controller.testStages.SequenceEqual(new[] {25,50,100,200,400})) throw new Exception("Falsche Teststufen.");
            var monitor = controller.benchmarkMonitor;
            monitor.StartMeasruement();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var frames = (System.Collections.Generic.List<float>)typeof(BenchmarkMonitor).GetField("frameTimes", flags).GetValue(monitor);
            for (int i = 0; i < 100; i++) frames.Add(i == 99 ? 100f : 10f);
            typeof(BenchmarkMonitor).GetField("measuredFrames", flags).SetValue(monitor, 100);
            typeof(BenchmarkMonitor).GetField("measuredSeconds", flags).SetValue(monitor, 1.09f);
            monitor.StopMeasrument();
            var result = monitor.LastResult;
            if (Mathf.Abs(result.averageFPS - 100f / 1.09f) > 0.001f || result.p95 != 10 || result.p99 != 10 || result.onePercentLow != 10)
                throw new Exception("Statistiktest fehlgeschlagen.");
            monitor.StartMeasruement();
            monitor.StopMeasrument();
            if (!float.IsNaN(monitor.LastResult.averageFPS)) throw new Exception("Leere Messung muss N/A liefern.");
            Debug.Log("BENCHMARK VALIDATION PASSED: scene references, stages, percentiles, average FPS, empty samples.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
}
