using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace KarlsonDemo.Plugin;

public sealed partial class MiningDefense3D
{
 [Category("Projectiles")] public float ProjectileSpeed {get;set;}=75;
 [Category("Projectiles")] public float ProjectileRadius {get;set;}=.025f;
 [Category("Projectiles")] public float TracerLength {get;set;}=2;
 [Category("Projectiles")] public float TracerWidth {get;set;}=.035f;
 [Category("Projectiles")] public int MaximumProjectiles {get;set;}=32;
 [Browsable(false)] public int ActiveProjectiles=>bullets.Count(b=>b.Scene==GameObject.Scene);
 readonly List<GameObject> bullets=[];

 bool LaunchDefenceProjectile(Vector3 origin,Vector3 direction,float damage,int pierce,float range,Guid ignore)
 {
  var scene=GameObject.Scene;if(scene==null||range<=.01f)return false;
  bullets.RemoveAll(b=>b.Scene!=scene);
  if(bullets.Count>=Math.Clamp(MaximumProjectiles,1,128))return false;
  float speed=WeaponDefinition3D.Bound(ProjectileSpeed,75,1,500);
  var bullet=scene.CreateGameObject("Company defence projectile");bullets.Add(bullet);
  bullet.Transform.WorldPosition=origin;bullet.Transform.LocalScale=new(.055f);
  var mesh=bullet.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,CastShadows=false,Material=new(){Shading=MaterialShadingMode.Unlit,BaseColor=new(1,.95f,.7f,1)}});
  var flight=bullet.AddComponent(new Projectile3D{OwnerId=ignore,Velocity=direction*speed,Damage=0,Radius=WeaponDefinition3D.Bound(ProjectileRadius,.025f,.005f,.2f),DestroyOnHit=false});
  var trail=bullet.AddComponent(new BulletTrail3D{Length=TracerLength,Width=TracerWidth});
  var life=bullet.AddComponent(new LifetimeComponent{LifetimeSeconds=range/speed});
  flight.Hit+=hit=>{
   GameObject? victim=hit.GameObject;while(victim!=null&&Op?.SpawnedEnemies.Contains(victim)!=true)victim=victim.Parent;
   if(victim?.GetComponent<HealthComponent>() is {IsDead:false} health){
    health.Damage(damage,direction);
    float remaining=range-Vector3.Distance(origin,hit.Point)-.03f;
    if(pierce>0)LaunchDefenceProjectile(hit.Point+direction*.03f,direction,damage,pierce-1,remaining,victim.Id);
   }
   trail.Finish(direction,.07f);mesh.Enabled=false;life.Restart(.07f);
  };
  return true;
 }
 protected override void OnStop(){foreach(var bullet in bullets.ToArray())if(bullet.Scene is {} scene)scene.DestroyGameObject(bullet);bullets.Clear();}
}
