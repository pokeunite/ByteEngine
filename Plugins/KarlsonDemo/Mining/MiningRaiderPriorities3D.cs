using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.AI;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
/// <summary>Local obstacle priority for drill attackers; hunters retain their player target.</summary>
public sealed class MiningRaiderPriorities3D:ByteEngine.Core.Scene.Component
{
 [SceneObjectReference] public Guid DrillObjectId{get;set;}
 public float DefenceNoticeRadius{get;set;}=6;
 public float MeleeReach{get;set;}=1.35f;
 public float ReconsiderSeconds{get;set;}=.25f;
 float clock;
 GameObject? drillContact;
 public bool TargetingDrill=>DrillObjectId!=Guid.Empty&&GameObject.GetComponent<AIBehavior3D>() is {} brain&&(brain.TargetObjectId==DrillObjectId||drillContact!=null&&brain.TargetObjectId==drillContact.Id);
 MiningDefensePlanner3D? planner;
 Collider3D[] drillColliders=[];
 public override int UpdateOrder=>-1100;
 protected override void OnUpdate()
 {
  if(GameObject.Scene is not {} scene||GameObject.GetComponent<SimpleEnemyAI3D>() is not {} combat||GameObject.GetComponent<AIBehavior3D>() is not {} brain)return;
  combat.AttackRange=Math.Max(.2f,MeleeReach);combat.StopDistance=Math.Min(combat.StopDistance,combat.AttackRange*.8f);
  clock-=(float)ByteEngine.Core.Time.DeltaTime;if(clock>0||combat.AttackPhase!="Ready")return;clock=Math.Max(.05f,ReconsiderSeconds);
  float radius=Math.Max(0,DefenceNoticeRadius);
  planner??=scene.GameObjects.Select(o=>o.GetComponent<MiningDefensePlanner3D>()).OfType<MiningDefensePlanner3D>().FirstOrDefault();
  var target=(planner?.LiveDefences??Enumerable.Empty<MiningDefense3D>()).Where(d=>d.CanBeAttacked).Select(d=>d.GameObject)
   .Where(o=>Vector3.DistanceSquared(o.Transform.WorldPosition,Transform.WorldPosition)<=radius*radius)
   .OrderBy(o=>Vector3.DistanceSquared(o.Transform.WorldPosition,Transform.WorldPosition)).FirstOrDefault();
  if(target==null&&scene.FindGameObject(DrillObjectId) is {} drill)
  {
   if(drillContact?.Scene!=scene)
   {
    drillContact=scene.CreateGameObject("Raider contact point on drill");
    drillColliders=scene.GameObjects.Where(o=>MiningCampGeometry.Under(o,drill)).SelectMany(o=>o.Components.OfType<Collider3D>()).ToArray();
    var health=drillContact.AddComponent(new HealthComponent{MaxHealth=1000000,CurrentHealth=1000000,DestroyOnDeath=false});
    health.Damaged+=(_,amount)=>{if(drill.Scene==scene&&MiningCampGeometry.SurfaceDistance(drill,Transform.WorldPosition)<=Math.Max(.2f,MeleeReach))drill.GetComponent<HealthComponent>()?.Damage(amount);health.CurrentHealth=health.MaxHealth;};
   }
   var point=ClosestSurface(drill,Transform.WorldPosition,drillColliders);var away=Transform.WorldPosition-point;away.Y=0;
   drillContact.Transform.WorldPosition=point+(away.LengthSquared()>.0001f?Vector3.Normalize(away)*.06f:Vector3.Zero);
   target=drillContact;
  }
  var id=target?.Id??DrillObjectId;brain.AttackReservationTargetId=target==drillContact?DrillObjectId:id;if(brain.TargetObjectId!=id){brain.TargetObjectId=id;combat.TargetId=id;}
 }
 static Vector3 ClosestSurface(GameObject root,Vector3 from,IReadOnlyList<Collider3D> colliders)
 {
  var best=root.Transform.WorldPosition;float distance=float.PositiveInfinity;
  foreach(var collider in colliders.Where(c=>c.Enabled&&!c.IsTrigger&&c.GameObject.ActiveInHierarchy))
  {var b=MiningCampGeometry.ColliderBounds(collider);if(b.Minimum.Y>from.Y+2.2f)continue;var p=new Vector3(Math.Clamp(from.X,b.Minimum.X,b.Maximum.X),from.Y,Math.Clamp(from.Z,b.Minimum.Z,b.Maximum.Z));float d=Vector3.DistanceSquared(from,p);if(d<distance){distance=d;best=p;}}
  return best;
 }
 protected override void OnDestroy(){if(drillContact?.Scene is {} s)s.DestroyGameObject(drillContact);drillContact=null;}
}
