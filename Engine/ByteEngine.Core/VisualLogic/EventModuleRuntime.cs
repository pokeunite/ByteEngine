using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
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

    private readonly Dictionary<AnimationController, AnimationSubscription> _animationSubscriptions =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<AnimationController> _observedAnimationControllers =
        new(ReferenceEqualityComparer.Instance);

    private EventModuleDefinition? _activeModule;
    private VariableStore? _activeGlobals;
    private ByteEngine.Core.Scene.Scene? _activeScene;
    private GameObject? _activeSelf;
    private Action<string>? _activeWarningSink;

    private sealed record AnimationSubscription(
        Action<AnimationEventOccurrence> EventFired,
        Action<AnimationWindowOccurrence> WindowEntered,
        Action<AnimationWindowOccurrence> WindowExited);
    public EventModuleRuntime(
        VisualLogicRegistry? registry = null)
    {
        _registry =
            registry ??
            VisualLogicRegistry.CreateDefault();
    }

    public void Reset()
    {
        foreach ((AnimationController controller, AnimationSubscription subscription) in _animationSubscriptions)
        {
            controller.AnimationEventFired -= subscription.EventFired;
            controller.WindowEntered -= subscription.WindowEntered;
            controller.WindowExited -= subscription.WindowExited;
        }

        _animationSubscriptions.Clear();
        _observedAnimationControllers.Clear();
        _triggerOnceLatched.Clear();
        _reportedWarnings.Clear();
        _activeModule = null;
        _activeGlobals = null;
        _activeScene = null;
        _activeSelf = null;
        _activeWarningSink = null;
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

        _activeModule = module;
        _activeGlobals = globals;
        _activeScene = scene;
        _activeSelf = self;
        _activeWarningSink = warningSink;
        _observedAnimationControllers.Clear();
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
                    warningSink,

                ObserveAnimationController =
                    controller => _observedAnimationControllers.Add(controller)
            };

        foreach (EventRuleDefinition rule
                 in module.Rules)
        {
            EvaluateRule(
                module,
                rule,
                context);
        }

        ReconcileAnimationSubscriptions();    }

    private void ReconcileAnimationSubscriptions()
    {
        foreach (AnimationController controller in _animationSubscriptions.Keys
                     .Where(controller => !_observedAnimationControllers.Contains(controller)).ToArray())
        {
            AnimationSubscription subscription = _animationSubscriptions[controller];
            controller.AnimationEventFired -= subscription.EventFired;
            controller.WindowEntered -= subscription.WindowEntered;
            controller.WindowExited -= subscription.WindowExited;
            _animationSubscriptions.Remove(controller);
        }

        foreach (AnimationController controller in _observedAnimationControllers)
        {
            if (_animationSubscriptions.ContainsKey(controller)) continue;
            Action<AnimationEventOccurrence> eventFired = occurrence =>
                DispatchAnimationSignal(AnimationSignalKind.EventFired, controller, occurrence, null);
            Action<AnimationWindowOccurrence> windowEntered = occurrence =>
                DispatchAnimationSignal(AnimationSignalKind.WindowEntered, controller, null, occurrence);
            Action<AnimationWindowOccurrence> windowExited = occurrence =>
                DispatchAnimationSignal(AnimationSignalKind.WindowExited, controller, null, occurrence);
            controller.AnimationEventFired += eventFired;
            controller.WindowEntered += windowEntered;
            controller.WindowExited += windowExited;
            _animationSubscriptions.Add(controller,
                new AnimationSubscription(eventFired, windowEntered, windowExited));
        }
    }

    private void DispatchAnimationSignal(
        AnimationSignalKind kind,
        AnimationController source,
        AnimationEventOccurrence? animationEvent,
        AnimationWindowOccurrence? animationWindow)
    {
        if (_activeModule == null || _activeGlobals == null || _activeScene == null || _activeSelf == null) return;
        string conditionId = kind switch
        {
            AnimationSignalKind.EventFired => "animation.eventFired",
            AnimationSignalKind.WindowEntered => "animation.windowEntered",
            AnimationSignalKind.WindowExited => "animation.windowExited",
            _ => string.Empty
        };
        if (conditionId.Length == 0) return;

        var context = new EventExecutionContext
        {
            Globals = _activeGlobals,
            Scene = _activeScene,
            Self = _activeSelf,
            WarningSink = _activeWarningSink,
            AnimationSignalKind = kind,
            AnimationSignalSource = source,
            AnimationEvent = animationEvent,
            AnimationWindow = animationWindow
        };
        foreach (EventRuleDefinition rule in _activeModule.Rules)
        {
            if (RuleContainsActiveCondition(rule, conditionId)) EvaluateRule(_activeModule, rule, context);
        }
    }

    private static bool RuleContainsActiveCondition(EventRuleDefinition rule, string conditionId)
    {
        Dictionary<Guid, VisualInstruction> map = rule.Conditions.ToDictionary(condition => condition.InstanceId);
        HashSet<Guid> visited = new();
        return GetActiveConditionIds(rule).Any(root => ContainsCondition(root, conditionId, map, visited));
    }

    private static bool ContainsCondition(
        Guid id,
        string conditionId,
        IReadOnlyDictionary<Guid, VisualInstruction> map,
        HashSet<Guid> visited)
    {
        if (!visited.Add(id) || !map.TryGetValue(id, out VisualInstruction? condition)) return false;
        if (condition.Id.Equals(conditionId, StringComparison.OrdinalIgnoreCase)) return true;
        return condition.ConditionInputIds.Any(inputId => ContainsCondition(inputId, conditionId, map, visited));
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

        /*
         * Once a rule has explicit ByteGraph condition wiring, zero root
         * Conditions means "nothing is connected to the Event" and therefore
         * the Event must NOT fire.
         *
         * Legacy rules without explicit wiring keep their previous behavior
         * for backwards compatibility.
         */
        bool allPassed =
            !rule.HasExplicitConditionFlow ||
            rootConditionIds.Count >
                0;

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
                context.AnimationSignalKind != AnimationSignalKind.None ||
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
        /*
         * Orange means execution reached this Action node.
         */
        VisualLogicDebugTrace.Mark(
            module.Id,
            action.InstanceId,
            VisualLogicTraceState.ActionExecuted);

        try
        {
            if (TryExecuteSpecialAction(
                    module,
                    action,
                    context,
                    out bool specialSucceeded))
            {
                if (!specialSucceeded)
                {
                    VisualLogicDebugTrace.Mark(
                        module.Id,
                        action.InstanceId,
                        VisualLogicTraceState.ActionFailed);
                }

                return;
            }

            if (!_registry.TryGetAction(
                    action.Id,
                    out VisualActionDefinition? definition) ||
                definition ==
                    null)
            {
                VisualLogicDebugTrace.Mark(
                    module.Id,
                    action.InstanceId,
                    VisualLogicTraceState.ActionFailed);

                WarnOnce(
                    $"{module.Id}:{action.InstanceId}",
                    $"Unknown visual action '{action.Id}' in '{module.Name}'.",
                    context);

                return;
            }

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
                $"Action '{GetActionDisplayName(action)}' failed: {exception.Message}",
                context);
        }
    }

    private bool TryExecuteSpecialAction(
        EventModuleDefinition module,
        VisualInstruction action,
        EventExecutionContext context,
        out bool succeeded)
    {
        succeeded =
            true;

        if (action.Id.Equals(
                "object.spawnEmpty",
                StringComparison.OrdinalIgnoreCase))
        {
            string name =
                EventValueResolver.GetString(
                    action,
                    "name",
                    context,
                    "GameObject");

            if (string.IsNullOrWhiteSpace(
                    name))
            {
                name =
                    "GameObject";
            }

            Vector3 position =
                EventValueResolver.GetVector3(
                    action,
                    "position",
                    context);

            GameObject created =
                context.Scene.CreateGameObject(
                    name);

            created.Transform.WorldPosition =
                position;

            return true;
        }

        if (!action.Id.Equals(
                "object.spawnBlueprint",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string token =
            EventValueResolver.GetString(
                action,
                "blueprint",
                context,
                string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(
                token))
        {
            succeeded =
                false;

            WarnOnce(
                $"{module.Id}:{action.InstanceId}:missing-blueprint",
                "Spawn Blueprint requires a Blueprint asset.",
                context);

            return true;
        }

        AssetReference reference;

        if (Guid.TryParse(
                token,
                out Guid blueprintGuid))
        {
            reference =
                new AssetReference(
                    blueprintGuid);
        }
        else
        {
            try
            {
                reference =
                    new AssetReference(
                        token);
            }
            catch (ArgumentException)
            {
                succeeded =
                    false;

                WarnOnce(
                    $"{module.Id}:{action.InstanceId}:invalid-blueprint",
                    $"Spawn Blueprint asset reference '{token}' is invalid.",
                    context);

                return true;
            }
        }

        if (!RuntimeSpawnService.IsBlueprintSpawnerConfigured)
        {
            succeeded =
                false;

            WarnOnce(
                $"{module.Id}:{action.InstanceId}:spawn-service",
                "Spawn Blueprint runtime service is not configured.",
                context);

            return true;
        }

        Vector3 worldPosition =
            EventValueResolver.GetVector3(
                action,
                "position",
                context);

        GameObject? spawned =
            RuntimeSpawnService.SpawnBlueprint(
                context.Scene,
                reference,
                worldPosition);

        if (spawned ==
            null)
        {
            succeeded =
                false;

            WarnOnce(
                $"{module.Id}:{action.InstanceId}:spawn-failed",
                $"Spawn Blueprint could not instantiate '{token}'.",
                context);
        }

        return true;
    }

    private static string GetActionDisplayName(
        VisualInstruction action)
    {
        if (action.Id.Equals(
                "object.spawnEmpty",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Spawn Empty Object";
        }

        if (action.Id.Equals(
                "object.spawnBlueprint",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Spawn Blueprint";
        }

        return action.Id;
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
