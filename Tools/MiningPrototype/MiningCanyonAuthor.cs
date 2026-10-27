using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningCanyonAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/alien-canyon-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  if(scene.FindGameObject("Alien canyon environment") is {} old)scene.DestroyGameObject(old);
  var root=scene.CreateGameObject("Alien canyon environment");
  foreach(var o in scene.GameObjects.Where(o=>new[]{"Planet rock","Cargo cover","West perimeter","East perimeter","North perimeter","South perimeter"}.Contains(o.Name)))o.Active=false;
  var floor=scene.FindGameObject("Alien worksite floor")!;floor.Transform.WorldPosition=new(10,-.15f,-54);floor.Transform.LocalScale=new(84,.3f,168);
  var ground=scene.FindGameObject("Ground")!;ground.Transform.WorldPosition=new(10,-.55f,-45);ground.Transform.LocalScale=new(600,1,800);
  var region=scene.GameObjects.SelectMany(o=>o.Components).OfType<NavigationRegion3D>().Single();region.Transform.WorldPosition=new(-32,0,-138);region.CellSize=.75f;region.Width=112;region.Height=224;region.EndpointSearchRadius=5;
  var layout=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningCampLayout3D>().Single();layout.BuildMinimum=new(-25,-30);layout.BuildMaximum=new(45,24);layout.CameraWidth=100;
  foreach(var camera in scene.GameObjects.SelectMany(o=>o.Components).OfType<Camera3D>())if(camera.ActiveGameCamera)camera.FarClip=400;
  // Match atmospheric depth to the enlarged site; preserve the approved sky texture and lighting.
  var sky=scene.GameObjects.SelectMany(o=>o.Components).OfType<SkyEnvironment>().First();sky.FogStartDistance=100;sky.FogEndDistance=330;sky.FogMaxOpacity=.28f;
  using var doc=JsonDocument.Parse(File.ReadAllText("Docs/Concepts/AlienMining/RockKit/level-layout.json"));var plan=doc.RootElement;
  Vector3 V(JsonElement p)=>new(p[0].GetSingle(),p[1].GetSingle(),p[2].GetSingle());
  var spawn=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningSpawnPoint3D>().ToArray();int n=0;foreach(var p in plan.GetProperty("spawnPoints").EnumerateArray()){spawn[n].Transform.WorldPosition=V(p);spawn[n].GameObject.Name="Far front approach lane "+(++n);}
  using var modelDoc=JsonDocument.Parse(File.ReadAllText("artifacts/mining-rock-kit/models.json"));var manifest=modelDoc.RootElement.GetProperty("assets").EnumerateArray().ToDictionary(a=>a.GetProperty("id").GetString()!);
  var groups=new Dictionary<string,GameObject>();var cache=new Dictionary<string,(AssetReference Reference,ModelAsset Model)>();int meshCount=0,colliderCount=0;
  foreach(var item in plan.GetProperty("items").EnumerateArray())
  {
   string id=item.GetProperty("id").GetString()!,group=item.GetProperty("group").GetString()!;
   if(!groups.TryGetValue(group,out var parent)){parent=scene.CreateGameObject(group);parent.SetParent(root,false);groups[group]=parent;}
   var instance=scene.CreateGameObject(item.GetProperty("name").GetString()!);instance.SetParent(parent,false);instance.Transform.LocalPosition=new(item.GetProperty("x").GetSingle(),item.GetProperty("y").GetSingle(),item.GetProperty("z").GetSingle());instance.Transform.EulerAngles=new(0,item.GetProperty("yaw").GetSingle(),0);instance.Transform.LocalScale=V(item.GetProperty("scale"));
   if(!cache.TryGetValue(id,out var source)){string path="Assets/Models/Environment/MiningRockKit/"+id+".glb";using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+path+".meta"));var reference=new AssetReference(meta.RootElement.GetProperty("guid").GetGuid(),path);source=(reference,runtime.Assets.LoadModel(reference));cache[id]=source;}
   instance.AddComponent(new ModelHierarchyInstance{Model=source.Reference});var nodes=new Dictionary<string,GameObject>();
   foreach(var node in source.Model.Nodes)
   {
    var obj=scene.CreateGameObject(node.Name);obj.SetParent(node.ParentKey is {} parentKey&&nodes.TryGetValue(parentKey,out var up)?up:instance,false);Matrix4x4.Decompose(node.LocalTransform,out var scale,out var rotation,out var pos);obj.Transform.LocalPosition=pos;obj.Transform.LocalRotation=rotation;obj.Transform.LocalScale=scale;nodes[node.Key]=obj;
    foreach(string key in node.MeshKeys)
    {
     var mesh=source.Model.Meshes.First(m=>m.Key==key);var child=scene.CreateGameObject(mesh.Name);child.SetParent(obj,false);child.AddComponent(new MeshRenderer{MeshReference=new(source.Reference,key),MaterialReference=mesh.MaterialKey is {} material?new(source.Reference,material):null,Material=mesh.MaterialKey is {} sourceMaterial?runtime.Assets.GetModelMaterial(source.Reference,sourceMaterial):new(),CastShadows=group!="Distant landforms"&&!id.StartsWith('D')&&id!="R11"&&id!="R12"});meshCount++;
    }
   }
   string collision=item.GetProperty("collision").GetString()!;
   void Box(Vector3 min,Vector3 max,string name)
   {
    // Blender XYZ -> glTF X,Z,-Y. Keep the proxy slightly inside the visible rock.
    var lo=new Vector3(min.X,min.Z,-max.Y);var hi=new Vector3(max.X,max.Z,-min.Y);var size=hi-lo;size.X=Math.Max(.1f,size.X-.25f);size.Z=Math.Max(.1f,size.Z-.25f);var body=scene.CreateGameObject(name);body.SetParent(instance,false);body.AddComponent(new BoxCollider3D{Center=(lo+hi)*.5f,Size=size});colliderCount++;
   }
   var def=manifest[id];if(collision=="wall")Box(V(def.GetProperty("min")),V(def.GetProperty("max")),id+" solid cliff collision");
   else if(collision=="parts")foreach(var part in def.GetProperty("parts").EnumerateArray())Box(V(part.GetProperty("min")),V(part.GetProperty("max")),part.GetProperty("name").GetString()+" collision");
  }
  foreach(string type in new[]{"Raider","Hunter"})
  {
   string path=Program.Root+"/Assets/Blueprints/Mining/Alien "+type+".byteblueprint";File.Copy(path,backup+"/Alien "+type+".byteblueprint");var data=JsonNode.Parse(File.ReadAllText(path))!;foreach(var c in data["root"]!["components"]!.AsArray())
   {if(c!["type"]!.GetValue<string>()=="AIBehavior3D")c["properties"]!["detectionRange"]=180;if(c["type"]!.GetValue<string>()=="NavigationAgent3D")c["properties"]!["maximumVisited"]=20000;}
   File.WriteAllText(path,data.ToJsonString(new(){WriteIndented=true}));
  }
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");MiningFloorAuthor.Build(scene,runtime);
  Console.WriteLine($"CANYON INSTALLED: {plan.GetProperty("items").GetArrayLength()} models, {meshCount} static mesh parts, {colliderCount} collision proxies; 84x168m floor, wider camp layout, far-front spawns. Backup {backup}");
 }
}
