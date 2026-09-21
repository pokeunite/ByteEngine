using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

/// <summary>
/// C10-A/B focused regressions for authoritative marker crossing and clip-owned metadata.
/// </summary>
internal static class C10AnimationEventsTests
{
    public static void Run()
    {
        SnapshotIsSanitizedAndSorted();
        NormalCrossingFiresInTimelineOrder();
        PreviousBoundaryDoesNotRefire();
        LoopWrapPreservesEndThenStartOrder();
        LargeFrameCrossesMultipleLoops();
        NonLoopingPlaybackStopsAtDuration();
        StartMarkerFiresOnce();
        EmptyTrackStillCountsLoops();
        ImportedAnimationOwnsRuntimeEventMetadata();
    }

    private static void SnapshotIsSanitizedAndSorted()
    {
        AnimationEventMarker[] snapshot =
            AnimationEventCrossing.BuildSnapshot(
                new[]
                {
                    Marker("Late", 1.5f),
                    Marker("Early", -0.25f),
                    Marker("Middle", 0.5f),
                    Marker(" ", 0.25f),
                    Marker("Invalid", float.NaN)
                },
                1.0f);

        Assert(
            snapshot.Length == 3,
            "C10 snapshot filters invalid markers");

        Assert(
            snapshot[0].Name == "Early" &&
            Nearly(snapshot[0].Time, 0.0f) &&
            snapshot[1].Name == "Middle" &&
            Nearly(snapshot[1].Time, 0.5f) &&
            snapshot[2].Name == "Late" &&
            Nearly(snapshot[2].Time, 1.0f),
            "C10 snapshot clamps and sorts marker times");
    }

    private static void NormalCrossingFiresInTimelineOrder()
    {
        AnimationEventMarker[] markers =
            AnimationEventCrossing.BuildSnapshot(
                new[]
                {
                    Marker("A", 0.20f),
                    Marker("B", 0.40f),
                    Marker("C", 0.70f)
                },
                1.0f);

        var hits =
            new List<AnimationEventCrossingHit>();

        AnimationEventCrossing.Collect(
            markers,
            1.0f,
            0.10f,
            0.35f,
            false,
            false,
            hits,
            out long loops);

        Assert(
            loops == 0,
            "C10 normal crossing does not report loops");

        AssertNames(
            hits,
            "A",
            "B");
    }

    private static void PreviousBoundaryDoesNotRefire()
    {
        AnimationEventMarker[] markers =
            AnimationEventCrossing.BuildSnapshot(
                new[]
                {
                    Marker("Boundary", 0.40f),
                    Marker("Next", 0.50f)
                },
                1.0f);

        var hits =
            new List<AnimationEventCrossingHit>();

        AnimationEventCrossing.Collect(
            markers,
            1.0f,
            0.40f,
            0.10f,
            false,
            false,
            hits,
            out _);

        AssertNames(
            hits,
            "Next");
    }

    private static void LoopWrapPreservesEndThenStartOrder()
    {
        AnimationEventMarker[] markers =
            AnimationEventCrossing.BuildSnapshot(
                new[]
                {
                    Marker("Zero", 0.00f),
                    Marker("Early", 0.10f),
                    Marker("Late", 0.90f),
                    Marker("End", 1.00f)
                },
                1.0f);

        var hits =
            new List<AnimationEventCrossingHit>();

        AnimationEventCrossing.Collect(
            markers,
            1.0f,
            0.80f,
            0.35f,
            true,
            false,
            hits,
            out long loops);

        Assert(
            loops == 1,
            "C10 loop wrap count");

        AssertNames(
            hits,
            "Late",
            "End",
            "Zero",
            "Early");

        Assert(
            hits[0].LoopOffset == 0 &&
            hits[1].LoopOffset == 0 &&
            hits[2].LoopOffset == 1 &&
            hits[3].LoopOffset == 1,
            "C10 loop offsets follow wrap boundary");
    }

