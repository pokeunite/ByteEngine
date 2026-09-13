using System.Numerics;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

/// <summary>
/// A third-person spring arm. It consumes PlayerController3D control rotation,
/// resolves its camera socket, handles collision, then places a child Camera3D.
/// </summary>
public sealed class CameraBoom3D : Component, IRuntimeDiagnosticSource
{
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
    public bool CameraLagEnabled { get; set; } = true;
    public bool RotationLagEnabled { get; set; } = true;
    public bool LagSubstepping { get; set; } = true;
    public float PositionSmoothness { get => _positionSmoothness; set => _positionSmoothness = Positive(value); }
    public float RotationSmoothness { get => _rotationSmoothness; set => _rotationSmoothness = Positive(value); }
    public float MaximumLagDistance { get => _maxLagDistance; set => _maxLagDistance = Positive(value); }
    public float MaxLagTimeStep { get => _maxLagTimeStep; set => _maxLagTimeStep = Math.Clamp(Positive(value), .001f, .1f); }
    public float ShoulderOffset { get => _shoulderOffset; set => _shoulderOffset = Finite(value); }
    public bool EnableCameraCollision { get; set; }
    public float CollisionRadius { get => _collisionRadius; set => _collisionRadius = Positive(value); }
    public float CollisionReturnSpeed { get => _collisionReturnSpeed; set => _collisionReturnSpeed = Positive(value); }
    public Vector3 DesiredSocketPosition => _desiredSocketPosition;
    public Vector3 ActualSocketPosition => _actualSocketPosition;
    public float DesiredBoomYaw => _desiredBoomYaw;
    public float DesiredBoomPitch => _desiredBoomPitch;
    public float SmoothedBoomYaw => _smoothedBoomYaw;
    public float SmoothedBoomPitch => _smoothedBoomPitch;
    public float ActualArmLength => _actualLength;
    public override int UpdateOrder => -200;

    protected override void OnStart() => UpdateRig(true);
    protected override void OnUpdate() => UpdateRig(false);
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

    public static float SmoothAngle(float current, float target, float speed, float deltaTime)
    {
        float factor = ExponentialFactor(speed, deltaTime);
        return current + PlayerController3D.DeltaAngle(current, target) * factor;
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

        float yawRadians = Radians(_smoothedBoomYaw);
        Vector3 right = new(MathF.Cos(yawRadians), 0f, -MathF.Sin(yawRadians));
        Vector3 fullOffset = CalculateOrbitVector(_smoothedBoomYaw, _smoothedBoomPitch, ArmLength) + right * ShoulderOffset;
        _desiredSocketPosition = desiredPivot + fullOffset;
        float desiredLength = fullOffset.Length();
        float collisionLength = desiredLength;
        _collisionHit = false;
        _collisionObject = null;

        if (EnableCameraCollision && desiredLength > .0001f &&
            GameplayQuery3D.SphereCast(scene, _smoothedPivot, fullOffset, CollisionRadius, out RaycastHit3D hit, desiredLength, root))
        {
            _collisionHit = true;
            _collisionObject = hit.GameObject;
            collisionLength = Math.Max(0f, hit.Distance - CollisionRadius);
        }

        if (immediate || collisionLength < _actualLength)
            _actualLength = collisionLength;
        else
            _actualLength = Lerp(_actualLength, collisionLength, ExponentialFactor(CollisionReturnSpeed, deltaTime));

        Vector3 direction = desiredLength > .0001f ? Vector3.Normalize(fullOffset) : Vector3.Zero;
        _actualSocketPosition = _smoothedPivot + direction * _actualLength;
        _cameraObject.Transform.WorldPosition = _actualSocketPosition;

        Vector3 lookDirection = _smoothedPivot - _actualSocketPosition;
        if (lookDirection.LengthSquared() > .000001f)
            _cameraObject.Transform.WorldRotation = LookRotation(Vector3.Normalize(lookDirection));
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
        writer.Add("DesiredSocketPosition", _desiredSocketPosition);
        writer.Add("ActualSocketPosition", _actualSocketPosition);
        writer.Add("RootPosition", root?.Transform.WorldPosition);
        writer.Add("RootRotation", root?.Transform.WorldRotation);
        writer.Add("Position", _cameraObject?.Transform.WorldPosition);
        writer.Add("Rotation", _cameraObject?.Transform.WorldRotation);
        writer.Add("Forward", _cameraObject?.Transform.Forward);
        writer.Add("FOV", camera?.FieldOfView);
        writer.Add("Collision.Enabled", EnableCameraCollision);
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
