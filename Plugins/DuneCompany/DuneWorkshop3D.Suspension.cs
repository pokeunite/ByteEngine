using System.Numerics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 readonly Dictionary<(int,string),Matrix4x4> _suspensionVisualRest=[];
 void UpdateSuspensionVisual(PlacedBlock b,GameObject root)
 {
  if(_physics==null||b.Type is not (16 or 17 or 32))return;
  var delta=_physicsDefinitions[b.Type].Axis*_physics.SuspensionOffset(b.Id);
  foreach(var name in new[]{"Visual_Spring","Visual_Damper","Visual_LowerArm","Visual_UpperArm"}){
   var node=Descendants(root).FirstOrDefault(n=>n.Name==name);if(node==null)continue;
   var key=(b.Id,name);if(!_suspensionVisualRest.TryGetValue(key,out var rest)){Matrix4x4.Invert(root.Transform.WorldMatrix,out var inv);rest=node.Transform.WorldMatrix*inv;_suspensionVisualRest[key]=rest;}
   Vector3 a,c;bool moveStart=false;
   if(b.Type==32){if(name=="Visual_LowerArm"){a=new(0,.09f,-.055f);c=new(0,.09f,-.79f);}else if(name=="Visual_UpperArm"){a=new(0,.46f,-.055f);c=new(0,.46f,-.79f);}else{a=new(0,.47f,-.11f);c=new(0,.11f,-.72f);}}
   else{a=new(0,.27f,0);c=new(0,(b.Type==17?1.42f:1.04f)-.13f,0);moveStart=true;}
   var start=a+(moveStart?delta:Vector3.Zero);var end=c+(moveStart?Vector3.Zero:delta);var v=c-a;var w=end-start;
   if(w.Length()<.02f)continue;
   var deformation=Matrix4x4.CreateTranslation(-a)*Matrix4x4.CreateFromQuaternion(FromTo(Vector3.Normalize(v),Vector3.UnitY))*Matrix4x4.CreateScale(1,w.Length()/v.Length(),1)*Matrix4x4.CreateFromQuaternion(FromTo(Vector3.UnitY,Vector3.Normalize(w)))*Matrix4x4.CreateTranslation(start);
   SetRenderedWorldPose(node,rest*deformation*root.Transform.WorldMatrix);
  }
 }
}
