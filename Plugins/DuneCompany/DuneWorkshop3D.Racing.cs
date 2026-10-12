using System.Numerics;
using ByteEngine.Core.AI;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 DuneSprintContract3D? _sprint;bool _sprintResolved;
 DuneSprintContract3D? Sprint{get{if(!_sprintResolved){_sprintResolved=true;_sprint=GameObject.Scene?.GameObjects.SelectMany(o=>o.Components).OfType<DuneSprintContract3D>().FirstOrDefault();}return _sprint;}}
 DuneWorldObstacle3D[] _raceObstacles=[];
 bool SprintMission=>Sprint!=null;
 RaceDirector3D? _race;AiAgent3D? _racerBrain;int _opponentRoot;
 GameObject? _opponentVisual;
 readonly List<(int id,GameObject node,GameObject? output,Matrix4x4 rest)> _opponentParts=[];
 void EnsureSprintContractRow(){if(!_ui.TryGetValue("Contracts overlay",out var panel)||_ui.ContainsKey("Contract row sprint"))return;var scene=GameObject.Scene!;var row=scene.CreateGameObject("Contract row sprint");row.SetParent(panel,false);row.AddComponent(new ByteEngine.Core.Graphics.UiWidget{Kind=ByteEngine.Core.Graphics.UiWidgetKind.Button,Offset=new(27,290),Size=new(_ui.TryGetValue("Contract row tow",out var sourceRow)?sourceRow.GetComponent<ByteEngine.Core.Graphics.UiWidget>()!.Size.X:500,25),FontSize=16,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Label="Scrapyard Sprint / $1000",OrderInLayer=35,Color=new(.105f,.108f,.105f,1),HoverColor=new(.3f,.23f,.13f,1)});_ui[row.Name]=row;var heading=scene.CreateGameObject("Contracts racing category");heading.SetParent(panel,false);heading.AddComponent(new ByteEngine.Core.Graphics.UiText{Text="DESERT MOTORSPORT",Offset=new(27,256),FontSize=15,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Color=new(.94f,.66f,.29f,1),OrderInLayer=35});_ui[heading.Name]=heading;}
 sealed class RacerDriver(DuneWorkshop3D workshop,int root):IAiVehicleDriver
 {
  public Vector3 Position=>workshop._physics!.AssemblyFrame(root).Position;
  public Quaternion Rotation=>workshop._physics!.AssemblyFrame(root).Rotation;
  public Vector3 Velocity=>workshop._physics!.AssemblyVelocity(root);
  public bool Grounded=>workshop._physics!.AssemblyGrounded(root);
  public bool CorridorClear(Vector3 target,float radius){
   var delta=target-Position;float length=delta.Length();if(length<.01f)return true;var player=workshop._physics!.Frame.Position;float t=Math.Clamp(Vector3.Dot(player-Position,delta)/(length*length),0,1);if(Vector3.DistanceSquared(player,Position+delta*t)<=MathF.Pow(radius+1.5f,2))return false;
   foreach(var obstacle in workshop._raceObstacles){
    if(!Matrix4x4.Invert(obstacle.Transform.WorldMatrix,out var inverse))continue;var a=Vector3.Transform(Position,inverse);var d=Vector3.Transform(target,inverse)-a;Matrix4x4.Decompose(obstacle.Transform.WorldMatrix,out var scale,out _,out _);float inflate=radius/Math.Max(.01f,Math.Min(Math.Abs(scale.X),Math.Abs(scale.Z)));var half=obstacle.Size*.5f+new Vector3(inflate,.25f,inflate);var lo=obstacle.Center-half;var hi=obstacle.Center+half;float near=0,far=1;bool hit=true;
    for(int axis=0;axis<3;axis++){float v=axis==0?a.X:axis==1?a.Y:a.Z,step=axis==0?d.X:axis==1?d.Y:d.Z,min=axis==0?lo.X:axis==1?lo.Y:lo.Z,max=axis==0?hi.X:axis==1?hi.Y:hi.Z;if(Math.Abs(step)<.00001f){if(v<min||v>max){hit=false;break;}}else{float enter=(min-v)/step,exit=(max-v)/step;if(enter>exit)(enter,exit)=(exit,enter);near=Math.Max(near,enter);far=Math.Min(far,exit);if(near>far){hit=false;break;}}}if(hit)return false;
   }return true;
  }

  public void Drive(AiDriveCommand command)=>workshop._physics?.SetAssemblyInput(root,command,workshop.Sprint?.OpponentSpeed??13);
 }
 void StartSprint(){if(!SprintMission||!_contractRun||_physics==null)return;var scene=GameObject.Scene!;_raceObstacles=scene.GameObjects.Where(o=>o.ActiveInHierarchy).SelectMany(o=>o.Components).OfType<DuneWorldObstacle3D>().Where(c=>c.Enabled).ToArray();var profile=Sprint!;var route=scene.GameObjects.SelectMany(o=>o.Components).OfType<RoutePath3D>().FirstOrDefault()??throw new InvalidDataException("Sprint needs a Route Path.");_race=route.GameObject.GetComponent<RaceDirector3D>()??route.GameObject.AddComponent(new RaceDirector3D());_race.Reset();_race.ExternalTick=true;_race.RouteObject=route.GameObject.Id;
  var blocks=BuildStore.Read(File.ReadAllText(Path.Combine(ProjectRoot,profile.OpponentBlueprint)),_catalog);foreach(var b in blocks){if(_catalog[b.Type].Power>0){b.Type=38;b.SpeedLimit=32;b.Power=1.25f;}if(_catalog[b.Type].Wheel){b.Grip=1.3f;b.Power=1.3f;}}BuildRaceGates(route);_opponentVisual=scene.CreateGameObject("Sprint opponent");_opponentParts.Clear();foreach(var b in blocks){var node=Model(scene,Assets!,_catalog[b.Type].File,_opponentVisual,"Racer part "+b.Id);node.Transform.LocalPosition=b.P;node.Transform.LocalRotation=b.Q;if(_catalog[b.Type].Power>0)node.AddComponent(new DuneEngineAnimation3D{Running=true});var output=Descendants(node).FirstOrDefault(o=>o.Name==OutputName(b.Type));Matrix4x4.Invert(node.Transform.WorldMatrix,out var inv);var rest=output==null?Matrix4x4.Identity:output.Transform.WorldMatrix*inv;CaptureDefinition(new(){Id=-1,Type=b.Type},node,output);_opponentParts.Add((b.Id,node,output,rest));}
  var start=_physics.Frame.Position+Vector3.UnitX*4;_opponentRoot=_physics.AddDrivenAssembly(blocks,_physicsDefinitions,start,_physics.Frame.Rotation);_racerBrain=_opponentVisual.AddComponent(new AiAgent3D{RouteObject=route.GameObject.Id,CruiseSpeed=profile.OpponentSpeed,Driver=new RacerDriver(this,_opponentRoot)});_race.Register("Player",()=>_physics.Frame.Position);_race.Register("Rival",()=>_physics.AssemblyFrame(_opponentRoot).Position);_race.StartRace();
 }
 void UpdateSprintControls(ref float throttle,ref float steer,ref bool brake,float dt){if(!SprintMission||_race==null)return;_race.Tick(dt);bool racing=_race.State==RaceState.Racing;_racerBrain!.Running=racing;if(!racing){throttle=steer=0;brake=true;_physics!.SetAssemblyInput(_opponentRoot,new(0,0,true));}}
 void RenderSprint(){if(_opponentVisual==null||_physics==null)return;var frame=_physics.AssemblyFrame(_opponentRoot,true);SetRenderedWorldPose(_opponentVisual,Pose(frame.Position,frame.Rotation));foreach(var (id,node,output,rest) in _opponentParts){var pose=_physics.RenderPartPose(_opponentRoot+id);SetRenderedWorldPose(node,Pose(pose.Position,pose.Rotation));if(output!=null)SetRenderedWorldPose(output,rest*_physics.RenderOutputDeformation(_opponentRoot+id)*node.Transform.WorldMatrix);}}
 void UpdateSprint(){RefreshRaceGates();if(Building||!_contractRun||_race==null)return;GarageActive("Recovery interaction",true);if(_race.State==RaceState.Finished){if(_race.Winner=="Player"&&!_contractComplete){_contractComplete=true;if(!AwardContract()){_contractComplete=false;return;}RecordContractCompletion();}RecoveryObjective=_race.Winner=="Player"?"SPRINT WON":"RIVAL FINISHED FIRST / R TO RETRY";}else RecoveryObjective=_race.State==RaceState.Countdown?$"START IN {Math.Ceiling(_race.Remaining)}":$"POSITION {_race.Position("Player")}/2 / GATE {_race.Gate("Player")+1} / {_race.Elapsed:0.0}s";Text("Battle objective",RecoveryObjective);Text("Recovery interaction","WASD drive / Space brake / R restart sprint");var route=_race.GameObject.GetComponent<RoutePath3D>();if(_race.State==RaceState.Racing&&route!=null)ProjectRecoveryMarker(route.Points[Math.Min(_race.Gate("Player"),route.Points.Count-1)]+Vector3.UnitY*1.5f,$"GATE {_race.Gate("Player")+1}");else GarageActive("Recovery marker label",false);}
 void ClearSprint(){DestroyRaceGates();_racerBrain=null;_race?.Reset();_race=null;if(_opponentVisual?.Scene is {} scene)scene.DestroyGameObject(_opponentVisual);_opponentVisual=null;_opponentParts.Clear();}
}
