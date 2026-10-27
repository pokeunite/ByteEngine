using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Characters;
using System.Numerics;
using KarlsonDemo.Plugin;
internal static class MiningPressAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/reference-press-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);
  File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var press=scene.FindGameObject("Ammo press station")??throw new Exception("Ammo press station missing");
  var bucket=press.GetComponent<MiningAmmoBucket3D>()??press.AddComponent(new MiningAmmoBucket3D());
  var trade=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>().Single();
  bucket.WorkshopObjectId=trade.GameObject.Id;bucket.BeltStartMarker="BeltStart";bucket.BeltEndMarker="BeltEnd";bucket.BucketMarker="BucketFloor";
  bucket.MaximumVisibleRounds=72;bucket.MaximumTravellingRounds=12;bucket.RoundsPerVisual=2;bucket.ConveyorSpeed=1.58f;bucket.DropSeconds=.4f;bucket.PileFootprint=new(1.08f,.70f);bucket.MaximumPileHeight=.3f;bucket.PickupRadius=1.45f;bucket.AutoPickup=true;
  foreach(string name in new[]{"Ammo press body","Ammo press hopper"})if(scene.FindGameObject(name) is {} old)foreach(var collider in old.Components.OfType<BoxCollider3D>())collider.Enabled=false;
  void Collision(string name,Vector3 centre,Vector3 size){var o=scene.FindGameObject(name)??scene.CreateGameObject(name);o.SetParent(press,false);o.Transform.LocalPosition=centre;var c=o.GetComponent<BoxCollider3D>()??o.AddComponent(new BoxCollider3D());c.Size=size;}
  Collision("Press frame collision",new(.12f,1.5f,.1f),new(2.25f,3,1.3f));
  Collision("Press conveyor collision",new(0,1.28f,-1.3f),new(1.2f,.35f,2.2f));
  Collision("Press tray collision",new(0,.32f,-2.9f),new(1.4f,.64f,1.1f));
  scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningAudio3D>().Single().RefineryVolumeMultiplier=3;
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");
  Console.WriteLine("REFERENCE PRESS anchors, bounded pile and refinery gain installed. Backup "+backup);
 }
}
