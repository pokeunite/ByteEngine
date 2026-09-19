namespace ByteEngine.Core.Animation;

/// <summary>
/// One instantaneous marker on an animation timeline.
///
/// C10-A keeps the payload intentionally lightweight. C10-F can expose richer
/// Event Sheet bindings without changing marker identity or timing.
/// </summary>
public sealed class AnimationEventMarker
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Marker time in seconds on the owning animation timeline.
    /// </summary>
    public float Time { get; set; }

    /// <summary>
    /// Optional lightweight author payload. It is deliberately a string for the
    /// foundation pass; typed Event Sheet values can be layered on later.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    public AnimationEventMarker Clone() =>
        new()
        {
            Id = Id,
            Name = Name,
            Time = Time,
            Payload = Payload
        };
}

/// <summary>
/// Runtime event delivered by AnimationController when playback crosses a
/// marker. AnimationKey identifies the target-model-owned clip, regardless of
/// whether that clip was native to the FBX or baked by the retarget workflow.
/// </summary>
public readonly record struct AnimationEventOccurrence(
    Guid ModelGuid,
    string AnimationKey,
    string AnimationName,
    Guid EventId,
    string EventName,
    string Payload,
    float EventTime,
    long LoopIndex);
