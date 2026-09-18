using System.Numerics;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Classification;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

/// <summary>
/// Unreal-style third-person spring arm.
///
/// TPS-C keeps the character/pivot framing stable.
/// TPS-D hardens spring-arm collision:
/// - follows after gameplay/physics in LateUpdate (TPS-A);
/// - position/rotation lag are optional effects, not default locomotion;
/// - orbit, shoulder framing and view rotation are resolved independently;
/// - collision probe radius is already accounted for by SphereCast, so the
///   boom no longer subtracts that radius twice;
/// - solid obstacles pull the camera in immediately;
/// - obstacle removal restores arm length smoothly;
/// - triggers are ignored and CameraCollisionMask is the authoritative probe
///   filter instead of the player's gameplay collision matrix.
///
/// Character movement remains owned by CharacterController3D and facing remains
/// owned by PlayerController3D.
/// </summary>
public sealed class CameraBoom3D : Component, IRuntimeDiagnosticSource
{
    public LayerMask CameraCollisionMask { get; set; } = LayerMask.All;
    private float _armLength = 4.75f;
    private float _pivotHeight = 1.6f;
    private float _yaw;
    private float _pitch = 12f;
    private float _minPitch = -40f;
    private float _maxPitch = 65f;
    private float _mouseSensitivityX = .12f;
    private float _mouseSensitivityY = .09f;
    private float _positionSmoothness = 14f;
    private float _rotationSmoothness = 20f;
    private float _shoulderOffset;
    private float _collisionRadius = .2f;
    private float _collisionSafetyMargin = .05f;
    private float _collisionReturnSpeed = 8f;
    private float _maxLagDistance = 1.5f;
    private float _maxLagTimeStep = 1f / 60f;
    private GameObject? _cameraObject;
    private Vector3 _desiredSocketPosition;
    private Vector3 _actualSocketPosition;
    private Vector3 _smoothedPivot;
    private bool _rigInitialized;
    private bool _collisionHit;
    private GameObject? _collisionObject;
    private float _actualLength;
    private float _desiredBoomYaw;
    private float _desiredBoomPitch;
    private float _smoothedBoomYaw;
    private float _smoothedBoomPitch;

    public Guid CameraObjectId { get; set; }
    public bool UseControlRotation { get; set; } = true;
    public float ArmLength { get => _armLength; set => _armLength = Positive(value); }
    public float PivotHeight { get => _pivotHeight; set => _pivotHeight = Finite(value); }
    public float Yaw { get => _yaw; set => _yaw = Finite(value); }
    public float Pitch { get => _pitch; set => _pitch = Math.Clamp(Finite(value), MinPitch, MaxPitch); }
    public float MinPitch { get => _minPitch; set { _minPitch = Math.Clamp(Finite(value), -89f, 89f); if (_maxPitch < _minPitch) _maxPitch = _minPitch; _pitch = Math.Clamp(_pitch, _minPitch, _maxPitch); } }
    public float MaxPitch { get => _maxPitch; set { _maxPitch = Math.Clamp(Finite(value), -89f, 89f); if (_minPitch > _maxPitch) _minPitch = _maxPitch; _pitch = Math.Clamp(_pitch, _minPitch, _maxPitch); } }
    public float MouseSensitivityX { get => _mouseSensitivityX; set => _mouseSensitivityX = Positive(value); }
    public float MouseSensitivityY { get => _mouseSensitivityY; set => _mouseSensitivityY = Positive(value); }
    public bool InvertHorizontalLook { get; set; }
    public bool InvertVerticalLook { get; set; }
    /// <summary>
/// Optional cinematic position lag. Standard TPS keeps this disabled so the
/// player remains locked to a stable camera pivot.
/// </summary>
    public bool CameraLagEnabled { get; set; } = false;
    /// <summary>
/// Optional camera-rotation lag. Standard TPS follows control rotation directly.
/// </summary>
    public bool RotationLagEnabled { get; set; } = false;
    public bool LagSubstepping { get; set; } = true;
    public float PositionSmoothness { get => _positionSmoothness; set => _positionSmoothness = Positive(value); }
    public float RotationSmoothness { get => _rotationSmoothness; set => _rotationSmoothness = Positive(value); }
    public float MaximumLagDistance { get => _maxLagDistance; set => _maxLagDistance = Positive(value); }
    public float MaxLagTimeStep { get => _maxLagTimeStep; set => _maxLagTimeStep = Math.Clamp(Positive(value), .001f, .1f); }
    public float ShoulderOffset { get => _shoulderOffset; set => _shoulderOffset = Finite(value); }
    /// <summary>
    /// Unreal-style camera probe. New TPS rigs default this on.
    /// Existing scenes that explicitly serialized false keep their authored value.
    /// </summary>
    public bool EnableCameraCollision { get; set; } = true;

