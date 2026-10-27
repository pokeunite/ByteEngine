using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
/// <summary>A delayed, bounded reaction to a nearby firing lane. No invulnerability, teleport or automatic evasion of every shot.</summary>
public sealed class MiningEnemyEvasion3D:Component
{
 [SceneObjectReference] public Guid PlayerObjectId{get;set;}
 public float Chance{get;set;}=.3f;
 public float Cooldown{get;set;}=4.5f;
 public float ReactionSeconds{get;set;}=.18f;
 public float ThreatRange{get;set;}=24;
 public float LaneWidth{get;set;}=1.6f;
 public int DodgeRequests{get;private set;}
 float clock,pending=-1;WeaponInstance3D? observed;int shots;Random? random;
 protected override void OnUpdate()=>Tick((float)Time.DeltaTime);
 public void Tick(float delta)
 {
  if(!float.IsFinite(delta)||delta<=0||GameObject.Scene==null||GameObject.GetComponent<HealthComponent>()?.IsDead==true)return;
  clock=Math.Max(0,clock-delta);var combat=GameObject.GetComponent<SimpleEnemyAI3D>();if(combat==null)return;
  if(pending>=0){pending-=delta;if(pending<=0){pending=-1;combat.RequestDodge();DodgeRequests++;}return;}
  var gun=GameObject.Scene.FindGameObject(PlayerObjectId)?.GetComponent<FpsGunLab3D>();var weapon=gun?.SelectedWeapon;
  if(weapon!=observed){observed=weapon;shots=weapon?.ShotsFired??0;return;}
  if(weapon==null||weapon.ShotsFired==shots)return;shots=weapon.ShotsFired;
  if(clock>0||combat.AttackPhase!="Ready"||!weapon.TryGetMuzzleWorldPosition(out var origin))return;
  var target=gun!.GetThirdPersonAimTarget(out _);if(!Threatens(origin,target-origin,Transform.WorldPosition+Vector3.UnitY*.8f,ThreatRange,LaneWidth))return;
  var to=Transform.WorldPosition+Vector3.UnitY*.8f-origin;float distance=to.Length();
  if(distance>0&&GameplayQuery3D.Raycast(GameObject.Scene,origin,to/distance,out var hit,distance,GameObject.Scene.FindGameObject(PlayerObjectId),includeTriggers:false)&&hit.GameObject!=GameObject&&hit.GameObject.GetComponent<HealthComponent>()==null)return;
  random??=new Random(GameObject.Id.GetHashCode());if(random.NextDouble()>=Math.Clamp(Chance,0,1))return;
  pending=Math.Clamp(ReactionSeconds,.1f,.5f);clock=Math.Max(2,Cooldown);
 }
 public static bool Threatens(Vector3 origin,Vector3 direction,Vector3 point,float range,float lane)
 {
  if(!float.IsFinite(direction.LengthSquared())||direction.LengthSquared()<.001f)return false;direction=Vector3.Normalize(direction);var delta=point-origin;float along=Vector3.Dot(delta,direction);
  return along>1&&along<Math.Clamp(range,1,60)&&(delta-direction*along).LengthSquared()<Math.Clamp(lane,.1f,3)*Math.Clamp(lane,.1f,3);
 }
}
