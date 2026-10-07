using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
namespace ByteEngine.Core.Gameplay;
/// <summary>Controlled surface comparison probe, deliberately independent of vehicle joint physics.</summary>
public sealed class SandLabProbe3D:Component
{
 public float Speed{get;set;}=5;public bool TyreMode{get;set;}
 InteractiveSand3D? _sand;Camera3D? _camera;float _yaw=35,_pitch=38,_distance=10;
 protected override void OnStart(){foreach(var obj in GameObject.Scene!.GameObjects){_sand??=obj.GetComponent<InteractiveSand3D>();}_camera=GameObject.Scene.ActiveCamera;}
 protected override void OnUpdate(){if(_sand==null)return;float dt=Math.Min((float)Time.DeltaTime,.05f);if(Input.IsKeyPressed(Key.F6))_sand.ResetTracks();if(Input.IsKeyPressed(Key.Tab))TyreMode=!TyreMode;
  var from=Transform.WorldPosition;var move=new Vector3((Input.IsKeyDown(Key.D)?1:0)-(Input.IsKeyDown(Key.A)?1:0),0,(Input.IsKeyDown(Key.S)?1:0)-(Input.IsKeyDown(Key.W)?1:0));if(move.LengthSquared()>1)move=Vector3.Normalize(move);
  var to=from+move*Speed*dt;to.X=Math.Clamp(to.X,-13,13);to.Z=Math.Clamp(to.Z,-13,13);
  if(move.LengthSquared()>0){
   if(TyreMode){foreach(float x in new[]{-.9f,.9f})_sand.Stamp(from+new Vector3(x,0,0),to+new Vector3(x,0,0),.28f,.65f);}
   else _sand.Stamp(from,to,.85f);
  }
  if(_sand.TrySampleWorld(to,out var surface,out _))to.Y=surface.Y+.7f;Transform.WorldPosition=to;
  if(Input.IsKeyDown(Key.J))_yaw-=60*dt;if(Input.IsKeyDown(Key.L))_yaw+=60*dt;if(Input.IsKeyDown(Key.I))_pitch=Math.Min(75,_pitch+40*dt);if(Input.IsKeyDown(Key.K))_pitch=Math.Max(10,_pitch-40*dt);
  if(Input.IsKeyDown(Key.U))_distance=Math.Max(4,_distance-8*dt);if(Input.IsKeyDown(Key.O))_distance=Math.Min(28,_distance+8*dt);
  if(_camera!=null){float y=_yaw*MathF.PI/180,p=_pitch*MathF.PI/180;var target=to-new Vector3(0,.4f,0);_camera.Transform.WorldPosition=target+new Vector3(MathF.Sin(y)*MathF.Cos(p),MathF.Sin(p),MathF.Cos(y)*MathF.Cos(p))*_distance;
   Matrix4x4.Invert(Matrix4x4.CreateLookAt(_camera.Transform.WorldPosition,target,Vector3.UnitY),out var world);_camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(world);
  }
 }
}
