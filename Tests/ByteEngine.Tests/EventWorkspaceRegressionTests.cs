using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Tests;

internal static class EventWorkspaceRegressionTests
{
    internal static void Run()
    {
        var registry = VisualLogicRegistry.CreateDefault();
        int count = 0;
        registry.RegisterAction(new VisualActionDefinition {
            Id = "test.workspaceCount", Category = "Test", DisplayName = "Count",
            Execute = (_, _) => count++ });
        var module = new EventModuleDefinition();
        EventRuleDefinition Rule()
        {
            var rule = new EventRuleDefinition();
            rule.Conditions.Add(new VisualInstruction { Id = "system.always" });
            rule.Actions.Add(new VisualInstruction { Id = "test.workspaceCount" });
            return rule;
        }
        var parent = Rule(); var child = Rule(); var sibling = Rule();
        parent.SubEvents.Add(child); module.Rules.Add(parent); module.Rules.Add(sibling);
        var runtime = new EventModuleRuntime(registry);
        var scene = new Scene("Event scratch regression"); var owner = scene.CreateGameObject(); var globals = new VariableStore();
        void Step() { Time.Update(.016); runtime.Update(module, globals, scene, owner); }
        Step(); Step();
        if (count != 6) throw new Exception("Nested/sibling rules shared evaluation scratch");
        child.Actions.Clear(); Step();
        if (count != 8) throw new Exception("Pooled action map ignored a live edit");
        child.Conditions.Clear(); child.Conditions.Add(new VisualInstruction { Id = "system.never" });
        child.Actions.Add(new VisualInstruction { Id = "test.workspaceCount" });
        // Unknown conditions are false; a changed condition map must not reuse its previous true result.
        Step(); if (count != 10) throw new Exception("Pooled condition result survived a live edit");
        parent.Enabled = false; Step();
        if (count != 11) throw new Exception("Disabled parent's scratch contaminated its sibling");
        runtime.Reset(); parent.Enabled = true; Step();
        if (count != 13) throw new Exception("Reset broke subsequent event evaluation");
        Console.WriteLine("PASS Event evaluation scratch: nested ownership, live edits, disabled rules and reset");
    }
}
