using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.InputSystem;

namespace ByteEngine.Core.Gameplay;

public enum CharacterRotationMode
{
    /// <summary>
    /// Standard third-person locomotion. Movement is camera-relative by default,
    /// while the character turns toward the actual movement direction.
    /// </summary>
    FaceMovement,

    /// <summary>
    /// Aim/strafe locomotion. The character faces the camera/control yaw even
    /// while moving sideways or backwards.
    /// </summary>
    FaceCamera,

    /// <summary>
    /// PlayerController3D does not rotate the character.
    /// </summary>
    Independent
}

/// <summary>
/// Converts player input into movement intent and persistent control rotation.
///
/// TPS-B separates movement direction from facing direction:
/// - camera-relative movement remains the default movement space;
/// - FaceMovement is the standard third-person default;
/// - FaceCamera is an explicit aim/strafe mode;
/// - CharacterController3D remains the sole owner of physical movement.
///
/// TPS-E adds idle turn-in-place and fixes the transform/control yaw convention:
/// - control yaw +90 looks toward world +X;
/// - gameplay character yaw +90 faces world -X;
/// - character-facing targets therefore convert control yaw into character yaw;
/// - idle FaceMovement can free-orbit before smoothly catching up.
///
/// TPS-F keeps gameplay-character rotation quaternion-native. It never performs
/// a read/modify/write round-trip through Transform.EulerAngles while turning.
/// That avoids the Euler decomposition feedback that can alternate the reported
/// yaw every frame once a character has a non-zero pitch/roll basis.
///
/// Camera placement remains in CameraBoom3D.
/// </summary>
public sealed class PlayerController3D : Component
{
    private float _controlYaw;
    private float _controlPitch = 12f;
    private float _turnSpeed = 540f;
    private float _desiredCharacterYaw;
    private bool _idleTurnInPlaceActive;

    /*
     * TPS-E turn-in-place defaults. These values were visually validated before
     * being promoted to normal authorable component properties.
     */
    private float _idleTurnStartAngle = 60f;
    private float _idleTurnFinishAngle = 5f;
    private float _idleTurnSpeed = 300f;

    public InputActionReference MoveAction { get; set; } = InputActionReference.Named("Move");
    public InputActionReference LookAction { get; set; } = InputActionReference.Named("Look");
    public InputActionReference JumpAction { get; set; } = InputActionReference.Named("Jump");
    public InputActionReference SprintAction { get; set; } = InputActionReference.Named("Sprint");

    /// <summary>
    /// When false (default), movement input is resolved relative to ControlYaw,
    /// giving standard third-person camera-relative movement.
    ///
    /// When true, movement uses the character's local forward/right axes.
    /// </summary>
    public bool UseLocalOrientation { get; set; }
    public bool AcceptLookInput { get; set; } = true;

    /// <summary>
    /// Standard TPS defaults to FaceMovement. FaceCamera is intended for
    /// aim/strafe gameplay where the body should follow camera yaw.
    /// </summary>
    public CharacterRotationMode CharacterRotation { get; set; } =
        CharacterRotationMode.FaceMovement;

    public float TurnSpeed
    {
        get => _turnSpeed;
        set => _turnSpeed = Positive(value);
    }

    /// <summary>
    /// Camera/body yaw separation allowed while idle before FaceMovement starts
    /// a turn-in-place. Range: 0..180 degrees.
    /// </summary>
    public float IdleTurnStartAngle
    {
        get => _idleTurnStartAngle;
        set
        {
            _idleTurnStartAngle =
                Math.Clamp(
                    Positive(value),
                    0f,
                    180f);

            if (_idleTurnFinishAngle >
                _idleTurnStartAngle)
            {
                _idleTurnFinishAngle =
                    _idleTurnStartAngle;
            }
        }
    }

    /// <summary>
    /// Remaining yaw difference at which an active idle turn is considered
    /// complete. Clamped so it cannot exceed IdleTurnStartAngle.
    /// </summary>
    public float IdleTurnFinishAngle
    {
        get => _idleTurnFinishAngle;
        set =>
            _idleTurnFinishAngle =
                Math.Clamp(
                    Positive(value),
                    0f,
                    IdleTurnStartAngle);
    }

    /// <summary>
    /// Rotation speed used only by idle FaceMovement turn-in-place.
    /// </summary>
    public float IdleTurnSpeed
    {
        get => _idleTurnSpeed;
        set => _idleTurnSpeed = Positive(value);
    }

    public float ControlYaw
    {
        get => _controlYaw;
        set => _controlYaw = NormalizeAngle(Finite(value));
    }

    public float ControlPitch
    {
        get => _controlPitch;
        set => _controlPitch = Math.Clamp(Finite(value), -89f, 89f);
    }

    public float DesiredCharacterYaw => _desiredCharacterYaw;

    public override int UpdateOrder => -300;

