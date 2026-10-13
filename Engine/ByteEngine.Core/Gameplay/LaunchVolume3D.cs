using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
namespace ByteEngine.Core.Gameplay;

/// <summary>Reusable launch trigger for characters and dynamic bodies.</summary>
public sealed class LaunchVolume3D : Component
{
    public Vector3 LaunchVelocity {get;set;}=new(0,12,0);
    public bool Additive {get;set;}
    public bool PreserveHorizontal {get;set;}=true;
    public float ReentryDelay {get;set;}=.3f;
    private readonly Dictionary<Guid,double> _last=new();
    protected override void OnTriggerEnter(PhysicsContact3D contact)
    {
        var target=contact.Other;var now=GameObject.Scene?.SimulationTime??0;
        if(_last.TryGetValue(target.Id,out var prior)&&now-prior<Math.Max(0,ReentryDelay))return;
        var velocity=LaunchVelocity;
        if(!float.IsFinite(velocity.LengthSquared()))return;
        if(target.GetComponent<CharacterController3D>() is {} motor)
        {
            if(Additive)motor.AddImpulse(velocity);
            else motor.SetVelocity(PreserveHorizontal?new(motor.Velocity.X+velocity.X,velocity.Y,motor.Velocity.Z+velocity.Z):velocity);
        }
        else if(target.GetComponent<Rigidbody3D>() is {BodyType:RigidbodyBodyType3D.Dynamic} body)body.Velocity=Additive?body.Velocity+velocity:velocity;
        else return;
        if(_last.Count>128)_last.Clear();_last[target.Id]=now;
    }
    protected override void OnStop()=>_last.Clear();
}
