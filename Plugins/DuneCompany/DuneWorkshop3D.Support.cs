using System.Numerics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 Vector3 MountPoint(int type,Vector3 incoming){var d=_catalog[type];if(d.Wheel)return new(0,0,d.High.Z);var bounds=_physicsDefinitions.TryGetValue(type,out var p)&&p.Articulated?p.Fixed:new PartBounds(d.Low,d.High);return bounds.Center+Vector3.Multiply(incoming,bounds.Size*.5f);}
 public bool HasMountSupport(int type,Vector3 position,Quaternion rotation,Vector3 incoming,int parent)
 {
  var def=_catalog[type];if(_blocks.FirstOrDefault(b=>b.Id==parent) is {} host && host.Type is 23 or 24 or 25 or 26 or 30 or 31)return false;if(type<=12)return true;
  Vector3 normal=Vector3.Transform(incoming,rotation);Vector3 first=Math.Abs(incoming.Y)>.5f?Vector3.UnitX:Math.Abs(incoming.X)>.5f?Vector3.UnitZ:Vector3.UnitX;Vector3 second=Vector3.Normalize(Vector3.Cross(incoming,first));
  Vector3 own=MountPoint(type,incoming);
  Vector3 center=position+Vector3.Transform(own,rotation);first=Vector3.Transform(first,rotation);second=Vector3.Transform(second,rotation);
  int samplesX=Math.Max(2,(int)MathF.Ceiling(def.SupportWidth/.20f)),samplesY=Math.Max(2,(int)MathF.Ceiling(def.SupportLength/.20f));
  for(int x=0;x<=samplesX;x++)for(int y=0;y<=samplesY;y++){
   Vector3 point=center+normal*.03f+first*((x/(float)samplesX-.5f)*(def.SupportWidth-.035f))+second*((y/(float)samplesY-.5f)*(def.SupportLength-.035f));bool supported=false;
   foreach(var b in _blocks){if(b.Id==_move||!(b.Type<=10||b.Id==parent))continue;var p=Vector3.Transform(point-b.P,Quaternion.Inverse(b.Q));var support=_catalog[b.Type];if(p.X>=support.Low.X-.015f&&p.X<=support.High.X+.015f&&p.Y>=support.Low.Y-.015f&&p.Y<=support.High.Y+.015f&&p.Z>=support.Low.Z-.015f&&p.Z<=support.High.Z+.015f){supported=true;break;}}
   if(!supported)return false;
  }return true;
 }
 public bool HasSteeringConnection(int id){var seen=new HashSet<int>();var p=_blocks.FirstOrDefault(b=>b.Id==id);while(p!=null&&p.Parent>=0&&seen.Add(p.Id)){var parent=_blocks.FirstOrDefault(b=>b.Id==p.Parent);if(parent?.Type==18&&p.MovingMount)return true;p=parent;}return false;}
}
