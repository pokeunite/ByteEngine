using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
namespace KarlsonDemo.Plugin;
/// <summary>Bounded depth-tested tracer: bright core, tapered additive glow, and a short frozen impact fade.</summary>
public sealed class BulletTrail3D : Component
{
 public float Length {get;set;}=1.8f;
 public float Width {get;set;}=.035f;
 public Vector4 CoreColor {get;set;}=new(1,.95f,.7f,1);
 public Vector4 GlowColor {get;set;}=new(1,.55f,.12f,.55f);
 public float GlowScale {get;set;}=3;
 public float Brightness {get;set;}=1.5f;
 private Vector3 _origin,_frozenDirection;
 private float _fadeSeconds,_age;
 private bool _finished;
 private readonly Material[] _materials=Enumerable.Range(0,6).Select(_=>new Material{Shading=MaterialShadingMode.Unlit,BlendMode=BlendMode3D.Additive,DepthWriteMode=DepthWriteMode3D.Disabled,DepthTest=true}).ToArray();
 [System.ComponentModel.Browsable(false)] public float VisibleLength=>Math.Min(WeaponDefinition3D.Bound(Length,1.8f,.05f,6),Vector3.Distance(_origin,Transform.WorldPosition));
 [System.ComponentModel.Browsable(false)] public bool Finished=>_finished;
 protected override void OnStart()=>_origin=Transform.WorldPosition;
 public void Finish(Vector3 direction,float fadeSeconds)
 {if(direction.LengthSquared()<.000001f)return;_frozenDirection=Vector3.Normalize(direction);_fadeSeconds=WeaponDefinition3D.Bound(fadeSeconds,.07f,.01f,.2f);_age=0;_finished=true;}
 protected override void OnUpdate(){if(_finished)_age+=Math.Max(0,(float)ByteEngine.Core.Time.DeltaTime);}
 protected override void OnRender(RenderContext context)
 {
  Vector3 velocity=_finished?_frozenDirection:GameObject.GetComponent<Projectile3D>()?.Velocity??Vector3.Zero;
  if(velocity.LengthSquared()<.000001f)return;
  float length=VisibleLength;if(length<.01f)return;float fade=_finished?Math.Clamp(1-_age/_fadeSeconds,0,1):1;if(fade<=0)return;
  var direction=Vector3.Normalize(velocity);float dot=Math.Clamp(Vector3.Dot(Vector3.UnitZ,direction),-1,1);
  var rotation=dot>.9999f?Quaternion.Identity:dot<-.9999f?Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI):Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitZ,direction)),MathF.Acos(dot));
  float width=WeaponDefinition3D.Bound(Width,.035f,.004f,.12f),glow=WeaponDefinition3D.Bound(GlowScale,3,1,5),brightness=WeaponDefinition3D.Bound(Brightness,1.5f,.1f,4);
  for(int segment=0;segment<3;segment++)
  {
   float start=segment/3f,end=(segment+1)/3f,taper=1-segment*.32f;
   for(int pass=0;pass<2;pass++)
   {
    float size=width*taper*(pass==0?glow:1);var color=pass==0?GlowColor:CoreColor;
    color=new(new Vector3(color.X,color.Y,color.Z)*brightness,color.W*fade*taper*(pass==0?.65f:1));var material=_materials[segment*2+pass];material.BaseColor=color;
    var matrix=Matrix4x4.CreateScale(size,size,length*(end-start))*Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(Transform.WorldPosition-direction*length*(start+end)*.5f);
    context.RenderWorld.Submit(context.Renderer3D.GetPrimitive(PrimitiveMeshType.Cube),material,matrix,RenderQueue3D.Transparent,true,false,false);
   }
  }
 }
}
