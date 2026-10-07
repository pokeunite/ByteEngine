using System.Numerics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 readonly Dictionary<int,(GameObject node,Vector3 position,Quaternion rotation,Matrix4x4 rest)> _outputs=[];readonly Dictionary<int,Matrix4x4> _poses=[];
 void CacheOutputs(){_suspensionVisualRest.Clear();_outputs.Clear();_poses.Clear();_outputRest.Clear();foreach(var b in _blocks){var root=_visuals[b.Id];var name=OutputName(b.Type);var node=Descendants(root).FirstOrDefault(o=>o.Name==name);CaptureDefinition(b,root,node);if(node==null)continue;Matrix4x4.Invert(root.Transform.WorldMatrix,out var inverse);_outputs[b.Id]=(node,node.Transform.LocalPosition,node.Transform.LocalRotation,node.Transform.WorldMatrix*inverse);}}
 static string OutputName(int type)=>type switch{13 or 14 or 15=>"Moving_Wheel",16 or 17 or 32=>"Moving_SuspensionOutput",18=>"Moving_SteeringOutput",19=>"Moving_ServoOutput",20=>"Moving_TrailerOutput",21=>"Moving_PistonOutput",22=>"Moving_ReleasedOutput",_=>""};
 static Matrix4x4 Pose(Vector3 p,Quaternion q)=>Matrix4x4.CreateFromQuaternion(q)*Matrix4x4.CreateTranslation(p);
 (PartBounds bounds,bool moving)[] PickingBounds(int type)
 {
  var def=_catalog[type];if(!_physicsDefinitions.TryGetValue(type,out var p)||!p.Articulated)return [(new(def.Low,def.High),false)];
  if(type==18){var mount=new PartBounds(new(p.Fixed.Low.X,p.Fixed.Low.Y,p.Fixed.Low.Z),new(p.Fixed.High.X,Math.Min(p.Fixed.High.Y,.11f),p.Fixed.High.Z));var plate=new PartBounds(new(p.Moving.Low.X,Math.Max(p.Moving.Low.Y,p.Moving.High.Y-.12f),p.Moving.Low.Z),p.Moving.High);return [(mount,false),(plate,true)];}
  if(type==32)return [(p.Fixed,false),(p.Moving,true)];
  if(type is 16 or 17){var top=p.Fixed;top=new(new(top.Low.X,Math.Max(top.Low.Y,top.High.Y-.26f),top.Low.Z),top.High);var bottom=p.Moving;bottom=new(bottom.Low,new(bottom.High.X,Math.Min(bottom.High.Y,p.Pivot.Y+.12f),bottom.High.Z));return [(top,false),(bottom,true)];}
  return [(p.Fixed,false),(p.Moving,true)];
 }
}
