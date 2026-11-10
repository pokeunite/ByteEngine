using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Tests;

internal static class PostReplayBenchmark
{
    internal sealed record Snapshot(int WorldWidth, int WorldHeight, int OutputWidth, int OutputHeight,
        float Exposure, bool SmoothEdges, Matrix4x4 Projection, GraphicsLook Look, string Renderer,
        string Build, string Workload, int SimulationSteps);
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };

    internal sealed class Capture(string projectFile, string output) : ByteEngineApplication(1920, 1080, "Frozen post capture")
    {
        GameProjectRuntime? project;
        int steps;
        bool done, previousParallel;
        protected override bool ShouldUpdateScene => false;
        protected override void OnEngineStart()
        {
            IsVisible = false; WindowBorder = WindowBorder.Hidden; ClientSize = new(1920, 1080); UpdateFrequency = 0;
            previousParallel = JobRunner.ParallelExecutionEnabled; JobRunner.ParallelExecutionEnabled = false;
            project = new(projectFile, Console.WriteLine);
            Scenes.LoadScene(project.LoadStartupScene());
        }
        protected override void OnEngineUpdate()
        {
            Time.Update(1d / 60); Scenes.UpdateInternal();
            if (++steps == 600)
                PostProcess3D.CaptureInput = (source, depth, w, h, exposure, smooth, projection, look) =>
                {
                    PostProcess3D.CaptureInput = null;
                    Directory.CreateDirectory(output);
                    GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D, source);
                    GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, out int sw);
                    GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, out int sh);
                    float[] pixels = new float[sw * sh * 4];
                    GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.Float, pixels);
                    File.WriteAllBytes(Path.Combine(output, "hdr.bin"), MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
                    float[] depths = new float[sw * sh]; Array.Fill(depths, 1f);
                    if (depth != 0) { GL.BindTexture(TextureTarget.Texture2D, depth); GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.DepthComponent, PixelType.Float, depths); }
                    File.WriteAllBytes(Path.Combine(output, "depth.bin"), MemoryMarshal.AsBytes(depths.AsSpan()).ToArray());
                    var meta = new Snapshot(sw, sh, w, h, exposure, smooth, projection, look,
                        GL.GetString(StringName.Renderer) ?? "unknown", typeof(PostProcess3D).Assembly.ManifestModule.ModuleVersionId.ToString(),
                        "Hazard Pay runtime after 600 fixed 1/60 updates; immutable HDR/depth replay; not combat FPS", steps);
                    File.WriteAllText(Path.Combine(output, "snapshot.json"), JsonSerializer.Serialize(meta, Json));
                    Console.WriteLine($"Captured immutable post input: world {sw}x{sh}, output {w}x{h}, quality={look.Quality}, scale={look.RenderScale}");
                    done = true;
                };
        }
        protected override void OnEngineRender() { if (done) Close(); }
        protected override void OnEngineShutdown()
        { PostProcess3D.CaptureInput = null; JobRunner.ParallelExecutionEnabled = previousParallel; project?.Dispose(); }
    }

    private sealed class Frame : IDisposable
    {
        internal int Color, Depth, Fbo, Target;
        internal Snapshot Meta;
        internal Frame(Snapshot meta, float[] pixels, float[] depths)
        {
            Meta = meta;
            Color = Texture(meta.WorldWidth, meta.WorldHeight, pixels, false);
            Depth = Texture(meta.WorldWidth, meta.WorldHeight, depths, true);
            Target = Texture(meta.OutputWidth, meta.OutputHeight, null, false, true);
            Fbo = GL.GenFramebuffer(); GL.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, Target, 0);
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete) throw new Exception("Replay framebuffer incomplete");
        }
        static int Texture(int w, int h, float[]? pixels, bool depth, bool output = false)
        {
            int id = GL.GenTexture(); GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D, id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, depth ? PixelInternalFormat.DepthComponent32f : output ? PixelInternalFormat.Rgba8 : PixelInternalFormat.Rgba16f,
                w, h, 0, depth ? PixelFormat.DepthComponent : PixelFormat.Rgba, PixelType.Float, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)(depth ? TextureMinFilter.Nearest : TextureMinFilter.Linear));
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)(depth ? TextureMagFilter.Nearest : TextureMagFilter.Linear));
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            return id;
        }
        public void Dispose() { GL.DeleteFramebuffer(Fbo); GL.DeleteTexture(Target); GL.DeleteTexture(Color); GL.DeleteTexture(Depth); }
    }

    internal static void Run(string output, bool captured)
    {
        Directory.CreateDirectory(output);
        using var window = new NativeWindow(new NativeWindowSettings { ClientSize = new(64, 64), StartVisible = false, API = ContextAPI.OpenGL, APIVersion = new(3, 3), Profile = ContextProfile.Core });
        window.Context.MakeCurrent(); GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
        using var post = new PostProcess3D();
        var visualRows = new List<object>(); bool visualPass = true;
        for (int kind = 0; kind < 5; kind++)
            for (int phase = 0; phase < 3; phase++)
            {
                using var frame = Synthetic(kind, phase);
                visualPass &= Compare(post, frame, output, $"fixture-{kind}-pan-{phase}", visualRows);
            }
        if (captured)
        {
            var meta = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(Path.Combine(output, "snapshot.json")), Json)!;
            using var frame = new Frame(meta, Floats(Path.Combine(output, "hdr.bin")), Floats(Path.Combine(output, "depth.bin")));
            visualPass &= Compare(post, frame, output, "hazard-pay", visualRows);
            Measure(post, frame, output);
        }
        else { using var frame = Synthetic(2, 0, 1920, 1080); Measure(post, frame, output); }
        File.WriteAllText(Path.Combine(output, "visual-results.json"), JsonSerializer.Serialize(new { visualPass, gate = "RGB MAE <= 2; pixels with any RGB channel difference >16 <=3%; unchanged AA-off/pixel-art paths exact", rows = visualRows }, Json));
        if (GL.GetError() != ErrorCode.NoError) throw new Exception("GL error in post replay");
        if (!visualPass) throw new Exception("Resolved post image gate failed; see visual-results.json. Candidate stays disabled.");
    }
    static float[] Floats(string path) => MemoryMarshal.Cast<byte, float>(File.ReadAllBytes(path)).ToArray();
    static Frame Synthetic(int kind, int phase, int ow = 1280, int oh = 720)
    {
        int w = (int)(ow * .784), h = (int)(oh * .784); float[] rgb = new float[w * h * 4], depth = new float[w * h];
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(1f, (float)ow / oh, .1f, 100f);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float u = (x + phase * .7f) / w, v = (float)y / h;
            bool edge = ((int)(u * 40 + v * 17) % 7 == 0) || Math.Abs(u - v * .6f - .15f) < .002f;
            Vector3 c = kind switch {
                0 => new(.2f + v * 1.5f, .35f + v, .6f + v),
                1 => new(.01f, .04f + v * .2f, .08f + v * .4f),
                2 => new(.45f + .1f * MathF.Sin(u * 150), .21f, .07f),
                3 => new(.002f, .004f, .01f),
                _ => new(.15f, .15f, .15f) };
            if (edge) c = kind == 3 ? new(12, 3, .2f) : new(.005f);
            if ((u - .75f) * (u - .75f) + (v - .7f) * (v - .7f) < .003f) c = new(15, 9, 2);
            int i = (y * w + x) * 4; rgb[i] = c.X; rgb[i + 1] = c.Y; rgb[i + 2] = c.Z; rgb[i + 3] = 1;
            var z = Vector4.Transform(new Vector4(0, 0, edge ? -2 : -2.3f, 1), projection); depth[y * w + x] = (z.Z / z.W) * .5f + .5f;
        }
        var look = new SkyEnvironment { Quality = GraphicsQuality.Balanced }.CaptureLook() with { Warmth = kind == 2 ? .4f : -.1f, Saturation = kind == 4 ? .1f : 1, Contrast = 1.1f, ProfileGpu = false, RenderScale = .784f };
        return new Frame(new(w, h, ow, oh, 1, true, projection, look, GL.GetString(StringName.Renderer) ?? "unknown", typeof(PostProcess3D).Assembly.ManifestModule.ModuleVersionId.ToString(), "Synthetic HDR stress fixture; not a reference game scene", 0), rgb, depth);
    }
    static void Draw(PostProcess3D post, Frame frame, bool resolved, bool smooth = true, GraphicsLook? look = null)
    {
        var m = frame.Meta; post.ResolveAntiAliasing = resolved;
        post.Render(frame.Color, frame.Fbo, m.OutputWidth, m.OutputHeight, true, m.Exposure, smooth, frame.Depth, m.Projection, (look ?? m.Look) with { ProfileGpu = false });
    }
    static byte[] Read(Frame f)
    { byte[] p = new byte[f.Meta.OutputWidth * f.Meta.OutputHeight * 4]; GL.ReadPixels(0, 0, f.Meta.OutputWidth, f.Meta.OutputHeight, PixelFormat.Rgba, PixelType.UnsignedByte, p); return p; }
    static bool Compare(PostProcess3D post, Frame f, string output, string name, List<object> rows)
    {
        Draw(post, f, false); byte[] a = Read(f); Draw(post, f, true); byte[] b = Read(f);
        long total = 0; int max = 0, changed = 0; byte[] diff = new byte[a.Length];
        for (int i = 0; i < a.Length; i += 4)
        { bool large = false; for (int c = 0; c < 3; c++) { int d = Math.Abs(a[i + c] - b[i + c]); total += d; max = Math.Max(max, d); large |= d > 16; diff[i + c] = (byte)Math.Min(255, d * 4); } if (large) changed++; diff[i + 3] = 255; }
        double mae = total / (a.Length * .75), fraction = changed / (a.Length / 4.0);
        Draw(post, f, false, false); var plain = Read(f); Draw(post, f, true, false);
        bool bypass = plain.SequenceEqual(Read(f));
        var pixel = f.Meta.Look with { ShaderStyle = BuiltInSceneShader.PixelArt };
        Draw(post, f, false, true, pixel); var pa = Read(f); Draw(post, f, true, true, pixel); bypass &= pa.SequenceEqual(Read(f));
        bool passed = mae <= 2 && fraction <= .03 && bypass;
        rows.Add(new { name, mae, max, changedFraction = fraction, bypassExact = bypass, passed });
        Save(a, f.Meta.OutputWidth, f.Meta.OutputHeight, Path.Combine(output, name + "-baseline.png"));
        Save(b, f.Meta.OutputWidth, f.Meta.OutputHeight, Path.Combine(output, name + "-resolved.png"));
        Save(diff, f.Meta.OutputWidth, f.Meta.OutputHeight, Path.Combine(output, name + "-difference-x4.png"));
        Console.WriteLine($"Image {name}: MAE={mae:F3}, >16={fraction:P2}, bypass={bypass}, pass={passed}"); return passed;
    }
    static void Save(byte[] rgba, int w, int h, string path)
    {
        byte[] bgra = (byte[])rgba.Clone(); for (int i = 0; i < bgra.Length; i += 4) (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
        using var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var bits = bmp.LockBits(new(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
        Marshal.Copy(bgra, 0, bits.Scan0, bgra.Length); bmp.UnlockBits(bits); bmp.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipY); bmp.Save(path);
    }
    static void Measure(PostProcess3D post, Frame f, string output)
    {
        Console.WriteLine("Warming both post paths for 60 seconds on the same immutable input...");
        var warm = Stopwatch.StartNew(); int i = 0;
        while (warm.Elapsed.TotalSeconds < 60) { Draw(post, f, (i++ & 1) != 0); GL.Finish(); }
        int begin = GL.GenQuery(), end = GL.GenQuery(); var rows = new List<object>(); var medians = new List<double>();
        // First two identical baseline blocks establish a same-path control.
        string[] modes = ["control-A", "control-A", "A", "B", "A", "B", "A", "B", "AA-off", "AO-off", "Bloom-off"];
        foreach (string mode in modes)
        {
            for (int frame = 0; frame < 30; frame++) { Draw(post, f, mode == "B", mode != "AA-off"); GL.Finish(); }
            double[] samples = new double[600]; var wall = Stopwatch.StartNew();
            var look = f.Meta.Look;
            if (mode == "AO-off") look = look with { Occlusion = 0 };
            if (mode == "Bloom-off") look = look with { Bloom = 0 };
            for (int frame = 0; frame < samples.Length; frame++)
            {
                GL.QueryCounter(begin, QueryCounterTarget.Timestamp);
                Draw(post, f, mode == "B", mode != "AA-off", look);
                GL.QueryCounter(end, QueryCounterTarget.Timestamp);
                GL.GetQueryObject(end, GetQueryObjectParam.QueryResult, out long b);
                GL.GetQueryObject(begin, GetQueryObjectParam.QueryResult, out long a);
                samples[frame] = (b - a) / 1e6;
            }
            double Median(IEnumerable<double> values) { var sorted = values.Order().ToArray(); return sorted[sorted.Length / 2]; }
            double median = Median(samples); medians.Add(median);
            rows.Add(new { mode, gpuP50Ms = median, gpuP95Ms = samples.Order().ElementAt(570), first100Ms = Median(samples.Take(100)), last100Ms = Median(samples.TakeLast(100)), wallSeconds = wall.Elapsed.TotalSeconds, samples });
            Console.WriteLine($"Post {mode}: {median:F3} ms GPU p50");
            File.WriteAllText(Path.Combine(output, "timings.json"), JsonSerializer.Serialize(new { metadata = f.Meta, executionBuild = typeof(PostProcess3D).Assembly.ManifestModule.ModuleVersionId, vsync = "offscreen/no swap", profiler = "external timestamp pairs; shipping profiler disabled", audio = "absent in immutable replay", measurement = "synchronized isolated post GPU time; not game FPS", rows }, Json));
        }
        GL.DeleteQuery(begin); GL.DeleteQuery(end);
        double baseline = new[] { medians[2], medians[4], medians[6] }.Order().ElementAt(1);
        double candidate = new[] { medians[3], medians[5], medians[7] }.Order().ElementAt(1);
        double controlNoise = Math.Abs(medians[1] / medians[0] - 1), improvement = 1 - candidate / baseline;
        bool gate = improvement > Math.Max(.10, controlNoise * 2);
        File.WriteAllText(Path.Combine(output, "timing-gate.json"), JsonSerializer.Serialize(new { baselineMs = baseline, candidateMs = candidate, improvement, controlNoise, passed = gate, target = "At least 10% GPU improvement and >2x measured same-path drift; isolated post only" }, Json));
        if (!gate) throw new Exception("Post timing gate failed; candidate stays disabled");
    }
}
