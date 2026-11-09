using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningSecurityDrone3D
{
 [Category("Patrol")] public float PatrolHalfWidth{get;set;}=16;
 [Category("Patrol")] public float PatrolLength{get;set;}=75;
 [Category("Patrol")] public float PatrolNearDistance{get;set;}=8;
 [Category("Tactics")] public float CrowdRadius{get;set;}=6;
 [Category("Tactics")] public int MinimumCrowdSize{get;set;}=3;
 [Category("Tactics")] public float PlayerThreatRadius{get;set;}=18;
 [Category("Camera clearance")] public float MinimumPlayerDistance{get;set;}=6;
 [Category("Camera clearance")] public float ViewCorridorWidth{get;set;}=3;
 [Browsable(false)] public Guid CurrentTargetObjectId=>target?.Id??Guid.Empty;
 [Browsable(false)] public string TacticalState{get;private set;}="Patrol";
 [Browsable(false)] public bool HiddenForCamera{get;private set;}
 readonly List<GameObject> tacticalEnemies=[];
 int patrolIndex;float patrolClock;
 Vector3 patrolOrigin,patrolForward=Vector3.UnitZ;bool patrolInitialized;
 void InitializePatrol(MiningOperation3D op)
 {
  if(patrolInitialized)return;patrolInitialized=true;
  patrolOrigin=GameObject.Scene?.FindGameObject(op.DrillObjectId)?.Transform.WorldPosition??Transform.WorldPosition;
  var points=GameObject.Scene!.GetComponentsSnapshot<MiningSpawnPoint3D>();Vector3 sum=default;int count=0;
  foreach(var p in points)if(p.OperationObjectId==op.GameObject.Id&&p.Available){sum+=p.Transform.WorldPosition;count++;}
  if(count>0){var direction=sum/count-patrolOrigin;direction.Y=0;if(direction.LengthSquared()>1)patrolForward=Vector3.Normalize(direction);}
 }
 static float GroundDistanceSquared(Vector3 a,Vector3 b){var d=a-b;return d.X*d.X+d.Z*d.Z;}
 void ChooseBattlefieldTarget(MiningOperation3D op)
 {
  InitializePatrol(op);target=null;tacticalEnemies.Clear();
  float patrolReach=Math.Max(Safe(PatrolLength,50,10,150),Safe(ThreatSearchRadius,28,1,100))+Safe(MaximumLeashDistance,32,5,100);
  foreach(var e in op.SpawnedEnemies)
   if(e.Scene==GameObject.Scene&&e.ActiveInHierarchy&&e.GetComponent<HealthComponent>() is {IsDead:false}&&GroundDistanceSquared(e.Transform.WorldPosition,patrolOrigin)<=patrolReach*patrolReach)tacticalEnemies.Add(e);
  int bestCount=0;float bestDistance=float.MaxValue,radius=Safe(CrowdRadius,6,1,20);int minimum=Math.Clamp(MinimumCrowdSize,2,20);
  foreach(var e in tacticalEnemies){int count=0;foreach(var other in tacticalEnemies)if(GroundDistanceSquared(e.Transform.WorldPosition,other.Transform.WorldPosition)<=radius*radius)count++;
   float distance=GroundDistanceSquared(e.Transform.WorldPosition,Transform.WorldPosition);
   if(count>=minimum&&(count>bestCount||count==bestCount&&distance<bestDistance)){bestCount=count;bestDistance=distance;target=e;}}
  if(target!=null){TacticalState="Attack crowd";return;}
  var player=GameObject.Scene!.FindGameObject(op.PlayerObjectId);bestDistance=MathF.Pow(Safe(PlayerThreatRadius,18,2,60),2);
  if(player!=null)foreach(var e in tacticalEnemies){float d=GroundDistanceSquared(e.Transform.WorldPosition,player.Transform.WorldPosition);if(d<bestDistance){bestDistance=d;target=e;}}
  if(target!=null){TacticalState="Protect player";return;}
  bestDistance=MathF.Pow(Safe(PursuitRadius,65,10,150),2);foreach(var e in tacticalEnemies){float d=GroundDistanceSquared(e.Transform.WorldPosition,Transform.WorldPosition);if(d<bestDistance){bestDistance=d;target=e;}}
  TacticalState=target==null?"Patrol":"Pursue enemies";
 }
 void Patrol(MiningOperation3D op,float delta)
 {
  InitializePatrol(op);TacticalState="Patrol";patrolClock+=delta;
  var side=new Vector3(patrolForward.Z,0,-patrolForward.X);float width=Safe(PatrolHalfWidth,16,3,50),length=Safe(PatrolLength,50,10,150),near=Safe(PatrolNearDistance,8,3,length-2);
  Vector2 corner=patrolIndex switch{0=>new(-.65f,near),1=>new(.65f,length*.55f),2=>new(-.65f,length),_=>new(.65f,near)};
  var point=patrolOrigin+side*corner.X*width+patrolForward*corner.Y+Vector3.UnitY*Safe(HoverHeight,1.7f,.5f,5);
  if(GroundDistanceSquared(point,Transform.WorldPosition)<2||patrolClock>18){patrolIndex=(patrolIndex+1)%4;patrolClock=0;}
  MoveTo(ClearPlayerView(op,point),delta);
 }
 Vector3 ClearPlayerView(MiningOperation3D op,Vector3 desired)
 {
  var player=GameObject.Scene!.FindGameObject(op.PlayerObjectId);if(player==null)return desired;
  var right=GameObject.Scene.ActiveCamera?.Transform.Right??Vector3.UnitX;right.Y=0;if(right.LengthSquared()<.01f)right=Vector3.UnitX;right=Vector3.Normalize(right);
  float clearance=Safe(MinimumPlayerDistance,6,3,15);var offset=desired-player.Transform.WorldPosition;offset.Y=0;
  if(offset.LengthSquared()<clearance*clearance){float sign=Vector3.Dot(Transform.WorldPosition-player.Transform.WorldPosition,right)>=0?1:-1;desired=player.Transform.WorldPosition+right*clearance*sign+Vector3.UnitY*Safe(HoverHeight,1.7f,.5f,5);}
  if(GameObject.Scene.ActiveCamera is {} camera){var f=camera.Transform.Forward;f.Y=0;if(f.LengthSquared()>.01f){f=Vector3.Normalize(f);var d=desired-camera.Transform.WorldPosition;float along=Vector3.Dot(d,f),across=Vector3.Dot(d,right),end=Vector3.Dot(player.Transform.WorldPosition-camera.Transform.WorldPosition,f)+14;float width=Safe(ViewCorridorWidth,3,1,8);if(along>0&&along<end&&Math.Abs(across)<width)desired+=right*((across>=0?1:-1)*width-across);}}
  return desired;
 }
 void UpdateCameraClearance(MiningOperation3D op)
 {
  HiddenForCamera=false;
  // Camera avoidance belongs to flight steering, never to visibility. A distant drone
  // crossing the centre of the screen must not disappear for the rest of its patrol.
  if(visualRoot!=null)visualRoot.Active=true;
 }
}
