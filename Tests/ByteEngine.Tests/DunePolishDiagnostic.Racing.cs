using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Scene;
using ByteEngine.Core.AI;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunSprintAndEconomy(string root){
  var saves=Path.Combine(root,"Saves");Directory.CreateDirectory(saves);foreach(var name in new[]{"workshop-economy-v1.json","sprint-progress-v1.json","vehicle.json","active-blueprint.txt","workshop-tutorial.json"})File.Delete(Path.Combine(saves,name));File.WriteAllText(Path.Combine(saves,"recovery-payment.json"),"3400");
  var scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/ScrapyardSprint.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);var workshop=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();
  if(!workshop.BuyPart(34)||workshop.RecoveryPayment!=2950||!workshop.UnlockTuning(34)||workshop.RecoveryPayment!=2900||!workshop.UnlockTuning(34)||workshop.RecoveryPayment!=2900)throw new Exception("Persistent purchase or one-time tuning charge failed");
  typeof(DuneWorkshop3D).GetField("_tutorialStep",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(workshop,-1);
  typeof(DuneWorkshop3D).GetMethod("SetGarageEditing",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(workshop,new object[]{true});workshop.SelectType(34);Tick(scene,new(.5f),false);Capture(scene,Path.Combine(root,"Preview/upgrade-shop.png"),false);
  if(!workshop.AddBlock(34,0,new(0,.5f,0),Quaternion.Identity)||!workshop.AddBlock(34,0,new(.5f,.5f,0),Quaternion.Identity))throw new Exception("Paid part duplication escaped ownership checks");workshop.Command("Delete selected");
  if(workshop.SellPart(34)||!workshop.BuyPart(34)||workshop.RecoveryPayment!=2900)throw new Exception("Permanent part unlock charged twice or sale remained active");
  var save=Path.Combine(saves,"vehicle.json");File.Copy(Path.Combine(root,"Assets/RecoveryBlueprints/Recovery-demo.json"),save,true);workshop.Command("Load vehicle");workshop.SelectRecoveryContract(4);Capture(scene,Path.Combine(root,"Preview/sprint-brief.png"),false);workshop.Command("Deploy contract");Tick(scene,new(.5f),false);
  var physics=(LandVehiclePhysics)typeof(DuneWorkshop3D).GetField("_physics",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(workshop)!;if(physics.HasRecovery||physics.BodyCount<10)throw new Exception("Sprint did not add its separate physical opponent");
  var audioField=typeof(DuneWorkshop3D).GetField("_jobSounds",BindingFlags.Instance|BindingFlags.NonPublic)!;
  var raceAudio=(System.Collections.IDictionary)audioField.GetValue(workshop)!;if(!raceAudio.Contains("race-countdown"))throw new Exception("Race countdown has no loaded audio cue");
  if(scene.FindGameObject("Sprint blue ring 1")?.ActiveInHierarchy!=true||scene.FindGameObject("Sprint blue ring 4")?.ActiveInHierarchy==true)throw new Exception("Next blue racing gates are not visible in order");
  var rival=scene.FindGameObject("Sprint opponent")!;bool RivalNode(GameObject o){for(var p=o.Parent;p!=null;p=p.Parent)if(p==rival)return true;return false;}
  var pulley=scene.GameObjects.FirstOrDefault(o=>o.Name=="Engine_Pulley"&&RivalNode(o))??throw new Exception("Race engine pulley not imported");var restPulley=pulley.Transform.LocalRotation;
  for(int i=0;i<10;i++)Tick(scene,new(.5f),false,ByteEngine.Core.Key.Space);if(pulley.Transform.LocalRotation==restPulley)throw new Exception("Engine pulley is not animated");
  Capture(scene,Path.Combine(root,"Preview/sprint-blue-gates.png"),false);
  var brain=scene.GameObjects.SelectMany(o=>o.Components).OfType<AiAgent3D>().Single();
  float peak=0;for(int i=0;i<8000&&brain.State!=AiAgentState.Finished;i++){Tick(scene,new(.5f),false,ByteEngine.Core.Key.Space);peak=Math.Max(peak,physics.AssemblyVelocity(20000).Length());}if(peak<9)throw new Exception("Rival still limited to starter speed");Console.WriteLine("Rival peak speed: "+peak);
  Console.WriteLine($"RACER progress={brain.Checkpoint} state={brain.State} position={physics.AssemblyFrame(20000).Position}");
  if(brain.State!=AiAgentState.Finished)throw new Exception("Physical opponent cannot negotiate the opening bends");
  Capture(scene,Path.Combine(root,"Preview/sprint-physical-rival.png"),false);
  workshop.Command("Recover vehicle");for(int i=0;i<200;i++)Tick(scene,new(.5f),false,ByteEngine.Core.Key.Space);
  physics=(LandVehiclePhysics)typeof(DuneWorkshop3D).GetField("_physics",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(workshop)!;
  var director=scene.GameObjects.SelectMany(o=>o.Components).OfType<RaceDirector3D>().Single();var route=scene.GameObjects.SelectMany(o=>o.Components).OfType<RoutePath3D>().Single();
  if(!raceAudio.Contains("race-go"))throw new Exception("Race launch has no loaded audio cue");
  physics.ResetTruck(route.Points[^1],Quaternion.Identity);Tick(scene,new(.5f),false,ByteEngine.Core.Key.Space);if(director.Gate("Player")!=0)throw new Exception("Finish shortcut skipped the ordered gates");
  int before=workshop.RecoveryPayment;foreach(var point in route.Points){physics.ResetTruck(point,Quaternion.Identity);Tick(scene,new(.5f),false,ByteEngine.Core.Key.Space);}
  if(!raceAudio.Contains("race-checkpoint"))throw new Exception("Crossing the ordered gates has no loaded audio cue");Console.WriteLine("PASS Loaded countdown, launch and actual ordered checkpoint audio cues");
  for(int i=0;i<200;i++)Tick(scene,new(.5f),false,ByteEngine.Core.Key.Space);if(!workshop.Building||workshop.RecoveryPayment!=before+1000)throw new Exception("Sprint payout / garage return failed");
  Scenes.UnloadScene();var reload=_project.Scenes.Load(Path.Combine(root,"Scenes/Workshop.bytescene"));Scenes.LoadScene(reload);Tick(reload,new(.5f),false);var restored=reload.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();if(restored.RecoveryPayment!=before+1000||!restored.UnlockTuning(34)||restored.RecoveryPayment!=before+1000)throw new Exception("Economy or tuning unlock did not survive scene reload");
  var blueprint=Path.Combine(saves,"blueprints/keep-test.json");Directory.CreateDirectory(Path.GetDirectoryName(blueprint)!);File.WriteAllText(blueprint,"[]");typeof(DuneWorkshop3D).Assembly.GetType("DuneCompany.DuneCampaignSaves")!.GetMethod("Reset",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,new object[]{saves});if(File.Exists(Path.Combine(saves,"workshop-economy-v1.json"))||File.Exists(Path.Combine(saves,"sprint-progress-v1.json"))||!File.Exists(blueprint))throw new Exception("New Game failed to reset purchases/tuning while preserving blueprints");
  var economyFile=Path.Combine(saves,"workshop-economy-v1.json");File.WriteAllText(economyFile,"{\"Version\":1,\"Balance\":100,\"Owned\":{\"34\":3},\"Tuning\":[34],\"Rewards\":[]}");
  var loadEconomy=typeof(DuneWorkshop3D).GetMethod("LoadEconomy",BindingFlags.Instance|BindingFlags.NonPublic)!;loadEconomy.Invoke(restored,null);
  if(restored.RecoveryPayment!=1000||!restored.BuyPart(34)||!restored.UnlockTuning(34)||restored.RecoveryPayment!=1000)throw new Exception("Legacy copies did not migrate to a permanent unlock and refund");
  loadEconomy.Invoke(restored,null);if(restored.RecoveryPayment!=1000)throw new Exception("Legacy extra-copy refund repeated");File.Delete(economyFile);Console.WriteLine("PASS Legacy purchase migration preserves tuning and refunds extra copies once");
  File.Delete(blueprint);Scenes.UnloadScene();Console.WriteLine("PASS Ordered race gates, $1000 payout, garage return, economy reload and New Game reset");Console.WriteLine("PASS Permanent parts, lower tuning charges, blue rings, animated engine and faster physical rival");
 }
}
