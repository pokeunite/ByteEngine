using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Physics;

namespace KarlsonDemo.Plugin;
internal sealed class WeaponFlightUpgrade3D:Component
{
 public GameObject? Target;public float TurnDegrees=180;float age;
 protected override void OnUpdate()
 {
  age+=(float)Time.DeltaTime;
  if(age>1.5f||Target?.Scene!=GameObject.Scene||Target?.GetComponent<HealthComponent>() is not {IsDead:false}||GameObject.GetComponent<Projectile3D>() is not {} flight||flight.Velocity.LengthSquared()<.001f)return;
  var delta=Target.Transform.WorldPosition+Vector3.UnitY*.55f-Transform.WorldPosition;
  if(delta.LengthSquared()<.01f)return;
  if(GameplayQuery3D.Raycast(GameObject.Scene!,Transform.WorldPosition,delta,out var hit,delta.Length(),GameObject,includeTriggers:false)&&hit.GameObject!=Target&&!hit.GameObject.IsDescendantOf(Target))return;
  float speed=flight.Velocity.Length();var from=flight.Velocity/speed;var to=Vector3.Normalize(delta);float angle=MathF.Acos(Math.Clamp(Vector3.Dot(from,to),-1,1));
  float fraction=angle<.0001f?1:Math.Min(1,Math.Max(0,TurnDegrees)*MathF.PI/180*(float)Time.DeltaTime/angle);
  var direction=Vector3.Lerp(from,to,fraction);if(direction.LengthSquared()>.001f)flight.Velocity=Vector3.Normalize(direction)*speed;
 }
}
