using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

public enum CharacterRotationMode
{
    FaceMovement,
    FaceCamera,
    Independent
}

/// <summary>
/// Converts player input into movement intent and persistent control rotation.
/// Physical movement remains in CharacterController3D and camera placement in CameraBoom3D.
/// </summary>
public sealed class PlayerController3D : Component
{
    private float _controlYaw;
    private float _controlPitch = 12f;
    private float _turnSpeed = 540f;
    private float _desiredCharacterYaw;

    public bool UseLocalOrientation { get; set; }
    public CharacterRotationMode CharacterRotation { get; set; } = CharacterRotationMode.FaceCamera;
    public float TurnSpeed { get => _turnSpeed; set => _turnSpeed = Positive(value); }
    public float ControlYaw { get => _controlYaw; set => _controlYaw = NormalizeAngle(Finite(value)); }
    public float ControlPitch { get => _controlPitch; set => _controlPitch = Math.Clamp(Finite(value), -89f, 89f); }
    public float DesiredCharacterYaw => _desiredCharacterYaw;
    public override int UpdateOrder => -300;

    public void SetControlRotation(float yaw, float pitch, float minPitch = -40f, float maxPitch = 65f)
    {
        ControlYaw = yaw;
        _controlPitch = Math.Clamp(Finite(pitch), Math.Min(minPitch, maxPitch), Math.Max(minPitch, maxPitch));
    }

    public void AddLookInput(float yawDelta, float pitchDelta, float minPitch = -40f, float maxPitch = 65f) =>
        SetControlRotation(ControlYaw + yawDelta, ControlPitch + pitchDelta, minPitch, maxPitch);

    protected override void OnUpdate()
    {
        GameObject? player = AttachedGameObject;
        CharacterController3D? controller = player?.GetComponent<CharacterController3D>();
        if (player == null || controller?.Enabled != true) return;

        CameraBoom3D? boom = player.GetComponent<CameraBoom3D>();
        if (Input.IsGameInputCaptured)
        {
            Vector2 mouse = Input.MouseDelta;
            Vector2 look = CalculateLookDelta(mouse, boom);
            AddLookInput(
                look.X,
                look.Y,
                boom?.MinPitch ?? -40f,
                boom?.MaxPitch ?? 65f);
        }

        float forwardInput = (Input.IsKeyDown(Key.W) ? 1f : 0f) - (Input.IsKeyDown(Key.S) ? 1f : 0f);
        float rightInput = (Input.IsKeyDown(Key.D) ? 1f : 0f) - (Input.IsKeyDown(Key.A) ? 1f : 0f);
        Vector3 forward;
        Vector3 right;

        if (UseLocalOrientation)
        {
            forward = Horizontal(player.Transform.Forward);
            right = Horizontal(player.Transform.Right);
        }
        else
        {
            forward = ForwardFromYaw(ControlYaw);
            right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        }

        Vector3 movement = forward * forwardInput + right * rightInput;
        if (movement.LengthSquared() > 1f) movement = Vector3.Normalize(movement);
        controller.Move(movement);
        UpdateCharacterRotation(movement, Math.Max((float)Time.DeltaTime, 0f));
        if (Input.IsKeyPressed(Key.Space)) controller.Jump();
    }

    public void UpdateCharacterRotation(Vector3 movement, float deltaTime)
    {
        GameObject? player = AttachedGameObject;
        if (player == null || CharacterRotation == CharacterRotationMode.Independent) return;

        bool hasMovement = Horizontal(movement).LengthSquared() > .0001f;
        if (CharacterRotation == CharacterRotationMode.FaceMovement && !hasMovement) return;

        _desiredCharacterYaw = CharacterRotation == CharacterRotationMode.FaceCamera
            ? ControlYaw
            : YawFromDirection(movement);

        Vector3 euler = player.Transform.EulerAngles;
        euler.Y = MoveTowardsAngle(euler.Y, _desiredCharacterYaw, TurnSpeed * Math.Max(deltaTime, 0f));
        player.Transform.EulerAngles = euler;
    }

    public static float DeltaAngle(float current, float target) => NormalizeAngle(target - current);

    public static float MoveTowardsAngle(float current, float target, float maxDelta)
    {
        float delta = DeltaAngle(current, target);
        if (MathF.Abs(delta) <= Math.Max(maxDelta, 0f)) return NormalizeAngle(target);
        return NormalizeAngle(current + MathF.CopySign(Math.Max(maxDelta, 0f), delta));
    }

    public static float YawFromDirection(Vector3 direction)
    {
        Vector3 horizontal = Horizontal(direction);
        return horizontal.LengthSquared() < .0001f
            ? 0f
            : NormalizeAngle(MathF.Atan2(horizontal.X, -horizontal.Z) * 180f / MathF.PI);
    }

    public static Vector3 ForwardFromYaw(float yawDegrees)
    {
        float radians = yawDegrees * MathF.PI / 180f;
        return Vector3.Normalize(new Vector3(MathF.Sin(radians), 0f, -MathF.Cos(radians)));
    }

    public static Vector2 CalculateLookDelta(Vector2 mouseDelta, CameraBoom3D? boom)
    {
        float horizontal = mouseDelta.X * (boom?.MouseSensitivityX ?? .12f);
        float vertical = -mouseDelta.Y * (boom?.MouseSensitivityY ?? .1f);
        if (boom?.InvertHorizontalLook == true) horizontal = -horizontal;
        if (boom?.InvertVerticalLook == true) vertical = -vertical;
        return new Vector2(horizontal, vertical);
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.Y = 0f;
        return value.LengthSquared() > .0001f ? Vector3.Normalize(value) : Vector3.Zero;
    }

    private static float NormalizeAngle(float value)
    {
        value = Finite(value) % 360f;
        if (value > 180f) value -= 360f;
        if (value <= -180f) value += 360f;
        return value;
    }

    private static float Positive(float value) => Math.Max(0f, Finite(value));
    private static float Finite(float value) => float.IsFinite(value) ? value : 0f;
}
