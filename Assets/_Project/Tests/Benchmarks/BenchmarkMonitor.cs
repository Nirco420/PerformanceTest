using System.Collections.Generic;
using UnityEngine;

public class BenchmarkMonitor : MonoBehaviour
{
    public void CancelMeasurement() { isMeasuring = false; }
    private bool isMeasuring;
    private int measuredFrames;
    private float measuredSeconds;

    private List<float> frameTimes = new List<float>(16384);

    public struct FrameResults
    {
        public float averageFPS;
        public float p95;
        public float p99;
        public float onePercentLow;
        public float measuredSeconds;
        public int measuredFrames;
    }

    public FrameResults LastResult { get; private set; }

    void Update()
    {
        if (!isMeasuring)
        {
            return;
        }

        measuredFrames++;
        frameTimes.Add(Time.unscaledDeltaTime * 1000f);
        measuredSeconds += Time.unscaledDeltaTime;
    }

    public void StartMeasruement()
    {
        measuredFrames = 0;
        measuredSeconds = 0f;
        frameTimes.Clear();

        LastResult = new FrameResults();

        isMeasuring = true;
    }

    public float StopMeasrument()
    {
        isMeasuring = false;

        FrameResults results = new FrameResults
        {
            averageFPS = float.NaN,
            p95 = float.NaN,
            p99 = float.NaN,
            onePercentLow = float.NaN,
            measuredSeconds = measuredSeconds,
            measuredFrames = measuredFrames
        };

        if (frameTimes.Count == 0 || measuredSeconds <= 0f)
        {
            LastResult = results;
            return float.NaN;
        }

        List<float> sortedFrames = new List<float>(frameTimes);
        sortedFrames.Sort();

        // P95 und P99
        int p95Index = Mathf.CeilToInt(sortedFrames.Count * 0.95f) - 1;
        int p99Index = Mathf.CeilToInt(sortedFrames.Count * 0.99f) - 1;

        results.p95 = sortedFrames[p95Index];
        results.p99 = sortedFrames[p99Index];

        // 1% Low
        int onePercentCount = Mathf.Max(
            1,
            Mathf.CeilToInt(sortedFrames.Count * 0.01f)
        );

        float totalFrameTime = 0f;

        for (int i = sortedFrames.Count - onePercentCount;
             i < sortedFrames.Count;
             i++)
        {
            totalFrameTime += sortedFrames[i];
        }

        float averageFrameTime = totalFrameTime / onePercentCount;

        if (averageFrameTime > 0f)
        {
            results.onePercentLow = 1000f / averageFrameTime;
        }

        // Average FPS
        results.averageFPS = measuredFrames / measuredSeconds;

        // Ergebnisse speichern
        LastResult = results;

        Debug.Log($"P95: {results.p95:F2} ms");
        Debug.Log($"P99: {results.p99:F2} ms");
        Debug.Log($"1% Low: {results.onePercentLow:F2} FPS");

        return results.averageFPS;
    }
}