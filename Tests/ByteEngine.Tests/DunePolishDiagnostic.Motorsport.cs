using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunMotorsport(string root){
  DriftScoringTests.Run();var saves=Path.Combine(root,"Saves");Directory.CreateDirectory(saves);
  foreach(string f in new[]{"drift-progress-v1.json","drag-progress-v1.json","rally-progress-v1.json","vehicle.json","workshop-economy-v1.json","workshop-tutorial.json","menu-settings.json"})File.Delete(Path.Combine(saves,f));
  File.WriteAllText(Path.Combine(saves,"recovery-payment.json"),"5000");File.WriteAllText(Path.Combine(saves,"workshop-tutorial.json"),"{\"version\":2,\"complete\":true}");
  if(Environment.GetEnvironmentVariable("DUNE_MOTORSPORT_COLD_START")=="1"){
   File.Copy(Path.Combine(root,"Assets/RecoveryBlueprints/Recovery-demo.json"),Path.Combine(saves,"vehicle.json"),true);
   typeof(DuneMainMenu3D).GetField("ContinuePending",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,true);
   typeof(DuneWorkshop3D).GetField("_deployContractAfterSwitch",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,6);
   var cold=_project!.Scenes.Load(Path.Combine(root,"Scenes/DeadStraight.bytescene"));Scenes.LoadScene(cold);for(int i=0;i<300;i++)Tick(cold,new(.5f),false);Capture(cold,Path.Combine(root,"Preview/drag-cold-start.png"),false);Console.WriteLine("PASS Cold auto-deployment during scene startup");Scenes.UnloadScene();Close();return;
  }
  if(Environment.GetEnvironmentVariable("DUNE_MOTORSPORT_SETUP_FAILURE")=="1"){
   var failed=_project!.Scenes.Load(Path.Combine(root,"Scenes/DeadStraight.bytescene"));Scenes.LoadScene(failed);Tick(failed,new(.5f),false);var driver=failed.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();File.Copy(Path.Combine(root,"Assets/RecoveryBlueprints/Recovery-demo.json"),Path.Combine(saves,"vehicle.json"),true);driver.Command("Load vehicle");driver.SelectRecoveryContract(6);
   var profile=failed.GameObjects.SelectMany(o=>o.Components).OfType<DuneSprintContract3D>().Single();string original=profile.OpponentBlueprint;profile.OpponentBlueprint="Assets/missing-rival-for-test.json";driver.Command("Deploy contract");
   if(!driver.Building||typeof(DuneWorkshop3D).GetField("_physics",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(driver)!=null||failed.FindGameObject("Sprint opponent")!=null)throw new Exception("Failed setup left a partial race active");for(int i=0;i<20;i++)Tick(failed,new(.5f),false);
   profile.OpponentBlueprint=original;driver.Command("Deploy contract");for(int i=0;i<220;i++)Tick(failed,new(.5f),false);if(driver.Building||failed.GameObjects.SelectMany(o=>o.Components).OfType<AiAgent3D>().Count()!=1)throw new Exception("Cannot retry after failed race setup");Console.WriteLine("PASS Missing race assets roll back safely, then a corrected setup can retry");Scenes.UnloadScene();Close();return;
  }
  foreach(var (name,id) in new[]{("DustBowlDrift",5),("DeadStraight",6),("RedRockRally",7)}){
   Console.WriteLine("START MOTORSPORT "+name);var scene=_project!.Scenes.Load(Path.Combine(root,$"Scenes/{name}.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);
   var workshop=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();typeof(DuneWorkshop3D).GetField("_tutorialStep",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(workshop,-1);
   File.Copy(Path.Combine(root,"Assets/RecoveryBlueprints/Recovery-demo.json"),Path.Combine(saves,"vehicle.json"),true);workshop.Command("Load vehicle");workshop.SelectRecoveryContract(id);Tick(scene,new(.5f),false);
   if(scene.FindGameObject("Contract row "+(id==5?"drift":id==6?"drag":"rally"))?.ActiveInHierarchy!=true)throw new Exception("New motorsport is missing from its category");
   Capture(scene,Path.Combine(root,$"Preview/{name}-brief.png"),false);workshop.Command("Deploy contract");for(int i=0;i<6;i++)Tick(scene,new(.5f),false);
   if(workshop.Building)throw new Exception("Cannot deploy new contract");
   var props=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneRaceProp3D>().ToArray();if(props.Length<100||props.Any(p=>p.RenderMeshCount==0))throw new Exception("Race kit failed to instantiate");
   Capture(scene,Path.Combine(root,$"Preview/{name}-start.png"),false);
   foreach(var o in scene.GameObjects.Where(o=>o.GetComponent<UiCanvas>()!=null))o.Active=false;
   var camera=scene.ActiveCamera!;var target=id==5?new Vector3(0,0,-100):id==6?new Vector3(2,0,-260):new Vector3(95,18,-310);
   var pos=id==5?new Vector3(115,125,-8):id==6?new Vector3(130,230,-20):new Vector3(380,330,-5);
   camera.Transform.WorldPosition=pos;var forward=Vector3.Normalize(target-pos);camera.Transform.WorldRotation=Quaternion.CreateFromYawPitchRoll(MathF.Atan2(-forward.X,-forward.Z),MathF.Asin(forward.Y),0);camera.FarClip=2500;
   Capture(scene,Path.Combine(root,$"Preview/{name}-overview.png"),false);foreach(var o in scene.GameObjects.Where(o=>o.GetComponent<UiCanvas>()!=null))o.Active=true;
   foreach(var o in scene.GameObjects.Where(o=>o.GetComponent<UiCanvas>()!=null))o.Active=false;
   using(var fb=new ByteEngine.Editor.SceneFramebuffer()){
    fb.RenderGame(Renderer,Renderer3D,scene,ByteEngine.Editor.EditorMode.Play,768,200,768,200);OpenTK.Graphics.OpenGL4.GL.Finish();var pixels=new byte[768*200*4];OpenTK.Graphics.OpenGL4.GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D,(int)fb.TextureId);OpenTK.Graphics.OpenGL4.GL.GetTexImage(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D,0,OpenTK.Graphics.OpenGL4.PixelFormat.Rgba,OpenTK.Graphics.OpenGL4.PixelType.UnsignedByte,pixels);Png(Path.Combine(root,$"Preview/contract-{id}.png"),pixels,768,200);
   }foreach(var o in scene.GameObjects.Where(o=>o.GetComponent<UiCanvas>()!=null))o.Active=true;
   if(Environment.GetEnvironmentVariable("DUNE_MOTORSPORT_RENDER_ONLY")=="1"){Scenes.UnloadScene();continue;}
   if(id==5){
    for(int i=0;i<220;i++)Tick(scene,new(.5f),false);for(int i=0;i<300;i++)Tick(scene,new(.5f),false,Key.W);for(int i=0;i<600;i++)Tick(scene,new(.5f),false,Key.W,Key.D,Key.LeftShift);
    var scoring=workshop.GameObject.GetComponent<DriftScore3D>()!;Console.WriteLine($"PHYSICAL DRIFT banked={scoring.BankedScore:0} pending={scoring.ChainScore:0} angle={scoring.SlipAngle:0}");
    for(int i=0;i<150;i++)Tick(scene,new(.5f),false);Capture(scene,Path.Combine(root,"Preview/drift-chain-gameplay.png"),false);
    int balance=workshop.RecoveryPayment;for(int i=0;i<800;i++)scoring.Sample(new(7,0,-10),Quaternion.Identity,true,false,1f/60);scoring.Bank();Tick(scene,new(.5f),false);
    if(!(bool)typeof(DuneWorkshop3D).GetField("_contractComplete",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(workshop)!||workshop.RecoveryPayment!=balance+900)throw new Exception("Drift target doesn't award its one-time reward");for(int i=0;i<180;i++)Tick(scene,new(.5f),false);if(!workshop.Building||workshop.RecoveryPayment!=balance+900)throw new Exception("Drift result must return to garage and not pay twice");
   }else{
    var physics=(LandVehiclePhysics)typeof(DuneWorkshop3D).GetField("_physics",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(workshop)!;if(physics.HasRecovery||physics.BodyCount<10)throw new Exception("Race rival is not in the player's physics world");
    var brain=scene.GameObjects.SelectMany(o=>o.Components).OfType<AiAgent3D>().Single();var race=scene.GameObjects.SelectMany(o=>o.Components).OfType<RaceDirector3D>().Single();var route=scene.FindGameObject(race.RouteObject)!.GetComponent<RoutePath3D>()!;
    if(id==7&&(route.Points.Count!=6||scene.FindGameObject(brain.RouteObject)!.GetComponent<RoutePath3D>()!.Points.Count<100))throw new Exception("Rally needs six gates and separate dense AI route");
    if(id==6){Tick(scene,new(.5f),false,Key.W);if((float)typeof(DuneWorkshop3D).GetField("_launchPenalty",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(workshop)!<.5f)throw new Exception("Early launch has no penalty");workshop.Command("Recover vehicle");physics=(LandVehiclePhysics)typeof(DuneWorkshop3D).GetField("_physics",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(workshop)!;brain=scene.GameObjects.SelectMany(o=>o.Components).OfType<AiAgent3D>().Single();race=scene.GameObjects.SelectMany(o=>o.Components).OfType<RaceDirector3D>().Single();}
    float peak=0;for(int i=0;i<20000&&race.State!=RaceState.Finished;i++){Tick(scene,new(.5f),false,Key.Space);peak=Math.Max(peak,physics.AssemblyVelocity(20000).Length());if(i%3000==0)Console.WriteLine($"AI {name} step={i} checkpoint={brain.Checkpoint} pos={physics.AssemblyFrame(20000).Position}");}
    Console.WriteLine($"RIVAL {name} winner={race.Winner} checkpoint={brain.Checkpoint} peak={peak:0.0}");if(race.Winner!="Rival"||peak<8)throw new Exception("Physical rival cannot finish new course: "+name);
    workshop.Command("Recover vehicle");for(int i=0;i<220;i++)Tick(scene,new(.5f),false,Key.Space);
    physics=(LandVehiclePhysics)typeof(DuneWorkshop3D).GetField("_physics",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(workshop)!;race=scene.GameObjects.SelectMany(o=>o.Components).OfType<RaceDirector3D>().Single();int balance=workshop.RecoveryPayment;
    if(id==7){physics.ResetTruck(route.Points[^1],Quaternion.Identity);Tick(scene,new(.5f),false,Key.Space);if(race.Gate("Player")!=0)throw new Exception("Rally finish shortcut skipped ordered gates");}
    foreach(var p in route.Points){physics.ResetTruck(p,Quaternion.Identity);Tick(scene,new(.5f),false,Key.Space);}for(int i=0;i<180;i++)Tick(scene,new(.5f),false,Key.Space);
    if(!workshop.Building||workshop.RecoveryPayment!=balance+(id==6?1200:1500))throw new Exception("Race payout or return failed");
   }
   Scenes.UnloadScene();Console.WriteLine("PASS MOTORSPORT "+name+" / assets, contract, result, reward and return");
  }Close();
 }
}
