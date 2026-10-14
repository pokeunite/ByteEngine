using System.Numerics;

namespace ByteEngine.Core.Scene;

public sealed class Transform
{
    private readonly GameObject _owner;
    private Vector3 _localScale = Vector3.One;
    private Quaternion _localRotation = Quaternion.Identity;

    private Vector3 _localPosition;
    private long _localVersion, _cachedLocalVersion = -1, _worldVersion;
    private Transform? _cachedParent;
    private long _cachedParentVersion = -1;
    private Vector3 _worldPosition, _worldScale;
    private Quaternion _worldRotation;
    private Matrix4x4 _worldMatrix, _localMatrix;
    public Vector3 LocalPosition
    {
        get => _localPosition;
        set { if (_localPosition != value) { _localPosition = value; _localVersion++; } }
    }
    public Quaternion LocalRotation
    {
        get => _localRotation;
        set { var normalized = Quaternion.Normalize(value.LengthSquared() < .0001f ? Quaternion.Identity : value); if (_localRotation != normalized) { _localRotation = normalized; _localVersion++; } }
    }
    public Vector3 LocalScale
    {
        get => _localScale;
        set { var scale = new Vector3(NonZero(value.X), NonZero(value.Y), NonZero(value.Z)); if (_localScale != scale) { _localScale = scale; _localVersion++; } }
    }

    public Vector3 WorldPosition
    {
        get
        {
            EnsureWorld();
            return _worldPosition;
        }
        set
        {
            Transform? parent = _owner.Parent?.Transform;
            LocalPosition = parent == null ? value : Vector3.Transform(value - parent.WorldPosition, Quaternion.Inverse(parent.WorldRotation)) / parent.WorldScale;
        }
    }

    public Quaternion WorldRotation
    {
        // Numerics quaternions compose parent * local; row-vector matrices compose
        // localMatrix * parentMatrix. Reversing these creates roll in a yaw/pitch hierarchy.
        get { EnsureWorld(); return _worldRotation; }
        set => LocalRotation = _owner.Parent == null ? value : Quaternion.Normalize(Quaternion.Inverse(_owner.Parent.Transform.WorldRotation) * value);
    }

    public Vector3 WorldScale
    {
        get { EnsureWorld(); return _worldScale; }
        set => LocalScale = _owner.Parent == null ? value : value / _owner.Parent.Transform.WorldScale;
    }

    public Vector2 Position
    {
        get => new(WorldPosition.X, WorldPosition.Y);
        set => WorldPosition = new Vector3(value, WorldPosition.Z);
    }

    public float Rotation
    {
        get => EulerAngles.Z;
        set { Vector3 euler = EulerAngles; euler.Z = value; EulerAngles = euler; }
    }

    public Vector3 EulerAngles
    {
        get => ToEulerDegrees(LocalRotation);
        set => LocalRotation = Quaternion.CreateFromYawPitchRoll(Radians(value.Y), Radians(value.X), Radians(value.Z));
    }

    public Vector3 Forward => Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, WorldRotation));
    public Vector3 Right => Vector3.Normalize(Vector3.Transform(Vector3.UnitX, WorldRotation));
    public Vector3 Up => Vector3.Normalize(Vector3.Transform(Vector3.UnitY, WorldRotation));
    public Matrix4x4 LocalMatrix { get { EnsureWorld(); return _localMatrix; } }
    public Matrix4x4 WorldMatrix { get { EnsureWorld(); return _worldMatrix; } }

    private void EnsureWorld()
    {
        var parent = _owner.Parent?.Transform;
        parent?.EnsureWorld();
        long parentVersion = parent?._worldVersion ?? 0;
        if (_cachedLocalVersion == _localVersion && ReferenceEquals(parent, _cachedParent) && _cachedParentVersion == parentVersion) return;
        _localMatrix = Matrix4x4.CreateScale(_localScale) * Matrix4x4.CreateFromQuaternion(_localRotation) * Matrix4x4.CreateTranslation(_localPosition);
        _worldMatrix = parent == null ? _localMatrix : _localMatrix * parent._worldMatrix;
        _worldPosition = parent == null ? _localPosition : parent._worldPosition + Vector3.Transform(_localPosition * parent._worldScale, parent._worldRotation);
        _worldRotation = parent == null ? _localRotation : Quaternion.Normalize(parent._worldRotation * _localRotation);
        _worldScale = parent == null ? _localScale : _localScale * parent._worldScale;
        _cachedLocalVersion = _localVersion; _cachedParent = parent; _cachedParentVersion = parentVersion;
        _worldVersion++;
    }

    private Vector3 _previousPosition;
    private Quaternion _previousRotation;
    private Vector3 _previousScale;
    private bool _hasSimulationPose;

    // Presentation samples never overwrite authoritative transforms or collider poses.
    internal void CaptureSimulationPose()
    {
        _previousPosition = LocalPosition;
        _previousRotation = LocalRotation;
        _previousScale = LocalScale;
        _hasSimulationPose = true;
    }

    public Matrix4x4 RenderMatrix
    {
        get
        {
            var scene = _owner.Scene;
            if (!_hasSimulationPose || scene is not { FixedSimulation: true, IsLoaded: true })
                return _owner.Parent == null ? WorldMatrix : LocalMatrix * _owner.Parent.Transform.RenderMatrix;
            float alpha = scene is { FixedSimulation: true, IsLoaded: true } && _hasSimulationPose
                ? (float)scene.SimulationClock.InterpolationAlpha : 1;
            var local = Matrix4x4.CreateScale(Vector3.Lerp(_previousScale, LocalScale, alpha)) *
                Matrix4x4.CreateFromQuaternion(_hasSimulationPose ? Quaternion.Slerp(_previousRotation, LocalRotation, alpha) : LocalRotation) *
                Matrix4x4.CreateTranslation(Vector3.Lerp(_previousPosition, LocalPosition, alpha));
            // An uninitialised pose must use authored values, including scale.
            if (!_hasSimulationPose || scene is not { FixedSimulation: true, IsLoaded: true }) local = LocalMatrix;
            return _owner.Parent == null ? local : local * _owner.Parent.Transform.RenderMatrix;
        }
    }

    /// <summary>Call after teleporting/resetting a simulated object to avoid interpolating across the teleport.</summary>
    public void ResetInterpolation() => CaptureSimulationPose();
    internal void ClearSimulationPose()=>_hasSimulationPose=false;

    internal Transform(GameObject owner) => _owner = owner;

    private static float NonZero(float value) => MathF.Abs(value) < .0001f ? MathF.CopySign(.0001f, value == 0f ? 1f : value) : value;
    private static float Radians(float value) => value * MathF.PI / 180f;
    private static Vector3 ToEulerDegrees(Quaternion q)
    {
        float x = MathF.Asin(Math.Clamp(2f * (q.W * q.X - q.Y * q.Z), -1f, 1f));
        float y = MathF.Atan2(2f * (q.W * q.Y + q.Z * q.X), 1f - 2f * (q.X * q.X + q.Y * q.Y));
        float z = MathF.Atan2(2f * (q.W * q.Z + q.X * q.Y), 1f - 2f * (q.X * q.X + q.Z * q.Z));
        return new Vector3(x, y, z) * (180f / MathF.PI);
    }
}
