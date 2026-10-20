using System.Numerics;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Physics;

namespace ByteEngine.Core.VisualLogic;

/// <summary>Scene-owned services used by event sheets. Cleared on stop; no project scripts required.</summary>
public sealed partial class CreatorSceneServices : IDisposable
{
    private static readonly ConditionalWeakTable<Scene.Scene,CreatorSceneServices> States=new();
    public static CreatorSceneServices For(Scene.Scene scene)=>States.GetValue(scene,key=>{var state=new CreatorSceneServices();state.Advance(0,key.Physics.Contacts);return state;});
    internal static void AdvanceIfPresent(Scene.Scene scene,double delta){if(States.TryGetValue(scene,out var state))state.Advance(delta,scene.Physics.Contacts);}
    internal static void Stop(Scene.Scene scene){if(States.TryGetValue(scene,out var state))state.Dispose();States.Remove(scene);}
    public sealed class Tween
    {
        internal Action<double> Apply = null!;
        internal double Duration,Elapsed;
        internal string Easing="Linear";
        public bool Paused {get;internal set;}
        public bool Completed {get;internal set;}
        public bool FinishedThisUpdate {get;internal set;}
        internal bool PendingCompletion;
    }
    private readonly Dictionary<string,Tween> _tweens=new(StringComparer.Ordinal);
    public Tween? GetTween(string key)=>_tweens.GetValueOrDefault(key);
    public void StartTween(string key,double seconds,string easing,Action<double> apply)
    {
        if(!double.IsFinite(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
        if(_tweens.Count>=4096&&!_tweens.ContainsKey(key))throw new InvalidOperationException("Remove unused tweens before starting more than 4096 named tweens in one scene.");
        var tween=new Tween{Duration=seconds,Easing=easing,Apply=apply};_tweens[key]=tween;
        if(seconds==0){apply(1);tween.Completed=true;tween.PendingCompletion=true;}else apply(0);
    }
    public void PauseTween(string key,bool paused){if(_tweens.TryGetValue(key,out var tween))tween.Paused=paused;}
    public bool RemoveTween(string key)=>_tweens.Remove(key);
    private Dictionary<(Guid,Guid,bool),PhysicsContactPair3D> _currentContacts=new(),_previousContacts=new();
    private static (Guid,Guid,bool) ContactKey(PhysicsContactPair3D p)=>p.A.Id.CompareTo(p.B.Id)<0?(p.A.Id,p.B.Id,p.IsTrigger):(p.B.Id,p.A.Id,p.IsTrigger);
    public PhysicsContactPair3D? Contact(Guid target,Guid? other,string phase,bool trigger)
    {
        var source=phase=="exit"?_previousContacts:_currentContacts;
        foreach(var pair in source)
        {
            var contact=pair.Value;
            if(contact.IsTrigger!=trigger || contact.A.Id!=target&&contact.B.Id!=target)continue;
            Guid otherId=contact.A.Id==target?contact.B.Id:contact.A.Id;
            if(other.HasValue&&otherId!=other)continue;
            if(phase=="enter"&&_previousContacts.ContainsKey(pair.Key)||phase=="exit"&&_currentContacts.ContainsKey(pair.Key))continue;
            return contact;
        }
        return null;
    }
    public void Advance(double delta,IEnumerable<PhysicsContactPair3D> contacts)
    {
        delta=double.IsFinite(delta)?Math.Max(0,delta):0;
        foreach(var tween in _tweens.Values.ToArray())
        {
            tween.FinishedThisUpdate=tween.PendingCompletion;tween.PendingCompletion=false;
            if(tween.Paused||tween.Completed)continue;
            tween.Elapsed=Math.Min(tween.Duration,tween.Elapsed+delta);
            double t=tween.Duration==0?1:tween.Elapsed/tween.Duration;
            double eased=tween.Easing.ToLowerInvariant() switch
            {"smoothstep"=>t*t*(3-2*t),"easein"=>t*t,"easeout"=>1-(1-t)*(1-t),"easeinout"=>t<.5?2*t*t:1-Math.Pow(-2*t+2,2)/2,_=>t};
            tween.Apply(eased);
            if(t>=1){tween.Completed=true;tween.FinishedThisUpdate=true;}
        }
        PollRequests();
        PollOperations();
        _previousContacts=_currentContacts;
        _currentContacts=contacts.GroupBy(ContactKey).ToDictionary(g=>g.Key,g=>g.First());
    }
    public void Dispose(){DisposeRequests();_operations.Clear();_tweens.Clear();_currentContacts.Clear();_previousContacts.Clear();}
}

