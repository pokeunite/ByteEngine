using System.Numerics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
/// <summary>Matches the model's ShiftUp/ShiftDown clips; only the lever render node moves.</summary>
public sealed class DuneGearboxAnimation3D:Component
{
 GameObject? _lever;Quaternion _rest;float _elapsed=1;int _direction;
 public float LeverAngle{get;private set;}
 public bool LeverResolved=>_lever!=null;
 public void Shift(int direction){_direction=Math.Sign(direction);_elapsed=0;Resolve();}
 void Resolve(){if(_lever!=null)return;_lever=Walk(GameObject).FirstOrDefault(o=>o.Name=="Shift_Lever");if(_lever!=null)_rest=_lever.Transform.LocalRotation;}
 public void Advance(float dt){Resolve();_elapsed+=Math.Max(0,dt);LeverAngle=_elapsed<.22f?MathF.Sin(_elapsed/.22f*MathF.PI)*.32f*_direction:0;if(_lever!=null)_lever.Transform.LocalRotation=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitX,LeverAngle)*_rest);}
 protected override void OnUpdate()=>Advance((float)ByteEngine.Core.Time.DeltaTime);
 static IEnumerable<GameObject> Walk(GameObject o){foreach(var child in o.Children){yield return child;foreach(var nested in Walk(child))yield return nested;}}
}
