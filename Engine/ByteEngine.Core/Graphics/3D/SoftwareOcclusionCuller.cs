using System.Runtime.CompilerServices;
using System.Numerics;
namespace ByteEngine.Core.Graphics.ThreeD;
/// <summary>Current-view conservative occlusion. Only cells fully covered by actual opaque triangles write depth.</summary>
public sealed class SoftwareOcclusionCuller
{
    private const int Width=128,Height=72;
    private sealed class TriangleSelection {public int Version=-1;public uint[] Indices=Array.Empty<uint>();}
    private static readonly ConditionalWeakTable<Mesh,TriangleSelection> TriangleSelections=new();
    private static uint[] OccluderTriangles(Mesh mesh)
    {
        var selected=TriangleSelections.GetOrCreateValue(mesh);
        if(selected.Version==mesh.GeometryVersion)return selected.Indices;
        var source=mesh.IndexData.Span;var vertices=mesh.VertexData.Span;
        var areas=new (float Area,int Offset)[source.Length/3];
        for(int i=0;i<areas.Length;i++)
        {
            int a=(int)source[i*3]*8,b=(int)source[i*3+1]*8,c=(int)source[i*3+2]*8;
            var ab=new Vector3(vertices[b]-vertices[a],vertices[b+1]-vertices[a+1],vertices[b+2]-vertices[a+2]);
            var ac=new Vector3(vertices[c]-vertices[a],vertices[c+1]-vertices[a+1],vertices[c+2]-vertices[a+2]);
            areas[i]=(Vector3.Cross(ab,ac).LengthSquared(),i*3);
        }
        Array.Sort(areas,static(a,b)=>b.Area.CompareTo(a.Area));
        selected.Indices=new uint[Math.Min(128,areas.Length)*3];
        for(int i=0;i<selected.Indices.Length/3;i++)source.Slice(areas[i].Offset,3).CopyTo(selected.Indices.AsSpan(i*3,3));
        selected.Version=mesh.GeometryVersion;return selected.Indices;
    }
    private readonly float[] _depth=new float[Width*Height];
    private readonly List<OccluderState> _cachedOccluders=new();
    private Matrix4x4 _cachedView;
    private bool _hasDepth;
    private readonly record struct OccluderState(Mesh Mesh,Matrix4x4 Model,int Version,CullMode3D Cull,FrontFaceWinding3D Front);
    private readonly List<(RenderSubmission Item,float Area)> _occluders=new();
    public int LastOccluderCount {get;private set;}
    public int LastCulledCount {get;private set;}
    private readonly record struct Rect(int X0,int Y0,int X1,int Y1,float Near);
    public void Cull(List<RenderSubmission> visible,RenderView3D view)
    {
        _occluders.Clear();LastOccluderCount=LastCulledCount=0;
        // Outside this floating-point precision envelope, keep rendering all objects.
        if(Math.Max(Math.Abs(view.ProjectionMatrix.M11),Math.Abs(view.ProjectionMatrix.M22))>4)return;
        if(Math.Max(Math.Abs(view.CameraPosition.X),Math.Max(Math.Abs(view.CameraPosition.Y),Math.Abs(view.CameraPosition.Z)))>1000)return;
        foreach(var item in visible)
        {
            var m=item.Material;
            if(item.Queue!=RenderQueue3D.Opaque || m.BlendMode!=BlendMode3D.Opaque || !m.DepthTest || !m.ResolveDepthWrite() || m.PolygonMode!=PolygonMode3D.Fill || m.HeightFieldTexture!=null)continue;
            if(Math.Max(Vector3.Abs(item.WorldBounds.Minimum).Length(),Vector3.Abs(item.WorldBounds.Maximum).Length())>1000)continue;
            if(Math.Max(item.Mesh.LocalBounds.Minimum.Length(),item.Mesh.LocalBounds.Maximum.Length())>1000)continue;
            if(item.Mesh.IsDynamic || item.Mesh.IndexCount>60000 || !ProjectBounds(item.WorldBounds,view.ViewProjectionMatrix,out var rect))continue;
            float area=(rect.X1-rect.X0)*(rect.Y1-rect.Y0);
            if(area>=32)_occluders.Add((item,area));
        }
        _occluders.Sort(static(a,b)=>b.Area.CompareTo(a.Area));
        LastOccluderCount=Math.Min(32,_occluders.Count);
        if(LastOccluderCount==0){_cachedOccluders.Clear();_hasDepth=false;_occluders.Clear();return;}
        bool reuse=_hasDepth && _cachedView==view.ViewProjectionMatrix && _cachedOccluders.Count==LastOccluderCount;
        for(int i=0;reuse && i<LastOccluderCount;i++)
        {
            var item=_occluders[i].Item;
            reuse=_cachedOccluders[i]==new OccluderState(item.Mesh,item.ModelMatrix,item.Mesh.GeometryVersion,item.Material.CullMode,item.Material.FrontFace);
        }
        if(!reuse)
        {
            Array.Fill(_depth,float.PositiveInfinity);_cachedOccluders.Clear();
            for(int i=0;i<LastOccluderCount;i++)
            {
                var item=_occluders[i].Item;Rasterize(item,view.ViewProjectionMatrix);
                _cachedOccluders.Add(new(item.Mesh,item.ModelMatrix,item.Mesh.GeometryVersion,item.Material.CullMode,item.Material.FrontFace));
            }
            _cachedView=view.ViewProjectionMatrix;_hasDepth=true;
        }
        int write=0;
        for(int i=0;i<visible.Count;i++)
        {
            var item=visible[i];
            bool hidden=item.Queue==RenderQueue3D.Opaque && item.Material.DepthTest && ProjectBounds(item.WorldBounds,view.ViewProjectionMatrix,out var rect) && Hidden(rect);
            if(hidden){LastCulledCount++;continue;}
            visible[write++]=item;
        }
        if(write<visible.Count)visible.RemoveRange(write,visible.Count-write);
        _occluders.Clear();
    }
    private bool Hidden(Rect r)
    {
        // Expand to complete cells around the projected bounds. Empty, offscreen,
        // near-plane-intersecting and camera-containing bounds are never rejected.
        if(r.X0<0||r.Y0<0||r.X1>=Width||r.Y1>=Height)return false;
        for(int y=r.Y0;y<=r.Y1;y++)for(int x=r.X0;x<=r.X1;x++)
            if(!(_depth[y*Width+x]+.0001f<r.Near))return false;
        return true;
    }
    private static bool ProjectBounds(BoundingBox3D bounds,Matrix4x4 vp,out Rect result)
    {
        float x0=float.PositiveInfinity,y0=x0,x1=float.NegativeInfinity,y1=x1,near=1;
        for(int corner=0;corner<8;corner++)
        {
            var p=new Vector3((corner&1)==0?bounds.Minimum.X:bounds.Maximum.X,(corner&2)==0?bounds.Minimum.Y:bounds.Maximum.Y,(corner&4)==0?bounds.Minimum.Z:bounds.Maximum.Z);
            var clip=Vector4.Transform(new Vector4(p,1),vp);
            if(!float.IsFinite(clip.X+clip.Y+clip.Z+clip.W)||clip.W<=.00001f||clip.Z<0){result=default;return false;}
            float x=(clip.X/clip.W*.5f+.5f)*Width,y=(clip.Y/clip.W*.5f+.5f)*Height;
            x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);near=Math.Min(near,clip.Z/clip.W);
        }
        result=new((int)MathF.Floor(Math.Clamp(x0,-1,Width))-1,(int)MathF.Floor(Math.Clamp(y0,-1,Height))-1,(int)MathF.Ceiling(Math.Clamp(x1,-1,Width))+1,(int)MathF.Ceiling(Math.Clamp(y1,-1,Height))+1,near);
        return x1>=0&&y1>=0&&x0<=Width&&y0<=Height;
    }
    private void Rasterize(RenderSubmission item,Matrix4x4 vp)
    {
        var indices=OccluderTriangles(item.Mesh).AsSpan();var matrix=item.ModelMatrix*vp;
        for(int i=0;i<indices.Length;i+=3)
        {
            Vector3 a=Project(indices[i]),b=Project(indices[i+1]),c=Project(indices[i+2]);
            if(!float.IsFinite(a.X+b.X+c.X))continue;
            float cross=Edge(a,b,c.X,c.Y);if(!float.IsFinite(cross)||Math.Abs(cross)<2f)continue;
            bool front=(cross>0)==(item.Material.FrontFace==FrontFaceWinding3D.CounterClockwise);
            if((item.Material.CullMode==CullMode3D.Back && !front)||(item.Material.CullMode==CullMode3D.Front && front))continue;
            if(cross<0)(b,c)=(c,b);
            int x0=Math.Clamp((int)MathF.Floor(Math.Min(a.X,Math.Min(b.X,c.X))),0,Width-1),x1=Math.Clamp((int)MathF.Ceiling(Math.Max(a.X,Math.Max(b.X,c.X))),0,Width-1);
            int y0=Math.Clamp((int)MathF.Floor(Math.Min(a.Y,Math.Min(b.Y,c.Y))),0,Height-1),y1=Math.Clamp((int)MathF.Ceiling(Math.Max(a.Y,Math.Max(b.Y,c.Y))),0,Height-1);
            float far=Math.Max(a.Z,Math.Max(b.Z,c.Z))+.00001f;
            var e0=CellEdge(a,b);var e1=CellEdge(b,c);var e2=CellEdge(c,a);
            for(int y=y0;y<=y1;y++)
            {
                double d0=e0.X*x0+e0.Y*y+e0.C,d1=e1.X*x0+e1.Y*y+e1.C,d2=e2.X*x0+e2.Y*y+e2.C;
                for(int x=x0;x<=x1;x++,d0+=e0.X,d1+=e1.X,d2+=e2.X)
                    if(d0>.01&&d1>.01&&d2>.01)_depth[y*Width+x]=Math.Min(_depth[y*Width+x],far);
            }
        }
        Vector3 Project(uint index)
        {
            int n=(int)index*8;var data=item.Mesh.VertexData.Span;
            var clip=Vector4.Transform(new Vector4(data[n],data[n+1],data[n+2],1),matrix);
            if(clip.W<=.00001f||clip.Z<0||clip.Z>clip.W)return new(float.NaN);
            return new((clip.X/clip.W*.5f+.5f)*Width,(clip.Y/clip.W*.5f+.5f)*Height,clip.Z/clip.W);
        }
    }
    private static float Edge(Vector3 a,Vector3 b,float x,float y)=>(b.X-a.X)*(y-a.Y)-(b.Y-a.Y)*(x-a.X);
    private static (double X,double Y,double C) CellEdge(Vector3 a,Vector3 b)
    {
        double x=(double)a.Y-b.Y,y=(double)b.X-a.X;
        return (x,y,-x*a.X-y*a.Y+Math.Min(0,x)+Math.Min(0,y)-.5*Math.Sqrt(x*x+y*y));
    }
}
