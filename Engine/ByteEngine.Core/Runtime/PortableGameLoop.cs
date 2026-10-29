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
    private readonly Graphics.ThreeD.RenderWorld _renderWorld = new();
    private bool _disposed;
    public double LastInputMilliseconds {get;private set;}
    public double LastUpdateMilliseconds {get;private set;}
    public double LastRenderMilliseconds {get;private set;}
    public long LastRenderAllocatedBytes {get;private set;}

    public void Tick(double seconds, RawInputSnapshot input, int width, int height,
        Vector2 pointer, bool focused, bool captured, IRenderFrameSink sink)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(sink);
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        long start=System.Diagnostics.Stopwatch.GetTimestamp();
        Time.Update(Math.Min(seconds, .1)); // No catch-up burst after a suspended tab.
        Input.UpdatePortable(input, captured);
        Input.SetGameViewPointer(pointer, new(width, height), focused, focused, pointer * new Vector2(width, height));
        InputActions.Update(Input.Snapshot, focused);
        LastInputMilliseconds=System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        start=System.Diagnostics.Stopwatch.GetTimestamp();
        Scenes.UpdateInternal();
        LastUpdateMilliseconds=System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        LastRenderMilliseconds=0;LastRenderAllocatedBytes=0;
        if (Scenes.ActiveScene is not { } scene) return;
        var context = new RenderContext(_renderer2D, _renderer3D, scene, width, height,
            camera3D: scene.ActiveCamera, prepareEnvironmentLighting3D: false,
            renderShadows3D: false, frameSink: sink, renderWorld: _renderWorld);
        start=System.Diagnostics.Stopwatch.GetTimestamp();long before=GC.GetAllocatedBytesForCurrentThread();
        Scenes.RenderInternal(context);
        LastRenderMilliseconds=System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        LastRenderAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-before;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Scenes.UnloadScene();
        _renderer3D.Dispose();
        _disposed = true;
    }
}
