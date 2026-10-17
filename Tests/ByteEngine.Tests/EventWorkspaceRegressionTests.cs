using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Tests;
internal static class EventWorkspaceRegressionTests
{
    private static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    public static void Run()
    {
        bool trace=VisualLogicDebugTrace.Enabled;VisualLogicDebugTrace.Enabled=false;
        try {
            var registry=VisualLogicRegistry.CreateDefault();var scene=new Scene("Event workspaces");var self=scene.CreateGameObject("Owner");var globals=new VariableStore();
            var module=new EventModuleDefinition();var runtime=new EventModuleRuntime(registry);bool nested=false;int count=0;
            registry.RegisterAction(new(){Id="test.workspaceCount",Category="Test",DisplayName="Count",Execute=(_,_)=>count++});
            registry.RegisterCondition(new(){Id="test.workspaceReentrant",Category="Test",DisplayName="Reenter",Evaluate=(_,_)=>{if(!nested){nested=true;try{runtime.Update(module,globals,scene,self);}finally{nested=false;}}return true;}});
            var condition=new VisualInstruction{Id="test.workspaceReentrant"};var action=new VisualInstruction{Id="test.workspaceCount"};
            var rule=new EventRuleDefinition{Conditions=[condition],Actions=[action]};module.Rules.Add(rule);
            runtime.Update(module,globals,scene,self);Check(count==2,"Reentrant evaluation leases independent condition state");
            rule.HasExplicitConditionFlow=true;rule.ConnectedConditionIds.Clear();runtime.Update(module,globals,scene,self);Check(count==2,"Changing live condition wiring is observed");
            condition.Id="system.always";rule.ConnectedConditionIds.Add(condition.InstanceId);var once=new VisualInstruction{Id="system.triggerOnce"};rule.Conditions.Add(once);rule.ConnectedConditionIds.Add(once.InstanceId);
            runtime.Update(module,globals,scene,self);runtime.Update(module,globals,scene,self);Check(count==3,"Trigger-once latches survive workspace reuse");
            rule.Enabled=false;runtime.Update(module,globals,scene,self);rule.Enabled=true;runtime.Update(module,globals,scene,self);Check(count==4,"Disabled rules reset latches without retaining previous results");
            rule.Conditions.Remove(once);rule.ConnectedConditionIds.Remove(once.InstanceId);rule.SubEvents.Add(new(){Conditions=[new(){Id="system.always"}],Actions=[new(){Id="test.workspaceCount"}]});
            runtime.Update(module,globals,scene,self);Check(count==6,"Nested rules keep parent workspace alive");runtime.Reset();runtime.Update(module,globals,scene,self);Check(count==8,"Reset releases workspaces and preserves live rule definitions");
            var many=new EventModuleDefinition();for(int i=0;i<64;i++)many.Rules.Add(new(){Conditions=[new(){Id="system.always"}],Actions=[new(){Id="test.workspaceCount"}]});
            for(int i=0;i<5;i++)runtime.Update(many,globals,scene,self);long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<100;i++)runtime.Update(many,globals,scene,self);long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(allocated<128*1024,"64-rule event evaluation avoids per-rule collection allocation ("+allocated+" bytes / 100 updates)");
            EventModuleTimerTests.Run();
        } finally {VisualLogicDebugTrace.Enabled=trace;}
    }
}
