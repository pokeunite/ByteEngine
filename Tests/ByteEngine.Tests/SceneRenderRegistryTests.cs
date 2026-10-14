using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Tests;

internal static class SceneRenderRegistryTests
{
    internal static void Run()
    {
        var scene = new Scene("Live render membership");
        var early = scene.CreateGameObject("Early");
        var later = scene.CreateGameObject("Later");
        var parent = scene.CreateGameObject("Parent");
        using var renderer2D = new Renderer2D();
        using var renderer3D = new Renderer3D();
        var context = new RenderContext(renderer2D, renderer3D, scene, 128, 128);
        var sky = later.AddComponent(new SkyEnvironment { SkyIntensity = .3f });
        Check(context.CaptureRenderEnvironment3D().SkyIntensity == .3f, "Initial environment");
        var preferred = early.AddComponent(new SkyEnvironment { SkyIntensity = .8f });
        Check(context.CaptureRenderEnvironment3D().SkyIntensity == .8f, "Late-added earlier environment priority");
        preferred.Enabled = false;
        Check(context.CaptureRenderEnvironment3D().SkyIntensity == .3f, "Live enabled state");
        later.SetParent(parent, false); parent.Active = false;
        Check(context.CaptureRenderEnvironment3D().SkyIntensity == RenderEnvironment3D.Default.SkyIntensity, "Parent active state");
        parent.Active = true;
        Check(context.CaptureRenderEnvironment3D().SkyIntensity == .3f, "Parent reactivation");
        later.RemoveComponent(sky);
        Check(context.CaptureRenderEnvironment3D().SkyIntensity == RenderEnvironment3D.Default.SkyIntensity, "Removed environment");
        var sun = early.AddComponent(new DirectionalLight { Intensity = .45f });
        var point = later.AddComponent(new PointLight { Intensity = .6f });
        Check(context.CaptureRenderLighting3D(null).PointLightCount == 1, "Added point light");
        sun.Intensity = .7f;
        Check(context.CaptureRenderLighting3D(null).DirectionalLights[0].Intensity == .7f, "Live light properties");
        point.Enabled = false;
        Check(context.CaptureRenderLighting3D(null).PointLightCount == 0, "Disabled point light");
        later.RemoveComponent(point);
        Check(scene.PointLights.Count == 0, "Removed light registry entry");
        Console.WriteLine("PASS render registry: live properties, enabled/parent state, ordering and membership changes");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
