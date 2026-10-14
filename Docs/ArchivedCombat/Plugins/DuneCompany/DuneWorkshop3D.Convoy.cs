using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 DuneConvoyContract3D? _convoy; bool _convoyResolved;
 DuneConvoyContract3D? Convoy { get { if(!_convoyResolved){_convoyResolved=true;_convoy=GameObject.Scene?.GameObjects.SelectMany(o=>o.Components).OfType<DuneConvoyContract3D>().FirstOrDefault();}return _convoy;} }
 bool ConvoyMission => Convoy!=null;
 sealed class ConvoyRig { public int Root; public required GameObject Visual; public required DuneBountyTarget3D Target; public required AiAgent3D Brain; public List<(int id,GameObject node,GameObject? output,Matrix4x4 rest)> Parts=[]; }
 readonly List<ConvoyRig> _convoyEscorts=[]; GameObject? _convoyCargoBody; DuneBountyTarget3D? _convoyEngine; AiAgent3D? _convoyBrain;
 float _convoyHull,_convoyCargoHull,_convoyFireTime; bool _convoyFailed; int _convoyPaid;
 public object ConvoyDiagnostics => new {active=ConvoyMission,failed=_convoyFailed,playerHealth=_convoyHull,cargoHealth=_convoyCargoHull,engineDisabled=_convoyEngine?.Defeated,escorts=_convoyEscorts.Count,escortsDisabled=_convoyEscorts.Count(e=>e.Target.Defeated),secured=WinchConnected,payment=_convoyPaid};
 sealed class ConvoyDriver(DuneWorkshop3D w,int root,float speed):IAiVehicleDriver
 {
  readonly RacerDriver sensor=new(w,root);
  public Vector3 Position=>w._physics!.AssemblyFrame(root).Position; public Quaternion Rotation=>w._physics!.AssemblyFrame(root).Rotation;
  public Vector3 Velocity=>w._physics!.AssemblyVelocity(root); public bool Grounded=>w._physics!.AssemblyGrounded(root);
  public bool CorridorClear(Vector3 p,float radius){if(!sensor.CorridorClear(p,radius))return false;var a=Position;var d=p-a;float length=d.LengthSquared();if(length<.01f)return true;foreach(int other in w._convoyEscorts.Select(e=>e.Root).Append(LandVehiclePhysics.RecoveryRootId)){if(other==root)continue;var b=w._physics!.AssemblyFrame(other).Position;float t=Math.Clamp(Vector3.Dot(b-a,d)/length,0,1);if(Vector3.DistanceSquared(a+d*t,b)<MathF.Pow(radius+1.4f,2))return false;}return true;}
  public void Drive(AiDriveCommand command)=>w._physics?.SetAssemblyInput(root,root==LandVehiclePhysics.RecoveryRootId&&w._convoyEngine?.Defeated==true&&!w._convoyFailed?new(0,0,false):command,speed);
 }
 void StartConvoy()
 {
  if(!ConvoyMission||!_contractRun||_physics==null)return;
  var scene=GameObject.Scene!;var job=Convoy!;_convoyHull=job.PlayerHealth;_convoyCargoHull=job.CargoHealth;_convoyPaid=ContractReward(8);_convoyFailed=false;_convoyFireTime=1.5f;
  _raceObstacles=scene.GameObjects.Where(o=>o.ActiveInHierarchy).SelectMany(o=>o.Components).OfType<DuneWorldObstacle3D>().Where(c=>c.Enabled&&!c.DriveSurface).ToArray();
  _physics.EnableRecoveryDrive();_physics.HookReach=16;_physics.MaximumHookRelativeSpeed=8;_physics.MaximumHookSpeed=24;
  _convoyCargoBody=scene.CreateGameObject("Convoy salvage cargo");_convoyCargoBody.AddComponent(new DuneRaceProp3D{Assets=Assets,ModelPath="Assets/RaceKit/convoy-cargo.obj"});
  var engine=scene.CreateGameObject("Convoy exposed engine target");_convoyEngine=engine.AddComponent(new DuneBountyTarget3D{MaximumHealth=110,Radius=1.1f});
  _convoyBrain=engine.AddComponent(new AiAgent3D{RouteObject=job.RouteObject,CruiseSpeed=job.ConvoySpeed,Driver=new ConvoyDriver(this,LandVehiclePhysics.RecoveryRootId,job.ConvoySpeed)});
  var blocks=BuildStore.Read(File.ReadAllText(Path.Combine(ProjectRoot,"Assets/RecoveryBlueprints/Recovery-demo.json")),_catalog);
  foreach(var b in blocks)if(_catalog[b.Type].Power>0){b.Power=1;b.SpeedLimit=18;}
  for(int i=0;i<2;i++){
   var visual=scene.CreateGameObject("Convoy escort "+(i+1));var parts=new List<(int,GameObject,GameObject?,Matrix4x4)>();
   foreach(var b in blocks){var node=Model(scene,Assets!,_catalog[b.Type].File,visual,"Escort part "+b.Id);node.Transform.LocalPosition=b.P;node.Transform.LocalRotation=b.Q;var output=Descendants(node).FirstOrDefault(o=>o.Name==OutputName(b.Type));Matrix4x4.Invert(node.Transform.WorldMatrix,out var inverse);var rest=output==null?Matrix4x4.Identity:output.Transform.WorldMatrix*inverse;CaptureDefinition(new(){Id=-1,Type=b.Type},node,output);parts.Add((b.Id,node,output,rest));}
   var spawn=RecoverySpawn+new Vector3(i==0?-4:4,0,i==0?-10:10);if(_sand?.Sample(spawn,out var ground,out _,out _,out _)==true)spawn.Y=ground.Y+.7f;
   int root=_physics.AddDrivenAssembly(blocks,_physicsDefinitions,spawn,Quaternion.Identity);
   var weak=scene.CreateGameObject("Escort turret target "+i);var target=weak.AddComponent(new DuneBountyTarget3D{MaximumHealth=140,Radius=1.2f});
   var turret=scene.CreateGameObject("Escort turret mount");turret.SetParent(weak,false);turret.AddComponent(new DuneRaceProp3D{Assets=Assets,ModelPath="Assets/RaceKit/convoy-turret.obj"});
   var brain=visual.AddComponent(new AiAgent3D{RouteObject=job.RouteObject,CruiseSpeed=job.ConvoySpeed+1,Driver=new ConvoyDriver(this,root,job.ConvoySpeed+1)});
   _convoyEscorts.Add(new(){Root=root,Visual=visual,Target=target,Brain=brain,Parts=parts});
  }
  _targetRefresh=-1;RenderConvoy();
 }
 void RenderConvoy()
 {
  if(!ConvoyMission||_physics==null||_convoyCargoBody==null)return;
  var cargo=_physics.RecoveryFrame(true);SetRenderedWorldPose(_convoyCargoBody,Pose(cargo.Position,cargo.Rotation));
  if(_convoyEngine!=null)SetRenderedWorldPose(_convoyEngine.GameObject,Pose(cargo.Position+Vector3.Transform(new(0,1.15f,.4f),cargo.Rotation),cargo.Rotation));
  foreach(var rig in _convoyEscorts){var frame=_physics.AssemblyFrame(rig.Root,true);SetRenderedWorldPose(rig.Visual,Pose(frame.Position,frame.Rotation));foreach(var (id,node,output,rest) in rig.Parts){var pose=_physics.RenderPartPose(rig.Root+id);SetRenderedWorldPose(node,Pose(pose.Position,pose.Rotation));if(output!=null)SetRenderedWorldPose(output,rest*_physics.RenderOutputDeformation(rig.Root+id)*node.Transform.WorldMatrix);}SetRenderedWorldPose(rig.Target.GameObject,Pose(frame.Position+Vector3.Transform(new(0,.7f,.35f),frame.Rotation),frame.Rotation));var aim=Transform.WorldPosition-rig.Target.Transform.WorldPosition;if(aim.LengthSquared()>.1f&&rig.Target.GameObject.Children.FirstOrDefault() is {} turret)turret.Transform.WorldRotation=FromTo(-Vector3.UnitZ,Vector3.Normalize(aim));}
 }
 bool ConvoyClearShot(Vector3 a,Vector3 b){var delta=b-a;float length=delta.Length();if(length<.1f)return false;if(_sand?.Heightfield.Cast(a,delta,0,length-.5f,out _,out _)==true)return false;foreach(var obstacle in GameObject.Scene!.GameObjects.Where(o=>o.ActiveInHierarchy).SelectMany(o=>o.Components).OfType<DuneWorldObstacle3D>()){if(!Matrix4x4.Invert(obstacle.Transform.WorldMatrix,out var inverse))continue;if(HitBox(Vector3.Transform(a,inverse),Vector3.TransformNormal(delta/length,inverse),obstacle.Center-obstacle.Size*.5f,obstacle.Center+obstacle.Size*.5f,out var t,out _)&&t<length)return false;}return true;}
 void ConvoyRocketSplash(Vector3 point){if(ConvoyMission&&_physics!=null&&Vector3.Distance(point,RecoveryPosition+Vector3.UnitY)<6)_convoyCargoHull=Math.Max(0,_convoyCargoHull-35);}
 void UpdateConvoy()
 {
  if(_physics==null||_convoyEngine==null||_convoyBrain==null)return;
  bool disabled=_convoyEngine.Defeated;_convoyBrain.Running=!disabled&&!_convoyFailed&&!_contractComplete;
  if(disabled)_physics.SetAssemblyInput(LandVehiclePhysics.RecoveryRootId,new(0,0,false));
  foreach(var rig in _convoyEscorts){rig.Brain.Running=!rig.Target.Defeated&&!_convoyFailed&&!_contractComplete;if(rig.Target.Defeated)_physics.SetAssemblyInput(rig.Root,new(0,0,true));}
  if(!_contractComplete&&!_convoyFailed){_convoyFireTime-=(float)Time.DeltaTime;if(_convoyFireTime<=0){_convoyFireTime=.9f;foreach(var rig in _convoyEscorts.Where(r=>!r.Target.Defeated)){var origin=rig.Target.Transform.WorldPosition+Vector3.UnitY*.35f;var hit=Transform.WorldPosition+Vector3.UnitY*.8f;if(Vector3.Distance(origin,hit)>65||!ConvoyClearShot(origin,hit))continue;_convoyHull=Math.Max(0,_convoyHull-12);Effect(VfxPreset.Impact,hit,Quaternion.Identity,.5f);var delta=hit-origin;if(_tracers.Count<24){var tracer=RecoveryPrimitive("Escort fire tracer",(origin+hit)*.5f,new(.025f,.025f,delta.Length()),new(1,.48f,.12f,1));tracer.Transform.WorldRotation=FromTo(Vector3.UnitZ,Vector3.Normalize(delta));_tracers.Add((tracer,.10f));}JobSound("metal-impact",.25f);}}}
  if(_convoyHull<=0||_convoyCargoHull<=0||RecoveryPosition.Y< -12){_convoyFailed=true;_convoyBrain.Stop();foreach(var rig in _convoyEscorts)rig.Brain.Stop();}
  UpdateAllWinches();
  bool secured=disabled&&WinchConnected&&!_convoyFailed&&DeliveryReady(DeliveryCenter);
  _deliveryHold=secured?_deliveryHold+(float)Time.DeltaTime:0;
  _convoyPaid=(int)MathF.Round(ContractReward(8)*(.55f+.45f*Math.Clamp(_convoyCargoHull/Convoy!.CargoHealth,0,1)));
  if(!_contractComplete&&_deliveryHold>1){_contractComplete=true;if(!AwardContract()){_contractComplete=false;return;}RecordContractCompletion();DisconnectWinch();}
  RecoveryObjective=_convoyFailed?"RIG OR CARGO LOST / R TO RESTART":_contractComplete?"CONVOY RECOVERED":!disabled?"Disable the stolen truck's exposed engine":WinchConnected?"Tow the cargo to the extraction yard":"Engine disabled / secure the cargo with your winch";
  Text("Battle objective",RecoveryObjective);Text("Recovery interaction",$"RIG {_convoyHull/Convoy.PlayerHealth:P0} / CARGO {_convoyCargoHull/Convoy.CargoHealth:P0} / SALVAGE ${_convoyPaid}\n"+(_convoyFailed?"R restarts the encounter":"LMB machine gun / F rockets / "+RecoveryConnectionHint));GarageActive("Recovery interaction",true);
  if(!_convoyFailed)ProjectRecoveryMarker((disabled&&WinchConnected?DeliveryCenter:_convoyEngine.Transform.WorldPosition)+Vector3.UnitY,disabled&&WinchConnected?"EXTRACTION":disabled?"SALVAGE TRUCK":"EXPOSED ENGINE");else GarageActive("Recovery marker label",false);
 }
 void ClearConvoy(){_convoyBrain=null;foreach(var rig in _convoyEscorts){if(rig.Target.GameObject.Scene is {} s)s.DestroyGameObject(rig.Target.GameObject);if(rig.Visual.Scene is {} scene)scene.DestroyGameObject(rig.Visual);}_convoyEscorts.Clear();if(_convoyEngine?.GameObject.Scene is {} a)a.DestroyGameObject(_convoyEngine.GameObject);_convoyEngine=null;if(_convoyCargoBody?.Scene is {} b)b.DestroyGameObject(_convoyCargoBody);_convoyCargoBody=null;_targetCache=[];_targetRefresh=-1;}
}
