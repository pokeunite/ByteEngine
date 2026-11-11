using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Characters;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningSupportMenusCheck
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS SUPPORT UI "+message);}
 static void Set(object o,string n,object v)=>o.GetType().GetProperty(n)!.SetValue(o,v);
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var sceneFile=Program.Root+"/Scenes/Main.bytescene";var hash=SHA256.HashData(File.ReadAllBytes(sceneFile));var menu=op.GameObject.GetComponent<MiningPauseMenu3D>()!;menu.PersistSettings=false;
  var host=new SceneManager();host.LoadScene(scene);var player=scene.FindGameObject(op.PlayerObjectId)!;var gun=player.GetComponent<FpsGunLab3D>()!;
  void Frame(Vector2? p=null,bool down=false,Key? key=null,float wheel=0){var raw=new RawInputSnapshot{MouseWheel=wheel};if(down)raw.MouseButtonsDown.Add(MouseButton.Left);if(key.HasValue)raw.KeysDown.Add(key.Value);typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[raw,true]);typeof(Input).GetMethod("SetGameInputCaptured",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[true]);Input.SetGameViewPointer((p??new(640,360))/new Vector2(1280,720),new(1280,720),true);typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}
  (string Label,Vector2 P,Vector2 Size)[] Items(){typeof(MiningPauseMenu3D).GetMethod("Build",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(menu,[1280f,720f,false]);return ((System.Collections.IEnumerable)typeof(MiningPauseMenu3D).GetField("items",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(menu)!).Cast<object>().Select(a=>{var t=a.GetType();float V(string n)=>(float)t.GetProperty(n)!.GetValue(a)!;return((string)t.GetProperty("Label")!.GetValue(a)!,new Vector2(V("X"),V("Y")),new Vector2(V("W"),V("H")));}).ToArray();}
  void Click(string text){Frame();var a=Items().Last(i=>i.Label.StartsWith(text,StringComparison.OrdinalIgnoreCase));Frame(a.P+a.Size/2,true);Frame();}
  Frame();Frame();Frame();Frame();menu.Open();Check(Items().Count()==4&&Items().All(a=>a.Size.X<=244),"compact pause keeps Resume, Settings, Screenshot and Quit");Check(!gun.Fire(),"pause pointer ownership blocks shooting");Click("Settings");Check(menu.Page==MiningPausePage.Sound&&Items().Any(a=>a.Label.StartsWith("Forward|")),"Settings opens the paired audio and controls view");
  Frame(new(409,226),true);Frame();Check(Math.Abs(AudioMixer.GetVolume(AudioBus.Master)-.5f)<.03f,"visible Master slider drives real audio volume");AudioMixer.SetVolume(AudioBus.Master,1);
  Click("Forward|");Frame(key:Key.Z);Frame();Check(Input.GameplayKey(Key.W)==Key.Z,"Listen captures a new movement key");Input.ClearGameplayKeyBindings();Frame(wheel:-20);Check(Items().Any(a=>a.Label.StartsWith("Aim / cancel")),"control list scrolls to mouse bindings and all defence keys");Check(!Items().Any(a=>a.Label.Contains("Jump")||a.Label.Contains("Drop")),"unwanted jump and drop controls stay absent");
  Click("Graphics");GameWindowMode applied=GameWindowMode.Fullscreen;using(var lease=GameWindowDisplay.Attach(m=>applied=m,GameWindowMode.Fullscreen)){foreach(var mode in Enum.GetValues<GameWindowMode>()){Click("Window mode|");Click(mode.ToString());Click("Apply");Check(applied==mode,"dropdown overlay and Apply choose "+mode);}}Click("Back");Check(menu.Page==MiningPausePage.Sound,"Graphics Back returns to settings");Click("Back");Click("Resume");Check(!menu.IsOpen&&!Input.IsUiPointerRequested,"Resume restores gameplay capture");
  Set(op,"CompletedRounds",2);Set(op,"Credits",100000);Set(op,"RefinedOre",500);var trade=op.GameObject.GetComponent<MiningTrade3D>()!;trade.Initialize();if(!trade.HasResearch("charter"))trade.BuyResearch("charter");if(!trade.HasResearch("rd"))trade.BuyResearch("rd");var shop=op.GameObject.GetComponent<MiningResearchStation3D>()!;Frame();Check(shop.PlaceStation(new(-15,0,-25)),"R&D placement remains available");player.Transform.WorldPosition=new(-15,.15f,-27);shop.Open();
  void ShopClick(int row){var p=new Vector2(1280-488+400,(720-480)/2+94+row*67);Frame(p);Frame(p,true);Frame();}
  int stock=shop.BarricadeStock,ore=op.RefinedOre;ShopClick(1);Check(shop.BarricadeStock==stock+1&&op.RefinedOre==ore-shop.BarricadeOreCost,"new barricade price button purchases a kit without placing it");stock=shop.MineStock;ShopClick(2);Check(shop.MineStock==stock+1,"new mine button purchases a kit");stock=shop.RefineryStock;ShopClick(3);Check(shop.RefineryStock==stock+1,"new refinery button orders a kit");int cargo=op.CargoCapacity;ShopClick(4);Check(op.CargoCapacity>cargo,"cargo expansion still upgrades capacity");ShopClick(0);Check(shop.ActiveDroneCount==1&&!gun.Fire(),"drone purchase works and R&D clicks cannot fire the weapon");Frame(new(1280-488+448,120+28));Frame(new(1280-488+448,120+28),true);Frame();Check(!shop.IsOpen,"new R&D close control closes the panel");
  var camp=player.GetComponent<MiningCampLayout3D>()!;Frame(key:Key.Tab);Frame();Check(camp.IsEditing,"Tab opens arrangement after the overhaul");Check(scene.FindGameObject(camp.EditButtonObjectId)?.Active==false,"old arrangement widget cannot overlap the new overlay");Frame(key:Key.Tab);Frame();Check(!camp.IsEditing,"Tab closes arrangement and restores controls");host.UnloadScene();Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(sceneFile))),"support UI tests preserve authored scene");
 }
}
