using GoblinScrapper.Construction;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using System.Numerics;
namespace ByteEngine.Tests;
internal sealed partial class GoblinVehicleDiagnostic {
 private void AuthorScene(Scene scene,VehicleBuilder3D builder,string path){
  if(builder.UseAuthoredScene)throw new InvalidOperationException("Scene already authored; refusing to overwrite edits.");
  Scenes.LoadScene(scene);for(int i=0;i<3;i++)TickInput(scene,new(.98f,.5f),[]);
  var ui=scene.GameObjects.Single(o=>o.Name=="Goblin Scraper workshop HUD");
  var keep=scene.GameObjects.Where(o=>o==ui||o.IsDescendantOf(ui)||o.Name is "Main Camera" or "Directional Light" or "SkyEnvironment"||o.GetComponent<VehicleBuilder3D>()!=null||o.GetComponent<SkyEnvironment>()!=null||o.GetComponent<DirectionalLight>()!=null||o.GetComponent<EventModuleComponent>()!=null).ToHashSet();
  foreach(var o in scene.GameObjects){
   if(o.GetComponent<MeshRenderer>()!=null&&o.Variables.Contains("GoblinAuthoringKey")&&!o.Name.Contains("skid",StringComparison.OrdinalIgnoreCase)&&!o.Name.Contains("highlight",StringComparison.OrdinalIgnoreCase)&&!o.Name.StartsWith("Mount:")&&!o.Name.StartsWith("Connector"))keep.Add(o);
   if(o.Name is "Spare parts stand" or "Master block")foreach(var child in scene.GameObjects.Where(c=>c==o||c.IsDescendantOf(o)))keep.Add(child);
  }
  foreach(var o in scene.GameObjects.Where(o=>!keep.Contains(o)).ToArray())if(o.Scene!=null)scene.DestroyGameObject(o);
  var world=scene.CreateGameObject("WORKSHOP - editable environment");var camp=scene.CreateGameObject("BATTLEFIELD - editable level");var enemies=scene.CreateGameObject("GOBLINS - duplicate or move these spawns");
  foreach(var o in keep.Where(o=>o.Scene!=null&&o.Parent==null&&o.GetComponent<MeshRenderer>()!=null).ToArray()){o.SetParent(o.Name.StartsWith("Battlefield")||o.Name.StartsWith("Red ")?camp:world,true);if(o.Name.Contains("barrier")||o.Name.Contains("boundary")||o.Name.Contains("cover")||o.Name=="Scrap crate"||o.Name=="Slalom marker")o.AddComponent(new BoxCollider3D());}
  foreach(var o in scene.GameObjects.Where(o=>o.Name is "Spare parts stand").ToArray())o.SetParent(world,true);
  var floor=scene.GameObjects.Single(o=>o.Name=="Test yard floor");var renderer=floor.GetComponent<MeshRenderer>()!;renderer.UsePrimitive=true;renderer.Mesh=null;floor.Transform.WorldPosition=new(0,-.15f,0);floor.AddComponent(new BoxCollider3D());
  var master=scene.GameObjects.Single(o=>o.Name=="Master block");master.Name="Starting block - editor preview";master.Variables.Set("GoblinEditorPreview",VariableValue.FromBoolean(true));
  for(int i=0;i<18;i++){var g=VehicleBuilder3D.CreateModelVisual(scene,_project!.Assets,builder.EnemyModel,enemies,"Goblin spawn "+i.ToString("00"));g.Transform.WorldPosition=new((i%6-2.5f)*2.4f,0,-9-(i/6)*4);g.Transform.WorldRotation=Quaternion.Identity;g.Variables.Set("GoblinEnemySpawn",VariableValue.FromBoolean(true));g.GetComponent<SkeletalMeshRenderer>()!.PlayOnStart=false;}
  builder.UseAuthoredScene=true;builder.Transform.WorldPosition=new(0,1.25f,0);
  _project!.Scenes.Save(scene,path);Console.WriteLine("SAVED: authored workshop, battlefield, HUD and 18 goblin spawns: "+path);
 }
}
