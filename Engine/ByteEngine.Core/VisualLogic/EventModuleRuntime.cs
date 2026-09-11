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
            ResetLatchTree(rule);

            return false;
        }

        bool triggerOnce =
            rule.Conditions.Any(
                condition =>
                    condition.Id.Equals(
                        "system.triggerOnce",
                        StringComparison.OrdinalIgnoreCase));

        foreach (VisualInstruction condition
                 in rule.Conditions)
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

                ResetLatchTree(rule);

                return false;
            }

            if (definition == null)
            {
                ResetLatchTree(rule);

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
                ResetLatchTree(rule);

                return false;
            }
        }

        if (triggerOnce)
        {
            if (!_triggerOnceLatched.Add(
                    rule.Id))
            {
                return false;
            }
        }

        foreach (VisualInstruction action
                 in rule.Actions)
        {
            if (!_registry.TryGetAction(
                    action.Id,
                    out VisualActionDefinition? definition))
            {
                WarnOnce(
                    $"{module.Id}:{action.InstanceId}",
                    $"Unknown visual action '{action.Id}' in '{module.Name}'.",
                    context);

                continue;
            }

            if (definition == null)
            {
                continue;
            }

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
        if (_reportedWarnings.Add(key))
        {
            context.WarningSink?.Invoke(
                message);
        }
    }
}