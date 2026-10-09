using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
namespace ByteEngine.Core.Navigation;
/// <summary>Authored static XZ navigation region. Baking inflates obstacle footprints for agent clearance.</summary>
public enum NavigationBakeMode { Grid, MeshSurface }
public sealed class NavigationRegion3D : Component
{
    public NavigationBakeMode BakeMode {get;set;}
    public Guid SurfaceObject {get;set;}
    public NavigationMesh? Mesh {get;private set;}
    public bool Ready=>Grid!=null||Mesh!=null;
    public IReadOnlyList<Vector3> FindPath(Vector3 start,Vector3 end,int maximumVisited=10000)=>Mesh?.FindPath(start,end,maximumVisited)??Grid?.FindPath(start,end,maximumVisited)??Array.Empty<Vector3>();
    public bool SampleTerrain {get;set;}
    public float MaximumSlope {get;set;}=45;
    public float MaximumStepHeight {get;set;}=.4f;
    public bool ShowDebug {get;set;}
    private ByteEngine.Core.Graphics.ThreeD.Mesh? _debugMesh;
    public int Width { get; set; } = 64;
    public int Height { get; set; } = 64;
    public float CellSize { get; set; } = 1;
    public float AgentRadius { get; set; } = .35f;
    public float AgentHeight { get; set; } = 1.8f;
    public bool BakeOnStart { get; set; } = true;
    public int ObstacleLayerMask { get; set; } = -1;
    public NavigationGrid? Grid { get; private set; }
    public int BakeVersion { get; private set; }
    public int WalkableCells { get; private set; }
    protected override void OnStart() { if (BakeOnStart) Bake(); }
    protected override void OnRender(ByteEngine.Core.Graphics.RenderContext context)
    {
        if(!ShowDebug||!Ready||!context.Has3DCamera)return;
        if(_debugMesh==null)
        {
            var quads=new List<(Vector3,Vector3,Vector3,Vector3)>();
            if(Mesh!=null)foreach(var polygon in Mesh.Polygons.Take(16384))quads.Add((polygon.A+Vector3.UnitY*.025f,polygon.B+Vector3.UnitY*.025f,polygon.C+Vector3.UnitY*.025f,polygon.C+Vector3.UnitY*.025f));
            if(Grid!=null)for(int z=0;z<Grid.Height;z++)for(int x=0;x<Grid.Width;x++)if(quads.Count<16384&&Grid.IsWalkable(x,z))
            {float a=Grid.Origin.X+x*Grid.CellSize,b=Grid.Origin.Y+z*Grid.CellSize,y=Grid.HeightAt(x,z,Transform.WorldPosition.Y)+.025f,c=Grid.CellSize*.94f;quads.Add((new(a,y,b),new(a+c,y,b),new(a+c,y,b+c),new(a,y,b+c)));if(quads.Count>=16384)break;}
            _debugMesh=NavigationDebugGeometry.Quads(quads);
        }
        context.RenderWorld.Submit(_debugMesh,NavigationDebugGeometry.Surface,Matrix4x4.Identity,ByteEngine.Core.Graphics.ThreeD.RenderQueue3D.Transparent,true,false,false);
    }
    protected override void OnDestroy(){_debugMesh?.Dispose();_debugMesh=null;}
    public void Bake()
    {
        var scene = GameObject.Scene;
        if (scene == null) return;
        float cell = float.IsFinite(CellSize) ? Math.Clamp(CellSize,.05f,100) : 1;
        int width = Math.Clamp(Width,1,1024), height = Math.Clamp(Height,1,1024);
        Vector3 origin = Transform.WorldPosition;
        var grid = new NavigationGrid(width,height,cell,new(origin.X,origin.Z));
        float radius = float.IsFinite(AgentRadius) ? Math.Max(0,AgentRadius) : .35f;
        float agentHeight = float.IsFinite(AgentHeight) ? Math.Max(.1f,AgentHeight) : 1.8f;
        var obstacles = new List<(Vector3 Minimum,Vector3 Maximum)>();
        foreach (var obj in scene.GameObjects)
            if (obj.ActiveInHierarchy && (ObstacleLayerMask & (1 << obj.Layer)) != 0)
                foreach (var collider in obj.Components.OfType<Collider3D>())
                {
                    if (!collider.Enabled || collider.IsTrigger || collider is HeightfieldCollider3D || (BakeMode==NavigationBakeMode.MeshSurface&&obj.Id==SurfaceObject)) continue;
                    // Oriented boxes are conservatively projected into world-space footprints.
                    Vector3 half = Vector3.Abs(collider.Size * obj.Transform.WorldScale) * .5f;
                    Vector3 center = Vector3.Transform(collider.Center,obj.Transform.WorldMatrix);
                    var rotation = Matrix4x4.CreateFromQuaternion(obj.Transform.WorldRotation);
                    Vector3 extent = Vector3.Abs(new(rotation.M11,rotation.M12,rotation.M13))*half.X +
                        Vector3.Abs(new(rotation.M21,rotation.M22,rotation.M23))*half.Y + Vector3.Abs(new(rotation.M31,rotation.M32,rotation.M33))*half.Z;
                    Vector3 min = center-extent,max=center+extent;
                    if (collider is MeshCollider3D mesh)
                    {
                        if (mesh.Geometry == null) continue;
                        min = mesh.WorldBounds.Minimum;
                        max = mesh.WorldBounds.Maximum;
                    }
                    if (max.Y <= origin.Y+.05f || min.Y >= origin.Y+agentHeight) continue;
                    obstacles.Add((min-new Vector3(radius,0,radius),max+new Vector3(radius,0,radius)));
                }
        if(BakeMode==NavigationBakeMode.MeshSurface)
        {
            Grid=null;Mesh=null;
            var geometry=scene.FindGameObject(SurfaceObject)?.GetComponent<MeshCollider3D>()?.Geometry;
            if(geometry!=null)
            {
                Mesh=NavigationMesh.Bake(geometry.Points,geometry.Indices,MaximumSlope,radius,agentHeight,obstacles);
                foreach(var link in scene.GameObjects.Where(o=>o.ActiveInHierarchy).SelectMany(o=>o.Components.OfType<NavigationLink3D>()).Where(l=>l.Enabled))Mesh.AddLink(link.Start,link.End,link.Cost,link.Bidirectional);
            }
            WalkableCells=Mesh?.Polygons.Count??0;BakeVersion++;_debugMesh?.Dispose();_debugMesh=null;return;
        }
        Mesh=null;
        grid.MaximumStepHeight=float.IsFinite(MaximumStepHeight)?Math.Max(0,MaximumStepHeight):.4f;
        var terrains=SampleTerrain?scene.GameObjects.Where(o=>o.ActiveInHierarchy).SelectMany(o=>o.Components.OfType<HeightfieldCollider3D>()).Where(c=>c.Enabled&&!c.IsTrigger).ToArray():Array.Empty<HeightfieldCollider3D>();
        float minimumUp=MathF.Cos(Math.Clamp(float.IsFinite(MaximumSlope)?MaximumSlope:45,0,89)*MathF.PI/180);
        int walkable = 0;
        for(int z=0;z<height;z++) for(int x=0;x<width;x++)
        {
            float minX=origin.X+x*cell,minZ=origin.Z+z*cell;
            bool blocked=obstacles.Any(b=>b.Minimum.X<minX+cell && b.Maximum.X>minX && b.Minimum.Z<minZ+cell && b.Maximum.Z>minZ);
            float heightValue=origin.Y;
            foreach(var terrain in terrains)
                if(terrain.TrySampleWorld(new(minX+cell*.5f,origin.Y,minZ+cell*.5f),out var surface,out var normal)){heightValue=surface.Y;if(normal.Y<minimumUp)blocked=true;break;}
            grid.SetHeight(x,z,heightValue);
            grid.SetCell(x,z,blocked); if(!blocked) walkable++;
        }
        foreach(var link in scene.GameObjects.Where(o=>o.ActiveInHierarchy).SelectMany(o=>o.Components.OfType<NavigationLink3D>()).Where(l=>l.Enabled))
        {try {grid.AddLink(link.Start,link.End,Math.Max(.001f,link.Cost),link.Bidirectional);}catch(ArgumentOutOfRangeException){}}
        Grid=grid;WalkableCells=walkable;BakeVersion++;
        _debugMesh?.Dispose();_debugMesh=null;
    }
}
