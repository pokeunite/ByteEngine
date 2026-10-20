using System.Runtime.CompilerServices;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;

namespace KarlsonDemo.Plugin;

/// <summary>Event-sheet plumbing only: publishes the shot already simulated by the weapon.</summary>
internal static class WeaponEventBridge
{
    private sealed class Observer
    {
        public Observer(){}
        public readonly Dictionary<(Guid Weapon,string Signal),State> States=[];
    }
    private sealed class State
    {
        public long Seen;
        public EventExecutionContext? Context;
        public bool Passed;
    }
    private static readonly ConditionalWeakTable<VisualInstruction,ConditionalWeakTable<GameObject,Observer>> Observers=new();

    // Sequence-backed pulses survive an event sheet running before the firing sheet.
    // Each condition/owner observes independently; one rule cannot consume another's event.
    internal static bool Pulse(VisualInstruction instruction,EventExecutionContext context,WeaponInstance3D weapon,string signal)
    {
        (long sequence,bool current)=signal switch
        {
            "fired"=>(weapon.ShotsFired,weapon.FiredThisFrame),
            "hit"=>(weapon.HitSequence,weapon.HitThisFrame),
            "equipped"=>(weapon.EquipSequence,weapon.EquippedThisFrame),
            "reloadStarted"=>(weapon.ReloadStartSequence,weapon.ReloadStartedThisFrame),
            "reloadCancelled"=>(weapon.ReloadCancelSequence,weapon.ReloadCancelledThisFrame),
            "reloadComplete"=>(weapon.ReloadCompleteSequence,weapon.ReloadCompletedThisFrame),
            _=>(0,false)
        };
        var owner=Observers.GetOrCreateValue(instruction).GetOrCreateValue(context.Self);
        var key=(weapon.GameObject.Id,signal);
        if(!owner.States.TryGetValue(key,out var state))owner.States[key]=state=new(){Seen=current?Math.Max(0,sequence-1):sequence};
        if(ReferenceEquals(state.Context,context)&&state.Seen==sequence)return state.Passed;
        state.Context=context;state.Passed=sequence>state.Seen;state.Seen=sequence;
        return state.Passed;
    }
    internal static void Publish(EventExecutionContext context,WeaponInstance3D? weapon,int hitIndex=0,bool accepted=true)
    {
        context.LastRaycastHit=null;context.LastRaycastDirection=default;
        context.RaycastPerformed=accepted&&weapon?.LastShotWasHitscan==true;
        if(!context.RaycastPerformed||weapon==null||hitIndex<0||hitIndex>=weapon.ShotHits.Count)return;
        var result=weapon.ShotHits[hitIndex];context.LastRaycastHit=result.Hit;context.LastRaycastDirection=result.Direction;
    }
}
