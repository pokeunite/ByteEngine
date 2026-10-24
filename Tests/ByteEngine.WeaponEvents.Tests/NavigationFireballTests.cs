using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Scene;
static class NavigationFireballTests
{
 public static void Run()
 {
  void Check(bool value,string why){if(!value)throw new Exception(why);}
  var world=new Scene("Navigation combat test");var target=world.CreateGameObject("Target");target.Transform.WorldPosition=new(3,0,0);target.AddComponent(new HealthComponent());target.AddComponent(new CapsuleCollider3D{Radius=.3f,Height=1.8f,Center=new(0,.9f,0)});
  var wall=world.CreateGameObject("Cover");wall.AddComponent(new BoxCollider3D{Size=new(2,2,3),Center=new(0,1,0)});
  var nav=world.CreateGameObject("Navigation");nav.Transform.WorldPosition=new(-5,0,-5);var region=nav.AddComponent(new NavigationRegion3D{Width=40,Height=40,CellSize=.25f,AgentRadius=.4f,ExcludeDynamicActors=true});
  var enemy=world.CreateGameObject("Enemy");enemy.Transform.WorldPosition=new(-3,0,0);enemy.AddComponent(new HealthComponent());enemy.AddComponent(new CapsuleCollider3D{Radius=.35f,Height=1.6f,Center=new(0,.8f,0)});var agent=enemy.AddComponent(new NavigationAgent3D{RegionObject=nav.Id,ExternallyDriven=true,CollisionAvoidance=true,ArrivalDistance=.04f,RepathInterval=.35f});var ai=enemy.AddComponent(new SimpleEnemyAI3D{UseNavigation=true,AttackStyle=EnemyAttackStyle.Rusher,TargetId=target.Id,MoveSpeed=3,StopDistance=1.05f,AttackRange=1.7f,WindupSeconds=10});
  region.Bake();var tick=typeof(SimpleEnemyAI3D).GetMethod("Simulate",BindingFlags.Instance|BindingFlags.NonPublic)!;
  for(int i=0;i<1200&&Vector3.Distance(enemy.Transform.WorldPosition,target.Transform.WorldPosition)>1.7f;i++)tick.Invoke(ai,[1f/60]);
  Check(agent.PathQueries>0&&agent.Path.Any(p=>Math.Abs(p.Z)>1.8f),"Enemy must compute a route around cover, not slide into it");Check(Vector3.Distance(enemy.Transform.WorldPosition,target.Transform.WorldPosition)<=1.7f,$"Enemy must actually follow its path to attack range: {enemy.Transform.WorldPosition} status={agent.Status} phase={ai.AttackPhase} queries={agent.PathQueries}");
  var path=region.FindPath(new(-1.4f,0,0),target.Transform.WorldPosition);Check(path.Count>0,"A start near inflated cover must recover to nearby walkable cells");
  ai.ResetAttackPattern();ai.UseNavigation=false;enemy.Transform.WorldPosition=new(-3,0,0);target.Transform.WorldPosition=new(-3,0,-5);ai.AttackStyle=EnemyAttackStyle.GroundCaster;ai.FallingFireball=true;ai.WindupSeconds=.8f;ai.FireballHeight=12;ai.StopDistance=10;ai.AttackRange=10;ai.Damage=14;
  var hp=target.GetComponent<HealthComponent>()!;hp.Revive();tick.Invoke(ai,[.01f]);var ball=world.GameObjects.Single(o=>o.Name.StartsWith("Falling fireball"));float height=ball.Transform.WorldPosition.Y;Check(height>10&&hp.CurrentHealth==100,"Sky fireball must start overhead without early damage");tick.Invoke(ai,[.3f]);Check(ball.Transform.WorldPosition.Y<height&&hp.CurrentHealth==100,"Fireball must visibly descend during the warning");tick.Invoke(ai,[.6f]);Check(hp.CurrentHealth==86&&world.GameObjects.Any(o=>o.Name.StartsWith("Fireball impact")),"Fireball impact must coincide with its area damage and explosion");
  var cachedImpact=world.GameObjects.Single(o=>o.Name.StartsWith("Fireball impact"));
  ai.ResetAttackPattern();tick.Invoke(ai,[.01f]);var repeated=world.GameObjects.Single(o=>o.Name.StartsWith("Falling fireball"));Check(ReferenceEquals(ball,repeated),"Repeated fireballs must reuse the same render object");tick.Invoke(ai,[.9f]);Check(ReferenceEquals(cachedImpact,world.GameObjects.Single(o=>o.Name.StartsWith("Fireball impact"))),"Repeated impacts must reuse their VFX buffers");
  ai.ResetAttackPattern();tick.Invoke(ai,[.01f]);enemy.GetComponent<HealthComponent>()!.Kill();Check(!ball.Active,"Killing a caster must cancel its cached falling effect even before another AI tick");
  Console.WriteLine("PASS A* enemy pursuit around cover, endpoint recovery, falling fireball descent and impact timing");
 }
}
