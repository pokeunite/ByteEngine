using System.Collections.Concurrent;
using System.Diagnostics;

namespace ByteEngine.Core.VisualLogic;

/// <summary>
/// Lightweight runtime trace used by the ByteGraph editor.
///
/// Runtime visual logic writes only the most recent timestamp for each
/// Event/Condition/Action node. The editor can then briefly highlight nodes
/// that actually evaluated true or executed while Play mode is running.
/// </summary>
public static class VisualLogicDebugTrace
{
    private readonly record struct TraceKey(
        Guid ModuleId,
        Guid NodeId);

    private static readonly ConcurrentDictionary<TraceKey, long> _hits =
        new();

    public static bool Enabled { get; set; } =
        true;

    public static void Mark(
        Guid moduleId,
        Guid nodeId)
    {
        if (!Enabled ||
            moduleId ==
                Guid.Empty ||
            nodeId ==
                Guid.Empty)
        {
            return;
        }

        _hits[
            new TraceKey(
                moduleId,
                nodeId)] =
            Stopwatch.GetTimestamp();
    }

    public static bool WasTriggered(
        Guid moduleId,
        Guid nodeId,
        double withinSeconds = 0.20)
    {
        if (!Enabled ||
            moduleId ==
                Guid.Empty ||
            nodeId ==
                Guid.Empty ||
            withinSeconds <=
                0.0)
        {
            return false;
        }

        if (!_hits.TryGetValue(
                new TraceKey(
                    moduleId,
                    nodeId),
                out long timestamp))
        {
            return false;
        }

        long elapsed =
            Stopwatch.GetTimestamp() -
            timestamp;

        double seconds =
            elapsed /
            (double)Stopwatch.Frequency;

        return seconds <=
               withinSeconds;
    }

    public static void Clear()
    {
        _hits.Clear();
    }
}
