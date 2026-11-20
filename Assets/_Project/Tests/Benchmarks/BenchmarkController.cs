using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[DefaultExecutionOrder(100)]
public class BenchmarkController : MonoBehaviour
{
    public GameObject testPrefab;
    public int[] testStages = { 25, 50, 100, 200, 400 };
    public Camera camera;
    public int currentStageIndex;
    public float timer;
    public BenchmarkMonitor benchmarkMonitor;
    public HardwareControler hardwareControler;
    public GameObject shareCSVButton;
    public int randomSeed = 12345;
    public enum BenchmarkPhase { Spawning, Warmup, Measuring, Finished, Failed }
    private BenchmarkPhase currentPhase;
    private readonly List<GameObject> spawnedObjects = new List<GameObject>(400);
    private BenchmarkExporter csvExporter;
    private System.Random random;
    private bool initialized, paused, unfocused, skipFrame;
    private int previousSleepTimeout, interruptions, initialWidth, initialHeight;
    private TextMeshProUGUI statusLabel;
    private string status
    {
        set { if (statusLabel) statusLabel.text = value; }
    }

    void Start()
    {
        previousSleepTimeout = Screen.sleepTimeout;
        try
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas)
            {
                var label = new GameObject("Benchmark Status", typeof(RectTransform));
                label.transform.SetParent(canvas.transform, false);
                statusLabel = label.AddComponent<TextMeshProUGUI>();
                statusLabel.fontSize = 22;
                statusLabel.alignment = TextAlignmentOptions.Center;
                statusLabel.raycastTarget = false;
                var rect = statusLabel.rectTransform;
                rect.anchorMin = new Vector2(0.05f, 0.8f);
                rect.anchorMax = new Vector2(0.95f, 0.95f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                status = "Benchmark startet";
            }
            if (!testPrefab || !camera || !camera.orthographic || !benchmarkMonitor || !hardwareControler || !shareCSVButton)
                throw new InvalidOperationException("Prefab, orthografische Kamera, Monitore und Teilen-Button zuweisen.");
            if (testStages == null || testStages.Length == 0) throw new InvalidOperationException("Keine Teststufen.");
            for (int i = 0; i < testStages.Length; i++)
                if (testStages[i] <= 0 || (i > 0 && testStages[i] <= testStages[i - 1]))
                    throw new InvalidOperationException("Teststufen muessen positiv und aufsteigend sein.");
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            currentStageIndex = 0;
            timer = 0;
            random = new System.Random(randomSeed);
            initialWidth = Screen.width;
            initialHeight = Screen.height;
            csvExporter = new BenchmarkExporter { prefabName = testPrefab.name, randomSeed = randomSeed };
            csvExporter.CreatFile();
            shareCSVButton.SetActive(false);
            initialized = true;
        }
        catch (Exception e) { Fail(e); }
    }
    void Update()
    {
        if (!initialized || paused || unfocused || currentPhase == BenchmarkPhase.Failed) return;
        if (skipFrame) { skipFrame = false; return; }
        if (currentPhase != BenchmarkPhase.Finished && (Screen.width != initialWidth || Screen.height != initialHeight))
        {
            Fail(new InvalidOperationException("Aufloesung geaendert. App fuer neuen Lauf neu starten."));
            return;
        }
        try { HandleBenchmarkPhase(); }
        catch (Exception e) { Fail(e); }
    }
    public void CreateTestObjekt()
    {
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        var obj = Instantiate(testPrefab, Vector3.zero, camera.transform.rotation);
        float margin = 0.5f;
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            margin = Mathf.Max(margin, renderer.bounds.extents.magnitude + renderer.bounds.center.magnitude);
        if (margin >= Mathf.Min(halfHeight, halfWidth))
        {
            Destroy(obj);
            throw new InvalidOperationException("Prefab ist zu gross fuer den Kamerabereich.");
        }
        float x = ((float)random.NextDouble() * 2 - 1) * (halfWidth - margin);
        float y = ((float)random.NextDouble() * 2 - 1) * (halfHeight - margin);
        obj.transform.position = camera.transform.TransformPoint(new Vector3(x, y, 10));
        spawnedObjects.Add(obj);
    }
    public void HandleBenchmarkPhase()
    {
        int target = testStages[currentStageIndex];
        switch (currentPhase)
        {
            case BenchmarkPhase.Spawning:
                if (spawnedObjects.Count < target) CreateTestObjekt();
                else
                {
                    timer = 0;
                    currentPhase = BenchmarkPhase.Warmup;
                    status = $"Stufe {currentStageIndex + 1}/{testStages.Length}: {target} Objekte - Warmup (60 s)";
                }
                break;
            case BenchmarkPhase.Warmup:
                timer += Time.unscaledDeltaTime;
                if (timer >= 60)
                {
                    benchmarkMonitor.StartMeasruement();
                    hardwareControler.StartMeasurment();
                    timer = 0;
                    currentPhase = BenchmarkPhase.Measuring;
                    status = $"Stufe {currentStageIndex + 1}/{testStages.Length}: {target} Objekte - Messung (120 s)";
                }
                break;
            case BenchmarkPhase.Measuring:
                timer += Time.unscaledDeltaTime;
                if (timer < 120) break;
                // Stop hardware sampling before percentile sorting allocates memory.
                var hardwareData = hardwareControler.StopMeasurment();
                benchmarkMonitor.StopMeasrument();
                csvExporter.interruptions = interruptions;
                csvExporter.SafeResults(target, benchmarkMonitor.LastResult, hardwareData);
                if (currentStageIndex < testStages.Length - 1)
                {
                    currentStageIndex++;
                    timer = 0;
                    currentPhase = BenchmarkPhase.Spawning;
                }
                else
                {
                    currentPhase = BenchmarkPhase.Finished;
                    status = "Benchmark abgeschlossen - CSV teilen";
                    shareCSVButton.SetActive(true);
                    Screen.sleepTimeout = previousSleepTimeout;
                }
                break;
        }
    }
    private void Interrupt()
    {
        if (!initialized || currentPhase == BenchmarkPhase.Finished || currentPhase == BenchmarkPhase.Failed) return;
        interruptions++;
        benchmarkMonitor.CancelMeasurement();
        hardwareControler.CancelMeasurement();
        if (currentPhase != BenchmarkPhase.Spawning) currentPhase = BenchmarkPhase.Warmup;
        timer = 0;
        skipFrame = true;
        status = "Unterbrochen - aktuelle Stufe beginnt erneut mit Warmup";
    }
    void OnApplicationPause(bool value) { if (value && !paused && !unfocused) Interrupt(); paused = value; skipFrame = true; }
    void OnApplicationFocus(bool value) { if (!value && !paused && !unfocused) Interrupt(); unfocused = !value; skipFrame = true; }
    void OnDestroy() { if (initialized) Screen.sleepTimeout = previousSleepTimeout; }
    private void Fail(Exception error)
    {
        currentPhase = BenchmarkPhase.Failed;
        if (benchmarkMonitor) benchmarkMonitor.CancelMeasurement();
        if (hardwareControler) hardwareControler.CancelMeasurement();
        Screen.sleepTimeout = previousSleepTimeout;
        status = "Fehler: " + error.Message;
        Debug.LogException(error);
    }
    public void ShareCSV()
    {
        if (csvExporter == null || !System.IO.File.Exists(csvExporter.filePath)) return;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        new NativeShare().AddFile(csvExporter.filePath, "text/csv").SetSubject("Benchmark Ergebnisse").Share();
#else
        Debug.Log("CSV: " + csvExporter.filePath);
#endif
    }
}
