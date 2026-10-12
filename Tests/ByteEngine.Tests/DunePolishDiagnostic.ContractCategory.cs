using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunContractCategory(string root)
 {
  foreach(var file in new[]{"tow-progress-v1.json","winch-progress-v1.json","anchor-progress-v1.json","no-brakes-progress-v1.json","sprint-progress-v1.json","recovery-history.json","winch-history.json"})File.Delete(Path.Combine(root,"Saves",file));
  var scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/Workshop.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);
  var workshop=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();workshop.SelectRecoveryContract(3);Tick(scene,new(.5f),false);
  string[] rows=["Contract row tow","Contract row winch","Contract row anchor","Contract row relay"];
  var category=scene.FindGameObject("Contracts recovery category")??throw new Exception("Recovery category missing");
  if(category.Parent!=scene.FindGameObject("Contracts overlay")||category.GetComponent<UiText>()!.Text!="RECOVERY OPERATIONS / 4 JOBS"||rows.Any(n=>scene.FindGameObject(n)?.ActiveInHierarchy!=true))throw new Exception("Available jobs are not grouped together");
  var details=scene.FindGameObject("Contract details")!.GetComponent<UiWidget>()!;var panel=scene.FindGameObject("Contracts overlay")!.GetComponent<UiWidget>()!;
  if(details.Offset.Y+details.Size.Y>panel.Size.Y)throw new Exception("Expanded contract spills out of the category panel");
  Capture(scene,Path.Combine(root,"Preview/contracts-recovery-available.png"),false);
  File.WriteAllText(Path.Combine(root,"Saves/tow-progress-v1.json"),"{\"SchemaVersion\":1,\"ContractId\":\"stranded-buggy\",\"Completions\":1}");
  workshop.SelectRecoveryContract(1);if(scene.FindGameObject(rows[0])!.Active||category.GetComponent<UiText>()!.Text!="RECOVERY OPERATIONS / 3 JOBS")throw new Exception("Completed job remains available");
  typeof(DuneWorkshop3D).GetField("_completedContracts",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(workshop,true);workshop.SelectRecoveryContract(0);Tick(scene,new(.5f),false);
  if(category.GetComponent<UiText>()!.Text!="RECOVERY OPERATIONS / 1 JOB"||!scene.FindGameObject(rows[0])!.ActiveInHierarchy||rows.Skip(1).Any(n=>scene.FindGameObject(n)!.Active)||scene.FindGameObject("Deploy contract")!.Active)throw new Exception("Completed category filtering/deployment failed");
  Capture(scene,Path.Combine(root,"Preview/contracts-recovery-completed.png"),false);Scenes.UnloadScene();Console.WriteLine("PASS Recovery Operations: all four available, completed filtering, expanded brief bounds and category hierarchy");
 }
}
