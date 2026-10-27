using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningCampAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/camp-arrangement-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();var ui=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>().Single();var player=scene.FindGameObject(op.PlayerObjectId)!;
  foreach(var pair in new[]{(scene.FindGameObject(op.DrillObjectId)!,"Drill"),(scene.FindGameObject(ui.AmmoMachineObjectId)!,"Ammo press"),(scene.FindGameObject(ui.RefineryMachineObjectId)!,"Refinery"),(scene.FindGameObject("Company tent")!,"Company tent")})
  {var marker=pair.Item1.GetComponent<MiningCampObject3D>()??pair.Item1.AddComponent(new MiningCampObject3D());marker.DisplayName=pair.Item2;}
  var button=scene.FindGameObject("Arrange camp button")??scene.CreateGameObject("Arrange camp button");button.SetParent(ui.GameObject,false);var widget=button.GetComponent<UiWidget>()??button.AddComponent(new UiWidget());widget.Kind=UiWidgetKind.Button;widget.Size=new(230,42);widget.Label="[TAB] ARRANGE CAMP";widget.FontReference=ui.BodyFont;widget.FontSize=20;widget.Color=new(.075f,.10f,.07f,.98f);widget.HoverColor=new(.28f,.40f,.22f,1);widget.BlocksWorldPointer=true;widget.ActivateWithSpace=false;widget.OrderInLayer=1200;
  var arrival=scene.FindGameObject("Camp arrival clearance")??scene.CreateGameObject("Camp arrival clearance");arrival.Transform.WorldPosition=player.Transform.WorldPosition;
  var camp=player.GetComponent<MiningCampLayout3D>()??player.AddComponent(new MiningCampLayout3D());camp.OperationObjectId=op.GameObject.Id;camp.InterfaceObjectId=ui.GameObject.Id;camp.EditButtonObjectId=button.Id;camp.SpawnClearanceObjectId=arrival.Id;camp.NavigationObjectId=scene.GameObjects.SelectMany(o=>o.Components).OfType<NavigationRegion3D>().Single().GameObject.Id;camp.Font=ui.BodyFont;
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("CAMP ARRANGEMENT INSTALLED. Backup "+backup);
 }
}
