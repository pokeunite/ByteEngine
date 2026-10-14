using System.Diagnostics;
namespace ByteEngine.Editor;
/// <summary>Last-frame editor CPU work and allocations, separate from scene simulation and GPU passes.</summary>
public static class EditorFrameProfile
{
    public readonly record struct Measurement(double Milliseconds,long AllocatedBytes);
    private static readonly Dictionary<string,Measurement> _panels=new();
    public static IReadOnlyDictionary<string,Measurement> Panels=>_panels;
    internal static void BeginFrame()=>_panels.Clear();
    internal static Scope Measure(string name)=>new(name);
    internal readonly struct Scope : IDisposable
    {
        private readonly string _name;
        private readonly long _start,_allocated;
        internal Scope(string name){_name=name;_start=Stopwatch.GetTimestamp();_allocated=GC.GetAllocatedBytesForCurrentThread();}
        public void Dispose()=>_panels[_name]=new(Stopwatch.GetElapsedTime(_start).TotalMilliseconds,GC.GetAllocatedBytesForCurrentThread()-_allocated);
    }
}
