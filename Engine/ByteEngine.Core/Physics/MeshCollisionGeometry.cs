using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Physics;
internal sealed class MeshCollisionGeometry
{
 internal readonly Vector3[] Points;
 internal readonly uint[] Indices;
 internal readonly bool Convex;
 internal readonly bool ValidConvex;
 internal readonly BoundingBox3D Bounds;
 internal readonly Vector3 Center;
 private sealed record Node(BoundingBox3D Bounds,int Triangle,Node? Left=null,Node? Right=null);
 private readonly Node? _tree;
 internal MeshCollisionGeometry(Vector3[] points,uint[] indices,bool convex)
 {
  Points=points;Indices=indices;Convex=convex;Bounds=new(points.Aggregate(Vector3.Min),points.Aggregate(Vector3.Max));Center=points.Aggregate(Vector3.Zero,(a,b)=>a+b)/points.Length;
  _tree=Build(Enumerable.Range(0,indices.Length/3).ToArray());
  if(convex&&points.Length<=128&&indices.Length/3<=256)
  {
   bool valid=true;float volume=0;var edges=new Dictionary<(uint,uint),int>();
   for(int t=0;t<indices.Length/3;t++){var (a,b,c)=Triangle(t);var cross=Vector3.Cross(b-a,c-a);volume+=Math.Abs(Vector3.Dot(a-Center,cross))/6f;var normal=Vector3.Normalize(cross);if(Vector3.Dot(normal,Center-a)>0)normal=-normal;if(points.Any(p=>Vector3.Dot(normal,p-a)>1e-4f)){valid=false;break;}for(int edge=0;edge<3;edge++){uint u=indices[t*3+edge],v=indices[t*3+(edge+1)%3];var key=(Math.Min(u,v),Math.Max(u,v));edges[key]=edges.GetValueOrDefault(key)+1;}}
   ValidConvex=valid&&volume>1e-9f&&edges.Values.All(count=>count==2);
  }
 }
 internal (Vector3 A,Vector3 B,Vector3 C) Triangle(int t)=>(Points[Indices[t*3]],Points[Indices[t*3+1]],Points[Indices[t*3+2]]);
 private BoundingBox3D TriangleBounds(int t){var (a,b,c)=Triangle(t);return new(Vector3.Min(a,Vector3.Min(b,c)),Vector3.Max(a,Vector3.Max(b,c)));}
 private Node? Build(int[] order)
 {
  if(order.Length==0)return null;var bounds=TriangleBounds(order[0]);foreach(int t in order.Skip(1)){var b=TriangleBounds(t);bounds=new(Vector3.Min(bounds.Minimum,b.Minimum),Vector3.Max(bounds.Maximum,b.Maximum));}
  if(order.Length==1)return new(bounds,order[0]);var span=bounds.Size;int axis=span.X>=span.Y&&span.X>=span.Z?0:span.Y>=span.Z?1:2;
  float Position(int t){var p=TriangleBounds(t).Center;return axis==0?p.X:axis==1?p.Y:p.Z;}
  Array.Sort(order,(a,b)=>{int c=Position(a).CompareTo(Position(b));return c!=0?c:a.CompareTo(b);});int middle=order.Length/2;return new(bounds,-1,Build(order[..middle]),Build(order[middle..]));
 }
 private static bool Intersects(BoundingBox3D a,BoundingBox3D b)=>a.Minimum.X<=b.Maximum.X&&a.Maximum.X>=b.Minimum.X&&a.Minimum.Y<=b.Maximum.Y&&a.Maximum.Y>=b.Minimum.Y&&a.Minimum.Z<=b.Maximum.Z&&a.Maximum.Z>=b.Minimum.Z;
 internal IEnumerable<int> Candidates(BoundingBox3D bounds)
 {
  var stack=new Stack<Node>();if(_tree!=null)stack.Push(_tree);while(stack.Count>0){var n=stack.Pop();if(!Intersects(n.Bounds,bounds))continue;if(n.Triangle>=0)yield return n.Triangle;else{if(n.Right!=null)stack.Push(n.Right);if(n.Left!=null)stack.Push(n.Left);}}
 }
 internal bool Contains(Vector3 point,out Vector3 normal,out float signedDistance)
 {
  normal=Vector3.UnitY;signedDistance=float.NegativeInfinity;if(!ValidConvex)return false;
  for(int i=0;i<Indices.Length/3;i++){var (a,b,c)=Triangle(i);var n=Vector3.Normalize(Vector3.Cross(b-a,c-a));if(Vector3.Dot(n,Center-a)>0)n=-n;float d=Vector3.Dot(n,point-a);if(d>1e-5f)return false;if(d>signedDistance){signedDistance=d;normal=n;}}return true;
 }
 internal float SegmentDistance(Vector3 a,Vector3 b,out Vector3 onSegment,out Vector3 onMesh)
 {
  Vector3 segmentPoint=a,meshPoint=Bounds.Center;float best=float.PositiveInfinity;var segmentBounds=new BoundingBox3D(Vector3.Min(a,b),Vector3.Max(a,b));
  void Visit(Node? node)
  {
   if(node==null)return;var gap=Vector3.Max(Vector3.Zero,Vector3.Max(node.Bounds.Minimum-segmentBounds.Maximum,segmentBounds.Minimum-node.Bounds.Maximum));if(gap.LengthSquared()>best)return;
   if(node.Triangle<0){Visit(node.Left);Visit(node.Right);return;}var (x,y,z)=Triangle(node.Triangle);ClosestSegmentTriangle(a,b,x,y,z,out var p,out var q);float squared=Vector3.DistanceSquared(p,q);if(squared<best){best=squared;segmentPoint=p;meshPoint=q;}
  }
  Visit(_tree);onSegment=segmentPoint;onMesh=meshPoint;return MathF.Sqrt(best);
 }
 internal bool Cast(Vector3 origin,Vector3 direction,float radius,out float distance,out Vector3 normal)
 {
  distance=0;normal=-direction;if(direction.LengthSquared()<1e-12f||radius<0)return false;direction=Vector3.Normalize(direction);
  Vector3 min=Bounds.Minimum-new Vector3(radius),max=Bounds.Maximum+new Vector3(radius);float enter=0,exit=float.PositiveInfinity;
  for(int axis=0;axis<3;axis++){float o=axis==0?origin.X:axis==1?origin.Y:origin.Z,d=axis==0?direction.X:axis==1?direction.Y:direction.Z,low=axis==0?min.X:axis==1?min.Y:min.Z,high=axis==0?max.X:axis==1?max.Y:max.Z;if(Math.Abs(d)<1e-9f){if(o<low||o>high)return false;continue;}float a=(low-o)/d,b=(high-o)/d;if(a>b)(a,b)=(b,a);enter=Math.Max(enter,a);exit=Math.Min(exit,b);if(exit<enter)return false;}
  float t=enter;
  for(int step=0;step<128&&t<=exit+.0001f;step++)
  {
   Vector3 p=origin+direction*t;if(Contains(p,out var inside,out _)){distance=t;normal=inside;return true;}
   float separation=SegmentDistance(p,p,out _,out var closest)-radius;
   if(separation<=.0001f){distance=t;var n=p-closest;normal=n.LengthSquared()>1e-10f?Vector3.Normalize(n):-direction;return true;}t+=Math.Max(.00005f,separation*.95f);
  }
  return false;
 }
 internal static Vector3 ClosestTriangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
 {
  var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
  var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
  var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));float inverse=1/(va+vb+vc);return a+ab*(vb*inverse)+ac*(vc*inverse);
 }
 private static void ClosestSegments(Vector3 p1,Vector3 q1,Vector3 p2,Vector3 q2,out Vector3 c1,out Vector3 c2)
 {
  var d1=q1-p1;var d2=q2-p2;var r=p1-p2;float a=Vector3.Dot(d1,d1),e=Vector3.Dot(d2,d2),f=Vector3.Dot(d2,r),s,t;
  if(a<=1e-12f&&e<=1e-12f){c1=p1;c2=p2;return;}if(a<=1e-12f){s=0;t=Math.Clamp(f/e,0,1);}else{float c=Vector3.Dot(d1,r);if(e<=1e-12f){t=0;s=Math.Clamp(-c/a,0,1);}else{float b=Vector3.Dot(d1,d2),den=a*e-b*b;s=den>1e-12f?Math.Clamp((b*f-c*e)/den,0,1):0;t=(b*s+f)/e;if(t<0){t=0;s=Math.Clamp(-c/a,0,1);}else if(t>1){t=1;s=Math.Clamp((b-c)/a,0,1);}}}c1=p1+d1*s;c2=p2+d2*t;
 }
 private static void ClosestSegmentTriangle(Vector3 p,Vector3 q,Vector3 a,Vector3 b,Vector3 c,out Vector3 first,out Vector3 second)
 {
  var n=Vector3.Cross(b-a,c-a);float denominator=Vector3.Dot(n,q-p);
  if(Math.Abs(denominator)>1e-10f){float t=Vector3.Dot(n,a-p)/denominator;if(t>=0&&t<=1){var x=p+(q-p)*t;var closest=ClosestTriangle(x,a,b,c);if(Vector3.DistanceSquared(x,closest)<1e-10f){first=second=x;return;}}}
  Vector3 bestP=p,bestQ=ClosestTriangle(p,a,b,c);float best=Vector3.DistanceSquared(bestP,bestQ);var end=ClosestTriangle(q,a,b,c);if(Vector3.DistanceSquared(q,end)<best){bestP=q;bestQ=end;best=Vector3.DistanceSquared(q,end);}foreach(var edge in new[]{(a,b),(b,c),(c,a)}){ClosestSegments(p,q,edge.Item1,edge.Item2,out var x,out var y);float d=Vector3.DistanceSquared(x,y);if(d<best){best=d;bestP=x;bestQ=y;}}first=bestP;second=bestQ;
 }
}
