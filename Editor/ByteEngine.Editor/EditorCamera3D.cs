using System.Numerics;

namespace ByteEngine.Editor;

internal sealed class EditorCamera3D
{
    public Vector3 Position { get; set; } = new(5f, 4f, 7f);
    public float Yaw { get; set; } = -135f;
    public float Pitch { get; set; } = -22f;
    public float FieldOfView { get; set; } = 60f;

    public Vector3 Forward
    {
        get
        {
            float yaw = Yaw * MathF.PI / 180f;
            float pitch = Pitch * MathF.PI / 180f;
            return Vector3.Normalize(new Vector3(
                MathF.Cos(pitch) * MathF.Cos(yaw),
                MathF.Sin(pitch),
                MathF.Cos(pitch) * MathF.Sin(yaw)));
        }
    }

    public Vector3 Right => Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
    public Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Position + Forward, Vector3.UnitY);

    public Matrix4x4 Projection(float aspectRatio) => Matrix4x4.CreatePerspectiveFieldOfView(
        FieldOfView * MathF.PI / 180f,
        Math.Max(aspectRatio, .001f),
        .05f,
        2000f);

    public void Reset()
    {
        Position = new Vector3(5f, 4f, 7f);
        Yaw = -135f;
        Pitch = -22f;
    }

    public void Frame(ByteEngine.Core.Scene.GameObject target)
    {
        Vector3 size = target.Transform.WorldScale;
        float largestDimension = Math.Max(Math.Max(Math.Abs(size.X), Math.Abs(size.Y)), Math.Abs(size.Z));
        Position = target.Transform.WorldPosition - Forward * (largestDimension * 3f + 2f);
    }
}
