using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
static class MiningPauseTests
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS pause/settings: "+text);}
 public static void Run()
 {
  var temp=Path.Combine(Path.GetTempPath(),"byte-menu-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  var saveScene=new Scene("Fresh menu");typeof(Scene).GetProperty("ProjectRoot")!.SetValue(saveScene,temp);saveScene.CreateGameObject("Operation").AddComponent(new MiningOperation3D());var fresh=saveScene.CreateGameObject("Menu").AddComponent(new MiningPauseMenu3D());
  Check(!fresh.HasSavedGame,"fresh build has no Continue option");Directory.CreateDirectory(Path.Combine(temp,"Saves"));File.WriteAllText(Path.Combine(temp,"Saves","mining-company-v1.json"),"{}");
  var slot=saveScene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single().SaveSlot;File.WriteAllText(Path.Combine(temp,"Saves",slot),"{invalid");Check(!fresh.HasSavedGame,"corrupt saves do not expose Continue");File.WriteAllText(Path.Combine(temp,"Saves",slot),"{\"Version\":1}");Check(fresh.HasSavedGame,"valid progress exposes Continue");Directory.Delete(temp,true);
  Check(new ByteEngine.Core.Serialization.SerializationModels.ProjectWindowData().StartFullscreen,"new Windows games default to fullscreen");
  var controls=(ValueTuple<string,Key>[])typeof(MiningPauseMenu3D).GetField("controls",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(fresh)!;Check(!controls.Any(c=>c.Item1.Contains("Jump")||c.Item1.Contains("Drop"))&&controls.Any(c=>c.Item2==Key.F5),"controls omit jump/drop and expose all five defence shortcuts");
  var seen=new List<ByteEngine.Core.Runtime.GameWindowMode>();using(var display=ByteEngine.Core.Runtime.GameWindowDisplay.Attach(seen.Add,ByteEngine.Core.Runtime.GameWindowMode.Fullscreen)){ByteEngine.Core.Runtime.GameWindowDisplay.Request(ByteEngine.Core.Runtime.GameWindowMode.Windowed);ByteEngine.Core.Runtime.GameWindowDisplay.Request(ByteEngine.Core.Runtime.GameWindowMode.Borderless);Check(seen.SequenceEqual(new[]{ByteEngine.Core.Runtime.GameWindowMode.Fullscreen,ByteEngine.Core.Runtime.GameWindowMode.Windowed,ByteEngine.Core.Runtime.GameWindowMode.Borderless}),"all three display choices reach the host");}Check(!ByteEngine.Core.Runtime.GameWindowDisplay.Available,"display host releases cleanly");ByteEngine.Core.Runtime.GameWindowDisplay.Request(ByteEngine.Core.Runtime.GameWindowMode.Fullscreen);
  Input.ClearGameplayKeyBindings();var scene=new Scene("Pause verification");var player=scene.CreateGameObject("Player");var control=player.AddComponent(new PlayerController3D());var disabled=player.AddComponent(new FpsGunLab3D{Enabled=false});
  var menu=scene.CreateGameObject("Pause").AddComponent(new MiningPauseMenu3D{PersistSettings=false});typeof(Component).GetMethod("StartInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(menu,null);
  Check(Input.HasGameplayEscapeHandler,"pause component owns Escape before the host releases capture");scene.SimulationTimeScale=.5f;menu.Open();Check(menu.IsOpen&&scene.SimulationTimeScale==0&&!control.Enabled&&Input.IsUiPointerRequested,"opening freezes gameplay and requests the cursor");
  disabled.AllowWeaponDropping=false;Check(!disabled.Drop(),"project equipment can explicitly forbid dropping weapons");
  menu.ShowPage(MiningPausePage.Controls);Check(menu.Rebind(Key.E,Key.F),"key binding accepts an unused physical key");Check(!menu.Rebind(Key.R,Key.F)&&!menu.Rebind(Key.E,Key.Escape),"duplicate and reserved key bindings are rejected");
  var update=typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!;
  menu.Resume();Check(scene.SimulationTimeScale==.5f&&control.Enabled&&!disabled.Enabled&&!Input.IsUiPointerRequested,"resume restores prior time scale and enabled state without a recapture click");
  var raw=new RawInputSnapshot();raw.KeysDown.Add(Key.F);update.Invoke(null,[raw,true]);Check(Input.IsKeyDown(Key.E)&&Input.Snapshot.KeysDown.Contains(Key.E),"rebinding reaches both Event Sheet key conditions and action-map snapshots");
  Input.RequestUiPointer(menu,true);Check(!Input.IsKeyDown(Key.E)&&Input.IsKeyDown(Key.F),"menu key navigation continues to use physical input");Input.RequestUiPointer(menu,false);
  Input.BindGameplayMouseButton(MouseButton.Left,MouseButton.Right);raw.MouseButtonsDown.Add(MouseButton.Right);update.Invoke(null,[raw,true]);Check(Input.IsMouseButtonDown(MouseButton.Left)&&Input.Snapshot.MouseButtonsDown.Contains(MouseButton.Left),"mouse rebinding reaches direct weapon input and actions");
  typeof(Component).GetMethod("StopInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(menu,null);Check(!Input.HasGameplayEscapeHandler&&Input.GameplayKey(Key.E)==Key.E,"stopping removes Escape ownership and game-specific bindings");update.Invoke(null,[new RawInputSnapshot(),false]);
 }
}
