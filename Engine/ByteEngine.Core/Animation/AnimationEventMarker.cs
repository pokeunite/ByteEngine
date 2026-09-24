namespace ByteEngine.Core.Animation;

/// <summary>
/// One instantaneous marker on an animation timeline.
///
/// Event Sheets consume marker names and optional payload filters without changing
/// marker identity, stable clip ownership, or timeline timing.
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
    /// Optional lightweight string payload used by animation-event conditions.
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
/// whether that clip was imported or loaded from legacy model-owned animation data.
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
