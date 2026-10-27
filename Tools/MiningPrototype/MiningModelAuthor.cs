using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningModelAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  var op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();
  string backup=Program.Root+"/.byteengine/Backups/mining-models-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var world=scene.FindGameObject("Mining first test")!;var drill=scene.FindGameObject(op.DrillObjectId)!;
  foreach(var o in scene.GameObjects.Where(o=>Under(o,drill)).ToArray()){if(o.GetComponent<MeshRenderer>() is {} mesh)mesh.Visible=false;if(o.GetComponent<MiningRigVisual3D>() is {} spin)spin.Enabled=false;}
  AssetReference Asset(string name){string p="Assets/Models/Mining/"+name+".glb";using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+p+".meta"));return new(meta.RootElement.GetProperty("guid").GetGuid(),p);}
  GameObject Model(string name,string asset,Vector3 position,MiningMachineKind kind,GameObject? parent=null)
  {if(scene.FindGameObject(name) is {} old)scene.DestroyGameObject(old);var o=scene.CreateGameObject(name);o.SetParent(parent??world,false);o.Transform.LocalPosition=position;o.AddComponent(new SkeletalMeshRenderer{Model=Asset(asset),DefaultAnimation="Operate",PlayOnStart=false,Loop=true,AnimationUpdateInterval=1f/30});o.AddComponent(new MiningMachineAnimation3D{OperationObjectId=op.GameObject.Id,WorkshopObjectId=op.GameObject.Id,Machine=kind});return o;}
  Model("Animated drill model","MiningDrill",Vector3.Zero,MiningMachineKind.Drill,drill);
  var pressOld=scene.FindGameObject("Ammo press body")!;var refineryOld=scene.FindGameObject("Refinery body")!;var terminalOld=scene.FindGameObject("Galactic market console")!;
  Vector3 Ground(GameObject o)=>new(o.Transform.WorldPosition.X,0,o.Transform.WorldPosition.Z);
  var press=Model("Ammo press station","AmmoPress",Ground(pressOld),MiningMachineKind.AmmoPress);
  var refinery=Model("Refinery station","Refinery",Ground(refineryOld),MiningMachineKind.Refinery);
  var tent=Model("Company tent","CompanyTent",Ground(terminalOld),MiningMachineKind.Tent);
  void Wall(string name,Vector3 centre,Vector3 size){var wall=scene.CreateGameObject(name);wall.SetParent(tent,false);wall.Transform.LocalPosition=centre;wall.AddComponent(new BoxCollider3D{Size=size});}
  Wall("Tent left collision",new(-1.85f,1,0),new(.18f,2,3.25f));Wall("Tent right collision",new(1.85f,1,0),new(.18f,2,3.25f));Wall("Tent rear collision",new(0,1,1.55f),new(3.7f,2,.18f));
  // A small interaction point at the open front, not the remote company object.
  var anchor=scene.CreateGameObject("Company tent computer interaction");anchor.SetParent(tent,false);anchor.Transform.LocalPosition=new(0,0,-1.85f);
  foreach(string name in new[]{"Ammo press body","Ammo press hopper","Refinery body","Refinery stack","Galactic market console","Market screen"})if(scene.FindGameObject(name)?.GetComponent<MeshRenderer>() is {} oldMesh)oldMesh.Visible=false;
  if(op.GameObject.GetComponent<MiningTrade3D>() is {} trade){trade.PriceIntervalSeconds=1;trade.PriceVolatility=.035f;}
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("ANIMATED BLENDER MODELS INSTALLED. Backup "+backup);
 }
 static bool Under(GameObject o,GameObject parent){for(var p=o;p!=null;p=p.Parent)if(p==parent)return true;return false;}
}
