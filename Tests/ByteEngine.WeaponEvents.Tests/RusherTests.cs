using System.Numerics;
using ByteEngine.Core.AI;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
static class RusherTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("Rusher: "+message);Console.WriteLine("PASS Rusher: "+message);}
    static (Scene Scene,GameObject Player,SimpleEnemyAI3D Rush,AIBehavior3D AI) Setup()
    {
        var scene=new Scene("Rusher test");var player=scene.CreateGameObject("Player");player.Transform.WorldPosition=new(0,0,-5);
        player.AddComponent(new HealthComponent());player.AddComponent(new BoxCollider3D{Size=new(.7f,1.6f,.7f),Center=new(0,.8f,0)});
        var actor=scene.CreateGameObject("Rusher");actor.AddComponent(new HealthComponent());
        var rush=actor.AddComponent(new SimpleEnemyAI3D{BehaviorControlled=true,PredatoryRush=true,AttackStyle=EnemyAttackStyle.Rusher,TargetId=player.Id,AttackRange=7.5f,ChargeSpeed=12.5f,ChargeDistance=7.5f,WindupSeconds=.7f,RecoverySeconds=.6f,AttackCooldown=1,Damage=20});
        var ai=actor.AddComponent(new AIBehavior3D{TargetObjectId=player.Id,FindNearestCharacter=false,DecisionInterval=.02f,PerceptionMemory=true,FieldOfView=360,MemorySeconds=.1f,MaximumAttackers=2});
        var attack=AIBehaviorPresets.AttackStages();var tree=AIBehaviorPresets.PredatoryRusher(new AssetReference(Guid.NewGuid(),"Assets/Attack.byteai"));ai.SetSubBehaviorResolver(_=>attack);ai.SetDefinition(tree);
        return(scene,player,rush,ai);
    }
    static void Tick(AIBehavior3D ai,float seconds){for(int i=0;i<(int)MathF.Round(seconds/.01f);i++)ai.Tick(.01f);}
    static void Until(AIBehavior3D ai,Func<bool> condition,int ticks=400){for(int i=0;i<ticks&&!condition();i++)ai.Tick(.01f);Check(condition(),"expected stage reached");}
    public static void Run()
    {
        var t=Setup();Tick(t.AI,.3f);Check(t.Rush.AttackPhase=="Windup"&&t.Player.GetComponent<HealthComponent>()!.CurrentHealth==100,"windup persists without damage or per-frame restart");
        t.Player.Transform.WorldPosition+=new Vector3(.5f,0,0);Tick(t.AI,.15f);var tracked=t.Rush.RushDirection;Check(tracked.X>0,"windup predicts lateral movement");
        Tick(t.AI,.14f);var locked=t.Rush.RushDirection;t.Player.Transform.WorldPosition+=new Vector3(3,0,0);Tick(t.AI,.1f);Check(Vector3.Distance(locked,t.Rush.RushDirection)<.00001f,"final lock ignores subsequent target motion");
        Until(t.AI,()=>t.Rush.LastRushOutcome==RushOutcome.Miss);Check(t.Player.GetComponent<HealthComponent>()!.CurrentHealth==100,"dodged charge deals no contact damage");Tick(t.AI,.4f);Check(t.Rush.AttackPhase=="Recover","miss leaves a real recovery window");Until(t.AI,()=>t.Rush.AttackPhase=="Ready");Check(t.Rush.AttackCooldownRemaining>.95f,"cooldown starts after recovery rather than expiring during it");
        t=Setup();Until(t.AI,()=>t.Rush.LastRushOutcome==RushOutcome.PlayerHit);Check(t.Player.GetComponent<HealthComponent>()!.CurrentHealth==80,"swept charge hits player exactly once");var pushed=t.Player.Transform.WorldPosition;Tick(t.AI,.4f);Check(t.Player.GetComponent<HealthComponent>()!.CurrentHealth==80&&pushed.Z< -5,"hit applies knockback and cannot damage again in recovery");
        t=Setup();Until(t.AI,()=>t.Rush.AttackPhase=="Charge");var wall=t.Scene.CreateGameObject("Wall inserted after lock");wall.Transform.WorldPosition=new(0,.8f,-2);wall.AddComponent(new BoxCollider3D{Size=new(3,2,.3f)});Until(t.AI,()=>t.Rush.LastRushOutcome==RushOutcome.WallCrash);
        Check(t.Player.GetComponent<HealthComponent>()!.CurrentHealth==100&&t.Rush.Transform.WorldPosition.Z> -2,"first wall collision blocks damage to player behind it");var enemyHealth=t.Rush.GameObject.GetComponent<HealthComponent>()!;enemyHealth.Damage(10);Check(enemyHealth.CurrentHealth==85,"wall crash grants 150 percent damage vulnerability");Tick(t.AI,.9f);Check(t.Rush.AttackPhase=="Recover","wall crash staggers longer than a miss");Until(t.AI,()=>t.Rush.AttackPhase=="Ready");Check(enemyHealth.IncomingDamageMultiplier==1,"crash vulnerability ends with recovery");
        t=Setup();var obstruction=t.Scene.CreateGameObject("Blocked lane");obstruction.Transform.WorldPosition=new(0,.8f,-2);obstruction.AddComponent(new BoxCollider3D{Size=new(3,2,.3f)});Tick(t.AI,.4f);Check(t.Rush.AttackPhase=="Ready"&&!t.Rush.CanBeginRush(t.Player),"blocked launch is rejected");
        t=Setup();Until(t.AI,()=>t.Rush.AttackPhase=="Windup");t.Rush.Stagger(.4f);Tick(t.AI,.2f);Check(t.Rush.AttackPhase=="Ready"&&t.Rush.StaggerRemaining>0,"designated stun interrupts and suspends normal decisions");Tick(t.AI,.3f);Check(t.Player.GetComponent<HealthComponent>()!.CurrentHealth==100,"stunned charge cannot leave delayed damage");
        t=Setup();Until(t.AI,()=>t.Rush.AttackPhase=="Charge");t.Rush.GameObject.GetComponent<HealthComponent>()!.Kill();Tick(t.AI,.5f);Check(t.AI.Status==BehaviorStatus.Idle&&t.Player.GetComponent<HealthComponent>()!.CurrentHealth==100,"death interrupts the committed charge");
        t=Setup();t.AI.PositionCandidates=24;Tick(t.AI,.2f);Check(t.AI.LastError==""&&t.Rush.AttackPhase=="Windup","subtree identities are independent of tactical candidate count");
        t=Setup();Until(t.AI,()=>t.Rush.AttackPhase=="Windup");t.Player.Transform.WorldPosition=new(30,0,-5);Tick(t.AI,.5f);Until(t.AI,()=>t.Rush.LastRushOutcome==RushOutcome.Miss);Check(t.AI.LastError=="","losing perception cannot strand an active charge");
    }
}
