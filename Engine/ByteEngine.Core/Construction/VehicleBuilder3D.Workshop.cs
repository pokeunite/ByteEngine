using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Assets;
namespace ByteEngine.Core.Construction;
public sealed partial class VehicleBuilder3D
{
    private void CreateWorkshopAtmosphere()
    {
        var sky=GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).OfType<SkyEnvironment>().FirstOrDefault() ?? Own("Scrapyard atmosphere").AddComponent(new SkyEnvironment());
        sky.SkyMode=SkyMode3D.Procedural;
        sky.ZenithColor=new(.22f,.31f,.32f);sky.HorizonColor=new(.47f,.56f,.50f);sky.GroundColor=new(.27f,.33f,.28f);
        sky.Exposure=1.08f;sky.AmbientIntensity=.45f;sky.EnvironmentIntensity=.65f;sky.FogEnabled=true;sky.FogColor=new(.30f,.31f,.29f);sky.FogStartDistance=22;sky.FogEndDistance=75;sky.FogMaxOpacity=.55f;
        foreach(var light in GameObject.Scene.GameObjects.SelectMany(o=>o.Components).OfType<DirectionalLight>())
        {light.Color=new(1,.94f,.82f);light.Transform.WorldRotation=AlignNormals(Vector3.UnitZ,Vector3.Normalize(new Vector3(-.4f,-1,-.6f)));light.Intensity=1.8f;light.AmbientIntensity=.45f;light.CastShadows=true;light.ShadowSoftness=2;light.ShadowResolution=2048;light.ShadowBias=.006f;light.ShadowDistance=35;}
        if(FreeBuilding)
        {
            var floorMaterial=new Material {BaseColor=new(.75f,.70f,.59f,1),MainTexture=Assets!.LoadTexture(new AssetReference("Assets/WorkshopMaterials/concrete-diffuse.jpg")),NormalTexture=Assets.LoadTexture(new AssetReference("Assets/WorkshopMaterials/concrete-normal.jpg")),PackedPbrTexture=Assets.LoadTexture(new AssetReference("Assets/WorkshopMaterials/concrete-arm.jpg")),PbrMapMode=MaterialPbrMapMode.Packed,DecodeColorTexturesSrgb=true,UvTiling=new(12),NormalStrength=.55f};
            foreach(var obj in _owned.Where(o=>o.Name=="Test yard floor"))obj.GetComponent<MeshRenderer>()!.Material=floorMaterial;
            var padMaterial=new Material {BaseColor=new(.8f,.76f,.67f,1),MainTexture=floorMaterial.MainTexture,NormalTexture=floorMaterial.NormalTexture,PackedPbrTexture=floorMaterial.PackedPbrTexture,PbrMapMode=MaterialPbrMapMode.Packed,DecodeColorTexturesSrgb=true,UvTiling=new(1.5f),NormalStrength=.45f};
            foreach(var obj in _owned.Where(o=>o.Name=="Build pad"))obj.GetComponent<MeshRenderer>()!.Material=padMaterial;
        }
        for(int i=0;i<(FreeBuilding?0:2);i++)
        {
            var bridge=Own("Chassis axle bridge");bridge.SetParent(GameObject,false);bridge.Transform.LocalScale=new(1.93f,.07f,.19f);
            bridge.AddComponent(new MeshRenderer {UsePrimitive=true,Material=new Material {BaseColor=new(.18f,.23f,.20f,1),Metallic=.65f,Roughness=.55f}});_axleBridges.Add(bridge);
        }
        UpdateAxleBridges();
        var cabReference=new AssetReference(PartsDirectory+(PartsDirectory.Contains("ContraptionParts")?"/goblin_single_wooden_block.glb":"/scrap_cab_shell.glb"));
        var cab=Assets!.LoadModel(cabReference);
        var wood=cab.Materials.First(m=>m.Name.Contains("wood"));
        var woodMaterial=Assets.GetModelMaterial(cabReference,wood.Key);
        for(int side=-1;side<=1;side+=2)
        {
            Vector3 p=new(side*7.5f,0,-2.6f);
            Box("Goblin banner pole",p+new Vector3(0,1.7f,0),new(.065f,3.4f,.065f),new(.20f,.23f,.19f,1));
            Box("Goblin faction banner",p+new Vector3(.5f,2.75f,0),new(1,.75f,.025f),side<0 ? new(.18f,.36f,.14f,1) : new(.52f,.14f,.09f,1));
            Box("Banner brass stripe",p+new Vector3(.5f,2.98f,-.019f),new(.82f,.055f,.02f),new(.64f,.44f,.18f,1));
            Box("Workshop workbench",p+new Vector3(0,.72f,-1.2f),new(2.2f,.16f,.9f),new(.23f,.19f,.12f,1));
            for(float x=-.8f;x<=.8f;x+=1.6f) Box("Workbench legs",p+new Vector3(x,.34f,-1.2f),new(.13f,.7f,.65f),new(.18f,.22f,.17f,1));
            var stand=Own("Spare parts stand");stand.Transform.WorldPosition=p+new Vector3(0,.87f,-1.2f);
            CreateModelVisual(GameObject.Scene,Assets!,PartsDirectory+(PartsDirectory.Contains("ContraptionParts")?(side<0?"/goblin_wheel.glb":"/goblin_cannon.glb"):(side<0 ? "/scrap_wheel_small.glb" : "/scrap_engine_block.glb")),stand,"Spare refined part");
        }
        foreach(var obj in _owned.Where(o=>o.Name.Contains("workbench",StringComparison.OrdinalIgnoreCase) || o.Name=="Scrap crate"))
            if(obj.GetComponent<MeshRenderer>() is {} renderer) renderer.Material=woodMaterial;
        for(int i=0;i<18;i++)
        {
            float angle=i*MathF.Tau/18;float distance=7.8f+(i%3)*.7f;
            var rock=Own("Scrapyard stone");rock.Transform.WorldPosition=new(MathF.Sin(angle)*distance,.10f,MathF.Cos(angle)*distance);
            rock.Transform.LocalScale=new(.3f+(i%4)*.13f,.18f,.38f);
            rock.AddComponent(new MeshRenderer {UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,Material=new Material {BaseColor=new(.27f,.31f,.26f,1),Roughness=.95f}});
        }
    }
    private void UpdateAxleBridges()
    {
        for(int i=0;i<_axleBridges.Count;i++) _axleBridges[i].Transform.LocalPosition=Layout.ActiveMounts[4+i].Position;
    }
}