    /// <summary>
    /// Radius of the swept camera probe.
    /// </summary>
    public float CollisionRadius
    {
        get => _collisionRadius;
        set => _collisionRadius = Positive(value);
    }

    /// <summary>
    /// Small gap left between the swept camera sphere and a blocking surface.
    /// SphereCast already expands geometry by CollisionRadius, so this margin
    /// must not include CollisionRadius again.
    /// </summary>
    public float CollisionSafetyMargin
    {
        get => _collisionSafetyMargin;
        set => _collisionSafetyMargin = Positive(value);
    }

    /// <summary>
    /// Speed used only when restoring the camera after an obstruction clears.
    /// Pull-in remains immediate to prevent wall clipping.
    /// </summary>
    public float CollisionReturnSpeed
    {
        get => _collisionReturnSpeed;
        set => _collisionReturnSpeed = Positive(value);
    }
    public Vector3 DesiredSocketPosition => _desiredSocketPosition;
    public Vector3 ActualSocketPosition => _actualSocketPosition;
    public float DesiredBoomYaw => _desiredBoomYaw;
    public float DesiredBoomPitch => _desiredBoomPitch;
    public float SmoothedBoomYaw => _smoothedBoomYaw;
    public float SmoothedBoomPitch => _smoothedBoomPitch;
    public float ActualArmLength => _actualLength;
    public override int UpdateOrder => -200;

    protected override void OnStart() => UpdateRig(true);

    /// <summary>
    /// The camera boom follows in the scene-wide late-update phase so it sees
    /// the character's final gameplay/physics transform for the frame.
    /// </summary>
    protected override void OnLateUpdate() => UpdateRig(false);

    public void SnapToSocket() => UpdateRig(true);

    public static Vector3 CalculateOrbitVector(float yawDegrees, float pitchDegrees, float armLength)
    {
        float yaw = Radians(Finite(yawDegrees));
        float pitch = Radians(Math.Clamp(Finite(pitchDegrees), -89f, 89f));
        float length = Positive(armLength);
        return new Vector3(
            -MathF.Sin(yaw) * MathF.Cos(pitch),
            MathF.Sin(pitch),
            MathF.Cos(yaw) * MathF.Cos(pitch)) * length;
    }

    /// <summary>
    /// Camera forward direction for a boom yaw/pitch. This is the inverse of
    /// the unit orbit direction: the camera sits behind the pivot and looks
    /// forward along control rotation.
    /// </summary>
    public static Vector3 CalculateViewForward(
        float yawDegrees,
        float pitchDegrees)
    {
        Vector3 orbit =
            CalculateOrbitVector(
                yawDegrees,
                pitchDegrees,
                1.0f);

        return orbit.LengthSquared() > .000001f
            ? -Vector3.Normalize(orbit)
            : new Vector3(0f, 0f, -1f);
    }

