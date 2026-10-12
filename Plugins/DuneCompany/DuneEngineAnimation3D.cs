using System.Numerics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
/// <summary>Visual pulley spin. Never changes a physics body, casting or mounting transform.</summary>
public sealed class DuneEngineAnimation3D:Component
{
 public bool Running {get;set;}
 public float Load {get;set;}
 GameObject? _pulley;Quaternion _rest;float _phase;bool _resolved;
 protected override void OnUpdate(){
  if(!_resolved){_resolved=true;var nodes=Walk(GameObject).ToArray();_pulley=nodes.FirstOrDefault(o=>o.Name is "Engine_Pulley" or "Moving_EnginePulley");_rest=_pulley?.Transform.LocalRotation??Quaternion.Identity;}
  float dt=(float)ByteEngine.Core.Time.DeltaTime;
  if(Running)_phase=(_phase+dt*MathF.Tau*(1.5f+Math.Clamp(Load,0,1)*6))%MathF.Tau;
  if(_pulley!=null)_pulley.Transform.LocalRotation=Quaternion.Normalize(_rest*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,_phase));
 }
 static IEnumerable<GameObject> Walk(GameObject node){foreach(var child in node.Children){yield return child;foreach(var item in Walk(child))yield return item;}}
}
public sealed partial class DuneWorkshop3D
{
 readonly List<DuneEngineAnimation3D> _engineAnimations=[];
 void UpdateEngineAnimations(){foreach(var animation in _engineAnimations){animation.Running=true;animation.Load=Building?0:Math.Clamp(Math.Abs(Speed)/25,0,1);}}
}
