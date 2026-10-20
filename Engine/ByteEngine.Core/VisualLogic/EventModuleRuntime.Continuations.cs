namespace ByteEngine.Core.VisualLogic;

public sealed partial class EventModuleRuntime
{
    private sealed class Continuation
    {
        public required EventModuleDefinition Module;
        public required EventRuleDefinition Rule;
        public required EventExecutionContext Context;
        public Guid? Next;
        public int LegacyIndex;
        public double Remaining;
    }
    private readonly List<Continuation> _continuations = new();
    public int PendingWaitCount => _continuations.Count;
    internal void CancelWaits()=>_continuations.Clear();
    private bool TrySuspendWait(EventModuleDefinition module,EventRuleDefinition rule,VisualInstruction action,EventExecutionContext context,Guid? next,int legacyIndex)
    {
        if(action.Id!="time.wait")return false;
        if(context.FunctionArguments!=null)throw new InvalidOperationException("Wait cannot run inside a synchronous event function. Start a separate timed event instead.");
        double delay=EventValueResolver.GetNumber(action,"seconds",context,1);
        if(!double.IsFinite(delay)||delay<0 || _continuations.Count>=1024)
        {WarnOnce(module.Id+":wait-limit","Wait duration must be finite and non-negative; at most 1024 waits may be pending per sheet instance.",context);return true;}
        var snapshot=new EventExecutionContext{TimerRuntime=this,Scene=context.Scene,Self=context.Self,Globals=context.Globals,
            WarningSink=context.WarningSink,ObserveAnimationController=context.ObserveAnimationController,
            LastRaycastHit=context.LastRaycastHit,LastRaycastDirection=context.LastRaycastDirection,RaycastPerformed=context.RaycastPerformed,
            AnimationSignalKind=context.AnimationSignalKind,AnimationSignalSource=context.AnimationSignalSource,
            AnimationEvent=context.AnimationEvent,AnimationWindow=context.AnimationWindow};
        foreach(var pair in context.PickedSets)snapshot.PickedSets[pair.Key]=pair.Value.ToList();
        _continuations.Add(new(){Module=module,Rule=rule,Context=snapshot,Next=next,LegacyIndex=legacyIndex,Remaining=delay});
        VisualLogicDebugTrace.Mark(module.Id,action.InstanceId,VisualLogicTraceState.ActionExecuted);
        return true;
    }
    private void ExecuteLegacyActions(EventModuleDefinition module,EventRuleDefinition rule,EventExecutionContext context,int first)
    {
        for(int index=first;index<rule.Actions.Count;index++)
        {
            var action=rule.Actions[index];
            if(TrySuspendWait(module,rule,action,context,null,index+1))return;
            ExecuteAction(module,action,context);
        }
    }
    private void AdvanceContinuations(double deltaTime)
    {
        double delta=double.IsFinite(deltaTime)?Math.Max(0,deltaTime):0;
        foreach(var pending in _continuations.ToArray())
        {
            if(pending.Module!=_activeModule || pending.Context.Scene!=_activeScene || pending.Context.Self!=_activeSelf ||
                pending.Context.Scene.FindGameObject(pending.Context.Self.Id)==null || !pending.Rule.Enabled)
            {_continuations.Remove(pending);continue;}
            pending.Remaining-=delta;
            if(pending.Remaining>1e-9)continue;
            _continuations.Remove(pending);
            if(pending.LegacyIndex>=0)ExecuteLegacyActions(pending.Module,pending.Rule,pending.Context,pending.LegacyIndex);
            else ExecuteActionFlow(pending.Module,pending.Rule,pending.Next,pending.Rule.Actions.ToDictionary(a=>a.InstanceId),pending.Context,new());
        }
    }
}
