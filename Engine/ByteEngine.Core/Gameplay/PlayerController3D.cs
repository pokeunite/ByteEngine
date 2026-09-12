using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class PlayerController3D : Component
{
    public bool UseLocalOrientation { get; set; } = true;
    public override int UpdateOrder => -100;

    protected override void OnUpdate()
    {
        CharacterController3D? controller = AttachedGameObject?.GetComponent<CharacterController3D>();
        if (controller == null || controller.Enabled == false) return;
        float forward = (Input.IsKeyDown(Key.W) ? 1f : 0f) - (Input.IsKeyDown(Key.S) ? 1f : 0f);
        float right = (Input.IsKeyDown(Key.D) ? 1f : 0f) - (Input.IsKeyDown(Key.A) ? 1f : 0f);
        Vector3 movement = UseLocalOrientation
            ? Horizontal(Transform.Forward) * forward + Horizontal(Transform.Right) * right
            : new Vector3(right, 0f, -forward);
        if (movement.LengthSquared() > 1f) movement = Vector3.Normalize(movement);
        controller.Move(movement);
        if (Input.IsKeyPressed(Key.Space)) controller.Jump();
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.Y = 0f;
        return value.LengthSquared() > .0001f ? Vector3.Normalize(value) : Vector3.Zero;
    }
}
