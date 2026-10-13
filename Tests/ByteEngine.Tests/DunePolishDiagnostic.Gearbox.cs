using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core.AI;
using DuneCompany;
using DesertTerrain;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunDragGearbox(string root)
 {
  void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
  Check(LandVehiclePhysics.FinalDriveRatio([])==1,"Legacy vehicle ratio remains 1:1");
  Check(LandVehiclePhysics.FinalDriveRatio([new(){Type=39,Power=2},new(){Type=39,Power=2}])==2,"Gearboxes cannot stack torque multipliers");
  var early=new ManualTransmission();var good=new ManualTransmission();var late=new ManualTransmission();
  for(int i=0;i<10;i++){early.Update(.1f,3,32,1);good.Update(.1f,9.2f,32,1);late.Update(.1f,10.3f,32,1);}
  Check(good.ShiftReady&&!early.ShiftReady&&late.Limiter,"Shift light and limiter follow speed-coupled RPM");
  Check(early.Shift(1,3,32,1)&&good.Shift(1,9.2f,32,1),"Manual upshifts select the next gear");
  Check(good.DriveFactor==0&&!good.Shift(1,9.2f,32,1),"Clutch cuts drive and rejects repeated shifts during engagement");
  for(int i=0;i<10;i++){early.Update(.1f,3,32,1);good.Update(.1f,9.2f,32,1);}
  Check(good.DriveFactor>early.DriveFactor+.25f,"Correct shift retains more power than an early shift");
  Check(late.DriveFactor==0&&late.Shift(1,10.3f,32,1),"Late shift hits the limiter but remains recoverable");
  for(int i=0;i<10;i++)good.Update(.1f,13,32,1);
  Check(!good.Shift(-1,13,32,1)&&good.Gear==2,"Unsafe downshift cannot over-rev the engine");
  var blocks=JsonSerializer.Deserialize<List<PlacedBlock>>(File.ReadAllText(Path.Combine(root,"Assets/RecoveryBlueprints/Recovery-demo.json")),BuildStore.Json)!;
  var saves=Path.Combine(root,"Saves");Directory.CreateDirectory(saves);
  File.WriteAllText(Path.Combine(saves,"vehicle.json"),JsonSerializer.Serialize(blocks,BuildStore.Json));
  File.WriteAllText(Path.Combine(saves,"workshop-tutorial.json"),"{\"version\":2,\"complete\":true}");
  typeof(DuneMainMenu3D).GetField("ContinuePending",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,true);
  var scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/DeadStraight.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);
  var workshop=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();
  typeof(DuneWorkshop3D).GetField("_tutorialStep",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(workshop,-1);
  workshop.SelectRecoveryContract(6);workshop.Command("Deploy contract");
  Check(workshop.Building&&!workshop.HasGearbox,"Drag deployment rejects a missing gearbox");
  var terrain=scene.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain3D>().Single();
  float lo=float.MaxValue,hi=float.MinValue;
  for(int z=30;z<terrain.Rows;z++)for(int x=0;x<terrain.Columns;x++){
   float wx=terrain.GridMinimum.X+x*terrain.CellSpacing,wz=terrain.GridMinimum.Y+z*terrain.CellSpacing;
   if(wx>=-6&&wx<=10&&wz>=-570&&wz<=20){var h=terrain.HeightAt(x,z);lo=Math.Min(lo,h);hi=Math.Max(hi,h);}
  }
  Check(hi-lo<.0001f,"Staging, racing and braking terrain share one elevation");
  var surface=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorldObstacle3D>().Single(o=>o.DriveSurface);
  Check(Math.Abs(surface.Transform.WorldPosition.Y+surface.Size.Y/2-.12f)<.0001f,"Solid road collider aligns with asphalt elevation");
  var catalog=JsonSerializer.Deserialize<List<DunePart>>(File.ReadAllText(Path.Combine(root,"Assets/DuneParts/catalog.json")),BuildStore.Json)!.ToDictionary(p=>p.Id);
  var engine=blocks.First(b=>GearboxMounts.IsEngine(b.Type));var gearbox=new PlacedBlock{Id=blocks.Max(b=>b.Id)+1,Type=39,Parent=engine.Id,Power=1};gearbox.Pose(GearboxMounts.Position(engine,catalog[engine.Type]),engine.Q);blocks.Add(gearbox);
  Check(GearboxMounts.Connected(gearbox,blocks,catalog),"Gearbox input connector snaps to the engine rear casting");
  var wrong=new PlacedBlock{Id=99,Type=39,Parent=0,Position=[0,2,2]};Check(!GearboxMounts.Connected(wrong,blocks,catalog),"A gearbox on the frame does not satisfy the engine attachment requirement");
  File.WriteAllText(Path.Combine(saves,"vehicle.json"),JsonSerializer.Serialize(blocks,BuildStore.Json));workshop.Command("Load vehicle");
  Check(workshop.HasGearbox,"New block loads and mounts in a saved vehicle");
  scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneSprintContract3D>().Single().OpponentSpeed=5;
  workshop.Command("Deploy contract");
  for(int i=0;i<220;i++)Tick(scene,new(.5f),false);
  Check(!workshop.Building,"Gearbox permits drag deployment");
  var physics=(LandVehiclePhysics)typeof(DuneWorkshop3D).GetField("_physics",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(workshop)!;
  var model=_project.Assets.LoadModel(new ByteEngine.Core.Assets.AssetReference("Assets/DuneParts/glb/39_gearbox.glb"));Check(new[]{"Idle","ShiftUp","ShiftDown"}.All(name=>model.Animations.Any(a=>a.Name==name)),"Animated model imports both lever shift clips");
  Check(scene.GameObjects.SelectMany(o=>o.Components).OfType<AiAgent3D>().Count()==1,"Physical rival starts with the flat road collider");
  var obstacles=(DuneWorldObstacle3D[])typeof(DuneWorkshop3D).GetField("_raceObstacles",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(workshop)!;
  Check(obstacles.All(o=>!o.DriveSurface),"Rival obstacle avoidance excludes the drivable road");
  Capture(scene,Path.Combine(root,"Preview/drag-gearbox-start.png"),false);
  for(int i=0;i<750;i++)Tick(scene,new(.5f),false,ByteEngine.Core.Key.W);
  Check(float.IsFinite(workshop.PhysicalVelocity.Length())&&physics.Frame.Position.Z<-15,"Player accelerates along the flat road");
  Check(physics.Transmission?.Gear==1&&new Vector2(workshop.PhysicalVelocity.X,workshop.PhysicalVelocity.Z).Length()<11,"Staying in first limits road speed");
  Console.WriteLine($"FIRST gear speed={workshop.PhysicalVelocity.Length():0.00} rpm={physics.Transmission!.Rpm:0} state={JsonSerializer.Serialize(workshop.MotorsportDiagnostics)}");
  Check(workshop.ShiftPlayerGear(1)&&physics.Transmission!.Gear==2,"Player gear input changes the physical transmission");
  var animation=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneGearboxAnimation3D>().Single();animation.Advance(.055f);Check(animation.LeverResolved&&animation.LeverAngle>0,"Upshift moves the actual lever node");
  for(int i=0;i<240;i++)Tick(scene,new(.5f),false,ByteEngine.Core.Key.W);
  Console.WriteLine($"SECOND gear speed={workshop.PhysicalVelocity.Length():0.00} rpm={physics.Transmission!.Rpm:0} state={JsonSerializer.Serialize(workshop.MotorsportDiagnostics)}");
  Check(new Vector2(workshop.PhysicalVelocity.X,workshop.PhysicalVelocity.Z).Length()>10,"Higher gear allows speed beyond first gear");
  Capture(scene,Path.Combine(root,"Preview/drag-gearbox-driving.png"),false);
  Console.WriteLine("DRAG GEARBOX PASS");Scenes.UnloadScene();Close();
 }
}
