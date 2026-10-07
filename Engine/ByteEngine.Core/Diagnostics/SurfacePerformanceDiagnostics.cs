using System.Globalization;
using System.Text;
using OpenTK.Graphics.OpenGL4;
namespace ByteEngine.Core.Diagnostics;
/// <summary>Bounded, opt-in traces shared by plugins and the Console Debug panel.</summary>
public static class SurfacePerformanceDiagnostics
{
 static bool _performanceEnabled;
 public static bool PerformanceEnabled {get=>_performanceEnabled;set{if(value&&!_performanceEnabled){_allocated=GC.GetTotalAllocatedBytes(false);_gc=GC.CollectionCount(0);_pause=GC.GetTotalPauseDuration().TotalMilliseconds;_sample=0;}_performanceEnabled=value;}}
 public static bool SandEnabled {get;set;}
 static readonly Queue<string> Frames=new(),Sand=new();
 static readonly object Gate=new();
 static readonly int[] Queries=new int[8];static readonly bool[] Pending=new bool[4];static int _slot,_active=-1;
 public static double? GpuMilliseconds{get;private set;}
 public static void BeginGpu()
 {
  _active=-1;if(!PerformanceEnabled)return;
  for(int i=0;i<4;i++){if(!Pending[i])continue;GL.GetQueryObject(Queries[i*2+1],GetQueryObjectParam.QueryResultAvailable,out int ready);if(ready==0)continue;GL.GetQueryObject(Queries[i*2],GetQueryObjectParam.QueryResult,out long start);GL.GetQueryObject(Queries[i*2+1],GetQueryObjectParam.QueryResult,out long end);GpuMilliseconds=(end-start)/1e6;Pending[i]=false;}
  int slot=_slot++%4;if(Pending[slot])return;if(Queries[slot*2]==0){Queries[slot*2]=GL.GenQuery();Queries[slot*2+1]=GL.GenQuery();}GL.QueryCounter(Queries[slot*2],QueryCounterTarget.Timestamp);_active=slot;
 }
 public static void EndGpu(){if(_active<0)return;GL.QueryCounter(Queries[_active*2+1],QueryCounterTarget.Timestamp);Pending[_active]=true;_active=-1;}
 public static void DisposeGpu(){for(int i=0;i<Queries.Length;i++)if(Queries[i]!=0){GL.DeleteQuery(Queries[i]);Queries[i]=0;}Array.Clear(Pending);GpuMilliseconds=null;}
 static double _elapsed,_sample;static long _allocated;static int _gc;static double _pause;
 public static void Frame(double dt,double render,double swap)
 {
  if(!PerformanceEnabled)return;_elapsed+=dt;_sample+=dt;if(_sample<.25&&dt<.05)return;
  long allocated=GC.GetTotalAllocatedBytes(false);if(_allocated==0){_allocated=allocated;_gc=GC.CollectionCount(0);_pause=GC.GetTotalPauseDuration().TotalMilliseconds;}int gc=GC.CollectionCount(0);double pause=GC.GetTotalPauseDuration().TotalMilliseconds;
  Record(Frames,FormattableString.Invariant($"t={_elapsed:F2}s frame={dt*1000:F2}ms renderCPU={render:F2}ms presentWait={swap:F2}ms gpuRender={GpuMilliseconds.GetValueOrDefault(-1):F2}ms allocated={(allocated-_allocated)/1048576.0:F2}MB gc0Delta={gc-_gc} gcPauseDelta={pause-_pause:F2}ms"));
  _allocated=allocated;_gc=gc;_pause=pause;_sample=0;
 }
 public static void RecordDetails(string value){if(PerformanceEnabled)Record(Frames,value);}
 public static void RecordSand(string value){if(SandEnabled)Record(Sand,value);}
 static void Record(Queue<string> queue,string value){lock(Gate){queue.Enqueue(value);while(queue.Count>240)queue.Dequeue();}}
 public static string GetPerformanceTrace(){lock(Gate)return "GPU render uses delayed nonblocking timestamps (excludes swap); -1 means pending. CPU submission and presentation wait are separate. Allocation/GC changes span the sampling interval.\n"+string.Join("\n",Frames);}
 public static string GetSandTrace(){lock(Gate)return string.Join("\n",Sand);}
 public static void Clear(){lock(Gate){Frames.Clear();Sand.Clear();_elapsed=_sample=0;_allocated=GC.GetTotalAllocatedBytes(false);_gc=GC.CollectionCount(0);_pause=GC.GetTotalPauseDuration().TotalMilliseconds;}}
}
