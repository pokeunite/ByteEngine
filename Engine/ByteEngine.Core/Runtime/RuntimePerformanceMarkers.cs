namespace ByteEngine.Core.Runtime;

/// <summary>Optional host diagnostics. No observer or timing overhead in normal native play.</summary>
public static class RuntimePerformanceMarkers
{
    public static Action<string,double>? Observer { get; set; }
}
