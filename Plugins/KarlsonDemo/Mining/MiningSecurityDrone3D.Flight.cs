using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Gameplay;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningSecurityDrone3D
{
 [Category("Flight navigation")] public float FlightClearance{get;set;}=.6f;
 [Category("Flight navigation")] public float MaximumFlightHeight{get;set;}=12;
 [Category("Final attack")] public float SelfDestructBatteryFraction{get;set;}=.1f;
 [Category("Final attack")] public float DiveSpeed{get;set;}=13;
 [Category("Tactics")] public float PursuitRadius{get;set;}=65;
 readonly List<Vector3> flightPath=[];float pathClock;bool diving;
 bool FlightClear(Vector3 a,Vector3 b){var d=b-a;return d.LengthSquared()<.001f||!GameplayQuery3D.SphereCast(GameObject.Scene!,a,Vector3.Normalize(d),Safe(FlightClearance,.6f,.2f,1.5f),out _,d.Length(),GameObject,includeTriggers:false);}
 void MoveTo(Vector3 desired,float delta)
 {
  pathClock-=delta;var position=Transform.WorldPosition;
  if(FlightClear(position,desired)){flightPath.Clear();StepFlight(desired,delta);return;}
  if(flightPath.Count==0&&pathClock<=0){PlanFlight(desired);pathClock=.75f;}
  if(flightPath.Count>0){if(Vector3.DistanceSquared(position,flightPath[0])<.5f)flightPath.RemoveAt(0);if(flightPath.Count>0){if(FlightClear(position,flightPath[0]))StepFlight(flightPath[0],delta);else flightPath.Clear();}return;}
  // Recover vertically if a moving obstacle blocked an old route. No sideways jitter loop.
  var rise=position+Vector3.UnitY*2;if(rise.Y<=patrolOrigin.Y+MaximumFlightHeight&&FlightClear(position,rise))StepFlight(rise,delta);
 }
 void StepFlight(Vector3 point,float delta){var d=point-Transform.WorldPosition;float distance=d.Length();if(distance>.02f)Transform.WorldPosition+=d/distance*Math.Min(distance,Safe(diving?DiveSpeed:FollowSpeed,7,.1f,25)*delta);}
 void PlanFlight(Vector3 desired)
 {
  flightPath.Clear();Vector3 origin=Transform.WorldPosition;const float cell=2;var toward=desired-origin;if(toward.Length()>24)desired=origin+Vector3.Normalize(toward)*24;
  var start=(0,0,0);var open=new PriorityQueue<(int,int,int),float>();var cost=new Dictionary<(int,int,int),float>{{start,0}};var parent=new Dictionary<(int,int,int),(int,int,int)>();open.Enqueue(start,0);var found=start;bool success=false;int budget=1800;
  Vector3 Point((int x,int y,int z) n)=>origin+new Vector3(n.x,n.y,n.z)*cell;
  var directions=new (int,int,int)[]{(1,0,0),(-1,0,0),(0,0,1),(0,0,-1),(1,0,1),(-1,0,1),(1,0,-1),(-1,0,-1),(0,1,0),(0,-1,0)};
  while(open.Count>0&&budget-->0){var n=open.Dequeue();var here=Point(n);if(Vector3.DistanceSquared(here,desired)<5&&FlightClear(here,desired)){found=n;success=true;break;}
   foreach(var d in directions){var next=(n.Item1+d.Item1,n.Item2+d.Item2,n.Item3+d.Item3);var p=Point(next);if(Math.Abs(next.Item1)>15||Math.Abs(next.Item3)>15||p.Y<patrolOrigin.Y+.6f||p.Y>patrolOrigin.Y+Safe(MaximumFlightHeight,12,3,25)||!FlightClear(here,p))continue;float candidate=cost[n]+Vector3.Distance(here,p);if(cost.TryGetValue(next,out float old)&&old<=candidate)continue;cost[next]=candidate;parent[next]=n;open.Enqueue(next,candidate+Vector3.Distance(p,desired));}}
  if(!success)return;flightPath.Add(desired);for(var n=found;n!=start;n=parent[n])flightPath.Add(Point(n));flightPath.Reverse();
 }
 void DiveAtNearest(MiningOperation3D op,float delta)
 {
  diving=true;TacticalState="Final dive";target=null;float best=float.MaxValue;
  foreach(var enemy in op.SpawnedEnemies)if(enemy.Scene==GameObject.Scene&&enemy.ActiveInHierarchy&&enemy.GetComponent<HealthComponent>() is {IsDead:false}){float d=Vector3.DistanceSquared(enemy.Transform.WorldPosition,Transform.WorldPosition);if(d<best){best=d;target=enemy;}}
  if(target==null){Patrol(op,delta);return;}
  var point=target.Transform.WorldPosition+Vector3.UnitY*.7f;
  var ray=point-Transform.WorldPosition;bool visible=!GameplayQuery3D.Raycast(GameObject.Scene!,Transform.WorldPosition,ray,out var hit,ray.Length(),GameObject,includeTriggers:false)||hit.GameObject==target||hit.GameObject.IsDescendantOf(target);
  if(ray.LengthSquared()<=MathF.Pow(Math.Max(1.4f,ExplosionRadius*.85f),2)&&visible){Detonate();return;}
  var away=Transform.WorldPosition-point;away.Y=0;if(away.LengthSquared()<.01f)away=Vector3.UnitX;MoveTo(point+Vector3.Normalize(away)*Math.Max(1.2f,ExplosionRadius*.6f),delta);
 }
}
