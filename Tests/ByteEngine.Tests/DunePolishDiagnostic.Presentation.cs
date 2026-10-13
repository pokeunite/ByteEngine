using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunPresentation(string root){
  if(!Path.GetFullPath(root).StartsWith(Path.GetFullPath(".artifacts")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Private fixture required");
  Directory.CreateDirectory(Path.Combine(root,"Saves"));File.Copy(Path.Combine(root,"Assets/RecoveryBlueprints/Recovery-demo.json"),Path.Combine(root,"Saves/vehicle.json"),true);File.WriteAllText(Path.Combine(root,"Saves/workshop-tutorial.json"),"true");
  var scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/Workshop.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);
  var workshop=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();workshop.Command("Load vehicle");
  const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
  object? Call(string method,params object[] args)=>typeof(DuneWorkshop3D).GetMethods(flags).First(m=>m.Name==method&&m.GetParameters().Length==args.Length&&m.GetParameters().Zip(args).All(p=>p.First.ParameterType.IsInstanceOfType(p.Second))).Invoke(workshop,args);
  for(int i=0;i<12;i++)Tick(scene,new(.5f),false);Capture(scene,Path.Combine(root,"Preview/polish-garage.png"),false);
  var canvases=scene.GameObjects.Where(o=>o.GetComponent<UiCanvas>()!=null).Select(o=>(o,o.Active)).ToArray();foreach(var (o,_) in canvases)o.Active=false;Capture(scene,Path.Combine(root,"Preview/polish-garage-clean.png"),false);foreach(var (o,active) in canvases)o.Active=active;
  Call("SetGarageEditing",true);Tick(scene,new(.5f),false);Capture(scene,Path.Combine(root,"Preview/polish-builder.png"),false);
  Call("SetGarageEditing",false);workshop.Command("Open contracts");Call("SelectRecoveryContract",0);typeof(DuneWorkshop3D).GetField("_contractExpanded",flags)!.SetValue(workshop,true);Call("RefreshContractBrowser");Tick(scene,new(.5f),false);Capture(scene,Path.Combine(root,"Preview/polish-contracts.png"),false);
  foreach(var viewport in new[]{new Vector2(1280,720),new(800,600),new(1920,1080),new(2560,1080)}){
   Input.SetGameViewPointer(new(.5f),viewport,true);Call("ResizeWorkshopV3");
   var names=new[]{"Contracts overlay","Contract details","Deploy contract","Garage wallet card"};
   foreach(string name in names){var o=scene.FindGameObject(name)!;var w=o.GetComponent<UiWidget>()!;var rect=UiLayout.Resolve(o,w.Anchor,w.Offset,w.Size,viewport);if(rect.Position.X<-.1f||rect.Position.Y<-.1f||rect.Position.X+rect.Size.X>viewport.X+.1f||rect.Position.Y+rect.Size.Y>viewport.Y+.1f)throw new Exception($"{name} outside {viewport}: {rect}");}
  }
  Input.SetGameViewPointer(new(.5f),new(1280,720),true);Call("ResizeWorkshopV3");Call("OpenGarageOverlay","Settings overlay");Tick(scene,new(.5f),false);Capture(scene,Path.Combine(root,"Preview/polish-settings.png"),false);
  var keys=(Dictionary<string,Key>)typeof(DuneWorkshop3D).GetField("_driveKeys",flags)!.GetValue(workshop)!;keys["Forward"]=Key.Up;Call("SavePolishPreferences");Call("CloseGarageOverlay");workshop.Command("Deploy contract");for(int i=0;i<180;i++)Tick(scene,new(.5f),false,Key.Up);if(workshop.Building||Math.Abs(workshop.Speed)<.5)throw new Exception("Rebound Forward does not drive the vehicle");Capture(scene,Path.Combine(root,"Preview/polish-driving.png"),false);
  if(!File.ReadAllText(Path.Combine(root,"Saves/menu-settings.json")).Contains("Up"))throw new Exception("Control binding was not saved");
  var casting=scene.GameObjects.FirstOrDefault(o=>o.Name=="Engine_Pulley"||o.Name=="Moving_EnginePulley");if(casting==null)throw new Exception("Animated pulley geometry missing");
  typeof(DuneWorkshop3D).GetField("_contractComplete",flags)!.SetValue(workshop,true);Tick(scene,new(.5f),false);Capture(scene,Path.Combine(root,"Preview/polish-result.png"),false);for(int i=0;i<170;i++)Tick(scene,new(.5f),false);if(!workshop.Building)throw new Exception("Reward did not return automatically to garage");
  Console.WriteLine("PASS polished contract layout at four aspect ratios, settings, saved driving binding, driving and rendered native graphics");Scenes.UnloadScene();Close();
 }
}
