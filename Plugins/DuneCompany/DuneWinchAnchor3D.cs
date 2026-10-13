using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics.ThreeD;
namespace DuneCompany;
/// <summary>A fixed, scene-authored cable attachment; placed on a solid anchor rock.</summary>
public sealed class DuneWinchAnchor3D:Component
{
 public string Label{get;set;}="Rock anchor";
 protected override void OnStart(){if(GameObject.GetComponent<DuneAttachmentEye3D>()==null)GameObject.AddComponent(new DuneAttachmentEye3D());}
}

/// <summary>Visible steel cable eye. The attachment/collision transform never moves.</summary>
public sealed class DuneAttachmentEye3D:Component
{
 public Vector4 Tint{get;set;}=new(.48f,.5f,.44f,1);
 Mesh? _mesh;
 protected override void OnStart(){
  var vertices=new List<float>();var indices=new List<uint>();const int rings=32,sides=8;
  for(int i=0;i<=rings;i++){float a=MathF.Tau*i/rings;var radial=new Vector3(MathF.Cos(a),MathF.Sin(a),0);for(int j=0;j<sides;j++){float b=MathF.Tau*j/sides;var normal=radial*MathF.Cos(b)+Vector3.UnitZ*MathF.Sin(b);var p=radial*.34f+normal*.10f;vertices.AddRange([p.X,p.Y,p.Z,normal.X,normal.Y,normal.Z,0,0]);if(i<rings){uint x=(uint)(i*sides+j),y=(uint)(i*sides+(j+1)%sides);indices.AddRange([x,y,x+sides,y,y+sides,x+sides]);}}}
  _mesh=new(vertices.ToArray(),indices.ToArray());var renderer=GameObject.GetComponent<MeshRenderer>()??GameObject.AddComponent(new MeshRenderer());renderer.UsePrimitive=false;renderer.Mesh=_mesh;renderer.CastShadows=false;renderer.Material=new(){BaseColor=Tint,Metallic=.8f,Roughness=.38f};
 }
 protected override void OnDestroy(){_mesh?.Dispose();_mesh=null;}
}
