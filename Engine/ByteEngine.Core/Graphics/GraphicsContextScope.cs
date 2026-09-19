namespace ByteEngine.Core.Graphics;

/// <summary>
/// Logical OpenGL-context identity used by GPU resources that contain
/// context-local state.
///
/// ByteEngine's main editor uses context id 0 by default. Native asset-editor
/// windows allocate their own ids and enter them while their OpenGL context is
/// current. Shared OpenGL objects such as buffers/textures may still be reused
/// across shared contexts, while context-local objects such as VAOs can be
/// cached per id safely.
/// </summary>
public static class GraphicsContextScope
{
    private static int _nextContextId;

    [ThreadStatic]
    private static int _currentContextId;

    public const int MainContextId = 0;

    public static int CurrentContextId =>
        _currentContextId;

    public static int AllocateContextId()
    {
        return Interlocked.Increment(
            ref _nextContextId);
    }

    public static Scope Enter(
        int contextId)
    {
        if (contextId < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contextId));
        }

        return new Scope(
            contextId);
    }

    public readonly struct Scope
        : IDisposable
    {
        private readonly int _previousContextId;

        internal Scope(
            int contextId)
        {
            _previousContextId =
                _currentContextId;

            _currentContextId =
                contextId;
        }

        public void Dispose()
        {
            _currentContextId =
                _previousContextId;
        }
    }
}
