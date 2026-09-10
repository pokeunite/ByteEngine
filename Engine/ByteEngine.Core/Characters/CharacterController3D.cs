using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Characters;

public sealed class CharacterController3D : Component
{
    private Vector3 _moveInput; private bool _jumpQueued; private bool _wasGrounded;
    public bool IsGrounded { get; private set; }
    public bool IsFalling => !IsGrounded && VerticalVelocity < 0f;
    public bool IsMoving => new Vector2(Velocity.X,Velocity.Z).LengthSquared()>.0001f;
    public bool JustLanded { get; private set; }
    public Vector3 Velocity { get; private set; }
    public float Speed=>new Vector2(Velocity.X,Velocity.Z).Length();
    public float VerticalVelocity=>Velocity.Y;
    public Vector3 GroundNormal { get; private set; }=Vector3.UnitY;
    public GameObject? GroundObject { get; private set; }
    public float MoveSpeed{get;set;}=5f; public float Acceleration{get;set;}=30f; public float Deceleration{get;set;}=35f;
    public float AirControl{get;set;}=.35f; public float JumpForce{get;set;}=7f; public float Gravity{get;set;}=20f;
    public float GroundDistance{get;set;}=.15f; public float MaxSlope{get;set;}=50f; public float StepHeight{get;set;}=.3f;
    public float CoyoteTime{get;set;}=.1f; public float JumpBuffer{get;set;}=.1f; public bool SnapToGround{get;set;}=true;
    public void Move(Vector3 direction)=>_moveInput+=direction;
    public void MoveForward(float amount=1f)=>Move(Transform.Forward*amount);
    public void MoveRight(float amount=1f)=>Move(Transform.Right*amount);
    public void Jump()=>_jumpQueued=true;
    public void SetVelocity(Vector3 velocity)=>Velocity=velocity;
    public void AddImpulse(Vector3 impulse)=>Velocity+=impulse;
    protected override void OnUpdate()
    {
        float dt=(float)Time.DeltaTime; JustLanded=false; DetectGround();
        Vector3 target=_moveInput;target.Y=0;if(target.LengthSquared()>1)target=Vector3.Normalize(target);target*=MoveSpeed;
        float control=IsGrounded?1f:AirControl; float rate=target.LengthSquared()>.001f?Acceleration:Deceleration;
        Velocity=new Vector3(Approach(Velocity.X,target.X,rate*control*dt),Velocity.Y,Approach(Velocity.Z,target.Z,rate*control*dt));
        if(_jumpQueued&&IsGrounded){Velocity=new Vector3(Velocity.X,JumpForce,Velocity.Z);IsGrounded=false;GroundObject=null;}
        if(!IsGrounded)Velocity+=Vector3.UnitY*(-Gravity*dt);
        Transform.WorldPosition+=Velocity*dt; _moveInput=Vector3.Zero;_jumpQueued=false; _wasGrounded=IsGrounded;
    }
    private void DetectGround()
    {
        GroundObject=null;float best=float.NegativeInfinity;
        if(GameObject.Scene!=null)foreach(GameObject item in GameObject.Scene.GameObjects){GroundSurface? ground=item.GetComponent<GroundSurface>();if(ground?.Walkable!=true)continue;float y=item.Transform.WorldPosition.Y;if(y<=Transform.WorldPosition.Y+GroundDistance&&y>best){best=y;GroundObject=item;}}
        IsGrounded=GroundObject!=null&&Transform.WorldPosition.Y<=best+GroundDistance&&Velocity.Y<=0;
        if(IsGrounded&&SnapToGround){Transform.WorldPosition=new Vector3(Transform.WorldPosition.X,best,Transform.WorldPosition.Z);Velocity=new Vector3(Velocity.X,0,Velocity.Z);}
        JustLanded=IsGrounded&&!_wasGrounded;
    }
    private static float Approach(float current,float target,float delta)=>current<target?Math.Min(current+delta,target):Math.Max(current-delta,target);
}
