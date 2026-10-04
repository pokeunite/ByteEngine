using ByteEngine.Core.Diagnostics;
using ByteEngine.Core;
using ByteEngine.Core.Construction;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
namespace GoblinScrapper.Construction;
/// <summary>Independent rigid bodies joined at authored attachment points. Motors supply torque, never vehicle translation.</summary>
public sealed partial class ContraptionPhysicsWorld : IDisposable
{
 // Bounded driving torque lets tire contact resolve unequal wheel speeds in a turn.
 // A near-hard velocity lock consumes the grip budget and can reverse the chassis yaw.
 private const float WheelDriveTorque=120,WheelBrakeTorque=10000;
 private readonly BufferPool _pool=new(); private readonly Simulation _simulation;
 private readonly Dictionary<ulong,int> _connectedPairs=new();private readonly Dictionary<int,float> _wheelFriction=new();
 private readonly Dictionary<int,(Vector3 Normal,float Depth)> _groundContacts=new();
 private readonly Dictionary<int,Piece> _pieces=new(); private readonly Dictionary<int,ConstraintHandle> _attachments=new();
 private readonly Dictionary<int,ConstraintHandle> _grabs=new();private readonly Dictionary<int,float> _health=new();private readonly HashSet<int> _exploded=new();
 public bool MechanismsActive=>_active;
 private readonly bool _usesSteeringJoints;
 private readonly VehicleAssembly _assembly;private int _traceGeneration=-1;private float _traceElapsed,_simulationTime;
 private float _accumulator; private bool _active;private int _projectileId;
 private readonly Dictionary<int,(BodyHandle Body,float Life)> _projectiles=new();
 public IEnumerable<(int Id,Vector3 Position)> Projectiles=>_projectiles.Select(p=>(p.Key,_simulation.Bodies[p.Value.Body].Pose.Position));
 public int Fire()
 {
  if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("FIRE","Weapon trigger");
  int count=0;foreach(var p in _pieces.Values)
  {
   if(p.Def.ReferenceId is 11 or 53 or 61)
   {
    var pose=Pose(p.Part.Id);var forward=Vector3.Transform(-Vector3.UnitZ,pose.Rotation);int number=p.Def.ReferenceId==53?7:1;
    for(int i=0;i<number;i++)
    {
     var direction=Vector3.Normalize(forward+Vector3.Transform(new Vector3((i%3-1)*.08f,(i/3-1)*.06f,0),pose.Rotation)*(number>1?1:0));var shape=new Sphere(.075f);
     var h=Body(pose.Position+forward*1.05f,Quaternion.Identity,shape.ComputeInertia(.4f),_simulation.Shapes.Add(shape));_simulation.Bodies[h].Velocity=new BodyVelocity(direction*(p.Def.ReferenceId==61?45:30)+Velocity(p.Part.Id));
     _projectiles[++_projectileId]=(h,5);Ignore(h,p.Root);Ignore(h,p.Output);
    }
    var body=_simulation.Bodies[p.Root];var v=body.Velocity;v.Linear-=forward*(number*.4f*30/p.Def.Mass);body.Velocity=v;count++;
   }
   if(p.Def.ReferenceId is 23 or 54){Explode(p);count++;}
   if(p.Def.ReferenceId==59){if(_attachments.Remove(p.Part.Id,out var joint))_simulation.Solver.Remove(joint);var body=_simulation.Bodies[p.Root];var v=body.Velocity;v.Linear+=Vector3.Transform(-Vector3.UnitZ,body.Pose.Orientation)*25;body.Velocity=v;count++;}
  }
  return count;
 }
 private void Explode(Piece source)
 {
  if(!_exploded.Add(source.Part.Id))return;
  var pos=_simulation.Bodies[source.Root].Pose.Position;
  var pushed=new HashSet<BodyHandle>();
  foreach(var p in _pieces.Values)
  {
   var handle=p.DirectWheelMount?p.Output:p.Root;var body=_simulation.Bodies[handle];var delta=body.Pose.Position-pos;float distance=delta.Length();if(distance>5)continue;
   var v=body.Velocity;v.Linear+=(distance<.01f?Vector3.UnitY:delta/distance)*(5-distance)*8/MathF.Sqrt(p.Def.Mass);if(pushed.Add(handle))body.Velocity=v;
   Damage(p.Part.Id,(5-distance)*120);
  }
 }

