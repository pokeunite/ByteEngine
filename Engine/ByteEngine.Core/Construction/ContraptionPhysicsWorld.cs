using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
namespace ByteEngine.Core.Construction;
/// <summary>Independent rigid bodies joined at authored attachment points. Motors supply torque, never vehicle translation.</summary>
public sealed class ContraptionPhysicsWorld : IDisposable
{
 private readonly BufferPool _pool=new(); private readonly Simulation _simulation;
 private readonly Dictionary<ulong,int> _connectedPairs=new();private readonly Dictionary<int,float> _wheelFriction=new();
 private readonly Dictionary<int,Piece> _pieces=new(); private readonly Dictionary<int,ConstraintHandle> _attachments=new();
 private readonly Dictionary<int,ConstraintHandle> _grabs=new();private readonly Dictionary<int,float> _health=new();private readonly HashSet<int> _exploded=new();
 public bool MechanismsActive=>_active;
 private float _accumulator; private bool _active;private int _projectileId;
 private readonly Dictionary<int,(BodyHandle Body,float Life)> _projectiles=new();
 public IEnumerable<(int Id,Vector3 Position)> Projectiles=>_projectiles.Select(p=>(p.Key,_simulation.Bodies[p.Value.Body].Pose.Position));
 public int Fire()
 {
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
  foreach(var p in _pieces.Values)
  {
   var body=_simulation.Bodies[p.Root];var delta=body.Pose.Position-pos;float distance=delta.Length();if(distance>5)continue;
   var v=body.Velocity;v.Linear+=(distance<.01f?Vector3.UnitY:delta/distance)*(5-distance)*8/MathF.Sqrt(p.Def.Mass);body.Velocity=v;
   Damage(p.Part.Id,(5-distance)*120);
  }
 }

