using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.AI;
using KarlsonDemo.Plugin;
static class MiningPcPolishTests
{
 public static void Run()
 {
  void Check(bool pass,string message){if(!pass)throw new Exception(message);Console.WriteLine("PASS PC polish: "+message);}
  var scene=new Scene("Damage-only triggers");var owner=scene.CreateGameObject("Shooter");
  var pickup=scene.CreateGameObject("Pickup volume");pickup.Transform.WorldPosition=new(0,.5f,-1);pickup.AddComponent(new BoxCollider3D{IsTrigger=true,Size=new(1)});
  var spider=scene.CreateGameObject("Spider");var hp=spider.AddComponent(new HealthComponent());
  var body=scene.CreateGameObject("Body");body.SetParent(spider,false);body.Transform.LocalPosition=new(0,.5f,-3);body.AddComponent(new BoxCollider3D{IsTrigger=true});body.AddComponent(new DamageRegion3D{HealthOwnerId=spider.Id,IsWeakPoint=false});
  Check(GameplayQuery3D.RaycastWithDamageHits(scene,new(0,.5f,0),-Vector3.UnitZ,out var hit,10,owner,includeTriggers:false,includeDamageTriggers:true)&&hit.GameObject==body,"weapon aim ignores pickup volumes and sees spider body regions");
  Check(!GameplayQuery3D.Raycast(scene,new(0,.5f,0),-Vector3.UnitZ,out _,10,owner,includeTriggers:false),"movement/camera solid queries still ignore damage triggers");
  var shot=scene.CreateGameObject("Bullet");shot.Transform.WorldPosition=new(0,.5f,0);var projectile=shot.AddComponent(new Projectile3D{OwnerId=owner.Id,Damage=20,Velocity=-Vector3.UnitZ*80});
  typeof(Projectile3D).GetMethod("Simulate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(projectile,[.1f]);
  Check(hp.CurrentHealth==80,"fast projectile crosses pickup trigger and damages body exactly once");
  var op=scene.CreateGameObject("Operation").AddComponent(new MiningOperation3D{PersistProgress=false,RaiderDamage=6});var combat=spider.AddComponent(new SimpleEnemyAI3D{Damage=8});spider.AddComponent(new AIBehavior3D());
  op.ConfigureEnemy(spider,false);op.ConfigureEnemy(spider,false);Check(combat.Damage==6,"enemy setup is idempotent and uses explicit per-hit damage");
  var trade=scene.CreateGameObject("Press").AddComponent(new MiningTrade3D{PersistProgress=false});Check(trade.Cost(0)==140&&trade.Cost(1)==110&&trade.Cost(2)==180,"machine purchases no longer cost a fraction of one wave reward");
  Check(op.Cost(MiningUpgrade.Refinery)==130&&op.Cost(MiningUpgrade.RefineryYield)==240,"refinery costs use separate progression tuning");
 }
}
