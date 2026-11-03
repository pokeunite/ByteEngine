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
  spider.AddComponent(new ByteEngine.Core.Navigation.NavigationAgent3D());var movement=spider.AddComponent(new CapsuleCollider3D{Center=new(0,.5f,0),Radius=.4f,Height=1});spider.Transform.WorldPosition=new(0,0,-5);op.ConfigureEnemy(spider,true);
  Check(movement.QueryOnly,"navigation-driven movement collider skips unused solver contacts");
  Check(GameplayQuery3D.SphereCast(scene,new(0,.5f,0),-Vector3.UnitZ,.1f,out var solid,10,owner,includeTriggers:false)&&solid.GameObject==spider,"query-only spider remains solid to navigation/camera sweeps");
  var healthBefore=hp.CurrentHealth;var testShot=scene.CreateGameObject("Query collider bullet");testShot.Transform.WorldPosition=new(0,.5f,0);var bullet=testShot.AddComponent(new Projectile3D{OwnerId=owner.Id,Damage=10,Velocity=-Vector3.UnitZ*80});typeof(Projectile3D).GetMethod("Simulate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(bullet,[.1f]);Check(hp.CurrentHealth==healthBefore-10,"query-only movement collider still receives projectile damage");
  op.EnemyMovementQueriesOnly=false;op.ConfigureEnemy(spider,true);Check(!movement.QueryOnly,"authored contact events can opt back into solver contacts");op.EnemyMovementQueriesOnly=true;
  spider.AddComponent(new Rigidbody3D{BodyType=RigidbodyBodyType3D.Dynamic});op.ConfigureEnemy(spider,true);Check(!movement.QueryOnly,"dynamic rigid-body enemies retain physical contacts");
  var trade=scene.CreateGameObject("Press").AddComponent(new MiningTrade3D{PersistProgress=false});Check(trade.Cost(0)==112&&trade.Cost(1)==88&&trade.Cost(2)==144,"machine costs include the requested 20 percent reduction");
  Check(op.Cost(MiningUpgrade.Refinery)==104&&op.Cost(MiningUpgrade.RefineryYield)==192,"refinery costs retain separate tuning and the requested 20 percent reduction");
 }
}
