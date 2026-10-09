using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Runtime;
namespace ByteEngine.Core.Navigation;
public enum NavigationAgentStatus { Idle, Following, Arrived, Unreachable, MissingRegion }
/// <summary>Budgeted path-following for transform/kinematic agents. Does not move a dynamic rigid body behind its solver.</summary>
public sealed class NavigationAgent3D : Component
{
    public bool ShowDebug {get;set;}
    private ByteEngine.Core.Graphics.ThreeD.Mesh? _debugMesh;
    public Guid RegionObject { get; set; }
    public Vector3 Destination { get; set; }
    public bool Moving { get; set; }
    public float Speed { get; set; } = 3;
    public float RepathInterval { get; set; } = .5f;
    public float ArrivalDistance { get; set; } = .15f;
    public int MaximumVisited { get; set; } = 10000;
    public NavigationAgentStatus Status { get; private set; }
    public IReadOnlyList<Vector3> Path => _path;
    public int PathQueries { get; private set; }
    private IReadOnlyList<Vector3> _path = Array.Empty<Vector3>();
    private int _index, _version = -1;
    private double _timer;
    private Vector3 _lastDestination;
    public void SetDestination(Vector3 destination) { Destination=destination;Moving=true;_timer=0;_path=Array.Empty<Vector3>();_debugMesh?.Dispose();_debugMesh=null; }
    public void Stop() { Moving=false;Status=NavigationAgentStatus.Idle; }
    protected override void OnUpdate() { if(GameObject.Scene is not {FixedSimulation:true}) Tick((float)Time.DeltaTime); }
    protected override void OnFixedUpdate() => Tick((float)Time.FixedDeltaTime);
    protected override void OnRender(ByteEngine.Core.Graphics.RenderContext context)
    {
        if(!ShowDebug||_path.Count<2||!context.Has3DCamera)return;
        if(_debugMesh==null)
        {
            var quads=new List<(Vector3,Vector3,Vector3,Vector3)>();
            for(int i=1;i<_path.Count;i++)
            {
                var a=_path[i-1]+Vector3.UnitY*.06f;var b=_path[i]+Vector3.UnitY*.06f;
                var direction=b-a;direction.Y=0;if(direction.LengthSquared()<.000001f)continue;
                var side=Vector3.Normalize(Vector3.Cross(direction,Vector3.UnitY))*.06f;
                quads.Add((a-side,a+side,b+side,b-side));
            }
            _debugMesh=NavigationDebugGeometry.Quads(quads);
        }
        context.RenderWorld.Submit(_debugMesh,NavigationDebugGeometry.Surface,Matrix4x4.Identity,ByteEngine.Core.Graphics.ThreeD.RenderQueue3D.Transparent,true,false,false);
    }
    protected override void OnDestroy(){_debugMesh?.Dispose();_debugMesh=null;}
    public void Tick(float delta)
    {
        if(!Moving){if(Status!=NavigationAgentStatus.Arrived)Status=NavigationAgentStatus.Idle;return;}
        if(!float.IsFinite(delta)||delta<=0)return;
        if(GameObject.GetComponent<ByteEngine.Core.Physics.Rigidbody3D>() is {BodyType:ByteEngine.Core.Physics.RigidbodyBodyType3D.Dynamic}) return;
        var scene=GameObject.Scene;
        var region=RegionObject==Guid.Empty ? scene?.GameObjects.Select(o=>o.GetComponent<NavigationRegion3D>()).FirstOrDefault(r=>r is {Enabled:true}) : scene?.FindGameObject(RegionObject)?.GetComponent<NavigationRegion3D>();
        if(region?.Ready!=true){Status=NavigationAgentStatus.MissingRegion;return;}
        _timer-=delta;
        if(_timer<=0 && (_path.Count==0 || Destination!=_lastDestination || _version!=region.BakeVersion))
        {
            _path=region.FindPath(Transform.WorldPosition,Destination,Math.Clamp(MaximumVisited,1,1_000_000));
            _debugMesh?.Dispose();_debugMesh=null;
            _lastDestination=Destination;_version=region.BakeVersion;_index=0;PathQueries++;
            _timer=Math.Max(.05f,float.IsFinite(RepathInterval)?RepathInterval:.5f);
            if(_path.Count==0){Status=NavigationAgentStatus.Unreachable;return;}
        }
        if(_path.Count==0)return;
        Status=NavigationAgentStatus.Following;
        float remaining=Math.Max(0,float.IsFinite(Speed)?Speed:0)*delta;
        while(_index<_path.Count)
        {
            Vector3 point=_path[_index];
            Vector3 direction=point-Transform.WorldPosition;float distance=direction.Length();
            if(distance<=Math.Max(.001f,ArrivalDistance)){_index++;continue;}
            float step=Math.Min(remaining,distance);Transform.WorldPosition+=direction*(step/distance);remaining-=step;
            if(remaining<=0)break;
        }
        if(_index>=_path.Count){Status=NavigationAgentStatus.Arrived;Moving=false;}
    }
}
