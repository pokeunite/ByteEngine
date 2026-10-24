using System.Numerics;
using ByteEngine.Core.AI;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
static class AICombatRoleTests
{
    static void Check(bool v,string message){if(!v)throw new Exception(message);}
    public static void Run()
    {
        var scene=new Scene("Combat role");var player=scene.CreateGameObject("Player");player.Transform.WorldPosition=new(0,0,-1);var hp=player.AddComponent(new HealthComponent());
        var actor=scene.CreateGameObject("Rusher");actor.AddComponent(new HealthComponent());
        var combat=actor.AddComponent(new SimpleEnemyAI3D{BehaviorControlled=true,AttackStyle=EnemyAttackStyle.Rusher,TargetId=player.Id,MoveSpeed=0,AttackRange=2,StopDistance=2,WindupSeconds=.1f,RecoverySeconds=.2f,AttackCooldown=.5f,Damage=8});
        var ai=actor.AddComponent(new AIBehavior3D{TargetObjectId=player.Id,FindNearestCharacter=false,DecisionInterval=.02f});
        var task=new BehaviorNode{Kind=BehaviorNodeKind.EnemyCombat,Name="Rusher combat"};ai.SetDefinition(new(){Root=task});
        ai.Tick(.05f);ai.Tick(.05f);Check(combat.AttackPhase=="Windup"&&hp.CurrentHealth==100,"Role must preserve native attack windup without immediate damage");
        for(int i=0;i<4;i++)ai.Tick(.05f);Check(hp.CurrentHealth==92,"Role must advance native combat once per simulation tick");
        ai.SetPaused(true);Check(combat.AttackPhase=="Ready","Pausing a role cancels warnings/recovery and clears its driver");
        ai.SetPaused(false);combat.Enabled=false;ai.Tick(.1f);Check(ai.Status==BehaviorStatus.Idle&&ai.LastError.Length==0,"Encounter disabling must suspend role without binding errors");combat.Enabled=true;combat.AttackStyle=EnemyAttackStyle.Charger;combat.WindupSeconds=.3f;combat.ChargeDistance=4;combat.ChargeSpeed=8;ai.Tick(.05f);ai.Tick(.05f);
        Check(combat.AttackPhase=="Windup","Same role task must retain charger implementation");
        ai.SetMemory("Interrupt",ByteEngine.Core.Variables.VariableValue.FromBoolean(true));
        ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Priority,Children=[new(){Kind=BehaviorNodeKind.Gate,Test=BehaviorTest.MemoryTrue,Key="Interrupt",Children=[new(){Kind=BehaviorNodeKind.Wait,Duration=1}]},task]}});
        ai.Tick(.05f);ai.Tick(.05f);ai.SetMemory("Interrupt",ByteEngine.Core.Variables.VariableValue.FromBoolean(true));ai.Tick(.05f);
        Check(combat.AttackPhase=="Ready","Higher priority rule must cancel an owned combat role");
        var invalid=actor.AddComponent(new AIBehavior3D{TargetObjectId=player.Id,FindNearestCharacter=false});invalid.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.EnemyCombat}});combat.BehaviorControlled=false;invalid.Tick(.1f);Check(invalid.LastError.Length>0,"Uncontrolled legacy AI must still be rejected");
        Console.WriteLine("PASS native combat role: windup, damage timing, charger role, pause and priority cancellation without two autonomous drivers");
    }
}
