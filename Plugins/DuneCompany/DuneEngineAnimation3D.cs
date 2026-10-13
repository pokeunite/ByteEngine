using System.Numerics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
/// <summary>Visual pulley spin. Never changes a physics body, casting or mounting transform.</summary>
public sealed class DuneEngineAnimation3D:Component
{
 public bool Running {get;set;}
 public float Load {get;set;}
 GameObject? _pulley,_casting;Quaternion _rest;Vector3 _castingRest;float _phase;bool _resolved;
 protected override void OnUpdate(){
  if(!_resolved){_resolved=true;var nodes=Walk(GameObject).ToArray();_pulley=nodes.FirstOrDefault(o=>o.Name is "Engine_Pulley" or "Moving_EnginePulley");_rest=_pulley?.Transform.LocalRotation??Quaternion.Identity;_casting=nodes.FirstOrDefault(o=>o.Name is "Fixed_Body" or "Fixed_Body_Geometry");_castingRest=_casting?.Transform.LocalPosition??Vector3.Zero;}
  float dt=(float)ByteEngine.Core.Time.DeltaTime;
  if(Running)_phase=(_phase+dt*MathF.Tau*(1.5f+Math.Clamp(Load,0,1)*6))%MathF.Tau;
  if(_pulley!=null)_pulley.Transform.LocalRotation=Quaternion.Normalize(_rest*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,_phase));
  // Only imported render geometry moves; mounting roots and physics poses stay untouched.
  if(_casting!=null)_casting.Transform.LocalPosition=_castingRest+(Running?new Vector3(MathF.Sin(_phase*2.7f),MathF.Sin(_phase*3.3f),0)*(.001f+Math.Clamp(Load,0,1)*.0015f):Vector3.Zero);
 }
 static IEnumerable<GameObject> Walk(GameObject node){foreach(var child in node.Children){yield return child;foreach(var item in Walk(child))yield return item;}}
}
public sealed partial class DuneWorkshop3D
{
 readonly List<DuneEngineAnimation3D> _engineAnimations=[];
 void UpdateEngineAnimations(){foreach(var animation in _engineAnimations){animation.Running=!Building;animation.Load=Building?0:_engineLoad;}}
}
