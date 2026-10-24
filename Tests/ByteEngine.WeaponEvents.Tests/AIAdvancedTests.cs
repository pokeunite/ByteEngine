using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
using System.Numerics;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
static class AIAdvancedTests
{
    static void Check(bool value,string message){if(!value)throw new Exception("Advanced AI: "+message);}
    static BehaviorNode N(BehaviorNodeKind kind)=>new(){Kind=kind,Name=kind.ToString()};
    static AIBehavior3D Brain(Scene scene,GameObject target,BehaviorNode? root=null,bool perception=true)
    {
        var actor=scene.CreateGameObject("Agent");actor.AddComponent(new HealthComponent());
        var ai=actor.AddComponent(new AIBehavior3D{TargetObjectId=target.Id,FindNearestCharacter=false,DecisionInterval=.02f,PerceptionMemory=perception,FieldOfView=360,MemorySeconds=1,HearingMemorySeconds=1});
        ai.SetDefinition(new(){Root=root??new(){Kind=BehaviorNodeKind.Wait,Duration=100}});return ai;
    }
    public static void Run()
    {
        Perception();ServicesAndSubtrees();Interruption();PositionsAndSquads();StagedAttacks();
        Console.WriteLine("PASS advanced AI suite: sight/FOV/occlusion, hearing, memory expiry, search, services, sub-behaviour isolation/recursion, interrupt modes, tactical positions, squad limits/cleanup, staged attacks and bounded history");
    }
    static void Perception()
    {
        var scene=new Scene("Perception");var target=scene.CreateGameObject("Player");target.Transform.WorldPosition=new(0,0,-4);target.AddComponent(new HealthComponent());var ai=Brain(scene,target);ai.Tick(.1f);
        Check(ai.Target==target&&ai.Blackboard["HasLineOfSight"].Boolean,"visible target must be sensed");var known=ai.LastKnownPosition;
        var wall=scene.CreateGameObject("Wall");wall.Transform.WorldPosition=new(0,.8f,-2);wall.AddComponent(new BoxCollider3D{Size=new(5,2,.2f)});target.Transform.WorldPosition=new(0,0,-7);ai.Tick(.1f);
        Check(!ai.Blackboard["HasLineOfSight"].Boolean&&ai.LastKnownPosition==known&&ai.HasInterest,"wall must preserve old position without tracking through cover");
        ai.Tick(1.1f);Check(ai.Target==null&&!ai.HasInterest,"lost target must expire");wall.Active=false;
        ai.FieldOfView=90;target.Transform.WorldPosition=new(0,0,6);ai.Tick(.1f);Check(ai.Target==null,"rear target outside FOV must not be visible");
        AIWorld.ReportNoise(scene,target.Transform.WorldPosition,10,target.Id,"Test shot");ai.Tick(.1f);Check(ai.Blackboard["HeardNoise"].Boolean&&ai.LastKnownPosition==target.Transform.WorldPosition&&!ai.Blackboard["HasLineOfSight"].Boolean,"hearing should create an interest point without granting sight");
        ai.Tick(.1f);Check(!ai.Blackboard["HeardNoise"].Boolean,"noise should be consumed once");ai.ForgetTarget();AIWorld.ReportNoise(scene,new(0,0,1),15,ai.GameObject.Id,"Self");ai.Tick(.1f);Check(!ai.HasInterest,"agent must ignore its own noises");
        var blockedScene=new Scene("Hearing walls");var source=blockedScene.CreateGameObject("Source");source.Transform.WorldPosition=new(0,0,8);var listener=Brain(blockedScene,source);listener.FieldOfView=60;listener.OccludedHearingMultiplier=.25f;
        var cover=blockedScene.CreateGameObject("Sound cover");cover.Transform.WorldPosition=new(0,.8f,4);cover.AddComponent(new BoxCollider3D{Size=new(5,2,.3f)});AIWorld.ReportNoise(blockedScene,source.Transform.WorldPosition,10,source.Id);listener.Tick(.1f);Check(!listener.HasInterest,"occlusion must attenuate hearing radius");
        cover.Active=false;AIWorld.ReportNoise(blockedScene,source.Transform.WorldPosition,10,source.Id);listener.Tick(.1f);Check(listener.HasInterest,"unblocked noise in range must be heard");
        var nav=blockedScene.CreateGameObject("Nav");nav.Transform.WorldPosition=new(-10,0,-10);var region=nav.AddComponent(new NavigationRegion3D{Width=40,Height=40,CellSize=.5f});region.Bake();listener.GameObject.AddComponent(new NavigationAgent3D{RegionObject=nav.Id});listener.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Search,Duration=.5f,Speed=2}});AIWorld.ReportNoise(blockedScene,source.Transform.WorldPosition,10,source.Id);listener.Tick(.1f);listener.Tick(.1f);Check(listener.Blackboard.Contains("SearchPoint"),"search should choose a last-known navigation point");listener.Tick(.7f);Check(listener.Status==BehaviorStatus.Success,"search task must finish within its duration");
    }
    static void ServicesAndSubtrees()
    {
        var scene=new Scene("Services");var target=scene.CreateGameObject("Target");var ai=Brain(scene,target);
        var service=new BehaviorService{Kind=AIServiceKind.HealthFlag,Key="NeedsHelp",Interval=.5f,Threshold=.4f};ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Wait,Duration=20},Services=[service]});ai.Tick(.1f);Check(!ai.Blackboard["NeedsHelp"].Boolean,"health service initial value");ai.GameObject.GetComponent<HealthComponent>()!.CurrentHealth=20;ai.Tick(.1f);Check(!ai.Blackboard["NeedsHelp"].Boolean,"service interval must be honoured");ai.Tick(.5f);Check(ai.Blackboard["NeedsHelp"].Boolean&&ai.Status==BehaviorStatus.Running,"service must update during a latent task");
        var reusable=new BehaviorTreeDefinition{Root=new(){Kind=BehaviorNodeKind.Sequence,Children=[new(){Kind=BehaviorNodeKind.Signal,Key="SubPulse"},new(){Kind=BehaviorNodeKind.Wait,Duration=.1f}]},Services=[new(){Kind=AIServiceKind.SetMemory,Key="SubService",Value=VariableValue.FromBoolean(true),Interval=.2f}]};
        ai.SetSubBehaviorResolver(_=>reusable);ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Sequence,Children=[new(){Kind=BehaviorNodeKind.SubBehavior,SubBehavior=new AssetReference("Assets/Shared.byteai")},new(){Kind=BehaviorNodeKind.SubBehavior,SubBehavior=new AssetReference("Assets/Shared.byteai")}]}});long before=ai.SignalSequence;
        for(int i=0;i<20;i++){ai.Tick(.05f);if(ai.Status==BehaviorStatus.Success)break;}Check(ai.SignalSequence==before+2,"each sub-behaviour call site must have isolated task state");Check(ai.Blackboard["SubService"].Boolean,"active sub-behaviour services must run");
        var loop=new BehaviorTreeDefinition{Root=new(){Kind=BehaviorNodeKind.SubBehavior,SubBehavior=new AssetReference("Assets/Loop.byteai")}};ai.SetSubBehaviorResolver(_=>loop);ai.SetDefinition(loop);ai.Tick(.1f);Check(ai.Status==BehaviorStatus.Failure&&ai.LastError.Contains("recursive"),"recursive sub-behaviours must fail safely");
        ai.RecordHistory=true;ai.HistoryCapacity=16;ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Priority,Children=[new(){Kind=BehaviorNodeKind.Gate,Test=BehaviorTest.MemoryTrue,Key="Off",Children=[N(BehaviorNodeKind.Wait)]},N(BehaviorNodeKind.Wait)]}});for(int i=0;i<100;i++){ai.SetMemory("Off",VariableValue.FromBoolean(i%2==0));ai.Tick(.1f);}Check(ai.DecisionHistory.Count<=16&&ai.ExportDecisionHistory().Contains("Reason"),"history must be bounded and export reasons");
    }
    static void Interruption()
    {
        var scene=new Scene("Interrupts");var target=scene.CreateGameObject("Target");var ai=Brain(scene,target,perception:false);
        BehaviorTreeDefinition Tree(BehaviorInterruption mode)=>new(){Root=new(){Kind=BehaviorNodeKind.Gate,Test=BehaviorTest.MemoryTrue,Key="Allowed",Interruption=mode,Children=[new(){Kind=BehaviorNodeKind.Wait,Duration=.3f}]}};
        ai.SetDefinition(Tree(BehaviorInterruption.FinishBranch));ai.SetMemory("Allowed",VariableValue.FromBoolean(true));ai.Tick(.1f);ai.SetMemory("Allowed",VariableValue.FromBoolean(false));ai.Tick(.1f);Check(ai.Status==BehaviorStatus.Running,"finish-branch gate must retain its running child");ai.Tick(.3f);Check(ai.Status==BehaviorStatus.Success,"finish-branch must complete");
        ai.SetDefinition(Tree(BehaviorInterruption.Recheck));ai.SetMemory("Allowed",VariableValue.FromBoolean(true));ai.Tick(.1f);ai.SetMemory("Allowed",VariableValue.FromBoolean(false));ai.Tick(.1f);Check(ai.Status==BehaviorStatus.Failure,"rechecking gate must abort immediately");
        ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Priority,Interruption=BehaviorInterruption.FinishBranch,Children=[new(){Kind=BehaviorNodeKind.Gate,Test=BehaviorTest.MemoryTrue,Key="Urgent",Children=[new(){Kind=BehaviorNodeKind.Signal,Key="Urgent"}]},new(){Kind=BehaviorNodeKind.Wait,Duration=.3f}]}});ai.Tick(.1f);long before=ai.SignalSequence;ai.SetMemory("Urgent",VariableValue.FromBoolean(true));ai.Tick(.1f);Check(ai.SignalSequence==before,"finish-branch priority must not preempt an active task");ai.Tick(.3f);ai.Tick(.1f);Check(ai.SignalSequence>before,"higher priority should run after branch completion");
    }
    static void PositionsAndSquads()
    {
        var scene=new Scene("Tactics");var target=scene.CreateGameObject("Target");target.AddComponent(new HealthComponent());
        var nav=scene.CreateGameObject("Nav");nav.Transform.WorldPosition=new(-12,0,-12);var region=nav.AddComponent(new NavigationRegion3D{Width=48,Height=48,CellSize=.5f,ExcludeDynamicActors=true});
        var wall=scene.CreateGameObject("Cover");wall.Transform.WorldPosition=new(-2,.8f,0);wall.AddComponent(new BoxCollider3D{Size=new(.5f,2,3)});region.Bake();
        AIBehavior3D Query(AIPositionKind kind){var ai=Brain(scene,target,perception:false);ai.GameObject.Transform.WorldPosition=new(-6,0,0);ai.GameObject.AddComponent(new NavigationAgent3D{RegionObject=nav.Id});ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.FindPosition,PositionKind=kind,Distance=4,SearchRadius=5,Spacing=1.5f,Duration=3,Key="Spot"}});for(int attempt=0;attempt<12;attempt++){ai.Tick(.1f);if(ai.Status!=BehaviorStatus.Running)break;}return ai;}
        var coverAI=Query(AIPositionKind.Cover);Check(coverAI.Status==BehaviorStatus.Success&&coverAI.Blackboard.Contains("Spot"),"cover query must find a reachable hidden point");
        coverAI.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.FindPosition,PositionKind=AIPositionKind.FiringLane,Distance=5,Key="Lane"}});for(int attempt=0;attempt<12;attempt++){coverAI.Tick(.1f);if(coverAI.Status!=BehaviorStatus.Running)break;}Check(coverAI.Status==BehaviorStatus.Success,"firing-lane query must find a reachable visible point");
        wall.Active=false;region.Bake();var first=Query(AIPositionKind.Surround);var second=Query(AIPositionKind.Surround);Check(Vector3.Distance(first.Blackboard["Spot"].Vector3,second.Blackboard["Spot"].Vector3)>=1.4f,"surrounders must reserve different positions");
        var flank=Query(AIPositionKind.Flank);Check(flank.Status==BehaviorStatus.Success&&Math.Abs(flank.Blackboard["Spot"].Vector3.Z)>1,"flank point must offset the direct approach");
        var world=AIWorld.For(scene);first.MaximumAttackers=2;second.MaximumAttackers=2;flank.MaximumAttackers=2;Check(world.ReserveAttack(first,target.Id,3)&&world.ReserveAttack(second,target.Id,3)&&!world.ReserveAttack(flank,target.Id,3),"squad limit must cap simultaneous attackers");first.SetPaused(true);Check(world.ReserveAttack(flank,target.Id,3),"paused agents must release attack capacity");second.GameObject.Active=false;Check(world.AttackReservationCount==1,"inactive agents must be pruned");flank.GameObject.GetComponent<HealthComponent>()!.Kill();Check(world.AttackReservationCount==0,"dead agents must release attack slots");
        var unrelated=new Scene("Other scene");Check(AIWorld.For(unrelated).AttackReservationCount==0,"reservations must not leak across scenes");
        var stress=new Scene("Squad stress");var player=stress.CreateGameObject("Player");var brains=new List<AIBehavior3D>();
        for(int i=0;i<40;i++){var ai=Brain(stress,player,perception:false);ai.MaximumAttackers=2;ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Sequence,Children=[new(){Kind=BehaviorNodeKind.ReserveAttack,Duration=5,Timeout=10},new(){Kind=BehaviorNodeKind.Wait,Duration=.1f},N(BehaviorNodeKind.ReleaseAttack)]}});brains.Add(ai);}
        int peak=0;for(int frame=0;frame<100;frame++){foreach(var ai in brains){ai.Tick(.02f);peak=Math.Max(peak,AIWorld.For(stress).AttackReservationCount);Check(peak<=2,"stress simulation exceeded attack cap");}}
        foreach(var ai in brains)ai.Stop();Check(peak==2&&AIWorld.For(stress).AttackReservationCount==0,"stress reservations must clean up");
    }
    static void StagedAttacks()
    {
        var scene=new Scene("Stages");var target=scene.CreateGameObject("Target");target.Transform.WorldPosition=new(0,0,-1);var hp=target.AddComponent(new HealthComponent());var ai=Brain(scene,target,perception:false);ai.GameObject.AddComponent(new SimpleEnemyAI3D{BehaviorControlled=true,AttackStyle=EnemyAttackStyle.Rusher,AttackRange=2,StopDistance=2,MoveSpeed=0,Damage=9,WindupSeconds=.2f,RecoverySeconds=.2f,AttackCooldown=.2f});
        var stages=new BehaviorTreeDefinition{Root=new(){Kind=BehaviorNodeKind.Sequence,Children=[new(){Kind=BehaviorNodeKind.ReserveAttack,Duration=5},N(BehaviorNodeKind.ChooseAttack),N(BehaviorNodeKind.Telegraph),N(BehaviorNodeKind.CommitAttack),N(BehaviorNodeKind.Strike),N(BehaviorNodeKind.Recover)]}};
        ai.SetDefinition(stages);ai.Tick(.05f);Check(hp.CurrentHealth==100&&ai.CurrentTask=="Telegraph","telegraph must remain separate and deal no damage");
        for(int i=0;i<7;i++)ai.Tick(.05f);Check(hp.CurrentHealth==91,"strike must deal exactly one staged hit");for(int i=0;i<4;i++)ai.Tick(.05f);Check(ai.Signals.Any(s=>s.Name=="AttackCommitted")&&ai.Signals.Any(s=>s.Name=="AttackRecovery"),"commit/recovery must produce separate observable stages");
        ai.SetPaused(true);Check(AIWorld.For(scene).AttackReservationCount==0,"cancelling staged combat must release squad reservation");
        ai.SetPaused(false);hp.Revive();target.Transform.WorldPosition=new(0,0,-1);ai.SetDefinition(stages);ai.Tick(.05f);target.Transform.WorldPosition=new(0,0,1);
        for(int i=0;i<20;i++){ai.Tick(.05f);if(ai.Status==BehaviorStatus.Success)break;}Check(hp.CurrentHealth==100,"committed melee must not swivel around and hit a dodging target behind its locked direction");
        hp.Revive();target.Transform.WorldPosition=new(0,0,-1);ai.SetDefinition(stages);ai.Tick(.05f);var wall=scene.CreateGameObject("Late cover");wall.Transform.WorldPosition=new(0,.8f,-.5f);wall.AddComponent(new BoxCollider3D{Size=new(2,2,.1f)});
        for(int i=0;i<20;i++){ai.Tick(.05f);if(ai.Status==BehaviorStatus.Success)break;}Check(hp.CurrentHealth==100,"staged melee must respect cover added during windup");wall.Active=false;
        ai.SetDefinition(stages);ai.Tick(.05f);ai.Tick(20);Check(hp.CurrentHealth==100&&AIWorld.For(scene).AttackReservationCount==0,"expired attack lease must prevent commitment after a long frame");
        var chargeScene=new Scene("Committed charge");var victim=chargeScene.CreateGameObject("Victim");victim.Transform.WorldPosition=new(0,0,-4);victim.AddComponent(new HealthComponent());var charger=Brain(chargeScene,victim,perception:false);var native=charger.GameObject.AddComponent(new SimpleEnemyAI3D{BehaviorControlled=true,AttackStyle=EnemyAttackStyle.Charger,AttackRange=5,ChargeDistance=5,ChargeSpeed=8,WindupSeconds=.1f,RecoverySeconds=.2f,AttackCooldown=.2f});
        charger.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Gate,Test=BehaviorTest.LineOfSight,Interruption=BehaviorInterruption.FinishCommittedAttack,Children=[stages.Root]}});
        for(int i=0;i<6;i++)charger.Tick(.05f);Check(native.AttackPhase=="Charge","charge should reach committed execution");
        var obstruction=chargeScene.CreateGameObject("Charge cover");obstruction.Transform.WorldPosition=new(0,.8f,-2.5f);obstruction.AddComponent(new BoxCollider3D{Size=new(3,2,.2f)});
        for(int i=0;i<30;i++){charger.Tick(.05f);if(charger.Status==BehaviorStatus.Success)break;}Check(charger.Status==BehaviorStatus.Success&&AIWorld.For(chargeScene).AttackReservationCount==0,"finish-committed rule must finish charge/recovery despite losing sight, then release reservations");

    }
}
