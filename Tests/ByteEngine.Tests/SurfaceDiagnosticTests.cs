using ByteEngine.Core.Diagnostics;
using ByteEngine.Editor.Panels;
using System.Reflection;
namespace ByteEngine.Tests;
internal static class SurfaceDiagnosticTests
{
 public static void Run()
 {
  void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
  SurfacePerformanceDiagnostics.Clear();SurfacePerformanceDiagnostics.PerformanceEnabled=true;SurfacePerformanceDiagnostics.SandEnabled=true;
  SurfacePerformanceDiagnostics.Frame(.3,8,2);SurfacePerformanceDiagnostics.RecordSand("rut=0.05 shader=True");
  var recorded=SurfacePerformanceDiagnostics.GetPerformanceTrace();Check(recorded.Contains("gcPauseDelta=")&&recorded.Contains("gpuRender="),"Performance trace omits required frame fields");
  SurfacePerformanceDiagnostics.PerformanceEnabled=false;SurfacePerformanceDiagnostics.Frame(1,100,0);Check(recorded==SurfacePerformanceDiagnostics.GetPerformanceTrace(),"Unticking performance does not freeze the trace");
  var sand=SurfacePerformanceDiagnostics.GetSandTrace();SurfacePerformanceDiagnostics.SandEnabled=false;SurfacePerformanceDiagnostics.RecordSand("unexpected");Check(sand==SurfacePerformanceDiagnostics.GetSandTrace(),"Unticking sand does not freeze the trace");
  var trace=(string)typeof(ConsolePanel).GetMethod("BuildVisibleDebugTrace",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;Check(trace.Contains("Performance / Frame Pacing")&&trace.Contains("Interactive Sand")&&trace.Contains("rut=0.05"),"Console Copy/Save omits diagnostics");
  SurfacePerformanceDiagnostics.PerformanceEnabled=true;for(int i=0;i<1000;i++)SurfacePerformanceDiagnostics.RecordDetails("bounded-sample");Check(SurfacePerformanceDiagnostics.GetPerformanceTrace().Split("bounded-sample").Length-1==240,"Performance trace grows without its ring limit");
  SurfacePerformanceDiagnostics.Clear();Check(!SurfacePerformanceDiagnostics.GetPerformanceTrace().Contains("bounded-sample")&&SurfacePerformanceDiagnostics.GetSandTrace().Length==0,"Clear Debug leaves stale data");SurfacePerformanceDiagnostics.PerformanceEnabled=SurfacePerformanceDiagnostics.SandEnabled=false;
  Console.WriteLine("Console performance/sand traces: fields, freeze, Copy/Save sections, bounded storage and clear passed.");
 }
}
