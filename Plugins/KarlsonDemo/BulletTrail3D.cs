using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
namespace KarlsonDemo.Plugin;
/// <summary>One depth-tested streak following actual projectile travel; never a debug ray through walls.</summary>
public sealed class BulletTrail3D : Component
{
    public float Length { get; set; } = 1.4f;
    public float Width { get; set; } = .018f;
    private Vector3 _origin;
    private readonly Material _material=new(){Shading=MaterialShadingMode.Unlit,BaseColor=new(1,.65f,.16f,.85f),BlendMode=BlendMode3D.Additive,DepthWriteMode=DepthWriteMode3D.Disabled,DepthTest=true};
    protected override void OnStart()=>_origin=Transform.WorldPosition;
    protected override void OnRender(RenderContext context)
    {
        var velocity=GameObject.GetComponent<Projectile3D>()?.Velocity??Vector3.Zero;
        if(velocity.LengthSquared()<.00001f)return;
        float length=Math.Min(Math.Clamp(Length,.05f,5),Vector3.Distance(_origin,Transform.WorldPosition));if(length<.01f)return;
        var direction=Vector3.Normalize(velocity);float dot=Math.Clamp(Vector3.Dot(Vector3.UnitZ,direction),-1,1);
        var rotation=dot>.9999f?Quaternion.Identity:dot<-.9999f?Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI):Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitZ,direction)),MathF.Acos(dot));
        var matrix=Matrix4x4.CreateScale(Math.Clamp(Width,.003f,.1f),Math.Clamp(Width,.003f,.1f),length)*Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(Transform.WorldPosition-direction*length*.5f);
        context.RenderWorld.Submit(context.Renderer3D.GetPrimitive(PrimitiveMeshType.Cube),_material,matrix,RenderQueue3D.Transparent,true,false,false);
    }
}
