using System.Diagnostics;
using System.Numerics;
using System.Reflection;

using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Vfx;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Tests;

internal static class VfxDepthTests
{
    private static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }

    internal static void Run(string root)
    {
        string output = Path.GetFullPath(".artifacts/vfx-depth");
        Directory.CreateDirectory(output);
        foreach (var preset in new[] { VfxPreset.Firebolt, VfxPreset.FireboltRelease, VfxPreset.FireboltImpact })
        {
            var definition = VfxPresets.Create(preset);
            string path = Path.Combine(root, "Assets", preset + ".bvfx");
            VfxEffectSerializer.Save(path, definition);
            var copy = VfxEffectSerializer.Load(path);
            Check(copy.Light.Enabled && copy.Layers.Any(layer => layer.RenderMode == VfxRenderMode.Mesh), preset + " mesh/light JSON roundtrip");
            var simulation = new VfxSimulation(copy);
            simulation.Play(Matrix4x4.Identity);
            for (int frame = 0; frame < 600; frame++) simulation.Advance(1f / 60, Matrix4x4.CreateTranslation(frame * .2f, 0, 0));
            Check(simulation.DroppedParticles == 0 && simulation.Capacity <= 96, preset + " bounded ten-second simulation without dropped particles");
        }
        var excessive = new VfxLayer { RenderMode = VfxRenderMode.Mesh, MaxParticles = 8192, MeshScale = new(float.NaN), MeshEmission = float.NaN };
        excessive.Validate();
        Check(excessive.MaxParticles == 64 && excessive.MeshScale == Vector3.One && float.IsFinite(excessive.MeshEmission), "Mesh geometry budget and nonfinite input guards");
        var flowing = new Material { VfxSurface = new(.9f, .2f, 8, 4) };
        var materialCopy = flowing.Clone();
        Check(MaterialRenderStateComparer.Instance.Equals(flowing, materialCopy), "Mesh surface-flow state survives material copies");
        materialCopy.VfxSurface = Vector4.Zero;
        Check(!MaterialRenderStateComparer.Instance.Equals(flowing, materialCopy), "Animated VFX materials do not batch with ordinary materials");
        using var database = new AssetDatabase(root, ["Assets"]);
        using var assets = new AssetManager(database);
        var codec = new ComponentSerializer(root, database, assets, loadGraphicsAssets: false);
        var impactAsset = database.Assets.Single(asset => asset.ProjectPath.EndsWith("FireboltImpact.bvfx"));
        var releaseAsset = database.Assets.Single(asset => asset.ProjectPath.EndsWith("FireboltRelease.bvfx"));
        var impact = new AssetReference(impactAsset.Guid, impactAsset.ProjectPath);
        var release = new AssetReference(releaseAsset.Guid, releaseAsset.ProjectPath);
        var projectile = new Projectile3D { ImpactEffect = impact, ImpactSize = 1.4f };
        var restored = (Projectile3D)codec.Deserialize(codec.Serialize(projectile)!)!;
        Check(restored.ImpactEffect == impact && restored.ImpactSize == 1.4f, "Projectile impact asset and scale serialization");
        var launcher = new ProjectileLauncher3D { ReleaseEffect = release, ReleaseSize = .6f };
        var restoredLauncher = (ProjectileLauncher3D)codec.Deserialize(codec.Serialize(launcher)!)!;
        Check(restoredLauncher.ReleaseEffect == release && restoredLauncher.ReleaseSize == .6f, "Launcher release asset and scale serialization");
        Guid registration = AnimationRuntimeAssets.Configure(assets);
        try { HitAndPool(impact, release); Render(output); }
        finally { AnimationRuntimeAssets.Clear(registration); }
    }

    private static void HitAndPool(AssetReference impact, AssetReference release)
    {
        var scene = new Scene("VFX contact regression");
        var wall = scene.CreateGameObject("Wall");
        wall.Transform.WorldPosition = new(0, 0, -2);
        wall.AddComponent(new BoxCollider3D { Size = new(4, 4, .2f) });
        var shooter = scene.CreateGameObject("Emitter");
        var launcher = shooter.AddComponent(new ProjectileLauncher3D { ReleaseEffect = release, ProjectileSpeed = 45, Damage = 10, FireCooldown = .2f });
        Guid spawn = RuntimeSpawnService.ConfigureBlueprintSpawner((target, _, position) =>
        {
            var obj = target.CreateGameObject("Visual blueprint");
            obj.Transform.WorldPosition = position;
            obj.AddComponent(new Projectile3D { Radius = .12f, ImpactEffect = impact, ImpactSize = 1.25f, LetEffectsFadeOnHit = true });
            obj.AddComponent(new VfxPlayer { Preset = VfxPreset.Firebolt, PlayOnStart = true });
            return obj;
        });
        try
        {
            launcher.ProjectileBlueprint = new AssetReference("Assets/Test.byteblueprint");
            var shot = launcher.Fire(-Vector3.UnitZ)!;
            var actual = shot.GetComponent<Projectile3D>()!;
            Check(actual.ImpactEffect == impact && actual.ImpactSize == 1.25f && actual.Radius == .12f && actual.Damage == 10 && actual.Velocity.Length() == 45,
                "Launcher transfers impact settings to the clean gameplay root without changing combat values");
            Check(scene.GameObjects.Count(obj => obj.Name == "VFX: FireboltRelease") == 1 && launcher.Fire(-Vector3.UnitZ) == null,
                "Release occurs exactly once per successful shot and respects cooldown");
            scene.LoadInternal();
            Time.Update(.05);
            scene.UpdateInternal();
            var hit = scene.GameObjects.Single(obj => obj.Name == "VFX: FireboltImpact").GetComponent<VfxPlayer>()!;
            var tail = scene.FindGameObject("Visual blueprint")!.GetComponent<VfxPlayer>()!;
            Check(tail.GameObject.Parent == null && !tail.IsEmitting && !tail.TryGetEffectLight(out _), "Flight trails detach and fade without retaining a stale light");
            Guid tailId = tail.GameObject.Id;
            for (int i = 0; i < 120 && scene.FindGameObject(tailId) != null; i++) tail.Advance(1f / 60);
            Check(scene.FindGameObject(tailId) == null, "Detached flight visuals clean up after particles drain");
            Check(hit.GameObject.Active && Vector3.Dot(hit.Transform.Up, Vector3.UnitZ) > .99f, "Impact survives projectile destruction and aligns with the wall normal");
            for (int i = 0; i < 120; i++) hit.Advance(1f / 60);
            Check(!hit.GameObject.Active && !hit.TryGetEffectLight(out _), "Completed impact returns to the pool and releases its light");
            var reused = VfxRuntime.Spawn(scene, impact, new(1, 0, 0), Quaternion.Identity);
            Check(ReferenceEquals(hit, reused), "Repeated impacts reuse the warm effect instance");
        }
        finally
        {
            RuntimeSpawnService.ClearBlueprintSpawner(spawn);
            foreach (var obj in scene.GameObjects.ToArray()) scene.DestroyGameObject(obj);
        }
    }

    private static void Render(string output)
    {
        using var window = new NativeWindow(new NativeWindowSettings { ClientSize = new(960, 540), StartVisible = false, API = ContextAPI.OpenGL, APIVersion = new(3, 3), Profile = ContextProfile.Core });
        window.Context.MakeCurrent();
        GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
        using var renderer = new Renderer2D(); renderer.Initialize(960, 540);
        using var renderer3D = new Renderer3D();
        using var framebuffer = new SceneFramebuffer();
        var scene = new Scene("Firebolt render regression");
        var camera = scene.CreateGameObject("Camera").AddComponent(new Camera3D());
        scene.CreateGameObject("Sun").AddComponent(new DirectionalLight { Intensity = .6f, AmbientIntensity = .15f });
        using var groundMesh = PrimitiveMesh.Create(PrimitiveMeshType.Cube);
        var ground = scene.CreateGameObject("Ground"); ground.Transform.LocalPosition = new(0, -.15f, 0); ground.Transform.LocalScale = new(12, .2f, 12);
        ground.AddComponent(new MeshRenderer { Mesh = groundMesh, Material = new Material { BaseColor = new(.22f, .25f, .3f, 1) } });
        var effect = VfxPresets.Create(VfxPreset.Firebolt);
        var player = scene.CreateGameObject("Firebolt").AddComponent(new VfxPlayer { PlayOnStart = false });
        player.Transform.WorldPosition = new(0, .7f, 0); player.SetDefinition(effect); player.Play(); player.Advance(.1f);
        void PointCamera(Vector3 position)
        {
            Matrix4x4.Invert(Matrix4x4.CreateLookAt(position, new(0, .7f, 0), Vector3.UnitY), out var inverse);
            camera.Transform.WorldPosition = position; camera.Transform.WorldRotation = Quaternion.CreateFromRotationMatrix(inverse);
        }
        void Draw(string name)
        {
            framebuffer.RenderGame(renderer, renderer3D, scene, EditorMode.Edit, 960, 540, 960, 540);
            GL.Finish();
            var pixels = new byte[960 * 540 * 4];
            GL.BindTexture(TextureTarget.Texture2D, (int)framebuffer.TextureId);
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            RoadmapUiVisualTests.Png(Path.Combine(output, name + ".png"), pixels, 960, 540);
            Check(player.RenderedParticles > 0 && GL.GetError() == ErrorCode.NoError, name + " has visible particles without GL errors");
        }
        PointCamera(new(0, 1.2f, 3)); Draw("head-on");
        var batches = (Array)typeof(VfxPlayer).GetField("_batches", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(player)!;
        var batch = batches.GetValue(0)!;
        var mesh = (Mesh)batch.GetType().GetField("Mesh")!.GetValue(batch)!;
        Check(mesh.LocalBounds.Size.X > .1f && mesh.LocalBounds.Size.Y > .1f && mesh.LocalBounds.Size.Z > .1f && mesh.VertexCount > 4, "Core has genuine three-dimensional geometry and finite bounds");
        PointCamera(new(3, 1.2f, 0)); Draw("side");
        var context = new RenderContext(renderer, renderer3D, scene, 960, 540, camera3D: camera);
        Check(context.CaptureRenderLighting3D(null).PointLightCount == 1, "Effect contributes a real scene light");
        player.Stop(true);
        Check(context.CaptureRenderLighting3D(null).PointLightCount == 0, "Stopping an effect immediately removes its illumination");
        player.Play();
        for (int i = 0; i < 19; i++)
        {
            var copy = scene.CreateGameObject("Stress firebolt").AddComponent(new VfxPlayer { PlayOnStart = false });
            copy.Transform.WorldPosition = new((i % 5 - 2) * .65f, .5f + (i / 5) * .35f, -i / 5);
            copy.SetDefinition(effect); copy.Play(); copy.Advance(.1f);
        }
        var lighting = context.CaptureRenderLighting3D(null);
        Check(lighting.PointLightCount == 4 && lighting.PointLights.All(light => !light.CastShadows), "Twenty simultaneous spells retain a four-light, shadow-free VFX cap");
        PointCamera(new(0, 2.4f, 6));
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 12; i++) framebuffer.RenderGame(renderer, renderer3D, scene, EditorMode.Edit, 960, 540, 960, 540);
        GL.Finish(); watch.Stop();
        Draw("twenty-spells");
        File.WriteAllText(Path.Combine(output, "render-timing.txt"), $"20 stationary spells, 960x540, 12 frames (includes warm-up): {watch.Elapsed.TotalMilliseconds:0.00} ms. Diagnostic only, no FPS improvement claim.\n");
        foreach (var obj in scene.GameObjects.ToArray()) scene.DestroyGameObject(obj);
    }
}

