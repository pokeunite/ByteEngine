using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunDragBalance(string root)
 {
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var catalog=JsonSerializer.Deserialize<List<DunePart>>(File.ReadAllText(Path.Combine(root,"Assets/DuneParts/catalog.json")),BuildStore.Json)!.ToDictionary(p=>p.Id);
  var blocks=BuildStore.Read(File.ReadAllText(Path.Combine(root,"Assets/RecoveryBlueprints/Recovery-demo.json")),catalog);
  foreach(var b in blocks.Where(b=>GearboxMounts.IsEngine(b.Type))){b.Type=24;b.Power=2;b.SpeedLimit=50;}
  var extraEngine=new PlacedBlock{Id=blocks.Max(b=>b.Id)+1,Type=24,Parent=1,Power=2,SpeedLimit=50};extraEngine.Pose(new(0,.5f,1.4f),Quaternion.Identity);blocks.Add(extraEngine);
  var engine=blocks.First(b=>GearboxMounts.IsEngine(b.Type));var gear=new PlacedBlock{Id=blocks.Max(b=>b.Id)+1,Type=39,Parent=engine.Id,Power=2};gear.Pose(GearboxMounts.Position(engine,catalog[engine.Type]),GearboxMounts.Rotation(engine));blocks.Add(gear);
  var saves=Path.Combine(root,"Saves");Directory.CreateDirectory(saves);File.Delete(Path.Combine(saves,"drag-progress-v1.json"));File.WriteAllText(Path.Combine(saves,"workshop-economy-v1.json"),"{\"Version\":2,\"Balance\":10000,\"Owned\":{\"39\":1},\"Tuning\":[],\"Rewards\":[]}");File.WriteAllText(Path.Combine(saves,"vehicle.json"),JsonSerializer.Serialize(blocks,BuildStore.Json));
  typeof(DuneMainMenu3D).GetField("ContinuePending",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,true);
  var scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/DeadStraight.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);
  var w=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();typeof(DuneWorkshop3D).GetField("_tutorialStep",flags)!.SetValue(w,-1);w.SelectRecoveryContract(6);w.Command("Deploy contract");
  var physics=(LandVehiclePhysics)typeof(DuneWorkshop3D).GetField("_physics",flags)!.GetValue(w)!;var race=scene.GameObjects.SelectMany(o=>o.Components).OfType<RaceDirector3D>().Single();
  if(w.Building||physics.Transmission==null)throw new Exception("Cannot deploy test drag rig");
  while(race.State==RaceState.Countdown){Tick(scene,new(.5f),false,Key.W);if(physics.Frame.Position.Z< -1)throw new Exception("Staged throttle moves before green");}
  float launchSpeed=0;for(int i=0;i<15;i++){Tick(scene,new(.5f),false,Key.W);launchSpeed=Math.Max(launchSpeed,w.PhysicalVelocity.Length());}
  if(launchSpeed<.1f||(float)typeof(DuneWorkshop3D).GetField("_launchPenalty",flags)!.GetValue(w)!=0)throw new Exception("Held throttle is penalized after green");Console.WriteLine($"PASS held-throttle launch within 0.25 seconds: {launchSpeed:0.00} m/s");
  float peak=0;int highest=1;for(int i=0;i<18000&&race.State!=RaceState.Finished;i++){if(physics.Transmission!.ShiftReady)w.ShiftPlayerGear(1);Tick(scene,new(.5f),false,Key.W);peak=Math.Max(peak,w.PhysicalVelocity.Length());highest=Math.Max(highest,physics.Transmission.Gear);}
  Console.WriteLine($"DRAG perfect winner={race.Winner} elapsed={race.Elapsed:0.00} highest={highest} peak={peak:0.0} player={physics.Frame.Position} rival={physics.AssemblyFrame(20000).Position}");
  if(race.Winner!="Player")throw new Exception("Correctly shifted two-heavy-engine rig cannot beat the rival");
  var transmission=new ManualTransmission();for(int next=2;next<=transmission.GearCount;next++){for(int i=0;i<4;i++)transmission.Update(.1f,0,32,2);if(!transmission.Shift(1,0,32,2))throw new Exception("Cannot select all gears");}if(transmission.Gear!=6||transmission.ShiftReady||transmission.GearSpeed(50,2)<=30)throw new Exception("Overdrive does not extend tuned final-drive speed");
  if(scene.FindGameObject("Contracts combat page")!=null||File.Exists(Path.Combine(root,"Scenes/ConvoyBreaker.bytescene")))throw new Exception("Retired combat contract is still active");
  Console.WriteLine("PASS six-speed overdrive, fair rival, immediate green launch and no combat category");Scenes.UnloadScene();Close();
 }
}
