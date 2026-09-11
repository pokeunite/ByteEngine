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

        Dictionary<Guid, VisualInstruction> conditionMap =
            rule.Conditions.ToDictionary(
                condition =>
                    condition.InstanceId);

        IReadOnlyList<Guid> rootConditionIds =
            GetActiveConditionIds(
                rule);

        Dictionary<Guid, bool> results =
            new();

        HashSet<Guid> evaluating =
            new();

        HashSet<Guid> evaluated =
            new();

        bool allPassed =
            true;

        foreach (Guid conditionId
                 in rootConditionIds)
        {
            bool passed =
                EvaluateConditionNode(
                    module,
                    rule,
                    conditionId,
                    conditionMap,
                    context,
                    results,
                    evaluating,
                    evaluated);

            if (!passed)
            {
                allPassed =
                    false;
            }
        }

        List<VisualInstruction> triggerOnceConditions =
            evaluated
                .Select(
                    id =>
                        conditionMap.TryGetValue(
                            id,
                            out VisualInstruction? condition)
                            ? condition
                            : null)
                .Where(
                    condition =>
                        condition != null &&
                        condition.Id.Equals(
                            "system.triggerOnce",
                            StringComparison.OrdinalIgnoreCase))
                .Cast<VisualInstruction>()
                .ToList();

        if (!allPassed)
        {
            /*
             * Any ordinary false condition resets Trigger Once, so the Event
             * can fire again after the complete expression becomes true.
             */
            ResetLatchTree(
                rule);

            foreach (VisualInstruction triggerOnce
                     in triggerOnceConditions)
            {
                VisualLogicDebugTrace.Mark(
                    module.Id,
                    triggerOnce.InstanceId,
                    VisualLogicTraceState.ConditionTrue);
            }

            VisualLogicDebugTrace.Mark(
                module.Id,
                rule.Id,
                VisualLogicTraceState.EventBlocked);

            MarkActionChainState(
                module,
                rule,
                VisualLogicTraceState.ActionSkipped);

            return false;
        }

        if (triggerOnceConditions.Count >
            0)
        {
            bool triggerAllowed =
                _triggerOnceLatched.Add(
                    rule.Id);

            foreach (VisualInstruction triggerOnce
                     in triggerOnceConditions)
            {
                VisualLogicDebugTrace.Mark(
                    module.Id,
                    triggerOnce.InstanceId,
                    triggerAllowed
                        ? VisualLogicTraceState.ConditionTrue
                        : VisualLogicTraceState.ConditionFalse);
            }

            if (!triggerAllowed)
            {
                VisualLogicDebugTrace.Mark(
                    module.Id,
                    rule.Id,
                    VisualLogicTraceState.EventBlocked);

                MarkActionChainState(
                    module,
                    rule,
                    VisualLogicTraceState.ActionSkipped);

                return false;
            }
        }

        VisualLogicDebugTrace.Mark(
            module.Id,
            rule.Id,
            VisualLogicTraceState.EventTriggered);

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

    private bool EvaluateConditionNode(
        EventModuleDefinition module,
        EventRuleDefinition rule,
        Guid conditionId,
        IReadOnlyDictionary<Guid, VisualInstruction> conditionMap,
        EventExecutionContext context,
        Dictionary<Guid, bool> results,
        HashSet<Guid> evaluating,
        HashSet<Guid> evaluated)
    {
        if (results.TryGetValue(
                conditionId,
                out bool cached))
        {
            return cached;
        }

        if (!conditionMap.TryGetValue(
                conditionId,
                out VisualInstruction? condition) ||
            condition ==
                null)
        {
            WarnOnce(
                $"{module.Id}:{rule.Id}:missing-condition:{conditionId}",
                $"Condition flow in '{module.Name}' points to a missing Condition.",
                context);

            return false;
        }

        evaluated.Add(
            conditionId);

        if (!evaluating.Add(
                conditionId))
        {
            WarnOnce(
                $"{module.Id}:{rule.Id}:condition-cycle",
                $"Condition flow in '{module.Name}' contains a cycle. The cycle was treated as false.",
                context);

            VisualLogicDebugTrace.Mark(
                module.Id,
                condition.InstanceId,
                VisualLogicTraceState.ConditionFalse);

            results[conditionId] =
                false;

            return false;
        }

        bool passed;

        if (condition.Id.Equals(
                "logic.and",
                StringComparison.OrdinalIgnoreCase))
        {
            passed =
                condition.ConditionInputIds.Count >
                0;

            foreach (Guid inputId
                     in condition.ConditionInputIds)
            {
                bool inputPassed =
                    EvaluateConditionNode(
                        module,
                        rule,
                        inputId,
                        conditionMap,
                        context,
                        results,
                        evaluating,
                        evaluated);

                if (!inputPassed)
                {
                    passed =
                        false;
                }
            }
        }
        else if (condition.Id.Equals(
                     "logic.or",
                     StringComparison.OrdinalIgnoreCase))
        {
            passed =
                false;

            foreach (Guid inputId
                     in condition.ConditionInputIds)
            {
                bool inputPassed =
                    EvaluateConditionNode(
                        module,
                        rule,
                        inputId,
                        conditionMap,
                        context,
                        results,
                        evaluating,
                        evaluated);

                if (inputPassed)
                {
                    passed =
                        true;
                }
            }
        }
        else if (condition.Id.Equals(
                     "system.triggerOnce",
                     StringComparison.OrdinalIgnoreCase))
        {
            /*
             * Trigger Once is applied after the complete Condition graph
             * passes. Treat it as logically true while evaluating the graph.
             */
            passed =
                true;
        }
        else if (TryEvaluateMouseCondition(
                     condition,
                     context,
                     out bool mousePassed))
        {
            passed =
                mousePassed;
        }
        else if (!_registry.TryGetCondition(
                     condition.Id,
                     out VisualConditionDefinition? definition) ||
                 definition ==
                     null)
        {
            WarnOnce(
                $"{module.Id}:{condition.InstanceId}",
                $"Unknown visual condition '{condition.Id}' in '{module.Name}'.",
                context);

            passed =
                false;
        }
        else
        {
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
        }

        evaluating.Remove(
            conditionId);

        results[conditionId] =
            passed;

        if (!condition.Id.Equals(
                "system.triggerOnce",
                StringComparison.OrdinalIgnoreCase))
        {
            VisualLogicDebugTrace.Mark(
                module.Id,
                condition.InstanceId,
                passed
                    ? VisualLogicTraceState.ConditionTrue
                    : VisualLogicTraceState.ConditionFalse);
        }

        return passed;
    }

    private static IReadOnlyList<Guid> GetActiveConditionIds(
        EventRuleDefinition rule)
    {
        if (!rule.HasExplicitConditionFlow)
        {
            return rule.Conditions
                .Select(
                    condition =>
                        condition.InstanceId)
                .ToList();
        }

        return rule.ConnectedConditionIds;
    }

    private void ExecuteActions(
        EventModuleDefinition module,
        EventRuleDefinition rule,
        EventExecutionContext context)
    {
        /*
         * Compatibility path for Event Modules authored before explicit
         * ByteGraph execution wires existed.
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

        ExecuteActionFlow(
            module,
            rule,
            rule.FirstActionId,
            actions,
            context,
            visited);
    }

    private void ExecuteActionFlow(
        EventModuleDefinition module,
        EventRuleDefinition rule,
        Guid? startActionId,
        IReadOnlyDictionary<Guid, VisualInstruction> actions,
        EventExecutionContext context,
        HashSet<Guid> visited)
    {
        Guid? current =
            startActionId;

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

            if (action.Id.Equals(
                    "flow.branch",
                    StringComparison.OrdinalIgnoreCase))
            {
                VisualLogicDebugTrace.Mark(
                    module.Id,
                    action.InstanceId,
                    VisualLogicTraceState.ActionExecuted);

                bool branchResult =
                    EventValueResolver.GetBoolean(
                        action,
                        "condition",
                        context,
                        false);

                current =
                    branchResult
                        ? action.TrueActionId
                        : action.FalseActionId;

                continue;
            }

            ExecuteAction(
                module,
                action,
                context);

            current =
                action.NextActionId;
        }
    }

    private static void MarkActionChainState(
        EventModuleDefinition module,
        EventRuleDefinition rule,
        VisualLogicTraceState state)
    {
        if (!VisualLogicDebugTrace.Enabled)
        {
            return;
        }

        if (!rule.HasExplicitExecutionFlow)
        {
            foreach (VisualInstruction action
                     in rule.Actions)
            {
                VisualLogicDebugTrace.Mark(
                    module.Id,
                    action.InstanceId,
                    state);
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

        Stack<Guid> pending =
            new();

        pending.Push(
            rule.FirstActionId.Value);

        while (pending.Count >
               0)
        {
            Guid actionId =
                pending.Pop();

            if (!visited.Add(
                    actionId))
            {
                continue;
            }

            if (!actions.TryGetValue(
                    actionId,
                    out VisualInstruction? action) ||
                action ==
                    null)
            {
                continue;
            }

            VisualLogicDebugTrace.Mark(
                module.Id,
                action.InstanceId,
                state);

            if (action.Id.Equals(
                    "flow.branch",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (action.TrueActionId.HasValue)
                {
                    pending.Push(
                        action.TrueActionId.Value);
                }

                if (action.FalseActionId.HasValue)
                {
                    pending.Push(
                        action.FalseActionId.Value);
                }

                continue;
            }

            if (action.NextActionId.HasValue)
            {
                pending.Push(
                    action.NextActionId.Value);
            }
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
         * Orange means this Action actually executed.
         */
        VisualLogicDebugTrace.Mark(
            module.Id,
            action.InstanceId,
            VisualLogicTraceState.ActionExecuted);

        try
        {
            definition.Execute(
                action,
                context);
        }
        catch (Exception exception)
        {
            VisualLogicDebugTrace.Mark(
                module.Id,
                action.InstanceId,
                VisualLogicTraceState.ActionFailed);

            WarnOnce(
                $"{module.Id}:{action.InstanceId}:exception",
                $"Action '{definition.DisplayName}' failed: {exception.Message}",
                context);
        }
    }

    private static bool TryEvaluateMouseCondition(
        VisualInstruction condition,
        EventExecutionContext context,
        out bool passed)
    {
        passed =
            false;

        bool isMouseCondition =
            condition.Id.Equals(
                "input.mouseHeld",
                StringComparison.OrdinalIgnoreCase) ||
            condition.Id.Equals(
                "input.mousePressed",
                StringComparison.OrdinalIgnoreCase) ||
            condition.Id.Equals(
                "input.mouseReleased",
                StringComparison.OrdinalIgnoreCase);

        if (!isMouseCondition)
        {
            return false;
        }

        string value =
            EventValueResolver.GetString(
                condition,
                "button",
                context,
                MouseButton.Left.ToString());

        if (!Enum.TryParse(
                value,
                true,
                out MouseButton button))
        {
            button =
                MouseButton.Left;
        }

        passed =
            condition.Id.Equals(
                "input.mouseHeld",
                StringComparison.OrdinalIgnoreCase)
                ? Input.IsMouseButtonDown(
                    button)
                : condition.Id.Equals(
                    "input.mousePressed",
                    StringComparison.OrdinalIgnoreCase)
                    ? Input.IsMouseButtonPressed(
                        button)
                    : Input.IsMouseButtonReleased(
                        button);

        return true;
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
