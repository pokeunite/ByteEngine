using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public sealed class EventModuleRuntime
{
    private readonly VisualLogicRegistry _registry;

    private readonly HashSet<Guid> _triggerOnceLatched =
        new();

    private readonly HashSet<string> _reportedWarnings =
        new(StringComparer.OrdinalIgnoreCase);

    public EventModuleRuntime(
        VisualLogicRegistry? registry = null)
    {
        _registry =
            registry ??
            VisualLogicRegistry.CreateDefault();
    }

    public void Reset()
    {
        _triggerOnceLatched.Clear();
        _reportedWarnings.Clear();
    }

    public void Update(
        EventModuleDefinition module,
        VariableStore globals,
        ByteEngine.Core.Scene.Scene scene,
        GameObject self,
        Action<string>? warningSink = null)
    {
        ArgumentNullException.ThrowIfNull(
            module);

        ArgumentNullException.ThrowIfNull(
            globals);

        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            self);

        var context =
            new EventExecutionContext
            {
                Globals =
                    globals,

                Scene =
                    scene,

                Self =
                    self,

                WarningSink =
                    warningSink
            };

        foreach (EventRuleDefinition rule
                 in module.Rules)
        {
            EvaluateRule(
                module,
                rule,
                context);
        }
    }

    private bool EvaluateRule(
        EventModuleDefinition module,
        EventRuleDefinition rule,
        EventExecutionContext context)
    {
        if (!rule.Enabled)
        {
            ResetLatchTree(
                rule);

            return false;
        }

        IReadOnlyList<VisualInstruction> activeConditions =
            GetActiveConditions(
                rule);

        bool triggerOnce =
            activeConditions.Any(
                condition =>
                    condition.Id.Equals(
                        "system.triggerOnce",
                        StringComparison.OrdinalIgnoreCase));

        foreach (VisualInstruction condition
                 in activeConditions)
        {
            /*
             * Trigger Once is handled at rule level.
             */
            if (condition.Id.Equals(
                    "system.triggerOnce",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!_registry.TryGetCondition(
                    condition.Id,
                    out VisualConditionDefinition? definition))
            {
                WarnOnce(
                    $"{module.Id}:{condition.InstanceId}",
                    $"Unknown visual condition '{condition.Id}' in '{module.Name}'.",
                    context);

                ResetLatchTree(
                    rule);

                return false;
            }

            if (definition ==
                null)
            {
                ResetLatchTree(
                    rule);

                return false;
            }

            bool passed;

            try
            {
                passed =
                    definition.Evaluate(
                        condition,
                        context);
            }
            catch (Exception exception)
            {
                WarnOnce(
                    $"{module.Id}:{condition.InstanceId}:exception",
                    $"Condition '{definition.DisplayName}' failed: {exception.Message}",
                    context);

                passed =
                    false;
            }

            if (!passed)
            {
                ResetLatchTree(
                    rule);

                return false;
            }

            /*
             * A Condition lights while it is actually true.
             */
            VisualLogicDebugTrace.Mark(
                module.Id,
                condition.InstanceId);
        }

        if (triggerOnce)
        {
            if (!_triggerOnceLatched.Add(
                    rule.Id))
            {
                return false;
            }

            foreach (VisualInstruction condition
                     in activeConditions.Where(
                         condition =>
                             condition.Id.Equals(
                                 "system.triggerOnce",
                                 StringComparison.OrdinalIgnoreCase)))
            {
                VisualLogicDebugTrace.Mark(
                    module.Id,
                    condition.InstanceId);
            }
        }

        /*
         * The Event card itself lights only when the complete rule fires.
         */
        VisualLogicDebugTrace.Mark(
            module.Id,
            rule.Id);

        ExecuteActions(
            module,
            rule,
            context);

        foreach (EventRuleDefinition child
                 in rule.SubEvents)
        {
            EvaluateRule(
                module,
                child,
                context);
        }

        return true;
    }

    private static IReadOnlyList<VisualInstruction> GetActiveConditions(
        EventRuleDefinition rule)
    {
        if (!rule.HasExplicitConditionFlow)
        {
            return rule.Conditions;
        }

        if (rule.ConnectedConditionIds.Count ==
            0)
        {
            return Array.Empty<VisualInstruction>();
        }

        HashSet<Guid> connected =
            rule.ConnectedConditionIds.ToHashSet();

        return rule.Conditions
            .Where(
                condition =>
                    connected.Contains(
                        condition.InstanceId))
            .ToList();
    }

    private void ExecuteActions(
        EventModuleDefinition module,
        EventRuleDefinition rule,
        EventExecutionContext context)
    {
        /*
         * Compatibility path for Event Modules authored before ByteGraph
         * execution wires existed.
         */
        if (!rule.HasExplicitExecutionFlow)
        {
            foreach (VisualInstruction action
                     in rule.Actions)
            {
                ExecuteAction(
                    module,
                    action,
                    context);
            }

            return;
        }

        if (!rule.FirstActionId.HasValue)
        {
            return;
        }

        Dictionary<Guid, VisualInstruction> actions =
            rule.Actions.ToDictionary(
                action =>
                    action.InstanceId);

        HashSet<Guid> visited =
            new();

        Guid? current =
            rule.FirstActionId;

        while (current.HasValue)
        {
            Guid actionId =
                current.Value;

            if (!visited.Add(
                    actionId))
            {
                WarnOnce(
                    $"{module.Id}:{rule.Id}:execution-cycle",
                    $"Execution flow in '{module.Name}' contains a cycle. Execution stopped.",
                    context);

                return;
            }

            if (!actions.TryGetValue(
                    actionId,
                    out VisualInstruction? action) ||
                action ==
                    null)
            {
                WarnOnce(
                    $"{module.Id}:{rule.Id}:missing-action:{actionId}",
                    $"Execution flow in '{module.Name}' points to a missing Action.",
                    context);

                return;
            }

            ExecuteAction(
                module,
                action,
                context);

            current =
                action.NextActionId;
        }
    }

    private void ExecuteAction(
        EventModuleDefinition module,
        VisualInstruction action,
        EventExecutionContext context)
    {
        if (!_registry.TryGetAction(
                action.Id,
                out VisualActionDefinition? definition))
        {
            WarnOnce(
                $"{module.Id}:{action.InstanceId}",
                $"Unknown visual action '{action.Id}' in '{module.Name}'.",
                context);

            return;
        }

        if (definition ==
            null)
        {
            return;
        }

        /*
         * Mark before execution so a node still lights if the Action itself
         * throws and the warning is shown to the developer.
         */
        VisualLogicDebugTrace.Mark(
            module.Id,
            action.InstanceId);

        try
        {
            definition.Execute(
                action,
                context);
        }
        catch (Exception exception)
        {
            WarnOnce(
                $"{module.Id}:{action.InstanceId}:exception",
                $"Action '{definition.DisplayName}' failed: {exception.Message}",
                context);
        }
    }

    private void ResetLatchTree(
        EventRuleDefinition rule)
    {
        _triggerOnceLatched.Remove(
            rule.Id);

        foreach (EventRuleDefinition child
                 in rule.SubEvents)
        {
            ResetLatchTree(
                child);
        }
    }

    private void WarnOnce(
        string key,
        string message,
        EventExecutionContext context)
    {
        if (_reportedWarnings.Add(
                key))
        {
            context.WarningSink?.Invoke(
                message);
        }
    }
}
