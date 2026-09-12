using System.Numerics;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class ThirdPersonCamera3D : Component
{
    private float _distance = 6f, _height = 1.2f, _lookAtHeight = .8f, _followSmoothing = 10f;
    private float _yaw, _pitch = 20f, _minPitch = -10f, _maxPitch = 55f, _mouseSensitivity = .15f;
    private float _shoulderOffset = .5f;

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
    public override int UpdateOrder => 1000;

    protected override void OnStart() => Follow(true);

    protected override void OnUpdate()
    {
        if (Input.IsGameViewFocused && Input.IsGameViewHovered)
        {
            Vector2 delta = Input.ConsumeGameViewMouseDelta();
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

        float yaw = Yaw * MathF.PI / 180f, pitch = Pitch * MathF.PI / 180f;
        Vector3 backward = new(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        Vector3 right = new(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));
        float horizontalDistance = MathF.Cos(pitch) * Distance;
        Vector3 lookTarget = target.Transform.WorldPosition + Vector3.UnitY * LookAtHeight;
        Vector3 desired = target.Transform.WorldPosition + backward * horizontalDistance + right * ShoulderOffset +
            Vector3.UnitY * (Height + MathF.Sin(pitch) * Distance);
        float factor = immediate || FollowSmoothing <= 0f ? 1f :
            1f - MathF.Exp(-FollowSmoothing * (float)Time.DeltaTime);
        camera.Transform.WorldPosition = Vector3.Lerp(camera.Transform.WorldPosition, desired, Math.Clamp(factor, 0f, 1f));
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
        if (scene == null) return null;
        GameObject? target = TargetId != Guid.Empty ? scene.FindGameObject(TargetId) : null;
        if (target == null && !string.IsNullOrWhiteSpace(TargetName)) target = scene.FindGameObject(TargetName);
        return target;
    }

    private static float Positive(float value) => Math.Max(0f, Finite(value));
    private static float Finite(float value) => float.IsFinite(value) ? value : 0f;
}
