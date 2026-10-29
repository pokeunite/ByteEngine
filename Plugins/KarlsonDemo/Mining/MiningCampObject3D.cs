using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
/// <summary>Marks an authored machine/prop as movable without recreating its gameplay components.</summary>
public sealed class MiningCampObject3D:Component
{
 public string DisplayName{get;set;}="Camp object";
 public bool AllowRelocation{get;set;}=true;
 public float Clearance{get;set;}=.30f;
}
internal static class MiningCampGeometry
{
 internal static bool Under(GameObject o,GameObject root){for(var p=o;p!=null;p=p.Parent)if(p==root)return true;return false;}
 internal static BoundingBox3D Transform(BoundingBox3D box,Matrix4x4 matrix)
 {
  var min=new Vector3(float.PositiveInfinity);var max=new Vector3(float.NegativeInfinity);
  for(int i=0;i<8;i++){var p=Vector3.Transform(new((i&1)==0?box.Minimum.X:box.Maximum.X,(i&2)==0?box.Minimum.Y:box.Maximum.Y,(i&4)==0?box.Minimum.Z:box.Maximum.Z),matrix);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
  return new(min,max);
 }
 internal static BoundingBox3D ColliderBounds(Collider3D collider)=>Transform(new(collider.Center-collider.Size*.5f,collider.Center+collider.Size*.5f),collider.Transform.WorldMatrix);
 internal static BoundingBox3D LocalBounds(GameObject root)
 {
  Matrix4x4.Invert(root.Transform.WorldMatrix,out var inverse);var min=new Vector3(float.PositiveInfinity);var max=new Vector3(float.NegativeInfinity);
  void Add(BoundingBox3D box){min=Vector3.Min(min,box.Minimum);max=Vector3.Max(max,box.Maximum);}
  foreach(var o in root.Scene!.GameObjects.Where(o=>o.ActiveInHierarchy&&Under(o,root)))
  {
   var toRoot=o.Transform.WorldMatrix*inverse;
   if(o.GetComponent<SkeletalMeshRenderer>() is {Visible:true} skin&&skin.TryGetCurrentModelBounds(out var b))Add(Transform(b,toRoot));
   if(o.GetComponent<MeshRenderer>() is {Visible:true} mesh){var mb=mesh.Mesh?.LocalBounds??new(new(-.5f),new(.5f));if(mesh.UsePrimitive||mesh.Mesh!=null)Add(Transform(mb,toRoot));}
   foreach(var c in o.Components.OfType<Collider3D>().Where(c=>c.Enabled&&!c.IsTrigger))Add(Transform(new(c.Center-c.Size*.5f,c.Center+c.Size*.5f),toRoot));
  }
  return float.IsFinite(min.X)?new(min,max):new(new(-.6f,0,-.6f),new(.6f,1.5f,.6f));
 }
 internal static float SurfaceDistance(GameObject root,Vector3 point)
 {
  float distance=float.PositiveInfinity;
  foreach(var o in Hierarchy(root).Where(o=>o.ActiveInHierarchy))foreach(var c in o.Components.OfType<Collider3D>().Where(c=>c.Enabled&&!c.IsTrigger))
  {
   var b=ColliderBounds(c);if(b.Minimum.Y>point.Y+2.2f)continue;
   float x=Math.Clamp(point.X,b.Minimum.X,b.Maximum.X),z=Math.Clamp(point.Z,b.Minimum.Z,b.Maximum.Z);distance=Math.Min(distance,Vector2.Distance(new(point.X,point.Z),new(x,z)));
  }
  return float.IsFinite(distance)?distance:Vector3.Distance(root.Transform.WorldPosition,point);
 }
 static IEnumerable<GameObject> Hierarchy(GameObject root){yield return root;foreach(var child in root.Children)foreach(var item in Hierarchy(child))yield return item;}
}
