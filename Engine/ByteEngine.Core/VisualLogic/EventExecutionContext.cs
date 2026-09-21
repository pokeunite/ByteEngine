using ByteEngine.Core.Animation;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;
public enum AnimationSignalKind
{
    None,
    EventFired,
    WindowEntered,
    WindowExited
}

public sealed class EventExecutionContext
{
    public required VariableStore Globals { get; init; }

    public required ByteEngine.Core.Scene.Scene Scene { get; init; }

    public required GameObject Self { get; init; }

    public Action<string>? WarningSink { get; init; }

    public Action<AnimationController>? ObserveAnimationController { get; init; }

    public AnimationSignalKind AnimationSignalKind { get; init; }

    public AnimationController? AnimationSignalSource { get; init; }

    public AnimationEventOccurrence? AnimationEvent { get; init; }

    public AnimationWindowOccurrence? AnimationWindow { get; init; }
}