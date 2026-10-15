using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Physics;

/// <summary>Reports collision-entry speed for event-authored damage, sound or VFX rules.</summary>
public sealed class PhysicsImpactSensor3D : Component
{
    public float LastImpactSpeed {get;private set;}
    public int Sequence {get;private set;}
    Vector3 previousVelocity;
    protected override void OnFixedUpdate()=>previousVelocity=GameObject.GetComponent<Rigidbody3D>()?.Velocity??Vector3.Zero;
    protected override void OnCollisionEnter(PhysicsContact3D contact)
    {
        if(contact.IsTrigger)return;
        var other=contact.Other.GetComponent<Rigidbody3D>()?.Velocity??Vector3.Zero;
        LastImpactSpeed=Math.Abs(Vector3.Dot(previousVelocity-other,contact.Normal));
        Sequence++;
    }
}