    protected override void OnStart()
    {
        GameObject? player = AttachedGameObject;

        if (player != null)
        {
            _desiredCharacterYaw =
                CurrentCharacterYaw(
                    player.Transform);
        }

        _idleTurnInPlaceActive = false;
    }

    public void SetControlRotation(
        float yaw,
        float pitch,
        float minPitch = -40f,
        float maxPitch = 65f)
    {
        ControlYaw = yaw;

        _controlPitch =
            Math.Clamp(
                Finite(pitch),
                Math.Min(minPitch, maxPitch),
                Math.Max(minPitch, maxPitch));
    }

    public void AddLookInput(
        float yawDelta,
        float pitchDelta,
        float minPitch = -40f,
        float maxPitch = 65f) =>
        SetControlRotation(
            ControlYaw + yawDelta,
            ControlPitch + pitchDelta,
            minPitch,
            maxPitch);

    protected override void OnUpdate()
    {
        GameObject? player = AttachedGameObject;
        CharacterController3D? controller =
            player?.GetComponent<CharacterController3D>();

        if (player == null ||
            controller?.Enabled != true)
        {
            _idleTurnInPlaceActive = false;
            return;
        }

        CameraBoom3D? boom =
            player.GetComponent<CameraBoom3D>();

        if (AcceptLookInput && InputActions.GameplayEnabled)
        {
            Vector2 lookInput =
                InputActions.ReadAxis2D(LookAction);

            Vector2 look =
                CalculateLookDelta(
                    lookInput,
                    boom);

            AddLookInput(
                look.X,
                look.Y,
                boom?.MinPitch ?? -40f,
                boom?.MaxPitch ?? 65f);
        }

        Vector2 moveInput =
            InputActions.ReadAxis2D(MoveAction);

        float forwardInput =
            moveInput.Y;

        float rightInput =
            moveInput.X;

        Vector3 forward;
        Vector3 right;

        if (UseLocalOrientation)
        {
            forward =
                Horizontal(
                    player.Transform.Forward);

            right =
                Horizontal(
                    player.Transform.Right);
        }
        else
        {
            /*
             * Standard TPS movement:
             * input follows camera/control yaw independently of body facing.
             */
            forward =
                ForwardFromYaw(
                    ControlYaw);

            right =
                Vector3.Normalize(
                    Vector3.Cross(
                        forward,
                        Vector3.UnitY));
        }

        Vector3 movement =
            forward * forwardInput +
            right * rightInput;

        if (movement.LengthSquared() > 1f)
        {
            movement =
                Vector3.Normalize(movement);
        }

        controller.Move(movement);

        UpdateCharacterRotation(
            movement,
            Math.Max(
                (float)Time.DeltaTime,
                0f));

        if (InputActions.WasPressed(JumpAction))
        {
            controller.Jump();
        }
    }

    public void UpdateCharacterRotation(
        Vector3 movement,
        float deltaTime)
    {
        GameObject? player = AttachedGameObject;

        if (player == null)
        {
            _idleTurnInPlaceActive = false;
            return;
        }

        if (CharacterRotation ==
            CharacterRotationMode.Independent)
        {
            _idleTurnInPlaceActive = false;
            return;
        }

        bool hasMovement =
            Horizontal(movement)
                .LengthSquared() >
            .0001f;

        /*
         * Never use Transform.EulerAngles as the live feedback value here.
         *
         * Transform's quaternion -> Euler decomposition is useful for editor
         * display, but read/modify/write feedback can change the decomposed
         * yaw when the quaternion also contains pitch/roll. At high headings
         * this showed up as alternating ~20-degree yaw changes every frame.
         *
         * The gameplay root is an upright capsule, so its authoritative yaw is
         * derived from the world-space forward vector and written back as a
         * pure world-Y quaternion.
         */
        float currentYaw =
            CurrentCharacterYaw(
                player.Transform);

        if (CharacterRotation ==
                CharacterRotationMode.FaceMovement &&
            !hasMovement)
        {
            /*
             * Control yaw and gameplay character yaw have opposite signs in
             * ByteEngine's current conventions. Convert first, then measure
             * the real body heading difference.
             */
            float cameraFacingYaw =
                TransformYawFromControlYaw(
                    ControlYaw);

            float cameraBodyDelta =
                DeltaAngle(
                    currentYaw,
                    cameraFacingYaw);

            float absoluteDelta =
                MathF.Abs(cameraBodyDelta);

            if (!_idleTurnInPlaceActive)
            {
                if (absoluteDelta <
                    IdleTurnStartAngle)
                {
                    _desiredCharacterYaw =
                        currentYaw;
                    return;
                }

                _idleTurnInPlaceActive = true;
            }

            _desiredCharacterYaw =
                cameraFacingYaw;

            /*
             * Once turn-in-place starts, finish the turn toward the camera
             * instead of stopping with a visible body/camera mismatch.
             */
            if (absoluteDelta <=
                IdleTurnFinishAngle)
            {
                SetCharacterYaw(
                    player.Transform,
                    cameraFacingYaw);

                _idleTurnInPlaceActive = false;
                return;
            }

            float nextYaw =
                MoveTowardsAngle(
                    currentYaw,
                    _desiredCharacterYaw,
                    IdleTurnSpeed *
                    Math.Max(deltaTime, 0f));

            SetCharacterYaw(
                player.Transform,
                nextYaw);

            return;
        }

        /*
         * Moving FaceMovement and explicit FaceCamera immediately own facing.
         */
        _idleTurnInPlaceActive = false;

        _desiredCharacterYaw =
            CharacterRotation ==
                CharacterRotationMode.FaceCamera
                ? TransformYawFromControlYaw(
                    ControlYaw)
                : YawFromDirection(
                    movement);

        float movingYaw =
            MoveTowardsAngle(
                currentYaw,
                _desiredCharacterYaw,
                TurnSpeed *
                Math.Max(deltaTime, 0f));

        SetCharacterYaw(
            player.Transform,
            movingYaw);
    }

