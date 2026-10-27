using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
/// <summary>Editable third-person combat framing; native controller/boom own movement and collision.</summary>
public sealed class MiningThirdPerson3D:Component
{
 [SceneObjectReference] public Guid InterfaceObjectId{get;set;}
 public InputActionReference AimAction{get;set;}=InputActionReference.Named("Aim");
 public float NormalArmLength{get;set;}=2.8f;
 public float AimArmLength{get;set;}=2.1f;
 public float NormalFieldOfView{get;set;}=65;
 public float AimFieldOfView{get;set;}=52;
 public float NormalSensitivity{get;set;}=.12f;
 public float AimSensitivity{get;set;}=.075f;
 public float ZoomSharpness{get;set;}=14;
 public bool FaceCameraWhileArmed{get;set;}=true;
 public bool DriveUpperBodyPitch{get;set;}=true;
 public bool ShowCrosshair{get;set;}=true;
 public Vector4 CrosshairColor{get;set;}=new(.96f,.94f,.82f,1);
 public bool Aiming{get;private set;}
 public override int UpdateOrder=>-350;
 public override int? RenderOrder=>850;
 protected override void OnUpdate()
 {
  var control=GameObject.GetComponent<PlayerController3D>();var boom=GameObject.GetComponent<CameraBoom3D>();var gun=GameObject.GetComponent<FpsGunLab3D>();if(control==null||boom==null)return;
  bool modal=GameObject.Scene?.FindGameObject(InterfaceObjectId)?.GetComponent<MiningBusinessUi3D>() is {Screen:not MiningScreen.None};
  Aiming=!modal&&control.Enabled&&InputActions.IsDown(AimAction);
  bool armed=gun?.HasWeapon==true;
  control.CharacterRotation=armed&&(FaceCameraWhileArmed||Aiming)?CharacterRotationMode.FaceCamera:CharacterRotationMode.FaceMovement;
  float smooth=1-MathF.Exp(-Math.Max(0,ZoomSharpness)*(float)Time.DeltaTime);
  boom.ArmLength+=(Math.Clamp(Aiming?AimArmLength:NormalArmLength,.5f,10)-boom.ArmLength)*smooth;
  boom.MouseSensitivityX=Math.Clamp(Aiming?AimSensitivity:NormalSensitivity,.001f,2);boom.MouseSensitivityY=boom.MouseSensitivityX*.8f;
  if(GameObject.Scene?.FindGameObject(boom.CameraObjectId)?.GetComponent<Camera3D>() is {} camera)camera.FieldOfView+=(Math.Clamp(Aiming?AimFieldOfView:NormalFieldOfView,25,100)-camera.FieldOfView)*smooth;
  if(DriveUpperBodyPitch)GameObject.GetComponent<AnimationController>()?.SetAim(0,armed?-control.ControlPitch:0);
 }
 protected override void OnRender(RenderContext c)
 {
  if(!ShowCrosshair||GameObject.GetComponent<FpsGunLab3D>()?.HasWeapon!=true||GameObject.Scene?.FindGameObject(InterfaceObjectId)?.GetComponent<MiningBusinessUi3D>() is {Screen:not MiningScreen.None})return;
  Vector2 p=new(c.TargetWidth/2f,c.TargetHeight/2f);var color=CrosshairColor;
  if(GameObject.Scene?.ActiveCamera is {} camera&&GameObject.GetComponent<FpsGunLab3D>() is {} gun)
  {
   Vector3 target=gun.GetThirdPersonAimTarget(out bool blocked);if(blocked)color=new(1,.3f,.16f,1);
   Vector4 clip=Vector4.Transform(new Vector4(target,1),camera.GetRenderViewMatrix()*camera.GetProjectionMatrix(c.TargetWidth/(float)Math.Max(1,c.TargetHeight)));
   if(clip.W>.001f)p=new((clip.X/clip.W+1)*c.TargetWidth*.5f,(1-clip.Y/clip.W)*c.TargetHeight*.5f);
  }
  float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),gap=(Aiming?3:5)*s,len=(Aiming?5:7)*s;
  foreach(var axis in new[]{Vector2.UnitX,-Vector2.UnitX,Vector2.UnitY,-Vector2.UnitY}){c.DrawUiLine(p+axis*gap,p+axis*(gap+len),3*s,new(0,0,0,.8f));c.DrawUiLine(p+axis*gap,p+axis*(gap+len),1.5f*s,color);}
 }
}
