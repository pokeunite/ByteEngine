using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class PlayerController3D : Component
{
    public bool UseLocalOrientation { get; set; } = true;
    public override int UpdateOrder => -100;

    protected override void OnUpdate()
    {
        GameObject? player = AttachedGameObject;
        CharacterController3D? controller =
            player?.GetComponent<CharacterController3D>();

        if (player == null ||
            controller == null ||
            controller.Enabled == false)
        {
            return;
        }

        float forwardInput =
            (Input.IsKeyDown(Key.W) ? 1f : 0f) -
            (Input.IsKeyDown(Key.S) ? 1f : 0f);

        float rightInput =
            (Input.IsKeyDown(Key.D) ? 1f : 0f) -
            (Input.IsKeyDown(Key.A) ? 1f : 0f);

        Vector3 forward;
        Vector3 right;

        if (UseLocalOrientation)
        {
            forward = Horizontal(player.Transform.Forward);
            right = Horizontal(player.Transform.Right);
        }
        else
        {
            Camera3D? camera = player.Scene?.ActiveCamera;

            if (camera != null)
            {
                forward = Horizontal(camera.Transform.Forward);
                right = Horizontal(camera.Transform.Right);
            }
            else
            {
                forward = -Vector3.UnitZ;
                right = Vector3.UnitX;
            }
        }

        Vector3 movement =
            forward * forwardInput +
            right * rightInput;

        if (movement.LengthSquared() > 1f)
        {
            movement = Vector3.Normalize(movement);
        }

        controller.Move(movement);

        if (Input.IsKeyPressed(Key.Space))
        {
            controller.Jump();
        }
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.Y = 0f;

        return value.LengthSquared() > .0001f
            ? Vector3.Normalize(value)
            : Vector3.Zero;
    }
}
