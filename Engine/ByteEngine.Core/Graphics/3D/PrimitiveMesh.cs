using System.Numerics;
namespace ByteEngine.Core.Graphics.ThreeD;
public enum PrimitiveMeshType { Cube, Plane, Sphere }
public static class PrimitiveMesh
{
    public static Mesh Create(PrimitiveMeshType type) => type switch { PrimitiveMeshType.Cube=>CreateCube(), PrimitiveMeshType.Plane=>CreatePlane(), PrimitiveMeshType.Sphere=>CreateSphere(), _=>throw new ArgumentOutOfRangeException(nameof(type)) };
    private static Mesh CreateCube()
    {
        var v=new List<float>(); var i=new List<uint>();
        AddFace(v,i,new(-.5f,-.5f,.5f),new(.5f,-.5f,.5f),new(.5f,.5f,.5f),new(-.5f,.5f,.5f),new(0,0,1));
        AddFace(v,i,new(.5f,-.5f,-.5f),new(-.5f,-.5f,-.5f),new(-.5f,.5f,-.5f),new(.5f,.5f,-.5f),new(0,0,-1));
        AddFace(v,i,new(.5f,-.5f,.5f),new(.5f,-.5f,-.5f),new(.5f,.5f,-.5f),new(.5f,.5f,.5f),new(1,0,0));
        AddFace(v,i,new(-.5f,-.5f,-.5f),new(-.5f,-.5f,.5f),new(-.5f,.5f,.5f),new(-.5f,.5f,-.5f),new(-1,0,0));
        AddFace(v,i,new(-.5f,.5f,.5f),new(.5f,.5f,.5f),new(.5f,.5f,-.5f),new(-.5f,.5f,-.5f),new(0,1,0));
        AddFace(v,i,new(-.5f,-.5f,-.5f),new(.5f,-.5f,-.5f),new(.5f,-.5f,.5f),new(-.5f,-.5f,.5f),new(0,-1,0)); return new(v.ToArray(),i.ToArray());
    }
    private static Mesh CreatePlane()=>new(new float[]{-.5f,0,-.5f,0,1,0,0,0,.5f,0,-.5f,0,1,0,1,0,.5f,0,.5f,0,1,0,1,1,-.5f,0,.5f,0,1,0,0,1},new uint[]{0,2,1,0,3,2});
    private static Mesh CreateSphere(int segments=24,int rings=16)
    {
        var v=new List<float>(); var ids=new List<uint>();
        for(int y=0;y<=rings;y++){float fy=(float)y/rings,phi=fy*MathF.PI;for(int x=0;x<=segments;x++){float fx=(float)x/segments,theta=fx*MathF.Tau;float nx=MathF.Sin(phi)*MathF.Cos(theta),ny=MathF.Cos(phi),nz=MathF.Sin(phi)*MathF.Sin(theta);v.AddRange(new[]{nx*.5f,ny*.5f,nz*.5f,nx,ny,nz,fx,1-fy});}}
        for(int y=0;y<rings;y++)for(int x=0;x<segments;x++){uint a=(uint)(y*(segments+1)+x),b=a+(uint)segments+1;ids.AddRange(new[]{a,b,a+1,a+1,b,b+1});} return new(v.ToArray(),ids.ToArray());
    }
    private static void AddFace(List<float> v,List<uint> i,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n){uint s=(uint)(v.Count/8);Add(v,a,n,0,0);Add(v,b,n,1,0);Add(v,c,n,1,1);Add(v,d,n,0,1);i.AddRange(new[]{s,s+1,s+2,s,s+2,s+3});}
    private static void Add(List<float> v,Vector3 p,Vector3 n,float u,float w)=>v.AddRange(new[]{p.X,p.Y,p.Z,n.X,n.Y,n.Z,u,w});
}
