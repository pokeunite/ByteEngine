using System.Numerics;
namespace ByteEngine.Core.Navigation;
/// <summary>Reusable XZ grid A*. Callers supply walkability and agent-clearance costs; no game-specific AI.</summary>
public sealed class NavigationGrid
{
 readonly bool[] _blocked;readonly float[] _cost;readonly float[] _heights;
 public float MaximumStepHeight {get;set;}=float.PositiveInfinity;
 readonly Dictionary<int,List<(int Target,float Cost)>> _links=new();
 public void AddLink(Vector3 from,Vector3 to,float cost=1,bool bidirectional=true){int a=Index((int)MathF.Floor((from.X-Origin.X)/CellSize),(int)MathF.Floor((from.Z-Origin.Y)/CellSize)),b=Index((int)MathF.Floor((to.X-Origin.X)/CellSize),(int)MathF.Floor((to.Z-Origin.Y)/CellSize));if(!float.IsFinite(cost)||cost<=0)throw new ArgumentOutOfRangeException(nameof(cost));Add(a,b,cost);if(bidirectional)Add(b,a,cost);}
 private void Add(int a,int b,float cost){if(!_links.TryGetValue(a,out var links))_links[a]=links=new();links.Add((b,cost));}
 public int Width {get;}public int Height {get;}public float CellSize {get;}public Vector2 Origin {get;}
 public NavigationGrid(int width,int height,float cellSize=1,Vector2 origin=default){if(width<1||height<1||(long)width*height>4_000_000||!float.IsFinite(cellSize)||cellSize<=0)throw new ArgumentOutOfRangeException(nameof(width));Width=width;Height=height;CellSize=cellSize;Origin=origin;_blocked=new bool[width*height];_heights=Enumerable.Repeat(float.NaN,width*height).ToArray();_cost=Enumerable.Repeat(1f,width*height).ToArray();}
 public void SetCell(int x,int z,bool blocked,float cost=1){int index=Index(x,z);if(!float.IsFinite(cost)||cost<1)throw new ArgumentOutOfRangeException(nameof(cost));_blocked[index]=blocked;_cost[index]=cost;}
 public void SetHeight(int x,int z,float height){if(!float.IsFinite(height))throw new ArgumentOutOfRangeException(nameof(height));_heights[Index(x,z)]=height;}
 public float HeightAt(int x,int z,float fallback=0)=>float.IsFinite(_heights[Index(x,z)])?_heights[Index(x,z)]:fallback;
 public bool IsWalkable(int x,int z)=>x>=0&&z>=0&&x<Width&&z<Height&&!_blocked[z*Width+x];
 int Index(int x,int z){if(x<0||z<0||x>=Width||z>=Height)throw new ArgumentOutOfRangeException(nameof(x));return z*Width+x;}
 public IReadOnlyList<Vector3> FindPath(Vector3 from,Vector3 to,int maximumVisited=100000,bool diagonal=true){
  if(!float.IsFinite(from.X)||!float.IsFinite(from.Z)||!float.IsFinite(to.X)||!float.IsFinite(to.Z))return Array.Empty<Vector3>();
  int sx=(int)MathF.Floor((from.X-Origin.X)/CellSize),sz=(int)MathF.Floor((from.Z-Origin.Y)/CellSize),tx=(int)MathF.Floor((to.X-Origin.X)/CellSize),tz=(int)MathF.Floor((to.Z-Origin.Y)/CellSize);if(!IsWalkable(sx,sz)||!IsWalkable(tx,tz)||maximumVisited<1)return Array.Empty<Vector3>();int start=Index(sx,sz),goal=Index(tx,tz);var g=new Dictionary<int,float>{{start,0}};var parents=new Dictionary<int,int>();var closed=new HashSet<int>();var open=new PriorityQueue<int,(float,int)>();int sequence=0;open.Enqueue(start,(0,sequence++));
  while(open.Count>0&&closed.Count<maximumVisited){int current=open.Dequeue();if(!closed.Add(current))continue;if(current==goal){var result=new List<Vector3>();for(int node=goal;;node=parents[node]){result.Add(new(Origin.X+(node%Width+.5f)*CellSize,HeightAt(node%Width,node/Width,from.Y),Origin.Y+(node/Width+.5f)*CellSize));if(node==start)break;}result.Reverse();return result;}if(_links.TryGetValue(current,out var links))foreach(var link in links){if(closed.Contains(link.Target)||_blocked[link.Target])continue;float value=g[current]+link.Cost;if(g.TryGetValue(link.Target,out float previous)&&value>=previous)continue;g[link.Target]=value;parents[link.Target]=current;open.Enqueue(link.Target,(value,sequence++));}int x=current%Width,z=current/Width;for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++){if(dx==0&&dz==0||!diagonal&&dx!=0&&dz!=0||!IsWalkable(x+dx,z+dz))continue;if(dx!=0&&dz!=0&&(!IsWalkable(x+dx,z)||!IsWalkable(x,z+dz)))continue;int next=Index(x+dx,z+dz);if(closed.Contains(next)||Math.Abs(HeightAt(x,z,from.Y)-HeightAt(x+dx,z+dz,from.Y))>MaximumStepHeight)continue;float score=g[current]+(dx!=0&&dz!=0?1.41421356f:1)*_cost[next];if(g.TryGetValue(next,out float old)&&score>=old)continue;g[next]=score;parents[next]=current;float ax=Math.Abs(x+dx-tx),az=Math.Abs(z+dz-tz);float heuristic=_links.Count>0?0:diagonal?Math.Max(ax,az)+.41421356f*Math.Min(ax,az):ax+az;open.Enqueue(next,(score+heuristic,sequence++));}}
  return Array.Empty<Vector3>();
 }
}
