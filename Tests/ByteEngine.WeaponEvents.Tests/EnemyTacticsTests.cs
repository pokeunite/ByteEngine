using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
static class EnemyTacticsTests
{
 public static void Run()
 {
  void Check(bool c,string m){if(!c)throw new Exception(m);}
  var step=typeof(SimpleEnemyAI3D).GetMethod("Simulate",BindingFlags.NonPublic|BindingFlags.Instance)!;
  void Tick(SimpleEnemyAI3D ai,float dt=.05f)=>step.Invoke(ai,[dt]);
  var world=new Scene("Tactics");var player=world.CreateGameObject("Player");player.Transform.WorldPosition=new(0,0,-6);var hp=player.AddComponent(new HealthComponent());
  var boss=world.CreateGameObject("Boss");boss.AddComponent(new HealthComponent());var ai=boss.AddComponent(new SimpleEnemyAI3D{TargetId=player.Id,AttackStyle=EnemyAttackStyle.Charger,AdaptiveBoss=true,MoveSpeed=0,AttackRange=20,StopDistance=4,WindupSeconds=.2f,ChargeSpeed=10,ChargeDistance=8,RecoverySeconds=.2f,AttackCooldown=.2f,SlamWarningSeconds=.3f,JumpSeconds=.4f,FeintWarningSeconds=.3f});
  Tick(ai);Tick(ai,.25f);Check(ai.AttackPhase=="Charge","Boss must introduce charge first");player.Transform.WorldPosition=new(6,0,-6);
  for(int i=0;i<60&&ai.EvadedCharges==0;i++)Tick(ai);Check(ai.EvadedCharges==1,"Missed charge must be remembered");
  for(int i=0;i<60&&ai.AttackPhase!="SlamWindup";i++)Tick(ai);Check(ai.BossAttack=="Slam"&&ai.AttackPhase=="SlamWindup","After evaded charge boss must introduce leap slam");player.Transform.WorldPosition=new(-6,0,-6);
  for(int i=0;i<60&&ai.EvadedSlams==0;i++)Tick(ai);Check(ai.EvadedSlams==1&&hp.CurrentHealth==100,"Evaded slam must miss and update learning");player.Transform.WorldPosition=new(0,0,-6);
  for(int i=0;i<100&&ai.CurrentTactic!="Rush feint into slam";i++)Tick(ai);Check(ai.BossAttack=="Feint"&&ai.CurrentTactic=="Rush feint into slam","Boss must unlock rush feint into a separately warned slam");
  ai.ResetAttackPattern();Check(ai.EvadedCharges==0&&ai.EvadedSlams==0&&ai.AttackPhase=="Ready","Boss retry must clear adaptation state");
  ai.AdaptiveBoss=false;ai.AttackStyle=EnemyAttackStyle.Rusher;ai.DodgeEnabled=true;ai.DodgeSeconds=.2f;ai.DodgeSpeed=6;ai.AttackRange=1.5f;boss.Transform.WorldPosition=Vector3.Zero;Tick(ai);boss.GetComponent<HealthComponent>()!.Damage(10,Vector3.UnitZ);Tick(ai);var before=boss.Transform.WorldPosition;Tick(ai,.1f);Check(ai.CurrentTactic=="Dodge"&&Vector3.Distance(before,boss.Transform.WorldPosition)>.4f,"Damage reaction must produce a physical sidestep");
  var nav=world.CreateGameObject("Nav");nav.Transform.WorldPosition=new(-12,0,-12);var region=nav.AddComponent(new NavigationRegion3D{Width=48,Height=48,CellSize=.5f,ExcludeDynamicActors=true,AgentRadius=.35f});
  var cover=world.CreateGameObject("Cover");cover.Transform.WorldPosition=new(-3,1,-4);cover.AddComponent(new BoxCollider3D{Size=new(1,2,3)});
  var rusher=world.CreateGameObject("Frontline");rusher.Transform.WorldPosition=new(3,0,-2);rusher.AddComponent(new HealthComponent());rusher.AddComponent(new SimpleEnemyAI3D{AttackStyle=EnemyAttackStyle.Rusher});
  player.Transform.WorldPosition=Vector3.Zero;boss.Transform.WorldPosition=new(0,0,-5);ai.ResetAttackPattern();ai.DodgeEnabled=false;ai.AttackStyle=EnemyAttackStyle.BoltCaster;ai.RetreatEnabled=true;ai.SeekCover=true;ai.PreferredRange=9;ai.CoverSearchRadius=6;ai.UseNavigation=true;ai.MoveSpeed=3;
  boss.AddComponent(new NavigationAgent3D{RegionObject=nav.Id,ExternallyDriven=true,ArrivalDistance=.04f});region.Bake();Tick(ai);Check(ai.CurrentTactic=="Retreat","Close ranged attacker must retreat instead of advancing");
  var destination=(Vector3)typeof(SimpleEnemyAI3D).GetField("_coverPoint",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(ai)!;
  bool hidden=(bool)typeof(SimpleEnemyAI3D).GetMethod("IsHiddenFrom",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(ai,[player,destination])!;Check(hidden,"Ranged attacker must choose actual occluding cover when frontline exists");
  for(int i=0;i<20;i++)Tick(ai);Check(Vector3.Distance(boss.Transform.WorldPosition,player.Transform.WorldPosition)>5,"Retreat path must increase distance from player");
  Console.WriteLine("PASS hit-triggered dodge, ranged retreat/cover, learned leap slam, rush feint and checkpoint adaptation reset");
 }
}
