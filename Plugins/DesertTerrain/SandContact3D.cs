using System.Numerics;
using ByteEngine.Core.Scene;
namespace DesertTerrain;
/// <summary>Put this component at the actual tyre/foot contact point, not the wheel centre.</summary>
public sealed class SandContact3D : Component
{
    public float Width { get; set; } = .9f;
    public float RutDepth { get; set; } = .07f;
    public float ContactTolerance { get; set; } = .15f;
    public bool EmitTracks { get; set; } = true;
    private DesertTerrain3D? _terrain;private Vector3? _previous;
    protected override void OnUpdate()
    {
        var scene=GameObject.Scene;if(scene==null)return;
        if(_terrain==null||!_terrain.Enabled||!_terrain.GameObject.ActiveInHierarchy)
            _terrain=scene.GameObjects.Where(g=>g.ActiveInHierarchy).SelectMany(g=>g.Components).OfType<DesertTerrain3D>().FirstOrDefault(t=>t.Enabled);
        var point=Transform.WorldPosition;
        if(!EmitTracks||_terrain==null||!_terrain.TryGetSand(point,out var sand)||Math.Abs(point.Y-sand.Position.Y)>Math.Clamp(ContactTolerance,.01f,.35f)) {_previous=null;return;}
        if(_previous is Vector3 from)_terrain.StampTrack(from,point,Math.Max(.1f,Width),Math.Clamp(RutDepth,0,.2f));
        _previous=point;
    }
    protected override void OnStop(){_previous=null;_terrain=null;}
}
