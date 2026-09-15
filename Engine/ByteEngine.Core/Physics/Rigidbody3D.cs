using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Physics;

/// <summary>
/// Linear 3D rigid body for ByteEngine's scene-owned PhysicsWorld3D.
///
/// v0.10-A intentionally focuses on stable translational dynamics. Angular
/// rigid-body response is a later extension; object rotation remains fully
/// available through Transform and kinematic gameplay.
/// </summary>
public sealed class Rigidbody3D
    : Component
{
    private float _mass =
        1.0f;

    private float _gravityScale =
        1.0f;

    private float _linearDamping =
        0.05f;

    private float _restitution =
        0.0f;

    private float _friction =
        0.6f;

    private Vector3 _accumulatedForce;

    public RigidbodyBodyType3D BodyType { get; set; } =
        RigidbodyBodyType3D.Dynamic;

    public float Mass
    {
        get => _mass;
        set =>
            _mass =
                Math.Max(
                    float.IsFinite(value)
                        ? value
                        : 1.0f,
                    0.0001f);
    }

    public bool UseGravity { get; set; } =
        true;

    public float GravityScale
    {
        get => _gravityScale;
        set =>
            _gravityScale =
                Math.Clamp(
                    float.IsFinite(value)
                        ? value
                        : 1.0f,
                    -16.0f,
                    16.0f);
    }

    public float LinearDamping
    {
        get => _linearDamping;
        set =>
            _linearDamping =
                Math.Clamp(
                    float.IsFinite(value)
                        ? value
                        : 0.0f,
                    0.0f,
                    100.0f);
    }

    public float Restitution
    {
        get => _restitution;
        set =>
            _restitution =
                Math.Clamp(
                    float.IsFinite(value)
                        ? value
                        : 0.0f,
                    0.0f,
                    1.0f);
    }

    public float Friction
    {
        get => _friction;
        set =>
            _friction =
                Math.Clamp(
                    float.IsFinite(value)
                        ? value
                        : 0.6f,
                    0.0f,
                    4.0f);
    }

    public bool FreezePositionX { get; set; }

    public bool FreezePositionY { get; set; }

    public bool FreezePositionZ { get; set; }

    /// <summary>
    /// Current linear world-space velocity in units/second.
    /// </summary>
    public Vector3 Velocity { get; set; }

    internal float InverseMass =>
        Enabled &&
        BodyType ==
            RigidbodyBodyType3D.Dynamic
            ? 1.0f /
              Math.Max(
                  Mass,
                  0.0001f)
            : 0.0f;

    public void AddForce(
        Vector3 force)
    {
        if (BodyType !=
            RigidbodyBodyType3D.Dynamic)
        {
            return;
        }

        _accumulatedForce +=
            force;
    }

    public void AddImpulse(
        Vector3 impulse)
    {
        if (InverseMass <=
            0.0f)
        {
            return;
        }

        Velocity +=
            FilterFrozen(
                impulse *
                InverseMass);
    }

    public void ClearForces()
    {
        _accumulatedForce =
            Vector3.Zero;
    }

    internal void Integrate(
        float deltaTime,
        Vector3 gravity)
    {
        if (!Enabled ||
            BodyType !=
                RigidbodyBodyType3D.Dynamic ||
            deltaTime <=
                0.0f)
        {
            return;
        }

        Vector3 acceleration =
            _accumulatedForce *
            InverseMass;

        if (UseGravity)
        {
            acceleration +=
                gravity *
                GravityScale;
        }

        Velocity +=
            FilterFrozen(
                acceleration *
                deltaTime);

        float dampingFactor =
            1.0f /
            (
                1.0f +
                LinearDamping *
                deltaTime
            );

        Velocity *=
            dampingFactor;

        Velocity =
            FilterFrozen(
                Velocity);

        Transform.WorldPosition +=
            Velocity *
            deltaTime;
    }

    internal void ApplyVelocityImpulse(
        Vector3 impulse)
    {
        if (InverseMass <=
            0.0f)
        {
            return;
        }

        Velocity -=
            FilterFrozen(
                impulse *
                InverseMass);
    }

    internal void ApplyPositionCorrection(
        Vector3 correction)
    {
        if (InverseMass <=
            0.0f)
        {
            return;
        }

        Transform.WorldPosition +=
            FilterFrozen(
                correction);
    }

    internal void EndPhysicsFrame()
    {
        _accumulatedForce =
            Vector3.Zero;
    }

    private Vector3 FilterFrozen(
        Vector3 value)
    {
        return
            new Vector3(
                FreezePositionX
                    ? 0.0f
                    : value.X,
                FreezePositionY
                    ? 0.0f
                    : value.Y,
                FreezePositionZ
                    ? 0.0f
                    : value.Z);
    }
}