    private static void LargeFrameCrossesMultipleLoops()
    {
        AnimationEventMarker[] markers =
            AnimationEventCrossing.BuildSnapshot(
                new[]
                {
                    Marker("Zero", 0.00f),
                    Marker("Quarter", 0.25f),
                    Marker("End", 1.00f)
                },
                1.0f);

        var hits =
            new List<AnimationEventCrossingHit>();

        AnimationEventCrossing.Collect(
            markers,
            1.0f,
            0.75f,
            2.60f,
            true,
            false,
            hits,
            out long loops);

        Assert(
            loops == 3,
            "C10 large frame preserves every crossed loop");

        AssertNames(
            hits,
            "End",
            "Zero",
            "Quarter",
            "End",
            "Zero",
            "Quarter",
            "End",
            "Zero",
            "Quarter");

        Assert(
            hits[^1].LoopOffset == 3,
            "C10 large frame reports final loop offset");
    }

    private static void NonLoopingPlaybackStopsAtDuration()
    {
        AnimationEventMarker[] markers =
            AnimationEventCrossing.BuildSnapshot(
                new[]
                {
                    Marker("Late", 0.90f),
                    Marker("End", 1.00f)
                },
                1.0f);

        var hits =
            new List<AnimationEventCrossingHit>();

        AnimationEventCrossing.Collect(
            markers,
            1.0f,
            0.80f,
            5.0f,
            false,
            false,
            hits,
            out long loops);

        Assert(
            loops == 0,
            "C10 non-looping playback never reports loop crossings");

        AssertNames(
            hits,
            "Late",
            "End");
    }

    private static void StartMarkerFiresOnce()
    {
        AnimationEventMarker[] markers =
            AnimationEventCrossing.BuildSnapshot(
                new[]
                {
                    Marker("Start", 0.00f),
                    Marker("Move", 0.10f)
                },
                1.0f);

        var hits =
            new List<AnimationEventCrossingHit>();

        AnimationEventCrossing.Collect(
            markers,
            1.0f,
            0.00f,
            0.00f,
            false,
            true,
            hits,
            out _);

        AssertNames(
            hits,
            "Start");

        AnimationEventCrossing.Collect(
            markers,
            1.0f,
            0.00f,
            0.15f,
            false,
            false,
            hits,
            out _);

        AssertNames(
            hits,
            "Move");
    }

    private static void EmptyTrackStillCountsLoops()
    {
        var hits =
            new List<AnimationEventCrossingHit>();

        AnimationEventCrossing.Collect(
            Array.Empty<AnimationEventMarker>(),
            1.0f,
            0.75f,
            2.60f,
            true,
            false,
            hits,
            out long loops);

        Assert(
            hits.Count == 0 &&
            loops == 3,
            "C10 empty track loop accounting");
    }

    private static void ImportedAnimationOwnsRuntimeEventMetadata()
    {
        var animation =
            new ImportedAnimation
            {
                Key = "attack",
                Name = "Attack",
                Duration = 1.0f
            };

        animation.Events.Add(
            Marker(
                "Impact",
                0.4f));

        Assert(
            animation.Events.Count == 1 &&
            animation.Events[0].Name == "Impact",
            "C10 event metadata belongs to the target-model animation");
    }

    private static AnimationEventMarker Marker(
        string name,
        float time) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Time = time
        };

    private static void AssertNames(
        IReadOnlyList<AnimationEventCrossingHit> hits,
        params string[] expected)
    {
        string[] actual =
            hits
                .Select(
                    hit =>
                        hit.Marker.Name)
                .ToArray();

        Assert(
            actual.SequenceEqual(
                expected,
                StringComparer.Ordinal),
            $"C10 marker order expected [{string.Join(", ", expected)}] but got [{string.Join(", ", actual)}]");
    }

    private static bool Nearly(
        float left,
        float right) =>
        MathF.Abs(
            left -
            right) <=
        0.0001f;

    private static void Assert(
        bool condition,
        string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "FAILED: " +
                name);
        }
    }
}
