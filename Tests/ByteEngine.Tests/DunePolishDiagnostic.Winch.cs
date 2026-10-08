using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunWinchFlow(string root){
  var scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/WinchRescue.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);var w=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();if(!w.WinchMission)throw new Exception("Winch scene did not deserialize mission flag");
  var blocks=new List<PlacedBlock>();void Add(int type,int parent,Vector3 p,Quaternion q){var b=new PlacedBlock{Id=blocks.Count,Type=type,Parent=parent,SpeedLimit=5,Grip=1.3f};b.Pose(p,q);blocks.Add(b);}
  Add(3,-1,Vector3.Zero,Quaternion.Identity);Add(3,0,new(0,0,2),Quaternion.Identity);Add(23,0,new(0,.5f,0),Quaternion.Identity);Add(25,1,new(0,.5f,2),Quaternion.Identity);foreach(int x in new[]{-1,1})foreach(float z in new[]{-.5f,2.5f})Add(13,z<0?0:1,new(x*.65f,.25f,z),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-x*MathF.PI*.5f));Add(33,1,new(0,.5f,2.5f),Quaternion.Identity);if(Environment.GetEnvironmentVariable("DUNE_SLOW_WINCH")=="1")blocks[^1].WinchRate=.25f;
  var file=Path.Combine(root,"Saves/vehicle.json");string original=File.ReadAllText(file);try{File.WriteAllText(file,JsonSerializer.Serialize(blocks,BuildStore.Json));w.Command("Load vehicle");}finally{File.WriteAllText(file,original);}
  w.Command("Open contracts");Capture(scene,Path.Combine(root,"Preview/winch-contract.png"),false);w.Command("Deploy contract");if(w.Building||!w.MissionActive)throw new Exception("Winch vehicle did not deploy");for(int i=0;i<180;i++)Tick(scene,new(.5f),false,Key.Space);
  if(w.ConnectWinch())throw new Exception("Long-distance winch attach succeeded");
  for(int i=0;i<2400&&w.Transform.WorldPosition.Z<21;i++){Tick(scene,new(.5f),false,w.PhysicalVelocity.Z>2.5f?Key.Space:Key.S);if(i%300==0)Console.WriteLine($"DRIVE z={w.Transform.WorldPosition.Z:F2} target={w.RecoveryPosition} velocity={w.PhysicalVelocity}");}
  for(int i=0;i<180;i++)Tick(scene,new(.5f),false,Key.Space);typeof(DuneWorkshop3D).GetField("_orbit",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(w,MathF.PI);typeof(DuneWorkshop3D).GetField("_lookTimer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(w,15f);for(int i=0;i<60;i++)Tick(scene,new(.5f),false,Key.Space);Capture(scene,Path.Combine(root,"Preview/winch-approach.png"),false);
  Console.WriteLine($"ATTACH truck={w.Transform.WorldPosition} target={w.RecoveryPosition} hint={w.RecoveryConnectionHint}");
  if(!w.ConnectWinch())throw new Exception("Winch cannot connect from the clear rescue shelf");if(w.ConnectWinch())throw new Exception("Duplicate cable accepted");w.DisconnectWinch();if(w.WinchConnected)throw new Exception("Cable did not detach");if(!w.ConnectWinch())throw new Exception("Cable cannot reconnect");
  int before=w.RecoveryPayment;bool paid=false;for(int i=0;i<3600;i++){Tick(scene,new(.5f),false,w.WinchCableLength<1 && w.RecoveryPosition.Z>23.5f && w.PhysicalVelocity.Z> -1.5f ? Key.W : Key.Space,Key.E);if(i%180==0)Console.WriteLine($"WINCH t={i/60f:F1} truck={w.Transform.WorldPosition} target={w.RecoveryPosition} length={w.WinchCableLength:F2} force={w.WinchTension:F0} objective={w.RecoveryObjective}");if(w.RecoveryPayment==before+500){paid=true;break;}}
  Capture(scene,Path.Combine(root,"Preview/winch-extracted.png"),false);if(!paid)throw new Exception("Actual winch rescue did not extract and pay");for(int i=0;i<120;i++)Tick(scene,new(.5f),false,Key.Space,Key.E);if(w.RecoveryPayment!=before+500)throw new Exception("Winch payment repeated");
  w.Command("Return to base");Tick(scene,new(.5f),false);w.Command("Deploy contract");for(int i=0;i<120;i++)Tick(scene,new(.5f),false,Key.Space);if(!w.MissionActive||w.WinchConnected||w.RecoveryPosition.Z<26)throw new Exception("Winch restart dirty");Tick(scene,new(.5f),false,Key.R);if(w.WinchConnected||!float.IsFinite(w.PhysicalVelocity.Length()))throw new Exception("Reset unsafe");w.Command("Quit mission");w.Command("Return to base");
  Console.WriteLine("PASS Workshop -> reverse to pit -> attach/detach/reattach -> reel -> extract -> $500 once -> restart -> reset -> return");
 }
}
