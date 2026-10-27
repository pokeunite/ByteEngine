using System.Runtime.CompilerServices;
using ByteEngine.Core.AI;
using ByteEngine.Core.Variables;
namespace ByteEngine.Core.VisualLogic;
public sealed partial class VisualLogicRegistry
{
    private static readonly ConditionalWeakTable<VisualInstruction,Dictionary<Guid,long>> _aiSignalObservers=new();
    private static void RegisterAIBehavior(VisualLogicRegistry r)
    {
        const string category="AI / Behaviour";
        var target=ObjectArg();
        AIBehavior3D? AI(VisualInstruction i,EventExecutionContext c)=>ResolveObjectTarget(i,c)?.GetComponent<AIBehavior3D>();
        var key=TextArg("key","Memory key","Ready");
        Add(r,"ai.bakeNavigation",category,"Rebuild navigation after opening a passage",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<ByteEngine.Core.Navigation.NavigationRegion3D>()?.Bake(),target);
        Add(r,"ai.pause",category,"Pause or resume AI behaviour",(i,c)=>AI(i,c)?.SetPaused(B(i,c,"paused",true)),target,BoolArg("paused","Paused",true));
        Add(r,"ai.noise",category,"Report AI hearing noise",(i,c)=>{var source=ResolveObjectTarget(i,c);if(source!=null)AIWorld.ReportNoise(c.Scene,source.Transform.WorldPosition,(float)N(i,c,"radius",15),source.Id,S(i,c,"kind","Noise"));},target,NumberArg("radius","Audible radius",15),TextArg("kind","Noise kind","Noise"));
        Add(r,"ai.forget",category,"Forget AI target and last known position",(i,c)=>AI(i,c)?.ForgetTarget(),target);
        Test(r,"ai.awareness",category,"AI awareness is",(i,c)=>AI(i,c)?.Awareness==S(i,c,"state","Visible"),target,TextArg("state","Awareness","Visible"));
        Add(r,"ai.release",category,"Release AI squad reservations",(i,c)=>{if(AI(i,c) is {} ai){AIWorld.For(c.Scene).ReleaseAttack(ai);AIWorld.For(c.Scene).ReleasePosition(ai);}},target);
        Add(r,"ai.stagger",category,"Stagger enemy and interrupt its attack",(i,c)=>ResolveObjectArgument(i,"target",c,false)?.GetComponent<ByteEngine.Core.Gameplay.SimpleEnemyAI3D>()?.Stagger((float)N(i,c,"seconds")),target,NumberArg("seconds","Stagger seconds",.5));
        Test(r,"ai.rushOutcome",category,"Enemy charge outcome is",(i,c)=>ResolveObjectArgument(i,"target",c,false)?.GetComponent<ByteEngine.Core.Gameplay.SimpleEnemyAI3D>()?.LastRushOutcome.ToString().Equals(S(i,c,"outcome","WallCrash"),StringComparison.OrdinalIgnoreCase)==true,target,TextArg("outcome","Outcome (PlayerHit, Miss, WallCrash, Interrupted)","WallCrash"));
        Add(r,"ai.clearHistory",category,"Clear AI decision history",(i,c)=>AI(i,c)?.ClearDecisionHistory(),target);
        Add(r,"ai.restart",category,"Restart AI behaviour and reset memory",(i,c)=>AI(i,c)?.Restart(),target);
        Add(r,"ai.target",category,"Set AI target",(i,c)=>{if(AI(i,c) is {} ai)ai.TargetObjectId=ResolveObjectArgument(i,"object",c,false)?.Id??Guid.Empty;},target,ObjectArg("object","Target object"));
        Add(r,"ai.memoryBool",category,"Set AI memory boolean",(i,c)=>AI(i,c)?.SetMemory(S(i,c,"key","Ready"),VariableValue.FromBoolean(B(i,c,"value",true))),target,key,BoolArg("value","Value",true));
        Add(r,"ai.memoryNumber",category,"Set AI memory number",(i,c)=>AI(i,c)?.SetMemory(S(i,c,"key","Ready"),VariableValue.FromNumber(N(i,c,"value"))),target,key,NumberArg("value","Value",0));
        Add(r,"ai.memoryText",category,"Set AI memory text",(i,c)=>AI(i,c)?.SetMemory(S(i,c,"key","Ready"),VariableValue.FromString(S(i,c,"value",""))),target,key,TextArg("value","Value",""));
        Add(r,"ai.memoryPoint",category,"Set AI memory position",(i,c)=>AI(i,c)?.SetMemory(S(i,c,"key","Destination"),VariableValue.FromVector3(EventValueResolver.GetVector3(i,"value",c,System.Numerics.Vector3.Zero))),target,TextArg("key","Memory key","Destination"),new("value","World position",VariableValue.FromVector3(System.Numerics.Vector3.Zero)));
        Add(r,"ai.readMemory",category,"Copy AI memory to object variable",(i,c)=>{if(AI(i,c) is {} ai&&ai.Blackboard.TryGet(S(i,c,"key","Ready"),out var value))ai.GameObject.Variables.Set(S(i,c,"variable","AIValue"),value!.Clone());},target,key,TextArg("variable","Destination object variable","AIValue"));
        Add(r,"ai.pickTarget",category,"Use AI target as Picked object",(i,c)=>c.PickedObject=AI(i,c)?.Target,target);
        Test(r,"ai.memoryNumber",category,"AI memory number is at least",(i,c)=>AI(i,c)?.Blackboard.TryGet(S(i,c,"key","Ready"),out var v)==true&&v!.Type==VariableType.Number&&v.Number>=N(i,c,"amount"),target,key,NumberArg("amount","Amount",1));
        Test(r,"ai.memoryTrue",category,"AI memory is true",(i,c)=>AI(i,c)?.Blackboard.TryGet(S(i,c,"key","Ready"),out var v)==true&&v!.Type==VariableType.Boolean&&v.Boolean,target,key);
        Test(r,"ai.task",category,"AI task is running",(i,c)=>AI(i,c) is {Status:BehaviorStatus.Running} ai&&ai.CurrentTask.Equals(S(i,c,"name",""),StringComparison.OrdinalIgnoreCase),target,TextArg("name","Task name","Approach target"));
        Test(r,"ai.signal",category,"AI sent signal",(i,c)=>{
            if(AI(i,c) is not {} ai)return false;var map=_aiSignalObservers.GetOrCreateValue(i);long previous=map.GetValueOrDefault(ai.GameObject.Id);map[ai.GameObject.Id]=ai.SignalSequence;
            return ai.Signals.Any(s=>s.Sequence>previous&&s.Name.Equals(S(i,c,"name","AttackWindup"),StringComparison.OrdinalIgnoreCase));
        },target,TextArg("name","Signal name","AttackWindup"));
    }
}
