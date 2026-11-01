using System.Numerics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Characters;
namespace KarlsonDemo.Plugin;
public sealed partial class WeaponInstance3D
{
 void ApplyEnemyKnockback(CombatDamageReceipt receipt,Vector3 direction,WeaponDefinition3D d)
 {
  if(!receipt.Enemy||receipt.Killed||d.EnemyKnockbackSeconds<=0||GameObject.Scene?.FindGameObject(receipt.TargetId) is not {} target)return;
  direction.Y=0;if(direction.LengthSquared()<.0001f)return;float seconds=Math.Clamp(d.EnemyKnockbackSeconds,0,.3f);
  target.GetComponent<CharacterController3D>()?.BeginBurstMotion(Vector3.Normalize(direction)*Math.Clamp(d.HitImpulse,0,15),seconds);
  target.GetComponent<SimpleEnemyAI3D>()?.Stagger(seconds);
 }
}
