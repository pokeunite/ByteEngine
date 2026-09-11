using System.Collections.Concurrent;
using System.Diagnostics;

namespace ByteEngine.Core.VisualLogic;

public enum VisualLogicTraceState
{
    None,
    ConditionTrue,
    ConditionFalse,
    EventTriggered,
    EventBlocked,
    ActionExecuted,
    ActionSkipped,
    ActionFailed
}

/// <summary>
/// Lightweight runtime trace used by the ByteGraph editor.
///
/// Runtime visual logic stores the most recent state and timestamp for each
/// Event/Condition/Action node. The editor can then show meaningful live
/// execution feedback while Play mode is running.
/// </summary>
public static class VisualLogicDebugTrace
{
    private readonly record struct TraceKey(
        Guid ModuleId,
        Guid NodeId);

    private readonly record struct TraceEntry(
        VisualLogicTraceState State,
        long Timestamp);

    private static readonly ConcurrentDictionary<TraceKey, TraceEntry> _entries =
        new();

    public static bool Enabled { get; set; } =
        true;

    public static void Mark(
        Guid moduleId,
        Guid nodeId,
        VisualLogicTraceState state)
    {
        if (!Enabled ||
            moduleId ==
                Guid.Empty ||
            nodeId ==
                Guid.Empty ||
            state ==
                VisualLogicTraceState.None)
        {
            return;
        }

        _entries[
            new TraceKey(
                moduleId,
                nodeId)] =
            new TraceEntry(
                state,
                Stopwatch.GetTimestamp());
    }

    public static bool TryGetRecentState(
        Guid moduleId,
        Guid nodeId,
        out VisualLogicTraceState state,
        double withinSeconds = 0.20)
    {
        state =
            VisualLogicTraceState.None;

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

        if (!_entries.TryGetValue(
                new TraceKey(
                    moduleId,
                    nodeId),
                out TraceEntry entry))
        {
            return false;
        }

        long elapsed =
            Stopwatch.GetTimestamp() -
            entry.Timestamp;

        double seconds =
            elapsed /
            (double)Stopwatch.Frequency;

        if (seconds >
            withinSeconds)
        {
            return false;
        }

        state =
            entry.State;

        return true;
    }

    public static void Clear()
    {
        _entries.Clear();
    }
}
