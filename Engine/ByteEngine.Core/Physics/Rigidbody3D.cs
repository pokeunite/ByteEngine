using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Physics;

/// <summary>
/// Scene-owned rigid body with linear dynamics and opt-in angular inertia.
/// Sleep and swept contact protection complement the iterative contact solver.
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
    private Vector3 _accumulatedTorque;

    /// <summary>Opt-in angular dynamics; old bodies retain their authored rotation.</summary>
    public bool ContinuousCollision { get; set; }
    public bool AllowSleep { get; set; } = true;
    public bool IsSleeping { get; private set; }
    private float _quietTime;
    private Vector3 _sleepPosition;
    public void WakeUp() { IsSleeping = false; _quietTime = 0; }
    internal void UpdateSleep(float delta, bool supported, bool quietContactIsland=false)
    {
        if (!AllowSleep || BodyType != RigidbodyBodyType3D.Dynamic) { WakeUp(); return; }
        if (IsSleeping) return;
        if ((supported || !UseGravity) && (quietContactIsland || Velocity.LengthSquared() < .0064f) && AngularVelocity.LengthSquared() < .0025f)
        {
            _quietTime += delta;
            if (_quietTime >= .75f) { IsSleeping = true; _sleepPosition = Transform.WorldPosition; Velocity = AngularVelocity = Vector3.Zero; }
        }
        else _quietTime = 0;
    }
    public bool SimulateRotation { get; set; }
    public Vector3 AngularVelocity { get; set; }
    /// <summary>Local principal moments of inertia in kg m². Tune for the compound body's geometry.</summary>
    public Vector3 InertiaTensor { get; set; } = Vector3.One;
    public float AngularDamping { get; set; } = .05f;

    public void AddTorque(Vector3 torque)
    {
        if (InverseMass > 0 && SimulateRotation && IsFinite(torque) && torque.LengthSquared() > 0) { WakeUp(); _accumulatedTorque += torque; }
    }

    public void AddTorqueImpulse(Vector3 impulse)
    {
        if (InverseMass > 0 && SimulateRotation && IsFinite(impulse))
        { if (impulse.LengthSquared() > .000001f) WakeUp(); AngularVelocity += ApplyInverseInertia(impulse); }
    }

    public void AddImpulseAtPosition(Vector3 impulse, Vector3 worldPosition)
    {
        if (!IsFinite(impulse) || !IsFinite(worldPosition)) return;
        AddImpulse(impulse);
        AddTorqueImpulse(Vector3.Cross(worldPosition - Transform.WorldPosition, impulse));
    }

    internal Vector3 VelocityAt(Vector3 worldPosition) => Velocity +
        (SimulateRotation ? Vector3.Cross(AngularVelocity, worldPosition - Transform.WorldPosition) : Vector3.Zero);

    internal float AngularImpulseDenominator(Vector3 point, Vector3 direction)
    {
        if (!SimulateRotation || InverseMass <= 0) return 0;
        Vector3 radius = point - Transform.WorldPosition;
        return Vector3.Dot(direction, Vector3.Cross(ApplyInverseInertia(Vector3.Cross(radius, direction)), radius));
    }

    internal void ApplyContactImpulse(Vector3 impulse, Vector3 point)
    {
        ApplyVelocityImpulse(impulse);
        AddTorqueImpulse(Vector3.Cross(point - Transform.WorldPosition, -impulse));
    }

    internal Vector3 ApplyInverseInertia(Vector3 impulse)
    {
        var rotation = Transform.WorldRotation;
        var local = Vector3.Transform(impulse, Quaternion.Inverse(rotation));
        float Moment(float value) => float.IsFinite(value) ? Math.Max(.0001f, value) : 1f;
        return Vector3.Transform(local / new Vector3(Moment(InertiaTensor.X), Moment(InertiaTensor.Y), Moment(InertiaTensor.Z)), rotation);
    }

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

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

        if (IsFinite(force) && force.LengthSquared() > 0) { WakeUp(); _accumulatedForce += force; }
    }

    public void AddImpulse(
        Vector3 impulse)
    {
        if (InverseMass <=
            0.0f)
        {
            return;
        }

        if (!IsFinite(impulse)) return;
        if (impulse.LengthSquared() > .000001f) WakeUp();
        Velocity +=
            FilterFrozen(
                impulse *
                InverseMass);
    }

    public void ClearForces()
    {
        _accumulatedTorque = Vector3.Zero;
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

        if (IsSleeping && (Transform.WorldPosition != _sleepPosition || Velocity.LengthSquared() > .000001f || AngularVelocity.LengthSquared() > .000001f)) WakeUp();
        if (IsSleeping) return;

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

        if (SimulateRotation)
        {
            if (!IsFinite(AngularVelocity)) AngularVelocity = Vector3.Zero;
            AngularVelocity += ApplyInverseInertia(_accumulatedTorque) * deltaTime;
            float damping = float.IsFinite(AngularDamping) ? Math.Clamp(AngularDamping, 0, 100) : .05f;
            AngularVelocity /= 1 + damping * deltaTime;
            float speed = AngularVelocity.Length();
            if (float.IsFinite(speed) && speed > .000001f)
                Transform.WorldRotation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(AngularVelocity / speed, speed * deltaTime) * Transform.WorldRotation);
        }
    }

    internal void ApplyVelocityImpulse(
        Vector3 impulse)
    {
        if (InverseMass <=
            0.0f)
        {
            return;
        }

        if (IsSleeping && impulse.LengthSquared() * InverseMass * InverseMass < .04f) return;
        if (IsSleeping) WakeUp();
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

        Transform.WorldPosition += FilterFrozen(correction);
        if (IsSleeping) _sleepPosition = Transform.WorldPosition;
    }

    internal void EndPhysicsFrame()
    {
        _accumulatedTorque = Vector3.Zero;
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
