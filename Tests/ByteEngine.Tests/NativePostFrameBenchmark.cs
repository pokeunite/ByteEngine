using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;

namespace ByteEngine.Tests;

internal sealed class NativePostFrameBenchmark(string projectFile, string output, bool solid = false)
    : ByteEngineApplication(1920, 1080, "Paired native renderer comparison")
{
    GameProjectRuntime? project;
    int steps, phase, quartet, frame, warmFrame, begin, end;
    bool ready, measuring, previousParallel, queryRecorded, candidate, reverse;
    readonly Random order = new(20261109);
    readonly List<double> controlA = new(100), controlB = new(100);
    long cpuStamp;
    double submissionMs;
    readonly Stopwatch warm = new();
    readonly double[] group = new double[4];
    readonly List<double> noise = new(200), improvements = new(600), epochImprovements = new(200), epochs = new(3);
    readonly List<double> baselineGpu = new(1200), candidateGpu = new(1200), baselineCpu = new(1200), candidateCpu = new(1200);
    readonly List<Sample> samples = new(3200);
    readonly List<object> phases = new(5);
    readonly record struct Sample(int Phase, int Quartet, int Frame, bool Candidate, bool Reversed, double GpuTimelineMs, double CpuSubmissionMs);
    Matrix4x4 fixedView, fixedProjection;
    int worldWidth, worldHeight, visible;
    bool hasView;
    protected override bool ShouldUpdateScene => false;
    protected override void OnEngineStart()
    {
        Directory.CreateDirectory(output);
        IsVisible = false; WindowBorder = WindowBorder.Hidden; ClientSize = new(1920, 1080); UpdateFrequency = 0;
        Renderer3D.DiagnosticSolidLit = false;
        previousParallel = JobRunner.ParallelExecutionEnabled; JobRunner.ParallelExecutionEnabled = false;
        project = new(projectFile, Console.WriteLine); Scenes.LoadScene(project.LoadStartupScene());
        begin = GL.GenQuery(); end = GL.GenQuery();
        DiagnosticFrameBegin = () => { if (ready) { cpuStamp = Stopwatch.GetTimestamp(); GL.QueryCounter(begin, QueryCounterTarget.Timestamp); } };
        DiagnosticFrameEnd = () => { if (ready) { GL.QueryCounter(end, QueryCounterTarget.Timestamp); submissionMs = Stopwatch.GetElapsedTime(cpuStamp).TotalMilliseconds; queryRecorded = true; } };
        RenderWorld.DiagnosticMainBegin = (view, count) =>
        {
            if (!ready) return;
            if (!hasView) { fixedView = view.ViewMatrix; fixedProjection = view.ProjectionMatrix; worldWidth = view.TargetWidth; worldHeight = view.TargetHeight; visible = count; hasView = true; }
            if (view.ViewMatrix != fixedView || view.ProjectionMatrix != fixedProjection || count != visible || view.TargetWidth != worldWidth || view.TargetHeight != worldHeight)
                throw new Exception("Frozen camera/raster/workload changed; comparison rejected");
        };
    }
    protected override void OnEngineUpdate()
    {
        if (phase >= 5) return;
        if (steps < 600)
        {
            Time.Update(1d / 60); Scenes.UpdateInternal(); steps++;
            if (steps == 600)
            {
                foreach (var sky in Scenes.ActiveScene!.Environments) sky.ProfileGraphicsGpu = false;
                ready = true; warm.Start(); Console.WriteLine("Paused after 600 fixed 1/60 updates; warming both renderer paths for 60 seconds.");
            }
        }
        if (ready && warm.Elapsed.TotalSeconds >= 60) { if (!hasView) throw new Exception("No frozen 3D workload"); measuring = true; }
        candidate = ready && (!measuring ? (warmFrame++ & 1) != 0 : phase == 1 || phase >= 2 && ((frame is 1 or 2) != reverse));
        Renderer3D.DiagnosticSolidLit = solid && candidate;
        PostProcess3D.ResolveAntiAliasingOverride = candidate;
    }
    protected override void OnEngineRender()
    {
        if (!queryRecorded) return; queryRecorded = false;
        GL.GetQueryObject(end, GetQueryObjectParam.QueryResult, out long b);
        GL.GetQueryObject(begin, GetQueryObjectParam.QueryResult, out long a);
        if (!measuring) return;
        double gpu = (b - a) / 1e6;
        samples.Add(new(phase, quartet, frame, candidate, reverse, gpu, submissionMs)); group[frame] = gpu;
        if (phase >= 2) { (candidate ? candidateGpu : baselineGpu).Add(gpu); (candidate ? candidateCpu : baselineCpu).Add(submissionMs); }
        if (++frame != 4) return;
        frame = 0;
        double outer = (group[0] + group[3]) * .5, inner = (group[1] + group[2]) * .5;
        double delta = phase >= 2 && reverse ? 1 - outer / inner : 1 - inner / outer;
        if (phase == 0) controlA.Add(delta);
        if (phase == 1) controlB.Add(delta);
        if (phase >= 2) reverse = order.Next(2) != 0;
        if (phase < 2) noise.Add(Math.Abs(delta));
        else { improvements.Add(delta); epochImprovements.Add(delta); }
        if (++quartet != (phase < 2 ? 100 : 200)) return;
        string mode = phase == 0 ? "AAAA control" : phase == 1 ? "BBBB control" : $"ABBA epoch {phase - 1}";
        double value = phase < 2 ? Percentile(noise.TakeLast(100), .95) : Percentile(epochImprovements, .5);
        phases.Add(new { mode, quartets = quartet, pairedImprovementMedianOrControlP95 = value });
        Console.WriteLine($"{mode}: {(phase < 2 ? "local control noise p95" : "paired median improvement")}={value:P2}");
        if (phase >= 2) { epochs.Add(value); epochImprovements.Clear(); }
        Save(); quartet = 0;
        if (++phase < 5) return;
        double median = Percentile(improvements, .5), noise95 = Percentile(noise, .95), p05 = Percentile(improvements, .05);
        var effectCi = Bootstrap(improvements, 1001);
        var controlACi = Bootstrap(controlA, 1002);
        var controlBCi = Bootstrap(controlB, 1003);
        double controlBound = new[] { Math.Abs(controlACi.Low), Math.Abs(controlACi.High), Math.Abs(controlBCi.Low), Math.Abs(controlBCi.High) }.Max();
        bool passed = controlBound < .03 && effectCi.Low > Math.Max(.05, controlBound + .03) && epochs.All(x => x > .03);
        File.WriteAllText(Path.Combine(output, "native-frame-summary.json"), JsonSerializer.Serialize(new {
            baselineGpuTimelineMs = Percentile(baselineGpu, .5), candidateGpuTimelineMs = Percentile(candidateGpu, .5),
            baselineCpuSubmissionMs = Percentile(baselineCpu, .5), candidateCpuSubmissionMs = Percentile(candidateCpu, .5),
            pairedMedianImprovement = median, pairedImprovementP05 = p05, localControlNoiseP95 = noise95, epochs, effectCi, controlACi, controlBCi, bootstrapBlockQuartets = 10, bootstrapResamples = 2000, passed,
            gate = "Predefined independent confirmation: randomized ABBA/BAAB; block-bootstrap 95% CI of control medians inside +/-3%; effect lower CI >max(5%,control CI bound+3%); all epochs >3%. Individual-frame jitter is not the confidence interval of the median."
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (!passed) throw new Exception("Paired full-render acceptance gate failed");
        Close();
    }
    readonly record struct Interval(double Estimate, double Low, double High);
    static Interval Bootstrap(IReadOnlyList<double> values, int seed)
    {
        const int count = 2000, block = 10;
        var rng = new Random(seed); double[] draw = new double[values.Count], medians = new double[count];
        for (int sample = 0; sample < count; sample++)
        {
            int n = 0;
            while (n < draw.Length)
            {
                int start = rng.Next(values.Count);
                for (int k = 0; k < block && n < draw.Length; k++) draw[n++] = values[(start + k) % values.Count];
            }
            Array.Sort(draw); medians[sample] = draw[draw.Length / 2];
        }
        Array.Sort(medians);
        return new(Percentile(values, .5), medians[(int)(count * .025)], medians[(int)(count * .975)]);
    }
    static double Percentile(IEnumerable<double> values, double p) { var sorted = values.Order().ToArray(); return sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * p))]; }
    void Save()
    {
        var env = Scenes.ActiveScene!.Environments.FirstOrDefault();
        File.WriteAllText(Path.Combine(output, "native-frame-timings.json"), JsonSerializer.Serialize(new {
            renderer = GL.GetString(StringName.Renderer), outputWidth = WindowWidth, outputHeight = WindowHeight,
            worldWidth, worldHeight, visible, quality = env?.Quality.ToString(), renderScale = env?.RenderScale,
            build = typeof(Renderer3D).Assembly.ManifestModule.ModuleVersionId, fixedView, fixedProjection,
            fixedSteps = steps, timestep = 1d / 60, parallelism = false, paused = true, vsync = "off", profiler = false,
            restrictedSolidVariant = solid, materialCache = Renderer3D.EnableMaterialTextureCaching,
            post = true, shadows = true, audio = "backend unavailable in test executable",
            passes = GraphicsDiagnostics.Passes,
            measurement = "Seeded randomized ABBA/BAAB frame quartets; AAAA and BBBB controls. 95% block-bootstrap CIs estimate uncertainty of paired medians, not individual frame jitter. Full render GPU timeline may include CPU-induced idle; aligned CPU submission recorded before query wait. Simulation, swap and query readback excluded. Not combat FPS.", phases, samples
        }, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
    }
    protected override void OnEngineShutdown()
    {
        DiagnosticFrameBegin = null; DiagnosticFrameEnd = null; RenderWorld.DiagnosticMainBegin = null;
        Renderer3D.DiagnosticSolidLit = null; PostProcess3D.ResolveAntiAliasingOverride = null;
        JobRunner.ParallelExecutionEnabled = previousParallel;
        GL.DeleteQuery(begin); GL.DeleteQuery(end); project?.Dispose();
    }
}
