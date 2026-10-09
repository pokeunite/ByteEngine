using System.Numerics;
using ByteEngine.Core.Physics;
namespace ByteEngine.Core.Navigation;
/// <summary>Static triangle-surface navigation with 3D adjacency, clearance portals and authored links.
/// Obstacle rejection is conservative; author sufficiently tessellated walkable geometry for detailed cuts.</summary>
public sealed class NavigationMesh
{
 public readonly record struct Polygon(Vector3 A,Vector3 B,Vector3 C){public Vector3 Center=>(A+B+C)/3;}
 private readonly List<Polygon> _polygons=[];
 private readonly List<List<Arc>> _adjacency=[];
 private readonly record struct Arc(int Target,Vector3 Entry,Vector3 Exit,float Cost);
 public IReadOnlyList<Polygon> Polygons=>_polygons;
 public static NavigationMesh Bake(Vector3[] vertices,uint[] indices,float maximumSlope,float radius,float height,IEnumerable<(Vector3 Minimum,Vector3 Maximum)> obstacles)
 {
  var mesh=new NavigationMesh();var blocked=obstacles.ToArray();float up=MathF.Cos(Math.Clamp(float.IsFinite(maximumSlope)?maximumSlope:45,0,89)*MathF.PI/180);radius=Math.Max(0,float.IsFinite(radius)?radius:0);height=Math.Max(.1f,float.IsFinite(height)?height:1.8f);
  var edges=new Dictionary<(Vector3 A,Vector3 B),List<int>>();
  var unique=new HashSet<(Vector3 A,Vector3 B,Vector3 C)>();
  int Compare(Vector3 a,Vector3 b){int x=a.X.CompareTo(b.X);if(x!=0)return x;int y=a.Y.CompareTo(b.Y);return y!=0?y:a.Z.CompareTo(b.Z);}
  for(int i=0;i+2<indices.Length;i+=3)
  {
   if(indices[i]>=vertices.Length||indices[i+1]>=vertices.Length||indices[i+2]>=vertices.Length)continue;
   var polygon=new Polygon(vertices[indices[i]],vertices[indices[i+1]],vertices[indices[i+2]]);
   var ordered=new[]{polygon.A,polygon.B,polygon.C};Array.Sort(ordered,Comparer<Vector3>.Create(Compare));if(!unique.Add((ordered[0],ordered[1],ordered[2])))continue;
   var normal=Vector3.Cross(polygon.B-polygon.A,polygon.C-polygon.A);if(!float.IsFinite(normal.X+normal.Y+normal.Z)||normal.LengthSquared()<1e-10f||Math.Abs(Vector3.Normalize(normal).Y)<up)continue;
   var min=Vector3.Min(polygon.A,Vector3.Min(polygon.B,polygon.C));var max=Vector3.Max(polygon.A,Vector3.Max(polygon.B,polygon.C));
   if(blocked.Any(b=>b.Maximum.Y>min.Y+.05f&&b.Minimum.Y<max.Y+height&&b.Minimum.X<max.X&&b.Maximum.X>min.X&&b.Minimum.Z<max.Z&&b.Maximum.Z>min.Z))continue;
   int index=mesh._polygons.Count;mesh._polygons.Add(polygon);mesh._adjacency.Add([]);
   foreach(var pair in new[]{(polygon.A,polygon.B),(polygon.B,polygon.C),(polygon.C,polygon.A)})
   {
    var key=Compare(pair.Item1,pair.Item2)<0?pair:(pair.Item2,pair.Item1);
    if(!edges.TryGetValue(key,out var owners))edges[key]=owners=[];owners.Add(index);
   }
  }
  foreach(var edge in edges)
  {
   if(edge.Value.Count!=2||Vector3.Distance(edge.Key.A,edge.Key.B)<=radius*2+.001f)continue;
   int a=edge.Value[0],b=edge.Value[1];var portal=(edge.Key.A+edge.Key.B)*.5f;
   float cost=Vector3.Distance(mesh._polygons[a].Center,portal)+Vector3.Distance(portal,mesh._polygons[b].Center);
   mesh._adjacency[a].Add(new(b,portal,portal,cost));mesh._adjacency[b].Add(new(a,portal,portal,cost));
  }
  return mesh;
 }
 private int Nearest(Vector3 point,out Vector3 onSurface)
 {
  int nearest=-1;float best=float.PositiveInfinity;onSurface=point;
  for(int i=0;i<_polygons.Count;i++){var p=_polygons[i];var closest=MeshCollisionGeometry.ClosestTriangle(point,p.A,p.B,p.C);float squared=Vector3.DistanceSquared(point,closest);if(squared<best){best=squared;nearest=i;onSurface=closest;}}
  return nearest;
 }
 public bool AddLink(Vector3 start,Vector3 end,float cost=1,bool bidirectional=true)
 {
  int a=Nearest(start,out var from),b=Nearest(end,out var to);if(a<0||b<0||a==b)return false;
  if(Vector3.Distance(from,start)>1||Vector3.Distance(to,end)>1)return false;
  cost=Math.Max(.001f,float.IsFinite(cost)?cost:1);_adjacency[a].Add(new(b,from,to,cost));if(bidirectional)_adjacency[b].Add(new(a,to,from,cost));return true;
 }
 public IReadOnlyList<Vector3> FindPath(Vector3 start,Vector3 goal,int maximumVisited=10000)
 {
  if(!float.IsFinite(start.X+start.Y+start.Z+goal.X+goal.Y+goal.Z))return [];
  int first=Nearest(start,out var from),last=Nearest(goal,out var to);if(first<0||last<0||Vector3.Distance(from,start)>2||Vector3.Distance(to,goal)>2)return [];
  if(first==last)return [from,to];
  var queue=new PriorityQueue<int,float>();var costs=new Dictionary<int,float>{{first,0}};var parents=new Dictionary<int,(int Previous,Arc Arc)>();var closed=new HashSet<int>();queue.Enqueue(first,0);int visited=0;
  while(queue.TryDequeue(out int current,out _)&&visited<Math.Clamp(maximumVisited,1,1000000))
  {
   if(!closed.Add(current))continue;visited++;if(current==last)
   {
    var arcs=new List<Arc>();for(int node=last;node!=first;){var parent=parents[node];arcs.Add(parent.Arc);node=parent.Previous;}arcs.Reverse();var path=new List<Vector3>{from};
    foreach(var arc in arcs){path.Add(arc.Entry);if(arc.Exit!=arc.Entry)path.Add(arc.Exit);path.Add(_polygons[arc.Target].Center);}path.Add(to);return path;
   }
   foreach(var arc in _adjacency[current]){float cost=costs[current]+arc.Cost;if(closed.Contains(arc.Target)||cost>=costs.GetValueOrDefault(arc.Target,float.PositiveInfinity))continue;costs[arc.Target]=cost;parents[arc.Target]=(current,arc);queue.Enqueue(arc.Target,cost);}
  }
  return [];
 }
}
