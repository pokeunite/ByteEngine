using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Graphics;

public sealed class RenderContext
{
    public Renderer2D Renderer2D { get; }
    public Renderer3D Renderer3D { get; }
    public ByteEngine.Core.Scene.Scene Scene { get; }
    public int TargetWidth { get; }
    public int TargetHeight { get; }
    public Camera2D? Camera2D { get; }
    public Camera3D? Camera3D { get; }
    public Matrix4x4? ViewMatrix3D { get; }
    public Matrix4x4? ProjectionMatrix3D { get; }
    public bool Has3DCamera => Camera3D != null || (ViewMatrix3D.HasValue && ProjectionMatrix3D.HasValue);
    public float AspectRatio => (float)TargetWidth / Math.Max(TargetHeight, 1);

    public RenderContext(Renderer2D renderer2D, Renderer3D renderer3D, ByteEngine.Core.Scene.Scene scene,
        int targetWidth, int targetHeight, Camera2D? camera2D = null, Camera3D? camera3D = null,
        Matrix4x4? viewMatrix3D = null, Matrix4x4? projectionMatrix3D = null)
    {
        Renderer2D = renderer2D; Renderer3D = renderer3D; Scene = scene;
        TargetWidth = Math.Max(targetWidth, 1); TargetHeight = Math.Max(targetHeight, 1);
        Camera2D = camera2D; Camera3D = camera3D;
        ViewMatrix3D = viewMatrix3D; ProjectionMatrix3D = projectionMatrix3D;
    }

    public Matrix4x4 GetViewMatrix3D() => Camera3D?.GetViewMatrix() ?? ViewMatrix3D ?? Matrix4x4.Identity;
    public Matrix4x4 GetProjectionMatrix3D() => Camera3D?.GetProjectionMatrix(AspectRatio) ?? ProjectionMatrix3D ?? Matrix4x4.Identity;
}
