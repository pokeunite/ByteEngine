using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public sealed class EventExecutionContext
{
    public required VariableStore Globals { get; init; }

    public required ByteEngine.Core.Scene.Scene Scene { get; init; }

    public required GameObject Self { get; init; }

    public Action<string>? WarningSink { get; init; }
}