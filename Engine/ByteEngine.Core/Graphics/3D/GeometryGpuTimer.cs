using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
namespace ByteEngine.Core.Graphics.ThreeD;
/// <summary>Non-blocking delayed query ring. Never waits for the GPU or reuses pending queries.</summary>
internal sealed class GeometryGpuTimer : IDisposable
{
 readonly int[] _queries=new int[3];readonly bool[] _pending=new bool[3];int _cursor,_active=-1;long _start;
 public void Begin(bool enabled){_start=Stopwatch.GetTimestamp();_active=-1;if(!enabled){GraphicsDiagnostics.GeometryGpuMs=null;return;}for(int i=0;i<3;i++){if(!_pending[i])continue;GL.GetQueryObject(_queries[i],GetQueryObjectParam.QueryResultAvailable,out int ready);if(ready==0)continue;GL.GetQueryObject(_queries[i],GetQueryObjectParam.QueryResult,out long elapsed);GraphicsDiagnostics.GeometryGpuMs=elapsed/1e6;_pending[i]=false;}int slot=_cursor++%3;if(_pending[slot])return;if(_queries[slot]==0)_queries[slot]=GL.GenQuery();GL.BeginQuery(QueryTarget.TimeElapsed,_queries[slot]);_active=slot;}
 public void End(){GraphicsDiagnostics.GeometryCpuMs=Stopwatch.GetElapsedTime(_start).TotalMilliseconds;if(_active<0)return;GL.EndQuery(QueryTarget.TimeElapsed);_pending[_active]=true;_active=-1;}
 public void Dispose(){if(_active>=0)End();foreach(int query in _queries)if(query!=0)GL.DeleteQuery(query);Array.Clear(_queries);Array.Clear(_pending);GraphicsDiagnostics.GeometryGpuMs=null;}
}
