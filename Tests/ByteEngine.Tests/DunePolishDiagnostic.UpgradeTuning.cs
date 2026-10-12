using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Graphics;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunUpgradeTuning(string root)
 {
  var saves=Path.Combine(root,"Saves");Directory.CreateDirectory(saves);
  File.Delete(Path.Combine(saves,"workshop-economy-v1.json"));File.WriteAllText(Path.Combine(saves,"recovery-payment.json"),"10000");
  var scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/Workshop.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);
  var workshop=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  typeof(DuneWorkshop3D).GetField("_tutorialStep",flags)!.SetValue(workshop,-1);
  typeof(DuneWorkshop3D).GetMethod("SetGarageEditing",flags)!.Invoke(workshop,new object[]{true});
  var tuning=typeof(DuneWorkshop3D).GetField("_tuning",flags)!;
  var settings=typeof(DuneWorkshop3D).GetMethod("Settings",flags)!;
  var set=typeof(DuneWorkshop3D).GetMethod("SetValue",flags)!; string Snapshot()=>(string)typeof(DuneWorkshop3D).GetMethod("Snapshot",flags)!.Invoke(workshop,null)!;
  foreach(int type in new[]{34,35,36,37,38})
  {
   workshop.SelectType(type);if(!workshop.BuyPart(type)||!workshop.AddBlock(type,0,new(0,.5f,0),Quaternion.Identity))throw new Exception("Cannot purchase/place upgrade "+type);
   typeof(DuneWorkshop3D).GetMethod("OpenTuning",flags)!.Invoke(workshop,null);Tick(scene,new(.5f),false);
   var fields=(Array)settings.Invoke(workshop,null)!;if(fields.Length<(type==37?4:3))throw new Exception("Missing upgrade tuning fields");
   if(scene.FindGameObject("Summary title")!.ActiveInHierarchy)throw new Exception("Summary overlaps tuning panel");if(scene.FindGameObject("Sell unused upgrade")?.ActiveInHierarchy==true)throw new Exception("Sale UI still shown");var unlock=scene.FindGameObject("Unlock tuning")!;if(!unlock.ActiveInHierarchy||!unlock.GetComponent<UiWidget>()!.Label.Contains(type==37?"25":"50"))throw new Exception("Missing paid unlock prompt");
   var numeric=scene.FindGameObject("Tuning numeric value 0")!.GetComponent<UiWidget>()!;if(!numeric.GameObject.ActiveInHierarchy||numeric.Interactable)throw new Exception("Locked tuning must be visible and disabled");
   int id=(int)tuning.GetValue(workshop)!;var before=Snapshot();set.Invoke(workshop,new object[]{0,1.4f});if(Snapshot()!=before)throw new Exception("Locked tuning changed without payment");
   Capture(scene,Path.Combine(root,$"Preview/tuning-{type}-locked.png"),false);
   int money=workshop.RecoveryPayment,cost=type==37?25:50;
   if(!workshop.UnlockTuning(type)||workshop.RecoveryPayment!=money-cost||!workshop.UnlockTuning(type)||workshop.RecoveryPayment!=money-cost)throw new Exception("Tuning charge not once per type");
   if(!numeric.Interactable)throw new Exception("Paid tuning controls remain disabled");set.Invoke(workshop,new object[]{0,1.4f});if(Snapshot()==before)throw new Exception("Paid tuning did not apply");
   Capture(scene,Path.Combine(root,$"Preview/tuning-{type}-unlocked.png"),false);workshop.Command("Delete selected");
  }
  Scenes.UnloadScene();Console.WriteLine("PASS all five upgrades: visible locked settings, payment, once-only charge and editable unlocked values");
 }
}
