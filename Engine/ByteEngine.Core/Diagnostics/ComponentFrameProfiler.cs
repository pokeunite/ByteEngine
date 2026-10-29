using System.Diagnostics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Diagnostics;

public readonly record struct ComponentTiming(string Component, string Phase, int Calls, double TotalMs,
    double SlowestMs, string SlowestObject) { public long AllocatedBytes { get; init; } }
public readonly record struct ObjectTiming(Guid Id,string Object,string Component,string Phase,int Calls,double TotalMs,double SlowestMs,long AllocatedBytes);
public readonly record struct SystemTiming(string Category,string Name,int Calls,double TotalMs,double SlowestMs,long AllocatedBytes);

/// <summary>Opt-in, bounded per-frame timings. Disabled during normal play; no global scene references.</summary>
public sealed class ComponentFrameProfiler
{
    private readonly Dictionary<(Type Type, string Phase), Sample> _samples = new();
    private readonly Dictionary<(Guid Id,Type Type,string Phase),Sample> _objects = new();
    private readonly Dictionary<(string Category,string Name),Sample> _systems = new();
    public int DroppedDetailRows {get;private set;}
    public int ObjectRows=>_objects.Count;
    public int SystemRows=>_systems.Count;
    public bool AccumulateFrames {get;set;}
    public int Frames {get;private set;}
    public bool Enabled { get; set; }
    public void ClearSamples() { _samples.Clear();_objects.Clear();_systems.Clear();DroppedDetailRows=0;Frames=0; }
    internal void BeginFrame() { if(!Enabled||!AccumulateFrames)ClearSamples();if(Enabled)Frames++; }
    internal readonly record struct Measurement(long Timestamp,long Allocated,Guid Id,string? Object);
    internal Measurement Begin(Component? component=null) => Enabled ? new(Stopwatch.GetTimestamp(),GC.GetAllocatedBytesForCurrentThread(),component?.GameObject.Id??Guid.Empty,component?.GameObject.Name) : default;
    internal void End(Component component, string phase, Measurement start)
    {
        if (!Enabled || start.Timestamp == 0) return;
        double elapsed = Stopwatch.GetElapsedTime(start.Timestamp).TotalMilliseconds;
        long allocated=GC.GetAllocatedBytesForCurrentThread()-start.Allocated;
        var key = (component.GetType(), phase);
        _samples.TryGetValue(key, out var sample);
        sample.Calls++;
        sample.TotalMs += elapsed;
        sample.Allocated+=allocated;
        if (elapsed >= sample.SlowestMs) { sample.SlowestMs = elapsed; sample.Object = start.Object; }
        _samples[key] = sample;
        var objectKey=(start.Id,component.GetType(),phase);
        if(_objects.TryGetValue(objectKey,out var detail)||_objects.Count<4096){
            detail.Calls++;detail.TotalMs+=elapsed;detail.SlowestMs=Math.Max(detail.SlowestMs,elapsed);detail.Allocated+=allocated;detail.Object=start.Object;_objects[objectKey]=detail;
        }else DroppedDetailRows++;
    }

    public ComponentTiming[] Snapshot() => _samples.Select(pair => new ComponentTiming(
        pair.Key.Type.FullName ?? pair.Key.Type.Name, pair.Key.Phase, pair.Value.Calls,
        pair.Value.TotalMs, pair.Value.SlowestMs, pair.Value.Object ?? "") {AllocatedBytes=pair.Value.Allocated})
        .OrderByDescending(sample => sample.TotalMs).ToArray();

    public ObjectTiming[] ObjectSnapshot(int limit=128)=>_objects.OrderByDescending(p=>p.Value.TotalMs).Take(Math.Clamp(limit,1,4096))
        .Select(p=>new ObjectTiming(p.Key.Id,p.Value.Object??"",p.Key.Type.FullName??"",p.Key.Phase,p.Value.Calls,p.Value.TotalMs,p.Value.SlowestMs,p.Value.Allocated)).ToArray();
    public SystemTiming[] SystemSnapshot()=>_systems.Select(p=>new SystemTiming(p.Key.Category,p.Key.Name,p.Value.Calls,p.Value.TotalMs,p.Value.SlowestMs,p.Value.Allocated)).OrderByDescending(p=>p.TotalMs).ToArray();
    public WorkScope MeasureSystem(string category,string name="")=>Enabled?new(this,category,name):default;
    public readonly struct WorkScope : IDisposable {
        private readonly ComponentFrameProfiler? owner;private readonly string category,name;private readonly Measurement start;
        internal WorkScope(ComponentFrameProfiler owner,string category,string name){this.owner=owner;this.category=category;this.name=name;start=owner.Begin();}
        public void Dispose(){if(owner==null||!owner.Enabled)return;double elapsed=Stopwatch.GetElapsedTime(start.Timestamp).TotalMilliseconds;long bytes=GC.GetAllocatedBytesForCurrentThread()-start.Allocated;
            var key=(category,name);if(owner._systems.TryGetValue(key,out var sample)||owner._systems.Count<1024){sample.Calls++;sample.TotalMs+=elapsed;sample.SlowestMs=Math.Max(sample.SlowestMs,elapsed);sample.Allocated+=bytes;owner._systems[key]=sample;}else owner.DroppedDetailRows++;}
    }
    private struct Sample { public int Calls; public double TotalMs, SlowestMs; public long Allocated;public string? Object; }
}
