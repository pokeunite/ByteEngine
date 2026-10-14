using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Gameplay;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunConvoy(string root)
 {
  File.Delete(Path.Combine(root,"Saves/convoy-progress-v1.json"));
  var economyFile=Path.Combine(root,"Saves/workshop-economy-v1.json");if(File.Exists(economyFile)){var economy=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(economyFile))!;if(economy["Rewards"] is System.Text.Json.Nodes.JsonArray rewards)foreach(var reward in rewards.Where(v=>v?.GetValue<string>()=="convoy-breaker").ToArray())rewards.Remove(reward);File.WriteAllText(economyFile,economy.ToJsonString());}
  var scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/ConvoyBreaker.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);
  var w=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  void Set(string name,object value)=>typeof(DuneWorkshop3D).GetField(name,flags)!.SetValue(w,value);
  T Get<T>(string name)=>(T)typeof(DuneWorkshop3D).GetField(name,flags)!.GetValue(w)!;
  Set("_tutorialStep",-1);File.Copy(Path.Combine(root,"Assets/RecoveryBlueprints/Recovery-demo.json"),Path.Combine(root,"Saves/vehicle.json"),true);w.Command("Load vehicle");
  if(!w.Blocks.Any(b=>b.Type==30))w.AddBlock(30,0,new(0,1.2f,-.6f),Quaternion.Identity);
  if(!w.Blocks.Any(b=>b.Type==33))w.AddBlock(33,0,new(0,.8f,2.7f),Quaternion.Identity);
  w.SelectRecoveryContract(8);Tick(scene,new(.5f),false);if(scene.FindGameObject("Contract row convoy")?.ActiveInHierarchy!=true)throw new Exception("Combat category has no convoy contract");Capture(scene,Path.Combine(root,"Preview/Convoy-brief.png"),false);
  w.Command("Deploy contract");for(int i=0;i<10;i++)Tick(scene,new(.5f),false);
  if(w.Building)throw new Exception("Convoy cannot deploy");var physics=Get<LandVehiclePhysics>("_physics");var start=w.RecoveryPosition;
  var targets=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneBountyTarget3D>().Where(t=>t.GameObject.Name.Contains("target")).ToArray();if(targets.Length!=3||!physics.HasRecovery)throw new Exception("Convoy needs cargo and two escorts in shared physics");
  for(int i=0;i<300;i++)Tick(scene,new(.5f),false,Key.Space);
  Console.WriteLine($"CONVOY moved={Vector3.Distance(start,w.RecoveryPosition):0.0} position={w.RecoveryPosition} velocity={physics.RecoveryVelocity}");
  if(Vector3.Distance(start,w.RecoveryPosition)<5)throw new Exception("Convoy cargo does not move with real wheel motors");
  physics.ResetTruck(w.RecoveryPosition+new Vector3(0,1,15),Quaternion.Identity);foreach(var target in targets)target.ResetTarget();float health=targets.Sum(t=>t.Health);
  for(int i=0;i<100;i++){w.FireWeapon(false);Tick(scene,new(.5f),false,Key.Space);}if(targets.Sum(t=>t.Health)>=health)throw new Exception("Existing machine gun cannot damage convoy weakpoints");Console.WriteLine("PASS actual machine-gun weakpoint damage");
  Capture(scene,Path.Combine(root,"Preview/Convoy-combat.png"),false);
  foreach(var ui in scene.GameObjects.Where(o=>o.GetComponent<UiCanvas>()!=null))ui.Active=false;
  var camera=scene.ActiveCamera!;var targetPoint=w.RecoveryPosition+Vector3.UnitY;var eye=targetPoint+new Vector3(-15,12,18);camera.Transform.WorldPosition=eye;var forward=Vector3.Normalize(targetPoint-eye);camera.Transform.WorldRotation=Quaternion.CreateFromYawPitchRoll(MathF.Atan2(-forward.X,-forward.Z),MathF.Asin(forward.Y),0);Capture(scene,Path.Combine(root,"Preview/Convoy-field.png"),false);
  camera.Transform.WorldPosition=new(150,180,25);forward=Vector3.Normalize(new Vector3(10,0,-145)-camera.Transform.WorldPosition);camera.Transform.WorldRotation=Quaternion.CreateFromYawPitchRoll(MathF.Atan2(-forward.X,-forward.Z),MathF.Asin(forward.Y),0);Capture(scene,Path.Combine(root,"Preview/Convoy-overview.png"),false);
  foreach(var ui in scene.GameObjects.Where(o=>o.GetComponent<UiCanvas>()!=null))ui.Active=true;
  var engine=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneBountyTarget3D>().Single(t=>t.GameObject.Name=="Convoy exposed engine target");engine.Damage(1000);Tick(scene,new(.5f),false);if(Get<ByteEngine.Core.AI.AiAgent3D>("_convoyBrain").Running)throw new Exception("Disabled truck keeps accelerating");
  Set("_convoyHull",0f);Tick(scene,new(.5f),false);if(!Get<bool>("_convoyFailed"))throw new Exception("Destroyed rig does not fail");w.Command("Recover vehicle");for(int i=0;i<8;i++)Tick(scene,new(.5f),false);if(Get<bool>("_convoyFailed")||scene.GameObjects.Count(o=>o.Name.StartsWith("Convoy escort "))!=2)throw new Exception("Retry failed or duplicated convoy actors");
  physics=Get<LandVehiclePhysics>("_physics");foreach(var target in scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneBountyTarget3D>().Where(t=>t.GameObject.Name.Contains("target")))target.Damage(1000);
  var towStart=w.RecoveryPosition;physics.ResetTruck(towStart+new Vector3(0,.3f,-8),Quaternion.Identity);Tick(scene,new(.5f),false);int winch=w.Blocks.First(b=>b.Type==33).Id;if(!w.ConnectBlockWinch(winch))throw new Exception("Cannot attach for physical tow");for(int i=0;i<240;i++)Tick(scene,new(.5f),false,Key.W);if(Vector3.Distance(towStart,w.RecoveryPosition)<3||!w.WinchConnected)throw new Exception("Disabled truck remains braked or cannot be physically towed");Console.WriteLine("PASS disabled truck rolls under a real secured tow");w.DisconnectWinch();
  var job=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneConvoyContract3D>().Single();var sand=scene.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain.DesertTerrain3D>().Single();sand.TryGetSand(job.Extraction,out var surface);var ground=surface.Position;
  physics.ResetTruck(ground+new Vector3(0,1.2f,3),Quaternion.Identity);physics.ResetRecovery(ground+new Vector3(0,.9f,-3),Quaternion.Identity);Tick(scene,new(.5f),false,Key.Space);
  if(!w.ConnectBlockWinch(winch))throw new Exception("Cannot secure disabled convoy cargo");
  int balance=w.RecoveryPayment;Set("_convoyCargoHull",110f);
  for(int i=0;i<600&&!w.Building;i++)Tick(scene,new(.5f),false,Key.Space);
  int expected=(int)MathF.Round(2200*(.55f+.45f*.5f));if(!w.Building||w.RecoveryPayment!=balance+expected)throw new Exception($"Cargo payout/return mismatch: balance={balance}, after={w.RecoveryPayment}, building={w.Building}");
  w.Command("Deploy contract");Tick(scene,new(.5f),false);if(!w.Building||w.RecoveryPayment!=balance+expected)throw new Exception("Completed combat contract paid twice");
  Console.WriteLine("PASS convoy movement, weapons, disable, fail/retry, secured extraction, damage-based payout and garage return");Scenes.UnloadScene();Close();
 }
}
