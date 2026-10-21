using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

public enum TopDownFacing { Cursor, Movement, Independent }

/// <summary>Duckov-style input/facing adapter; CharacterController3D owns all physical movement.
/// Based on EugeneVorobyev/godot-duckov-controller (MIT); see ThirdPartyNotices/DuckovController.txt.</summary>
public sealed class TopDownController3D : Component
{
    public InputActionReference MoveAction { get; set; } = InputActionReference.Named("Move");
    public InputActionReference JumpAction { get; set; } = InputActionReference.Named("Jump");
    public Guid CameraObjectId { get; set; }
    public bool InputEnabled { get; set; } = true;
    public bool CameraRelativeMovement { get; set; } = true;
    public bool AllowJump { get; set; }
    public TopDownFacing Facing { get; set; } = TopDownFacing.Cursor;
    public float TurnSharpness { get; set; } = 20f;
    public float AimPlaneHeight { get; set; } = .8f;
    public Vector3 MoveDirection { get; private set; }
    public Vector3 AimWorldPoint { get; private set; }
    public bool HasCursorAim { get; private set; }
    public override int UpdateOrder => -300;

    protected override void OnUpdate()
    {
        var player = AttachedGameObject;
        var motor = player?.GetComponent<CharacterController3D>();
        if (player == null || motor?.Enabled != true) return;
        if (Facing == TopDownFacing.Cursor) Input.NotifyGameViewPointerAim();
        var camera = CameraObjectId == Guid.Empty ? player.Scene?.ActiveCamera : player.Scene?.FindGameObject(CameraObjectId)?.GetComponent<Camera3D>();
        // Two input owners must not silently fight over the motor/rotation.
        if (player.GetComponent<PlayerController3D>()?.Enabled == true)
        { MoveDirection = Vector3.Zero; HasCursorAim = false; return; }
        bool accept = InputEnabled && InputActions.GameplayEnabled;
        MoveDirection = CalculateMovement(accept ? InputActions.ReadAxis2D(MoveAction) : Vector2.Zero,
            CameraRelativeMovement ? camera?.Transform.Forward ?? -Vector3.UnitZ : -Vector3.UnitZ);
        motor.Move(MoveDirection);
        HasCursorAim = false;
        Vector3 facing = MoveDirection;
        if (accept && Facing == TopDownFacing.Cursor && Input.IsPointerOverGameView && camera != null &&
            TryCursorPoint(camera, Input.GameViewPointerNormalized, Input.GameViewSize, player.Transform.WorldPosition.Y + Safe(AimPlaneHeight), out var point))
        { AimWorldPoint = point; HasCursorAim = true; facing = point - player.Transform.WorldPosition; }
        else if (Facing == TopDownFacing.Cursor) facing = Vector3.Zero;
        facing.Y = 0;
        if (accept && Facing != TopDownFacing.Independent && facing.LengthSquared() > .0001f)
        {
            float yaw = MathF.Atan2(-facing.X, -facing.Z);
            var desired = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
            player.Transform.WorldRotation = Quaternion.Slerp(player.Transform.WorldRotation, desired,
                SmoothFactor(TurnSharpness, (float)Time.DeltaTime));
        }
        if (accept && AllowJump && InputActions.WasPressed(JumpAction)) motor.Jump();
    }

    public static Vector3 CalculateMovement(Vector2 input, Vector3 cameraForward)
    {
        cameraForward.Y = 0;
        if (!float.IsFinite(cameraForward.LengthSquared()) || cameraForward.LengthSquared() < .0001f) cameraForward = -Vector3.UnitZ;
        cameraForward = Vector3.Normalize(cameraForward);
        if (!float.IsFinite(input.LengthSquared())) return Vector3.Zero;
        if (input.LengthSquared() > 1) input = Vector2.Normalize(input);
        return cameraForward * input.Y + Vector3.Cross(cameraForward, Vector3.UnitY) * input.X;
    }

    public static bool TryCursorPoint(Camera3D camera, Vector2 pointer, Vector2 viewport, float height, out Vector3 point)
    {
        point = default;
        if (viewport.X <= 0 || viewport.Y <= 0 || !float.IsFinite(viewport.LengthSquared())) return false;
        var ray = camera.ScreenPointToRay(pointer, viewport.X / viewport.Y);
        if (!float.IsFinite(ray.Direction.Y) || Math.Abs(ray.Direction.Y) < .00001f) return false;
        float distance = (height - ray.Origin.Y) / ray.Direction.Y;
        if (!float.IsFinite(distance) || distance < 0 || distance > camera.FarClip) return false;
        point = ray.Origin + ray.Direction * distance;
        return float.IsFinite(point.LengthSquared());
    }

    internal static float Safe(float value) => float.IsFinite(value) ? value : 0;
    internal static float SmoothFactor(float sharpness, float seconds) => sharpness <= 0 ? 1 :
        1 - MathF.Exp(-Math.Max(0, Safe(sharpness)) * Math.Max(0, Safe(seconds)));
}
