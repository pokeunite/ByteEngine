using System.Numerics;
using ByteEngine.Core;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 bool _cameraReady;float _followYaw,_lookTimer;Vector3 _cameraTarget;
 public Vector3 CameraPosition=>_camera?.Transform.WorldPosition??Vector3.Zero;
 void UpdateCamera()
 {
  if(_camera==null)return;float dt=Math.Clamp((float)Time.DeltaTime,0,.1f);Vector3 low=new(float.MaxValue),high=new(float.MinValue);foreach(var block in _blocks){var bounds=Box(_catalog[block.Type],block.P,block.Q);low=Vector3.Min(low,bounds.low);high=Vector3.Max(high,bounds.high);}float span=Math.Max(high.X-low.X,high.Z-low.Z);
  var localCenter=(low+high)*.5f;var target=Transform.WorldPosition+Vector3.Transform(localCenter,Transform.WorldRotation);
  if(Building){_camera.FieldOfView=62;var pos=target+new Vector3(MathF.Sin(_orbit)*MathF.Cos(_elevation),MathF.Sin(_elevation),MathF.Cos(_orbit)*MathF.Cos(_elevation))*_zoom;LookCamera(pos,target);_cameraReady=false;return;}
  if(!_cameraReady){_followYaw=_yaw;_cameraTarget=target;_cameraReady=true;}
  _lookTimer=Math.Max(0,_lookTimer-dt);float delta=MathF.Atan2(MathF.Sin(_yaw-_followYaw),MathF.Cos(_yaw-_followYaw));_followYaw+=delta*(1-MathF.Exp(-5*dt));float orbit=_followYaw+(_lookTimer>0?_orbit:0);float pitch=_lookTimer>0?Math.Clamp(_elevation,.15f,.8f):.30f;
  float distance=Math.Clamp(span*.8f+3.0f+Math.Min(Math.Abs(Speed)*.012f,.45f),4.5f,12);_cameraTarget=Vector3.Lerp(_cameraTarget,target,1-MathF.Exp(-10*dt));var desired=_cameraTarget+new Vector3(MathF.Sin(orbit)*MathF.Cos(pitch),MathF.Sin(pitch),MathF.Cos(orbit)*MathF.Cos(pitch))*distance;
  if(_sand!=null){for(int i=1;i<=12;i++){var point=Vector3.Lerp(_cameraTarget+Vector3.UnitY*.6f,desired,i/12f);if(_sand.Sample(point,out var ground,out _,out _,out _)&&point.Y<ground.Y+.45f){desired=Vector3.Lerp(_cameraTarget,desired,Math.Max(.25f,(i-1)/12f));desired.Y=Math.Max(desired.Y,ground.Y+.55f);break;}}}
  var position=Vector3.Lerp(_camera.Transform.WorldPosition,desired,1-MathF.Exp(-9*dt));LookCamera(position,_cameraTarget+new Vector3(-MathF.Sin(_yaw),0,-MathF.Cos(_yaw))*Math.Min(1.5f,Math.Abs(Speed)*.06f));_camera.FieldOfView=62+Math.Clamp(Math.Abs(Speed)*.15f,0,5);
 }
 void LookCamera(Vector3 position,Vector3 target){_camera!.Transform.WorldPosition=position;Matrix4x4.Invert(Matrix4x4.CreateLookAt(position,target,Vector3.UnitY),out var matrix);_camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(matrix);}
}
