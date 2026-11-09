using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
/// <summary>Both spider roles breach a local barricade obstructing their approach, then resume their role.</summary>
public sealed class MiningBarricadeBreaker3D:Component
{
 [SceneObjectReference]public Guid NormalTargetId{get;set;}
 public float LookAhead{get;set;}=8;
 public float MeleeReach{get;set;}=1.35f;
 public float ReconsiderSeconds{get;set;}=.2f;
 [Browsable(false)]public Guid BlockingBarricadeId{get;private set;}
 public override int UpdateOrder=>-1050;
 GameObject? contact,blocking;Guid resumeTarget;float clock;
 protected override void OnUpdate()=>Tick((float)Time.DeltaTime);
 public void Tick(float dt)
 {
  if(GameObject.Scene is not {} scene||GameObject.GetComponent<AIBehavior3D>() is not {} brain||GameObject.GetComponent<SimpleEnemyAI3D>() is not {} combat)return;
  clock-=dt;if(clock>0||combat.AttackPhase!="Ready")return;clock=Math.Max(.05f,ReconsiderSeconds);
  if(contact==null||brain.TargetObjectId!=contact.Id)resumeTarget=brain.TargetObjectId==Guid.Empty?NormalTargetId:brain.TargetObjectId;
  var goal=scene.FindGameObject(resumeTarget)??scene.FindGameObject(NormalTargetId);if(goal==null)return;
  var from=Transform.WorldPosition;var delta=goal.Transform.WorldPosition-from;delta.Y=0;float distance=delta.Length();if(distance<.1f)return;
  var direction=delta/distance;float nearest=Math.Min(Math.Max(1,LookAhead),distance);GameObject? candidate=null;
  foreach(var shop in scene.GetComponentsSnapshot<MiningResearchStation3D>())foreach(var barrier in shop.DeployedBarricades){
   if(barrier.Scene!=scene||!barrier.ActiveInHierarchy||barrier.GetComponent<HealthComponent>() is not {IsDead:false})continue;
   var box=MiningCampGeometry.Transform(MiningCampGeometry.LocalBounds(barrier),barrier.Transform.WorldMatrix);
   if(IntersectsApproach(from,direction,box.Minimum,box.Maximum,nearest,out float entry)){nearest=entry;candidate=barrier;}
  }
  blocking=candidate;BlockingBarricadeId=candidate?.Id??Guid.Empty;
  if(candidate==null){if(contact!=null&&brain.TargetObjectId==contact.Id){brain.TargetObjectId=resumeTarget;brain.AttackReservationTargetId=resumeTarget;combat.TargetId=resumeTarget;}return;}
  if(contact?.Scene!=scene){contact=scene.CreateGameObject("Spider barricade contact");var hp=contact.AddComponent(new HealthComponent{MaxHealth=1000000,CurrentHealth=1000000});hp.Damaged+=(_,amount)=>{if(blocking?.Scene==scene&&MiningCampGeometry.SurfaceDistance(blocking,Transform.WorldPosition)<=MeleeReach+.05f)blocking.GetComponent<HealthComponent>()?.Damage(amount);hp.CurrentHealth=hp.MaxHealth;};}
  var bounds=MiningCampGeometry.Transform(MiningCampGeometry.LocalBounds(candidate),candidate.Transform.WorldMatrix);var point=new Vector3(Math.Clamp(from.X,bounds.Minimum.X,bounds.Maximum.X),from.Y,Math.Clamp(from.Z,bounds.Minimum.Z,bounds.Maximum.Z));
  var away=from-point;away.Y=0;contact.Transform.WorldPosition=point+(away.LengthSquared()>.0001f?Vector3.Normalize(away)*.08f:Vector3.Zero);brain.TargetObjectId=contact.Id;brain.AttackReservationTargetId=candidate.Id;combat.TargetId=contact.Id;combat.AttackRange=Math.Max(.3f,MeleeReach);combat.StopDistance=Math.Min(combat.StopDistance,combat.AttackRange*.8f);
 }
 static bool IntersectsApproach(Vector3 origin,Vector3 direction,Vector3 min,Vector3 max,float limit,out float entry){float lo=0,hi=limit;bool Slab(float o,float d,float low,float high){low-=.4f;high+=.4f;if(Math.Abs(d)<.0001f)return o>=low&&o<=high;float a=(low-o)/d,b=(high-o)/d;if(a>b)(a,b)=(b,a);lo=Math.Max(lo,a);hi=Math.Min(hi,b);return lo<=hi;}bool hit=Slab(origin.X,direction.X,min.X,max.X)&&Slab(origin.Z,direction.Z,min.Z,max.Z);entry=lo;return hit&&lo<=limit;}
 protected override void OnDestroy(){if(contact?.Scene is {} scene)scene.DestroyGameObject(contact);}
}
