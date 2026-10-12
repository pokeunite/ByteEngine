using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.AI;
public enum RaceState{Waiting,Countdown,Racing,Finished}
/// <summary>Ordered checkpoint race rules, independent of vehicle implementation.</summary>
public sealed class RaceDirector3D:Component
{
 public bool ExternalTick{get;set;}
 protected override void OnUpdate(){if(!ExternalTick&&GameObject.Scene is not {FixedSimulation:true})Tick((float)ByteEngine.Core.Time.DeltaTime);}
 protected override void OnFixedUpdate(){if(!ExternalTick)Tick((float)ByteEngine.Core.Time.FixedDeltaTime);}
 public Guid RouteObject{get;set;}
 public float CountdownSeconds{get;set;}=3;
 public RaceState State{get;private set;}
 public float Remaining{get;private set;}
 public float Elapsed{get;private set;}
 public string Winner{get;private set;}="";
 sealed class Racer{public required Func<Vector3> Position;public int Gate;public Vector3 Previous;}
 readonly Dictionary<string,Racer> _racers=[];
 public event Action<string>? Finished;
 public void Register(string id,Func<Vector3> position){if(State is RaceState.Racing or RaceState.Countdown)throw new InvalidOperationException("Register before starting.");_racers[id]=new(){Position=position,Previous=position()};}
 public void StartRace(){if(_racers.Count<2)throw new InvalidOperationException("A race needs at least two racers.");foreach(var r in _racers.Values){r.Gate=0;r.Previous=r.Position();}Elapsed=0;Winner="";Remaining=Math.Clamp(CountdownSeconds,0,10);State=Remaining>0?RaceState.Countdown:RaceState.Racing;}
 public void Reset(){State=RaceState.Waiting;_racers.Clear();Winner="";Elapsed=0;}
 public int Gate(string id)=>_racers.TryGetValue(id,out var racer)?racer.Gate:0;
 public int Position(string id){if(!_racers.TryGetValue(id,out var own))return 0;var route=GameObject.Scene?.FindGameObject(RouteObject)?.GetComponent<RoutePath3D>();if(route==null||route.Points.Count==0)return 0;float Distance(Racer r)=>Vector3.DistanceSquared(r.Position(),route.Points[Math.Min(r.Gate,route.Points.Count-1)]);return 1+_racers.Count(pair=>pair.Key!=id&&(pair.Value.Gate>own.Gate||pair.Value.Gate==own.Gate&&Distance(pair.Value)<Distance(own)));}
 public void Tick(float delta){if(!float.IsFinite(delta)||delta<=0)return;if(State==RaceState.Countdown){Remaining=Math.Max(0,Remaining-delta);if(Remaining<=0)State=RaceState.Racing;return;}if(State!=RaceState.Racing)return;var route=GameObject.Scene?.FindGameObject(RouteObject)?.GetComponent<RoutePath3D>();if(route==null||route.Points.Count<2)return;Elapsed+=delta;foreach(var (id,r) in _racers){var current=r.Position();var target=route.Points[r.Gate];var segment=current-r.Previous;float d=segment.LengthSquared();float t=d>.0001f?Math.Clamp(Vector3.Dot(target-r.Previous,segment)/d,0,1):0;bool crossed=Vector3.DistanceSquared(r.Previous+segment*t,target)<=route.GateRadius*route.GateRadius;r.Previous=current;if(crossed&&++r.Gate>=route.Points.Count){Winner=id;State=RaceState.Finished;Finished?.Invoke(id);break;}}}
}
