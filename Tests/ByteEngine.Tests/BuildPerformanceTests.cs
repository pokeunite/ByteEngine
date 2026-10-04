using GoblinScrapper.Construction;
using System.Numerics;
using System.Diagnostics;
namespace ByteEngine.Tests;
internal static class BuildPerformanceTests
{
 public static void Run(string game)
 {
  var c=VehiclePartCatalog.Load(Path.Combine(game,"Assets/GarageUI/parts-catalog.json"));var beam=c.Parts.Values.First(p=>p.ReferenceId==1);var a=new VehicleAssembly(c);
  for(int i=1;i<=80;i++)a.Parts[i]=new(i,beam.File,-1,new((i%10)*2-9,(i/10)*2,0),Quaternion.Identity);
  var watch=Stopwatch.StartNew();int occupied=0;
  foreach(var p in a.Parts.Values)foreach(var socket in c[p.File].Sockets)if(a.IsSocketOccupied(p.Id,socket.Name))occupied++;
  watch.Stop();double baseline=watch.Elapsed.TotalMilliseconds;var optimized=Stopwatch.StartNew();var indexed=a.OccupiedSockets();optimized.Stop();
  foreach(var p in a.Parts.Values)foreach(var socket in c[p.File].Sockets)if(indexed.Contains((p.Id,socket.Name))!=a.IsSocketOccupied(p.Id,socket.Name))throw new Exception("Socket index changed placement rules");
  Console.WriteLine($"Indexed connector audit: {optimized.Elapsed.TotalMilliseconds:0.00} ms versus {baseline:0.00} ms; exact same occupancy rules.");
  Console.WriteLine($"Connector audit: blocks={a.Parts.Count}, sockets={a.Parts.Values.Sum(p=>c[p.File].Sockets.Length)}, occupied={occupied}, elapsed={watch.Elapsed.TotalMilliseconds:0.00} ms");
 }
}
