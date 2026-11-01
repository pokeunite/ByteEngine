using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Gameplay;
/// <summary>Actual health lost by one impact, captured before the target can be removed.</summary>
public readonly record struct CombatDamageReceipt(Guid TargetId,Vector3 Position,float Amount,bool Killed,bool Enemy=false)
{
 public static CombatDamageReceipt? Apply(GameObject hit,float damage,Vector3 direction)
 {
  var region=hit.GetComponent<DamageRegion3D>();GameObject? owner=null;
  if(region!=null)owner=hit.Scene?.FindGameObject(region.HealthOwnerId)??hit.Parent;
  else for(var candidate=hit;candidate!=null;candidate=candidate.Parent)if(candidate.GetComponent<HealthComponent>()!=null){owner=candidate;break;}
  if(owner?.GetComponent<HealthComponent>() is not {} health)return null;
  var id=owner.Id;var position=owner.Transform.WorldPosition;bool enemy=owner.GetComponent<SimpleEnemyAI3D>()!=null;float before=health.CurrentHealth;
  if(region!=null)region.ApplyDamage(damage,direction);else health.Damage(damage,direction);
  float actual=before-health.CurrentHealth;
  return actual>0?new(id,position,actual,before>0&&health.IsDead,enemy):null;
 }
}
