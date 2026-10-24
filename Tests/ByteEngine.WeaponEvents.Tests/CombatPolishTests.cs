using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
static class CombatPolishTests
{
 public static void Run()
 {
  void Require(bool c,string m){if(!c)throw new Exception(m);}
  var world=new Scene("Combat polish tests");var gun=world.CreateGameObject("Gun");var definition=gun.AddComponent(new WeaponDefinition3D{MagazineCapacity=12,ReserveCapacity=60,AutoReload=true,ReloadSeconds=.5f,EmptyReloadSeconds=.5f,EquipSeconds=0});var weapon=gun.AddComponent(new WeaponInstance3D{CurrentAmmo=0,ReserveAmmo=24});
  void Tick(float dt)=>typeof(WeaponInstance3D).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(weapon,[dt]);
  weapon.Equip(true);Tick(.01f);Require(weapon.Reloading&&weapon.ReloadStartSequence==1,"Empty equipped magazine must start one automatic reload");Tick(.6f);Require(!weapon.Reloading&&weapon.CurrentAmmo==12&&weapon.ReserveAmmo==12,"Auto reload must transfer reserve ammo normally");
  weapon.SetAmmo(0,0);Tick(.1f);Require(!weapon.Reloading,"No reserve ammo must prevent automatic reload loops");weapon.SetAmmo(0,12);weapon.Equip(false);Tick(.1f);Require(!weapon.Reloading,"Unequipped guns must not auto reload");weapon.Equip(true);definition.AutoReload=false;Tick(.1f);Require(!weapon.Reloading,"Auto reload must remain optional");
  var enemy=world.CreateGameObject("Enemy");var hp=enemy.AddComponent(new HealthComponent());enemy.AddComponent(new CapsuleCollider3D{Radius=.35f,Height=1.6f,Center=new(0,.8f,0)});
  var ui=world.CreateGameObject("Feedback");var feedback=ui.AddComponent(new CombatFeedback3D{EnemyAIOnly=false,KnockbackSpeed=4,KnockbackDecay=20});
  typeof(Component).GetMethod("StartInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(feedback,null);
  hp.Damage(10,Vector3.UnitX);hp.Damage(5,Vector3.UnitX);Require(feedback.ActiveNumberCount==1,"Same-frame pellets must form one popup");
  typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Component).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(feedback,null);Require(enemy.Transform.WorldPosition.X>0,"Hit direction must push enemy away");
  var wall=world.CreateGameObject("Wall");wall.Transform.WorldPosition=enemy.Transform.WorldPosition+new Vector3(.6f,.8f,0);wall.AddComponent(new BoxCollider3D{Size=new(.2f,2,2)});
  hp.Damage(1,Vector3.UnitX);float x=enemy.Transform.WorldPosition.X;typeof(Component).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(feedback,null);Require(enemy.Transform.WorldPosition.X<=x+.01f,"Knockback must not pass through cover");
  hp.Damage(1000,Vector3.UnitX);enemy.Active=false;Require(feedback.ActiveNumberCount>0,"Killing hits must retain their damage popup after enemy hides");
  Console.WriteLine("PASS optional auto reload, reserve transfer, no-ammo and unequipped guards, damage popup aggregation and collision-aware knockback");
 }
}