    public static float SmoothAngle(float current, float target, float speed, float deltaTime)
    {
        float factor = ExponentialFactor(speed, deltaTime);
        return current + PlayerController3D.DeltaAngle(current, target) * factor;
    }

    /// <summary>
    /// Converts a sphere-cast hit into an allowed spring-arm length.
    /// hitDistance is already radius-aware.
    /// </summary>
    public static float CalculateCollisionLength(
        float desiredLength,
        float hitDistance,
        float safetyMargin)
    {
        float desired =
            Positive(desiredLength);

        float hit =
            Positive(hitDistance);

        float margin =
            Positive(safetyMargin);

        return Math.Clamp(
            hit - margin,
            0f,
            desired);
    }

    public static int CalculateLagSteps(float deltaTime, bool enabled, float maxStep) =>
        !enabled || deltaTime <= Math.Max(maxStep, .001f)
            ? 1
            : Math.Clamp((int)MathF.Ceiling(deltaTime / Math.Max(maxStep, .001f)), 1, 32);

    private void UpdateRig(bool immediate)
    {
        GameObject? root = AttachedGameObject;
        if (root?.Scene is not { } scene) return;
        _cameraObject = ResolveCamera(root, scene);
        Camera3D? camera = _cameraObject?.GetComponent<Camera3D>();
        if (_cameraObject == null || camera == null) return;

        PlayerController3D? player = UseControlRotation ? root.GetComponent<PlayerController3D>() : null;
        _desiredBoomYaw = player?.ControlYaw ?? Yaw;
        _desiredBoomPitch = Math.Clamp(player?.ControlPitch ?? Pitch, MinPitch, MaxPitch);

        Vector3 desiredPivot = root.Transform.WorldPosition + Vector3.UnitY * PivotHeight;
        float deltaTime = immediate ? 0f : Math.Max((float)Time.DeltaTime, 0f);
        if (!_rigInitialized || immediate)
        {
            _smoothedBoomYaw = _desiredBoomYaw;
            _smoothedBoomPitch = _desiredBoomPitch;
            _smoothedPivot = desiredPivot;
            _actualLength = ArmLength;
            _rigInitialized = true;
        }
        else
        {
            int steps = CalculateLagSteps(deltaTime, LagSubstepping, MaxLagTimeStep);
            float step = steps > 0 ? deltaTime / steps : 0f;
            for (int index = 0; index < steps; index++)
            {
                if (RotationLagEnabled)
                {
                    _smoothedBoomYaw = SmoothAngle(_smoothedBoomYaw, _desiredBoomYaw, RotationSmoothness, step);
                    _smoothedBoomPitch = Lerp(_smoothedBoomPitch, _desiredBoomPitch, ExponentialFactor(RotationSmoothness, step));
                }
                else
                {
                    _smoothedBoomYaw = _desiredBoomYaw;
                    _smoothedBoomPitch = _desiredBoomPitch;
                }

                if (CameraLagEnabled)
                {
                    _smoothedPivot = Vector3.Lerp(_smoothedPivot, desiredPivot, ExponentialFactor(PositionSmoothness, step));
                    Vector3 lag = _smoothedPivot - desiredPivot;
                    if (MaximumLagDistance > 0f && lag.LengthSquared() > MaximumLagDistance * MaximumLagDistance)
                        _smoothedPivot = desiredPivot + Vector3.Normalize(lag) * MaximumLagDistance;
                }
                else
                {
                    _smoothedPivot = desiredPivot;
                }
            }
        }

        /*
         * Resolve framing around the FINAL follow pivot. Shoulder offset is a
         * socket/framing offset; it does not change what direction the camera
         * is looking.
         */
        float yawRadians = Radians(_smoothedBoomYaw);

        Vector3 right =
            new(
                MathF.Cos(yawRadians),
                0f,
                -MathF.Sin(yawRadians));

        Vector3 orbitOffset =
            CalculateOrbitVector(
                _smoothedBoomYaw,
                _smoothedBoomPitch,
                ArmLength);

        Vector3 shoulderOffset =
            right *
            ShoulderOffset;

        Vector3 fullOffset =
            orbitOffset +
            shoulderOffset;

        _desiredSocketPosition =
            _smoothedPivot +
            fullOffset;

        float desiredLength =
            fullOffset.Length();
        float collisionLength = desiredLength;
        _collisionHit = false;
        _collisionObject = null;

        if (EnableCameraCollision &&
            desiredLength > .0001f &&
            GameplayQuery3D.SphereCast(
                scene,
                _smoothedPivot,
                fullOffset,
                CollisionRadius,
                out RaycastHit3D hit,
                desiredLength,
                ignore: root,
                layerMask: CameraCollisionMask,
                source: root,
                bypassCollisionMatrix: true,
                includeTriggers: false))
        {
            _collisionHit = true;
            _collisionObject = hit.GameObject;

            /*
             * GameplayQuery3D.SphereCast intersects against geometry expanded by
             * the cast radius. hit.Distance is therefore already the safe center
             * travel distance for the camera sphere. Subtracting CollisionRadius
             * again over-compresses the spring arm.
             */
            collisionLength =
                CalculateCollisionLength(
                    desiredLength,
                    hit.Distance,
                    CollisionSafetyMargin);
        }

        /*
         * Obstruction response is deliberately asymmetric:
         * - pull in immediately so the camera cannot clip through a wall;
         * - restore smoothly when the view becomes clear.
         */
        if (immediate ||
            collisionLength < _actualLength)
        {
            _actualLength =
                collisionLength;
        }
        else
        {
            _actualLength =
                Lerp(
                    _actualLength,
                    collisionLength,
                    ExponentialFactor(
                        CollisionReturnSpeed,
                        deltaTime));
        }

        Vector3 direction = desiredLength > .0001f ? Vector3.Normalize(fullOffset) : Vector3.Zero;
        _actualSocketPosition = _smoothedPivot + direction * _actualLength;
        _cameraObject.Transform.WorldPosition = _actualSocketPosition;

        /*
         * Do not LookAt(pivot) here. With a shoulder offset, LookAt forces the
         * player back into screen center and defeats over-the-shoulder framing.
         * Unreal-style spring arms keep the camera aligned to control rotation.
         */
        Vector3 viewForward =
            CalculateViewForward(
                _smoothedBoomYaw,
                _smoothedBoomPitch);

        _cameraObject.Transform.WorldRotation =
            LookRotation(viewForward);
    }

