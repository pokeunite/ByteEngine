using ByteEngine.Core.Variables;
namespace ByteEngine.Core.VisualLogic;

public sealed partial class EventModuleRuntime
{
    private int _functionDepth;
    internal VariableValue CallFunction(string name,VariableValue arguments,EventExecutionContext caller)
    {
        if(_activeModule==null)throw new InvalidOperationException("Functions require an active event module.");
        if(_functionDepth>=16)throw new InvalidOperationException("Event function call depth exceeds 16.");
        static IEnumerable<EventRuleDefinition> Rules(IEnumerable<EventRuleDefinition> rules)
        {foreach(var entry in rules){yield return entry;foreach(var nested in Rules(entry.SubEvents))yield return nested;}}
        var matches=Rules(_activeModule.Rules).Where(r=>r.FunctionName==name).ToArray();
        if(matches.Length!=1)throw new InvalidOperationException("Function name must identify exactly one function: "+name);
        var rule=matches[0];if(!rule.Enabled)return VariableValue.FromNull();
        if(arguments.Type!=VariableType.Structure||rule.FunctionDefaults.Type!=VariableType.Structure)throw new ArgumentException("Function arguments and defaults must be structures.");
        var parameters=rule.FunctionDefaults.Clone();
        foreach(var pair in arguments.Children)
        {
            if(!parameters.Children.TryGetValue(pair.Key,out var expected))throw new ArgumentException("Unknown function parameter: "+pair.Key);
            if(expected.Type!=VariableType.Null&&expected.Type!=pair.Value.Type)throw new ArgumentException("Incorrect parameter type: "+pair.Key);
            parameters.Children[pair.Key]=pair.Value.Clone();
        }
        var context=new EventExecutionContext{Scene=caller.Scene,Self=caller.Self,Globals=caller.Globals,TimerRuntime=this,WarningSink=caller.WarningSink,ObserveAnimationController=caller.ObserveAnimationController,FunctionArguments=parameters,LastRaycastHit=caller.LastRaycastHit,LastRaycastDirection=caller.LastRaycastDirection,RaycastPerformed=caller.RaycastPerformed};
        foreach(var pair in caller.PickedSets)context.PickedSets[pair.Key]=pair.Value.ToList();
        _functionDepth++;
        try {EvaluateRule(_activeModule,rule,context);return context.FunctionReturn.Clone();}
        finally {_functionDepth--;}
    }
}
