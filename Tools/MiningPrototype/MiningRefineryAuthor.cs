using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningRefineryAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/reference-refinery-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var station=scene.FindGameObject("Refinery station")!;var op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();
  var output=station.GetComponent<MiningRefineryOutput3D>()??station.AddComponent(new MiningRefineryOutput3D());output.OperationObjectId=op.GameObject.Id;output.ConveyorSpeed=1.45f;output.MaximumVisibleIngots=96;
  foreach(string name in new[]{"Refinery body","Refinery stack"})if(scene.FindGameObject(name) is {} old)foreach(var c in old.Components.OfType<BoxCollider3D>())c.Enabled=false;
  void Collision(string name,Vector3 p,Vector3 size){var o=scene.FindGameObject(name)??scene.CreateGameObject(name);o.SetParent(station,false);o.Transform.LocalPosition=p;var c=o.GetComponent<BoxCollider3D>()??o.AddComponent(new BoxCollider3D());c.Size=size;}
  Collision("Refinery frame collision",new(.50f,1.2f,.05f),new(4.4f,2.4f,1.8f));
  Collision("Refinery hopper collision",new(1.6f,3,.13f),new(2.5f,1.2f,2));
  Collision("Refinery conveyor collision",new(-1.95f,1.4f,-.35f),new(2,.35f,1.15f));
  Collision("Refinery bulk bin collision",new(-3.65f,.62f,-.35f),new(2.85f,1.24f,2.35f));
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("REFINERY INSTALLED: discharge port, continuous belt, stock-backed bulk ingots. Backup "+backup);
 }
}