 private sealed class Piece
 {
  public required AssemblyPart Part; public required AssemblyPartDefinition Def; public BodyHandle Root,Output;
  public Vector3 RootCenter,OutputCenter; public Quaternion ShapeRotation=Quaternion.Identity;
  public ConstraintHandle? Motor,Linear,Steer;public Quaternion RestRelative; public Vector3 LocalAxis; public bool Slider;
 }
 public ContraptionPhysicsWorld(VehicleAssembly assembly,Vector3 origin,Quaternion rotation)
 {
  _simulation=Simulation.Create(_pool,new Contacts(_connectedPairs,_wheelFriction),new Gravity(new(0,-9.81f,0)),new SolveDescription(12,4));
  AddObstacle(new(0,-.5f,0),new(100,1,100));
  foreach(var part in assembly.Parts.Values)
  {
   var d=assembly.Catalog[part.File]; var box=d.RootBounds??d.Bounds;
   var q=Quaternion.Normalize(rotation*part.Rotation);var pos=origin+Vector3.Transform(part.Position,rotation);
   var p=new Piece{Part=part,Def=d,RootCenter=(box.Min+box.Max)*.5f};
   bool split=d.MovingBounds!=null&&d.ReferenceId is 2 or 5 or 9 or 12 or 13 or 14 or 16 or 17 or 18 or 19 or 22 or 27 or 28 or 38 or 39 or 40 or 42 or 44 or 46 or 48 or 50 or 51 or 60 or 77 or 79 or 86 or 88 or 95;
   p.Root=BoxBody(pos+Vector3.Transform(p.RootCenter,q),q,box.Max-box.Min,Math.Max(.2f,d.Mass*(split?.25f:1)));p.Output=p.Root;
   if(split)
   {
    var m=d.MovingBounds!;p.OutputCenter=(m.Min+m.Max)*.5f;var size=Vector3.Max(m.Max-m.Min,new Vector3(.04f));
    bool round=d.ReferenceId is 2 or 17 or 38 or 39 or 40 or 46 or 48 or 51 or 60 or 88;
    if(round)
    {
     p.ShapeRotation=Align(Vector3.UnitY,d.Axis);var local=Vector3.Abs(d.Axis);float width=Vector3.Dot(size,local);float radius=Math.Max(size.X*(1-local.X),Math.Max(size.Y*(1-local.Y),size.Z*(1-local.Z)))*.5f;
     var shape=new Cylinder(Math.Max(.04f,radius),Math.Max(.04f,width));p.Output=Body(pos+Vector3.Transform(p.OutputCenter,q),Quaternion.Normalize(q*p.ShapeRotation),shape.ComputeInertia(d.Mass*.75f),_simulation.Shapes.Add(shape));
    }
    else p.Output=BoxBody(pos+Vector3.Transform(p.OutputCenter,q),q,size,d.Mass*.75f);
    Ignore(p.Root,p.Output);var a=_simulation.Bodies[p.Root].Pose;var b=_simulation.Bodies[p.Output].Pose;var pivot=pos+Vector3.Transform(d.Pivot,q);
    var oa=Vector3.Transform(pivot-a.Position,Quaternion.Inverse(a.Orientation));var ob=Vector3.Transform(pivot-b.Position,Quaternion.Inverse(b.Orientation));var axis=Vector3.Transform(d.Axis,q);p.LocalAxis=Vector3.Transform(axis,Quaternion.Inverse(a.Orientation));
    p.Slider=d.ReferenceId is 9 or 12 or 16 or 18 or 42;
    if(p.Slider)
    {
     _simulation.Solver.Add(p.Root,p.Output,new PointOnLineServo{LocalOffsetA=oa,LocalOffsetB=ob,LocalDirection=p.LocalAxis,SpringSettings=new(30,1),ServoSettings=new(20,0,2000)});
     _simulation.Solver.Add(p.Root,p.Output,new AngularServo{TargetRelativeRotationLocalA=Quaternion.Normalize(b.Orientation*Quaternion.Inverse(a.Orientation)),SpringSettings=new(30,1),ServoSettings=new(20,0,2000)});
     _simulation.Solver.Add(p.Root,p.Output,new LinearAxisLimit{LocalOffsetA=oa,LocalOffsetB=ob,LocalAxis=p.LocalAxis,MinimumOffset=-.30f,MaximumOffset=.4f,SpringSettings=new(30,1)});
     if(d.ReferenceId!=42)p.Linear=_simulation.Solver.Add(p.Root,p.Output,new LinearAxisServo{LocalOffsetA=oa,LocalOffsetB=ob,LocalPlaneNormal=p.LocalAxis,TargetOffset=0,SpringSettings=new(d.ReferenceId==16?4:20,1),ServoSettings=new(2,0,1500)});
    }
    else if(d.ReferenceId==44)_simulation.Solver.Add(p.Root,p.Output,new BallSocket{LocalOffsetA=oa,LocalOffsetB=ob,SpringSettings=new(30,1)});
    else
    {
     _simulation.Solver.Add(p.Root,p.Output,new Hinge{LocalOffsetA=oa,LocalOffsetB=ob,LocalHingeAxisA=p.LocalAxis,LocalHingeAxisB=Vector3.Transform(axis,Quaternion.Inverse(b.Orientation)),SpringSettings=new(30,1)});
     if(d.ReferenceId is 13 or 27 or 28 or 77 or 79 or 95)
     {
      p.RestRelative=Quaternion.Normalize(b.Orientation*Quaternion.Inverse(a.Orientation));
      p.Steer=_simulation.Solver.Add(p.Root,p.Output,new AngularServo{TargetRelativeRotationLocalA=p.RestRelative,SpringSettings=new(12,1),ServoSettings=new(3,0,300)});
     }
     if(d.ReferenceId is 2 or 14 or 17 or 22 or 39 or 46 or 48)p.Motor=_simulation.Solver.Add(p.Root,p.Output,new AngularAxisMotor{LocalAxisA=p.LocalAxis,Settings=new(120,.001f)});
    }
   }
   if(d.ReferenceId is 2 or 40 or 46 or 50 or 60 or 86)_wheelFriction[p.Output.Value]=1.5f;
   _health[part.Id]=d.Kind=="beam"?80:d.Kind=="panel"?40:d.Kind=="armor"?400:200;
   _pieces.Add(part.Id,p);
  }
  foreach(var p in _pieces.Values)
  {
   if(p.Part.Parent<0)continue;var parent=_pieces[p.Part.Parent];var a=p.Part.ParentBone=="Root"?parent.Root:parent.Output;var b=p.Root;
   _attachments[p.Part.Id]=WeldBodies(a,b);Ignore(a,b);
   Ignore(parent.Root,b);Ignore(parent.Output,p.Output);
  }
  var pieces=_pieces.Values.ToArray();
  for(int i=0;i<pieces.Length;i++)for(int j=i+1;j<pieces.Length;j++)
  {
   var a=pieces[i];var b=pieces[j];if(a.Part.Parent==b.Part.Id||b.Part.Parent==a.Part.Id)continue;
   foreach(var sa in a.Def.Sockets)foreach(var sb in b.Def.Sockets)
   {
    var pa=a.Part.Position+Vector3.Transform(sa.Position,a.Part.Rotation);var pb=b.Part.Position+Vector3.Transform(sb.Position,b.Part.Rotation);
    if(Vector3.Distance(pa,pb)>.025f||Vector3.Dot(Vector3.Transform(sa.Normal,a.Part.Rotation),Vector3.Transform(sb.Normal,b.Part.Rotation))>-.995f)continue;
    var ba=sa.Bone=="Root"?a.Root:a.Output;var bb=sb.Bone=="Root"?b.Root:b.Output;WeldBodies(ba,bb);Ignore(ba,bb);
   }
  }
  var gears=_pieces.Values.Where(p=>p.Def.ReferenceId is 38 or 39 or 51).ToArray();
  for(int i=0;i<gears.Length;i++)for(int j=i+1;j<gears.Length;j++)
  {
   var a=gears[i];var b=gears[j];var pa=_simulation.Bodies[a.Output].Pose;var pb=_simulation.Bodies[b.Output].Pose;
   var axisA=Vector3.Transform(a.Def.Axis,a.Part.Rotation);var axisB=Vector3.Transform(b.Def.Axis,b.Part.Rotation);var delta=pb.Position-pa.Position;
   var ba=a.Def.MovingBounds!;var bb=b.Def.MovingBounds!;float ra=(ba.Max-ba.Min).Z*.5f,rb=(bb.Max-bb.Min).Z*.5f;
   if(Math.Abs(Vector3.Dot(axisA,axisB))<.98f||Math.Abs(Vector3.Dot(delta,axisA))>.12f||Math.Abs(delta.Length()-ra-rb)>.15f)continue;
   _simulation.Solver.Add(a.Output,b.Output,new AngularAxisGearMotor{LocalAxisA=Vector3.Transform(axisA,Quaternion.Inverse(pa.Orientation)),VelocityScale=-ra/rb,Settings=new(200,.001f)});Ignore(a.Output,b.Output);
  }

 }
 public void AddObstacle(Vector3 centre,Vector3 size)=>_simulation.Statics.Add(new StaticDescription(centre,_simulation.Shapes.Add(new Box(size.X,size.Y,size.Z))));
 private BodyHandle BoxBody(Vector3 pos,Quaternion q,Vector3 size,float mass){size=Vector3.Max(size,new(.04f));var shape=new Box(size.X,size.Y,size.Z);return Body(pos,q,shape.ComputeInertia(mass),_simulation.Shapes.Add(shape));}
 private BodyHandle Body(Vector3 pos,Quaternion q,BodyInertia inertia,TypedIndex shape)=>_simulation.Bodies.Add(BodyDescription.CreateDynamic(new RigidPose(pos,q),inertia,shape,.01f));
 private ConstraintHandle WeldBodies(BodyHandle a,BodyHandle b){var pa=_simulation.Bodies[a].Pose;var pb=_simulation.Bodies[b].Pose;return _simulation.Solver.Add(a,b,new Weld{LocalOffset=Vector3.Transform(pb.Position-pa.Position,Quaternion.Inverse(pa.Orientation)),LocalOrientation=Quaternion.Normalize(pb.Orientation*Quaternion.Inverse(pa.Orientation)),SpringSettings=new(30,1)});}
 private void Ignore(BodyHandle a,BodyHandle b){if(a!=b)_connectedPairs[PairKey(a,b)]=1;}
 private static Quaternion Align(Vector3 from,Vector3 to){float dot=Vector3.Dot(from,to);return dot>.999f?Quaternion.Identity:dot<-.999f?Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI):Quaternion.Normalize(new Quaternion(Vector3.Cross(from,to),1+dot));}
 public (Vector3 Position,Quaternion Rotation) Pose(int id){var p=_pieces[id];var b=_simulation.Bodies[p.Root].Pose;return(b.Position-Vector3.Transform(p.RootCenter,b.Orientation),b.Orientation);}
 public bool HasPhysicalOutput(int id)=>_pieces[id].Root!=_pieces[id].Output;
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
  foreach(var key in _wheelFriction.Keys.ToArray())_wheelFriction[key]=drift?.18f:1.5f;
  foreach(var p in _pieces.Values)
  {
   if(p.Motor is {} motor)
   {
    float speed=p.Def.ReferenceId is 2 or 46?(brake?0:(throttle-steering*Math.Sign(p.Part.Position.X)*.65f)*12):p.Def.ReferenceId is 13 or 28 or 79 or 95?steering*2:p.Def.ReferenceId is 27 or 77?(_active?2:-2):p.Def.ReferenceId==14?throttle*30:powered?25:0;
    if(_health[p.Part.Id]<=0)speed=0;
    if(p.Def.ReferenceId is 2 or 46)speed*=Vector3.Dot(Vector3.Transform(p.Def.Axis,p.Part.Rotation),Vector3.UnitX)<-.1f?-1:1;
    _simulation.Solver.ApplyDescription(motor,new AngularAxisMotor{LocalAxisA=p.LocalAxis,TargetVelocity=speed,Settings=new(brake?500:120,.001f)});
   }
   if(p.Steer is {} servo)
   {
    float angle=p.Def.ReferenceId is 27 or 77?(_active?.7f:0):steering*.6f;
    _simulation.Solver.ApplyDescription(servo,new AngularServo{TargetRelativeRotationLocalA=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(p.LocalAxis,angle)*p.RestRelative),SpringSettings=new(12,1),ServoSettings=new(3,0,300)});
   }
   if(p.Linear is {} linear)
   {_simulation.Solver.GetDescription(linear,out LinearAxisServo d);d.TargetOffset=_active?(p.Def.ReferenceId is 9 or 16?-.3f:.4f):0;_simulation.Solver.ApplyDescription(linear,d);}
   var body=_simulation.Bodies[p.Root];var velocity=body.Velocity;float h=Math.Min(dt,.05f);
   if(p.Def.ReferenceId==43)velocity.Linear.Y+=95*h*body.LocalInertia.InverseMass;
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
  _accumulator=Math.Min(_accumulator+dt,.1f);while(_accumulator>=1f/60){_simulation.Timestep(1f/60);_accumulator-=1f/60;}
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
 }
 private void Damage(int id,float damage)
 {
  if(id==0||damage<=0)return;_health[id]-=damage;
  if(_health[id]<=0&&_attachments.Remove(id,out var joint))_simulation.Solver.Remove(joint);
 }
 public void Dispose(){_simulation.Dispose();_pool.Clear();}
    private static ulong PairKey(BodyHandle a, BodyHandle b)
    {
        uint low = (uint)Math.Min(a.Value, b.Value);
        uint high = (uint)Math.Max(a.Value, b.Value);
        return ((ulong)low << 32) | high;
    }

    private struct Contacts : INarrowPhaseCallbacks
    {
        private readonly Dictionary<ulong, int> _connectedPairs;private readonly Dictionary<int,float> _friction;
        public Contacts(Dictionary<ulong, int> connectedPairs,Dictionary<int,float> friction){_connectedPairs=connectedPairs;_friction=friction;}
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
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair,
            ref TManifold manifold, out PairMaterialProperties material)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            material = new PairMaterialProperties
            {
                FrictionCoefficient = Math.Min(pair.A.Mobility==CollidableMobility.Static?1.5f:_friction.GetValueOrDefault(pair.A.BodyHandle.Value,1.5f),pair.B.Mobility==CollidableMobility.Static?1.5f:_friction.GetValueOrDefault(pair.B.BodyHandle.Value,1.5f)),
                MaximumRecoveryVelocity = 2,
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
