using System.Numerics;
namespace GoblinScrapper.Construction;
public sealed partial class VehicleAssembly
{
 public HashSet<(int Block,string Socket)> OccupiedSockets(int ignore=-1)
 {
  var result=new HashSet<(int,string)>();
  foreach(var p in Parts.Values)if(p.Parent>=0){result.Add((p.Id,p.OwnConnector));if(p.Id!=ignore)result.Add((p.Parent,p.ParentConnector));}
  if(!Catalog.Standard)return result;
  var cells=new Dictionary<(int,int,int),List<(int Id,string Name,Vector3 Point,Vector3 Normal)>>();
  var all=new List<(int Id,string Name,Vector3 Point,Vector3 Normal)>();
  (int,int,int) Cell(Vector3 v)=>((int)MathF.Floor(v.X/.025f),(int)MathF.Floor(v.Y/.025f),(int)MathF.Floor(v.Z/.025f));
  foreach(var p in Parts.Values)foreach(var socket in Catalog[p.File].Sockets){
   var entry=(p.Id,socket.Name,p.Position+Vector3.Transform(socket.Position,p.Rotation),Vector3.Transform(socket.Normal,p.Rotation));all.Add(entry);
   var key=Cell(entry.Item3);if(!cells.TryGetValue(key,out var list))cells[key]=list=new();list.Add(entry);
  }
  foreach(var s in all){
   if(result.Contains((s.Id,s.Name)))continue;var key=Cell(s.Point);bool found=false;
   for(int x=-1;x<=1&&!found;x++)for(int y=-1;y<=1&&!found;y++)for(int z=-1;z<=1&&!found;z++)
    if(cells.TryGetValue((key.Item1+x,key.Item2+y,key.Item3+z),out var list))foreach(var other in list)
     if(other.Id!=s.Id&&other.Id!=ignore&&Vector3.DistanceSquared(s.Point,other.Point)<.000625f&&Vector3.Dot(s.Normal,other.Normal)<-.995f){found=true;break;}
   if(found)result.Add((s.Id,s.Name));
  }
  return result;
 }
}
