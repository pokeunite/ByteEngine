using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic{
 void RunUnifiedContracts(string root,Scene scene,DuneWorkshop3D workshop){
 var dir=ByteEngine.Core.Runtime.GameSaveStorage.GetDirectory(root);var files=new[]{"tow-progress-v1.json","winch-progress-v1.json","vehicle.json"};var backup=files.ToDictionary(n=>n,n=>File.Exists(Path.Combine(dir,n))?File.ReadAllBytes(Path.Combine(dir,n)):null);
 try{
 void Click(Scene sc,string name){var o=sc.FindGameObject(name)!;var w=o.GetComponent<UiWidget>()!;var r=UiLayout.Resolve(o,w.Anchor,w.Offset,w.Size,new(1280,720));var pt=(r.Position+r.Size*.5f)/new Vector2(1280,720);Tick(sc,pt,false);Tick(sc,pt,true);Tick(sc,pt,false);}
 void AssertRows(Scene sc,bool tow,bool winch){if(sc.FindGameObject("Contract row tow")!.ActiveInHierarchy!=tow||sc.FindGameObject("Contract row winch")!.ActiveInHierarchy!=winch)throw new Exception("Wrong unified contract filtering");if(sc.FindGameObject("Choose winch contract")?.ActiveInHierarchy==true)throw new Exception("Old mission tabs remain");}
 if(ByteEngine.Core.Runtime.GameSaveStorage.IsRuntimeProject(root)){foreach(var name in files.Take(2))if(File.Exists(Path.Combine(dir,name)))File.Delete(Path.Combine(dir,name));workshop.Command("Open contracts");Tick(scene,new(.5f),false);AssertRows(scene,true,true);Console.WriteLine("PASS Windows fresh progress ignores legacy completion and payment files");}
 File.WriteAllText(Path.Combine(dir,files[0]),"{\"SchemaVersion\":1,\"ContractId\":\"stranded-buggy\",\"Completions\":0}");File.WriteAllText(Path.Combine(dir,files[1]),"{\"SchemaVersion\":1,\"ContractId\":\"well-truck-rescue\",\"Completions\":0}");workshop.Command("Open contracts");Tick(scene,new(.5f),false);AssertRows(scene,true,true);Capture(scene,Path.Combine(root,"Preview/unified-contracts-available.png"),false);
 typeof(DuneWorkshop3D).GetMethod("RecordContractCompletion",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(workshop,null);AssertRows(scene,false,true);Click(scene,"Contracts completed");AssertRows(scene,true,false);Capture(scene,Path.Combine(root,"Preview/unified-contracts-completed.png"),false);Click(scene,"Contracts available");AssertRows(scene,false,true);
 Click(scene,"Contract row winch");var requested=typeof(Scene).GetProperty("LoadRequested",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(scene) as string;if(requested!="Scenes/WinchRescue.bytescene")throw new Exception("Winch row click failed");
 var next=_project!.Scenes.Load(Path.Combine(root,requested));Scenes.LoadScene(next);Tick(next,new(.5f),false);var wnext=next.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();if(!wnext.WinchMission||!next.FindGameObject("Contract details")!.ActiveInHierarchy)throw new Exception("Winch details missing");AssertRows(next,false,true);Capture(next,Path.Combine(root,"Preview/unified-contracts-winch-details.png"),false);
 typeof(DuneWorkshop3D).GetMethod("RecordContractCompletion",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(wnext,null);AssertRows(next,false,false);Click(next,"Contracts completed");AssertRows(next,true,true);Capture(next,Path.Combine(root,"Preview/unified-contracts-both-completed.png"),false);
 Click(next,"Contracts available");AssertRows(next,false,false);Console.WriteLine("PASS Fresh list has both jobs; tow completion moves only tow; winch opens correct scene and details; winch completion leaves Available empty and both under Completed; saved history filtering re-read on scene load.");
 }finally{foreach(var name in files){var path=Path.Combine(dir,name);if(backup[name] is {} bytes)File.WriteAllBytes(path,bytes);else if(File.Exists(path))File.Delete(path);}}
 }
}
