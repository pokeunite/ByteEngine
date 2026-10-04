using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
namespace GoblinScrapper.Construction;
public sealed record GoblinChunk(string Name,Vector3 Centre,Vector3 Size,Vector3 Pivot,Quaternion Rotation);
public sealed record MachineStrike(int Part,Vector3 Point,Vector3 Velocity,bool Sharp);
public sealed partial class ContraptionPhysicsWorld
{
 private readonly Dictionary<int,(BodyHandle[] Bodies,ConstraintHandle[] Joints)> _corpses=new();
 private readonly Dictionary<(float,float,float),TypedIndex> _corpseShapes=new();
 public MachineStrike? StrikeGoblin(Vector3 feet,float dt)
 {
  MachineStrike? best=null;float bestSpeed=1.8f;var centre=feet+new Vector3(0,.58f,0);
  foreach(var p in _pieces.Values){
   var pose=Pose(p.Part.Id);var moving=p.Def.MovingBounds;var matrix=Matrix4x4.CreateFromQuaternion(pose.Rotation)*Matrix4x4.CreateTranslation(pose.Position);
   if(moving!=null)matrix=OutputDeformation(p.Part.Id)*matrix;
   var box=(moving??p.Def.Bounds);Matrix4x4.Invert(matrix,out var inverse);var local=Vector3.Transform(centre,inverse);
   var closest=Vector3.Clamp(local,box.Min,box.Max);var point=Vector3.Transform(closest,matrix);
   var body=_simulation.Bodies[p.Output];var velocity=body.Velocity.Linear+Vector3.Cross(body.Velocity.Angular,point-body.Pose.Position);
   float speed=velocity.Length();bool sharp=p.Def.Kind is "blade" or "drill" or "saw"||p.Def.ReferenceId is 20 or 22 or 17;
   if(Vector3.DistanceSquared(point,centre)>.55f*.55f){
    // Swept centre test catches fast straight passes between frames.
    var previous=point-velocity*Math.Max(0,dt);var line=point-previous;var t=line.LengthSquared()<1e-6f?0:Math.Clamp(Vector3.Dot(centre-previous,line)/line.LengthSquared(),0,1);
    if(Vector3.DistanceSquared(previous+line*t,centre)>.45f*.45f)continue;
   }
   if(speed>bestSpeed){bestSpeed=speed;best=new(p.Part.Id,point,velocity,sharp);}
  }return best;
 }
 public MachineStrike? ProjectileStrikeGoblin(Vector3 feet,float dt)
 {
  var centre=feet+new Vector3(0,.85f,0);
  foreach(var shot in _projectiles.ToArray()){
   var body=_simulation.Bodies[shot.Value.Body];var velocity=body.Velocity.Linear;var end=body.Pose.Position;var start=end-velocity*Math.Max(0,dt);var delta=end-start;float t=delta.LengthSquared()<1e-6f?0:Math.Clamp(Vector3.Dot(centre-start,delta)/delta.LengthSquared(),0,1);
   if(Vector3.DistanceSquared(start+delta*t,centre)>.42f*.42f)continue;
   _simulation.Bodies.Remove(shot.Value.Body);_projectiles.Remove(shot.Key);return new(-1,end,velocity,true);
  }return null;
 }
 public void SpawnGoblinRagdoll(int id,GoblinChunk[] chunks,Vector3 velocity,int sever=-1)
 {
  RemoveGoblinRagdoll(id);var bodies=new BodyHandle[chunks.Length];var joints=new List<ConstraintHandle>();
  for(int i=0;i<chunks.Length;i++){
   var c=chunks[i];var size=Vector3.Max(c.Size,new Vector3(.08f));var key=(size.X,size.Y,size.Z);
   if(!_corpseShapes.TryGetValue(key,out var shapeIndex)){shapeIndex=_simulation.Shapes.Add(new Box(size.X,size.Y,size.Z));_corpseShapes[key]=shapeIndex;}
   var shape=new Box(size.X,size.Y,size.Z);float mass=i==0?12:i==1?3:4;
   bodies[i]=Body(c.Centre,c.Rotation,shape.ComputeInertia(mass),shapeIndex);
   var body=_simulation.Bodies[bodies[i]];body.Velocity=new BodyVelocity(Vector3.Clamp(velocity*.7f+Vector3.UnitY*2,new(-18),new(18)),new Vector3((i%2==0?1:-1)*3,2,1));
   for(int j=0;j<i;j++)Ignore(bodies[i],bodies[j]);
   if(i>0&&i!=sever){var a=_simulation.Bodies[bodies[0]].Pose;var b=body.Pose;var pivot=c.Pivot;
    joints.Add(_simulation.Solver.Add(bodies[0],bodies[i],new BallSocket{LocalOffsetA=Vector3.Transform(pivot-a.Position,Quaternion.Inverse(a.Orientation)),LocalOffsetB=Vector3.Transform(pivot-b.Position,Quaternion.Inverse(b.Orientation)),SpringSettings=new(25,1)}));
   }
  }_corpses[id]=(bodies,joints.ToArray());
 }
 public (Vector3 Position,Quaternion Rotation) GoblinChunkPose(int id,int chunk){var b=_simulation.Bodies[_corpses[id].Bodies[chunk]].Pose;return(b.Position,b.Orientation);}
 public void RemoveGoblinRagdoll(int id){if(!_corpses.Remove(id,out var rag))return;foreach(var joint in rag.Joints)_simulation.Solver.Remove(joint);foreach(var body in rag.Bodies){foreach(var key in _connectedPairs.Keys.Where(k=>(uint)k==(uint)body.Value||(uint)(k>>32)==(uint)body.Value).ToArray())_connectedPairs.Remove(key);_simulation.Bodies.Remove(body);}}
}
