using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Immutable camera/view data captured at the beginning of a 3D render pass.
/// </summary>
public readonly record struct RenderView3D
{
    public Matrix4x4 ViewMatrix { get; }

    public Matrix4x4 ProjectionMatrix { get; }

    public Matrix4x4 ViewProjectionMatrix { get; }

    public Vector3 CameraPosition { get; }

    public int TargetWidth { get; }

    public int TargetHeight { get; }

    public float AspectRatio =>
        (float)TargetWidth /
        Math.Max(
            TargetHeight,
            1);

    public Frustum3D Frustum { get; }

    public RenderView3D(
        Matrix4x4 viewMatrix,
        Matrix4x4 projectionMatrix,
        Vector3 cameraPosition,
        int targetWidth,
        int targetHeight)
    {
        ViewMatrix =
            viewMatrix;

        ProjectionMatrix =
            projectionMatrix;

        ViewProjectionMatrix =
            viewMatrix *
            projectionMatrix;

        CameraPosition =
            cameraPosition;

        TargetWidth =
            Math.Max(
                targetWidth,
                1);

        TargetHeight =
            Math.Max(
                targetHeight,
                1);

        Frustum =
            new Frustum3D(
                ViewProjectionMatrix);
    }
}
