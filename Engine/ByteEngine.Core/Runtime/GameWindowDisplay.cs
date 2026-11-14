namespace ByteEngine.Core.Runtime;

public enum GameWindowMode
{
    Windowed,
    Borderless,
    Fullscreen
}

/// <summary>
/// Standalone hosts own display changes; editor previews never resize the editor.
///
/// Plugins (for example the pause-menu graphics page) request a mode without
/// holding a window handle. A host attaches a handler while its window is alive;
/// with no handler attached the request only records <see cref="Mode"/>.
/// </summary>
public static class GameWindowDisplay
{
    private static Action<GameWindowMode>? _apply;

    public static GameWindowMode Mode { get; private set; } = GameWindowMode.Fullscreen;

    public static bool Available => _apply != null;

    public static void Request(GameWindowMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        Mode = mode;
        _apply?.Invoke(mode);
    }

    public static IDisposable Attach(Action<GameWindowMode> handler, GameWindowMode initial)
    {
        var prior = _apply;
        _apply = handler;
        Request(initial);
        return new Lease(() =>
        {
            if (_apply == handler)
                _apply = prior;
        });
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            release();
        }
    }
}
