using System.Numerics;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class CameraBoom3D : Component, IRuntimeDiagnosticSource
{
    private float _armLength = 5f;
    private float _pivotHeight = 1.5f;
    private float _yaw;
    private float _pitch = 12f;
    private float _minPitch = -10f;
    private float _maxPitch = 50f;
    private float _mouseSensitivityX = .12f;
    private float _mouseSensitivityY = .1f;
    private float _positionSmoothness = 14f;
    private float _rotationSmoothness = 18f;
    private float _shoulderOffset;
    private float _collisionRadius = .2f;
    private GameObject? _cameraObject;
    private Vector3 _desiredSocketPosition;
    private Vector3 _actualSocketPosition;
    private bool _collisionHit;
    private GameObject? _collisionObject;
    private float _actualLength;

    public Guid CameraObjectId { get; set; }
    public float ArmLength { get => _armLength; set => _armLength = Positive(value); }
    public float PivotHeight { get => _pivotHeight; set => _pivotHeight = Finite(value); }
    public float Yaw { get => _yaw; set => _yaw = Finite(value); }
    public float Pitch { get => _pitch; set => _pitch = Math.Clamp(Finite(value), MinPitch, MaxPitch); }
    public float MinPitch { get => _minPitch; set { _minPitch = Math.Clamp(Finite(value), -89f, 89f); if (_maxPitch < _minPitch) _maxPitch = _minPitch; _pitch = Math.Clamp(_pitch, _minPitch, _maxPitch); } }
    public float MaxPitch { get => _maxPitch; set { _maxPitch = Math.Clamp(Finite(value), -89f, 89f); if (_minPitch > _maxPitch) _minPitch = _maxPitch; _pitch = Math.Clamp(_pitch, _minPitch, _maxPitch); } }
    public float MouseSensitivityX { get => _mouseSensitivityX; set => _mouseSensitivityX = Positive(value); }
    public float MouseSensitivityY { get => _mouseSensitivityY; set => _mouseSensitivityY = Positive(value); }
    public float PositionSmoothness { get => _positionSmoothness; set => _positionSmoothness = Positive(value); }
    public float RotationSmoothness { get => _rotationSmoothness; set => _rotationSmoothness = Positive(value); }
    public float ShoulderOffset { get => _shoulderOffset; set => _shoulderOffset = Finite(value); }
    public bool EnableCameraCollision { get; set; }
    public float CollisionRadius { get => _collisionRadius; set => _collisionRadius = Positive(value); }
    public Vector3 DesiredSocketPosition => _desiredSocketPosition;
    public Vector3 ActualSocketPosition => _actualSocketPosition;
    public float ActualArmLength => _actualLength;
    public override int UpdateOrder => -200;

    protected override void OnStart() => UpdateRig(true);

    protected override void OnUpdate()
    {
        if (Input.IsGameInputCaptured)
        {
            Vector2 delta = Input.MouseDelta;
            Yaw += delta.X * MouseSensitivityX;
            Pitch -= delta.Y * MouseSensitivityY;
        }

        UpdateRig(false);
    }

    public void SnapToSocket() => UpdateRig(true);

    public static Vector3 CalculateOrbitVector(float yawDegrees, float pitchDegrees, float armLength)
    {
        float yaw = Radians(Finite(yawDegrees));
        float pitch = Radians(Math.Clamp(Finite(pitchDegrees), -89f, 89f));
        float length = Positive(armLength);
        return new Vector3(
            MathF.Sin(yaw) * MathF.Cos(pitch),
            MathF.Sin(pitch),
            MathF.Cos(yaw) * MathF.Cos(pitch)) * length;
    }

    private void UpdateRig(bool immediate)
    {
        GameObject? root = AttachedGameObject;
        if (root?.Scene is not { } scene) return;

        _cameraObject = ResolveCamera(root, scene);
        Camera3D? camera = _cameraObject?.GetComponent<Camera3D>();
        if (_cameraObject == null || camera == null) return;

        Vector3 pivot = root.Transform.WorldPosition + Vector3.UnitY * PivotHeight;
        float yawRadians = Radians(Yaw);
        Vector3 right = new(MathF.Cos(yawRadians), 0f, -MathF.Sin(yawRadians));
        Vector3 desiredOffset = CalculateOrbitVector(Yaw, Pitch, ArmLength) + right * ShoulderOffset;
        _desiredSocketPosition = pivot + desiredOffset;
        _actualSocketPosition = _desiredSocketPosition;
        _collisionHit = false;
        _collisionObject = null;
        _actualLength = desiredOffset.Length();

        float desiredLength = desiredOffset.Length();
        if (EnableCameraCollision && desiredLength > .0001f &&
            GameplayQuery3D.SphereCast(scene, pivot, desiredOffset, CollisionRadius, out RaycastHit3D hit, desiredLength, root))
        {
            _collisionHit = true;
            _collisionObject = hit.GameObject;
            _actualLength = Math.Max(0f, hit.Distance - CollisionRadius);
            _actualSocketPosition = pivot + Vector3.Normalize(desiredOffset) * _actualLength;
        }

        float positionFactor = SmoothingFactor(PositionSmoothness, immediate);
        _cameraObject.Transform.WorldPosition = Vector3.Lerp(
            _cameraObject.Transform.WorldPosition, _actualSocketPosition, positionFactor);

        Vector3 lookDirection = pivot - _cameraObject.Transform.WorldPosition;
        if (lookDirection.LengthSquared() > .000001f)
        {
            Quaternion desiredRotation = LookRotation(Vector3.Normalize(lookDirection));
            float rotationFactor = SmoothingFactor(RotationSmoothness, immediate);
            _cameraObject.Transform.WorldRotation = Quaternion.Slerp(
                _cameraObject.Transform.WorldRotation, desiredRotation, rotationFactor);
        }
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
        Camera3D? camera = _cameraObject?.GetComponent<Camera3D>();
        Camera3D? active = root?.Scene?.ActiveCamera;
        writer.Section($"CameraBoom3D: {root?.Name ?? "<detached>"}");
        writer.Add("ActiveCamera", Describe(active?.GameObject));
        writer.Add("CameraOwner", Describe(_cameraObject));
        writer.Add("MatchesActiveCamera", ReferenceEquals(camera, active));
        writer.Add("Captured", Input.IsGameInputCaptured);
        writer.Add("MouseDelta", Input.MouseDelta);
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
    private static float SmoothingFactor(float smoothness, bool immediate) => immediate || smoothness <= 0f
        ? 1f : Math.Clamp(1f - MathF.Exp(-smoothness * Math.Max((float)Time.DeltaTime, 0f)), 0f, 1f);
    private static Quaternion LookRotation(Vector3 forward)
    {
        float pitch = MathF.Asin(Math.Clamp(forward.Y, -1f, 1f));
        float yaw = MathF.Atan2(-forward.X, -forward.Z);
        return Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0f);
    }
    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
    private static float Positive(float value) => Math.Max(0f, Finite(value));
    private static float Finite(float value) => float.IsFinite(value) ? value : 0f;
}
