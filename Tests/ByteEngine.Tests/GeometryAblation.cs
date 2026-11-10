using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;

namespace ByteEngine.Tests;

// Isolates main-pass shading on the same paused runtime scene. Flat mode uses
// the same vertex shader, skinning, instance stream, cutout test and GL states.
internal sealed class GeometryAblation(string projectFile, string output, bool solid = false) : ByteEngineApplication(1920, 1080, "Geometry ablation")
{
    GameProjectRuntime? project;
    int steps, block, samples, begin, end;
    bool previousParallel, ready, queryRecorded, measuring;
    byte[]? reference;
    object? depthImage;
    readonly Stopwatch warm = new();
    readonly List<double> gpu = new(600);
    readonly List<object> rows = new();
    readonly List<double> medians = new();
    readonly string[] modes = solid
        ? ["control-A", "control-A", "A-lit", "B-solid", "A-lit", "B-solid", "A-lit", "B-solid"]
        : ["control-A", "control-A", "A-lit", "B-flat", "A-lit", "B-flat", "A-lit", "B-flat", "A-lit", "C-depth", "A-lit", "C-depth", "A-lit", "C-depth"];
    Matrix4x4 fixedView, fixedProjection;
    int worldWidth, worldHeight, visible;
    bool hasView;
    protected override bool ShouldUpdateScene => false;
    protected override void OnEngineStart()
    {
        Directory.CreateDirectory(output);
        IsVisible = false; WindowBorder = WindowBorder.Hidden; ClientSize = new(1920, 1080); UpdateFrequency = 0;
        previousParallel = JobRunner.ParallelExecutionEnabled; JobRunner.ParallelExecutionEnabled = false;
        project = new(projectFile, Console.WriteLine); Scenes.LoadScene(project.LoadStartupScene());
        begin = GL.GenQuery(); end = GL.GenQuery();
        RenderWorld.DiagnosticMainBegin = (view, count) =>
        {
            if (!ready) return;
            if (!hasView) { fixedView = view.ViewMatrix; fixedProjection = view.ProjectionMatrix; worldWidth = view.TargetWidth; worldHeight = view.TargetHeight; visible = count; hasView = true; }
            if (view.ViewMatrix != fixedView || view.ProjectionMatrix != fixedProjection || count != visible || view.TargetWidth != worldWidth || view.TargetHeight != worldHeight)
                throw new Exception("Paused geometry workload changed; comparison rejected");
            GL.QueryCounter(begin, QueryCounterTarget.Timestamp);
        };
        RenderWorld.DiagnosticMainEnd = () => { if (ready) { GL.QueryCounter(end, QueryCounterTarget.Timestamp); queryRecorded = true; } };
    }
    protected override void OnEngineUpdate()
    {
        if (block >= modes.Length) return;
        if (steps < 600)
        {
            Time.Update(1d / 60); Scenes.UpdateInternal(); steps++;
            if (steps == 600)
            {
                foreach (var sky in Scenes.ActiveScene!.Environments) sky.ProfileGraphicsGpu = false;
                ready = true; warm.Start(); Console.WriteLine("Scene simulation paused after 600 fixed updates. Warming lit/flat paths for 60 seconds.");
            }
        }
        if (ready && warm.Elapsed.TotalSeconds >= 60 && !measuring) { measuring = true; samples = 0; }
        Renderer3D.DiagnosticFlatFragments = ready && !solid && (!measuring ? (samples++ % 3) == 1 : modes[block] == "B-flat");
        Renderer3D.DiagnosticSolidLit = ready && solid && (!measuring ? (samples++ & 1) != 0 : modes[block] == "B-solid");
        Renderer3D.DiagnosticDepthPrepass = ready && !solid && (!measuring ? samples % 3 == 2 : modes[block] == "C-depth");
    }
    protected override void OnEngineRender()
    {
        if (!queryRecorded) return;
        queryRecorded = false;
        GL.GetQueryObject(end, GetQueryObjectParam.QueryResult, out long b);
        GL.GetQueryObject(begin, GetQueryObjectParam.QueryResult, out long a);
        if (warm.Elapsed.TotalSeconds < 60) return;
        // Discard settling frames after every mode transition.
        if (samples++ < 30) return;
        gpu.Add((b - a) / 1e6);
        if (gpu.Count != 600) return;
        double Median(IEnumerable<double> values) { var sorted = values.Order().ToArray(); return sorted[sorted.Length / 2]; }
        double median = Median(gpu); medians.Add(median);
        if (block == 0 || modes[block] == "C-depth" || modes[block] == "B-solid")
        {
            byte[] image = new byte[WindowWidth * WindowHeight * 4];
            GL.ReadPixels(0, 0, WindowWidth, WindowHeight, PixelFormat.Rgba, PixelType.UnsignedByte, image);
            if (block == 0) reference = image;
            else
            {
                long sum = 0; int changed = 0, max = 0;
                for (int i = 0; i < image.Length; i++) if (i % 4 != 3) { int d = Math.Abs(image[i] - reference![i]); sum += d; if (d > 1) changed++; max = Math.Max(max, d); }
                double mae = sum / (image.Length * .75), fraction = changed / (image.Length * .75);
                depthImage = new { mae, max, channelsChangedAbove1 = fraction, passed = mae < .1 && fraction < .001 };
                Console.WriteLine($"Candidate image: MAE={mae:F4}, channels >1={fraction:P3}");
            }
        }
        rows.Add(new { mode = modes[block], gpuP50Ms = median, gpuP95Ms = gpu.Order().ElementAt(570), first100Ms = Median(gpu.Take(100)), last100Ms = Median(gpu.TakeLast(100)), samples = gpu.ToArray() });
        Console.WriteLine($"Geometry {modes[block]}: {median:F3} ms GPU p50; visible={visible}, raster={worldWidth}x{worldHeight}");
        var env = Scenes.ActiveScene!.Environments.FirstOrDefault();
        File.WriteAllText(Path.Combine(output, "geometry-timings.json"), JsonSerializer.Serialize(new {
            renderer = GL.GetString(StringName.Renderer), outputWidth = WindowWidth, outputHeight = WindowHeight,
            worldWidth, worldHeight, visible, quality = env?.Quality.ToString(), renderScale = env?.RenderScale,
            build = typeof(Renderer3D).Assembly.ManifestModule.ModuleVersionId, fixedView, fixedProjection,
            fixedSteps = steps, timestep = 1d / 60, parallelism = false, paused = true, audio = "backend unavailable in test executable",
            measurement = "Main-pass timestamp pairs only; flat preserves vertex deformation/cutout and raster states. Not game FPS or a shipping shader.", rows
        }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        gpu.Clear(); samples = 0;
        if (++block < modes.Length) return;
        double lit = new[] { medians[2], medians[4], medians[6] }.Order().ElementAt(1);
        double flat = new[] { medians[3], medians[5], medians[7] }.Order().ElementAt(1);
        if (solid)
        {
            File.WriteAllText(Path.Combine(output, "geometry-summary.json"), JsonSerializer.Serialize(new { litMs = lit, solidLitMs = flat, improvement = 1 - flat / lit, controlNoise = Math.Abs(medians[1] / medians[0] - 1), image = depthImage, conclusion = "Restricted solid-material program; preserves PBR, shadows, fog and IBL; generic shader retained for other materials." }, new JsonSerializerOptions { WriteIndented = true }));
            Close(); return;
        }
        double depthLit = new[] { medians[8], medians[10], medians[12] }.Order().ElementAt(1);
        double depthPass = new[] { medians[9], medians[11], medians[13] }.Order().ElementAt(1);
        File.WriteAllText(Path.Combine(output, "geometry-summary.json"), JsonSerializer.Serialize(new { litMs = lit, flatMs = flat, reduction = 1 - flat / lit, controlNoise = Math.Abs(medians[1] / medians[0] - 1), depthBaselineMs = depthLit, depthPrepassMs = depthPass, depthImprovement = 1 - depthPass / depthLit, depthImage, conclusion = "Flat is an ablation, not a shipping shader. Prepass timing includes its depth draw and original colour pass." }, new JsonSerializerOptions { WriteIndented = true }));
        Close();
    }
    protected override void OnEngineShutdown()
    {
        RenderWorld.DiagnosticMainBegin = null; RenderWorld.DiagnosticMainEnd = null;
        Renderer3D.DiagnosticSolidLit = false;
        Renderer3D.DiagnosticFlatFragments = false;
        Renderer3D.DiagnosticDepthPrepass = false;
        JobRunner.ParallelExecutionEnabled = previousParallel;
        GL.DeleteQuery(begin); GL.DeleteQuery(end); project?.Dispose();
    }
}
