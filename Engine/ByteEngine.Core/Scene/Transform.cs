using System.Numerics;

namespace ByteEngine.Core.Scene;

public sealed class Transform
{
    private readonly GameObject _owner;
    private Vector3 _localScale = Vector3.One;
    private Quaternion _localRotation = Quaternion.Identity;

    public Vector3 LocalPosition { get; set; }
    public Quaternion LocalRotation
    {
        get => _localRotation;
        set => _localRotation = Quaternion.Normalize(value.LengthSquared() < .0001f ? Quaternion.Identity : value);
    }
    public Vector3 LocalScale
    {
        get => _localScale;
        set => _localScale = new Vector3(NonZero(value.X), NonZero(value.Y), NonZero(value.Z));
    }

    public Vector3 WorldPosition
    {
        get
        {
            Transform? parent = _owner.Parent?.Transform;
            return parent == null ? LocalPosition : parent.WorldPosition + Vector3.Transform(LocalPosition * parent.WorldScale, parent.WorldRotation);
        }
        set
        {
            Transform? parent = _owner.Parent?.Transform;
            LocalPosition = parent == null ? value : Vector3.Transform(value - parent.WorldPosition, Quaternion.Inverse(parent.WorldRotation)) / parent.WorldScale;
        }
    }

    public Quaternion WorldRotation
    {
        get => _owner.Parent == null ? LocalRotation : Quaternion.Normalize(LocalRotation * _owner.Parent.Transform.WorldRotation);
        set => LocalRotation = _owner.Parent == null ? value : Quaternion.Normalize(value * Quaternion.Inverse(_owner.Parent.Transform.WorldRotation));
    }

    public Vector3 WorldScale
    {
        get => _owner.Parent == null ? LocalScale : LocalScale * _owner.Parent.Transform.WorldScale;
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
    public Matrix4x4 LocalMatrix => Matrix4x4.CreateScale(LocalScale) * Matrix4x4.CreateFromQuaternion(LocalRotation) * Matrix4x4.CreateTranslation(LocalPosition);
    public Matrix4x4 WorldMatrix => _owner.Parent == null ? LocalMatrix : LocalMatrix * _owner.Parent.Transform.WorldMatrix;

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
