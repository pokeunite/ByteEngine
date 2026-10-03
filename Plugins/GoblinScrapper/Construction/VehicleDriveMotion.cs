using ByteEngine.Core;
using ByteEngine.Core.Construction;
using System.Numerics;

namespace GoblinScrapper.Construction;

/// <summary>Planar arcade motion. Heading and momentum are separate, so low grip produces a real slide.</summary>
public sealed class VehicleDriveMotion
{
    public Vector3 Velocity { get; private set; }
    public float Yaw { get; private set; }
    public float ForwardSpeed => Vector3.Dot(Velocity, Forward);
    public float LateralSpeed => Vector3.Dot(Velocity, Right);
    public float DriftAngle => MathF.Atan2(Math.Abs(LateralSpeed), Math.Max(.01f, Math.Abs(ForwardSpeed))) * 180 / MathF.PI;
    public bool IsDrifting { get; private set; }
    public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, Quaternion.CreateFromAxisAngle(Vector3.UnitY, Yaw));
    private Vector3 Right => Vector3.Transform(Vector3.UnitX, Quaternion.CreateFromAxisAngle(Vector3.UnitY, Yaw));
    public void Reset(float yaw = 0) { Velocity = Vector3.Zero; Yaw = yaw; IsDrifting = false; }
    public void Stop() { Velocity = Vector3.Zero; IsDrifting = false; }
    public Vector3 Step(float throttle, float steering, bool brake, bool handbrake, float dt,
        float maximumSpeed, float roadGrip = 12, float driftGrip = .9f)
    {
        if (dt <= 0 || !float.IsFinite(dt)) return Vector3.Zero;
        dt = Math.Min(dt, .05f);
        throttle = float.IsFinite(throttle) ? Math.Clamp(throttle, -1, 1) : 0;
        steering = float.IsFinite(steering) ? Math.Clamp(steering, -1, 1) : 0;
        maximumSpeed = float.IsFinite(maximumSpeed) ? Math.Clamp(maximumSpeed, 1, 30) : 12;
        roadGrip = float.IsFinite(roadGrip) ? Math.Clamp(roadGrip, 2, 30) : 12;
        driftGrip = float.IsFinite(driftGrip) ? Math.Clamp(driftGrip, .25f, 2) : .9f;
        float speed = ForwardSpeed;
        bool sliding = handbrake && !brake && speed > 3;
        float target = brake ? 0 : throttle >= 0 ? throttle * maximumSpeed : throttle * maximumSpeed * .45f;
        float acceleration = brake ? 24 : throttle == 0 ? 3.5f : 7;
        speed += Math.Clamp(target - speed, -acceleration * dt, acceleration * dt);
        Vector3 momentum = Forward * speed + Right * LateralSpeed;
        Yaw -= steering * speed * dt * .16f * (sliding ? 1.65f : 1);
        float longitudinal = Vector3.Dot(momentum, Forward);
        float lateral = Vector3.Dot(momentum, Right) * MathF.Exp(-(brake ? 20 : sliding ? driftGrip : roadGrip) * dt);
        Velocity = Forward * longitudinal + Right * lateral;
        if (Velocity.Length() > maximumSpeed) Velocity = Vector3.Normalize(Velocity) * maximumSpeed;
        if (brake && Velocity.Length() < .02f) Velocity = Vector3.Zero;
        IsDrifting = sliding && DriftAngle > 5;
        return Velocity * dt;
    }
}
