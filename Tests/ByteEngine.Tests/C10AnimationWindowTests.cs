using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class C10AnimationWindowTests
{
    public static void Run()
    {
        SnapshotIsSanitizedAndSorted();
        EnterOnly();
        ExitOnly();
        FrameSkipsAcrossBothBoundaries();
        LoopWrapPreservesBoundaryOrder();
        MultipleLoopsPreserveEveryBoundary();
        NonLoopingPlaybackReachesEnd();
        PlaybackAdvanceChangesDoNotRefireBoundaries();
        ImportedAnimationOwnsWindows();
    }

    private static void SnapshotIsSanitizedAndSorted()
    {
        AnimationWindowBoundary[] snapshot =
            AnimationWindowCrossing.BuildSnapshot(
                new[]
                {
                    Window("Late", 0.7f, 2.0f),
                    Window("Reverse", 0.6f, 0.2f),
                    Window(" ", 0.1f, 0.2f),
                    Window("Invalid", float.NaN, 0.5f)
                },
                1.0f);

        Assert(snapshot.Length == 4, "C10-C filters invalid windows");
        Assert(snapshot[0].BoundaryName() == "Reverse:Enter" &&
               Nearly(snapshot[0].Time, 0.2f) &&
               snapshot[1].BoundaryName() == "Reverse:Exit" &&
               Nearly(snapshot[1].Time, 0.6f) &&
               snapshot[2].BoundaryName() == "Late:Enter" &&
               Nearly(snapshot[3].Time, 1.0f),
            "C10-C clamps, normalizes, and sorts windows");
    }

    private static void EnterOnly()
    {
        AnimationWindowBoundary[] snapshot = Snapshot(Window("Damage", 0.2f, 0.8f));
        List<AnimationWindowCrossingHit> hits = Collect(snapshot, 0.1f, 0.2f, false, out _);
        AssertNames(hits, "Damage:Enter");
    }

    private static void ExitOnly()
    {
        AnimationWindowBoundary[] snapshot = Snapshot(Window("Damage", 0.2f, 0.8f));
        List<AnimationWindowCrossingHit> hits = Collect(snapshot, 0.5f, 0.4f, false, out _);
        AssertNames(hits, "Damage:Exit");
    }

    private static void FrameSkipsAcrossBothBoundaries()
    {
        AnimationWindowBoundary[] snapshot = Snapshot(Window("Damage", 0.2f, 0.8f));
        List<AnimationWindowCrossingHit> hits = Collect(snapshot, 0.1f, 0.8f, false, out _);
        AssertNames(hits, "Damage:Enter", "Damage:Exit");
    }

    private static void LoopWrapPreservesBoundaryOrder()
    {
        AnimationWindowBoundary[] snapshot =
            Snapshot(
                Window("Late", 0.8f, 0.9f),
                Window("Early", 0.1f, 0.2f));
        List<AnimationWindowCrossingHit> hits = Collect(snapshot, 0.75f, 0.5f, true, out long loops);
        Assert(loops == 1, "C10-C wrap reports one loop");
        AssertNames(hits, "Late:Enter", "Late:Exit", "Early:Enter", "Early:Exit");
        Assert(hits[2].LoopOffset == 1, "C10-C wrap advances loop index");
    }

    private static void MultipleLoopsPreserveEveryBoundary()
    {
        AnimationWindowBoundary[] snapshot = Snapshot(Window("Damage", 0.2f, 0.4f));
        List<AnimationWindowCrossingHit> hits = Collect(snapshot, 0.5f, 2.0f, true, out long loops);
        Assert(loops == 2, "C10-C large frame counts complete loops");
        AssertNames(hits, "Damage:Enter", "Damage:Exit", "Damage:Enter", "Damage:Exit");
        Assert(hits[^1].LoopOffset == 2, "C10-C large frame preserves loop offsets");
    }

    private static void NonLoopingPlaybackReachesEnd()
    {
        AnimationWindowBoundary[] snapshot = Snapshot(Window("Final", 0.8f, 1.0f));
        List<AnimationWindowCrossingHit> hits = Collect(snapshot, 0.7f, 5.0f, false, out long loops);
        Assert(loops == 0, "C10-C non-looping playback reports no loops");
        AssertNames(hits, "Final:Enter", "Final:Exit");
    }

    private static void PlaybackAdvanceChangesDoNotRefireBoundaries()
    {
        AnimationWindowBoundary[] snapshot = Snapshot(Window("Damage", 0.2f, 0.8f));
        List<AnimationWindowCrossingHit> hits = Collect(snapshot, 0.0f, 0.3f, false, out _);
        AssertNames(hits, "Damage:Enter");
        hits = Collect(snapshot, 0.3f, 0.1f, false, out _);
        AssertNames(hits);
        hits = Collect(snapshot, 0.4f, 0.5f, false, out _);
        AssertNames(hits, "Damage:Exit");
    }

    private static void ImportedAnimationOwnsWindows()
    {
        var animation = new ImportedAnimation { Key = "attack", Name = "Attack", Duration = 1.0f };
        animation.Windows.Add(Window("Damage", 0.2f, 0.5f));
        Assert(animation.Windows.Count == 1 && animation.Windows[0].Name == "Damage",
            "C10-C window metadata belongs to target-model animation");
    }

    private static AnimationWindowBoundary[] Snapshot(params AnimationWindow[] windows) =>
        AnimationWindowCrossing.BuildSnapshot(windows, 1.0f);

    private static List<AnimationWindowCrossingHit> Collect(
        IReadOnlyList<AnimationWindowBoundary> snapshot,
        float from,
        float advance,
        bool loop,
        out long loops)
    {
        var hits = new List<AnimationWindowCrossingHit>();
        AnimationWindowCrossing.Collect(snapshot, 1.0f, from, advance, loop, false, hits, out loops);
        return hits;
    }

    private static AnimationWindow Window(string name, float start, float end) =>
        new() { Id = Guid.NewGuid(), Name = name, StartTime = start, EndTime = end };

    private static string BoundaryName(this AnimationWindowBoundary boundary) =>
        $"{boundary.Window.Name}:{boundary.Kind}";

    private static void AssertNames(
        IReadOnlyList<AnimationWindowCrossingHit> hits,
        params string[] expected)
    {
        string[] actual = hits.Select(hit => hit.Boundary.BoundaryName()).ToArray();
        Assert(actual.SequenceEqual(expected, StringComparer.Ordinal),
            $"C10-C expected [{string.Join(", ", expected)}] but got [{string.Join(", ", actual)}]");
    }

    private static bool Nearly(float left, float right) =>
        MathF.Abs(left - right) <= 0.0001f;

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
