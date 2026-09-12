using System.Numerics;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class ThirdPersonCamera3D : Component, IRuntimeDiagnosticSource
{
    private float _distance = 6f, _height = 1.2f, _lookAtHeight = .8f, _followSmoothing = 10f;
    private float _yaw, _pitch = 20f, _minPitch = -10f, _maxPitch = 55f, _mouseSensitivity = .15f;
    private float _shoulderOffset = .5f;
    private GameObject? _resolvedTarget;
    private string _targetResolution = "Not evaluated";
    private Vector3 _calculatedOrbitVector;
    private Vector3 _desiredCameraPosition;
    private Vector3 _pivotPosition;

    public Guid TargetId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public float Distance { get => _distance; set => _distance = Positive(value); }
    public float Height { get => _height; set => _height = Finite(value); }
    public float LookAtHeight { get => _lookAtHeight; set => _lookAtHeight = Finite(value); }
    public float FollowSmoothing { get => _followSmoothing; set => _followSmoothing = Positive(value); }
    public float Yaw { get => _yaw; set => _yaw = Finite(value); }
    public float Pitch { get => _pitch; set => _pitch = Math.Clamp(Finite(value), MinPitch, MaxPitch); }
    public float MinPitch { get => _minPitch; set { _minPitch = Finite(value); if (_maxPitch < _minPitch) _maxPitch = _minPitch; _pitch = Math.Clamp(_pitch, _minPitch, _maxPitch); } }
    public float MaxPitch { get => _maxPitch; set { _maxPitch = Finite(value); if (_minPitch > _maxPitch) _minPitch = _maxPitch; _pitch = Math.Clamp(_pitch, _minPitch, _maxPitch); } }
    public float MouseSensitivity { get => _mouseSensitivity; set => _mouseSensitivity = Positive(value); }
    public float ShoulderOffset { get => _shoulderOffset; set => _shoulderOffset = Finite(value); }
    public override int UpdateOrder => -200;

    protected override void OnStart() => Follow(true);

    protected override void OnUpdate()
    {
        if (Input.IsGameInputCaptured)
        {
            Vector2 delta = Input.MouseDelta;
            Yaw += delta.X * MouseSensitivity;
            Pitch -= delta.Y * MouseSensitivity;
        }
        Follow(false);
    }

    private void Follow(bool immediate)
    {
        GameObject? camera = AttachedGameObject;
        RuntimeScene? scene = camera?.Scene;
        GameObject? target = ResolveTarget(scene);
        if (camera == null || target == null || !target.ActiveInHierarchy) return;

        float yaw = Yaw * MathF.PI / 180f;
        Vector3 backward = new(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        Vector3 right = new(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));
        Vector3 aimForward = -backward;
        _pivotPosition = target.Transform.WorldPosition + Vector3.UnitY * LookAtHeight;
        Vector3 lookTarget = _pivotPosition + aimForward * 10f;
        _calculatedOrbitVector = CalculateOrbitVector(Yaw, Pitch, Distance);
        _desiredCameraPosition = target.Transform.WorldPosition + _calculatedOrbitVector +
            right * ShoulderOffset + Vector3.UnitY * Height;
        float factor = immediate || FollowSmoothing <= 0f ? 1f :
            1f - MathF.Exp(-FollowSmoothing * (float)Time.DeltaTime);
        camera.Transform.WorldPosition = Vector3.Lerp(camera.Transform.WorldPosition, _desiredCameraPosition, Math.Clamp(factor, 0f, 1f));
        Vector3 direction = lookTarget - camera.Transform.WorldPosition;
        if (direction.LengthSquared() <= .0001f) return;
        direction = Vector3.Normalize(direction);
        camera.Transform.EulerAngles = new Vector3(
            MathF.Asin(Math.Clamp(direction.Y, -1f, 1f)) * 180f / MathF.PI,
            MathF.Atan2(-direction.X, -direction.Z) * 180f / MathF.PI,
            0f);
    }

    private GameObject? ResolveTarget(RuntimeScene? scene)
    {
        if (scene == null) { _resolvedTarget = null; _targetResolution = "Failed: no scene"; return null; }
        GameObject? target = TargetId != Guid.Empty ? scene.FindGameObject(TargetId) : null;
        if (target != null) _targetResolution = "TargetId";
        if (target == null && !string.IsNullOrWhiteSpace(TargetName))
        {
            target = scene.FindGameObject(TargetName);
            if (target != null) _targetResolution = "TargetName";
        }
        _resolvedTarget = target;
        if (target == null) _targetResolution = "Failed: target not found";
        return _resolvedTarget;
    }

    public static Vector3 CalculateOrbitVector(float yawDegrees, float pitchDegrees, float distance)
    {
        float yaw = yawDegrees * MathF.PI / 180f;
        float pitch = pitchDegrees * MathF.PI / 180f;
        float safeDistance = Math.Max(0f, float.IsFinite(distance) ? distance : 0f);
        return new Vector3(
            MathF.Sin(yaw) * MathF.Cos(pitch),
            MathF.Sin(pitch),
            MathF.Cos(yaw) * MathF.Cos(pitch)) * safeDistance;
    }

    public void WriteDiagnostics(RuntimeDiagnosticWriter writer)
    {
        GameObject? camera = AttachedGameObject;
        RuntimeScene? scene = camera?.Scene;
        Camera3D? rendered = scene?.ActiveCamera;
        Camera3D? attached = camera?.GetComponent<Camera3D>();
        writer.Section($"ThirdPersonCamera3D: {camera?.Name ?? "<detached>"}");
        writer.Add("GameInputCaptured", Input.IsGameInputCaptured);
        writer.Add("RawMouseDelta", Input.MouseDelta);
        writer.Add("TargetRequest", $"Id={TargetId}; Name={TargetName}");
        writer.Add("TargetResolution", _targetResolution);
        writer.Add("ResolvedTarget", _resolvedTarget == null ? "<none>" : $"{_resolvedTarget.Name} ({_resolvedTarget.Id})");
        writer.Add("TargetPosition", _resolvedTarget?.Transform.WorldPosition);
        writer.Add("TargetEulerAngles", _resolvedTarget?.Transform.EulerAngles);
        writer.Add("TargetScale", _resolvedTarget?.Transform.WorldScale);
        writer.Add("Yaw", Yaw);
        writer.Add("Pitch", Pitch);
        writer.Add("PitchLimits", $"{MinPitch:0.###} .. {MaxPitch:0.###}");
        writer.Add("Distance", Distance);
        writer.Add("Height", Height);
        writer.Add("LookAtHeight", LookAtHeight);
        writer.Add("PivotPosition", _pivotPosition);
        writer.Add("CalculatedOrbitVector", _calculatedOrbitVector);
        writer.Add("DesiredCameraPosition", _desiredCameraPosition);
        writer.Add("ActualCameraPosition", camera?.Transform.WorldPosition);
        writer.Add("ActualCameraForward", attached?.Transform.Forward);
        writer.Add("CameraEulerAngles", camera?.Transform.EulerAngles);
        writer.Add("RenderedCamera3D", rendered == null ? "<none>" :
            $"{rendered.GameObject.Name} ({rendered.GameObject.Id}){(ReferenceEquals(rendered, attached) ? " [this camera]" : " [DIFFERENT CAMERA]")}");
    }

    private static float Positive(float value) => Math.Max(0f, Finite(value));
    private static float Finite(float value) => float.IsFinite(value) ? value : 0f;
}
