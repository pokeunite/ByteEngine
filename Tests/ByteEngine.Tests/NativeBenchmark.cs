using System.Globalization;
using ByteEngine.Core;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Tests;

// Runs the real player path, without modifying or exporting the source project.
internal sealed class NativeBenchmark(string projectFile, string csv, int width, int height, bool cache)
    : ByteEngineApplication(width, height, "Native benchmark")
{
    private GameProjectRuntime? _project;
    private readonly List<double> _cpu = new(300);
    private readonly List<double> _gpu = new(300);
    private long _allocations, _binds, _skipped;
    private long _startBinds, _startSkipped;
    private int _frames, _gc;

    protected override void OnEngineStart()
    {
        IsVisible = false;
        WindowBorder = OpenTK.Windowing.Common.WindowBorder.Hidden;
        ClientSize = new(width, height);
        UpdateFrequency = 0;
        Renderer3D.EnableMaterialTextureCaching = cache;
        SurfacePerformanceDiagnostics.PerformanceEnabled = true;
        _project = new GameProjectRuntime(projectFile, Console.WriteLine);
        Scenes.LoadScene(_project.LoadStartupScene());
        foreach (var item in Scenes.ActiveScene!.GameObjects)
            if (item.GetComponent<SkyEnvironment>() is { } sky) sky.ProfileGraphicsGpu = true;
        if (WindowWidth != width || WindowHeight != height)
            throw new InvalidOperationException($"Benchmark viewport is {WindowWidth}x{WindowHeight}; expected {width}x{height}.");
        Console.WriteLine($"Native bench {WindowWidth}x{WindowHeight}, material cache={cache}; GPU={GL.GetString(StringName.Renderer)}");
    }

    protected override void OnEngineRender()
    {
        // LastRenderCpuMilliseconds is the completed preceding frame. Warmup
        // keeps startup, asset upload and first-use shader compilation out.
        if (++_frames == 120)
        {
            _gc = GC.CollectionCount(0);
            _startBinds = Renderer3D.MaterialTextureBinds;
            _startSkipped = Renderer3D.SkippedMaterialTextureBinds;
        }
        if (_frames <= 120) return;
        var metrics = Scenes.ActiveScene!.LastFrameMetrics;
        _cpu.Add(LastRenderCpuMilliseconds + metrics.UpdateMs + metrics.PhysicsMs + metrics.LateUpdateMs);
        if (SurfacePerformanceDiagnostics.GpuMilliseconds is { } gpu) _gpu.Add(gpu);
        _allocations += metrics.AllocatedBytes;
        if (_cpu.Count < 300) return;
        _binds = Renderer3D.MaterialTextureBinds - _startBinds;
        _skipped = Renderer3D.SkippedMaterialTextureBinds - _startSkipped;
        _cpu.Sort(); _gpu.Sort();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(csv))!);
        if (!File.Exists(csv))
            File.WriteAllText(csv, "Configuration,CpuP50Ms,CpuP95Ms,GpuP50Ms,UpdateAllocatedBytesPerFrame,Gen0Collections,MaterialBindsPerFrame,SkippedMaterialBindsPerFrame\n");
        string configuration = $"{WindowWidth}x{WindowHeight}-material-cache-{cache}";
        File.AppendAllText(csv, string.Join(",", configuration,
            F(_cpu[150]), F(_cpu[285]), _gpu.Count == 0 ? "" : F(_gpu[_gpu.Count / 2]),
            F(_allocations / 300.0), GC.CollectionCount(0) - _gc, F(_binds / 300.0), F(_skipped / 300.0)) + "\n");
        Console.WriteLine($"Wrote {configuration}: CPU p50={_cpu[150]:F2} ms, material binds={_binds/300.0:F1}/frame");
        GraphicsDiagnostics.ExportCapture(Path.ChangeExtension(csv, $".{WindowWidth}x{WindowHeight}.json"), Scenes.ActiveScene!);
        Console.WriteLine($"Last sampled geometry GPU={GraphicsDiagnostics.GeometryGpuMs:F2} ms; post GPU={GraphicsDiagnostics.PostGpuMs:F2} ms; update={metrics.UpdateMs:F2}, physics={metrics.PhysicsMs:F2}, late={metrics.LateUpdateMs:F2} ms");
        SurfacePerformanceDiagnostics.PerformanceEnabled = false;
        Close();
    }

    private static string F(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
    protected override void OnEngineShutdown() => _project?.Dispose();
}
