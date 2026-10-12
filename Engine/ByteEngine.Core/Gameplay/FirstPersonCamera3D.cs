using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

/// <summary>Direct first-person view. The saved camera attachment is the eye anchor;
/// controller rotation changes its view, never spring-arm distance or collision.</summary>
public sealed class FirstPersonCamera3D : Component
{
    private Guid _boundId;
    private GameObject? _camera;
    private Vector3 _anchor;
    public Guid CameraObjectId { get; set; }
    public bool UseControlRotation { get; set; } = true;
    public float MouseSensitivityX { get; set; } = .12f;
    public float MouseSensitivityY { get; set; } = .10f;
    public bool InvertHorizontalLook { get; set; }
    public bool InvertVerticalLook { get; set; }
    public float MinPitch { get; set; } = -89f;
    public float MaxPitch { get; set; } = 89f;
    public override int UpdateOrder => -200;
    protected override void OnStart() { _camera = null; UpdateView(); }
    protected override void OnLateUpdate() => UpdateView();
    public void SnapToView() => UpdateView();
    public void RebindAuthoredEye() { _camera = null; UpdateView(); }
    public float MinimumPitch => Math.Min(ValidPitch(MinPitch, -89), ValidPitch(MaxPitch, 89));
    public float MaximumPitch => Math.Max(ValidPitch(MinPitch, -89), ValidPitch(MaxPitch, 89));
    private static float ValidPitch(float value, float fallback) => Math.Clamp(float.IsFinite(value) ? value : fallback, -89, 89);
    public Vector2 CalculateLookDelta(Vector2 input) => new(
        input.X * Positive(MouseSensitivityX) * (InvertHorizontalLook ? -1 : 1),
        -input.Y * Positive(MouseSensitivityY) * (InvertVerticalLook ? -1 : 1));
    private static float Positive(float value) => float.IsFinite(value) ? Math.Max(0, value) : 0;
    private void UpdateView()
    {
        var root = AttachedGameObject;
        if (root?.Scene == null) return;
        var target = root.Scene.FindGameObject(CameraObjectId);
        if (target?.GetComponent<Camera3D>() == null || !target.IsDescendantOf(root)) return;
        if (_camera != target || _boundId != CameraObjectId)
        {
            // Exact authored anchor, including zero. Rebind on restart or camera replacement.
            if (!Matrix4x4.Invert(root.Transform.WorldMatrix, out var inverse)) return;
            _anchor = Vector3.Transform(target.Transform.WorldPosition, inverse);
            _camera = target; _boundId = CameraObjectId;
        }
        target.Transform.WorldPosition = Vector3.Transform(_anchor, root.Transform.WorldMatrix);
        var controller = root.GetComponent<PlayerController3D>();
        if (UseControlRotation && controller?.Enabled == true)
        {
            float pitch = Math.Clamp(controller.ControlPitch, MinimumPitch, MaximumPitch);
            Vector3 forward = CameraBoom3D.CalculateViewForward(controller.ControlYaw, pitch);
            target.Transform.WorldRotation = Quaternion.CreateFromYawPitchRoll(
                MathF.Atan2(-forward.X, -forward.Z), MathF.Asin(Math.Clamp(forward.Y, -1, 1)), 0);
        }
    }
}
