using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Tests;

internal static class GraphicsQualityTests
{
    public static void Run(string root)
    {
        int checks=0;
        void Check(bool ok,string message) { checks++; if(!ok) throw new Exception(message); }
        var env=new SkyEnvironment();
        Check(env.CaptureLook()==GraphicsLook.Default,"Legacy defaults changed.");
        foreach(var quality in Enum.GetValues<GraphicsQuality>())
        {
            env.Quality=quality;
            var look=env.CaptureLook();
            Check(look.ShadowResolutionCap<=4096 && look.PointShadowLimit<=2,"Unbounded shadows.");
            Check(look.AoSamples<=12,"Unbounded sample count.");
        }
        env.Quality=GraphicsQuality.Fast;
        Check(env.AmbientOcclusion==0 && env.Bloom==0 && env.CaptureLook().PointShadowLimit==0,"Fast must disable extra passes and point shadows.");
        env.Quality=GraphicsQuality.Balanced;
        Check(env.CaptureLook().PointShadowLimit==1 && env.CaptureLook().ShadowResolutionCap==2048,"Balanced shadow budget.");
        env.Bloom=.21f; env.Exposure=1.7f; env.Lighting=LightingPreset.Overcast; env.Exposure=1.7f;
        var saved=GraphicsLookSerialization.Save(env);
        var restored=GraphicsLookSerialization.Load(new SkyEnvironment { Exposure=1.7f },saved);
        Check(restored.CaptureLook()==env.CaptureLook() && restored.Exposure==1.7f,"Saved adjustments overwritten by presets.");
        using(var db=new AssetDatabase(root,new[] { "Assets" }))
        using(var assets=new AssetManager(db))
        {
            var codec=new ComponentSerializer(root,db,assets);
            RendererSerializationRegistrar.Register(codec);
            foreach(bool exposureCodec in new[] { false,true })
            {
                if(exposureCodec) SkyEnvironmentExposureSerialization.Register(codec);
                var data=codec.Serialize(env)!;
                var copy=(SkyEnvironment)codec.Deserialize(data)!;
                Check(copy.CaptureLook()==env.CaptureLook(),"Graphics look failed codec round-trip.");
                data.Properties.Remove("graphicsLook");
                copy=(SkyEnvironment)codec.Deserialize(data)!;
                Check(copy.CaptureLook()==GraphicsLook.Default,"Legacy serialized scene changed defaults.");
            }
        }
        env.Bloom=float.NaN; env.OcclusionRadius=float.PositiveInfinity; env.Contrast=9; env.Warmth=-9;
        Check(env.Bloom==0 && env.OcclusionRadius==.5f && env.Contrast==1.5f && env.Warmth==-1,"Invalid inputs were not bounded.");
        var scene=new Scene("lighting presets"); env=scene.CreateGameObject("Sky").AddComponent(new SkyEnvironment());
        foreach(var preset in Enum.GetValues<LightingPreset>().Where(p=>p!=LightingPreset.Custom))
        {
            env.Lighting=preset; env.ApplyLightingToScene();
            var suns=scene.GameObjects.SelectMany(o=>o.Components).OfType<DirectionalLight>().ToArray();
            Check(suns.Length==1,"Preset duplicated sunlight.");
            Check(suns[0].Direction.Y<0,"Sun must cast light down toward the ground.");
        }
        var sun=scene.GameObjects.SelectMany(o=>o.Components).OfType<DirectionalLight>().Single();
        sun.ShadowResolution=4096;
        for(int i=0;i<3;i++) scene.CreateGameObject("Point "+i).AddComponent(new PointLight { CastShadows=true,ShadowResolution=2048 });
        using(var renderer2D=new Renderer2D())
        using(var renderer3D=new Renderer3D())
        {
            var context=new RenderContext(renderer2D,renderer3D,scene,128,128,prepareEnvironmentLighting3D:false,renderShadows3D:false);
            foreach(var q in new[] { GraphicsQuality.Fast,GraphicsQuality.Balanced,GraphicsQuality.High })
            {
                env.Quality=q; var lighting=context.CaptureRenderLighting3D(null);
                Check(lighting.FindShadowPointLightIndices(Vector3.Zero).Count==env.CaptureLook().PointShadowLimit,"Actual snapshot ignores point shadow budget.");
                Check(lighting.DirectionalLights[0].ShadowResolution<=env.CaptureLook().ShadowResolutionCap,"Actual snapshot ignores shadow resolution cap.");
                Check(sun.ShadowResolution==4096,"Quality overwrote authored light resolution.");
            }
        }
        var p=new MaterialParameters(); Check(GraphicsAssetAudit.Material(p).Count==0,"Default material should be clean.");
        p.PbrMapMode=MaterialPbrMapMode.Packed;
        Check(GraphicsAssetAudit.Material(p).Any(x=>x.Contains("without")),"Missing packed map warning.");
        p.BaseColorTexture=new AssetReference("Assets/missing.png");
        Check(GraphicsAssetAudit.Material(p,_=>false).Any(x=>x.Contains("missing")),"Missing texture warning.");
        var model=new ModelAsset(new ImportedModel { Name="Audit",Meshes=new() { new ImportedMesh { Name="Bad mesh",Vertices=new float[24] } } });
        Check(GraphicsAssetAudit.Model(model).Contains("missing normals=3"),"Model normal audit.");
        Console.WriteLine($"Graphics authoring: {checks} checks passed.");
    }
}
