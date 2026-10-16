using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Static bathymetry rasterized from real scene meshes. Shared by GPU ocean waves
/// and CPU water queries; no level-specific island silhouette is stored in engine code.</summary>
public sealed class WaterDepthField3D : Component
{
    public Vector2 Origin {get;set;}=new(-35,-30);
    public Vector2 Size {get;set;}=new(70,60);
    public int Resolution {get;set;}=192;
    public float MinimumHeight {get;set;}=-8;
    public float MaximumHeight {get;set;}=8;
    public float DefaultHeight {get;set;}=-5.2f;
    public string IncludeRootName {get;set;}="";
    public int TrianglesBaked {get;private set;}
    public Texture2D? Texture {get;private set;}
    float[]? heights;int count;
    public void Rebuild(){Texture?.Dispose();Texture=null;heights=null;TrianglesBaked=0;}
    public void Bake()
    {
        if(heights!=null)return;count=Math.Clamp(Resolution,16,512);heights=Enumerable.Repeat(DefaultHeight,count*count).ToArray();
        foreach(var o in GameObject.Scene!.GameObjects){
            if(!o.ActiveInHierarchy)continue;
            if(IncludeRootName.Length>0){var ancestor=o;while(ancestor!=null&&ancestor.Name!=IncludeRootName)ancestor=ancestor.Parent;if(ancestor==null)continue;}
            var r=o.GetComponent<MeshRenderer>();if(r?.Mesh==null||!r.Visible||r.Material.WaterSurface||r.Material.WaterVolume||r.Material.BlendMode!=BlendMode3D.Opaque)continue;
            var vertices=r.Mesh.VertexData.Span;var indices=r.Mesh.IndexData.Span;var matrix=r.Transform.WorldMatrix;
            var world=new Vector3[r.Mesh.VertexCount];for(int i=0;i<world.Length;i++)world[i]=Vector3.Transform(new(vertices[i*8],vertices[i*8+1],vertices[i*8+2]),matrix);
            for(int i=0;i<indices.Length;i+=3)Rasterize(world[(int)indices[i]],world[(int)indices[i+1]],world[(int)indices[i+2]]);
        }
    }
    void Rasterize(Vector3 a,Vector3 b,Vector3 c)
    {
        float cross=(b.X-a.X)*(c.Z-a.Z)-(b.Z-a.Z)*(c.X-a.X);if(Math.Abs(cross)<1e-7f)return;
        float sx=(count-1)/Math.Max(.1f,Size.X),sz=(count-1)/Math.Max(.1f,Size.Y);
        int x0=Math.Max(0,(int)MathF.Ceiling((Math.Min(a.X,Math.Min(b.X,c.X))-Origin.X)*sx)),x1=Math.Min(count-1,(int)MathF.Floor((Math.Max(a.X,Math.Max(b.X,c.X))-Origin.X)*sx));
        int z0=Math.Max(0,(int)MathF.Ceiling((Math.Min(a.Z,Math.Min(b.Z,c.Z))-Origin.Y)*sz)),z1=Math.Min(count-1,(int)MathF.Floor((Math.Max(a.Z,Math.Max(b.Z,c.Z))-Origin.Y)*sz));
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){
            float px=Origin.X+x/sx-a.X,pz=Origin.Y+z/sz-a.Z;
            float u=(px*(c.Z-a.Z)-pz*(c.X-a.X))/cross,v=((b.X-a.X)*pz-(b.Z-a.Z)*px)/cross;
            if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
            heights![z*count+x]=Math.Max(heights[z*count+x],Math.Clamp(a.Y+u*(b.Y-a.Y)+v*(c.Y-a.Y),MinimumHeight,MaximumHeight));
        }TrianglesBaked++;
    }
    public float SampleHeight(Vector2 point)
    {
        Bake();var uv=(point-Origin)/Size;if(uv.X<0||uv.Y<0||uv.X>1||uv.Y>1)return DefaultHeight;
        float fx=uv.X*(count-1),fz=uv.Y*(count-1);int x=(int)fx,z=(int)fz,x1=Math.Min(x+1,count-1),z1=Math.Min(z+1,count-1);
        return float.Lerp(float.Lerp(heights![z*count+x],heights[z*count+x1],fx-x),float.Lerp(heights[z1*count+x],heights[z1*count+x1],fx-x),fz-z);
    }
    public Texture2D Upload()
    {
        Bake();if(Texture!=null)return Texture;byte[] pixels=new byte[count*count*4];float range=Math.Max(.01f,MaximumHeight-MinimumHeight);
        for(int i=0;i<heights!.Length;i++){int value=(int)MathF.Round(Math.Clamp((heights[i]-MinimumHeight)/range,0,1)*65535);pixels[i*4]=(byte)(value>>8);pixels[i*4+1]=(byte)value;pixels[i*4+3]=255;}
        return Texture=Texture2D.FromPixels(count,count,pixels);
    }
    protected override void OnDestroy()=>Texture?.Dispose();
}
