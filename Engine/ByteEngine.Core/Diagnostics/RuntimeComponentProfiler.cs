using System.Diagnostics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Diagnostics;

/// <summary>Opt-in CPU attribution. Disabled in normal gameplay; no engine-specific browser dependency.</summary>
public static class RuntimeComponentProfiler
{
    public static bool Enabled { get; set; }
    public sealed record Sample(string Component, string Phase, int Calls, double Milliseconds);
    private sealed class Counter { public int Calls; public long Ticks; }
    private static readonly Dictionary<(Type, string), Counter> Counters = new();
    public static void BeginFrame() { if (Enabled) foreach (var value in Counters.Values) { value.Calls = 0; value.Ticks = 0; } }
    public static Scope Measure(Component component, string phase) => new(component.GetType(), phase);
    public readonly struct Scope : IDisposable
    {
        private readonly Type type;
        private readonly string phase;
        private readonly long start;
        internal Scope(Type type, string phase) { this.type = type; this.phase = phase; start = Stopwatch.GetTimestamp(); }
        public void Dispose()
        {
            var key = (type, phase);
            if (!Counters.TryGetValue(key, out var value)) Counters.Add(key, value = new());
            value.Calls++; value.Ticks += Stopwatch.GetTimestamp() - start;
        }
    }
    public static Sample[] Snapshot() => !Enabled ? [] : Counters.Where(p => p.Value.Calls > 0)
        .Select(p => new Sample(p.Key.Item1.Name, p.Key.Item2, p.Value.Calls, p.Value.Ticks * 1000.0 / Stopwatch.Frequency))
        .OrderByDescending(p => p.Milliseconds).ToArray();
}
