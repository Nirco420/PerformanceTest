using System;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Profiling;

public class HardwareControler : MonoBehaviour
{
    // Rendering
    private ProfilerRecorder drawCallsRecorder;
    private ProfilerRecorder trianglesRecorder;
    private ProfilerRecorder verticesRecorder;
    private ProfilerRecorder setPassRecorder;

    // CPU / GPU
    private ProfilerRecorder cpuFrameRecorder;
    private ProfilerRecorder cpuMainThreadRecorder;
    private ProfilerRecorder cpuRenderThreadRecorder;
    private ProfilerRecorder gpuFrameRecorder;

    // Garbage Collection
    private ProfilerRecorder gcAllocRecorder;

    public void CancelMeasurement() { isMeasuring = false; }
    private bool isMeasuring;
    private float nextMemorySampleTime;
    private int gcCollectionsAtStart;
    private int previousGcCollections;
    private int gcCollectionsOnSlowFrames;

    private const double BytesPerMiB = 1024.0 * 1024.0;

    // Speichert Summe, Anzahl und Maximum eines Messwerts
    private struct Samples
    {
        private double total;
        private double maximum;
        private int count;

        public void Add(double value)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value < 0)
            {
                return;
            }

            total += value;

            if (count == 0 || value > maximum)
            {
                maximum = value;
            }

