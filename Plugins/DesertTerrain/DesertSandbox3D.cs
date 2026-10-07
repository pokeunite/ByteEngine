using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

namespace DesertTerrain;
/// <summary>Replaceable kinematic terrain test harness, not a production vehicle solver.</summary>
public sealed class DesertSandbox3D : Component
{
    public float MaximumSpeed { get; set; } = 22;
    public float Acceleration { get; set; } = 12;
    public float TurnRate { get; set; } = 65;
    public bool UseControls { get; set; } = true;
    public bool FollowCamera { get; set; } = true;
    public float Speed { get; private set; }
    private DesertTerrain3D? _terrain;private Camera3D? _camera;private UiText? _status;
    private readonly Dictionary<Guid,Vector3> _previousContacts=[];
    private float _yaw,_cameraYaw=35,_cameraPitch=36,_cameraDistance=11;
    protected override void OnStart()
    {
        var scene=GameObject.Scene!;_terrain=scene.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain3D>().FirstOrDefault();_camera=scene.ActiveCamera;
        _status=scene.FindGameObject("HUD - Status")?.GetComponent<UiText>();
        _yaw=MathF.Atan2(Transform.Forward.X,-Transform.Forward.Z);
        FitToGround();
    }
    protected override void OnUpdate()
    {
        if(_terrain==null)return;float dt=Math.Clamp((float)Time.DeltaTime,0,.05f);
        if(UseControls)
        {
            if(Input.IsKeyPressed(Key.R)){Transform.WorldPosition=new(0,1,0);_yaw=0;Speed=0;_previousContacts.Clear();}
            if(Input.IsKeyPressed(Key.F6))_terrain.ResetSand();
            if(Input.IsKeyPressed(Key.F5))_terrain.SaveSandToDisk();
            if(Input.IsKeyPressed(Key.F9))_terrain.LoadSandFromDisk();
            if(Input.IsKeyDown(Key.J))_cameraYaw-=70*dt;if(Input.IsKeyDown(Key.L))_cameraYaw+=70*dt;
            if(Input.IsKeyDown(Key.I))_cameraPitch=Math.Min(78,_cameraPitch+45*dt);if(Input.IsKeyDown(Key.K))_cameraPitch=Math.Max(10,_cameraPitch-45*dt);
            if(Input.IsKeyDown(Key.U))_cameraDistance=Math.Max(7,_cameraDistance-15*dt);if(Input.IsKeyDown(Key.O))_cameraDistance=Math.Min(65,_cameraDistance+15*dt);
            StepDrive((Input.IsKeyDown(Key.W)?1:0)-(Input.IsKeyDown(Key.S)?1:0),(Input.IsKeyDown(Key.D)?1:0)-(Input.IsKeyDown(Key.A)?1:0),Input.IsKeyDown(Key.Space),dt);
        }
        if(FollowCamera)UpdateCamera();
        if(_status!=null)_status.Text=$"ROVER / {Math.Abs(Speed)*3.6f:0} km/h / {_terrain.LastSaveMessage}";
    }
    public void StepDrive(float throttle,float steer,bool brake,float dt)
    {
        if(_terrain==null){OnStart();if(_terrain==null)return;}
        dt=Math.Clamp(dt,0,.05f);var position=Transform.WorldPosition;
        if(!_terrain.TryGetSand(position,out var sand))return;
        float acceleration=Math.Clamp(Acceleration,0,80)*sand.Grip;
        Speed+=Math.Clamp(throttle,-1,1)*acceleration*dt;
        Speed*=MathF.Exp(-(brake?9:sand.RollingResistance+(.35f*(Math.Abs(throttle)<.01f?1:0)))*dt);
        Speed=Math.Clamp(Speed,-Math.Clamp(MaximumSpeed,1,60)*.45f,Math.Clamp(MaximumSpeed,1,60));
        _yaw-=Math.Clamp(steer,-1,1)*Math.Clamp(TurnRate,0,150)*MathF.PI/180*dt*Math.Clamp(Speed/6,-1,1)*sand.Grip;
        Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,_yaw);
        var forward=Transform.Forward;position+=new Vector3(forward.X,0,forward.Z)*Speed*dt;
        if(_terrain.TryGetSand(position,out _)){Transform.WorldPosition=position;FitToGround();}else Speed=0;
        foreach(var wheel in GameObject.Children.Where(o=>o.Name.StartsWith("Wheel ",StringComparison.Ordinal)))
        {
            var point=Vector3.Transform(wheel.Transform.LocalPosition with {Y=0},Transform.WorldMatrix);
            if(!_terrain.TryGetSand(point,out var ground))continue;point=ground.Position;
            var visual=wheel.Transform.WorldPosition;visual.Y=ground.Position.Y+.625f;wheel.Transform.WorldPosition=visual;
            if(_previousContacts.TryGetValue(wheel.Id,out var previous))_terrain.StampTrack(previous,point,.9f,.08f);
            _previousContacts[wheel.Id]=point;
            wheel.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,-Speed*(float)Time.TotalTime/.58f);
        }
    }
    private void FitToGround()
    {
        if(_terrain==null||!_terrain.TryGetSand(Transform.WorldPosition,out var center))return;
        var p=Transform.WorldPosition;p.Y=center.Position.Y+1;Transform.WorldPosition=p;
        var forward=new Vector3(-MathF.Sin(_yaw),0,-MathF.Cos(_yaw));
        if(_terrain.TryGetSand(p+forward*1.4f,out var front)&&_terrain.TryGetSand(p-forward*1.4f,out var rear))
        {
            var f=Vector3.Normalize(front.Position-rear.Position);var right=Vector3.Normalize(Vector3.Cross(f,center.Normal));var up=Vector3.Cross(right,f);
            var m=new Matrix4x4(right.X,right.Y,right.Z,0,up.X,up.Y,up.Z,0,-f.X,-f.Y,-f.Z,0,0,0,0,1);Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(m);
        }
    }
    private void UpdateCamera()
    {
        if(_camera==null)return;float yaw=_cameraYaw*MathF.PI/180,pitch=_cameraPitch*MathF.PI/180;var target=Transform.WorldPosition+Vector3.UnitY;
        var pos=target+new Vector3(MathF.Sin(yaw)*MathF.Cos(pitch),MathF.Sin(pitch),MathF.Cos(yaw)*MathF.Cos(pitch))*_cameraDistance;
        if(_terrain!.TryGetSand(pos,out var ground))pos.Y=Math.Max(pos.Y,ground.Position.Y+2);
        _camera.Transform.WorldPosition=pos;Matrix4x4.Invert(Matrix4x4.CreateLookAt(pos,target,Vector3.UnitY),out var world);_camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(world);
    }
    protected override void OnStop(){_previousContacts.Clear();_terrain=null;_camera=null;Speed=0;}
}
