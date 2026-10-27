using System.Numerics;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using RuntimeScene=ByteEngine.Core.Scene.Scene;
namespace ByteEngine.Core.AI;

public sealed record AINoiseEvent(long Sequence, Vector3 Position, float Radius, Guid Source, string Kind, double Time);
/// <summary>Scene-local bounded stimuli and reservations. No cross-scene/static actor ownership.</summary>
public sealed class AIWorld
{
    private static readonly ConditionalWeakTable<RuntimeScene,AIWorld> Worlds=new();
    public static AIWorld For(RuntimeScene scene)=>Worlds.GetValue(scene,_=>new());
    private readonly List<AINoiseEvent> _noises=[];
    private long _noiseSequence;
    private long _querySlot=-1;
    public bool TryPositionQuery(AIBehavior3D owner)
    {
        double time=owner.GameObject.Scene!.SimulationTime; if(time<=0)time=owner.BehaviorTime;
        long slot=(long)Math.Floor(time*20);if(slot==_querySlot)return false;_querySlot=slot;return true;
    }
    private sealed record AttackLease(AIBehavior3D Owner, Guid Target, string Squad, double Start, float Duration);
    private sealed record PositionLease(AIBehavior3D Owner, Vector3 Point, double Start, float Duration, float Spacing);
    private readonly Dictionary<Guid,AttackLease> _attacks=[];
    private readonly Dictionary<Guid,PositionLease> _positions=[];
    public IReadOnlyList<AINoiseEvent> Noises=>_noises;
    public static void ReportNoise(RuntimeScene scene,Vector3 point,float radius,Guid source=default,string kind="Noise")
    {
        if(!float.IsFinite(point.X)||!float.IsFinite(point.Y)||!float.IsFinite(point.Z)||!float.IsFinite(radius)||radius<=0)return;
        var world=For(scene);world._noises.Add(new(++world._noiseSequence,point,Math.Clamp(radius,0,1000),source,kind,scene.SimulationTime));if(world._noises.Count>128)world._noises.RemoveAt(0);
    }
    private static bool Alive(AIBehavior3D owner)=>owner.ReservationOwner is {Scene:not null,ActiveInHierarchy:true} actor&&owner.Enabled&&!owner.Paused&&actor.GetComponent<HealthComponent>()?.IsDead!=true&&actor.GetComponent<SimpleEnemyAI3D>() is not {BehaviorControlled:true,Enabled:false};
    private void Prune()
    {
        foreach(var key in _attacks.Where(p=>!Alive(p.Value.Owner)||p.Value.Owner.BehaviorTime-p.Value.Start>=p.Value.Duration).Select(p=>p.Key).ToArray())_attacks.Remove(key);
        foreach(var key in _positions.Where(p=>!Alive(p.Value.Owner)||p.Value.Owner.BehaviorTime-p.Value.Start>=p.Value.Duration).Select(p=>p.Key).ToArray())_positions.Remove(key);
    }
    public bool ReserveAttack(AIBehavior3D owner,Guid target,float duration)
    {
        Prune();if(_attacks.ContainsKey(owner.GameObject.Id))return true;
        var peers=_attacks.Values.Where(p=>p.Target==target&&p.Squad==owner.Squad).ToArray();
        int limit=Math.Clamp(owner.MaximumAttackers,1,16);if(peers.Length>0)limit=Math.Min(limit,peers.Min(p=>Math.Clamp(p.Owner.MaximumAttackers,1,16)));
        if(peers.Length>=limit)return false;
        _attacks[owner.GameObject.Id]=new(owner,target,owner.Squad,owner.BehaviorTime,Math.Clamp(duration,1,30));return true;
    }
    public bool OwnsAttack(AIBehavior3D owner){Prune();return _attacks.ContainsKey(owner.GameObject.Id);}
    public void ReleaseAttack(AIBehavior3D owner)=>_attacks.Remove(owner.GameObject.Id);
    public bool PositionAvailable(AIBehavior3D owner,Vector3 point,float spacing)
    {
        Prune();return _positions.Values.All(p=>p.Owner==owner||Vector3.DistanceSquared(p.Point,point)>=MathF.Pow(Math.Max(spacing,p.Spacing),2));
    }
    public void ReservePosition(AIBehavior3D owner,Vector3 point,float duration,float spacing){Prune();_positions[owner.GameObject.Id]=new(owner,point,owner.BehaviorTime,Math.Clamp(duration,.2f,30),spacing);}
    public bool TryPosition(AIBehavior3D owner,out Vector3 point){Prune();if(_positions.TryGetValue(owner.GameObject.Id,out var lease)){point=lease.Point;return true;}point=default;return false;}
    public void ReleasePosition(AIBehavior3D owner)=>_positions.Remove(owner.GameObject.Id);
    public int AttackReservationCount {get {Prune();return _attacks.Count;}}
    public int PositionReservationCount {get {Prune();return _positions.Count;}}
}
