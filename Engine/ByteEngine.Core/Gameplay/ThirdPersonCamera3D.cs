using System.Numerics;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class ThirdPersonCamera3D : Component
{
    private float _distance = 7f, _height = 4f, _lookAtHeight = 1f, _followSmoothing = 10f;
    public Guid TargetId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public float Distance { get => _distance; set => _distance = Safe(value); }
    public float Height { get => _height; set => _height = Finite(value); }
    public float LookAtHeight { get => _lookAtHeight; set => _lookAtHeight = Finite(value); }
    public float FollowSmoothing { get => _followSmoothing; set => _followSmoothing = Safe(value); }
    public override int UpdateOrder => 1000;

    protected override void OnStart() => Follow(true);
    protected override void OnUpdate() => Follow(false);

    private void Follow(bool immediate)
    {
        GameObject? camera = AttachedGameObject;
        RuntimeScene? scene = camera?.Scene;
        if (camera == null || scene == null) return;
        GameObject? target = TargetId != Guid.Empty ? scene.FindGameObject(TargetId) : null;
        if (target == null && !string.IsNullOrWhiteSpace(TargetName)) target = scene.FindGameObject(TargetName);
        if (target == null || !target.ActiveInHierarchy) return;
        Vector3 desired = target.Transform.WorldPosition - HorizontalForward(target) * Distance + Vector3.UnitY * Height;
        float factor = immediate || FollowSmoothing <= 0f ? 1f : 1f - MathF.Exp(-FollowSmoothing * (float)Time.DeltaTime);
        camera.Transform.WorldPosition = Vector3.Lerp(camera.Transform.WorldPosition, desired, Math.Clamp(factor, 0f, 1f));
        Vector3 direction = target.Transform.WorldPosition + Vector3.UnitY * LookAtHeight - camera.Transform.WorldPosition;
        if (direction.LengthSquared() <= .0001f) return;
        direction = Vector3.Normalize(direction);
        camera.Transform.EulerAngles = new Vector3(
            MathF.Asin(Math.Clamp(direction.Y, -1f, 1f)) * 180f / MathF.PI,
            MathF.Atan2(-direction.X, -direction.Z) * 180f / MathF.PI,
            0f);
    }

    private static Vector3 HorizontalForward(GameObject target)
    {
        Vector3 forward = target.Transform.Forward; forward.Y = 0f;
        return forward.LengthSquared() > .0001f ? Vector3.Normalize(forward) : -Vector3.UnitZ;
    }
    private static float Safe(float value) => Math.Max(0f, Finite(value));
    private static float Finite(float value) => float.IsFinite(value) ? value : 0f;
}
