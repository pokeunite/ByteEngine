using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Navigation;
internal static class NavigationDebugGeometry
{
 internal static Mesh Quads(IEnumerable<(Vector3 A,Vector3 B,Vector3 C,Vector3 D)> quads)
 {
  var vertices=new List<float>();var indices=new List<uint>();foreach(var q in quads){uint n=(uint)(vertices.Count/8);foreach(var p in new[]{q.A,q.B,q.C,q.D})vertices.AddRange(new[]{p.X,p.Y,p.Z,0f,1f,0f,0f,0f});indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}return new(vertices.ToArray(),indices.ToArray());
 }
 internal static readonly Material Surface=new(){BaseColor=new(.1f,.7f,.35f,.3f),Shading=MaterialShadingMode.Unlit,BlendMode=BlendMode3D.AlphaBlend,DepthWriteMode=DepthWriteMode3D.Disabled,CullMode=CullMode3D.None};
}
