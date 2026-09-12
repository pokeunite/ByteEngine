using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public sealed class Camera3D : Component
{
    public float FieldOfView { get; set; } = 60f;
    public float NearClip { get; set; } = .1f;
    public float FarClip { get; set; } = 1000f;
    public Matrix4x4 GetViewMatrix() => Matrix4x4.CreateLookAt(Transform.WorldPosition, Transform.WorldPosition + Transform.Forward, Transform.Up);
    public Matrix4x4 GetProjectionMatrix(float aspectRatio) => Matrix4x4.CreatePerspectiveFieldOfView(
        Math.Clamp(FieldOfView, 1f, 179f) * MathF.PI / 180f, Math.Max(aspectRatio, .001f),
        Math.Max(NearClip, .001f), Math.Max(FarClip, NearClip + .001f));

    public (Vector3 Origin, Vector3 Direction) ScreenPointToRay(Vector2 normalizedPoint, float aspectRatio)
    {
        float x = Math.Clamp(normalizedPoint.X, 0f, 1f) * 2f - 1f;
        float y = 1f - Math.Clamp(normalizedPoint.Y, 0f, 1f) * 2f;
        float tangent = MathF.Tan(Math.Clamp(FieldOfView, 1f, 179f) * MathF.PI / 360f);
        Vector3 direction = Transform.Forward + Transform.Right * (x * Math.Max(aspectRatio, .001f) * tangent) +
            Transform.Up * (y * tangent);
        return (Transform.WorldPosition, Vector3.Normalize(direction));
    }
}
