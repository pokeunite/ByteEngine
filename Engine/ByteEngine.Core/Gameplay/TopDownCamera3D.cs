using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

/// <summary>Standalone Duckov-style follow/look-ahead rig. Attach beside Camera3D on a separate object.
/// Based on EugeneVorobyev/godot-duckov-controller (MIT); see ThirdPartyNotices/DuckovController.txt.</summary>
public sealed class TopDownCamera3D : Component
{
    private bool _hasPose;
    private Guid _lastTarget;
    private Vector3 _followPosition, _lookAhead;
    [SceneObjectReference]
    public Guid TargetId { get; set; }
    public bool Orthographic { get; set; } = true;
    public float OrthographicWidth { get; set; } = 22f;
    public float Distance { get; set; } = 20f;
    public float Yaw { get; set; } = 45f;
    public float Pitch { get; set; } = 55f;
    public float TargetHeight { get; set; } = .8f;
    public float FollowSharpness { get; set; } = 12f;
    public bool CursorLookAhead { get; set; } = true;
    public float LookAheadDeadZone { get; set; } = .10f;
    public float LookAheadFullRadius { get; set; } = .30f;
    public float LookAheadDistance { get; set; } = 4f;
    public float LookAheadSharpness { get; set; } = 8f;
    public bool EnableWheelZoom { get; set; } = true;
    public float ZoomSpeed { get; set; } = 2f;
    public float MinimumZoom { get; set; } = 6f;
    public float MaximumZoom { get; set; } = 40f;
    public bool UseBounds { get; set; }
    public Vector2 BoundsMinimum { get; set; } = new(-100, -100);
    public Vector2 BoundsMaximum { get; set; } = new(100, 100);
    public Vector3 CurrentLookAhead => _lookAhead;
    public override int UpdateOrder => -400;

    protected override void OnUpdate()
    {
        // Keep the pointer visible before gameplay capture is granted on the first click.
        Input.NotifyGameViewPointerAim();
        if (EnableWheelZoom && InputActions.GameplayEnabled && Input.IsPointerOverGameView)
        {
            float delta = TopDownController3D.Safe(Input.Snapshot.MouseWheel) * Math.Max(0, TopDownController3D.Safe(ZoomSpeed));
            if (Orthographic) OrthographicWidth = ClampZoom(OrthographicWidth - delta);
            else Distance = ClampZoom(Distance - delta);
        }
    }
    protected override void OnLateUpdate() => Follow(false);
    protected override void OnStop() { _hasPose = false; _lastTarget = Guid.Empty; _lookAhead = Vector3.Zero; }
    public void SnapToTarget() => Follow(true);
    public void SetIsometricView()
    { Orthographic = true; Yaw = 45; Pitch = 35.26439f; _hasPose = false; }
    private float ClampZoom(float value)
    {
        float min = Math.Max(.5f, TopDownController3D.Safe(MinimumZoom));
        float max = Math.Max(min, TopDownController3D.Safe(MaximumZoom));
        return Math.Clamp(TopDownController3D.Safe(value), min, max);
    }

    private void Follow(bool snap)
    {
        var camera = AttachedGameObject?.GetComponent<Camera3D>();
        var target = AttachedGameObject?.Scene?.FindGameObject(TargetId);
        if (camera == null || target == null || !target.ActiveInHierarchy || target == AttachedGameObject) { _hasPose = false; return; }
        if (AttachedGameObject!.GetComponent<ThirdPersonCamera3D>()?.Enabled == true) return;
        float dt = (float)Time.DeltaTime;
        bool immediate = snap || !_hasPose || _lastTarget != target.Id;
        var pivot = target.Transform.WorldPosition + Vector3.UnitY * TopDownController3D.Safe(TargetHeight);
        var offset = CursorLookAhead && InputActions.GameplayEnabled && Input.IsPointerOverGameView
            ? CalculateLookAhead(Input.GameViewPointerNormalized, Input.GameViewSize, Yaw, LookAheadDeadZone, LookAheadFullRadius, LookAheadDistance) : Vector3.Zero;
        _followPosition = immediate ? pivot : Vector3.Lerp(_followPosition, pivot, TopDownController3D.SmoothFactor(FollowSharpness, dt));
        _lookAhead = immediate ? Vector3.Zero : Vector3.Lerp(_lookAhead, offset, TopDownController3D.SmoothFactor(LookAheadSharpness, dt));
        pivot = _followPosition + _lookAhead;
        if (UseBounds)
        {
            pivot.X = Math.Clamp(pivot.X, Math.Min(BoundsMinimum.X, BoundsMaximum.X), Math.Max(BoundsMinimum.X, BoundsMaximum.X));
            pivot.Z = Math.Clamp(pivot.Z, Math.Min(BoundsMinimum.Y, BoundsMaximum.Y), Math.Max(BoundsMinimum.Y, BoundsMaximum.Y));
        }
        var orbit = ThirdPersonCamera3D.CalculateOrbitVector(TopDownController3D.Safe(Yaw), Math.Clamp(TopDownController3D.Safe(Pitch), 5, 89), ClampZoom(Distance));
        camera.Transform.WorldPosition = pivot + orbit;
        camera.Transform.WorldRotation = Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateWorld(Vector3.Zero, -Vector3.Normalize(orbit), Vector3.UnitY));
        camera.Orthographic = Orthographic;
        camera.OrthographicWidth = ClampZoom(OrthographicWidth);
        _lastTarget = target.Id; _hasPose = true;
    }

    public static Vector3 CalculateLookAhead(Vector2 pointer, Vector2 viewport, float yaw, float near, float far, float distance)
    {
        if (viewport.X <= 0 || viewport.Y <= 0) return Vector3.Zero;
        Vector2 offset = (pointer - new Vector2(.5f)) * new Vector2(1, viewport.Y / viewport.X);
        float radius = offset.Length();
        near = Math.Max(0, TopDownController3D.Safe(near));
        far = Math.Max(near + .001f, TopDownController3D.Safe(far));
        if (!float.IsFinite(radius) || radius <= near) return Vector3.Zero;
        float strength = Math.Clamp((radius - near) / (far - near), 0, 1) * Math.Max(0, TopDownController3D.Safe(distance));
        float radians = TopDownController3D.Safe(yaw) * MathF.PI / 180;
        var right = new Vector3(MathF.Cos(radians), 0, -MathF.Sin(radians));
        var forward = new Vector3(-MathF.Sin(radians), 0, -MathF.Cos(radians));
        return (right * offset.X - forward * offset.Y) * (strength / radius);
    }
}
