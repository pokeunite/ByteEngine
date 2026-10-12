using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.AI;

public readonly record struct AiDriveCommand(float Throttle,float Steering,bool Brake);
/// <summary>Adapters own their physics bodies. The agent requests controls, never writes a dynamic transform.</summary>
public interface IAiVehicleDriver
{
    Vector3 Position {get;}
    Quaternion Rotation {get;}
    Vector3 Velocity {get;}
    bool Grounded {get;}
    bool CorridorClear(Vector3 target,float radius);
    void Drive(AiDriveCommand command);
}
public enum AiAgentState { Idle, Following, Reversing, Finished, MissingRoute }
/// <summary>Small route-following brain: ordered progress, speed planning, local avoidance and bounded recovery.</summary>
public sealed class AiAgent3D : Component
{
    public Guid RouteObject {get;set;}
    public bool Running {get;set;}
    public float CruiseSpeed {get;set;}=16;
    public float LookAheadSeconds {get;set;}=.65f;
    public float VehicleRadius {get;set;}=1.4f;
    public AiAgentState State {get;private set;}
    public int Checkpoint {get;private set;}
    public event Action<AiAgentState>? StateChanged;
    public event Action<int>? CheckpointReached;
    public IAiVehicleDriver? Driver {get;set;}
    float _stuck,_reverse,_perceptionTimer,_avoidance;bool _blocked;
    public void Restart(){Checkpoint=0;_stuck=_reverse=0;Running=true;SetState(AiAgentState.Idle);}
    public void Stop(){Running=false;Driver?.Drive(new(0,0,true));SetState(AiAgentState.Idle);}
    void SetState(AiAgentState state){if(State==state)return;State=state;StateChanged?.Invoke(state);}
    protected override void OnStart(){Driver??=GameObject.Components.OfType<IAiVehicleDriver>().FirstOrDefault();}
    protected override void OnFixedUpdate()=>Tick((float)ByteEngine.Core.Time.FixedDeltaTime);
    protected override void OnUpdate(){if(GameObject.Scene is not {FixedSimulation:true})Tick((float)ByteEngine.Core.Time.DeltaTime);}
    public void Tick(float delta)
    {
        if(Driver is not {} driver||!float.IsFinite(delta)||delta<=0)return;
        if(!Running){driver.Drive(new(0,0,true));return;}
        var route=GameObject.Scene?.FindGameObject(RouteObject)?.GetComponent<RoutePath3D>();
        var points=route?.Points;
        if(points==null||points.Count<2){driver.Drive(new(0,0,true));SetState(AiAgentState.MissingRoute);return;}
        Checkpoint=Math.Clamp(Checkpoint,0,points.Count-1);
        // Advance only the next gate. Nearby crossings cannot skip a whole hairpin.
        while(Checkpoint<points.Count&&Vector3.Distance(driver.Position,points[Checkpoint])<=Math.Max(2,route!.GateRadius))
        {CheckpointReached?.Invoke(Checkpoint);Checkpoint++;}
        if(Checkpoint>=points.Count){Running=false;driver.Drive(new(0,0,true));SetState(AiAgentState.Finished);return;}
        var target=points[Checkpoint];var speed=driver.Velocity.Length();
        if(Checkpoint>0){var a=points[Checkpoint-1];var segment=target-a;var length=segment.Length();if(length>.01f){var along=Math.Clamp(Vector3.Dot(driver.Position-a,segment)/(length*length),0,1);target=a+segment*Math.Clamp(along+Math.Max(3,speed*Math.Clamp(LookAheadSeconds,.1f,2))/length,0,1);}}
        var side=Vector3.Transform(Vector3.UnitX,driver.Rotation);_perceptionTimer-=delta;
        if(_perceptionTimer<=0){_perceptionTimer=.1f;_avoidance=0;_blocked=false;
        if(!driver.CorridorClear(target,Math.Max(.2f,VehicleRadius)))
        {
            float width=Math.Max(0,route!.CorridorWidth*.5f-VehicleRadius);
            if(width>VehicleRadius&&driver.CorridorClear(target+side*width,VehicleRadius))_avoidance=width;
            else if(width>VehicleRadius&&driver.CorridorClear(target-side*width,VehicleRadius))_avoidance=-width;
            else _blocked=true;
        }
        }
        target+=side*_avoidance;
        var local=Vector3.Transform(target-driver.Position,Quaternion.Inverse(driver.Rotation));
        // Vehicle convention: local -Z forward, positive steering turns toward local +X.
        float angle=MathF.Atan2(local.X,-local.Z);
        float desired=_blocked?0:Math.Clamp(CruiseSpeed,1,50)/(1+Math.Abs(angle)*2.5f);
        if(Checkpoint==points.Count-1)desired=Math.Min(desired,Math.Max(2,Vector3.Distance(driver.Position,points[Checkpoint])*.8f));
        if(driver.Grounded&&speed<.7f)_stuck+=delta;else _stuck=0;
        if(_stuck>2.5f){_stuck=0;_reverse=1.1f;}
        if(_reverse>0){_reverse-=delta;driver.Drive(new(-.5f,-Math.Clamp(angle,-1,1),false));SetState(AiAgentState.Reversing);return;}
        driver.Drive(new(speed<desired?1:0,driver.Grounded?Math.Clamp(angle*1.8f,-1,1):0,speed>desired+1));SetState(AiAgentState.Following);
    }
}
public sealed class RoutePath3D : Component
{
    public string WaypointsJson {get;set;}="[[0,0,0],[0,0,-20]]";
    public float CorridorWidth {get;set;}=10;
    public float GateRadius {get;set;}=6;
    string? _source;IReadOnlyList<Vector3> _points=Array.Empty<Vector3>();
    public IReadOnlyList<Vector3> Points {get{if(_source!=WaypointsJson){_source=WaypointsJson;try{var rows=JsonSerializer.Deserialize<float[][]>(WaypointsJson);_points=rows?.Where(p=>p.Length==3&&p.All(float.IsFinite)).Take(4096).Select(p=>new Vector3(p[0],p[1],p[2])).ToArray()??Array.Empty<Vector3>();}catch(JsonException){_points=Array.Empty<Vector3>();}}return _points;}}
}
