using System.Numerics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 public static Vector3 SnapFace(Vector3 hit,Vector3 normal,Vector3 low,Vector3 high,float step=.25f)
 {
  Vector3 center=(low+high)*.5f;
  float Snap(float value,float minimum,float maximum,float origin){float half=(maximum-minimum)*.5f;int steps=Math.Max(0,(int)MathF.Floor((half+.001f)/step));return origin+Math.Clamp(MathF.Round((value-origin)/step),-steps,steps)*step;}
  step=Math.Clamp(step,.125f,.5f);
  var p=new Vector3(Snap(hit.X,low.X,high.X,center.X),Snap(hit.Y,low.Y,high.Y,center.Y),Snap(hit.Z,low.Z,high.Z,center.Z));
  if(Math.Abs(normal.X)>.5f)p.X=normal.X>0?high.X:low.X;if(Math.Abs(normal.Y)>.5f)p.Y=normal.Y>0?high.Y:low.Y;if(Math.Abs(normal.Z)>.5f)p.Z=normal.Z>0?high.Z:low.Z;return p;
 }
 static Vector3 NearestAxis(Vector3 v){var a=Vector3.Abs(v);return a.X>=a.Y&&a.X>=a.Z?new(MathF.Sign(v.X),0,0):a.Y>=a.Z?new(0,MathF.Sign(v.Y),0):new(0,0,MathF.Sign(v.Z));}
}