    /// <summary>
    /// Converts camera/control yaw into gameplay-character yaw.
    /// Camera/control +90 looks toward +X, while character +90 faces -X.
    /// </summary>
    public static float TransformYawFromControlYaw(
        float controlYaw) =>
        NormalizeAngle(
            -Finite(controlYaw));

    public static float DeltaAngle(
        float current,
        float target) =>
        NormalizeAngle(target - current);

    public static float MoveTowardsAngle(
        float current,
        float target,
        float maxDelta)
    {
        float delta =
            DeltaAngle(current, target);

        if (MathF.Abs(delta) <=
            Math.Max(maxDelta, 0f))
        {
            return NormalizeAngle(target);
        }

        return NormalizeAngle(
            current +
            MathF.CopySign(
                Math.Max(maxDelta, 0f),
                delta));
    }

    /// <summary>
    /// Returns gameplay-character yaw that makes Transform.Forward face direction.
    /// </summary>
    public static float YawFromDirection(
        Vector3 direction)
    {
        Vector3 horizontal =
            Horizontal(direction);

        return horizontal.LengthSquared() <
               .0001f
            ? 0f
            : NormalizeAngle(
                -MathF.Atan2(
                    horizontal.X,
                    -horizontal.Z) *
                180f /
                MathF.PI);
    }

    /// <summary>
    /// Returns world movement/camera-forward direction from control yaw.
    /// This remains control-space yaw and intentionally is NOT negated.
    /// </summary>
    public static Vector3 ForwardFromYaw(
        float yawDegrees)
    {
        float radians =
            yawDegrees *
            MathF.PI /
            180f;

        return Vector3.Normalize(
            new Vector3(
                MathF.Sin(radians),
                0f,
                -MathF.Cos(radians)));
    }

    public static Vector2 CalculateLookDelta(
        Vector2 lookInput,
        CameraBoom3D? boom)
    {
        float horizontal =
            lookInput.X *
            (boom?.MouseSensitivityX ?? .12f);

        /*
         * InputActions normalizes all 2D look sources to +Y = up.
         * Negative pitch therefore means looking upward in CameraBoom3D.
         */
        float vertical =
            -lookInput.Y *
            (boom?.MouseSensitivityY ?? .1f);

        if (boom?.InvertHorizontalLook == true)
        {
            horizontal =
                -horizontal;
        }

        if (boom?.InvertVerticalLook == true)
        {
            vertical =
                -vertical;
        }

        return new Vector2(
            horizontal,
            vertical);
    }

    private static float CurrentCharacterYaw(
        Transform transform)
    {
        Vector3 forward =
            Horizontal(
                transform.Forward);

        return forward.LengthSquared() >
               .0001f
            ? YawFromDirection(
                forward)
            : 0f;
    }

    private static void SetCharacterYaw(
        Transform transform,
        float yawDegrees)
    {
        /*
         * CharacterController3D owns an upright world-space capsule. Any
         * imported/model-facing correction belongs on the model/visual child,
         * not on the gameplay root. Writing a pure world-Y quaternion therefore
         * removes accidental pitch/roll feedback without touching visual-model
         * correction.
         */
        transform.WorldRotation =
            Quaternion.CreateFromAxisAngle(
                Vector3.UnitY,
                yawDegrees *
                MathF.PI /
                180f);
    }

    private static Vector3 Horizontal(
        Vector3 value)
    {
        value.Y = 0f;

        return value.LengthSquared() >
               .0001f
            ? Vector3.Normalize(value)
            : Vector3.Zero;
    }

    private static float NormalizeAngle(
        float value)
    {
        value =
            Finite(value) %
            360f;

        if (value > 180f)
        {
            value -= 360f;
        }

        if (value <= -180f)
        {
            value += 360f;
        }

        return value;
    }

    private static float Positive(
        float value) =>
        Math.Max(
            0f,
            Finite(value));

    private static float Finite(
        float value) =>
        float.IsFinite(value)
            ? value
            : 0f;
}
