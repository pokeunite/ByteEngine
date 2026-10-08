using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunRecoveryFlow(Scene scene,DuneWorkshop3D workshop,string root,bool heavy=false)
 {
  void Check(bool pass,string message){if(!pass)throw new Exception(message);Console.WriteLine("PASS "+message);}
  Console.WriteLine("MODEL steering="+workshop.PhysicsDefinition(18)+" wheel="+workshop.PhysicsDefinition(13));
  // Use a centred, four-equal-tyre reference truck; never overwrite the user's vehicle file.
  var blocks=new List<PlacedBlock>();void Add(int type,int parent,Vector3 pos,Quaternion q,bool moving=false){var b=new PlacedBlock{Id=blocks.Count,Type=type,Parent=parent,MovingMount=moving,SpeedLimit=5,Grip=1.2f};b.Pose(pos,q);blocks.Add(b);}
  Add(3,-1,new(0,0,0),Quaternion.Identity);Add(3,0,new(0,0,2),Quaternion.Identity);Add(heavy?24:23,0,new(0,.5f,0),Quaternion.Identity);Add(25,1,new(0,.57f,2),Quaternion.Identity);
  foreach(int x in new[]{-1,1}){Add(18,0,new(x*.501f,.25f,-.5f),x<0?new Quaternion(-.5f,.5f,.5f,.5f):new Quaternion(-.5f,-.5f,-.5f,.5f));int h=blocks.Last().Id;Add(13,h,new(x*1.43f,.25f,-.5f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-x*MathF.PI*.5f),true);Add(13,1,new(x*.588f,.25f,2.5f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-x*MathF.PI*.5f));}
  Add(20,1,new(0,0,3.09f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI));
  Directory.CreateDirectory(Path.Combine(root,"Design/Recovery"));File.WriteAllText(Path.Combine(root,heavy?"Design/Recovery/Heavy-recovery-truck.json":"Design/Recovery/Reference-truck.json"),JsonSerializer.Serialize(blocks,BuildStore.Json));
  var file=Path.Combine(root,"Saves/vehicle.json");string original=File.ReadAllText(file);try{File.WriteAllText(file,JsonSerializer.Serialize(blocks,BuildStore.Json));workshop.Command("Load vehicle");}finally{File.WriteAllText(file,original);}
  workshop.Command("Deploy contract");Check(!workshop.Building&&workshop.MissionActive,"Workshop deploys recovery job with a trailer hinge");for(int i=0;i<240;i++)Tick(scene,new(.5f),false,Key.Space);
  Capture(scene,Path.Combine(root,"Preview/recovery-start.png"),false);
  bool ready=false;for(int i=0;i<2400;i++){
   if(i%120==0)Console.WriteLine($"APPROACH t={i/60f:F1}s truck={workshop.Transform.WorldPosition} target={workshop.RecoveryPosition} hint={workshop.RecoveryConnectionHint} hitch={workshop.RecoveryTruckHitch} targetHitch={workshop.RecoveryHitchPosition} speed={workshop.PhysicalVelocity.Length():F2}");
   if(workshop.RecoveryConnectionHint=="H connect hitch"){ready=true;break;}
   float gap=workshop.RecoveryHitchPosition.Z-workshop.RecoveryTruckHitch.Z;float speed=workshop.PhysicalVelocity.Z;
   var forward=Vector3.Transform(-Vector3.UnitZ,workshop.Transform.WorldRotation);float yaw=MathF.Atan2(-forward.X,-forward.Z);float dx=workshop.RecoveryHitchPosition.X-workshop.RecoveryTruckHitch.X;float desired=Math.Clamp(MathF.Atan2(dx,Math.Max(3,gap)), -.25f,.25f);float error=MathF.Atan2(MathF.Sin(desired-yaw),MathF.Cos(desired-yaw));
   if(gap<.18f||speed>Math.Clamp(gap*.8f,.12f,2))Tick(scene,new(.5f),false,Key.Space);else if(error>.025f)Tick(scene,new(.5f),false,Key.S,Key.D);else if(error<-.025f)Tick(scene,new(.5f),false,Key.S,Key.A);else Tick(scene,new(.5f),false,Key.S);
  }
  Capture(scene,Path.Combine(root,"Preview/recovery-align.png"),false);Check(ready,"Driving inputs reach a valid close hitch position");
  Tick(scene,new(.5f),false,Key.H);Tick(scene,new(.5f),false);Check(workshop.RecoveryConnected,"H connects the actual separate buggy");Capture(scene,Path.Combine(root,"Preview/recovery-connected.png"),false);
  Tick(scene,new(.5f),false,Key.H);Tick(scene,new(.5f),false);Check(!workshop.RecoveryConnected,"H deliberately detaches");Tick(scene,new(.5f),false,Key.H);Tick(scene,new(.5f),false);Check(workshop.RecoveryConnected,"H reattaches while aligned");
  int before=workshop.RecoveryPayment;bool paid=false;for(int i=0;i<3000;i++){
   float remaining=workshop.RecoveryPosition.Z;bool braking=remaining<3.6f&&Math.Abs(workshop.RecoveryPosition.X)<4.3f;var direction=new Vector3(0,0,-6)-workshop.Transform.WorldPosition;var forward=Vector3.Transform(-Vector3.UnitZ,workshop.Transform.WorldRotation);float desired=MathF.Atan2(-direction.X,-direction.Z),yaw=MathF.Atan2(-forward.X,-forward.Z);float error=MathF.Atan2(MathF.Sin(desired-yaw),MathF.Cos(desired-yaw));if(braking)Tick(scene,new(.5f),false,Key.Space);else if(error<-.06f)Tick(scene,new(.5f),false,Key.W,Key.D);else if(error>.06f)Tick(scene,new(.5f),false,Key.W,Key.A);else Tick(scene,new(.5f),false,Key.W);
   if(i%180==0)Console.WriteLine($"TOW {(heavy?"heavy":"compact")} {i/60f:F1}s truck={workshop.Transform.WorldPosition} buggy={workshop.RecoveryPosition} v={workshop.PhysicalVelocity} objective={workshop.RecoveryObjective}");
   if(workshop.RecoveryPayment==before+250){paid=true;break;}
  }
  Capture(scene,Path.Combine(root,"Preview/recovery-delivered.png"),false);Check(paid&&!workshop.MissionActive,"Tow delivery completes and pays exactly $250");for(int i=0;i<180;i++)Tick(scene,new(.5f),false,Key.Space);Check(workshop.RecoveryPayment==before+250,"Payment cannot repeat while waiting in delivery zone");
  workshop.Command("Return to base");Tick(scene,new(.5f),false);Check(workshop.Building,"Completed job returns to workshop");workshop.Command("Deploy contract");Tick(scene,new(.5f),false);Check(workshop.MissionActive&&!workshop.RecoveryConnected&&workshop.RecoveryPosition.Z>15,"Restart spawns a fresh disconnected target");var physics=(LandVehiclePhysics)typeof(DuneWorkshop3D).GetField("_physics",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(workshop)!;int restartBalance=workshop.RecoveryPayment;physics.ResetRecovery(new(0,.7f,0),Quaternion.Identity);for(int i=0;i<120;i++)Tick(scene,new(.5f),false,Key.Space);Check(workshop.RecoveryPayment==restartBalance,"Unconnected buggy in delivery zone cannot trigger payment");Tick(scene,new(.5f),false,Key.R);Check(!workshop.RecoveryConnected&&float.IsFinite(workshop.PhysicalVelocity.LengthSquared()),"Recovery reset remains safe in the playable contract");workshop.Command("Quit mission");workshop.Command("Return to base");Tick(scene,new(.5f),false);
 }
}
