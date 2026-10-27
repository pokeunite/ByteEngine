using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningGroundSkyAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/ground-sky-sample-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  string folder="Assets/Environment/MiningSandbox";Directory.CreateDirectory(Program.Root+"/"+folder);
  AssetReference Copy(string file,string type){string target=folder+"/"+file;File.Copy("Docs/Concepts/AlienMining/GroundSkySample/"+file,Program.Root+"/"+target,true);return Ref(target,type);}
  AssetReference Ref(string path,string type){string meta=Program.Root+"/"+path+".meta";if(!File.Exists(meta))File.WriteAllText(meta,JsonSerializer.Serialize(new{guid=Guid.NewGuid(),type}));using var data=JsonDocument.Parse(File.ReadAllText(meta));return new(data.RootElement.GetProperty("guid").GetGuid(),path);}
  var texture=Copy("OrangeSand-BaseColor.png","Texture2D");var skyTexture=Copy("RingedPlanet-Sunset.png","Texture2D");var mesh=Copy("OrangeSand-Sample.glb","Model3D");
  string materialPath=folder+"/OrangeSand.bmat";MaterialAssetSerializer.Save(Program.Root+"/"+materialPath,new(){Name="Orange faceted sand",Standard=new(){BaseColor=Vector4.One,BaseColorTexture=texture,Metallic=0,Roughness=.96f,NormalStrength=0,DoubleSided=false}});var material=Ref(materialPath,"Material");
  foreach(var image in new[]{texture,skyTexture}){runtime.Database.SetTextureFilter(image.Guid,TextureFilter.Linear);}
  var sample=scene.FindGameObject("Orange sand ground sample")??scene.CreateGameObject("Orange sand ground sample");sample.Transform.WorldPosition=new(10,0,-4);sample.Layer=20;
  if(sample.GetComponent<SkeletalMeshRenderer>() is {} old)sample.RemoveComponent(old);
  var renderer=sample.GetComponent<MeshRenderer>()??sample.AddComponent(new MeshRenderer());renderer.MeshReference=new(mesh,runtime.Assets.LoadModel(mesh).Meshes.First().Key);renderer.MaterialAssetReference=material;renderer.UsePrimitive=false;
  var sky=scene.GameObjects.SelectMany(o=>o.Components).OfType<SkyEnvironment>().First();sky.SkyMode=SkyMode3D.EnvironmentMap;sky.EnvironmentMapReference=skyTexture;sky.EnvironmentRotationDegrees=90;sky.EnvironmentLightingEnabled=true;sky.EnvironmentIntensity=.85f;sky.Exposure=1.05f;sky.OverrideAmbient=true;sky.AmbientIntensity=.20f;sky.AmbientOcclusion=.40f;sky.OcclusionRadius=.55f;sky.Bloom=.04f;sky.BloomThreshold=1.25f;sky.Saturation=1;sky.Contrast=1.03f;sky.Warmth=0;sky.FogEnabled=true;sky.FogMode=FogMode3D.Linear;sky.FogColor=new(.55f,.34f,.39f);sky.FogStartDistance=65;sky.FogEndDistance=180;sky.FogMaxOpacity=.45f;
  var lights=scene.GameObjects.SelectMany(o=>o.Components).OfType<DirectionalLight>().ToArray();var sun=lights.First();foreach(var other in lights.Skip(1))other.Enabled=false;sun.Enabled=true;sun.Color=new(1,.88f,.74f);sun.Intensity=1.85f;sun.AmbientIntensity=.08f;sun.CastShadows=true;sun.ShadowResolution=2048;sun.ShadowDistance=55;sun.ShadowStrength=.78f;sun.ShadowSoftness=1.25f;var towardSun=Vector3.Normalize(new Vector3(.85f,.5f,-.35f));Matrix4x4.Invert(Matrix4x4.CreateLookAt(Vector3.Zero,towardSun,Vector3.UnitY),out var rotation);sun.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(rotation);
  var fillObject=scene.FindGameObject("Alien sky soft fill")??scene.CreateGameObject("Alien sky soft fill");var fill=fillObject.GetComponent<DirectionalLight>()??fillObject.AddComponent(new DirectionalLight());fill.Enabled=true;fill.Color=new(.72f,.72f,1);fill.Intensity=.50f;fill.AmbientIntensity=0;fill.CastShadows=false;Matrix4x4.Invert(Matrix4x4.CreateLookAt(Vector3.Zero,Vector3.Normalize(new Vector3(-.8f,.45f,.4f)),Vector3.UnitY),out var fillPose);fill.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(fillPose);
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("GROUND / SKY SAMPLE INSTALLED: 12 x 14 m beside drill, reusable .bmat, ringed-planet PNG environment, warm sun and shadows. Backup "+backup);
 }
}
