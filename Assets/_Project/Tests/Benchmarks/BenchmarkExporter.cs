using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BenchmarkExporter
{
    public string filePath;
    public string prefabName;
    public int randomSeed;
    public int interruptions;

    private readonly CultureInfo culture =
        CultureInfo.GetCultureInfo("de-DE");

    public void CreatFile()
    {
        string fileName = "Benchmark_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N") +
            ".csv";

        filePath = Path.Combine(
            Application.persistentDataPath,
            fileName
        );

        string header =
            "Timestamp;Objekte;Frames;MessdauerSekunden;" +
            "AverageFPS;P95ms;P99ms;1PercentLowFPS;" +

            "DrawCalls;Triangles;Vertices;SetPassCalls;" +

            "CPUTotalMs;CPUMainThreadMs;" +
            "CPURenderThreadMs;GPUFrameMs;" +

            "RAMAllocatedAverageMiB;RAMAllocatedPeakMiB;" +
            "RAMReservedAverageMiB;RAMReservedPeakMiB;" +
            "ManagedMemoryPeakMiB;" +

            "TextureMemoryAverageMiB;TextureMemoryPeakMiB;" +
            "TextureTargetMemoryMiB;" +
            "GraphicsDriverMemoryPeakMiB;" +

            "GCAllocatedAverageBytesPerFrame;" +
            "GCAllocatedPeakBytesPerFrame;" +
            "GCCollections;GCCollectionsOnSlowFrames;" +

            "DeviceModel;OperatingSystem;Processor;" +
            "GraphicsDevice;DeviceMemoryMB;GraphicsMemoryMB;" +
            "ScreenWidth;ScreenHeight;" +

            "UnityVersion;GraphicsAPI;TargetFPS;Prefab;RandomSeed;QualityLevel;RenderPipeline;RenderScale;DevelopmentBuild;WarmupSeconds;MeasurementSeconds;Interruptions;SlowFrameThresholdMs;AppVersion";

        Directory.CreateDirectory(Application.persistentDataPath);

        File.WriteAllText(
            filePath,
            header + Environment.NewLine,
            new UTF8Encoding(true)
        );

        Debug.Log("CSV erstellt unter: " + filePath);
    }

    // Formatiert Zahlen für die CSV
    private string Number(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "N/A";
        }

        return value.ToString("0.###", culture);
    }

    // Schützt Textfelder vor Trennzeichen und Anführungszeichen
    private string CsvText(string value)
    {
        return "\"" +
            (value ?? "").Replace("\"", "\"\"") +
            "\"";
    }

    public void SafeResults(
        int objectCount,
        BenchmarkMonitor.FrameResults frameData,
        HardwareControler.MeasuredData hardwareData)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new InvalidOperationException("CSV-Datei wurde noch nicht erstellt!");
        }

        // CPU- und GPU-Werte von Nanosekunden in Millisekunden
        double cpuTotalMs = hardwareData.cpuTotalFrame / 1_000_000.0;
        double cpuMainMs = hardwareData.cpuMainThread / 1_000_000.0;
        double cpuRenderMs = hardwareData.cpuRenderThread / 1_000_000.0;
        double gpuMs = hardwareData.gpuFrame / 1_000_000.0;

        // Eine CSV-Zeile mit allen Messergebnissen
        string[] values =
        {
            // Testdaten
            CsvText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
            objectCount.ToString(),
            frameData.measuredFrames.ToString(),
            Number(frameData.measuredSeconds),

            // FPS
            Number(frameData.averageFPS),
            Number(frameData.p95),
            Number(frameData.p99),
            Number(frameData.onePercentLow),

            // Rendering
            Number(hardwareData.drawCalls),
            Number(hardwareData.triangleCount),
            Number(hardwareData.verticesCount),
            Number(hardwareData.setCount),

            // CPU / GPU
            Number(cpuTotalMs),
            Number(cpuMainMs),
            Number(cpuRenderMs),
            Number(gpuMs),

            // RAM
            Number(hardwareData.ramAllocatedAverage),
            Number(hardwareData.ramAllocatedPeak),
            Number(hardwareData.ramReservedAverage),
            Number(hardwareData.ramReservedPeak),
            Number(hardwareData.managedMemoryPeak),

            // Texture Memory
            Number(hardwareData.textureMemoryAverage),
            Number(hardwareData.textureMemoryPeak),
            Number(hardwareData.textureTargetMemory),
            Number(hardwareData.graphicsDriverMemoryPeak),

            // Garbage Collection
            Number(hardwareData.gcAllocatedAverage),
            Number(hardwareData.gcAllocatedPeak),
            hardwareData.gcCollections.ToString(),
            hardwareData.gcCollectionsOnSlowFrames.ToString(),

            // Gerätedaten
            CsvText(hardwareData.deviceModel),
            CsvText(hardwareData.operatingSystem),
            CsvText(hardwareData.processor),
            CsvText(hardwareData.graphicsDevice),

            hardwareData.deviceMemoryMB.ToString(),
            hardwareData.graphicsMemoryMB.ToString(),
            hardwareData.screenWidth.ToString(),
            hardwareData.screenHeight.ToString(),

            // Zusätzliche Informationen
            CsvText(Application.unityVersion),
            CsvText(SystemInfo.graphicsDeviceVersion),
            Application.targetFrameRate.ToString(),
            CsvText(prefabName),
            randomSeed.ToString(),
            CsvText(QualitySettings.names[QualitySettings.GetQualityLevel()]),
            CsvText(GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "Built-in"),
            Number(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp ? urp.renderScale : 1),
            Debug.isDebugBuild.ToString(),
            "60", "120", interruptions.ToString(), Number(1000.0 / 30), CsvText(Application.version)
        };

        string csvLine = string.Join(";", values);

        try
        {
            File.AppendAllText(
                filePath,
                csvLine + Environment.NewLine,
                Encoding.UTF8
            );

            Debug.Log(
                "CSV-Ergebnisse gespeichert für " +
                objectCount + " Objekte."
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "Fehler beim CSV-Export: " + e.Message
            );
            throw;
        }
    }
}
