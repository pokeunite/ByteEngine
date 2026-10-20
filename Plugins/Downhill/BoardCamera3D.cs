using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
namespace Downhill;
public sealed class BoardCamera3D:Component
{
 public string TargetObject {get;set;}="Board";
 public float Distance {get;set;}=5.5f;
 public float Height {get;set;}=2.5f;
 public float LookAhead {get;set;}=6;
 public float Response {get;set;}=6;
 public float BaseFov {get;set;}=65;
 public float MaximumFov {get;set;}=78;
 public bool AvoidObstructions {get;set;}=true;
 public float TravelDirectionInfluence {get;set;}=.9f;
 public float DirectionResponse {get;set;}=3;
 Vector3 followDirection;
 GameObject? target;BoardController3D? board;Camera3D? camera;bool placed;
 protected override void OnStart(){target=GameObject.Scene!.FindGameObject(TargetObject);board=target?.GetComponent<BoardController3D>();camera=GameObject.GetComponent<Camera3D>();}
 protected override void OnLateUpdate(){
  if(target==null)return;float dt=(float)Time.DeltaTime,speed=board?.Speed??0;Vector3 forward=target.Transform.Forward;forward.Y=0;forward=Vector3.Normalize(forward);
  Vector3 travel=board?.Body?.Velocity??forward;travel.Y=0;if(speed>1.5f&&travel.LengthSquared()>.1f)forward=Vector3.Normalize(Vector3.Lerp(forward,Vector3.Normalize(travel),TravelDirectionInfluence));
  followDirection=placed?Vector3.Normalize(Vector3.Lerp(followDirection,forward,1-MathF.Exp(-DirectionResponse*dt))):forward;forward=followDirection;
  Vector3 focus=target.Transform.WorldPosition+Vector3.UnitY*.9f,desired=focus-forward*Distance+Vector3.UnitY*Height;
  Vector3 offset=desired-focus;if(AvoidObstructions&&GameplayQuery3D.SphereCast(GameObject.Scene!,focus,Vector3.Normalize(offset),.22f,out var hit,offset.Length(),target,includeTriggers:false))desired=focus+Vector3.Normalize(offset)*Math.Max(.8f,hit.Distance-.2f);
  Transform.WorldPosition=placed?Vector3.Lerp(Transform.WorldPosition,desired,1-MathF.Exp(-Response*dt)):desired;placed=true;
  Vector3 look=focus+forward*(LookAhead+speed*.15f);Matrix4x4.Invert(Matrix4x4.CreateLookAt(Transform.WorldPosition,look,Vector3.UnitY),out var m);Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(m);
  if(camera!=null)camera.FieldOfView=float.Lerp(BaseFov,MaximumFov,Math.Clamp(speed/25,0,1));
 }
}
