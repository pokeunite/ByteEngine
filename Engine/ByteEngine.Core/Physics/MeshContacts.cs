using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Physics;
internal static class MeshContacts
{
 internal static bool Contact(MeshCollider3D mesh,Collider3D other,out Vector3 point,out Vector3 normal,out float penetration)
 {
  point=default;normal=Vector3.UnitY;penetration=0;var geometry=mesh.Geometry;if(geometry==null)return false;
  if(mesh.Mode==MeshCollisionMode.StaticTriangles&&Body(mesh)?.BodyType==RigidbodyBodyType3D.Dynamic)return false;
  if(other is CapsuleCollider3D capsule)
  {
   var center=Vector3.Transform(capsule.Center,capsule.Transform.WorldMatrix);float radius=capsule.Radius*Math.Max(Math.Abs(capsule.Transform.WorldScale.X),Math.Abs(capsule.Transform.WorldScale.Z));float half=Math.Max(0,capsule.Height*Math.Abs(capsule.Transform.WorldScale.Y)*.5f-radius);var axis=Vector3.Normalize(capsule.Transform.Up);var a=center-axis*half;var b=center+axis*half;
   if(geometry.Contains(center,out normal,out float signed)){penetration=radius-signed;point=center-normal*signed;return true;}
   float distance=geometry.SegmentDistance(a,b,out var onCapsule,out point);if(distance>radius+.00001f)return false;normal=distance>1e-6f?(onCapsule-point)/distance:Vector3.Normalize(center-geometry.Center);if(!float.IsFinite(normal.X+normal.Y+normal.Z))normal=Vector3.UnitY;penetration=Math.Max(0,radius-distance);return true;
  }
  Vector3[] points;List<Vector3> axes,edges;
  if(other is BoxCollider3D box)
  {
   var center=Vector3.Transform(box.Center,box.Transform.WorldMatrix);var half=Vector3.Abs(box.Size*box.Transform.WorldScale)*.5f;var rotation=Matrix4x4.CreateFromQuaternion(box.Transform.WorldRotation);var x=Vector3.TransformNormal(Vector3.UnitX,rotation);var y=Vector3.TransformNormal(Vector3.UnitY,rotation);var z=Vector3.TransformNormal(Vector3.UnitZ,rotation);points=new Vector3[8];int i=0;for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)for(int c=-1;c<=1;c+=2)points[i++]=center+x*half.X*a+y*half.Y*b+z*half.Z*c;axes=[x,y,z];edges=[x,y,z];
  }
  else if(other is MeshCollider3D otherMesh&&otherMesh.Geometry is {} otherGeometry)
  {
   if(!otherGeometry.Convex){if(!geometry.Convex)return false;bool found=Contact(otherMesh,mesh,out point,out normal,out penetration);normal=-normal;return found;}
   points=otherGeometry.Points;Axes(otherGeometry,out axes,out edges);
  }
  else return false;
  var bounds=new BoundingBox3D(points.Aggregate(Vector3.Min),points.Aggregate(Vector3.Max));
  if(geometry.Convex){Axes(geometry,out var firstAxes,out var firstEdges);bool hit=Sat(geometry.Points,points,firstAxes,axes,firstEdges,edges,out point,out normal,out penetration);if(hit)geometry.SegmentDistance(bounds.Center,bounds.Center,out _,out point);return hit;}
  bool result=false;
  foreach(int triangle in geometry.Candidates(bounds))
  {
   var (a,b,c)=geometry.Triangle(triangle);var triEdges=new List<Vector3>{b-a,c-b,a-c};var triAxes=new List<Vector3>{Vector3.Cross(b-a,c-a)};
   if(Sat([a,b,c],points,triAxes,axes,triEdges,edges,out var hitPoint,out var hitNormal,out float depth)&&(!result||depth>penetration)){result=true;point=MeshCollisionGeometry.ClosestTriangle(bounds.Center,a,b,c);normal=hitNormal;penetration=depth;}
  }
  return result;
 }
 private static Rigidbody3D? Body(MeshCollider3D collider){for(var owner=collider.GameObject;owner!=null;owner=owner.Parent)if(owner.GetComponent<Rigidbody3D>() is {Enabled:true} body)return body;return null;}
 private static void AddAxis(List<Vector3> axes,Vector3 axis){if(axis.LengthSquared()<1e-10f)return;axis=Vector3.Normalize(axis);if(!axes.Any(a=>Math.Abs(Vector3.Dot(a,axis))>.99999f))axes.Add(axis);}
 private static void Axes(MeshCollisionGeometry geometry,out List<Vector3> normals,out List<Vector3> edges)
 {
  normals=[];edges=[];for(int i=0;i<geometry.Indices.Length/3;i++){var (a,b,c)=geometry.Triangle(i);AddAxis(normals,Vector3.Cross(b-a,c-a));AddAxis(edges,b-a);AddAxis(edges,c-b);AddAxis(edges,a-c);}
 }
 private static bool Sat(Vector3[] a,Vector3[] b,List<Vector3> axesA,List<Vector3> axesB,List<Vector3> edgesA,List<Vector3> edgesB,out Vector3 point,out Vector3 normal,out float penetration)
 {
  point=default;normal=Vector3.UnitY;penetration=float.PositiveInfinity;var axes=new List<Vector3>();foreach(var axis in axesA.Concat(axesB))AddAxis(axes,axis);foreach(var ea in edgesA)foreach(var eb in edgesB)AddAxis(axes,Vector3.Cross(ea,eb));
  foreach(var axis in axes)
  {
   float aMin=float.PositiveInfinity,aMax=float.NegativeInfinity,bMin=float.PositiveInfinity,bMax=float.NegativeInfinity;foreach(var p in a){float d=Vector3.Dot(p,axis);aMin=Math.Min(aMin,d);aMax=Math.Max(aMax,d);}foreach(var p in b){float d=Vector3.Dot(p,axis);bMin=Math.Min(bMin,d);bMax=Math.Max(bMax,d);}
   if(aMax<bMin-1e-5f||bMax<aMin-1e-5f){penetration=0;return false;}float positive=aMax-bMin,negative=bMax-aMin,overlap=Math.Max(0,Math.Min(positive,negative));if(overlap<penetration){penetration=overlap;normal=positive<=negative?axis:-axis;}
  }
  if(!float.IsFinite(penetration))return false;
  var contactNormal=normal;var onA=a.OrderByDescending(p=>Vector3.Dot(p,contactNormal)).First();var onB=b.OrderBy(p=>Vector3.Dot(p,contactNormal)).First();
  // Average supporting vertices rather than choosing a corner on coplanar faces.
  float maxA=Vector3.Dot(onA,normal),minB=Vector3.Dot(onB,normal);var supportA=a.Where(p=>Math.Abs(Vector3.Dot(p,contactNormal)-maxA)<1e-4f).ToArray();var supportB=b.Where(p=>Math.Abs(Vector3.Dot(p,contactNormal)-minB)<1e-4f).ToArray();point=(supportA.Aggregate(Vector3.Zero,(x,y)=>x+y)/supportA.Length+supportB.Aggregate(Vector3.Zero,(x,y)=>x+y)/supportB.Length)*.5f;return true;
 }
}
