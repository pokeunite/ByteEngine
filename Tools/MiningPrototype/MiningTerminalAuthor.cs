using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningTerminalAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/terminal-and-massive-drill-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();var ui=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>().Single();var drill=scene.FindGameObject(op.DrillObjectId)!;var model=scene.FindGameObject("Animated drill model")!;var tent=scene.FindGameObject("Company tent")!;
  AssetReference Asset(string name){string p="Assets/Models/Mining/"+name+".glb";using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+p+".meta"));return new(meta.RootElement.GetProperty("guid").GetGuid(),p);}
  void Assign(GameObject o,string name){var renderer=o.GetComponent<SkeletalMeshRenderer>()!;renderer.Model=Asset(name);renderer.SkeletonKey=null;renderer.MaterialKeys.Clear();renderer.MaterialAssetSlots.Clear();renderer.DefaultAnimation="Operate";renderer.PlayOnStart=false;renderer.AnimationUpdateInterval=1f/30;}
  Assign(model,"MiningDrill-Massive");Assign(tent,"CompanyTerminal");
  bool Under(GameObject o,GameObject parent){for(var p=o.Parent;p!=null;p=p.Parent)if(p==parent)return true;return false;}
  foreach(var o in scene.GameObjects.Where(o=>Under(o,drill)||Under(o,tent)))foreach(var c in o.Components.OfType<Collider3D>())c.Enabled=false;
  var core=drill.GetComponent<BoxCollider3D>()!;core.Size=new(3,6.2f,3);core.Center=new(0,3.1f,0);
  void Collision(GameObject parent,string name,Vector3 p,Vector3 size,float yaw=0){var o=scene.FindGameObject(name)??scene.CreateGameObject(name);o.SetParent(parent,false);o.Transform.LocalPosition=p;o.Transform.EulerAngles=new(0,yaw,0);var c=o.GetComponent<BoxCollider3D>()??o.AddComponent(new BoxCollider3D());c.Size=size;c.Enabled=true;}
  for(int i=0;i<9;i++){float angle=-30+i*30,radians=angle*MathF.PI/180;Collision(drill,"Massive drill base collision "+i,new(-MathF.Cos(radians)*3.3f,.85f,MathF.Sin(radians)*3.3f),new(2.25f,1.7f,1.95f),180+angle);}
  foreach(float side in new[]{-1f,1f}){Collision(drill,"Massive drill column "+side,new(side*3.10f,3.1f,0),new(1.55f,6.2f,1.8f));Collision(drill,"Massive drill front foot "+side,new(side*4.35f,.30f,-3.45f),new(1.9f,.6f,1.6f));}
  Collision(drill,"Massive drill ore hopper collision",new(-8.3f,1.8f,1.3f),new(4.95f,3.6f,4.1f));
  Collision(tent,"Tent left collision",new(-2.40f,1.40f,0),new(.23f,2.8f,3.9f));Collision(tent,"Tent right collision",new(2.40f,1.40f,0),new(.23f,2.8f,3.9f));Collision(tent,"Tent rear collision",new(0,1.4f,1.90f),new(4.9f,2.8f,.20f));
  Collision(tent,"Terminal desk collision",new(.20f,.62f,.43f),new(2.93f,1.24f,.97f));Collision(tent,"Terminal screen collision",new(.63f,1.85f,.63f),new(1.67f,1.15f,.25f));Collision(tent,"Terminal crate collision",new(-1.90f,.45f,.50f),new(1.05f,.92f,.78f));
  foreach(float side in new[]{-1f,1f})Collision(tent,"Tent entry post collision "+side,new(side*2.48f,1.4f,-1.9f),new(.24f,2.8f,.25f));
  Collision(tent,"Terminal floor mat collision",new(0,.05f,-.12f),new(4.98f,.10f,4.05f));scene.FindGameObject("Terminal floor mat collision")!.Layer=20;
  var anchor=scene.FindGameObject("Company tent computer interaction")!;anchor.SetParent(tent,false);anchor.Transform.LocalPosition=new(0,0,-1.45f);ui.TerminalObjectId=anchor.Id;
  var console=scene.FindGameObject("Massive drill control interaction")??scene.CreateGameObject("Massive drill control interaction");console.SetParent(drill,false);console.Transform.LocalPosition=new(2.16f,0,-3.25f);ui.DrillInteractionObjectId=console.Id;
  var light=scene.FindGameObject("Company terminal work light")??scene.CreateGameObject("Company terminal work light");light.SetParent(tent,false);light.Transform.LocalPosition=new(0,2.55f,-1.35f);var lamp=light.GetComponent<PointLight>()??light.AddComponent(new PointLight());lamp.Color=new(1,.83f,.56f);lamp.Intensity=1.15f;lamp.Range=5;lamp.CastShadows=false;
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("NEW DRILL AND TERMINAL ASSIGNED. Full-scale drill, fitted collision, console and tent interaction retained. Backup "+backup);
 }
}
