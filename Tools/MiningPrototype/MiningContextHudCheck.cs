using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningContextHudCheck
{
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS HUD/POINTER "+label);}
 static void Raw(bool down){var raw=new RawInputSnapshot();if(down)raw.MouseButtonsDown.Add(MouseButton.Left);typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[raw,true]);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var ui=scene.GetComponentsSnapshot<MiningBusinessUi3D>().Single();
  typeof(MiningOperation3D).GetProperty("Phase")!.SetValue(op,MiningPhase.Preparation);Check(ui.EconomyHudVisible,"economy is visible during preparation");
  typeof(MiningOperation3D).GetProperty("Phase")!.SetValue(op,MiningPhase.Drilling);Check(!ui.EconomyHudVisible&&ui.DrillHudVisible,"combat hides economy while keeping drill health");
  ui.AlwaysShowEconomyHud=true;Check(ui.EconomyHudVisible,"always-visible preference overrides economy hiding");ui.AlwaysShowEconomyHud=false;
  Raw(false);var first=new object();var second=new object();Input.RequestUiPointer(first,true);Raw(true);Check(Input.IsMouseButtonPressedForUi(MouseButton.Left)&&!Input.IsMouseButtonDown(MouseButton.Left)&&!Input.IsMouseButtonPressed(MouseButton.Left),"portable browser input remains clickable for UI but cannot fire gameplay");
  Input.RequestUiPointer(second,true);Input.RequestUiPointer(first,false);Check(!Input.IsMouseButtonDown(MouseButton.Left),"nested menus keep gameplay blocked");Input.RequestUiPointer(second,false);Check(!Input.IsMouseButtonDown(MouseButton.Left),"closing last menu cannot leak its held click");Raw(false);Raw(true);Check(Input.IsMouseButtonPressed(MouseButton.Left),"a fresh click works after releasing the menu click");Raw(false);
 }
}
