using System.Numerics;
using BepuPhysics;
using BepuPhysics.Constraints;
namespace DuneCompany;
public sealed partial class LandVehiclePhysics
{
 public const int RecoveryRootId=10000;
 ConstraintHandle? _recoveryConnection;int _connectedHinge=-1;Vector3 _recoveryHitch;
 public bool RecoveryConnected=>_recoveryConnection.HasValue;
 public bool HasRecovery=>_parts.ContainsKey(RecoveryRootId);
 public float RecoveryMass {get;private set;}
 public void AddRecoveryVehicle(IReadOnlyList<PlacedBlock> blocks,IReadOnlyDictionary<int,VehiclePartPhysics> definitions,Vector3 position,Quaternion rotation,Vector3 hitch)
 {
  if(HasRecovery)throw new InvalidOperationException("Only one recovery target is supported.");
  var copies=blocks.Select(b=>System.Text.Json.JsonSerializer.Deserialize<PlacedBlock>(System.Text.Json.JsonSerializer.Serialize(b,BuildStore.Json),BuildStore.Json)!).ToArray();
  foreach(var b in copies){b.Id+=RecoveryRootId;if(b.Parent>=0)b.Parent+=RecoveryRootId;b.Power=0;}
  AddAssembly(copies,definitions,position,rotation);_recoveryHitch=hitch;RecoveryMass=copies.Sum(b=>_catalog[b.Type].Mass);
 }
 public (Vector3 Position,Quaternion Rotation) RecoveryFrame(bool rendered=false){var p=_parts[RecoveryRootId];var pose=rendered?RenderPose(p.Root):_simulation.Bodies[p.Root.Body].Pose;return(pose.Position-Vector3.Transform(p.Root.Center,pose.Orientation),pose.Orientation);}
 public Vector3 RecoveryVelocity=>HasRecovery?_simulation.Bodies[_parts[RecoveryRootId].Root.Body].Velocity.Linear:Vector3.Zero;
 public Vector3 TargetHitch {get{var p=RecoveryFrame();return p.Position+Vector3.Transform(_recoveryHitch,p.Rotation);}}
 public Vector3 TrailerHitch(int id){var p=_parts[id];var pose=OutputPose(id);return pose.Position+Vector3.Transform(new Vector3(p.Definition.Moving.Center.X,p.Definition.Moving.Center.Y,p.Definition.Moving.Low.Z),pose.Rotation);}
 public string HitchAvailability(int id)
 {
  if(!HasRecovery)return "No recovery target";if(RecoveryConnected)return "Already connected";
  if(!_parts.TryGetValue(id,out var part)||part.Block.Type!=20||part.Output==null||id>=RecoveryRootId)return "Fit a trailer hinge";
  float distance=Vector3.Distance(TrailerHitch(id),TargetHitch);if(distance>.55f)return "Move hinge within 0.55 m of hitch";
  var a=OutputPose(id);var b=RecoveryFrame();var direction=Vector3.Normalize(Vector3.Transform(Vector3.UnitZ,a.Rotation));var target=Vector3.Normalize(Vector3.Transform(Vector3.UnitZ,b.Rotation));
  // Hinge mounting axes vary with placement; require vehicle headings to agree for the first hitch.
  var truck=Frame;var forwardA=Vector3.Transform(Vector3.UnitZ,truck.Rotation);var forwardB=Vector3.Transform(Vector3.UnitZ,b.Rotation);
  if(Vector3.Dot(forwardA,forwardB)<.75f||Vector3.Dot(Vector3.Transform(-Vector3.UnitZ,a.Rotation),Vector3.Transform(-Vector3.UnitZ,b.Rotation))>-.75f)return "Align vehicles within 40 degrees";
  if(RecoveryOverlapsTruck())return "Separate the vehicles before connecting";
  if(Velocity.Length()>1.2f||RecoveryVelocity.Length()>1.2f)return "Stop both vehicles to connect";
  return "Ready";
 }
 bool RecoveryOverlapsTruck()
 {
  (Vector3 low,Vector3 high) Bounds(Part p,bool output){var pose=output?OutputPose(p.Block.Id):PartPose(p.Block.Id);var bounds=output?p.Definition.Moving:p.Definition.Fixed;Vector3 low=new(float.MaxValue),high=new(float.MinValue);for(int i=0;i<8;i++){var point=pose.Position+Vector3.Transform(new Vector3((i&1)==0?bounds.Low.X:bounds.High.X,(i&2)==0?bounds.Low.Y:bounds.High.Y,(i&4)==0?bounds.Low.Z:bounds.High.Z),pose.Rotation);low=Vector3.Min(low,point);high=Vector3.Max(high,point);}return(low,high);}
  var a=_parts.Values.Where(p=>p.Block.Id<RecoveryRootId).SelectMany(p=>p.Output==null?new[]{Bounds(p,false)}:new[]{Bounds(p,false),Bounds(p,true)}).ToArray();var b=_parts.Values.Where(p=>p.Block.Id>=RecoveryRootId).SelectMany(p=>p.Output==null?new[]{Bounds(p,false)}:new[]{Bounds(p,false),Bounds(p,true)}).ToArray();
  return a.Any(x=>b.Any(y=>Vector3.Min(x.high,y.high).X-Vector3.Max(x.low,y.low).X>.1f&&Vector3.Min(x.high,y.high).Y-Vector3.Max(x.low,y.low).Y>.1f&&Vector3.Min(x.high,y.high).Z-Vector3.Max(x.low,y.low).Z>.1f));
 }
 public bool ConnectRecovery(int hinge)
 {
  if(HitchAvailability(hinge)!="Ready")return false;var part=_parts[hinge];var output=part.Output!;var target=_parts[RecoveryRootId].Root;var a=_simulation.Bodies[output.Body].Pose;var b=_simulation.Bodies[target.Body].Pose;
  // Preserve both poses. Matching offsets bridge only the validated short hitch distance,
  // rather than teleporting a vehicle into another collider or injecting correction energy.
  _recoveryConnection=_simulation.Solver.Add(output.Body,target.Body,new Weld{LocalOffset=Vector3.Transform(b.Position-a.Position,Quaternion.Inverse(a.Orientation)),LocalOrientation=Quaternion.Normalize(Quaternion.Inverse(a.Orientation)*b.Orientation),SpringSettings=new(35,1)});
  _connectedHinge=hinge;_ignored.Add(Pair(output.Body,target.Body));_simulation.Awakener.AwakenBody(output.Body);_simulation.Awakener.AwakenBody(target.Body);return true;
 }
 public void DisconnectRecovery(){if(_recoveryConnection is {} handle){_simulation.Solver.Remove(handle);if(_parts.TryGetValue(_connectedHinge,out var hinge))_ignored.Remove(Pair(hinge.Output!.Body,_parts[RecoveryRootId].Root.Body));}_recoveryConnection=null;_connectedHinge=-1;}
 public void ResetRecovery(Vector3 position,Quaternion rotation)
 {
  DisconnectWinch();DisconnectRecovery();if(!HasRecovery)return;ResetAssembly(_parts.Values.Where(p=>p.Block.Id>=RecoveryRootId),position,rotation);
 }
 public void ResetTruck(Vector3 position,Quaternion rotation){DisconnectWinch();DisconnectRecovery();ResetAssembly(_parts.Values.Where(p=>p.Block.Id<RecoveryRootId),position,rotation);}
 void ResetAssembly(IEnumerable<Part> parts,Vector3 position,Quaternion rotation){foreach(var group in parts.SelectMany(p=>p.Output==null?new[]{p.Root}:new[]{p.Root,p.Output}).Distinct()){var body=_simulation.Bodies[group.Body];body.Pose=new(position+Vector3.Transform(group.Center,rotation),rotation);body.Velocity=default;_previousPoses.Remove(group.Body.Value);_simulation.Awakener.AwakenBody(group.Body);} _contacts.Clear();}
 void ApplyRecoveryWheelContacts(Func<Vector3,float>? grip,Func<Vector3,float>? resistance,float dt)
 {
  var wheels=_parts.Values.Where(p=>p.Block.Id>=RecoveryRootId&&_catalog[p.Block.Type].Wheel).ToArray();foreach(var p in wheels){var wheel=_simulation.Bodies[p.Output!.Body];_friction[p.Output.Body.Value]=Math.Clamp(p.Block.Grip*(grip?.Invoke(wheel.Pose.Position)??.8f),.05f,3);if(resistance!=null&&_contacts.ContainsKey(p.Output.Body.Value)){var v=wheel.Velocity.Linear;v.Y=0;float speed=v.Length();if(speed>.01f)wheel.ApplyLinearImpulse(-v/speed*Math.Min(RecoveryMass*9.81f/wheels.Length*Math.Clamp(resistance(wheel.Pose.Position),0,2),RecoveryMass/wheels.Length*speed/dt)*dt);}}
 }
}
