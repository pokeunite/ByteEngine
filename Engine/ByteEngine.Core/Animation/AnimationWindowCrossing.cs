namespace ByteEngine.Core.Animation;

public enum AnimationWindowBoundaryKind
{
    Enter,
    Exit
}

public readonly record struct AnimationWindowBoundary(
    AnimationWindow Window,
    AnimationWindowBoundaryKind Kind,
    float Time);

public readonly record struct AnimationWindowCrossingHit(
    AnimationWindowBoundary Boundary,
    long LoopOffset);

/// <summary>
/// Allocation-free-per-frame forward crossing for pre-sorted animation-window
/// boundaries. Snapshots are rebuilt only when clip metadata changes.
/// </summary>
public static class AnimationWindowCrossing
{
    private const float Epsilon = 0.000001f;

    public static AnimationWindowBoundary[] BuildSnapshot(
        IEnumerable<AnimationWindow>? windows,
        float duration)
    {
        if (windows == null ||
            !float.IsFinite(duration) ||
            duration <= Epsilon)
        {
            return Array.Empty<AnimationWindowBoundary>();
        }

        var boundaries = new List<AnimationWindowBoundary>();

        foreach (AnimationWindow? source in windows)
        {
            if (source == null ||
                string.IsNullOrWhiteSpace(source.Name) ||
                !float.IsFinite(source.StartTime) ||
                !float.IsFinite(source.EndTime))
            {
                continue;
            }

            float first = Math.Clamp(source.StartTime, 0.0f, duration);
            float second = Math.Clamp(source.EndTime, 0.0f, duration);
            float start = Math.Min(first, second);
            float end = Math.Max(first, second);

            var window = new AnimationWindow
            {
                Id = source.Id,
                Name = source.Name.Trim(),
                StartTime = start,
                EndTime = end,
                Payload = source.Payload ?? string.Empty
            };

            boundaries.Add(
                new AnimationWindowBoundary(
                    window,
                    AnimationWindowBoundaryKind.Enter,
                    start));
            boundaries.Add(
                new AnimationWindowBoundary(
                    window,
                    AnimationWindowBoundaryKind.Exit,
                    end));
        }

        boundaries.Sort(CompareBoundaries);
        return boundaries.ToArray();
    }

    public static int Collect(
        IReadOnlyList<AnimationWindowBoundary> sortedBoundaries,
        float duration,
        float fromTime,
        float deltaTime,
        bool loop,
        bool includeStart,
        List<AnimationWindowCrossingHit> destination,
        out long loopsCrossed)
    {
        ArgumentNullException.ThrowIfNull(sortedBoundaries);
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();
        loopsCrossed = 0;

        if (!float.IsFinite(duration) || duration <= Epsilon ||
            !float.IsFinite(fromTime) || !float.IsFinite(deltaTime))
        {
            return 0;
        }

        float advance = Math.Max(deltaTime, 0.0f);
        float position = loop
            ? NormalizeLoopTime(fromTime, duration)
            : Math.Clamp(fromTime, 0.0f, duration);

        if (!loop)
        {
            CollectSegment(
                sortedBoundaries,
                position,
                Math.Min(position + advance, duration),
                includeStart,
                0,
                destination);
            return destination.Count;
        }

        if (sortedBoundaries.Count == 0)
        {
            loopsCrossed = CountLoopCrossings(position, advance, duration);
            return 0;
        }

        if (advance <= Epsilon)
        {
            if (includeStart)
                CollectPoint(sortedBoundaries, position, 0, destination);
            return destination.Count;
        }

        bool firstSegment = true;
        long loopOffset = 0;
        float remaining = advance;

        while (remaining > Epsilon)
        {
            float distanceToEnd = duration - position;

            if (distanceToEnd <= Epsilon)
            {
                loopsCrossed++;
                loopOffset++;
                position = 0.0f;
                CollectPoint(sortedBoundaries, 0.0f, loopOffset, destination);
                firstSegment = false;
                continue;
            }

            if (remaining < distanceToEnd - Epsilon)
            {
                CollectSegment(
                    sortedBoundaries,
                    position,
                    position + remaining,
                    firstSegment && includeStart,
                    loopOffset,
                    destination);
                break;
            }

            CollectSegment(
                sortedBoundaries,
                position,
                duration,
                firstSegment && includeStart,
                loopOffset,
                destination);

            remaining = Math.Max(remaining - distanceToEnd, 0.0f);
            loopsCrossed++;
            loopOffset++;
            position = 0.0f;
            CollectPoint(sortedBoundaries, 0.0f, loopOffset, destination);
            firstSegment = false;
        }

        return destination.Count;
    }

    private static int CompareBoundaries(
        AnimationWindowBoundary left,
        AnimationWindowBoundary right)
    {
        int time = left.Time.CompareTo(right.Time);
        if (time != 0) return time;
        return left.Kind.CompareTo(right.Kind);
    }

    private static void CollectSegment(
        IReadOnlyList<AnimationWindowBoundary> boundaries,
        float start,
        float end,
        bool includeStart,
        long loopOffset,
        List<AnimationWindowCrossingHit> destination)
    {
        if (end + Epsilon < start) return;

        foreach (AnimationWindowBoundary boundary in boundaries)
        {
            if (boundary.Time < start - Epsilon) continue;
            if (boundary.Time > end + Epsilon) break;

            bool afterStart = includeStart
                ? boundary.Time >= start - Epsilon
                : boundary.Time > start + Epsilon;

            if (afterStart)
                destination.Add(new AnimationWindowCrossingHit(boundary, loopOffset));
        }
    }

    private static void CollectPoint(
        IReadOnlyList<AnimationWindowBoundary> boundaries,
        float point,
        long loopOffset,
        List<AnimationWindowCrossingHit> destination)
    {
        foreach (AnimationWindowBoundary boundary in boundaries)
        {
            if (boundary.Time < point - Epsilon) continue;
            if (boundary.Time > point + Epsilon) break;
            destination.Add(new AnimationWindowCrossingHit(boundary, loopOffset));
        }
    }

    private static long CountLoopCrossings(
        float fromTime,
        float advance,
        float duration) =>
        advance <= Epsilon
            ? 0
            : (long)Math.Floor((fromTime + advance + Epsilon) / duration);

    private static float NormalizeLoopTime(float time, float duration)
    {
        float result = time % duration;
        return result < 0.0f ? result + duration : result;
    }
}
