using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Physics;
/// <summary>Reusable articulated distance constraint. Anchors are local to each body; an empty target anchors to the world.</summary>
public sealed class DistanceJoint3D : Component,IPhysicsConstraint3D
{
    public Guid ConnectedObject { get; set; }
    public Vector3 LocalAnchor { get; set; }
    public Vector3 ConnectedAnchor { get; set; }
    public float Length { get; set; } = 1;
    public float BreakForce { get; set; } = float.MaxValue;
    public bool Broken { get; private set; }
    public float LastForce { get; private set; }
    public void Reconnect() { Broken = false; LastForce = 0; }
    public void Solve(float delta)
    {
        if (Broken || GameObject.Scene is not { } scene || GameObject.GetComponent<Rigidbody3D>() is not { Enabled: true } a) return;
        var target = ConnectedObject == Guid.Empty ? null : scene.FindGameObject(ConnectedObject);
        if (ConnectedObject != Guid.Empty && (target == null || ReferenceEquals(target, GameObject) || !target.ActiveInHierarchy)) return;
        var b = target?.GetComponent<Rigidbody3D>();
        Vector3 pa = Vector3.Transform(LocalAnchor, Transform.WorldMatrix);
        Vector3 pb = target == null ? ConnectedAnchor : Vector3.Transform(ConnectedAnchor, target.Transform.WorldMatrix);
        Vector3 direction = pb - pa;
        float distance = direction.Length();
        if (distance < .00001f || !float.IsFinite(distance)) return;
        direction /= distance;
        float denominator = a.InverseMass + (b?.InverseMass ?? 0) + a.AngularImpulseDenominator(pa, direction) + (b?.AngularImpulseDenominator(pb, direction) ?? 0);
        if (denominator < .000001f) return;
        float error = distance - Math.Max(0, float.IsFinite(Length) ? Length : 1);
        float velocity = Vector3.Dot((b?.VelocityAt(pb) ?? Vector3.Zero) - a.VelocityAt(pa), direction);
        float impulse = (velocity + .15f * error / Math.Max(delta, .00001f)) / denominator;
        LastForce = Math.Abs(impulse) / Math.Max(delta, .00001f);
        if (LastForce > BreakForce) { Broken = true; return; }
        a.AddImpulseAtPosition(direction * impulse, pa);
        b?.AddImpulseAtPosition(-direction * impulse, pb);
        float linear = a.InverseMass + (b?.InverseMass ?? 0);
        if (linear > 0)
        {
            Vector3 correction = direction * (error * .15f / linear);
            a.ApplyPositionCorrection(correction * a.InverseMass);
            b?.ApplyPositionCorrection(-correction * b.InverseMass);
        }
    }
}
