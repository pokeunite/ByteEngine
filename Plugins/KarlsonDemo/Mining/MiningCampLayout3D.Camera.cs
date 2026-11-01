using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningCampLayout3D
{
 public float PanSpeed{get;set;}=30;
 public float MinimumCameraWidth{get;set;}=20;
 public float MaximumCameraWidth{get;set;}=150;
 bool draggingView;Vector3 panAnchor;
 public Vector2 ViewCentre=>editCamera is {} camera?new(camera.Transform.WorldPosition.X,camera.Transform.WorldPosition.Z-24):Vector2.Zero;
 public void PanView(Vector2 delta)
 {
  if(!IsEditing||editCamera==null||!float.IsFinite(delta.X)||!float.IsFinite(delta.Y))return;
  var old=ViewCentre;var next=Vector2.Clamp(old+delta,Vector2.Min(BuildMinimum,BuildMaximum),Vector2.Max(BuildMinimum,BuildMaximum));
  editCamera.Transform.WorldPosition+=new Vector3(next.X-old.X,0,next.Y-old.Y);
 }
 public void ZoomView(float wheel)
 {
  if(!IsEditing||editCamera?.GetComponent<Camera3D>() is not {} camera||!float.IsFinite(wheel))return;
  float min=Math.Clamp(MinimumCameraWidth,10,100),max=Math.Max(min,Math.Clamp(MaximumCameraWidth,20,250));
  camera.OrthographicWidth=Math.Clamp(camera.OrthographicWidth*MathF.Exp(-wheel*.12f),min,max);
 }
 bool UpdateCampCamera()
 {
  if(!Input.IsGameViewHovered){draggingView=false;return false;}
  Vector2 direction=new((Input.IsKeyDown(Key.D)||Input.IsKeyDown(Key.Right)?1:0)-(Input.IsKeyDown(Key.A)||Input.IsKeyDown(Key.Left)?1:0),(Input.IsKeyDown(Key.S)||Input.IsKeyDown(Key.Down)?1:0)-(Input.IsKeyDown(Key.W)||Input.IsKeyDown(Key.Up)?1:0));
  if(direction.LengthSquared()>0)PanView(Vector2.Normalize(direction)*Math.Clamp(PanSpeed,1,150)*(float)Time.UnscaledDeltaTime);
  if(!ButtonHovered())ZoomView(Input.Snapshot.MouseWheel);
  bool middle=Input.IsMouseButtonDownForUi(MouseButton.Middle)&&!ButtonHovered();
  if(middle&&PointerGround(out var point))
  {
   if(draggingView){PanView(new(panAnchor.X-point.X,panAnchor.Z-point.Z));PointerGround(out point);}
   panAnchor=point;
  }
  draggingView=middle;return middle;
 }
}
