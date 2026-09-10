using System.Numerics;

namespace ByteEngine.Core.Scene;

public sealed class Transform2D
{
    public Vector2 Position { get; set; }

    public Vector2 Size { get; set; }

    public float Rotation { get; set; }

    public Transform2D()
    {
        Position = Vector2.Zero;
        Size = new Vector2(
            64.0f,
            64.0f
        );

        Rotation = 0.0f;
    }
}