using System.Numerics;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using System.Text.Json.Nodes;
namespace DuneCompany;
/// <summary>Race-kit scenery uses one shared atlas and material, with separately authored collision.</summary>
public sealed class DuneRaceProp3D:Component
{
 public string ModelPath{get;set;}="";
 internal AssetManager? Assets;
 static readonly ConditionalWeakTable<AssetManager,Material> Materials=new();
 static readonly ConditionalWeakTable<AssetManager,Material> Rocks=new();
 static readonly ConditionalWeakTable<AssetManager,Material> Gravel=new(),Asphalt=new();
 string? _loadedPath;Mesh[] _high=[],_low=[];Material? _material;bool _road,_cliff;
 readonly Dictionary<string,MeshLodGroup> _views=[];
 public int RenderMeshCount=>_high.Length;
 protected override void OnStart()=>Load();
 void Load(){
  if(Assets==null||ModelPath.Length==0)return;var reference=new AssetReference(ModelPath);var model=Assets.LoadModel(reference);
  var material=Materials.GetValue(Assets,a=>new Material{MainTexture=a.LoadTexture(new("Assets/RaceKit/race-kit-albedo.png")),NormalTexture=a.LoadTexture(new("Assets/RaceKit/race-kit-normal.png")),PackedPbrTexture=a.LoadTexture(new("Assets/RaceKit/race-kit-pbr.png")),PbrMapMode=MaterialPbrMapMode.Packed,Metallic=1,Roughness=1,NormalStrength=.65f,BaseColor=Vector4.One,CullMode=CullMode3D.None,DecodeColorTexturesSrgb=true});
  if(ModelPath.Contains("/cliff-"))material=Rocks.GetValue(Assets,a=>new Material{MainTexture=a.LoadTexture(new("Assets/ContractLevels/rock_09_diff_2k.jpg")),Roughness=.93f,BaseColor=Vector4.One,CullMode=CullMode3D.None,DecodeColorTexturesSrgb=true});
  bool road=ModelPath.Contains("-road.obj");if(road){bool asphalt=ModelPath.Contains("asphalt");material=(asphalt?Asphalt:Gravel).GetValue(Assets,a=>new Material{MainTexture=a.LoadTexture(new(asphalt?"Assets/RaceKit/road-asphalt.png":"Assets/RaceKit/road-gravel.png")),Roughness=.95f,BaseColor=Vector4.One,CullMode=CullMode3D.None,DecodeColorTexturesSrgb=true});}
  var lowReference=new AssetReference(ModelPath.Replace(".obj","-lod.obj"));var low=Assets.LoadModel(lowReference);
  _high=model.Meshes.Select(m=>Assets.GetModelMesh(reference,m.Key)).ToArray();_low=low.Meshes.Select(m=>Assets.GetModelMesh(lowReference,m.Key)).ToArray();_material=material;_road=road;_cliff=ModelPath.Contains("/cliff-");_loadedPath=ModelPath;_views.Clear();
 }
 protected override void OnRender(RenderContext context){
  // Share meshes and materials; select detail without spawning six extra objects
  // per prop. The same path previews in Edit and renders in Play.
  if(Assets==null||ModelPath.Length==0||!context.Has3DCamera||context.RenderWorld.View is not {} view)return;if(_loadedPath!=ModelPath)Load();
  if(!_views.TryGetValue(context.ViewportName,out var lod)){lod=new(){Distances=new(_cliff?65:30,_cliff?180:85),CullDistance=_road?2000:_cliff?1000:360};_views[context.ViewportName]=lod;}
  float distance=Vector3.Distance(Transform.WorldPosition,view.CameraPosition);int level=lod.SelectStableLevel(distance);if(level<0)return;
  foreach(var mesh in level==0?_high:_low)context.RenderWorld.Submit(mesh,_material!,Transform.RenderMatrix,castShadows:!_road&&level<2);
 }
}
internal sealed class RacePropCodec:IComponentCodec
{
 public string TypeName=>"bytebard.dunecompany.DuneRaceProp3D";public Type ComponentType=>typeof(DuneRaceProp3D);
 public ComponentData Serialize(Component c,ComponentSerializationContext x)=>new(){Type=TypeName,Properties=new JsonObject{["modelPath"]=((DuneRaceProp3D)c).ModelPath}};
 public Component Deserialize(ComponentData d,ComponentSerializationContext x)=>new DuneRaceProp3D{ModelPath=d.Properties["modelPath"]?.GetValue<string>()??"",Assets=x.Assets};
}
