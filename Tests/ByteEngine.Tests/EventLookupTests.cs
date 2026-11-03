using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
namespace ByteEngine.Tests;
internal static class EventLookupTests
{
    public static void Run()
    {
        var registry=new VisualLogicRegistry();int fired=0;bool allowed=true;
        registry.RegisterCondition(new(){Id="test.allow",Category="Test",DisplayName="Allow",Evaluate=(_,_)=>allowed});
        registry.RegisterAction(new(){Id="test.fire",Category="Test",DisplayName="Fire",Execute=(_,_)=>fired++});
        var scene=new Scene("Event lookup tests");var actor=scene.CreateGameObject("Actor");var globals=new VariableStore();
        var condition=new VisualInstruction{Id="test.allow"};var action=new VisualInstruction{Id="test.fire"};
        var rule=new EventRuleDefinition{Conditions=[condition],Actions=[action],HasExplicitConditionFlow=true,ConnectedConditionIds=[condition.InstanceId],HasExplicitExecutionFlow=true,FirstActionId=action.InstanceId};
        var module=new EventModuleDefinition{Rules=[rule]};var runtime=new EventModuleRuntime(registry);
        void Step(int expected){runtime.Update(module,globals,scene,actor);if(fired!=expected)throw new Exception($"Live event lookup: expected {expected}, got {fired}");}
        Step(1);Step(2);allowed=false;Step(2);allowed=true;
        var replacement=new VisualInstruction{Id="test.allow",InstanceId=condition.InstanceId};rule.Conditions[0]=replacement;Step(3);
        replacement.Id="missing";Step(3);replacement.Id="test.allow";Step(4);
        replacement.InstanceId=Guid.NewGuid();Step(4);rule.ConnectedConditionIds[0]=replacement.InstanceId;Step(5);
        action.InstanceId=Guid.NewGuid();Step(5);rule.FirstActionId=action.InstanceId;Step(6);
        rule.Actions=[new VisualInstruction{Id="test.fire",InstanceId=action.InstanceId}];Step(7);
        rule.Conditions.Clear();Step(7);rule.HasExplicitConditionFlow=false;Step(8);
        rule.Actions.Clear();Step(8);rule.Actions.Add(action);Step(9);
        runtime.Reset();Step(10);
        EventModuleTimerTests.Run();
        Console.WriteLine("PASS event lookups: live object replacement, IDs, arguments/registry IDs, list replacement/removal, explicit/legacy flow, reset and timer regressions");
    }
}
