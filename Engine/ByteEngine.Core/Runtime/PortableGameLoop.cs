using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Runtime;

/// <summary>
/// Host-driven loop. Runs the original scene, physics, animation and Event Sheet
/// lifecycle. The host owns asset bootstrap and rendering resources.
/// </summary>
public sealed class PortableGameLoop : IDisposable
{
    public SceneManager Scenes { get; } = new();
    private readonly Renderer2D _renderer2D = new();
    private readonly Graphics.ThreeD.Renderer3D _renderer3D = new();
    private bool _disposed;
    public double InputMilliseconds { get; private set; }
    public double RenderPreparationMilliseconds { get; private set; }

    public void Tick(double seconds, RawInputSnapshot input, int width, int height,
        Vector2 pointer, bool focused, bool captured, IRenderFrameSink sink)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(sink);
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        long inputStart = System.Diagnostics.Stopwatch.GetTimestamp();
        Diagnostics.RuntimeComponentProfiler.BeginFrame();
        Time.Update(Math.Min(seconds, .1)); // No catch-up burst after a suspended tab.
        Input.UpdatePortable(input, captured);
        Input.SetGameViewPointer(pointer, new(width, height), focused, focused, pointer * new Vector2(width, height));
        InputActions.Update(Input.Snapshot, focused);
        InputMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(inputStart).TotalMilliseconds;
        Scenes.UpdateInternal();
        if (Scenes.ActiveScene is not { } scene) return;
        long renderStart = System.Diagnostics.Stopwatch.GetTimestamp();
        var context = new RenderContext(_renderer2D, _renderer3D, scene, width, height,
            camera3D: scene.ActiveCamera, prepareEnvironmentLighting3D: false,
            renderShadows3D: false, frameSink: sink);
        Scenes.RenderInternal(context);
        RenderPreparationMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Scenes.UnloadScene();
        _renderer3D.Dispose();
        _disposed = true;
    }
}
