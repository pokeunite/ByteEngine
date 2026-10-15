using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Characters;

namespace ByteEngine.Core.Physics;

/// <summary>Direct mouse crane/drag on a world XY plane. Availability is authored through events.</summary>
public sealed class PhysicsDrag3D : Component
{
    public bool Interactable { get; set; } = true;
    public float PlaneZ { get; set; }
    public bool FollowBodyPlane { get; set; }
    public Vector2 Minimum { get; set; } = new(-100,0);
    public Vector2 Maximum { get; set; } = new(100,100);
    public bool IsDragging { get; private set; }
    Vector3 offset;
    RigidbodyBodyType3D previousType;
    protected override void OnUpdate()
    {
        if(GameObject.Scene?.ActiveCamera is not {} camera || GameObject.GetComponent<Rigidbody3D>() is not {} body || GameObject.GetComponent<BoxCollider3D>() is not {} box || Input.GameViewSize.Y<=0) return;
        if(FollowBodyPlane&&!IsDragging)PlaneZ=Transform.WorldPosition.Z;
        var ray=camera.ScreenPointToRay(Input.GameViewPointerNormalized,Input.GameViewSize.X/Input.GameViewSize.Y);
        if(Math.Abs(ray.Direction.Z)<.00001f)return;
        Vector3 point=ray.Origin+ray.Direction*((PlaneZ-ray.Origin.Z)/ray.Direction.Z);
        if(!IsDragging && Interactable && Input.IsGameViewHovered && Input.IsMouseButtonPressedForUi(MouseButton.Left))
        {
            Vector3 p=Transform.WorldPosition+box.Center,s=box.Size*Transform.WorldScale;
            if(Math.Abs(point.X-p.X)<=s.X/2 && Math.Abs(point.Y-p.Y)<=s.Y/2)
            {IsDragging=true;offset=Transform.WorldPosition-point;previousType=body.BodyType;body.BodyType=RigidbodyBodyType3D.Kinematic;Input.NotifyGameViewPointerAim();}
        }
        if(!IsDragging)return;
        if(!Input.IsMouseButtonDownForUi(MouseButton.Left)||!Input.IsGameViewFocused)
        {Release(body);return;}
        var next=point+offset;
        Transform.WorldPosition=new(Math.Clamp(next.X,Minimum.X,Maximum.X),Math.Clamp(next.Y,Minimum.Y,Maximum.Y),PlaneZ);
        body.Velocity=Vector3.Zero;
    }
    void Release(Rigidbody3D body){IsDragging=false;body.BodyType=previousType;body.Velocity=Vector3.Zero;body.WakeUp();}
    protected override void OnStop(){if(IsDragging&&GameObject.GetComponent<Rigidbody3D>() is {} body)Release(body);}
}
