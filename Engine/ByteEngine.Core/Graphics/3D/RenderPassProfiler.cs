using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
namespace ByteEngine.Core.Graphics.ThreeD;
public sealed record RenderPassMeasurement(string Viewport,string Pass,int Width,int Height,double CpuMs,double? GpuMs,int GpuSampleAge);
/// <summary>CPU scopes plus delayed timestamp queries. Timestamp pairs permit nested passes without overlapping TimeElapsed queries.</summary>
internal sealed class RenderPassProfiler : IDisposable
{
    private readonly Dictionary<string,Timer> _timers=new();
    private sealed class Timer
    {
        public int[] Start=new int[3],End=new int[3];public bool[] Pending=new bool[3];public long[] Frames=new long[3];
        public int Cursor;public long Frame;public double? Gpu;public long GpuFrame;
    }
    public IDisposable Begin(string viewport,string pass,int width,int height,bool gpu)
    {
        string key=viewport+"/"+pass;
        if(!_timers.TryGetValue(key,out var timer))_timers[key]=timer=new();
        timer.Frame++;int slot=-1;
        if(gpu)
        {
            for(int i=0;i<3;i++)if(timer.Pending[i])
            {
                GL.GetQueryObject(timer.End[i],GetQueryObjectParam.QueryResultAvailable,out int ready);if(ready==0)continue;
                GL.GetQueryObject(timer.Start[i],GetQueryObjectParam.QueryResult,out long a);GL.GetQueryObject(timer.End[i],GetQueryObjectParam.QueryResult,out long b);
                timer.Gpu=(b-a)/1e6;timer.GpuFrame=timer.Frames[i];timer.Pending[i]=false;
            }
            int next=timer.Cursor++%3;if(!timer.Pending[next])
            {
                slot=next;if(timer.Start[slot]==0){timer.Start[slot]=GL.GenQuery();timer.End[slot]=GL.GenQuery();}
                GL.QueryCounter(timer.Start[slot],QueryCounterTarget.Timestamp);timer.Frames[slot]=timer.Frame;
            }
        }
        else timer.Gpu=null;
        long start=Stopwatch.GetTimestamp();
        return new Scope(()=>
        {
            if(slot>=0){GL.QueryCounter(timer.End[slot],QueryCounterTarget.Timestamp);timer.Pending[slot]=true;}
            GraphicsDiagnostics.RecordPass(new(viewport,pass,width,height,Stopwatch.GetElapsedTime(start).TotalMilliseconds,timer.Gpu,timer.Gpu.HasValue?(int)(timer.Frame-timer.GpuFrame):0));
        });
    }
    private sealed class Scope(Action end):IDisposable{private Action? _end=end;public void Dispose(){var callback=_end;_end=null;callback?.Invoke();}}
    public void Dispose(){foreach(var timer in _timers.Values)for(int i=0;i<3;i++){if(timer.Start[i]!=0)GL.DeleteQuery(timer.Start[i]);if(timer.End[i]!=0)GL.DeleteQuery(timer.End[i]);}_timers.Clear();}
}
