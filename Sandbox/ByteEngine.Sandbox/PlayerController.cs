using ByteEngine.Core;
using ByteEngine.Core.Scene;

using Vector2 = System.Numerics.Vector2;

namespace ByteEngine.Sandbox;

public sealed class PlayerController
    : Component
{
    public float Speed { get; set; } =
        300.0f;

    protected override void OnUpdate()
    {
        Vector2 movement =
            Vector2.Zero;

        if (Input.IsKeyDown(Key.W) ||
            Input.IsKeyDown(Key.Up))
        {
            movement.Y -= 1.0f;
        }

        if (Input.IsKeyDown(Key.S) ||
            Input.IsKeyDown(Key.Down))
        {
            movement.Y += 1.0f;
        }

        if (Input.IsKeyDown(Key.A) ||
            Input.IsKeyDown(Key.Left))
        {
            movement.X -= 1.0f;
        }

        if (Input.IsKeyDown(Key.D) ||
            Input.IsKeyDown(Key.Right))
        {
            movement.X += 1.0f;
        }

        if (movement != Vector2.Zero)
        {
            movement =
                Vector2.Normalize(
                    movement
                );
        }

        Transform.Position +=
            movement *
            Speed *
            (float)Time.DeltaTime;
    }
}