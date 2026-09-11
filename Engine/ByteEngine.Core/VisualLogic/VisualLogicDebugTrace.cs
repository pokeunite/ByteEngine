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

        TraceKey key =
            new(
                moduleId,
                nodeId);

        long now =
            Stopwatch.GetTimestamp();

        /*
         * Edge-triggered Conditions such as Mouse Button Pressed are TRUE
         * for only one update frame. Without a short positive-state hold,
         * the very next FALSE evaluation immediately overwrites the TRUE
         * trace before the editor can visibly render it.
         *
         * Keep successful pulses visible briefly:
         *
         * ConditionTrue  -> ConditionFalse
         * EventTriggered -> EventBlocked
         * ActionExecuted -> ActionSkipped
         *
         * Real failures are never delayed.
         */
        if (_entries.TryGetValue(
                key,
                out TraceEntry previous) &&
            ShouldHoldPositivePulse(
                previous.State,
                state))
        {
            long elapsed =
                now -
                previous.Timestamp;

            double seconds =
                elapsed /
                (double)Stopwatch.Frequency;

            if (seconds <
                PositivePulseHoldSeconds)
            {
                return;
            }
        }

        _entries[key] =
            new TraceEntry(
                state,
                now);
    }

    private const double PositivePulseHoldSeconds =
        0.24;

    private static bool ShouldHoldPositivePulse(
        VisualLogicTraceState previous,
        VisualLogicTraceState incoming)
    {
        return
            (
                previous ==
                    VisualLogicTraceState.ConditionTrue &&
                incoming ==
                    VisualLogicTraceState.ConditionFalse
            ) ||
            (
                previous ==
                    VisualLogicTraceState.EventTriggered &&
                incoming ==
                    VisualLogicTraceState.EventBlocked
            ) ||
            (
                previous ==
                    VisualLogicTraceState.ActionExecuted &&
                incoming ==
                    VisualLogicTraceState.ActionSkipped
            );
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
