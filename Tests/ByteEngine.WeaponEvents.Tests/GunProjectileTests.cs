using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Variables;
using KarlsonDemo.Plugin;
static class GunProjectileTests
{
 public static void Run()
 {
  void Check(bool c,string m){if(!c)throw new Exception(m);}
  var step=typeof(Projectile3D).GetMethod("Simulate",BindingFlags.NonPublic|BindingFlags.Instance)!;
  void Advance(Projectile3D p,float dt)=>step.Invoke(p,[dt]);
  var scene=new Scene("Gun projectiles");var owner=scene.CreateGameObject("Player");owner.AddComponent(new CapsuleCollider3D{Radius=.3f,Height=1.8f,Center=new(0,.9f,0)});
  var gun=scene.CreateGameObject("Gun");var def=gun.AddComponent(new WeaponDefinition3D{ShotMode=WeaponShotMode.Projectile,ProjectileSpeed=80,ProjectileRadius=.02f,MaxActiveProjectiles=64,MuzzleSource=WeaponMuzzleSource.LocalOffset,MuzzleLocalOffset=new(0,.8f,0),MagazineCapacity=100,ReserveCapacity=100,Range=80,EquipSeconds=0,ShowShotTrace=true,MuzzleFlashEnabled=false});var weapon=gun.AddComponent(new WeaponInstance3D{CurrentAmmo=100});weapon.Equip(true);
  Projectile3D Fire()
  {typeof(WeaponInstance3D).GetMethod("Tick",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(weapon,[1f]);Check(weapon.Fire(new(0,.8f,0),-Vector3.UnitZ,owner),"Projectile shot must be accepted");return scene.GameObjects.Select(o=>o.GetComponent<Projectile3D>()).Last(p=>p!=null)!;}
  var enemy=scene.CreateGameObject("Enemy");enemy.Transform.WorldPosition=new(0,0,-6);enemy.AddComponent(new CapsuleCollider3D{Radius=.35f,Height=1.8f,Center=new(0,.9f,0)});var hp=enemy.AddComponent(new HealthComponent());
  var p=Fire();Check(hp.CurrentHealth==100&&!weapon.LastShotWasHitscan,"Firing must not apply hitscan damage");var trail=p.GameObject.GetComponent<BulletTrail3D>()!;typeof(Component).GetMethod("StartInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(trail,null);
  Advance(p,.02f);Check(hp.CurrentHealth==100,"Bullet flight must delay damage and ignore its owner's collider");Advance(p,.1f);Check(hp.CurrentHealth==75&&weapon.HitCount==1&&trail.Finished,"Projectile impact must apply one hit and freeze its trail");Advance(p,.1f);Check(hp.CurrentHealth==75,"Frozen impact must not deal damage again");
  var publish=typeof(WeaponInstance3D).Assembly.GetType("KarlsonDemo.Plugin.WeaponEventBridge")!.GetMethod("Publish",BindingFlags.Static|BindingFlags.NonPublic)!;var context=new EventExecutionContext{Scene=scene,Self=owner,Globals=new VariableStore()};publish.Invoke(null,[context,weapon,0,true]);Check(context.RaycastPerformed&&context.LastRaycastHit?.GameObject==enemy,"Projectile impact must be exposed to Event Sheets");
  var wall=scene.CreateGameObject("Thin cover");wall.Transform.WorldPosition=new(0,1,-2);wall.AddComponent(new BoxCollider3D{Size=new(2,2,.04f)});hp.Revive();p=Fire();Advance(p,.25f);Check(hp.CurrentHealth==100&&p.Transform.WorldPosition.Z>-2.1f,"Swept fast projectile must stop at thin cover");wall.Active=false;enemy.Active=false;
  var boss=scene.CreateGameObject("Boss");boss.Transform.WorldPosition=new(0,0,-6);boss.AddComponent(new CapsuleCollider3D{Radius=.625f,Height=2.9f,Center=new(0,1.45f,0)});var bh=boss.AddComponent(new HealthComponent{MaxHealth=900,CurrentHealth=900});var core=scene.CreateGameObject("Core");core.SetParent(boss,false);core.Transform.LocalPosition=new(0,.85f,.8f);core.AddComponent(new BoxCollider3D{Size=new(.9f,.9f,.8f)});core.AddComponent(new DamageRegion3D{HealthOwnerId=boss.Id});var armour=boss.AddComponent(new CombatArmour3D{WeakPointObjectId=core.Id});armour.SetExposed(true);
  p=Fire();Advance(p,.2f);Check(bh.CurrentHealth==850&&bh.LastHitKind==CombatHitKind.WeakPoint,"Physical bullet must damage the exposed back core");bh.Revive();p=Fire();armour.SetExposed(false);Advance(p,.2f);Check(bh.CurrentHealth==899&&bh.LastHitKind==CombatHitKind.Armour,"Weak-point state must be resolved on arrival, not launch");
  boss.Active=false;def.Range=5;def.ProjectileLifetime=5;p=Fire();Advance(p,.3f);Check(p.Transform.WorldPosition.Z>=-5.01f,"Projectile travel must not exceed range during a long frame");
  int ammo=weapon.CurrentAmmo;def.MaxActiveProjectiles=1;typeof(WeaponInstance3D).GetMethod("Tick",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(weapon,[1f]);Check(!weapon.Fire(new(0,.8f,0),-Vector3.UnitZ,owner)&&weapon.CurrentAmmo==ammo,"Projectile budget must reject a shot without consuming ammunition");
  var clock=scene.CreateGameObject("Lifetime clock").AddComponent(new LifetimeComponent());clock.Restart(1);typeof(LifetimeComponent).GetMethod("Simulate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(clock,[.2f]);Check(Math.Abs(clock.RemainingSeconds-.8f)<.001f,"Lifetime must use simulation delta, not the unrelated render frame delta");
  Console.WriteLine("PASS physical flight, owner ignore, swept cover collision, one-hit damage, event output, impact-time core state, range and projectile budgets");
 }
}
