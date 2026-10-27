using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using KarlsonDemo.Plugin;
internal static class MiningFlowAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/compact-flow-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();var ui=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>().Single();
  var press=scene.FindGameObject(ui.AmmoMachineObjectId)!;var bucket=press.GetComponent<MiningAmmoBucket3D>()??press.AddComponent(new MiningAmmoBucket3D());bucket.WorkshopObjectId=ui.WorkshopObjectId;bucket.BucketOffset=new(.5f,.5f,-1.5f);bucket.EjectionOffset=new(.5f,.9f,-.8f);
  if(scene.FindGameObject("Alien worksite floor") is {} floor){floor.Transform.WorldPosition=new(0,-.15f,-30);floor.Transform.LocalScale=new(44,.3f,96);}
  foreach(string name in new[]{"West perimeter","East perimeter"})if(scene.FindGameObject(name) is {} wall){var p=wall.Transform.WorldPosition;p.Z=-30;wall.Transform.WorldPosition=p;wall.Transform.LocalScale=new(.7f,2,96);}
  if(scene.FindGameObject("North perimeter") is {} north){north.Transform.WorldPosition=new(0,1,-78);}
  int i=0;foreach(var point in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningSpawnPoint3D>()){point.GameObject.Name="Front approach lane "+(++i);point.Transform.WorldPosition=new((i-3)*7,0,-38-(i%3)*7);}
  foreach(var pad in scene.GameObjects.Where(o=>o.Name=="Emergence pad"))pad.Active=false;
  foreach(var region in scene.GameObjects.SelectMany(o=>o.Components).OfType<NavigationRegion3D>()){region.Transform.WorldPosition=new(-22,0,-78);region.Width=88;region.Height=192;}
  string file=Program.Root+"/Assets/Events/Mining/Mining business controls.byteevents";File.Copy(file,backup+"/Mining business controls.byteevents");var codec=new EventModuleSerializer();var module=codec.Load(file);
  if(!module.Rules.Any(r=>r.DisplayName=="Enter - confirm current purchase or action"))module.Rules.Add(new(){DisplayName="Enter - confirm current purchase or action",Conditions=[new(){Id="input.keyPressed",Arguments=new(){["key"]=EventValue.String("Enter")}}],Actions=[new(){Id="bytebard.karlsondemo.mining.confirm",Arguments=new(){["target"]=EventValue.String("id:"+ui.GameObject.Id)}}]});
  codec.Save(module,file);runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("COMPACT FLOW INSTALLED. Backup "+backup);
 }
}
