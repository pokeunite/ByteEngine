using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using DuneCompany;
using DesertTerrain;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunStarterLevels(string root)
 {
  if(!Path.GetFullPath(root).StartsWith(Path.GetFullPath(".artifacts")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Use an isolated diagnostic fixture, never player saves");
  if(Environment.GetEnvironmentVariable("DUNE_CONTRACT_CATEGORY_ONLY")=="1"){RunContractCategory(root);Close();return;}
  if(Environment.GetEnvironmentVariable("DUNE_NO_BRAKES_ONLY")=="1"){RunAnchorRelay(root);Close();return;}
  if(Environment.GetEnvironmentVariable("DUNE_RACING_ONLY")=="1"){RunSprintAndEconomy(root);Close();return;}
  if(Environment.GetEnvironmentVariable("DUNE_SPRING_STABILITY_ONLY")=="1"){RunSpringStability(root);return;}
  if(Environment.GetEnvironmentVariable("DUNE_UPGRADE_TUNING_ONLY")=="1"){RunUpgradeTuning(root);Close();return;}
  foreach(var name in new[]{"workshop-economy-v1.json","winch-progress-v1.json","tow-progress-v1.json","recovery-payment.json"})File.Delete(Path.Combine(root,"Saves",name));
  foreach(bool winch in new[]{false,true})
  {
   var scene=_project!.Scenes.Load(Path.Combine(root,winch?"Scenes/WinchRescue.bytescene":"Scenes/Workshop.bytescene"));
   Scenes.LoadScene(scene);Tick(scene,new(.5f),false);
   var terrain=scene.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain3D>().Single();
   float Ground(float x,float z){if(!terrain.TryGetSand(new(x,0,z),out var s))throw new Exception("Route outside terrain");return s.Position.Y;}
   if(terrain.UseReferenceSandSurface||terrain.HeightmapPath.Length==0)throw new Exception("Landscape is still flat reference surface");
   if(Math.Abs(Ground(0,0))>.02f)throw new Exception("Garage ground changed");
   if(winch && Ground(18.5f,70)-Ground(12,70)<4.5f)throw new Exception("Deep extraction pit is missing");
   var points=winch?new Vector2[]{new(0,0),new(-15,15),new(-20,33),new(0,45),new(12,59)}:new Vector2[]{new(0,0),new(0,18),new(12,35),new(-10,52),new(0,70)};
   float maxSlope=0;
   foreach(var pair in points.Zip(points.Skip(1))){int count=(int)Vector2.Distance(pair.First,pair.Second)*2;for(int i=0;i<count;i++){var a=Vector2.Lerp(pair.First,pair.Second,i/(float)count);var b=Vector2.Lerp(pair.First,pair.Second,(i+1)/(float)count);maxSlope=Math.Max(maxSlope,Math.Abs(Ground(a.X,a.Y)-Ground(b.X,b.Y))/Vector2.Distance(a,b));}}
   if(maxSlope>.35f)throw new Exception($"Starter route too steep: {maxSlope}");
   var workshop=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();
   var save=Path.Combine(root,"Saves/vehicle.json");Directory.CreateDirectory(Path.GetDirectoryName(save)!);File.Copy(Path.Combine(root,"Assets/RecoveryBlueprints",winch?"Winch-rescue-truck.json":"Recovery-demo.json"),save,true);workshop.Command("Load vehicle");workshop.Command("Deploy contract");Tick(scene,new(.5f),false);
   var field=typeof(DuneWorkshop3D).GetField("_physics",BindingFlags.NonPublic|BindingFlags.Instance)!;var physics=(LandVehiclePhysics)field.GetValue(workshop)!;
   if(winch){for(int i=0;i<60;i++)Tick(scene,new(.5f),false,Key.Space);var beforeCamera=Vector3.Normalize(workshop.CameraPosition-workshop.Transform.WorldPosition);for(int i=0;i<30;i++)Tick(scene,new(.5f),false,Key.S);var afterCamera=Vector3.Normalize(workshop.CameraPosition-workshop.Transform.WorldPosition);if(Vector3.Dot(beforeCamera,afterCamera)<.5f)throw new Exception("Reverse flipped the follow camera");Console.WriteLine("PASS Reverse retains follow-camera side");}
   var cam=scene.ActiveCamera!;
   if(!winch){for(int i=0;i<60;i++)Tick(scene,new(.5f),false,Key.Space);var start=workshop.Transform.WorldPosition;for(int i=0;i<150;i++)Tick(scene,new(.5f),false,Key.W);var patch=terrain.SimulationPatch??throw new Exception("Vehicle did not enable the fine sand field");var tiles=(System.Collections.IDictionary)typeof(SandSimulationPatch3D).GetField("_tiles",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(patch)!;float deepest=-tiles.Values.Cast<object>().SelectMany(t=>(float[])t.GetType().GetField("Delta")!.GetValue(t)!).Min();if(deepest<.04f||Vector3.Distance(start,workshop.Transform.WorldPosition)<2)throw new Exception($"Actual driving did not create visible tracks: {deepest:F3}m");var center=(start+workshop.Transform.WorldPosition)*.5f;cam.Transform.WorldPosition=center+new Vector3(5,5,6);var f=Vector3.Normalize(center-cam.Transform.WorldPosition);cam.Transform.WorldRotation=Quaternion.CreateFromYawPitchRoll(MathF.Atan2(-f.X,-f.Z),MathF.Asin(f.Y),0);Capture(scene,Path.Combine(root,"Preview/actual-wheel-grooves.png"),false);Console.WriteLine($"PASS Actual driving creates {deepest:F3}m physical grooves");}
   foreach(var (label,pos,target) in new[]{("overview",new Vector3(58,53,-8),new Vector3(0,1,40)),("site",winch?new Vector3(30,17,49):new Vector3(23,13,48),winch?new Vector3(12,3,70):new Vector3(0,1,70))}){
    cam.Transform.WorldPosition=pos;var f=Vector3.Normalize(target-pos);cam.Transform.WorldRotation=Quaternion.CreateFromYawPitchRoll(MathF.Atan2(-f.X,-f.Z),MathF.Asin(f.Y),0);Capture(scene,Path.Combine(root,$"Preview/{(winch?"extraction":"riverbed")}-{label}.png"),false);
   }
   // Job-site extraction must never award money; only delivery can do so.
   int before=workshop.RecoveryPayment;
   physics.ResetRecovery(new(winch?12:0,Ground(winch?12:0,60)+.7f,60),Quaternion.Identity);
   for(int i=0;i<90;i++)Tick(scene,new(.5f),false,Key.Space);
   if(workshop.RecoveryPayment!=before)throw new Exception("Paid at job site instead of depot");
   if(winch)
   {
    physics.ResetTruck(new(12,Ground(12,60)+1.1f,60),Quaternion.Identity);
    physics.ResetRecovery(new(12,Ground(12,70)+.65f,70),Quaternion.Identity);
    for(int i=0;i<90;i++)Tick(scene,new(.5f),false,Key.Space);
    if(!workshop.ConnectWinch())throw new Exception("Deep pit cable cannot connect from the south ramp approach");
    var pitStart=workshop.RecoveryPosition;
    for(int i=0;i<720;i++)Tick(scene,new(.5f),false,Key.Space,Key.E);
    float pulled=pitStart.Z-workshop.RecoveryPosition.Z;
    if(pulled<2)throw new Exception($"Deep pit winch did not move casualty up ramp: {pulled}; player={workshop.Transform.WorldPosition} load={workshop.RecoveryPosition} tension={physics.CableTension}");
    Console.WriteLine($"PASS Deep pit cable clears ramp and physically pulls casualty {pulled:F2}m");
    workshop.DisconnectWinch();
    typeof(DuneWorkshop3D).GetField("_winchExtracted",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(workshop,false);
    typeof(DuneWorkshop3D).GetField("_winchWorkedAtPit",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(workshop,false);
    // Use reset poses to isolate completion gates from steering in this diagnostic.
    physics.ResetTruck(new(0,1.1f,-3),Quaternion.Identity);
    physics.ResetRecovery(new(0,.65f,1),Quaternion.Identity);
    for(int i=0;i<90;i++)Tick(scene,new(.5f),false,Key.Space);
    if(!workshop.ConnectWinch())throw new Exception("Depot cable could not connect for completion check");
    for(int i=0;i<90;i++)Tick(scene,new(.5f),false,Key.Space);
    if(workshop.RecoveryPayment!=before)throw new Exception("Depot paid before extraction milestone");
    typeof(DuneWorkshop3D).GetField("_winchExtracted",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(workshop,true);
    for(int i=0;i<150;i++)Tick(scene,new(.5f),false,Key.Space);
    if(workshop.RecoveryPayment!=before+500)throw new Exception("Delivered extracted vehicle did not pay once");
    for(int i=0;i<120;i++)Tick(scene,new(.5f),false,Key.Space);
    if(workshop.RecoveryPayment!=before+500)throw new Exception("Delivery payment repeated");
    Console.WriteLine("PASS Extraction milestone + stationary depot delivery + one payment");
   }
   if(winch){for(int i=0;i<180;i++)Tick(scene,new(.5f),false,Key.Space);
   if(!workshop.Building)throw new Exception("Paid contract did not automatically return to garage");
   Console.WriteLine("PASS Reward followed by automatic garage return");}
   Console.WriteLine($"PASS {(winch?"extraction":"riverbed")}: native render, route slope {maxSlope:F3}, pit/garage collision heights, no site payout");
   workshop.Command("Return to base");Scenes.UnloadScene();
  }
  if(File.Exists(Path.Combine(root,"Scenes/AnchorRescue.bytescene")))RunAnchorRescue(root);
  if(File.Exists(Path.Combine(root,"Scenes/AnchorRelay.bytescene")))RunAnchorRelay(root);
  _project!.Dispose();Close();
 }
}
