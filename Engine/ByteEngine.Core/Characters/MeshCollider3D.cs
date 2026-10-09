using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
namespace ByteEngine.Core.Characters;
public enum MeshCollisionMode { StaticTriangles, Convex }
/// <summary>Static triangle soup or a closed convex hull, using authored CPU geometry or an imported mesh.
/// Rendering LODs never replace collision geometry. Convex mode validates its hull and is capped for solver cost.</summary>
public sealed class MeshCollider3D:Collider3D
{
 private Vector3[] _vertices=[];private uint[] _triangles=[];private Vector3 _size=Vector3.One;
 private int _revision,_builtRevision=-1;private Matrix4x4 _matrix;private Vector3 _geometryCenter;private object? _model;
 private AssetReference _modelReference=AssetReference.Empty;private string _meshKey=string.Empty;
 private MeshCollisionMode _mode;
 internal MeshCollisionGeometry? Geometry {get{try{EnsureGeometry();}catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException){_geometry=null;GeometryStatus=e.Message;}return _geometry;}}
 private MeshCollisionGeometry? _geometry;
 public AssetReference Model {get=>_modelReference;set{_modelReference=value??AssetReference.Empty;_model=null;_revision++;}}
 public string MeshKey {get=>_meshKey;set{_meshKey=value??string.Empty;_model=null;_revision++;}}
 public MeshCollisionMode Mode {get=>_mode;set{_mode=value;_revision++;}}
 public Vector3[] Vertices {get=>_vertices.ToArray();set{_vertices=(value??[]).ToArray();_revision++;}}
 public uint[] Triangles {get=>_triangles.ToArray();set{_triangles=(value??[]).ToArray();_revision++;}}
 /// <summary>Multiplier for source coordinates. Object scale and Center are applied separately.</summary>
 public override Vector3 Size {get=>_size;set{_size=value;_revision++;}}
 public string GeometryStatus {get;private set;}="Not prepared";
 internal BoundingBox3D WorldBounds=>Geometry?.Bounds??new(Transform.WorldPosition,Transform.WorldPosition);
 private void EnsureGeometry()
 {
  Vector3[] vertices=_vertices;uint[] triangles=_triangles;
  if(!Model.IsEmpty&&AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null)
  {
   var model=assets.LoadModel(Model);if(!ReferenceEquals(model,_model)){_model=model;_revision++;}
   var mesh=model.Meshes.FirstOrDefault(m=>m.Key==MeshKey)??(string.IsNullOrWhiteSpace(MeshKey)?model.Meshes.FirstOrDefault():null);
   if(mesh==null){_geometry=null;GeometryStatus="Mesh not found";return;}
   if(_builtRevision==_revision&&_matrix==Transform.WorldMatrix&&_geometryCenter==Center)return;
   vertices=new Vector3[mesh.Vertices.Length/8];for(int i=0;i<vertices.Length;i++)vertices[i]=new(mesh.Vertices[i*8],mesh.Vertices[i*8+1],mesh.Vertices[i*8+2]);triangles=mesh.Indices;
  }
  var matrix=Transform.WorldMatrix;if(_builtRevision==_revision&&_matrix==matrix&&_geometryCenter==Center)return;
  _builtRevision=_revision;_matrix=matrix;_geometryCenter=Center;_geometry=null;
  if(vertices.Length<3||triangles.Length<3||triangles.Length%3!=0||triangles.Any(i=>i>=vertices.Length)||vertices.Any(p=>!float.IsFinite(p.X+p.Y+p.Z))||!float.IsFinite(Size.X+Size.Y+Size.Z)||Math.Abs(matrix.GetDeterminant())<1e-10f){GeometryStatus="Invalid or empty mesh geometry";return;}
  var welded=new List<Vector3>();var map=new Dictionary<Vector3,uint>();var remap=new uint[vertices.Length];
  for(int i=0;i<vertices.Length;i++){var p=Vector3.Transform(vertices[i]*Size+Center,matrix);if(!map.TryGetValue(p,out uint index)){index=(uint)welded.Count;map[p]=index;welded.Add(p);}remap[i]=index;}
  var faces=new List<uint>();for(int i=0;i<triangles.Length;i+=3){uint a=remap[triangles[i]],b=remap[triangles[i+1]],c=remap[triangles[i+2]];if(a==b||a==c||b==c||Vector3.Cross(welded[(int)b]-welded[(int)a],welded[(int)c]-welded[(int)a]).LengthSquared()<1e-12f)continue;faces.AddRange([a,b,c]);}
  if(faces.Count==0){GeometryStatus="Mesh has no valid triangles";return;}
  var geometry=new MeshCollisionGeometry(welded.ToArray(),faces.ToArray(),Mode==MeshCollisionMode.Convex);
  if(Mode==MeshCollisionMode.Convex&&!geometry.ValidConvex){GeometryStatus="Convex requires a closed convex hull (max 128 vertices / 256 triangles)";return;}
  _geometry=geometry;GeometryStatus=$"{Mode}: {welded.Count} vertices / {faces.Count/3} triangles";
 }
}
