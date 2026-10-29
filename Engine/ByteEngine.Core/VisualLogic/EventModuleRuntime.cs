using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class EventModuleRuntime
{
    private readonly VisualLogicRegistry _registry;
    // Each recursive evaluation owns its scratch data until it returns. Rebuilding
    // the maps still observes live sheet edits; pooling only removes allocations.
    private readonly Stack<RuleScratch> _scratchPool = new();
    private sealed class RuleScratch
    {
        public readonly Dictionary<Guid, VisualInstruction> Conditions = new(), Actions = new();
        public readonly Dictionary<Guid, bool> Results = new();
        public readonly HashSet<Guid> Evaluating = new(), Evaluated = new(), VisitedActions = new();
        public readonly List<Guid> Roots = new();
        public readonly List<VisualInstruction> Triggers = new();
        public void Clear()
        {
            Conditions.Clear(); Actions.Clear(); Results.Clear(); Evaluating.Clear();
            Evaluated.Clear(); VisitedActions.Clear(); Roots.Clear(); Triggers.Clear();
        }
    }

    private readonly HashSet<Guid> _triggerOnceLatched =
        new();

    private readonly Dictionary<string, TimerState> _timers =
        new(StringComparer.OrdinalIgnoreCase);

    private sealed class TimerState
    {
        public double Remaining;
        public double Elapsed;
        public bool Paused;
        public bool Running;
        public bool FinishedThisUpdate;
    }

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
        _timers.Clear();
        _continuations.Clear();
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
        AdvanceTimers(Time.DeltaTime);
        AdvanceContinuations(Time.DeltaTime);
        var context =
            new EventExecutionContext
            {
                TimerRuntime = this,
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
            if (!string.IsNullOrWhiteSpace(rule.FunctionName)) continue;
            context.PickedSets.Clear();context.PickedObject=null;
            EvaluateRule(
                module,
                rule,
                context);
        }

        ReconcileAnimationSubscriptions();    }

    internal bool StartTimer(string name, double duration)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0 || !double.IsFinite(duration)) return false;
        // A re-arm during a finish rule must not hide the pulse from later rules this update.
        bool finished = _timers.TryGetValue(name, out TimerState? previous) && previous.FinishedThisUpdate;
        _timers[name] = new TimerState
        {
            Remaining = Math.Max(0, duration),
            Running = true,
            FinishedThisUpdate = finished
        };
        return true;
    }

    internal void StopTimer(string name)
    {
        if (!string.IsNullOrWhiteSpace(name)) _timers.Remove(name.Trim());
    }

    internal bool IsTimerRunning(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        _timers.TryGetValue(name.Trim(), out TimerState? timer) && timer.Running && !timer.Paused;

    internal bool IsTimerFinished(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        _timers.TryGetValue(name.Trim(), out TimerState? timer) && timer.FinishedThisUpdate;

    internal void PauseTimer(string name,bool paused){if(_timers.TryGetValue(name,out var timer))timer.Paused=paused;}
    internal bool TimerPaused(string name)=>_timers.TryGetValue(name,out var timer)&&timer.Paused;
    internal double TimerElapsed(string name)=>_timers.TryGetValue(name,out var timer)?timer.Elapsed:0;

    private void AdvanceTimers(double deltaTime)
    {
        double elapsed = double.IsFinite(deltaTime) ? Math.Max(0, deltaTime) : 0;
        foreach (TimerState timer in _timers.Values)
        {
            timer.FinishedThisUpdate = false;
            if (!timer.Running || timer.Paused) continue;
            timer.Elapsed += Math.Min(elapsed,timer.Remaining);
            timer.Remaining = Math.Max(0, timer.Remaining - elapsed);
            if (timer.Remaining > 1e-9) continue;
            timer.Remaining = 0;
            timer.Running = false;
            timer.FinishedThisUpdate = true;
        }
    }
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
            TimerRuntime = this,
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
            if (string.IsNullOrWhiteSpace(rule.FunctionName)&&RuleContainsActiveCondition(rule, conditionId)) EvaluateRule(_activeModule, rule, context);
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
        using var capture=context.Scene.ComponentProfiler.MeasureSystem("EventSheet.Rule",rule.DisplayName);
        var scratch = _scratchPool.Count > 0 ? _scratchPool.Pop() : new RuleScratch();
        try { return EvaluateRuleCore(module, rule, context, scratch); }
        finally { scratch.Clear(); if (_scratchPool.Count < 64) _scratchPool.Push(scratch); }
    }

    private bool EvaluateRuleCore(EventModuleDefinition module, EventRuleDefinition rule,
        EventExecutionContext context, RuleScratch scratch)
    {
        if (!rule.Enabled)
        {
            ResetLatchTree(
                rule);

            return false;
        }

        var conditionMap = scratch.Conditions;
        foreach (var condition in rule.Conditions) conditionMap.Add(condition.InstanceId, condition);
        if (!rule.HasExplicitConditionFlow)
            foreach (var condition in rule.Conditions) scratch.Roots.Add(condition.InstanceId);
        IReadOnlyList<Guid> rootConditionIds = rule.HasExplicitConditionFlow ? rule.ConnectedConditionIds : scratch.Roots;
        var results = scratch.Results;
        var evaluating = scratch.Evaluating;
        var evaluated = scratch.Evaluated;

        /*
         * Once a rule has explicit ByteGraph condition wiring, zero root
         * Conditions means "nothing is connected to the Event" and therefore
         * the Event must NOT fire.
         *
         * Legacy rules without explicit wiring keep their previous behavior
         * for backwards compatibility.
         */
        bool allPassed =
            context.FunctionArguments!=null&&rule.Conditions.Count==0 || !rule.HasExplicitConditionFlow ||
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

        var triggerOnceConditions = scratch.Triggers;
        foreach (var id in evaluated)
            if (conditionMap.TryGetValue(id, out var condition) &&
                condition.Id.Equals("system.triggerOnce", StringComparison.OrdinalIgnoreCase))
                triggerOnceConditions.Add(condition);

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
            context, scratch);

        foreach (EventRuleDefinition child
                 in rule.SubEvents)
        {
            if(!string.IsNullOrWhiteSpace(child.FunctionName))continue;
            var inherited=context.PickedSets.ToDictionary(p=>p.Key,p=>p.Value.ToList());
            EvaluateRule(
                module,
                child,
                context);
            context.PickedSets.Clear();foreach(var pair in inherited)context.PickedSets[pair.Key]=pair.Value;
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
            passed=false;
            var inherited=context.PickedSets.ToDictionary(p=>p.Key,p=>p.Value.ToList());
            var union=new Dictionary<string,HashSet<Guid>>(StringComparer.OrdinalIgnoreCase);
            foreach(Guid inputId in condition.ConditionInputIds)
            {
                context.PickedSets.Clear();foreach(var pair in inherited)context.PickedSets[pair.Key]=pair.Value.ToList();
                if(!EvaluateConditionNode(module,rule,inputId,conditionMap,context,new(),evaluating,evaluated))continue;
                passed=true;
                foreach(var pair in context.PickedSets){if(!union.TryGetValue(pair.Key,out var ids))union[pair.Key]=ids=new();ids.UnionWith(pair.Value);}
            }
            context.PickedSets.Clear();foreach(var pair in union)context.PickedSets[pair.Key]=pair.Value.ToList();
        }
        else if (condition.Id.Equals("logic.not",StringComparison.OrdinalIgnoreCase))
        {
            var inherited=context.PickedSets.ToDictionary(p=>p.Key,p=>p.Value.ToList());
            var rejected=new Dictionary<string,HashSet<Guid>>(StringComparer.OrdinalIgnoreCase);bool anyTrue=false;
            foreach(var input in condition.ConditionInputIds)
            {
                context.PickedSets.Clear();foreach(var pair in inherited)context.PickedSets[pair.Key]=pair.Value.ToList();
                if(!EvaluateConditionNode(module,rule,input,conditionMap,context,new(),evaluating,evaluated))continue;
                anyTrue=true;foreach(var pair in context.PickedSets){if(!rejected.TryGetValue(pair.Key,out var ids))rejected[pair.Key]=ids=new();ids.UnionWith(pair.Value);}
            }
            context.PickedSets.Clear();foreach(var pair in inherited)context.PickedSets[pair.Key]=pair.Value.Where(id=>!rejected.TryGetValue(pair.Key,out var ids)||!ids.Contains(id)).ToList();
            passed=condition.ConditionInputIds.Count>0&&(!anyTrue||inherited.Count>0&&context.PickedSets.Values.Any(ids=>ids.Count>0));
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
                string targetToken=EventValueResolver.GetString(condition,"target",context,"Self");
                if(context.PickedObject==null&&(targetToken.Equals("Picked",StringComparison.OrdinalIgnoreCase)||targetToken.StartsWith("Picked:",StringComparison.OrdinalIgnoreCase)))
                {
                    string set=targetToken.StartsWith("Picked:",StringComparison.OrdinalIgnoreCase)?targetToken[7..]:EventValueResolver.GetString(condition,"set",context,"Selection");
                    var accepted=new List<Guid>();
                    if(context.PickedSets.TryGetValue(set,out var ids))
                    {try{foreach(var id in ids.ToArray())if(context.Scene.FindGameObject(id) is {} obj){context.PickedObject=obj;if(definition.Evaluate(condition,context))accepted.Add(id);}}finally{context.PickedObject=null;}}
                    context.PickedSets[set]=accepted;passed=accepted.Count>0;
                }
                else passed=definition.Evaluate(condition,context);
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
        EventExecutionContext context, RuleScratch scratch)
    {
        /*
         * Compatibility path for Event Modules authored before explicit
         * ByteGraph execution wires existed.
         */
        if (!rule.HasExplicitExecutionFlow)
        {
            ExecuteLegacyActions(module,rule,context,0);

            return;
        }

        if (!rule.FirstActionId.HasValue)
        {
            return;
        }

        var actions = scratch.Actions;
        foreach (var action in rule.Actions) actions.Add(action.InstanceId, action);
        var visited = scratch.VisitedActions;

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

            if(TrySuspendWait(module,rule,action,context,action.NextActionId,-1))return;
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
        string targetToken=EventValueResolver.GetString(action,"target",context,"Self");
        if(context.PickedObject==null && (targetToken.Equals("Picked",StringComparison.OrdinalIgnoreCase)||targetToken.StartsWith("Picked:",StringComparison.OrdinalIgnoreCase)))
        {
            string set=targetToken.StartsWith("Picked:",StringComparison.OrdinalIgnoreCase)?targetToken[7..]:EventValueResolver.GetString(action,"set",context,"Selection");
            if(!context.PickedSets.TryGetValue(set,out var picked))return;
            try{foreach(var id in picked.ToArray())if(context.Scene.FindGameObject(id) is {} obj){context.PickedObject=obj;ExecuteAction(module,action,context);}}
            finally{context.PickedObject=null;}
            return;
        }
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
