using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
static class BossOpeningTests
{
 public static void Run()
 {
  void Check(bool value,string message){if(!value)throw new Exception(message);}
  var detached=new HealthComponent();detached.Damage(5);Check(detached.CurrentHealth==95,"Standalone health use must remain compatible without a scene object");
  var scene=new Scene("Boss openings");var boss=scene.CreateGameObject("Armoured boss");var health=boss.AddComponent(new HealthComponent{MaxHealth=900,CurrentHealth=900});boss.AddComponent(new CapsuleCollider3D{Radius=.625f,Height=2.9f,Center=new(0,1.45f,0)});
  var core=scene.CreateGameObject("Back core");core.SetParent(boss,false);core.Transform.LocalPosition=new(0,.85f,.8f);core.AddComponent(new BoxCollider3D{Size=new(.9f,.9f,.8f)});core.AddComponent(new DamageRegion3D{HealthOwnerId=boss.Id});
  var armour=boss.AddComponent(new CombatArmour3D{WeakPointObjectId=core.Id});var player=scene.CreateGameObject("Player");var gun=scene.CreateGameObject("Gun");gun.AddComponent(new WeaponDefinition3D{MagazineCapacity=100,ReserveCapacity=100,DamagePerPellet=25,EquipSeconds=0,ShowShotTrace=false,MuzzleFlashEnabled=false});var weapon=gun.AddComponent(new WeaponInstance3D{CurrentAmmo=100,ReserveAmmo=100});weapon.Equip(true);
  void Shot(Vector3 origin,Vector3 direction){typeof(WeaponInstance3D).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(weapon,[1f]);Check(weapon.Fire(origin,direction,player),"Test shot must be accepted");}
  armour.SetExposed(false);Shot(new(0,.85f,5),-Vector3.UnitZ);Check(health.CurrentHealth==899&&health.LastHitKind==CombatHitKind.Armour,"Closed core must leave rear shots hitting armour for 1 damage");
  armour.SetExposed(true);Shot(new(0,.85f,-5),Vector3.UnitZ);Check(health.CurrentHealth==898&&health.LastHitKind==CombatHitKind.Armour,"Exposed core must not make the front/body vulnerable");
  Shot(new(0,.85f,5),-Vector3.UnitZ);Check(health.CurrentHealth==848&&health.LastHitKind==CombatHitKind.WeakPoint,"Rear ray must hit the exposed region for 50 actual damage");
  armour.SetExposed(false);Shot(new(0,.85f,5),-Vector3.UnitZ);Check(health.CurrentHealth==847&&health.LastHitKind==CombatHitKind.Armour,"Closing recovery must immediately restore armour");
  health.CurrentHealth=300;Check(armour.IsEnraged,"Low health must trigger enrage");
  var target=scene.CreateGameObject("Target");target.Transform.WorldPosition=new(0,0,-5);target.AddComponent(new HealthComponent());var ai=boss.AddComponent(new SimpleEnemyAI3D{TargetId=target.Id,AttackStyle=EnemyAttackStyle.Charger,AdaptiveBoss=true,RecoveryDamageMultiplier=1});armour.SetExposed(true);ai.ResetAttackPattern();Check(!armour.IsExposed&&!core.Active,"Checkpoint reset must close the weak point");
  Console.WriteLine("PASS actual gun rays: closed armour, front protection during exposure, rear weak-point damage, window closure, enrage and reset");
 }
}