            count++;
        }

        public double Average =>
            count > 0 ? total / count : double.NaN;

        public double Maximum =>
            count > 0 ? maximum : double.NaN;
    }

    // Gesammelte Messwerte
    private Samples drawCalls;
    private Samples triangles;
    private Samples vertices;
    private Samples setPass;

    private Samples cpuTotal;
    private Samples cpuMain;
    private Samples cpuRender;
    private Samples gpuFrame;

    private Samples ramAllocated;
    private Samples ramReserved;
    private Samples managedMemory;

    private Samples textureMemory;
    private Samples textureTargetMemory;
    private Samples graphicsDriverMemory;

    private Samples gcAllocated;

    private void OnEnable()
    {
        // Rendering
        drawCallsRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Render, "Draw Calls Count");

        trianglesRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Render, "Triangles Count");

        verticesRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Render, "Vertices Count");

        setPassRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Render, "SetPass Calls Count");

        // CPU / GPU
        cpuFrameRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal, "CPU Total Frame Time");

        cpuMainThreadRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal, "CPU Main Thread Frame Time");

        cpuRenderThreadRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal, "CPU Render Thread Frame Time");

        gpuFrameRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal, "GPU Frame Time");

        // Garbage Collection
        gcAllocRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Memory, "GC Allocated In Frame");
    }

    private void Update()
    {
        if (!isMeasuring)
        {
            return;
        }

        // Rendering
        ReadCounter(drawCallsRecorder, ref drawCalls);
        ReadCounter(trianglesRecorder, ref triangles);
        ReadCounter(verticesRecorder, ref vertices);
        ReadCounter(setPassRecorder, ref setPass);

        // CPU / GPU
        // Die Werte bleiben vorerst in Nanosekunden.
        ReadCounter(cpuFrameRecorder, ref cpuTotal, true);
        ReadCounter(cpuMainThreadRecorder, ref cpuMain, true);
        ReadCounter(cpuRenderThreadRecorder, ref cpuRender, true);
        ReadCounter(gpuFrameRecorder, ref gpuFrame, true);

        // Garbage Allocations
        ReadCounter(gcAllocRecorder, ref gcAllocated);

        // GC Collections
        int currentCollections = GC.CollectionCount(0);

        if (currentCollections > previousGcCollections &&
            Time.unscaledDeltaTime > 1f / 30f)
        {
            gcCollectionsOnSlowFrames += currentCollections - previousGcCollections;
        }

        previousGcCollections = currentCollections;

        // RAM und Texturen alle 0,5 Sekunden messen
        if (Time.unscaledTime >= nextMemorySampleTime)
        {
            ReadMemory();

            nextMemorySampleTime = Time.unscaledTime + 0.5f;
        }
    }

    private static void ReadCounter(
        ProfilerRecorder recorder,
        ref Samples samples,
        bool requirePositive = false)
    {
        if (!recorder.Valid || recorder.Count == 0)
        {
            return;
        }

        long value = recorder.LastValue;

        // Nicht verfügbare Zeitmessungen können 0 liefern.
        if (requirePositive && value <= 0)
        {
            return;
        }

        samples.Add(value);
    }

    private void ReadMemory()
    {
        // Unity RAM
        ramAllocated.Add(
            Profiler.GetTotalAllocatedMemoryLong());

        ramReserved.Add(
            Profiler.GetTotalReservedMemoryLong());

        // Managed Memory
        managedMemory.Add(
            Profiler.GetMonoUsedSizeLong());

        // Texture Memory
        textureMemory.Add(
            Texture.currentTextureMemory);

        textureTargetMemory.Add(
            Texture.targetTextureMemory);

        // Nur in Editor und Development Build verfügbar
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        long graphicsMemory =
            Profiler.GetAllocatedMemoryForGraphicsDriver();

        if (graphicsMemory > 0)
        {
            graphicsDriverMemory.Add(graphicsMemory);
        }

#endif
    }

    public void StartMeasurment()
    {
        isMeasuring = false;

        // Rendering zurücksetzen
        drawCalls = default;
        triangles = default;
        vertices = default;
        setPass = default;

        // CPU / GPU zurücksetzen
        cpuTotal = default;
        cpuMain = default;
        cpuRender = default;
        gpuFrame = default;

        // Memory zurücksetzen
        ramAllocated = default;
        ramReserved = default;
        managedMemory = default;

        textureMemory = default;
        textureTargetMemory = default;
        graphicsDriverMemory = default;

        // GC zurücksetzen
        gcAllocated = default;

        gcCollectionsAtStart = GC.CollectionCount(0);
        previousGcCollections = gcCollectionsAtStart;
        gcCollectionsOnSlowFrames = 0;

        nextMemorySampleTime = Time.unscaledTime;

        isMeasuring = true;
    }

    public struct MeasuredData
    {
        // Rendering
        public double drawCalls;
        public double triangleCount;
        public double verticesCount;
        public double setCount;

        // CPU / GPU (Nanosekunden)
        public double cpuTotalFrame;
        public double cpuMainThread;
        public double cpuRenderThread;
        public double gpuFrame;

        // RAM (MiB)
        public double ramAllocatedAverage;
        public double ramAllocatedPeak;
        public double ramReservedAverage;
        public double ramReservedPeak;
        public double managedMemoryPeak;

        // Texturen (MiB)
        public double textureMemoryAverage;
        public double textureMemoryPeak;
        public double textureTargetMemory;

        // Graphics Driver (MiB)
        public double graphicsDriverMemoryPeak;

        // Garbage Collection
        public double gcAllocatedAverage;
        public double gcAllocatedPeak;
        public int gcCollections;
        public int gcCollectionsOnSlowFrames;

        // Gerätedaten
        public string deviceModel;
        public string operatingSystem;
        public string processor;
        public string graphicsDevice;

        public int deviceMemoryMB;
        public int graphicsMemoryMB;
        public int screenWidth;
        public int screenHeight;
    }

    public MeasuredData StopMeasurment()
    {
        isMeasuring = false;

        MeasuredData data = new MeasuredData();

        // Rendering
        data.drawCalls = drawCalls.Average;
        data.triangleCount = triangles.Average;
        data.verticesCount = vertices.Average;
        data.setCount = setPass.Average;

        // CPU / GPU
        // Weiterhin Nanosekunden
        data.cpuTotalFrame = cpuTotal.Average;
        data.cpuMainThread = cpuMain.Average;
        data.cpuRenderThread = cpuRender.Average;
        data.gpuFrame = gpuFrame.Average;

        // RAM in MiB
        data.ramAllocatedAverage =
            ramAllocated.Average / BytesPerMiB;

        data.ramAllocatedPeak =
            ramAllocated.Maximum / BytesPerMiB;

        data.ramReservedAverage =
            ramReserved.Average / BytesPerMiB;

        data.ramReservedPeak =
            ramReserved.Maximum / BytesPerMiB;

        data.managedMemoryPeak =
            managedMemory.Maximum / BytesPerMiB;

        // Texture Memory in MiB
        data.textureMemoryAverage =
            textureMemory.Average / BytesPerMiB;

        data.textureMemoryPeak =
            textureMemory.Maximum / BytesPerMiB;

        data.textureTargetMemory =
            textureTargetMemory.Average / BytesPerMiB;

        // Graphics Driver Memory in MiB
        data.graphicsDriverMemoryPeak =
            graphicsDriverMemory.Maximum / BytesPerMiB;

        // Garbage Collection
        data.gcAllocatedAverage = gcAllocated.Average;
        data.gcAllocatedPeak = gcAllocated.Maximum;

        data.gcCollections =
            GC.CollectionCount(0) - gcCollectionsAtStart;

        data.gcCollectionsOnSlowFrames =
            gcCollectionsOnSlowFrames;

        // Gerätedaten
        data.deviceModel = SystemInfo.deviceModel;
        data.operatingSystem = SystemInfo.operatingSystem;
        data.processor = SystemInfo.processorType;
        data.graphicsDevice = SystemInfo.graphicsDeviceName;

        data.deviceMemoryMB = SystemInfo.systemMemorySize;
        data.graphicsMemoryMB = SystemInfo.graphicsMemorySize;

        data.screenWidth = Screen.width;
        data.screenHeight = Screen.height;

        return data;
    }
    

    private void OnDisable()
    {
        drawCallsRecorder.Dispose();
        trianglesRecorder.Dispose();
        verticesRecorder.Dispose();
        setPassRecorder.Dispose();

        cpuFrameRecorder.Dispose();
        cpuMainThreadRecorder.Dispose();
        cpuRenderThreadRecorder.Dispose();
        gpuFrameRecorder.Dispose();

        gcAllocRecorder.Dispose();
    }
}