 private sealed class Piece
 {
  public required AssemblyPart Part; public required AssemblyPartDefinition Def; public BodyHandle Root,Output;
  public bool DirectWheelMount,WheelMountBound,WheelDetached;public Vector3 MountPositionLocal;public Quaternion MountRotationLocal=Quaternion.Identity;
  public float MotorTarget,SteeringAngle;
  public Vector3 RootCenter,OutputCenter; public Quaternion ShapeRotation=Quaternion.Identity;
  public ConstraintHandle? Motor,Linear,Steer;public Quaternion RestRelative; public Vector3 LocalAxis; public bool Slider;
 }
 public ContraptionPhysicsWorld(VehicleAssembly assembly,Vector3 origin,Quaternion rotation)
 {
  _assembly=assembly;
  _usesSteeringJoints=assembly.Parts.Values.Any(p=>assembly.Catalog[p.File].ReferenceId is 13 or 28);
  _simulation=Simulation.Create(_pool,new Contacts(_connectedPairs,_wheelFriction,_groundContacts),new Gravity(new(0,-9.81f,0)),new SolveDescription(16,8));
  AddObstacle(new(0,-.5f,0),new(100,1,100));
  foreach(var part in assembly.Parts.Values)
  {
   var d=assembly.Catalog[part.File]; var box=d.RootBounds??d.Bounds;
   var q=Quaternion.Normalize(rotation*part.Rotation);var pos=origin+Vector3.Transform(part.Position,rotation);
   var p=new Piece{Part=part,Def=d,RootCenter=(box.Min+box.Max)*.5f};
   bool directWheel=(d.ReferenceId is 2 or 40 or 46 or 60)&&part.Parent>=0;
   bool split=d.MovingBounds!=null&&d.ReferenceId is 2 or 5 or 9 or 12 or 13 or 14 or 16 or 17 or 18 or 19 or 22 or 27 or 28 or 38 or 39 or 40 or 42 or 44 or 46 or 48 or 50 or 51 or 60 or 77 or 79 or 86 or 88 or 95;
   p.Root=BoxBody(pos+Vector3.Transform(p.RootCenter,q),q,box.Max-box.Min,Math.Max(.2f,d.Mass*(split?.25f:1)+assembly.Braces.Values.Count(b=>b.A.Block==part.Id||b.B.Block==part.Id)*.25f));p.Output=p.Root;
   if(split)
   {
    var m=d.MovingCollision??d.MovingBounds!;p.DirectWheelMount=directWheel;p.OutputCenter=(m.Min+m.Max)*.5f;var size=Vector3.Max(m.Max-m.Min,new Vector3(.04f));
    bool round=d.ReferenceId is 2 or 17 or 38 or 39 or 40 or 46 or 48 or 51 or 60 or 88;
    if(round)
    {
     p.ShapeRotation=Align(Vector3.UnitY,d.Axis);var local=Vector3.Abs(d.Axis);float width=Vector3.Dot(size,local);float radius=Math.Max(size.X*(1-local.X),Math.Max(size.Y*(1-local.Y),size.Z*(1-local.Z)))*.5f;
     // PhysX's faceted tire hull chatters in Bepu at wheel speed. Use its measured
     // radius/width with Bepu's smooth rolling primitive; rendered geometry is unchanged.
     var shape=new Cylinder(Math.Max(.04f,radius),Math.Max(.04f,width));p.Output=Body(pos+Vector3.Transform(p.OutputCenter,q),Quaternion.Normalize(q*p.ShapeRotation),shape.ComputeInertia(d.Mass*(directWheel?1:.75f)),_simulation.Shapes.Add(shape));
    }
    else p.Output=BoxBody(pos+Vector3.Transform(p.OutputCenter,q),q,size,d.Mass*.75f);
    Ignore(p.Root,p.Output);var a=_simulation.Bodies[p.Root].Pose;var b=_simulation.Bodies[p.Output].Pose;var pivot=pos+Vector3.Transform(d.Pivot,q);
    var oa=Vector3.Transform(pivot-a.Position,Quaternion.Inverse(a.Orientation));var ob=Vector3.Transform(pivot-b.Position,Quaternion.Inverse(b.Orientation));var axis=Vector3.Transform(d.Axis,q);p.LocalAxis=Vector3.Transform(axis,Quaternion.Inverse(a.Orientation));
    if(!directWheel)
    {
    p.Slider=d.ReferenceId is 9 or 12 or 16 or 18 or 42;
    if(p.Slider)
    {
     _simulation.Solver.Add(p.Root,p.Output,new PointOnLineServo{LocalOffsetA=oa,LocalOffsetB=ob,LocalDirection=p.LocalAxis,SpringSettings=new(30,1),ServoSettings=new(20,0,2000)});
     _simulation.Solver.Add(p.Root,p.Output,new AngularServo{TargetRelativeRotationLocalA=Quaternion.Normalize(Quaternion.Inverse(a.Orientation)*b.Orientation),SpringSettings=new(30,1),ServoSettings=new(20,0,2000)});
     _simulation.Solver.Add(p.Root,p.Output,new LinearAxisLimit{LocalOffsetA=oa,LocalOffsetB=ob,LocalAxis=p.LocalAxis,MinimumOffset=-.30f,MaximumOffset=.4f,SpringSettings=new(30,1)});
     if(d.ReferenceId!=42)p.Linear=_simulation.Solver.Add(p.Root,p.Output,new LinearAxisServo{LocalOffsetA=oa,LocalOffsetB=ob,LocalPlaneNormal=p.LocalAxis,TargetOffset=0,SpringSettings=new(d.ReferenceId==16?4:20,1),ServoSettings=new(2,0,1500)});
    }
    else if(d.ReferenceId==44)_simulation.Solver.Add(p.Root,p.Output,new BallSocket{LocalOffsetA=oa,LocalOffsetB=ob,SpringSettings=new(30,1)});
    else
    {
     _simulation.Solver.Add(p.Root,p.Output,new Hinge{LocalOffsetA=oa,LocalOffsetB=ob,LocalHingeAxisA=p.LocalAxis,LocalHingeAxisB=Vector3.Transform(axis,Quaternion.Inverse(b.Orientation)),SpringSettings=new(d.ReferenceId is 2 or 40 or 46 or 50 or 60?180:60,1)});
     if(d.ReferenceId is 13 or 27 or 28 or 77 or 79 or 95)
     {
      p.RestRelative=Quaternion.Normalize(Quaternion.Inverse(a.Orientation)*b.Orientation);
      if(d.ReferenceId is 13 or 28)
      {
       var worldBasis=Align(Vector3.UnitZ,axis);
       _simulation.Solver.Add(p.Root,p.Output,new TwistLimit{LocalBasisA=Quaternion.Normalize(Quaternion.Inverse(a.Orientation)*worldBasis),LocalBasisB=Quaternion.Normalize(Quaternion.Inverse(b.Orientation)*worldBasis),MinimumAngle=-40*MathF.PI/180,MaximumAngle=40*MathF.PI/180,SpringSettings=new(30,1)});
      }
      p.Steer=_simulation.Solver.Add(p.Root,p.Output,new AngularServo{TargetRelativeRotationLocalA=p.RestRelative,SpringSettings=new(60,1),ServoSettings=new(3,0,40000)});
     }
     if(d.ReferenceId is 2 or 14 or 17 or 22 or 39 or 46 or 48)p.Motor=_simulation.Solver.Add(p.Root,p.Output,new AngularAxisMotor{LocalAxisA=p.LocalAxis,Settings=new(120,.001f)});
    }
   }
   }
   if(d.ReferenceId is 2 or 40 or 46 or 50 or 60 or 86)_wheelFriction[p.Output.Value]=.6f;
   _health[part.Id]=d.Kind=="beam"?80:d.Kind=="panel"?40:d.Kind=="armor"?400:200;
   _pieces.Add(part.Id,p);
  }
  var boundWheels=new HashSet<int>();
  void BindWheel(Piece p)
  {
   if(!p.DirectWheelMount||!boundWheels.Add(p.Part.Id))return;
   var parent=_pieces[p.Part.Parent];BindWheel(parent);
   var target=p.Part.ParentBone=="Root"?parent.Root:parent.Output;
   var mountPose=Pose(p.Part.Id);var a=_simulation.Bodies[target].Pose;
   var pivot=mountPose.Position+Vector3.Transform(p.Def.Pivot,mountPose.Rotation);
   var axis=Vector3.Transform(p.Def.Axis,mountPose.Rotation);var output=_simulation.Bodies[p.Output].Pose;
   foreach(var key in _connectedPairs.Keys.Where(k=>(uint)k==(uint)p.Root.Value||(uint)(k>>32)==(uint)p.Root.Value).ToArray())_connectedPairs.Remove(key);
   _simulation.Bodies.Remove(p.Root);p.Root=target;p.WheelMountBound=true;
   p.MountPositionLocal=Vector3.Transform(mountPose.Position-a.Position,Quaternion.Inverse(a.Orientation));
   p.MountRotationLocal=Quaternion.Normalize(Quaternion.Inverse(a.Orientation)*mountPose.Rotation);
   p.LocalAxis=Vector3.Transform(axis,Quaternion.Inverse(a.Orientation));
   _attachments[p.Part.Id]=_simulation.Solver.Add(target,p.Output,new Hinge{
    LocalOffsetA=Vector3.Transform(pivot-a.Position,Quaternion.Inverse(a.Orientation)),
    LocalOffsetB=Vector3.Transform(pivot-output.Position,Quaternion.Inverse(output.Orientation)),
    LocalHingeAxisA=p.LocalAxis,LocalHingeAxisB=Vector3.Transform(axis,Quaternion.Inverse(output.Orientation)),SpringSettings=new(180,1)});
   if(p.Def.ReferenceId is 2 or 46)p.Motor=_simulation.Solver.Add(target,p.Output,new AngularAxisMotor{LocalAxisA=p.LocalAxis,Settings=new(WheelBrakeTorque,.00001f)});
   Ignore(target,p.Output);
  }
  // One tire rigid body attached directly to the supporting frame/output, as in the source wheel prefab.
  foreach(var wheel in _pieces.Values.Where(p=>p.DirectWheelMount))BindWheel(wheel);
  foreach(var p in _pieces.Values)
  {
   if(p.DirectWheelMount)continue;
   if(p.Part.Parent<0)continue;var parent=_pieces[p.Part.Parent];var a=p.Part.ParentBone=="Root"?parent.Root:parent.Output;var b=p.Root;
   _attachments[p.Part.Id]=WeldBodies(a,b);Ignore(a,b);
   Ignore(parent.Root,b);Ignore(parent.Output,p.Output);
  }
  var pieces=_pieces.Values.ToArray();
  for(int i=0;i<pieces.Length;i++)for(int j=i+1;j<pieces.Length;j++)
  {
   var a=pieces[i];var b=pieces[j];if(a.Part.Parent==b.Part.Id||b.Part.Parent==a.Part.Id)continue;
   foreach(var sa in a.Def.Sockets.Where(s=>!s.Name.StartsWith("SOCKET_Surface_")))foreach(var sb in b.Def.Sockets.Where(s=>!s.Name.StartsWith("SOCKET_Surface_")))
   {
    var pa=a.Part.Position+Vector3.Transform(sa.Position,a.Part.Rotation);var pb=b.Part.Position+Vector3.Transform(sb.Position,b.Part.Rotation);
    if(Vector3.Distance(pa,pb)>.025f||Vector3.Dot(Vector3.Transform(sa.Normal,a.Part.Rotation),Vector3.Transform(sb.Normal,b.Part.Rotation))>-.995f)continue;
    var ba=sa.Bone=="Root"?a.Root:a.Output;var bb=sb.Bone=="Root"?b.Root:b.Output;if(ba!=bb){WeldBodies(ba,bb);Ignore(ba,bb);}
   }
  }
  foreach(var brace in assembly.Braces.Values)
  {
   BodyHandle Endpoint(BraceEndpoint e){var p=_pieces[e.Block];var socket=p.Def.Sockets.First(s=>s.Name==e.Socket);return socket.Bone=="Root"?p.Root:p.Output;}
   var a=Endpoint(brace.A);var b=Endpoint(brace.B);if(a!=b){
    var pa=_simulation.Bodies[a].Pose;var pb=_simulation.Bodies[b].Pose;
    Vector3 Point(BraceEndpoint e)=>origin+Vector3.Transform(assembly.BracePoint(e),rotation);
    var start=Point(brace.A);var end=Point(brace.B);
    // A two-ended brace constrains its span, not the orientations of both connected
    // bodies. Welding the bodies overconstrained sliders and rotating mechanisms.
    _simulation.Solver.Add(a,b,new DistanceServo{
     LocalOffsetA=Vector3.Transform(start-pa.Position,Quaternion.Inverse(pa.Orientation)),
     LocalOffsetB=Vector3.Transform(end-pb.Position,Quaternion.Inverse(pb.Orientation)),
     TargetDistance=Vector3.Distance(start,end),SpringSettings=new(30,1),ServoSettings=new(10,0,4000)});
   }
  }
  var gears=_pieces.Values.Where(p=>p.Def.ReferenceId is 38 or 39 or 51).ToArray();
  for(int i=0;i<gears.Length;i++)for(int j=i+1;j<gears.Length;j++)
  {
   var a=gears[i];var b=gears[j];var pa=_simulation.Bodies[a.Output].Pose;var pb=_simulation.Bodies[b.Output].Pose;
   var axisA=Vector3.Transform(Vector3.UnitY,pa.Orientation);var axisB=Vector3.Transform(Vector3.UnitY,pb.Orientation);var delta=pb.Position-pa.Position;
   float ra=CogGeometry.PitchRadius(a.Def),rb=CogGeometry.PitchRadius(b.Def);
   if(!CogGeometry.Meshes(a.Def,a.Part.Position,a.Part.Rotation,b.Def,b.Part.Position,b.Part.Rotation))continue;
   _simulation.Solver.Add(a.Output,b.Output,new AngularAxisGearMotor{LocalAxisA=Vector3.Transform(axisA,Quaternion.Inverse(pa.Orientation)),VelocityScale=-ra/rb,Settings=new(200,.001f)});Ignore(a.Output,b.Output);
  }

 }
 public void AddObstacle(Vector3 centre,Vector3 size)=>_simulation.Statics.Add(new StaticDescription(centre,_simulation.Shapes.Add(new Box(size.X,size.Y,size.Z))));
 private BodyHandle BoxBody(Vector3 pos,Quaternion q,Vector3 size,float mass){size=Vector3.Max(size,new(.04f));var shape=new Box(size.X,size.Y,size.Z);return Body(pos,q,shape.ComputeInertia(mass),_simulation.Shapes.Add(shape));}
 private BodyHandle Body(Vector3 pos,Quaternion q,BodyInertia inertia,TypedIndex shape)=>_simulation.Bodies.Add(BodyDescription.CreateDynamic(new RigidPose(pos,q),inertia,shape,.01f));
 private ConstraintHandle WeldBodies(BodyHandle a,BodyHandle b){var pa=_simulation.Bodies[a].Pose;var pb=_simulation.Bodies[b].Pose;return _simulation.Solver.Add(a,b,new Weld{LocalOffset=Vector3.Transform(pb.Position-pa.Position,Quaternion.Inverse(pa.Orientation)),LocalOrientation=Quaternion.Normalize(Quaternion.Inverse(pa.Orientation)*pb.Orientation),SpringSettings=new(1000,1)});}
 private void Ignore(BodyHandle a,BodyHandle b){if(a!=b)_connectedPairs[PairKey(a,b)]=1;}
 private static Quaternion Align(Vector3 from,Vector3 to){float dot=Vector3.Dot(from,to);return dot>.999f?Quaternion.Identity:dot<-.999f?Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI):Quaternion.Normalize(new Quaternion(Vector3.Cross(from,to),1+dot));}
 public (Vector3 Position,Quaternion Rotation) Pose(int id){var p=_pieces[id];var b=_simulation.Bodies[p.Root].Pose;if(p.WheelDetached){var q=Quaternion.Normalize(b.Orientation*Quaternion.Inverse(p.ShapeRotation));return(b.Position-Vector3.Transform(p.OutputCenter,q),q);}return p.WheelMountBound?(b.Position+Vector3.Transform(p.MountPositionLocal,b.Orientation),Quaternion.Normalize(b.Orientation*p.MountRotationLocal)):(b.Position-Vector3.Transform(p.RootCenter,b.Orientation),b.Orientation);}
 public Vector3 BracePoint(BraceEndpoint e){var p=_pieces[e.Block];var s=p.Def.Sockets.First(s=>s.Name==e.Socket);var pose=Pose(e.Block);var local=s.Bone=="Root"?s.Position:Vector3.Transform(s.Position,OutputDeformation(e.Block));return pose.Position+Vector3.Transform(local,pose.Rotation);}
 public bool HasPhysicalOutput(int id)=>_pieces[id].Root!=_pieces[id].Output;
 private static float MovingRadius(AssemblyPartDefinition definition)
 {
  var bounds=definition.MovingCollision??definition.MovingBounds!;var size=bounds.Max-bounds.Min;var axis=Vector3.Abs(definition.Axis);
  return Math.Max(size.X*(1-axis.X),Math.Max(size.Y*(1-axis.Y),size.Z*(1-axis.Z)))*.5f;
 }
 public float OutputAngularSpeed(int id)
 {
  var p=_pieces[id];var body=_simulation.Bodies[p.Output];var rotation=Quaternion.Normalize(body.Pose.Orientation*Quaternion.Inverse(p.ShapeRotation));
  return Vector3.Dot(body.Velocity.Angular-_simulation.Bodies[p.Root].Velocity.Angular,Vector3.Transform(p.Def.Axis,rotation));
 }
 public Vector3 Velocity(int id)=>_simulation.Bodies[_pieces[id].Root].Velocity.Linear;
 public Matrix4x4 OutputDeformation(int id)
 {
  var p=_pieces[id];if(p.Root==p.Output)return Matrix4x4.Identity;
  var output=_simulation.Bodies[p.Output].Pose;var q=Quaternion.Normalize(output.Orientation*Quaternion.Inverse(p.ShapeRotation));var pos=output.Position-Vector3.Transform(p.OutputCenter,q);var root=Pose(id);Matrix4x4.Invert(Matrix4x4.CreateFromQuaternion(root.Rotation)*Matrix4x4.CreateTranslation(root.Position),out var inv);
  return Matrix4x4.CreateFromQuaternion(q)*Matrix4x4.CreateTranslation(pos)*inv;
 }
 public void Activate()
 {
  _active=!_active;
  if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("MECHANISMS",$"active={_active}");
  foreach(var p in _pieces.Values)
  {
   if(p.Def.ReferenceId==4&&_attachments.Remove(p.Part.Id,out var joint))_simulation.Solver.Remove(joint);
   if(p.Def.ReferenceId==30)foreach(var child in _pieces.Values.Where(c=>c.Part.Parent==p.Part.Id))if(_attachments.Remove(child.Part.Id,out var hold))_simulation.Solver.Remove(hold);
   if(p.Def.ReferenceId is 27 or 77)
   {
    if(_grabs.Remove(p.Part.Id,out var grab))_simulation.Solver.Remove(grab);
    if(!_active)continue;
    var centre=_simulation.Bodies[p.Output].Pose.Position;
    var candidate=_pieces.Values.Where(o=>o.Part.Id!=0&&o.Part.Id!=p.Part.Id&&!_attachments.ContainsKey(o.Part.Id)).OrderBy(o=>Vector3.DistanceSquared(_simulation.Bodies[o.Root].Pose.Position,centre)).FirstOrDefault();
    if(candidate!=null&&Vector3.Distance(_simulation.Bodies[candidate.Root].Pose.Position,centre)<.8f){_grabs[p.Part.Id]=WeldBodies(p.Output,candidate.Root);Ignore(p.Output,candidate.Root);}
   }
  }
 }
 public void Step(float dt,float throttle,float steering,bool brake,bool powered,bool drift=false)
 {
  if(!float.IsFinite(dt)||dt<=0)return;
  _accumulator=Math.Min(_accumulator+dt,.1f);
  while(_accumulator>=1f/60){StepFixed(1f/60,throttle,steering,brake,powered,drift);_accumulator-=1f/60;}
 }
 private void StepFixed(float dt,float throttle,float steering,bool brake,bool powered,bool drift)
 {
  foreach(var key in _wheelFriction.Keys.ToArray())_wheelFriction[key]=drift?.18f:.6f;
  foreach(var p in _pieces.Values)
  {
   if(p.Motor is {} motor)
   {
    bool wheel=p.Def.ReferenceId is 2 or 46;
    float speed=wheel?(brake?0:throttle*(p.Def.ReferenceId==46?7.3f:7.5f)*80*MathF.PI/180):p.Def.ReferenceId==14?throttle*30:powered?25:0;
    if(_health[p.Part.Id]<=0)speed=0;
    if(wheel)
    {
     // Besiege CogMotorControllerHinge: block input, degreesPerSecond*80, smoothed target; no vehicle steering mix.
     speed*=Vector3.Dot(Vector3.Transform(p.Def.Axis,p.Part.Rotation),Vector3.UnitX)<-.1f?-1:1;
     p.MotorTarget+=(speed-p.MotorTarget)*Math.Clamp(dt*(p.Def.ReferenceId==46?8:16),0,1);
     speed=p.MotorTarget;
    }
    _simulation.Solver.ApplyDescription(motor,new AngularAxisMotor{LocalAxisA=p.LocalAxis,TargetVelocity=speed,Settings=new(wheel?(Math.Abs(throttle)<.001f||brake?WheelBrakeTorque:WheelDriveTorque):120,wheel?.00001f:.001f)});
   }
   if(p.Steer is {} servo)
   {
    if(Math.Abs(steering)>.001f||Math.Abs(p.SteeringAngle)>.001f){_simulation.Awakener.AwakenBody(p.Root);_simulation.Awakener.AwakenBody(p.Output);}
    float angle;
    if(p.Def.ReferenceId is 13 or 28)
    {
     // SteeringWheel: accumulate key-driven angle, 40-degree limits; hinge auto-return when released.
     float rate=-steering*100*MathF.PI/180;
     if(Math.Abs(steering)<.001f)rate=-Math.Sign(p.SteeringAngle)*60*MathF.PI/180;
     float next=p.SteeringAngle+rate*dt;
     if(Math.Abs(steering)<.001f&&Math.Sign(next)!=Math.Sign(p.SteeringAngle))next=0;
     p.SteeringAngle=Math.Clamp(next,-40*MathF.PI/180,40*MathF.PI/180);angle=p.SteeringAngle;
    }
    else angle=p.Def.ReferenceId is 27 or 77?(_active?.7f:0):steering*.6f;
    _simulation.Solver.ApplyDescription(servo,new AngularServo{TargetRelativeRotationLocalA=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(p.LocalAxis,angle)*p.RestRelative),SpringSettings=new(60,1),ServoSettings=new(3,0,40000)});
   }
   if(p.Linear is {} linear)
   {_simulation.Solver.GetDescription(linear,out LinearAxisServo d);d.TargetOffset=_active?(p.Def.ReferenceId is 9 or 16?-.3f:.4f):0;_simulation.Solver.ApplyDescription(linear,d);}
   var body=_simulation.Bodies[p.Root];var velocity=body.Velocity;float h=Math.Min(dt,.05f);
   if(!p.DirectWheelMount){velocity.Linear*=MathF.Exp(-p.Def.LinearDrag*dt);velocity.Angular*=MathF.Exp(-p.Def.AngularDrag*dt);}
   if(p.DirectWheelMount){var wheelBody=_simulation.Bodies[p.Output];var wheelVelocity=wheelBody.Velocity;wheelVelocity.Linear*=MathF.Exp(-p.Def.LinearDrag*dt);wheelVelocity.Angular*=MathF.Exp(-p.Def.AngularDrag*dt);wheelBody.Velocity=wheelVelocity;}
   // BalloonController prefab: lift 28, startup ramp .6 seconds; force units scale with mass 30 and length .5.
   if(p.Def.ReferenceId==43)velocity.Linear.Y+=420*Math.Clamp(_simulationTime/.6f,0,1)*h*body.LocalInertia.InverseMass;
   if(p.Def.ReferenceId is 26 or 55)
   {
    var rotor=_simulation.Bodies[p.Output];float rpm=Vector3.Dot(rotor.Velocity.Angular,Vector3.Transform(p.Def.Axis,body.Pose.Orientation));
    velocity.Linear+=Vector3.Transform(p.Def.Axis,body.Pose.Orientation)*(-rpm*Math.Abs(rpm)*.15f*h*body.LocalInertia.InverseMass);
   }
   if(powered&&p.Def.ReferenceId is 21 or 56 or 62)
   {
    var direction=Vector3.Transform(-Vector3.UnitZ,body.Pose.Orientation);
    foreach(var other in _pieces.Values)
    {
     if(other.Part.Id==p.Part.Id)continue;var target=_simulation.Bodies[other.Root];var delta=target.Pose.Position-body.Pose.Position;float distance=delta.Length();if(distance<.1f||distance>6||Vector3.Dot(delta/distance,direction)<.85f)continue;
     var tv=target.Velocity;tv.Linear+=delta/distance*(p.Def.ReferenceId==62?-30:20)*h/other.Def.Mass;target.Velocity=tv;if(p.Def.ReferenceId==21)Damage(other.Part.Id,80*h);
    }
   }
   if(p.Def.ReferenceId==14)velocity.Linear+=Vector3.Transform(p.Def.Axis,body.Pose.Orientation)*(throttle*450*h*body.LocalInertia.InverseMass);
   if(p.Def.ReferenceId is 25 or 34 or 79 or 89 or 95 or 97){float drag=p.Def.ReferenceId==97&&_active?3:.12f;velocity.Linear*=MathF.Exp(-drag*h);if(p.Def.ReferenceId is 25 or 34){var up=Vector3.Transform(Vector3.UnitY,body.Pose.Orientation);velocity.Linear+=up*Math.Min(300,velocity.Linear.LengthSquared()*.5f)*h*body.LocalInertia.InverseMass;}}
   body.Velocity=velocity;
  }
  foreach(var item in _projectiles.ToArray())
  {float life=item.Value.Life-dt;if(life<=0){_simulation.Bodies.Remove(item.Value.Body);_projectiles.Remove(item.Key);}else _projectiles[item.Key]=(item.Value.Body,life);}
  _groundContacts.Clear();_simulation.Timestep(dt);
  foreach(var projectile in _projectiles.ToArray())
  {
   var ball=_simulation.Bodies[projectile.Value.Body];var hit=_pieces.Values.FirstOrDefault(p=>p.Def.Bounds.Transform(Pose(p.Part.Id).Position,Pose(p.Part.Id).Rotation).Intersects(new AssemblyBox(ball.Pose.Position-new Vector3(.075f),ball.Pose.Position+new Vector3(.075f)),0));
   if(hit!=null){Damage(hit.Part.Id,60);_simulation.Bodies.Remove(projectile.Value.Body);_projectiles.Remove(projectile.Key);}
  }
  foreach(var p in _pieces.Values)
  {
   if(p.Def.ReferenceId is 23 or 59 && !_exploded.Contains(p.Part.Id)&&_simulation.Bodies[p.Root].Pose.Position.Y<.35f)Explode(p);
   bool sharp=p.Def.ReferenceId is 3 or 17 or 20 or 33 or 48;bool hot=p.Def.ReferenceId is 31 or 47;
   if(!sharp&&!hot)continue;var box=p.Def.MovingBounds??p.Def.Bounds;var pose=Pose(p.Part.Id);var contact=box.Transform(pose.Position,pose.Rotation);
   foreach(var target in _pieces.Values)
   {
    if(target.Part.Id==0||target.Part.Id==p.Part.Id||target.Part.Id==p.Part.Parent||target.Part.Parent==p.Part.Id)continue;var targetPose=Pose(target.Part.Id);
    if(!contact.Intersects(target.Def.Bounds.Transform(targetPose.Position,targetPose.Rotation),0))continue;
    float speed=(_simulation.Bodies[p.Output].Velocity.Linear-_simulation.Bodies[target.Root].Velocity.Linear).Length()+_simulation.Bodies[p.Output].Velocity.Angular.Length()*.3f;
    if(hot||speed>2)Damage(target.Part.Id,(hot?35:speed*12)*Math.Min(dt,.05f));
   }
  }
  TracePhysics(dt,throttle,steering,brake,powered,drift);
 }
 private void TracePhysics(float dt,float throttle,float steering,bool brake,bool powered,bool drift)
 {
  _simulationTime+=dt;
  if(!ConstructionDiagnostics.Enabled){_traceElapsed=0;return;}
  if(_traceGeneration!=ConstructionDiagnostics.Generation)
  {
   _traceGeneration=ConstructionDiagnostics.Generation;
   ConstructionDiagnostics.Record("PHYSICS SNAPSHOT",$"plugin=0.4.0 catalog={_assembly.Catalog.Revision} steering={(_usesSteeringJoints?"hinges":"fixed axles (A/D does not drive wheels)")} wheelDriveTorque=120 autoBrakeTorque=10000 (auto-brake enabled) solver=16iterations/8substeps build={_assembly.ToJson()}");
   foreach(var p in _pieces.Values)ConstructionDiagnostics.Record("BODY SETUP",$"id={p.Part.Id} file={p.Part.File} parent={p.Part.Parent}/{p.Part.ParentBone} target={p.Part.ParentConnector} source={p.Part.OwnConnector} authoredPosition={p.Part.Position} authoredRotation={p.Part.Rotation} mass={p.Def.Mass} axis={p.Def.Axis} pivot={p.Def.Pivot} rootBounds={p.Def.RootBounds} tireBounds={p.Def.MovingCollision} rootBody={p.Root.Value} outputBody={p.Output.Value} motor={p.Motor.HasValue}");
   _traceElapsed=.25f;
  }
  _traceElapsed+=dt;if(_traceElapsed<.25f)return;_traceElapsed=0;
  var text=new System.Text.StringBuilder();var master=Pose(0);
  text.AppendLine(FormattableString.Invariant($"t={_simulationTime:F3} dt={dt:F4} throttle={throttle:F2} steering={steering:F2} brake={brake} power={powered} drift={drift} mode={(_usesSteeringJoints?"hinges":"fixed axles (A/D does not drive wheels)")} masterPosition={master.Position} rotation={master.Rotation} velocity={Velocity(0)} up={Vector3.Transform(Vector3.UnitY,master.Rotation).Y:F4}"));
  foreach(var p in _pieces.Values)
  {
   var root=_simulation.Bodies[p.Root];var output=_simulation.Bodies[p.Output];float attachmentGap=0,attachmentAngle=0;
   if(p.Part.Parent>=0&&_pieces.TryGetValue(p.Part.Parent,out var parent))
   {
    var relative=Quaternion.Normalize(Quaternion.Inverse(Pose(parent.Part.Id).Rotation)*Pose(p.Part.Id).Rotation);
    var authored=Quaternion.Normalize(Quaternion.Inverse(parent.Part.Rotation)*p.Part.Rotation);
    if(p.Part.ParentBone=="Root")attachmentAngle=2*MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(relative,authored)),0,1))*180/MathF.PI;
    var target=parent.Def.Sockets.FirstOrDefault(x=>x.Name==p.Part.ParentConnector);var source=p.Def.Sockets.FirstOrDefault(x=>x.Name==p.Part.OwnConnector);
    if(target!=null&&source!=null){var parentPose=Pose(parent.Part.Id);var targetPoint=target.Bone=="Root"?target.Position:Vector3.Transform(target.Position,OutputDeformation(parent.Part.Id));var pose=Pose(p.Part.Id);attachmentGap=Vector3.Distance(parentPose.Position+Vector3.Transform(targetPoint,parentPose.Rotation),pose.Position+Vector3.Transform(source.Position,pose.Rotation));}
   }
   var actualAxle=Vector3.Transform(Vector3.UnitY,output.Pose.Orientation);var expectedAxle=Vector3.Transform(p.Def.Axis,Pose(p.Part.Id).Rotation);float hubGap=p.Root==p.Output?0:Vector3.Distance(p.Def.Pivot,Vector3.Transform(p.Def.Pivot,OutputDeformation(p.Part.Id)));float wobble=p.Root==p.Output?0:MathF.Acos(Math.Clamp(Vector3.Dot(actualAxle,expectedAxle),-1,1))*180/MathF.PI;
   var bounds=p.Def.MovingCollision??p.Def.MovingBounds;float radius=bounds==null?0:MovingRadius(p.Def);bool near=output.Pose.Position.Y<=radius+.08f;
   text.AppendLine(FormattableString.Invariant($"part={p.Part.Id} {p.Part.File} parent={p.Part.Parent}/{p.Part.ParentBone} position={Pose(p.Part.Id).Position} velocity={root.Velocity.Linear} angular={root.Velocity.Angular} jointPresent={_attachments.ContainsKey(p.Part.Id)} attachmentGap={attachmentGap:F5} mountAngleDeg={attachmentAngle:F3} health={_health[p.Part.Id]:F2}"));
   if(_wheelFriction.ContainsKey(p.Output.Value))text.AppendLine(FormattableString.Invariant($" TIRE targetRadPerSec={p.MotorTarget:F3} steeringTargetDeg={p.SteeringAngle*180/MathF.PI:F3} rpm={OutputAngularSpeed(p.Part.Id)*60/(2*MathF.PI):F2} groundNear={near} groundContactReported={_groundContacts.ContainsKey(p.Output.Value)} contactDepth={(_groundContacts.TryGetValue(p.Output.Value,out var ground)?ground.Depth:0):F5} axle={actualAxle} wobbleDeg={wobble:F3} hubGap={hubGap:F5} lateralSpeed={Vector3.Dot(output.Velocity.Linear,actualAxle):F4} friction={_wheelFriction[p.Output.Value]:F3} center={output.Pose.Position} radius={radius:F3} warning={(wobble>2||attachmentGap>.025f||hubGap>.025f?"CHECK JOINT":"none")}"));
   if(p.Steer.HasValue||p.Linear.HasValue)text.AppendLine($" OUTPUT deformation={OutputDeformation(p.Part.Id)}");
  }
  ConstructionDiagnostics.Record("PHYSICS",text.ToString(),true);
 }
 private void Damage(int id,float damage)
 {
  if(id==0||damage<=0)return;_health[id]-=damage;
  if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("DAMAGE",$"id={id} damage={damage:F3} health={_health[id]:F3}");
  if(_health[id]<=0&&_attachments.Remove(id,out var joint))
  {
   _simulation.Solver.Remove(joint);var p=_pieces[id];
   if(p.DirectWheelMount)
   {
    if(p.Motor is {} motor){_simulation.Solver.Remove(motor);p.Motor=null;}
    _connectedPairs.Remove(PairKey(p.Root,p.Output));p.Root=p.Output;
    p.WheelMountBound=false;p.DirectWheelMount=false;p.WheelDetached=true;
    _simulation.Awakener.AwakenBody(p.Output);
   }
  }
 }
 public void Dispose(){if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("PHYSICS STOP",$"time={_simulationTime:F3}");_simulation.Dispose();_pool.Clear();}
    private static ulong PairKey(BodyHandle a, BodyHandle b)
    {
        uint low = (uint)Math.Min(a.Value, b.Value);
        uint high = (uint)Math.Max(a.Value, b.Value);
        return ((ulong)low << 32) | high;
    }

    private struct Contacts : INarrowPhaseCallbacks
    {
        private readonly Dictionary<ulong, int> _connectedPairs;private readonly Dictionary<int,float> _friction;
        private readonly Dictionary<int,(Vector3 Normal,float Depth)> _groundContacts;
        public Contacts(Dictionary<ulong, int> connectedPairs,Dictionary<int,float> friction,Dictionary<int,(Vector3 Normal,float Depth)> groundContacts){_connectedPairs=connectedPairs;_friction=friction;_groundContacts=groundContacts;}
        public void Initialize(Simulation simulation) { }
        public bool AllowContactGeneration(int workerIndex, CollidableReference a,
            CollidableReference b, ref float speculativeMargin)
        {
            if (a.Mobility != CollidableMobility.Dynamic &&
                b.Mobility != CollidableMobility.Dynamic) return false;
            return a.Mobility == CollidableMobility.Static ||
                   b.Mobility == CollidableMobility.Static ||
                   !_connectedPairs.ContainsKey(PairKey(a.BodyHandle, b.BodyHandle));
        }
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair,
            int childIndexA, int childIndexB) => true;
        private float ContactFriction(CollidablePair pair)
        {
            bool wheelA=pair.A.Mobility!=CollidableMobility.Static&&_friction.ContainsKey(pair.A.BodyHandle.Value);
            bool wheelB=pair.B.Mobility!=CollidableMobility.Static&&_friction.ContainsKey(pair.B.BodyHandle.Value);
            float a=wheelA?_friction[pair.A.BodyHandle.Value]:1.5f,b=wheelB?_friction[pair.B.BodyHandle.Value]:1.5f;
            return wheelA||wheelB?a*b:Math.Min(a,b);
        }
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair,
            ref TManifold manifold, out PairMaterialProperties material)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            if(pair.A.Mobility==CollidableMobility.Static||pair.B.Mobility==CollidableMobility.Static)
            {
                for(int i=0;i<manifold.Count;i++)
                {
                    manifold.GetContact(i,out var offset,out var normal,out float depth,out int feature);
                    if(depth>=-.005f&&Math.Abs(normal.Y)>.5f)
                    {
                        var dynamic=pair.A.Mobility==CollidableMobility.Static?pair.B:pair.A;
                        if(!_groundContacts.TryGetValue(dynamic.BodyHandle.Value,out var previous)||depth>previous.Depth)_groundContacts[dynamic.BodyHandle.Value]=(normal,depth);
                    }
                }
            }
            material = new PairMaterialProperties
            {
                // WheelU5 uses PhysicMaterialCombine.Multiply, not the minimum-material rule.
                FrictionCoefficient = ContactFriction(pair),
                MaximumRecoveryVelocity = .2f,
                SpringSettings = new SpringSettings(30, 1)
            };
            return true;
        }
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair,
            int childIndexA, int childIndexB, ref ConvexContactManifold manifold) => true;
        public void Dispose() { }
    }

    private struct Gravity : IPoseIntegratorCallbacks
    {
        private readonly Vector3 _gravity;
        private Vector3Wide _gravityDt;
        public Gravity(Vector3 gravity) => _gravity = gravity;
        public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
        public bool AllowSubstepsForUnconstrainedBodies => false;
        public bool IntegrateVelocityForKinematics => false;
        public void Initialize(Simulation simulation) { }
        public void PrepareForIntegration(float dt) => _gravityDt = Vector3Wide.Broadcast(_gravity * dt);
        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position,
            QuaternionWide orientation, BodyInertiaWide localInertia,
            Vector<int> integrationMask, int workerIndex, Vector<float> dt,
            ref BodyVelocityWide velocity) => velocity.Linear += _gravityDt;
    }
}
