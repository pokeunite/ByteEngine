namespace ByteEngine.Core.Animation;

/// <summary>
/// Internal crossing result reused by runtime playback to avoid allocating a
/// new event list every frame.
/// </summary>
public readonly record struct AnimationEventCrossingHit(
    AnimationEventMarker Marker,
    long LoopOffset);

/// <summary>
/// Performance-first event crossing helper for forward animation playback.
///
/// The runtime builds a sorted snapshot only when the active clip changes.
/// Per-frame crossing then performs no sorting and can reuse the caller's list.
/// </summary>
public static class AnimationEventCrossing
{
    private const float Epsilon = 0.000001f;

    /// <summary>
    /// Builds a stable, sanitized, time-sorted snapshot for one active clip.
    /// This is intended to run on clip changes, not every frame.
    /// </summary>
    public static AnimationEventMarker[] BuildSnapshot(
        IEnumerable<AnimationEventMarker>? markers,
        float duration)
    {
        if (markers == null ||
            !float.IsFinite(duration) ||
            duration <= Epsilon)
        {
            return Array.Empty<AnimationEventMarker>();
        }

        return markers
            .Where(
                marker =>
                    marker != null &&
                    !string.IsNullOrWhiteSpace(marker.Name) &&
                    float.IsFinite(marker.Time))
            .Select(
                marker =>
                    new AnimationEventMarker
                    {
                        Id = marker.Id,
                        Name = marker.Name.Trim(),
                        Time = Math.Clamp(
                            marker.Time,
                            0.0f,
                            duration),
                        Payload = marker.Payload ?? string.Empty
                    })
            .OrderBy(marker => marker.Time)
            .ToArray();
    }

    /// <summary>
    /// Collects all markers crossed by a forward playback advance.
    ///
    /// fromTime is the current local clip time before the advance. deltaTime is
    /// the unwrapped playback advance, so a large frame can cross more than one
    /// loop and still emit every marker in order.
    ///
    /// includeStart is used when a newly-started clip first becomes active so a
    /// marker at time zero fires once.
    /// </summary>
    public static int Collect(
        IReadOnlyList<AnimationEventMarker> sortedMarkers,
        float duration,
        float fromTime,
        float deltaTime,
        bool loop,
        bool includeStart,
        List<AnimationEventCrossingHit> destination,
        out long loopsCrossed)
    {
        ArgumentNullException.ThrowIfNull(sortedMarkers);
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();
        loopsCrossed = 0;

        if (!float.IsFinite(duration) ||
            duration <= Epsilon ||
            !float.IsFinite(fromTime) ||
            !float.IsFinite(deltaTime))
        {
            return 0;
        }

        float advance =
            Math.Max(
                deltaTime,
                0.0f);

        float position =
            loop
                ? NormalizeLoopTime(
                    fromTime,
                    duration)
                : Math.Clamp(
                    fromTime,
                    0.0f,
                    duration);

        if (!loop)
        {
            float end =
                Math.Min(
                    position + advance,
                    duration);

            CollectSegment(
                sortedMarkers,
                position,
                end,
                includeStart,
                0,
                destination);

            return destination.Count;
        }

        if (sortedMarkers.Count == 0)
        {
            loopsCrossed =
                CountLoopCrossings(
                    position,
                    advance,
                    duration);

            return 0;
        }

        if (advance <= Epsilon)
        {
            if (includeStart)
            {
                CollectPoint(
                    sortedMarkers,
                    position,
                    0,
                    destination);
            }

            return destination.Count;
        }

        bool firstSegment =
            true;

        long loopOffset =
            0;

        float remaining =
            advance;

        while (remaining > Epsilon)
        {
            float distanceToEnd =
                duration -
                position;

            if (distanceToEnd <= Epsilon)
            {
                loopsCrossed++;
                loopOffset++;
                position = 0.0f;

                CollectPoint(
                    sortedMarkers,
                    0.0f,
                    loopOffset,
                    destination);

                firstSegment = false;
                continue;
            }

            if (remaining <
                distanceToEnd -
                Epsilon)
            {
                float end =
                    position +
                    remaining;

                CollectSegment(
                    sortedMarkers,
                    position,
                    end,
                    firstSegment &&
                    includeStart,
                    loopOffset,
                    destination);

                remaining = 0.0f;
                break;
            }

            CollectSegment(
                sortedMarkers,
                position,
                duration,
                firstSegment &&
                includeStart,
                loopOffset,
                destination);

            remaining =
                Math.Max(
                    remaining -
                    distanceToEnd,
                    0.0f);

            loopsCrossed++;
            loopOffset++;
            position = 0.0f;

            /*
             * Playback modulo lands exactly at the next cycle's time zero.
             * Fire zero-time markers now. The next segment excludes its left
             * boundary, so the marker cannot fire twice.
             */
            CollectPoint(
                sortedMarkers,
                0.0f,
                loopOffset,
                destination);

            firstSegment = false;
        }

        return destination.Count;
    }

    private static void CollectSegment(
        IReadOnlyList<AnimationEventMarker> markers,
        float start,
        float end,
        bool includeStart,
        long loopOffset,
        List<AnimationEventCrossingHit> destination)
    {
        if (end + Epsilon <
            start)
        {
            return;
        }

        foreach (AnimationEventMarker marker
                 in markers)
        {
            float time =
                marker.Time;

            if (time <
                start -
                Epsilon)
            {
                continue;
            }

            if (time >
                end +
                Epsilon)
            {
                break;
            }

            bool afterStart =
                includeStart
                    ? time >=
                      start -
                      Epsilon
                    : time >
                      start +
                      Epsilon;

            if (!afterStart)
            {
                continue;
            }

            destination.Add(
                new AnimationEventCrossingHit(
                    marker,
                    loopOffset));
        }
    }

    private static void CollectPoint(
        IReadOnlyList<AnimationEventMarker> markers,
        float point,
        long loopOffset,
        List<AnimationEventCrossingHit> destination)
    {
        foreach (AnimationEventMarker marker
                 in markers)
        {
            if (marker.Time <
                point -
                Epsilon)
            {
                continue;
            }

            if (marker.Time >
                point +
                Epsilon)
            {
                break;
            }

            destination.Add(
                new AnimationEventCrossingHit(
                    marker,
                    loopOffset));
        }
    }

    private static long CountLoopCrossings(
        float fromTime,
        float advance,
        float duration)
    {
        if (advance <= Epsilon)
        {
            return 0;
        }

        double total =
            fromTime +
            advance;

        return (long)Math.Floor(
            (total + Epsilon) /
            duration);
    }

    private static float NormalizeLoopTime(
        float time,
        float duration)
    {
        float result =
            time %
            duration;

        if (result <
            0.0f)
        {
            result +=
                duration;
        }

        return result;
    }
}