    private GameObject? ResolveCamera(GameObject root, RuntimeScene scene)
    {
        if (CameraObjectId != Guid.Empty)
        {
            GameObject? requested = scene.FindGameObject(CameraObjectId);
            if (requested?.GetComponent<Camera3D>() != null && requested.IsDescendantOf(root)) return requested;
        }
        return Descendants(root).FirstOrDefault(item => item.GetComponent<Camera3D>() != null);
    }

    private static IEnumerable<GameObject> Descendants(GameObject root)
    {
        foreach (GameObject child in root.Children)
        {
            yield return child;
            foreach (GameObject descendant in Descendants(child)) yield return descendant;
        }
    }

    public void WriteDiagnostics(RuntimeDiagnosticWriter writer)
    {
        GameObject? root = AttachedGameObject;
        PlayerController3D? player = root?.GetComponent<PlayerController3D>();
        Camera3D? camera = _cameraObject?.GetComponent<Camera3D>();
        Camera3D? active = root?.Scene?.ActiveCamera;
        writer.Section($"CameraBoom3D: {root?.Name ?? "<detached>"}");
        writer.Add("ActiveCamera", Describe(active?.GameObject));
        writer.Add("CameraOwner", Describe(_cameraObject));
        writer.Add("MatchesActiveCamera", ReferenceEquals(camera, active));
        writer.Add("Captured", Input.IsGameInputCaptured);
        writer.Add("MouseDelta", Input.MouseDelta);
        writer.Add("InvertHorizontalLook", InvertHorizontalLook);
        writer.Add("InvertVerticalLook", InvertVerticalLook);
        writer.Add("ControlYaw", player?.ControlYaw ?? Yaw);
        writer.Add("ControlPitch", player?.ControlPitch ?? Pitch);
        writer.Add("DesiredBoomYaw", _desiredBoomYaw);
        writer.Add("DesiredBoomPitch", _desiredBoomPitch);
        writer.Add("SmoothedBoomYaw", _smoothedBoomYaw);
        writer.Add("SmoothedBoomPitch", _smoothedBoomPitch);
        writer.Add("CharacterRotationMode", player?.CharacterRotation);
        writer.Add("DesiredCharacterYaw", player?.DesiredCharacterYaw);
        writer.Add("ActualCharacterYaw", root?.Transform.EulerAngles.Y);
        writer.Add("TurnSpeed", player?.TurnSpeed);
        writer.Add("CameraLagEnabled", CameraLagEnabled);
        writer.Add("RotationLagEnabled", RotationLagEnabled);
        writer.Add("LagSubsteps", LagSubstepping);
        writer.Add("DesiredArmLength", ArmLength);
        writer.Add("ActualArmLength", _actualLength);
        writer.Add("Yaw", Yaw);
        writer.Add("Pitch", Pitch);
        writer.Add("ArmLength", ArmLength);
        writer.Add("PivotHeight", PivotHeight);
        writer.Add("ShoulderOffset", ShoulderOffset);
        writer.Add("StableFollow.DefaultPositionLag", false);
        writer.Add("StableFollow.DefaultRotationLag", false);
        writer.Add("ViewForward", CalculateViewForward(_smoothedBoomYaw, _smoothedBoomPitch));
        writer.Add("DesiredSocketPosition", _desiredSocketPosition);
        writer.Add("ActualSocketPosition", _actualSocketPosition);
        writer.Add("RootPosition", root?.Transform.WorldPosition);
        writer.Add("RootRotation", root?.Transform.WorldRotation);
        writer.Add("Position", _cameraObject?.Transform.WorldPosition);
        writer.Add("Rotation", _cameraObject?.Transform.WorldRotation);
        writer.Add("Forward", _cameraObject?.Transform.Forward);
        writer.Add("FOV", camera?.FieldOfView);
        writer.Add("Collision.Enabled", EnableCameraCollision);
        writer.Add("Collision.Radius", CollisionRadius);
        writer.Add("Collision.SafetyMargin", CollisionSafetyMargin);
        writer.Add("Collision.ReturnSpeed", CollisionReturnSpeed);
        writer.Add("Collision.Hit", _collisionHit);
        writer.Add("Collision.HitObject", Describe(_collisionObject));
        writer.Add("Collision.DesiredLength", ArmLength);
        writer.Add("Collision.ActualLength", _actualLength);
    }

    private static string Describe(GameObject? value) => value == null ? "<none>" : $"{value.Name} ({value.Id})";
    private static float ExponentialFactor(float speed, float deltaTime) =>
        speed <= 0f ? 1f : Math.Clamp(1f - MathF.Exp(-speed * Math.Max(deltaTime, 0f)), 0f, 1f);
    private static Quaternion LookRotation(Vector3 forward)
    {
        float pitch = MathF.Asin(Math.Clamp(forward.Y, -1f, 1f));
        float yaw = MathF.Atan2(-forward.X, -forward.Z);
        return Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f);
    }
    private static float Lerp(float start, float end, float amount) => start + (end - start) * Math.Clamp(amount, 0f, 1f);
    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
    private static float Positive(float value) => Math.Max(0f, Finite(value));
    private static float Finite(float value) => float.IsFinite(value) ? value : 0f;
}
