using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningInteractionRegressionCheck
{
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS INTERACTION "+label);}
 static void Set(object o,string key,object value)=>o.GetType().GetProperty(key)!.SetValue(o,value);
 static void Update(object o)=>o.GetType().GetMethod("OnUpdate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(o,null);
 static void Raw(params Key[] keys){var raw=new RawInputSnapshot();foreach(var key in keys)raw.KeysDown.Add(key);typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[raw,true]);}
 public static void VerifyRoutes(Scene scene,MiningOperation3D op)
 {
  var host=new SceneManager();host.LoadScene(scene);var ui=scene.GetComponentsSnapshot<MiningBusinessUi3D>().Single();scene.GetComponentsSnapshot<ByteEngine.Core.Navigation.NavigationRegion3D>().Single().Bake();
  foreach(var step in new[]{CampTutorialStep.AmmoPress,CampTutorialStep.StartDrill,CampTutorialStep.Refinery,CampTutorialStep.CompanyHub}){
   Set(op,"TutorialStep",step);typeof(MiningOperation3D).GetMethod("UpdateTutorialPath",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(op,[ui]);
   Check(op.TutorialRouteReady&&op.TutorialRoutePointCount>=2,"tutorial has a complete navigable route for "+step);
  }
  host.UnloadScene();
 }
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var host=new SceneManager();host.LoadScene(scene);var player=scene.FindGameObject(op.PlayerObjectId)!;var gun=player.GetComponent<FpsGunLab3D>()!;var shop=op.GameObject.GetComponent<MiningResearchStation3D>()!;var ui=scene.GetComponentsSnapshot<MiningBusinessUi3D>().Single();var trade=op.GameObject.GetComponent<MiningTrade3D>()!;
  op.Initialize();Set(op,"CompletedRounds",2);Set(op,"Credits",100000);Set(op,"Phase",MiningPhase.Preparation);trade.Initialize();if(!trade.HasResearch("charter"))trade.BuyResearch("charter");if(!trade.HasResearch("rd"))trade.BuyResearch("rd");Update(shop);
  Check(shop.StationKitAvailable&&shop.DeployedStation==null,"unlock retains an R&D placement kit");Check(shop.PlaceStation(new(-15,0,-25)),"station can be explicitly deployed");
  foreach(var point in new[]{new Vector3(0,0,-30),new Vector3(12,0,-30)})Check(shop.Buy(3)&&shop.PlaceRefinery(point),"additional refinery can be independently placed");
  var machines=scene.GetComponentsSnapshot<MiningAuxiliaryRefinery3D>().ToArray();Check(machines.Length==2,"two local refinery controllers exist");
  foreach(var local in machines){
   player.Transform.WorldPosition=local.Transform.WorldPosition+new Vector3(0,.15f,-2);Raw();Raw(Key.E);
   foreach(var other in machines)Update(other);ui.OpenNearby();
   Check(local.IsOpen&&machines.Count(r=>r.IsOpen)==1&&ui.Screen==MiningScreen.None,"E opens only the closest refinery, not the original or neighbouring machine");
   int ammo=gun.SelectedWeapon?.CurrentAmmo??0;Check(Input.IsUiPointerRequested&&!gun.Fire()&&(gun.SelectedWeapon?.CurrentAmmo??0)==ammo,"refinery menu clicks cannot fire the weapon");
   local.Close();Raw();
  }
  shop.Open();Check(shop.IsOpen&&!gun.Fire(),"R&D menu cannot fire the weapon");
  var click=new RawInputSnapshot();click.MouseButtonsDown.Add(MouseButton.Left);typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[click,true]);shop.Close();Check(!gun.Fire(),"closing a menu consumes its click before gameplay resumes");Raw();
  ui.Open(MiningScreen.Refinery);Check(ui.Screen==MiningScreen.Refinery&&!gun.Fire(),"original refinery also blocks firing");ui.Close();Raw();
  int before=op.Credits;Set(op,"RawOre",70);Check(machines[0].BeginLoad()&&machines[1].BeginLoad()&&machines[0].LoadedRaw==50&&machines[1].LoadedRaw==20,"local machines reserve independent finite loads");float second=machines[1].Remaining;machines[0].Tick(1);Check(machines[1].Remaining==second,"ticking one refinery does not advance another");Check(machines[0].BuyUpgrade(1)&&machines[1].SpeedLevel==0&&op.Credits<before,"local upgrades do not upgrade other machines");
  shop.PersistSupport();Check(op.ResearchSupportState.Contains("StationRotation")&&op.ResearchSupportState.Contains("Refineries"),"station placement and independent refinery state are saved");
  host.UnloadScene();
 }
}
