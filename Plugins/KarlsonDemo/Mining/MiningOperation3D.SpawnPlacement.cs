using System.Numerics;
using System.ComponentModel;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Gameplay;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningOperation3D
{
 [Category("Enemies")]public float SpawnSpacing{get;set;}=1.8f;
 bool TryChooseSpacedSpawn(MiningSpawnPoint3D[] points,out Vector3 position)
 {
  position=default;if(points.Length==0)return false;
  for(int attempt=0;attempt<32;attempt++){
   int index=_pointCursor++;var p=points[index%points.Length].Transform.WorldPosition;
   float angle=index*2.399963f;float radius=attempt==0?0:1.5f+.5f*(attempt%5);p+=new Vector3(MathF.Sin(angle),0,MathF.Cos(angle))*radius;
   if(Player is {} player&&Vector3.DistanceSquared(p,player.Transform.WorldPosition)<64)continue;
   if(GameplayQuery3D.Raycast(GameObject.Scene!,p+Vector3.UnitY*4,-Vector3.UnitY,out var floor,8,includeTriggers:false)){
    if(floor.Normal.Y<.65f||floor.Point.Y>p.Y+.8f)continue;p.Y=floor.Point.Y+.04f;
   }
   bool crowded=false;foreach(var e in SpawnedEnemies)if(e.Scene==GameObject.Scene&&e.ActiveInHierarchy&&e.GetComponent<HealthComponent>() is {IsDead:false}){var d=e.Transform.WorldPosition-p;d.Y=0;if(d.LengthSquared()<MathF.Pow(Math.Clamp(SpawnSpacing,1,4),2)){crowded=true;break;}}
   if(crowded||GameplayQuery3D.OverlapSphere(GameObject.Scene!,p+Vector3.UnitY*.7f,.4f,includeTriggers:false).Any())continue;
   position=p;return true;
  }
  return false;
 }
}
