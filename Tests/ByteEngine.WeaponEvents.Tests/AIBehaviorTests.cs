using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
static class AIBehaviorTests
{
    static void Check(bool value,string message){if(!value)throw new Exception("AI behaviour: "+message);}
    static BehaviorNode Node(BehaviorNodeKind kind,string name="Task")=>new(){Kind=kind,Name=name};
    public static void Run()
    {
        var scene=new Scene("AI tests");var target=scene.CreateGameObject("Target");target.Transform.WorldPosition=new(0,0,-1);var health=target.AddComponent(new HealthComponent());
        var enemy=scene.CreateGameObject("Enemy");var ai=enemy.AddComponent(new AIBehavior3D{TargetObjectId=target.Id,FindNearestCharacter=false,DecisionInterval=.02f});
        var template=BehaviorTreeDefinition.CreateExample();ai.SetDefinition(template);
        ai.Tick(.05f);Check(health.CurrentHealth==100&&ai.LastSignal=="AttackWindup","windup must signal before any damage");
        for(int i=0;i<12;i++)ai.Tick(.05f);
        Check(health.CurrentHealth==90,"melee should deal one hit after windup, not every tick");
        for(int i=0;i<5;i++)ai.Tick(.05f);Check(health.CurrentHealth==90,"recovery must not repeat damage");
        ai.SetDefinition(template);health.Revive();ai.Tick(.05f);target.Transform.WorldPosition=new(0,0,-10);ai.Tick(.05f);
        Check(health.CurrentHealth==100,"leaving attack gate must abort pending damage");
        target.Transform.WorldPosition=new(0,0,-1);ai.Tick(.05f);Check(health.CurrentHealth==100,"reentering gate starts a fresh windup");
        ai.SetPaused(true);for(int i=0;i<20;i++)ai.Tick(.05f);Check(health.CurrentHealth==100,"pause must not advance latent tasks");
        ai.SetPaused(false);
        var abortSignal=Node(BehaviorNodeKind.Signal);abortSignal.Key="Urgent";
        var longWait=Node(BehaviorNodeKind.Wait);longWait.Duration=5;
        var priority=new BehaviorTreeDefinition{Root=new(){Kind=BehaviorNodeKind.Priority,Children=[new(){Kind=BehaviorNodeKind.Gate,Test=BehaviorTest.MemoryTrue,Key="Urgent",Children=[abortSignal]},longWait]}};
        ai.SetDefinition(priority);ai.Tick(.1f);Check(ai.NodeStatus[longWait.Id]==BehaviorStatus.Running,"fallback task must run");
        ai.SetMemory("Urgent",VariableValue.FromBoolean(true));ai.Tick(.02f);Check(ai.LastSignal=="Urgent"&&ai.NodeStatus[longWait.Id]==BehaviorStatus.Aborted,"higher-priority memory change must preempt and mark old task aborted");
        var second=scene.CreateGameObject("Independent enemy").AddComponent(new AIBehavior3D{FindNearestCharacter=false,DecisionInterval=.02f});
        priority.Blackboard.Add(new(){Name="Personal",Value=VariableValue.FromBoolean(false)});ai.SetDefinition(priority);second.SetDefinition(priority);ai.SetMemory("Personal",VariableValue.FromBoolean(true));Check(!second.Blackboard["Personal"].Boolean,"shared assets must not share mutable blackboard values");
        var signal=Node(BehaviorNodeKind.Signal);signal.Key="Once";var wait=Node(BehaviorNodeKind.Wait);wait.Duration=.3f;
        ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Sequence,Children=[signal,wait]}});long before=ai.SignalSequence;
        ai.Tick(.05f);ai.Tick(.05f);ai.Tick(.05f);Check(ai.SignalSequence==before+1,"sequence must not rerun finished signals while a later task runs");
        var waitMemory=Node(BehaviorNodeKind.WaitForMemory);waitMemory.Key="Done";waitMemory.Timeout=.2f;ai.SetDefinition(new(){Root=waitMemory});ai.Tick(.05f);ai.Tick(.3f);Check(ai.Status==BehaviorStatus.Failure,"async task timeout must fail");
        ai.SetDefinition(new(){Root=waitMemory});ai.SetMemory("Done",VariableValue.FromBoolean(true));ai.Tick(.05f);Check(ai.Status==BehaviorStatus.Success,"Event Sheet memory can complete a task");
        var coolSignal=Node(BehaviorNodeKind.Signal);coolSignal.Key="Pulse";ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.Cooldown,Duration=.5f,Children=[coolSignal]}});before=ai.SignalSequence;ai.Tick(.05f);ai.Tick(.1f);ai.Tick(.1f);Check(ai.SignalSequence==before+1,"cooldown must persist across completed evaluations");ai.Tick(.5f);Check(ai.SignalSequence==before+2,"cooldown must expire");
        var blocker=scene.CreateGameObject("Wall");blocker.Transform.WorldPosition=new(0,.8f,-.5f);blocker.AddComponent(new BoxCollider3D{Size=new(2,2,.1f)});
        ai.SetDefinition(new(){Root=new(){Kind=BehaviorNodeKind.DamageTarget,Distance=2,Amount=30}});ai.Tick(.05f);Check(ai.Status==BehaviorStatus.Failure&&health.CurrentHealth==100,"damage task must respect cover");blocker.Active=false;
        var nav=scene.CreateGameObject("Navigation");nav.Transform.WorldPosition=new(-5,0,-5);var region=nav.AddComponent(new NavigationRegion3D{Width=40,Height=40,CellSize=.25f});region.Bake();
        var agent=enemy.AddComponent(new NavigationAgent3D{RegionObject=nav.Id});target.Transform.WorldPosition=new(3,0,0);
        var move=Node(BehaviorNodeKind.MoveToTarget);move.Speed=2;move.Distance=.3f;move.Timeout=8;
        ai.SetDefinition(new(){Root=move});ai.Tick(.05f);Check(agent.Moving&&agent.ExternallyDriven,"AI must own its navigation task");
        for(int i=0;i<10;i++)ai.Tick(.05f);Check(enemy.Transform.WorldPosition.X>.1f,"navigation must move between decision updates");
        ai.SetPaused(true);Check(!agent.Moving&&!agent.ExternallyDriven,"pause must stop and release owned navigation");
        ai.SetPaused(false);enemy.AddComponent(new SimpleEnemyAI3D());ai.Tick(.1f);Check(ai.LastError.Contains("Disable Simple Enemy"),"conflicting AI drivers must be reported");
        var invalid=new BehaviorTreeDefinition();invalid.Root.Children.Add(invalid.Root);Check(invalid.Validate().Count>0,"cycles must be rejected without recursion overflow");
        var dup=new BehaviorTreeDefinition{Root=new(){Kind=BehaviorNodeKind.Sequence,Children=[new(){Id=signal.Id,Kind=BehaviorNodeKind.Wait},new(){Id=signal.Id,Kind=BehaviorNodeKind.Wait}]}};Check(dup.Validate().Count>0,"duplicate IDs must be rejected");
        enemy.GetComponent<SimpleEnemyAI3D>()!.Enabled=false;
        var registry=VisualLogicRegistry.CreateDefault();registry.TryGetCondition("ai.signal",out var condition);var instruction=new VisualInstruction();instruction.Arguments["name"]=new(){Constant=VariableValue.FromString("Once")};
        var context=new EventExecutionContext{Scene=scene,Self=enemy,Globals=new VariableStore()};ai.SetDefinition(new(){Root=signal});ai.Tick(.1f);
        Check(condition!.Evaluate(instruction,context)&&!condition.Evaluate(instruction,context),"signal observer must consume each pulse once per event");
        string root=Path.Combine(Path.GetTempPath(),"ByteAI-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"Assets"));Directory.CreateDirectory(Path.Combine(root,"Scenes"));
        string path=Path.Combine(root,"Assets","Test.byteai");BehaviorTreeSerializer.Save(path,template);var loaded=BehaviorTreeSerializer.Load(path);Check(loaded.Validate().Count==0&&loaded.Root.Id==template.Root.Id,"asset must roundtrip with stable IDs");
        using var db=new AssetDatabase(root,["Assets","Scenes"]);using var assets=new AssetManager(db);var codecs=new ComponentSerializer(root,db,assets);
        var component=new AIBehavior3D{Behavior=new AssetReference("Assets/Test.byteai"),TargetObjectId=target.Id,DecisionInterval=.2f};var data=codecs.Serialize(component)!;var clone=(AIBehavior3D)codecs.Deserialize(data)!;
        Check(clone.TargetObjectId==target.Id&&clone.DecisionInterval==.2f,"native component codec must preserve authored fields");
        target.Transform.WorldPosition=new(0,0,-1);
        scene.CreateGameObject("Loaded component").AddComponent(clone);clone.Tick(.3f);Check(clone.Definition!=null&&clone.LastError.Length==0,"deserialized component must resolve its native behaviour asset");
        Console.WriteLine("PASS AI behaviour: latent tasks, windup/recovery, guards, preemption, per-agent memory, signals, cooldown, cover, navigation ownership, conflicts, validation and native save/load");
    }
}
