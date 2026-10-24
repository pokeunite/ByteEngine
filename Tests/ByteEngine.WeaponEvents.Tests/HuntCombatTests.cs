using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Variables;

static class HuntCombatTests
{
 public static void Run()
 {
  var tick=typeof(SimpleEnemyAI3D).GetMethod("Simulate",BindingFlags.NonPublic|BindingFlags.Instance)!;
  void Step(SimpleEnemyAI3D ai,float dt=.1f)=>tick.Invoke(ai,[dt]);
  void Require(bool condition,string why){if(!condition)throw new Exception(why);}
  var world=new Scene("Attack tests");var hero=world.CreateGameObject("Target");hero.Transform.WorldPosition=new(0,0,-5);hero.AddComponent(new CapsuleCollider3D{Radius=.3f,Height=1.8f,Center=new(0,.9f,0)});var hp=hero.AddComponent(new HealthComponent());
  var enemy=world.CreateGameObject("Caster");var ai=enemy.AddComponent(new SimpleEnemyAI3D{TargetId=hero.Id,AttackStyle=EnemyAttackStyle.GroundCaster,DetectionRange=20,AttackRange=10,StopDistance=10,Damage=15,WindupSeconds=.5f,RecoverySeconds=.5f,HazardRadius=2});
  Step(ai);Require(ai.AttackPhase=="Windup"&&hp.CurrentHealth==100,"Ground strike must telegraph before damage");hero.Transform.WorldPosition=new(4,0,-5);
  for(int n=0;n<6;n++)Step(ai);Require(hp.CurrentHealth==100,"Ground strike must lock the old target position so it can be dodged");
  ai.ResetAttackPattern();Step(ai);for(int n=0;n<6;n++)Step(ai);Require(hp.CurrentHealth==85,"Undodged ground strike must deal exactly one hit");
  ai.ResetAttackPattern();ai.AttackStyle=EnemyAttackStyle.Charger;ai.ChargeSpeed=8;ai.ChargeDistance=4;ai.RecoverySeconds=2;ai.WindupSeconds=.2f;var eh=enemy.AddComponent(new HealthComponent());hero.Transform.WorldPosition=new(0,0,-5);enemy.Transform.WorldPosition=Vector3.Zero;
  var wall=world.CreateGameObject("Obstacle");wall.Transform.WorldPosition=new(0,1,-2);wall.AddComponent(new BoxCollider3D{Size=new(3,2,.4f)});
  // Aim has a clear line, then an obstacle enters the locked charge path.
  wall.Active=false;Step(ai);wall.Active=true;for(int n=0;n<20&&!ai.IsExposed;n++)Step(ai);
  Require(ai.IsExposed&&enemy.Transform.WorldPosition.Z>-2,"Charge must stop at cover and expose recovery");float before=eh.CurrentHealth;eh.Damage(10);Require(before-eh.CurrentHealth==20,"Recovery weak point must double damage");ai.ResetAttackPattern();Require(eh.IncomingDamageMultiplier==1&&ai.AttackPhase=="Ready","Reset must remove recovery damage modifier");
  var registry=VisualLogicRegistry.CreateDefault();var context=new EventExecutionContext{Self=enemy,Scene=world,Globals=new VariableStore()};
  var group=world.CreateGameObject("Encounter");enemy.SetParent(group,true);var condition=new VisualInstruction{Id="combat.groupDefeated",Arguments=new(){["target"]=EventValue.String("id:"+group.Id)}};
  Require(registry.TryGetCondition(condition.Id,out var check),"Group defeated condition must be registered");Require(!check!.Evaluate(condition,context),"Living group must not pass");eh.Kill();enemy.Active=false;Require(check.Evaluate(condition,context),"Inactive defeated enemy must still count as cleared");
  Console.WriteLine("PASS enemy telegraphs, dodgeable ground strike, cover collision, recovery weakness and inactive encounter completion");
 }
}
