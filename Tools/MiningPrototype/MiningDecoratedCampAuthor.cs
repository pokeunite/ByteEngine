using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningDecoratedCampAuthor
{
 static Vector3 V(JsonElement p)=>new(p[0].GetSingle(),p[1].GetSingle(),p[2].GetSingle());
 static bool Under(GameObject child,GameObject root){for(var p=child;p!=null;p=p.Parent)if(p==root)return true;return false;}
 // Rotate Blender's forward-facing camp to the game's negative-Z battlefield.
 static Vector3 Camp(Vector3 p)=>new(10-p.X,p.Z,p.Y);
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/decorated-camp-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  if(scene.FindGameObject("Approved decorated camp") is {} old)scene.DestroyGameObject(old);
  var root=scene.CreateGameObject("Approved decorated camp");var op=scene.GetComponentsSnapshot<MiningOperation3D>().Single();var ui=scene.GetComponentsSnapshot<MiningBusinessUi3D>().Single();
  using var document=JsonDocument.Parse(File.ReadAllText("artifacts/camp-decorations/layout.json"));using var assetsDocument=JsonDocument.Parse(File.ReadAllText("artifacts/camp-decorations/game-assets.json"));
  var definitions=assetsDocument.RootElement.EnumerateArray().ToDictionary(a=>a.GetProperty("asset").GetString()!);var cache=new Dictionary<string,(AssetReference Ref,ModelAsset Model)>();int instances=0,meshes=0,colliders=0;
  foreach(var item in document.RootElement.GetProperty("props").EnumerateArray())
  {
   string asset=item.GetProperty("asset").GetString()!;var instance=scene.CreateGameObject(asset+" decoration "+instances++);instance.SetParent(root,false);instance.Transform.LocalPosition=Camp(V(item.GetProperty("position")));instance.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI+V(item.GetProperty("rotation")).Z);var bs=V(item.GetProperty("scale"));instance.Transform.LocalScale=new(bs.X,bs.Z,bs.Y);
   if(!cache.TryGetValue(asset,out var source)){string path="Assets/Models/Environment/CampDecorations/"+asset+".glb";using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+path+".meta"));var reference=new AssetReference(meta.RootElement.GetProperty("guid").GetGuid(),path);source=(reference,runtime.Assets.LoadModel(reference));cache[asset]=source;}
   instance.AddComponent(new ModelHierarchyInstance{Model=source.Ref});var nodes=new Dictionary<string,GameObject>();
   foreach(var node in source.Model.Nodes){var obj=scene.CreateGameObject(node.Name);obj.SetParent(node.ParentKey is {} parent&&nodes.TryGetValue(parent,out var up)?up:instance,false);Matrix4x4.Decompose(node.LocalTransform,out var scale,out var rotation,out var position);obj.Transform.LocalPosition=position;obj.Transform.LocalRotation=rotation;obj.Transform.LocalScale=scale;nodes[node.Key]=obj;
    foreach(string key in node.MeshKeys){var mesh=source.Model.Meshes.First(m=>m.Key==key);var part=scene.CreateGameObject(mesh.Name);part.SetParent(obj,false);part.AddComponent(new MeshRenderer{MeshReference=new(source.Ref,key),MaterialReference=mesh.MaterialKey is {} material?new(source.Ref,material):null,Material=mesh.MaterialKey is {} mk?runtime.Assets.GetModelMaterial(source.Ref,mk):new(),CastShadows=!asset.StartsWith("15-")&&!asset.StartsWith("13-")&&!asset.StartsWith("14-")});if(asset.StartsWith("15-")){var paving=part.GetComponent<MeshRenderer>()!;paving.MaterialReference=null;paving.Material.Metallic=.05f;paving.Material.Roughness=.85f;}meshes++;}}
   if(asset.StartsWith("15-")||asset.StartsWith("13-")||asset.StartsWith("14-"))continue;
   instance.AddComponent(new MiningCampObject3D{DisplayName=asset[3..].Replace('-',' '),AllowRelocation=false,Clearance=.15f});
   void Box(Vector3 center,Vector3 size){instance.AddComponent(new BoxCollider3D{Center=center,Size=size});colliders++;}
   if(asset.StartsWith("05-")){foreach(float x in new[]{-1.75f,1.75f})foreach(float z in new[]{-1.3f,1.3f})Box(new(x,1.25f,z),new(.10f,2.5f,.10f));}
   else if(asset.StartsWith("01-"))Box(new(0,1.2f,0),new(.18f,2.4f,.18f));
   else if(asset.StartsWith("12-")){Box(new(0,.25f,0),new(.8f,.5f,.8f));Box(new(0,2.3f,0),new(.36f,4.1f,.36f));}
   else {var def=definitions[asset];var min=V(def.GetProperty("min"));var max=V(def.GetProperty("max"));var c=(min+max)/2;Box(new(c.X,c.Z,-c.Y),new(max.X-min.X,max.Z-min.Z,max.Y-min.Y));}
  }
  // One walkable foundation rather than hundreds of collider contacts for paving tiles.
  var foundation=scene.CreateGameObject("Camp paving collision");foundation.SetParent(root,false);foundation.Layer=20;foundation.Transform.WorldPosition=new(10,.035f,-3);foundation.AddComponent(new BoxCollider3D{Size=new(51.03f,.12f,32.4f)});colliders++;
  void PlaceMachine(GameObject machine,Vector3 desired,float width)
  {
   machine.Transform.LocalScale=Vector3.One;machine.Transform.WorldRotation=Quaternion.Identity;machine.Transform.WorldPosition=Vector3.Zero;var low=new Vector3(float.PositiveInfinity);var high=new Vector3(float.NegativeInfinity);
   foreach(var obj in scene.GameObjects.Where(o=>Under(o,machine)))if(obj.GetComponent<SkeletalMeshRenderer>() is {} skin){skin.ResolveRuntimeResources();if(skin.TryGetCurrentModelBounds(out var bounds)){for(int i=0;i<8;i++){var p=Vector3.Transform(new((i&1)==0?bounds.Minimum.X:bounds.Maximum.X,(i&2)==0?bounds.Minimum.Y:bounds.Maximum.Y,(i&4)==0?bounds.Minimum.Z:bounds.Maximum.Z),obj.Transform.WorldMatrix);low=Vector3.Min(low,p);high=Vector3.Max(high,p);}}}
   float factor=float.IsFinite(low.X)?width/Math.Max(.1f,high.X-low.X):1;machine.Transform.LocalScale=new(factor);machine.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI);var center=float.IsFinite(low.X)?new Vector3((low.X+high.X)/2,0,(low.Z+high.Z)/2):Vector3.Zero;machine.Transform.WorldPosition=desired-Vector3.Transform(center*factor,machine.Transform.WorldRotation);
  }
  PlaceMachine(scene.FindGameObject(op.DrillObjectId)!,Camp(new(-11,8,0)),8.5f);
  PlaceMachine(scene.FindGameObject(ui.RefineryMachineObjectId)!,Camp(new(-1,9,0)),5.5f);
  PlaceMachine(scene.FindGameObject(ui.AmmoMachineObjectId)!,Camp(new(12.5f,7,0)),3.6f);
  if(scene.FindGameObject("Company tent") is {} tent){PlaceMachine(tent,Camp(new(17,10,0)),5.5f);if(scene.FindGameObject("Company tent computer interaction") is {} interaction)interaction.Transform.LocalPosition=new(0,0,1.7f);}
  if(op.GameObject.GetComponent<MiningResearchStation3D>() is {} shop)shop.StationPosition=Camp(new(17,1,0));
  var player=scene.FindGameObject(op.PlayerObjectId)!;player.Transform.WorldPosition=new(10,.15f,-4);if(scene.FindGameObject("Camp arrival clearance") is {} arrival)arrival.Transform.WorldPosition=player.Transform.WorldPosition;
  // Props are dressing; progression still owns deployment of purchased defences and extra refineries.
  CorrectPresentation(scene);var region=scene.GetComponentsSnapshot<NavigationRegion3D>().Single();region.ObstacleLayerMask&=~(1<<20);region.Bake();runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");
  File.WriteAllText("artifacts/camp-decorations/installed-layout.json",JsonSerializer.Serialize(new{Instances=instances,StaticMeshes=meshes,ColliderCount=colliders,Backup=backup},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"DECORATED CAMP INSTALLED: {instances} instances, {meshes} static material meshes, {colliders} colliders. Backup: {backup}");
 }
 public static void CorrectPresentation(Scene scene)
 {
  var root=scene.FindGameObject("Approved decorated camp")!;
  foreach(var obj in scene.GameObjects.Where(o=>Under(o,root)))if(obj.GetComponent<MiningCampObject3D>() is {} marker)marker.AllowRelocation=false;
  var ui=scene.GetComponentsSnapshot<MiningBusinessUi3D>().Single();
  // Rotate around each model's visual centre, preserving its footprint and all child interaction anchors.
  foreach(var machine in new[]{scene.FindGameObject(ui.AmmoMachineObjectId),scene.FindGameObject("Company tent")})if(machine!=null){
   Vector3 Centre(){var low=new Vector3(float.PositiveInfinity);var high=new Vector3(float.NegativeInfinity);foreach(var obj in scene.GameObjects.Where(o=>Under(o,machine)))if(obj.GetComponent<SkeletalMeshRenderer>() is {} skin){skin.ResolveRuntimeResources();if(skin.TryGetCurrentModelBounds(out var box))for(int i=0;i<8;i++){var p=Vector3.Transform(new((i&1)==0?box.Minimum.X:box.Maximum.X,(i&2)==0?box.Minimum.Y:box.Maximum.Y,(i&4)==0?box.Minimum.Z:box.Maximum.Z),obj.Transform.WorldMatrix);low=Vector3.Min(low,p);high=Vector3.Max(high,p);}}return float.IsFinite(low.X)?(low+high)/2:machine.Transform.WorldPosition;}
   var centre=Centre();machine.Transform.WorldRotation=Quaternion.Identity;machine.Transform.WorldPosition+=centre-Centre();
  }
  var menu=scene.GetComponentsSnapshot<MiningPauseMenu3D>().Single();menu.MainMenuCameraOffset=new(-12,13,-23);menu.MainMenuLookOffset=new(-4,2,0);menu.MainMenuFieldOfView=60;
 }
 public static void FixPresentation(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/camp-presentation-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  CorrectPresentation(scene);scene.GetComponentsSnapshot<NavigationRegion3D>().Single().Bake();runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("Saved fixed decorations, forward-facing tent/ammo press and clearing-side menu camera. Backup: "+backup);
 }
}
