using System.Diagnostics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;
internal static class SchedulingBenchmarkTests
{
 sealed class Marker:Component{public int Order;public override int UpdateOrder=>Order;public int Calls;protected override void OnUpdate(){Calls++;}}
 public static void Run(string output){var scene=new Scene("Scheduling benchmark");for(int i=0;i<1000;i++){var obj=scene.CreateGameObject("fixture");obj.AddComponent(new Marker{Order=2});obj.AddComponent(new Marker{Order=-1});obj.AddComponent(new Marker{Order=2});}
  foreach(var obj in scene.GameObjects)obj.StartInternal();
  void Old(){foreach(var obj in scene.GameObjects)foreach(var c in obj.Components.OrderBy(c=>c.UpdateOrder))c.UpdateInternal();}
  void New(){foreach(var obj in scene.GameObjects)obj.UpdateInternal();}
  for(int i=0;i<20;i++){Old();New();}long start=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();for(int i=0;i<100;i++)Old();watch.Stop();long oldBytes=GC.GetAllocatedBytesForCurrentThread()-start;double oldMs=watch.Elapsed.TotalMilliseconds;
  watch.Reset();start=GC.GetAllocatedBytesForCurrentThread();watch.Start();for(int i=0;i<100;i++)New();watch.Stop();long newBytes=GC.GetAllocatedBytesForCurrentThread()-start;double newMs=watch.Elapsed.TotalMilliseconds;
  if(newBytes>oldBytes/10)throw new Exception("Pooled component scheduling still allocates excessively");
  File.WriteAllText(output,$"1000 objects /3components /100 update passes\nLINQ baseline: {oldBytes} bytes /{oldMs:0.00}ms\npooled snapshot: {newBytes}bytes /{newMs:0.00}ms\nCPU microbenchmark, not game FPS.\n");Console.WriteLine("PASS Pooled scheduling reduces allocation by >90%");
 }
}
