using System.Diagnostics;
using System.Numerics;

using ByteEngine.Core;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class PerformancePanel
{
    private const double ProcessSampleIntervalSeconds = 0.5;

    private readonly Process _process =
        Process.GetCurrentProcess();

    private readonly Stopwatch _wallClock =
        Stopwatch.StartNew();

    private readonly float[] _frameTimes =
        new float[180];

    private TimeSpan _lastProcessorTime;

    private double _lastProcessSampleSeconds;

    private int _frameHistoryCount;

    private int _frameHistoryIndex;

    private float _cpuPercent;

    private long _workingSetBytes;

    private long _privateBytes;

    private long _managedBytes;

    private long _heapBytes;

    private long _totalAllocatedBytes;

    private int _threadCount;

    private int _handleCount;

    public bool IsOpen { get; set; } =
        true;

    public PerformancePanel()
    {
        _lastProcessorTime =
            _process.TotalProcessorTime;

        _lastProcessSampleSeconds =
            _wallClock.Elapsed.TotalSeconds;

        SampleProcess(
            force: true);
    }

    public void Draw(
        EditorState state,
        bool sceneViewOpen,
        bool gameViewOpen)
    {
        float frameTimeMs =
            (float)Math.Max(
                Time.DeltaTime * 1000.0,
                0.0);

        if (float.IsFinite(frameTimeMs) &&
            frameTimeMs > 0.0f)
        {
            AddFrameSample(
                frameTimeMs);
        }

        SampleProcess(
            force: false);

        bool open =
            IsOpen;

        ImGui.SetNextWindowSize(
            new Vector2(
                390.0f,
                500.0f),
            ImGuiCond.FirstUseEver);

        bool visible =
            ImGui.Begin(
                "Performance",
                ref open);

        IsOpen =
            open;

        if (!visible)
        {
            ImGui.End();

            return;
        }

        DrawFrameSection();
        DrawProcessSection();
        DrawMemorySection();
        DrawSceneSection(
            state,
            sceneViewOpen,
            gameViewOpen);

        ImGui.End();
    }

    private void DrawFrameSection()
    {
        GetFrameStatistics(
            out float averageMs,
            out float minimumMs,
            out float maximumMs);

        float fps =
            averageMs > 0.0001f
                ? 1000.0f / averageMs
                : 0.0f;

        ImGui.SeparatorText(
            "FRAME");

        DrawMetric(
            "FPS",
            $"{fps:0.0}");

        DrawMetric(
            "Frame Time",
            $"{averageMs:0.00} ms");

        DrawMetric(
            "Best",
            $"{minimumMs:0.00} ms");

        DrawMetric(
            "Worst",
            $"{maximumMs:0.00} ms");

        const float sixtyFpsBudgetMs =
            1000.0f / 60.0f;

        float budgetRatio =
            sixtyFpsBudgetMs > 0.0f
                ? averageMs /
                  sixtyFpsBudgetMs
                : 0.0f;

        ImGui.TextDisabled(
            "60 FPS frame budget");

        ImGui.ProgressBar(
            Math.Clamp(
                budgetRatio,
                0.0f,
                1.0f),
            new Vector2(
                -1.0f,
                0.0f),
            $"{averageMs:0.00} / {sixtyFpsBudgetMs:0.00} ms");

        if (averageMs <=
            sixtyFpsBudgetMs)
        {
            ImGui.TextDisabled(
                "Within 60 FPS budget.");
        }
        else
        {
            ImGui.TextDisabled(
                "Over 60 FPS budget.");
        }
    }

    private void DrawProcessSection()
    {
        ImGui.SeparatorText(
            "PROCESS");

        DrawMetric(
            "CPU",
            $"{_cpuPercent:0.0}%");

        DrawMetric(
            "Threads",
            _threadCount.ToString());

        DrawMetric(
            "Handles",
            _handleCount.ToString());

        DrawMetric(
            "Logical CPUs",
            Environment.ProcessorCount
                .ToString());
    }

    private void DrawMemorySection()
    {
        ImGui.SeparatorText(
            "MEMORY");

        DrawMetric(
            "Working Set",
            FormatBytes(
                _workingSetBytes));

        DrawMetric(
            "Private Memory",
            FormatBytes(
                _privateBytes));

        DrawMetric(
            "Managed Memory",
            FormatBytes(
                _managedBytes));

        DrawMetric(
            "GC Heap",
            FormatBytes(
                _heapBytes));

        DrawMetric(
            "Total Allocated",
            FormatBytes(
                _totalAllocatedBytes));

        DrawMetric(
            "GC Collections",
            $"Gen0 {GC.CollectionCount(0)}   " +
            $"Gen1 {GC.CollectionCount(1)}   " +
            $"Gen2 {GC.CollectionCount(2)}");
    }

    private static void DrawSceneSection(
        EditorState state,
        bool sceneViewOpen,
        bool gameViewOpen)
    {
        ImGui.SeparatorText(
            "EDITOR / SCENE");

        DrawMetric(
            "Mode",
            state.Mode.ToString());

        DrawMetric(
            "GameObjects",
            state.DisplayedScene
                .GameObjectCount
                .ToString());

        DrawMetric(
            "Scene View",
            sceneViewOpen
                ? "Open"
                : "Closed");

        DrawMetric(
            "Game View",
            gameViewOpen
                ? "Open"
                : "Closed");

        ImGui.Dummy(
            new Vector2(
                0.0f,
                4.0f));

        ImGui.TextWrapped(
            "Hidden dock tabs do not render their viewport, keeping idle GPU load low.");
    }

    private void SampleProcess(
        bool force)
    {
        double nowSeconds =
            _wallClock.Elapsed.TotalSeconds;

        double elapsed =
            nowSeconds -
            _lastProcessSampleSeconds;

        if (!force &&
            elapsed <
            ProcessSampleIntervalSeconds)
        {
            return;
        }

        try
        {
            _process.Refresh();

            TimeSpan processorTime =
                _process.TotalProcessorTime;

            double cpuSeconds =
                (
                    processorTime -
                    _lastProcessorTime
                ).TotalSeconds;

            if (elapsed > 0.0001)
            {
                double normalizedCpu =
                    cpuSeconds /
                    elapsed /
                    Math.Max(
                        Environment.ProcessorCount,
                        1) *
                    100.0;

                _cpuPercent =
                    (float)Math.Clamp(
                        normalizedCpu,
                        0.0,
                        100.0);
            }

            _lastProcessorTime =
                processorTime;

            _lastProcessSampleSeconds =
                nowSeconds;

            _workingSetBytes =
                _process.WorkingSet64;

            _privateBytes =
                _process.PrivateMemorySize64;

            _threadCount =
                _process.Threads.Count;

            _handleCount =
                _process.HandleCount;

            _managedBytes =
                GC.GetTotalMemory(
                    forceFullCollection: false);

            GCMemoryInfo memoryInfo =
                GC.GetGCMemoryInfo();

            _heapBytes =
                memoryInfo.HeapSizeBytes;

            _totalAllocatedBytes =
                GC.GetTotalAllocatedBytes(
                    precise: false);
        }
        catch
        {
            /*
             * Performance monitoring should never be able to
             * crash the editor.
             */
        }
    }

    private void AddFrameSample(
        float milliseconds)
    {
        _frameTimes[_frameHistoryIndex] =
            milliseconds;

        _frameHistoryIndex =
            (
                _frameHistoryIndex +
                1
            ) %
            _frameTimes.Length;

        _frameHistoryCount =
            Math.Min(
                _frameHistoryCount +
                1,
                _frameTimes.Length);
    }

    private void GetFrameStatistics(
        out float average,
        out float minimum,
        out float maximum)
    {
        if (_frameHistoryCount ==
            0)
        {
            average =
                0.0f;

            minimum =
                0.0f;

            maximum =
                0.0f;

            return;
        }

        float total =
            0.0f;

        minimum =
            float.MaxValue;

        maximum =
            0.0f;

        for (int index = 0;
             index < _frameHistoryCount;
             index++)
        {
            float value =
                _frameTimes[index];

            total +=
                value;

            minimum =
                Math.Min(
                    minimum,
                    value);

            maximum =
                Math.Max(
                    maximum,
                    value);
        }

        average =
            total /
            _frameHistoryCount;
    }

    private static void DrawMetric(
        string name,
        string value)
    {
        if (ImGui.BeginTable(
                $"Metric:{name}",
                2,
                ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn(
                "Name",
                ImGuiTableColumnFlags.WidthStretch,
                0.55f);

            ImGui.TableSetupColumn(
                "Value",
                ImGuiTableColumnFlags.WidthStretch,
                0.45f);

            ImGui.TableNextColumn();

            ImGui.TextDisabled(
                name);

            ImGui.TableNextColumn();

            ImGui.Text(
                value);

            ImGui.EndTable();
        }
    }

    private static string FormatBytes(
        long bytes)
    {
        const double kilobyte =
            1024.0;

        const double megabyte =
            kilobyte *
            1024.0;

        const double gigabyte =
            megabyte *
            1024.0;

        if (bytes >=
            gigabyte)
        {
            return $"{bytes / gigabyte:0.00} GB";
        }

        if (bytes >=
            megabyte)
        {
            return $"{bytes / megabyte:0.0} MB";
        }

        if (bytes >=
            kilobyte)
        {
            return $"{bytes / kilobyte:0.0} KB";
        }

        return $"{bytes} B";
    }
}
