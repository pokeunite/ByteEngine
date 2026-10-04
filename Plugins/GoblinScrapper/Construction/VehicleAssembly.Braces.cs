using System.Numerics;
using ByteEngine.Core.Diagnostics;
namespace GoblinScrapper.Construction;
public sealed record BraceEndpoint(int Block,string Socket);
public sealed record AssemblyBrace(int Id,BraceEndpoint A,BraceEndpoint B);
public sealed partial class VehicleAssembly
{
 public Dictionary<int,AssemblyBrace> Braces {get;}=new();
 public Vector3 BracePoint(BraceEndpoint e){var p=Parts[e.Block];var s=Catalog[p.File].Sockets.First(s=>s.Name==e.Socket);return p.Position+Vector3.Transform(s.Position,p.Rotation);}
 public string BraceIssue(BraceEndpoint a,BraceEndpoint b)
 {
  bool Valid(BraceEndpoint e)=>Parts.TryGetValue(e.Block,out var p)&&Catalog[p.File].Sockets.Any(s=>s.Name==e.Socket);
  if(!Valid(a)||!Valid(b))return "Choose a connector at each end";
  if(a.Block==b.Block)return "Connect two different blocks";
  float length=Vector3.Distance(BracePoint(a),BracePoint(b));
  if(length<.15f||length>12)return "Brace span must be between 0.15 and 12 metres";
  if(Braces.Count+Parts.Count>=256)return "Workshop limit: 256 parts";
  if(Braces.Values.Any(v=>v.A==a&&v.B==b||v.A==b&&v.B==a))return "These endpoints are already braced";
  return "";
 }
 public int AddBrace(BraceEndpoint a,BraceEndpoint b)
 {
  if(BraceIssue(a,b)!="")return -1;int id=_nextId++;Braces.Add(id,new(id,a,b));
  if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("BRACE",$"id={id} first={a} second={b} span={Vector3.Distance(BracePoint(a),BracePoint(b))}");
  return id;
 }
}
