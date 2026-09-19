namespace ByteEngine.Core.Animation;

/// <summary>
/// One duration-based gameplay window owned by a model animation.
/// </summary>
public sealed class AnimationWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public float StartTime { get; set; }
    public float EndTime { get; set; }
    public string Payload { get; set; } = string.Empty;

    public AnimationWindow Clone() =>
        new()
        {
            Id = Id,
            Name = Name,
            StartTime = StartTime,
            EndTime = EndTime,
            Payload = Payload
        };
}

public readonly record struct AnimationWindowOccurrence(
    Guid ModelGuid,
    string AnimationKey,
    string AnimationName,
    Guid WindowId,
    string WindowName,
    string Payload,
    float StartTime,
    float EndTime,
    long LoopIndex);
