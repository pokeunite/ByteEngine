using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Graphics.ThreeD;
/// <summary>Three authored distance tiers, or generated static mesh tiers. Rendering only; collision is unchanged.</summary>
public sealed class MeshLodGroup : Component
{
    public Vector2 Distances {get;set;}=new(30,90);
    public float CullDistance {get;set;}
    public float Hysteresis {get;set;}=.1f;
    public bool GenerateOnStart {get;set;}
    public Vector2 GeneratedRatios {get;set;}=new(.5f,.2f);
    public bool ScreenSize {get;set;}
    public Vector2 ScreenHeightThresholds {get;set;}=new(160,45);
    private int _level=-2;
    private readonly Dictionary<(string Viewport,int Width,int Height,bool Perspective),int> _viewLevels=new();
    private readonly List<Mesh> _generated=new();
    private readonly List<GameObject> _generatedChildren=new();
    private MeshRenderer? _source;
    private bool _sourceWasVisible;
    public int SelectLevel(float distance)
    {
        float first=float.IsFinite(Distances.X)?Math.Max(0,Distances.X):30,second=float.IsFinite(Distances.Y)?Math.Max(first,Distances.Y):90;
        if(float.IsFinite(CullDistance)&&CullDistance>0&&distance>CullDistance)return -1;
        return distance<first?0:distance<second?1:2;
    }
    public int SelectStableLevel(float distance,float projectedHeight=0)
    {
        int desired=ScreenSize ? projectedHeight>=ScreenHeightThresholds.X?0:projectedHeight>=ScreenHeightThresholds.Y?1:2 : SelectLevel(distance);
        if(CullDistance>0&&distance>CullDistance)desired=-1;
        if(_level==-2||desired==-1||_level==-1){_level=desired;return desired;}
        float h=Math.Clamp(float.IsFinite(Hysteresis)?Hysteresis:0,0,.4f);
        if(desired>_level)
        {
            float threshold=ScreenSize?(_level==0?ScreenHeightThresholds.X:ScreenHeightThresholds.Y):(_level==0?Distances.X:Distances.Y);
            if(ScreenSize?projectedHeight>threshold*(1-h):distance<threshold*(1+h))return _level;
        }
        if(desired<_level)
        {
            float threshold=ScreenSize?(desired==0?ScreenHeightThresholds.X:ScreenHeightThresholds.Y):(desired==0?Distances.X:Distances.Y);
            if(ScreenSize?projectedHeight<threshold*(1+h):distance>threshold*(1-h))return _level;
        }
        return _level=desired;
    }
    internal int SelectForViewport(string viewport,RenderView3D view,float distance,float projectedHeight)
    {
        var key=(viewport,view.TargetWidth,view.TargetHeight,view.ProjectionMatrix.M44==0);
        int saved=_level;_level=_viewLevels.GetValueOrDefault(key,-2);
        int result=SelectStableLevel(distance,projectedHeight);_viewLevels[key]=_level;_level=saved;return result;
    }
    protected override void OnStart()
    {
        if(!GenerateOnStart||GameObject.Children.Count>0||GameObject.GetComponent<MeshRenderer>() is not {Mesh:{} mesh} source)return;
        _source=source;_sourceWasVisible=source.Visible;source.Visible=false;
        for(int i=0;i<3;i++)
        {
            var child=GameObject.Scene!.CreateGameObject("Generated LOD "+i);child.SetParent(GameObject,false);_generatedChildren.Add(child);
            Mesh detail=i==0?mesh:MeshSimplifier.Simplify(mesh,i==1?GeneratedRatios.X:GeneratedRatios.Y);
            if(i>0)_generated.Add(detail);
            child.AddComponent(new MeshRenderer{Mesh=detail,Visible=_sourceWasVisible,Material=source.Material,MaterialAssetReference=source.MaterialAssetReference,CastShadows=source.CastShadows,ReceiveShadows=source.ReceiveShadows});
        }
    }
    protected override void OnStop()
    {
        foreach(var child in _generatedChildren.ToArray())GameObject.Scene?.DestroyGameObject(child);
        _generatedChildren.Clear();foreach(var mesh in _generated)mesh.Dispose();_generated.Clear();if(_source!=null)_source.Visible=_sourceWasVisible;_source=null;_level=-2;_viewLevels.Clear();
    }
    internal static bool Allows(GameObject item,RenderView3D view,string viewport)
    {
        GameObject branch=item;
        for(GameObject? owner=item.Parent;owner!=null;owner=owner.Parent)
        {
            if(owner.GetComponent<MeshLodGroup>() is {Enabled:true} group)
            {
                int available=Math.Min(3,owner.Children.Count);if(available==0)return true;
                float distance=Vector3.Distance(owner.Transform.WorldPosition,view.CameraPosition);
                float radius=owner.Children.SelectMany(c=>c.Components.OfType<MeshRenderer>()).Where(r=>r.Mesh!=null).Select(r=>r.Mesh!.LocalBounds.Size.Length()*.5f).DefaultIfEmpty(1).Max()*Vector3.Abs(owner.Transform.WorldScale).Length()/MathF.Sqrt(3);
                float projected=radius*view.ProjectionMatrix.M22*view.TargetHeight/Math.Max(.001f,distance);
                int level=group.SelectForViewport(viewport,view,distance,projected);if(level<0)return false;level=Math.Min(level,available-1);
                if(!ReferenceEquals(owner.Children[level],branch))return false;
            }
            branch=owner;
        }
        return true;
    }
}
