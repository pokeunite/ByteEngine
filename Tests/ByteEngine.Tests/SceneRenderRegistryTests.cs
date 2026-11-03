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
        var region0=early.AddComponent(new ByteEngine.Core.Navigation.NavigationRegion3D());
        var region1=later.AddComponent(new ByteEngine.Core.Navigation.NavigationRegion3D());
        Check(ReferenceEquals(scene.FindNavigationRegion(Guid.Empty),region0),"Navigation region scene-order priority");
        region0.Enabled=false;Check(ReferenceEquals(scene.FindNavigationRegion(Guid.Empty),region1),"Navigation region live enabled state");
        early.RemoveComponent(region0);Check(ReferenceEquals(scene.FindNavigationRegion(Guid.Empty),region1),"Navigation region removal invalidates registry");
        Check(ReferenceEquals(scene.FindNavigationRegion(later.Id),region1),"Explicit navigation region reference preserved");
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
        for(int i=0;i<100;i++){var o=scene.CreateGameObject("Lamp "+i);o.Transform.WorldPosition=new(i%12,0,i/12);o.AddComponent(new PointLight{Intensity=i+1,CastShadows=true});}
        var snapshot=context.CaptureRenderLighting3D(null);
        var expected=scene.PointLights.OrderBy(l=>l.Position.LengthSquared()).Take(8).Select(l=>l.Intensity).ToArray();
        Check(snapshot.PointLights.Select(l=>l.Intensity).SequenceEqual(expected),"Nearest eight lights and stable ties");
        scene.PointLights[0].Intensity=500;
        Check(snapshot.PointLights[0].Intensity==1,"Lighting snapshot remains immutable after scene edit");
        var expectedShadows=snapshot.PointLights.Select((l,i)=>(l,i)).Where(p=>p.l.CastShadows).OrderBy(p=>p.l.Position.Length()-p.l.Range).ThenByDescending(p=>p.l.Intensity).ThenBy(p=>p.i).Take(2).Select(p=>p.i);
        Check(snapshot.FindShadowPointLightIndices(System.Numerics.Vector3.Zero).SequenceEqual(expectedShadows),"Point shadow selection preserves influence/intensity/index ordering");
        for(int i=0;i<50;i++)context.CaptureRenderLighting3D(null);
        long start=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++)context.CaptureRenderLighting3D(null);
        long bytes=(GC.GetAllocatedBytesForCurrentThread()-start)/1000;
        Check(bytes<2500,"Lighting capture allocation budget exceeded: "+bytes);
        Console.WriteLine("PASS lighting capture: "+bytes+" bytes/pass with 100 point lights; snapshot isolation and selection preserved");
        Console.WriteLine("PASS render registry: live properties, enabled/parent state, ordering and membership changes");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
