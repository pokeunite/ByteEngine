using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using Downhill;
internal sealed class ControllerCheck
{
 internal readonly Scene Scene=new("Controller regression"){FixedSimulation=true};
 readonly BoardController3D board;float brakeStart,slidePeak,cameraWorst=1;
 internal ControllerCheck(){Scene.SimulationClock.StepSeconds=1d/120;var floor=Scene.CreateGameObject("Test road");floor.AddComponent(new BoxCollider3D{Size=new(300,.2f,300),Center=new(0,-.1f,0)});var root=Scene.CreateGameObject("Board");root.Transform.LocalPosition=new(0,.13f,0);foreach(float x in new[]{-.155f,.155f})foreach(float z in new[]{-.38f,.38f})root.AddComponent(new CapsuleCollider3D{Radius=.037f,Height=.074f,Center=new(x,-.077f,z)});root.AddComponent(new Rigidbody3D{Mass=80,AllowSleep=false,ContinuousCollision=true,Friction=0,LinearDamping=0});board=root.AddComponent(new BoardController3D{ReadPlayerInput=false});var c=Scene.CreateGameObject("Camera");c.AddComponent(new Camera3D{ActiveGameCamera=true,FarClip=400});c.AddComponent(new BoardCamera3D());}
 internal bool Step(int frame){
  if(frame==1)board.Body!.Velocity=-Vector3.UnitZ*15;
  if(frame==60)brakeStart=board.Speed;
  if(frame>=61&&frame<=180)board.BrakeInput=true;
  if(frame==180){Check(board.Speed<brakeStart-6,"Brake does not decelerate");Check(Vector3.Dot(board.Transform.Forward,-Vector3.UnitZ)>.9999f,"Straight braking changes heading");Console.WriteLine($"PASS straight brake {brakeStart:0.00} -> {board.Speed:0.00} m/s, no yaw");}
  if(frame==181){board.ResetAt(new(0,.13f,0),Quaternion.Identity);board.Body!.Velocity=-Vector3.UnitZ*12;board.BrakeInput=false;}
  if(frame>=181&&frame<=240){board.SlideInput=true;board.SteerInput=1;slidePeak=Math.Max(slidePeak,Math.Abs(board.SlipAngle));}
  if(frame>210&&frame<240){var camera=Scene.FindGameObject("Camera")!;var behind=board.Transform.WorldPosition-camera.Transform.WorldPosition;behind.Y=0;var travel=board.Body!.Velocity;travel.Y=0;if(behind.LengthSquared()>.1f&&travel.LengthSquared()>.1f)cameraWorst=Math.Min(cameraWorst,Vector3.Dot(Vector3.Normalize(behind),Vector3.Normalize(travel)));}
  if(frame==241){Check(slidePeak>15,"Slide does not break lateral grip");board.SlideInput=false;board.SteerInput=0;}
  if(frame==390){Console.WriteLine($"RECOVERY side={board.SideSpeed} angle={board.SlipAngle} sliding={board.IsSliding} cameraDot={cameraWorst}");Check(Math.Abs(board.SideSpeed)<.7f,"Slide cannot regain grip after release");Check(cameraWorst>.5f,"Camera orbits with sliding heading instead of travel");Console.WriteLine($"PASS slide peak {slidePeak:0.0} deg, released grip recovered");}
  if(frame==391){board.ResetAt(new(0,.13f,0),Quaternion.Identity);board.Body!.Velocity=-Vector3.UnitZ*27.78f;slidePeak=0;}
  if(frame>=391&&frame<=570){board.SlideInput=true;board.SteerInput=1;slidePeak=Math.Max(slidePeak,Math.Abs(board.SlipAngle));}
  if(frame==571){Console.WriteLine($"HIGH SPEED slide angle={slidePeak:0.0} speed={board.Speed:0.00}");Check(slidePeak<70,"Sustained slide spins through 90 degrees");Check(board.Speed<21,"High speed slide does not scrub speed");board.SlideInput=false;board.SteerInput=0;}
  if(frame==661){Check(Math.Abs(board.SlipAngle)<5&&Math.Abs(board.SideSpeed)<1,"High speed slide release fails to straighten");Console.WriteLine($"PASS 100 km/h entry, bounded slide, speed scrub and 1.5 s release recovery: {board.SlipAngle:0.0} deg");return true;}return false;
 }
 static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
}
