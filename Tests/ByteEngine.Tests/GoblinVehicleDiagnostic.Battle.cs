using GoblinScrapper.Construction;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Tests;
internal sealed partial class GoblinVehicleDiagnostic
{
 private void RunBattleTest(Scene scene,VehicleBuilder3D builder)
 {
  builder.BattlefieldEnabled=true;builder.BattlefieldEnemyCount=1;Scenes.LoadScene(scene);
  for(int i=0;i<30;i++)TickInput(scene,new(.98f,.5f),[]);
  var catalog=VehiclePartCatalog.Load(Path.Combine(_project!.ProjectRoot,"Assets/GarageUI/parts-catalog.json"));builder.RestoreAssembly(ContraptionTests.Build(catalog).ToJson());
  Assert(builder.BeginDriving()&&builder.BattleRunning,"Battle deployment failed");
  var goblin=scene.GameObjects.Single(o=>o.Name=="Red goblin #0");var rig=goblin.GetComponent<SkeletalMeshRenderer>()!;
  Assert(rig.ModelLoaded&&rig.AnimationNames.Contains("Idle")&&rig.AnimationNames.Contains("Walk")&&rig.AnimationNames.Contains("Attack"),"Goblin animation import failed");
  goblin.Transform.WorldPosition=new(0,0,-7);
  for(int i=0;i<60;i++)TickInput(scene,new(.98f,.5f),[]);
  Screenshot(scene,"Designs/BattlePrototype/battlefield.png",1920,1080);
  Assert(rig.CurrentAnimation=="Walk","Goblin pursuit animation failed");
  for(int i=0;i<360&&!builder.BattleWon;i++)TickInput(scene,new(.98f,.5f),[Key.W]);
  Assert(builder.BattleKills==1&&builder.BattleWon,"Vehicle did not physically hit the goblin");
  Assert(scene.GameObjects.Count(o=>o.Name.StartsWith("Ragdoll 0 "))==6,"Missing jointed corpse pieces");
  for(int i=0;i<30;i++)TickInput(scene,new(.98f,.5f),[]);
  Screenshot(scene,"Designs/BattlePrototype/ram-ragdoll.png",1920,1080);
  var machine=builder.Assembly!.ToJson();builder.ReturnToBuild();TickInput(scene,new(.98f,.5f),[]);
  Assert(builder.Assembly.ToJson()==machine&&!builder.BattleRunning&&!scene.GameObjects.Any(o=>o.Name.StartsWith("Ragdoll ")),"Return to workshop leaked corpses or changed machine");
  Assert(builder.BeginDriving()&&builder.BattleKills==0&&!builder.BattleWon,"Battle retry did not reset goal");
  Console.WriteLine("PASS: native goblin rig and Idle/Walk/Attack; pursuit; powered-wheel ram hit; six-body jointed corpse; victory; retained machine; corpse cleanup; retry reset.");
  builder.ReturnToBuild();
  var cannonMachine=new VehicleAssembly(catalog);var cannon=catalog.Parts.Values.First(d=>d.ReferenceId==11);Assert(cannonMachine.AddAtSocket(cannon.File,0,"Front",cannon.Sockets.First(s=>s.Bone=="Root").Name)>0,"Cannon fixture placement");builder.RestoreAssembly(cannonMachine.ToJson());Assert(builder.BeginDriving(),"Cannon battle start");
  goblin=scene.GameObjects.Single(o=>o.Name=="Red goblin #0");goblin.Transform.WorldPosition=new(0,0,-4);builder.FireWeapons();
  for(int i=0;i<90&&!builder.BattleWon;i++)TickInput(scene,new(.98f,.5f),[]);
  Assert(builder.BattleWon&&builder.BattleSevered==1,"Cannon hit failed to kill and sever a limb");
  for(int i=0;i<60;i++)TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,"Designs/BattlePrototype/dismemberment.png",1920,1080);
  foreach(var piece in scene.GameObjects.Where(o=>o.Name.StartsWith("Ragdoll ")))Assert(float.IsFinite(piece.Transform.WorldPosition.LengthSquared()),"Corpse simulation invalid");
  builder.ReturnToBuild();builder.BattlefieldEnemyCount=18;builder.RestoreAssembly(ContraptionTests.Build(catalog).ToJson());Assert(builder.BeginDriving(),"Full battle start");
  Assert(scene.GameObjects.Count(o=>o.Name.StartsWith("Red goblin #"))==18,"Full wave did not spawn");
  for(int i=0;i<120;i++)TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,"Designs/BattlePrototype/full-battlefield.png",1920,1080);
  var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<120;i++)TickInput(scene,new(.98f,.5f),[]);watch.Stop();Console.WriteLine($"18 goblins: mean scene CPU update {watch.Elapsed.TotalMilliseconds/120:0.00} ms; rendering excluded.");
  builder.ReturnToBuild();builder.BattlefieldEnemyCount=1;builder.RestoreAssembly(new VehicleAssembly(catalog).ToJson());Assert(builder.BeginDriving(),"Defeat test deployment");goblin=scene.GameObjects.Single(o=>o.Name=="Red goblin #0");goblin.Transform.WorldPosition=new(0,0,1);for(int i=0;i<2000&&!builder.BattleLost;i++)TickInput(scene,new(.98f,.5f),[]);Assert(builder.BattleLost&&!builder.BattleWon,"Enemy melee did not cause defeat");builder.ReturnToBuild();
  Console.WriteLine("PASS: cannon projectile hit, severed body part, finite corpse simulation; 18-enemy wave; melee defeat; return to workshop.");
 }
}
