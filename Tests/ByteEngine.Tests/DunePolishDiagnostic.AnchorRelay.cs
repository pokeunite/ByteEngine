using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using DuneCompany;
using DesertTerrain;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunAnchorRelay(string root)
 {
  File.Delete(Path.Combine(root,"Saves/no-brakes-progress-v1.json"));
  var scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/AnchorRelay.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);
  var w=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();
  var terrain=scene.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain3D>().Single();
  float Ground(float x,float z){terrain.TryGetSand(new(x,0,z),out var s);return s.Position.Y;}
  File.Copy(Path.Combine(root,"Assets/RecoveryBlueprints/Anchor-rescue-truck.json"),Path.Combine(root,"Saves/vehicle.json"),true);w.Command("Load vehicle");w.SelectRecoveryContract(3);Tick(scene,new(.5f),false);
  if(scene.FindGameObject("Contract row relay")?.Active!=true||!w.NoBrakesMission)throw new Exception("No Brakes missing from available jobs");
  Capture(scene,Path.Combine(root,"Preview/no-brakes-contract.png"),false);
  w.Command("Deploy contract");Tick(scene,new(.5f),false);
  var physics=(LandVehiclePhysics)typeof(DuneWorkshop3D).GetField("_physics",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(w)!;
  var ids=w.Blocks.Where(b=>b.Type==33).OrderBy(b=>b.Id).Select(b=>b.Id).ToArray();
  var anchors=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWinchAnchor3D>().ToArray();
  if(anchors.Length!=2||anchors.Any(a=>a.GameObject.Parent==null))throw new Exception("Chase anchor hierarchy incorrect");
  if(Ground(0,42)>-3.7f||Ground(0,48)<7||Ground(20,38)<0)throw new Exception("Bridge gap / escape road missing");
  Console.WriteLine($"CHASE mass={physics.RecoveryMass:F0}kg start={w.RecoveryPosition} velocity={physics.RecoveryVelocity}");
  if(physics.RecoveryMass<700||physics.Velocity.Length()<6||physics.RecoveryVelocity.Length()<6)throw new Exception("Rolling heavyweight start missing");
  if(!physics.ConnectCable(ids[0],null))throw new Exception("Speed-matched moving catch failed");
  for(int i=0;i<90;i++)Tick(scene,new(.5f),false);
  Console.WriteLine($"CHASE caught at={w.RecoveryPosition} velocity={physics.RecoveryVelocity} player={w.Transform.WorldPosition}");
  if(w.RunawayFailed||!w.WinchConnected)throw new Exception("Caught load immediately failed");
  Capture(scene,Path.Combine(root,"Preview/no-brakes-chase.png"),false);
  bool anchored=false;
  for(int i=0;i<600&&!anchored&&!w.RunawayFailed;i++){anchored=w.ConnectBlockWinch(ids[1]);if(!anchored)Tick(scene,new(.5f),false);}
  if(!anchored||!physics.IsAnchorCable(ids[1]))throw new Exception($"Bridge anchor unreachable during rolling catch: player={w.Transform.WorldPosition} load={w.RecoveryPosition} failed={w.RunawayFailed}");
  var redirectStart=w.RecoveryPosition;
  void ControlledReel(){var keys=new List<Key>{Key.Space};if(physics.WinchLength(ids[0])>5)keys.Add(Key.E);if(physics.WinchLength(ids[1])>4)keys.Add(Key.U);Tick(scene,new(.5f),false,keys.ToArray());}
  for(int i=0;i<360&&!w.RunawayFailed;i++)ControlledReel();
  Console.WriteLine($"CHASE anchor redirect from={redirectStart} to={w.RecoveryPosition} failed={w.RunawayFailed}");
  if(w.RecoveryPosition.X-redirectStart.X<2||w.RunawayFailed)throw new Exception("Bridge anchor did not redirect and secure the load");
  for(int i=0;i<720&&!w.RunawayFailed;i++)ControlledReel();
  Console.WriteLine($"CHASE dual reel secured={w.RecoveryPosition} failed={w.RunawayFailed}");
  if(w.RunawayFailed||w.RecoveryPosition.Y<1)throw new Exception("Dual cables cannot recover the swinging load onto the escape road");
  Capture(scene,Path.Combine(root,"Preview/no-brakes-anchor-recovery.png"),false);
  int money=w.RecoveryPayment;
  typeof(DuneWorkshop3D).GetMethod("ResetRecoveryPair",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,null);
  for(int i=0;i<900&&!w.RunawayFailed;i++)Tick(scene,new(.5f),false);
  Console.WriteLine($"CHASE unattended at={w.RecoveryPosition} failed={w.RunawayFailed}");
  if(!w.RunawayFailed||w.RecoveryPayment!=money)throw new Exception("Runaway failure / no-payout gate failed");
  typeof(DuneWorkshop3D).GetMethod("ResetRecoveryPair",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,null);Tick(scene,new(.5f),false);
  if(w.RunawayFailed||w.WinchConnected||physics.RecoveryVelocity.Length()<6||w.RecoveryPosition.Z<107)throw new Exception("Instant rolling restart failed");
  physics.SetTruckVelocity(new(0,0,25));physics.SetRecoveryVelocity(new(0,0,-8));
  if(physics.CanConnectCable(ids[0],null))throw new Exception("Mismatched high-speed hook allowed");
  typeof(DuneWorkshop3D).GetMethod("ResetRecoveryPair",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,null);
  typeof(DuneWorkshop3D).GetField("_runawayCaught",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(w,true); // Isolate depot payment after the earlier successful catch.
  physics.ResetTruck(new(0,Ground(0,4)+.85f,4),Quaternion.Identity);physics.ResetRecovery(new(0,Ground(0,8)+.85f,8),Quaternion.Identity);
  for(int i=0;i<90;i++)Tick(scene,new(.5f),false,Key.Space);
  if(!physics.ConnectCable(ids[0],null))throw new Exception("Yard catch failed");
  for(int i=0;i<300;i++)Tick(scene,new(.5f),false,Key.Space);
  if(!w.Building||w.RecoveryPayment!=money+1800)throw new Exception("No Brakes payment / garage return failed");
  w.SelectRecoveryContract(3);if(scene.FindGameObject("Contract row relay")?.Active==true)throw new Exception("Completed No Brakes still available");
  Console.WriteLine("PASS No Brakes: rolling catch, relative speed gate, heavy load, real gap failure, quick retry, yard stop, $1800 once, garage return");Scenes.UnloadScene();
 }
}
