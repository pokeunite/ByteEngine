using System.Numerics;

namespace ByteEngine.Core.Scene;

public sealed class Transform2D
{
    private readonly GameObject? _owner;

    public Vector2 LocalPosition { get; set; }
    public Vector2 LocalSize { get; set; } = new(64f, 64f);
    public float LocalRotation { get; set; }

    public Vector2 Position
    {
        get
        {
            Transform2D? parent = _owner?.Parent?.Transform;
            return parent == null ? LocalPosition : parent.Position + Rotate(LocalPosition, parent.Rotation);
        }
        set
        {
            Transform2D? parent = _owner?.Parent?.Transform;
            LocalPosition = parent == null ? value : Rotate(value - parent.Position, -parent.Rotation);
        }
    }

    public Vector2 Size
    {
        get => LocalSize;
        set => LocalSize = value;
    }

    public float Rotation
    {
        get => (_owner?.Parent?.Transform.Rotation ?? 0f) + LocalRotation;
        set => LocalRotation = value - (_owner?.Parent?.Transform.Rotation ?? 0f);
    }

    public Transform2D() { }
    internal Transform2D(GameObject owner) => _owner = owner;

    private static Vector2 Rotate(Vector2 value, float degrees)
    {
        float radians = degrees * MathF.PI / 180f;
        float cosine = MathF.Cos(radians);
        float sine = MathF.Sin(radians);
        return new Vector2(value.X * cosine - value.Y * sine, value.X * sine + value.Y * cosine);
    }
}
