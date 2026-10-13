using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
namespace Karlson.Plugin;

public sealed class ParkourPlayer3D : Component
{
    public float GroundSpeed {get;set;}=10;
    public float SprintSpeed {get;set;}=14;
    public float GroundAcceleration {get;set;}=65;
    public float AirAcceleration {get;set;}=18;
    public float JumpSpeed {get;set;}=8;
    public float Gravity {get;set;}=23;
    public float MouseSensitivity {get;set;}=.13f;
    public float GrappleRange {get;set;}=55;
    public float GrappleSpring {get;set;}=17;
    public float GrappleMaximumAcceleration {get;set;}=110;
    public float GrappleReelSpeed {get;set;}=8;
    public float WallRunSpeed {get;set;}=12;
    public bool EnableGrapple {get;set;}=true;
    public bool EnableWallRun {get;set;}=true;
    public float ShakeMultiplier {get;set;}=1;
    public Guid CameraObjectId {get;set;}
    public string EquippedName => _weapon?.DisplayName??"EMPTY HANDS";
    public int Ammo => _weapon?.Ammo??0;
    public bool Sliding {get;private set;}
    public bool WallRunning {get;private set;}
    public bool Grappling {get;private set;}
    public Vector3 GrapplePoint {get;private set;}
    public float Speed => _motor?.Speed??0;
    public bool IsGrounded=>_motor?.IsGrounded==true;
    public CharacterController3D? Motor=>_motor;
    public Vector3 EyePosition=>_eye?.Transform.WorldPosition??Transform.WorldPosition+Vector3.UnitY*1.65f;
    public Vector3 AimDirection=>_eye?.Transform.Forward??Transform.Forward;
    public KarlsonWeapon3D? Equipped=>_weapon;
    public Vector3 LastGroundedPosition {get;private set;}
    public string LastGroundName {get;private set;}="none";
    public override int UpdateOrder=>-400;
    CharacterController3D? _motor;CapsuleCollider3D? _capsule;GameObject? _eye;Camera3D? _camera;CableRenderer3D? _rope;
    KarlsonDirector3D? _director;KarlsonWeapon3D? _weapon;Rigidbody3D? _grab;
    Vector3 _wish,_wallNormal;float _yaw,_pitch,_eyeHeight=1.65f,_roll,_jumpBuffer,_ropeLength,_recoil,_hitShake,_grabDamping,_grabAngular;
    bool _crouch,_sprint,_grappleDown;float _wallCooldown;
    public bool Playing => _director?.Playing==true;
    public void SetLookRotation(float yawDegrees,float pitchDegrees){_yaw=yawDegrees;_pitch=Math.Clamp(pitchDegrees,-85,85);Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,_yaw*MathF.PI/180);}

    protected override void OnStart()
    {
        _motor=GameObject.GetComponent<CharacterController3D>();_capsule=GameObject.GetComponent<CapsuleCollider3D>();
        _eye=GameObject.Scene?.FindGameObject(CameraObjectId)??GameObject.Children.FirstOrDefault(o=>o.GetComponent<Camera3D>()!=null);
        _camera=_eye?.GetComponent<Camera3D>();_director=GameObject.Scene?.FindComponent<KarlsonDirector3D>();
        _rope=GameObject.GetComponent<CableRenderer3D>();if(_rope!=null)_rope.Enabled=false;
        if(_motor!=null){_motor.Acceleration=0;_motor.Deceleration=0;_motor.AirControl=1;_motor.JumpForce=JumpSpeed;_motor.Gravity=Gravity;_motor.GroundDistance=Math.Max(.25f,_motor.GroundDistance);}
        _yaw=0;_pitch=0;
    }
    protected override void OnUpdate()
    {
        if(!Playing){_wish=Vector3.Zero;ReleaseGrapple();ReleaseGrab();return;}
        if(Input.IsGameInputCaptured)
        {
            _yaw-=Input.MouseDelta.X*MouseSensitivity;_pitch=Math.Clamp(_pitch-Input.MouseDelta.Y*MouseSensitivity,-85,85);
            Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,_yaw*MathF.PI/180);
        }
        float x=(Input.IsKeyDown(Key.D)?1:0)-(Input.IsKeyDown(Key.A)?1:0),z=(Input.IsKeyDown(Key.W)?1:0)-(Input.IsKeyDown(Key.S)?1:0);
        _wish=Transform.Right*x+Transform.Forward*z;if(_wish.LengthSquared()>1)_wish=Vector3.Normalize(_wish);
        _crouch=Input.IsKeyDown(Key.LeftControl)||Input.IsKeyDown(Key.C);_sprint=Input.IsKeyDown(Key.LeftShift);
        if(Input.IsKeyPressed(Key.Space))_jumpBuffer=.14f;
        _grappleDown=EnableGrapple&&Input.IsMouseButtonDown(MouseButton.Right);
        if(Input.IsMouseButtonPressed(MouseButton.Right)&&EnableGrapple)BeginGrapple();
        if(!_grappleDown)ReleaseGrapple();
        if(Input.IsKeyPressed(Key.E))Interact();
        if(Input.IsKeyPressed(Key.Q))ThrowWeapon();
        if(_weapon!=null&&Input.IsMouseButtonDown(MouseButton.Left)&&Input.IsGameInputCaptured)_weapon.Shoot(this);
        if(Input.IsKeyPressed(Key.F))Melee();
        if(Input.IsKeyPressed(Key.R))GameObject.Scene?.RequestRestart();
    }
    protected override void OnFixedUpdate()
    {
        if(!Playing||_motor==null)return;
        float dt=(float)Time.FixedDeltaTime;_wallCooldown=Math.Max(0,_wallCooldown-dt);_jumpBuffer=Math.Max(0,_jumpBuffer-dt);
        var v=_motor.Velocity;var planar=new Vector3(v.X,0,v.Z);bool grounded=_motor.IsGrounded;
        if(grounded){LastGroundedPosition=Transform.WorldPosition;LastGroundName=_motor.GroundObject?.Name??"none";}
        bool slide=_crouch&&grounded&&planar.Length()>4;
        if(slide&&!Sliding)planar+=Transform.Forward*3;
        Sliding=slide;WallRunning=false;
        if(_crouch){if(_capsule!=null){_capsule.Height=1.0f;_capsule.Center=new(0,.5f,0);}}
        else if(!GameplayQuery3D.Raycast(GameObject.Scene!,Transform.WorldPosition+Vector3.UnitY*.9f,Vector3.UnitY,out _,1.05f,GameObject,includeTriggers:false))
        {if(_capsule!=null){_capsule.Height=1.8f;_capsule.Center=new(0,.9f,0);}}
        float max=_sprint?SprintSpeed:GroundSpeed;
        if(slide)planar*=MathF.Exp(-.65f*dt);
        else if(grounded)planar=Approach(planar,_wish*max,GroundAcceleration*dt);
        else if(_wish.LengthSquared()>0){var along=Vector3.Dot(planar,_wish);if(along<max)planar+=_wish*Math.Min(AirAcceleration*dt,max-along);}
        if(planar.Length()>32)planar=Vector3.Normalize(planar)*32;
        _motor.Gravity=Gravity;
        if(EnableWallRun&&!grounded&&!Grappling&&_wallCooldown<=0&&_wish.LengthSquared()>.1f)
        {
            var origin=Transform.WorldPosition+Vector3.UnitY;
            if(GameplayQuery3D.Raycast(GameObject.Scene!,origin,Transform.Right,out var wall,.8f,GameObject,includeTriggers:false)||GameplayQuery3D.Raycast(GameObject.Scene!,origin,-Transform.Right,out wall,.8f,GameObject,includeTriggers:false))
            {
                if(Math.Abs(wall.Normal.Y)<.25f){WallRunning=true;_wallNormal=wall.Normal;_motor.Gravity=3;v.Y=Math.Max(v.Y,-1.8f);var tangent=Transform.Forward-_wallNormal*Vector3.Dot(Transform.Forward,_wallNormal);if(tangent.LengthSquared()>.01f)planar=Vector3.Normalize(tangent)*Math.Max(WallRunSpeed,planar.Length());}
            }
        }
        if(_jumpBuffer>0&&WallRunning){planar+=_wallNormal*8;v.Y=JumpSpeed;_jumpBuffer=0;_wallCooldown=.25f;WallRunning=false;}
        else if(_jumpBuffer>0){_motor.Jump();_jumpBuffer=0;}
        _motor.SetVelocity(new(planar.X,v.Y,planar.Z));
        if(Grappling)
        {
            var offset=GrapplePoint-EyePosition;float distance=offset.Length();
            if(distance>.1f){var dir=offset/distance;float pull=Math.Clamp((distance-_ropeLength)*GrappleSpring-Vector3.Dot(_motor.Velocity,dir)*2,-6,Math.Clamp(GrappleMaximumAcceleration,1,180));_motor.AddImpulse(dir*Math.Max(0,pull)*dt);_ropeLength=Math.Max(2,_ropeLength-dt*GrappleReelSpeed*(_sprint?2:1));}
        }
        if(_grab is {} grabbed&&grabbed.GameObject.Scene==GameObject.Scene){var target=EyePosition+AimDirection*2.6f;grabbed.Velocity=Vector3.Clamp((target-grabbed.Transform.WorldPosition)*14,new(-20),new(20));}
        if(Transform.WorldPosition.Y<-12)_director?.Die();
    }
    protected override void OnLateUpdate()
    {
        if(_eye==null)return;float dt=(float)Time.UnscaledDeltaTime;
        _eyeHeight+=( (_crouch?.88f:1.65f)-_eyeHeight)*(1-MathF.Exp(-14*dt));
        _roll+=((WallRunning?Vector3.Dot(_wallNormal,Transform.Right)*-10:0)-_roll)*(1-MathF.Exp(-9*dt));
        _recoil=Math.Max(0,_recoil-dt*10);_hitShake=Math.Max(0,_hitShake-dt*5);
        float bob=Playing&&IsGrounded?MathF.Sin((float)(GameObject.Scene?.SimulationTime??0)*Speed*1.5f)*Math.Min(.035f,Speed*.003f):0;
        _eye.Transform.LocalPosition=new(0,_eyeHeight+bob,0);
        _eye.Transform.LocalRotation=Quaternion.CreateFromYawPitchRoll(0,(_pitch+_recoil)*MathF.PI/180,(_roll+MathF.Sin((float)Time.TotalTime*83)*_hitShake*ShakeMultiplier)*MathF.PI/180);
        if(_camera!=null)_camera.FieldOfView=80+Math.Min(12,Speed*.5f)+(Grappling?3:0);
        if(_rope!=null&&Grappling){_rope.Start=EyePosition+Transform.Right*.2f-Vector3.UnitY*.15f;_rope.End=GrapplePoint;_rope.Length=Vector3.Distance(_rope.Start,_rope.End);}
    }
    public void Recoil(float amount){_recoil=Math.Min(6,_recoil+amount);_hitShake=Math.Max(_hitShake,.6f);}
    public void BeginGrapple()
    {
        if(!Playing||GameObject.Scene==null)return;
        if(GameplayQuery3D.Raycast(GameObject.Scene,EyePosition,AimDirection,out var hit,GrappleRange,GameObject,includeTriggers:false))
        {GrapplePoint=hit.Point;_ropeLength=Vector3.Distance(EyePosition,hit.Point)*.55f;Grappling=true;if(_rope!=null)_rope.Enabled=true;_director?.Sound("grapple");}
    }
    public void ReleaseGrapple(){Grappling=false;if(_rope!=null)_rope.Enabled=false;}
    public void Equip(KarlsonWeapon3D weapon){ThrowWeapon(false);_weapon=weapon;weapon.Equip(this);_director?.Sound("pickup");}
    public void ThrowWeapon(bool launch=true){if(_weapon==null)return;_weapon.Drop(this,launch);_weapon=null;}
    public void Interact()
    {
        if(_grab!=null){ReleaseGrab();return;}var scene=GameObject.Scene;if(scene==null)return;
        if(!GameplayQuery3D.SphereCast(scene,EyePosition,AimDirection,.15f,out var hit,4,GameObject,includeTriggers:false))return;
        if(hit.GameObject.GetComponent<KarlsonWeapon3D>() is {} weapon){Equip(weapon);return;}
        if(hit.GameObject.GetComponent<Rigidbody3D>() is {BodyType:RigidbodyBodyType3D.Dynamic} body&&body.Mass<=40){_grab=body;_grabDamping=body.LinearDamping;_grabAngular=body.AngularDamping;body.LinearDamping=8;body.AngularDamping=5;}
    }
    public void ReleaseGrab(){if(_grab!=null){_grab.LinearDamping=_grabDamping;_grab.AngularDamping=_grabAngular;_grab=null;}}
    public void Melee()
    {
        if(GameObject.Scene==null)return;
        if(_grab!=null){var body=_grab;ReleaseGrab();body.AddImpulse(AimDirection*body.Mass*15);Recoil(2);return;}
        if(GameplayQuery3D.SphereCast(GameObject.Scene,EyePosition,AimDirection,.25f,out var hit,2.5f,GameObject,includeTriggers:false))
        {hit.GameObject.GetComponent<HealthComponent>()?.Damage(65);hit.GameObject.GetComponent<Rigidbody3D>()?.AddImpulseAtPosition(AimDirection*12,hit.Point);hit.GameObject.GetComponent<KarlsonProp3D>()?.Impact(65);Recoil(2);_director?.Sound("hit");}
    }
    static Vector3 Approach(Vector3 v,Vector3 target,float distance){var delta=target-v;float length=delta.Length();return length<=distance?target:v+delta/length*distance;}
    protected override void OnStop(){ReleaseGrapple();ReleaseGrab();}
}
