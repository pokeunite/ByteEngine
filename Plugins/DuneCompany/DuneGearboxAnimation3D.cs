using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace DuneCompany;
/// <summary>Samples the imported lever clips without skinning the entire fixed housing.</summary>
public sealed class DuneGearboxAnimation3D:Component
{
 internal ModelAsset? Source;
 public string LeverNodeName{get;set;}="Shift_Lever";
 public string LeverBoneName{get;set;}="GearboxLever";
 public string UpshiftClip{get;set;}="ShiftUp";
 public string DownshiftClip{get;set;}="ShiftDown";
 public float ShiftSeconds{get;set;}=.22f;
 GameObject? _lever;Quaternion _rest;float _elapsed=1;int _direction;bool _moving;
 ImportedAnimation? _up,_down;ImportedQuaternionTrack? _upTrack,_downTrack;
 Matrix4x4 _boneParent,_bindRotation,_leverParent,_inverseLeverParent;Vector3 _boneScale,_bonePosition;
 public float LeverAngle{get;private set;}
 public bool LeverResolved=>_lever!=null&&_upTrack!=null&&_downTrack!=null;
 public void Shift(int direction){_direction=Math.Sign(direction);_elapsed=0;_moving=true;Resolve();}
 void Resolve()
 {
  if(LeverResolved)return;
  if(Source==null&&GameObject.GetComponent<ModelHierarchyInstance>() is {} hierarchy&&AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null)Source=assets.LoadModel(hierarchy.Model);
  if(Source==null)return;
  _lever=Walk(GameObject).FirstOrDefault(o=>o.Name==LeverNodeName);if(_lever==null)return;_rest=_lever.Transform.LocalRotation;
  _up=Source.Animations.FirstOrDefault(a=>a.Name==UpshiftClip);_down=Source.Animations.FirstOrDefault(a=>a.Name==DownshiftClip);
  _upTrack=_up?.Channels.FirstOrDefault(c=>c.NodeName==LeverBoneName)?.Rotation;_downTrack=_down?.Channels.FirstOrDefault(c=>c.NodeName==LeverBoneName)?.Rotation;
  var bone=Source.Nodes.FirstOrDefault(n=>n.Name==LeverBoneName);var mesh=Source.Nodes.FirstOrDefault(n=>n.Name==LeverNodeName);if(bone==null||mesh==null){_lever=null;return;}
  var globals=new Dictionary<string,Matrix4x4>();
  Matrix4x4 Global(string? key){if(key==null)return Matrix4x4.Identity;if(globals.TryGetValue(key,out var result))return result;var node=Source.Nodes.First(n=>n.Key==key);var local=node.LocalTransform;if(node.Name=="Import Space"){Matrix4x4.Decompose(local,out var s,out _,out var p);local=Matrix4x4.CreateScale(s)*Matrix4x4.CreateTranslation(p);}return globals[key]=local*Global(node.ParentKey);}
  _boneParent=Global(bone.ParentKey);_leverParent=RotationOnly(Global(mesh.ParentKey));Matrix4x4.Invert(_leverParent,out _inverseLeverParent);
  Matrix4x4.Decompose(bone.LocalTransform,out _boneScale,out _,out _bonePosition);_bindRotation=RotationOnly(bone.LocalTransform*_boneParent);
 }
 static Matrix4x4 RotationOnly(Matrix4x4 matrix){Matrix4x4.Decompose(matrix,out _,out var rotation,out _);return Matrix4x4.CreateFromQuaternion(rotation);}
 static Quaternion Sample(ImportedQuaternionTrack track,float time)
 {
  var keys=track.Keys;if(keys.Count==0)return Quaternion.Identity;if(time<=keys[0].Time)return keys[0].Value;
  for(int i=1;i<keys.Count;i++)if(time<=keys[i].Time){var a=keys[i-1];var b=keys[i];if(track.Interpolation==ImportedAnimationInterpolation.Step)return a.Value;float span=Math.Max(.000001f,b.Time-a.Time),t=Math.Clamp((time-a.Time)/span,0,1);if(track.Interpolation==ImportedAnimationInterpolation.CubicSpline){float t2=t*t,t3=t2*t;return Quaternion.Normalize(a.Value*(2*t3-3*t2+1)+a.OutTangent*((t3-2*t2+t)*span)+b.Value*(-2*t3+3*t2)+b.InTangent*((t3-t2)*span));}return Quaternion.Normalize(Quaternion.Slerp(a.Value,b.Value,t));}
  return keys[^1].Value;
 }
 public void Advance(float dt)
 {
  Resolve();_elapsed+=Math.Max(0,dt);if(!LeverResolved)return;float duration=Math.Clamp(ShiftSeconds,.05f,1);
  if(_elapsed>=duration){if(_moving)_lever!.Transform.LocalRotation=_rest;_moving=false;LeverAngle=0;return;}
  var clip=_direction>0?_up!:_down!;var track=_direction>0?_upTrack!:_downTrack!;
  var local=Matrix4x4.CreateScale(_boneScale)*Matrix4x4.CreateFromQuaternion(Sample(track,_elapsed/duration*clip.Duration))*Matrix4x4.CreateTranslation(_bonePosition);
  Matrix4x4.Invert(_bindRotation,out var inverseBind);var delta=inverseBind*RotationOnly(local*_boneParent);
  var leverDelta=_leverParent*delta*_inverseLeverParent;Matrix4x4.Decompose(Matrix4x4.CreateFromQuaternion(_rest)*leverDelta,out _,out var rotation,out _);
  _lever!.Transform.LocalRotation=Quaternion.Normalize(rotation);Matrix4x4.Decompose(leverDelta,out _,out var q,out _);LeverAngle=2*MathF.Atan2(q.X,q.W);
 }
 protected override void OnUpdate()=>Advance((float)ByteEngine.Core.Time.DeltaTime);
 static IEnumerable<GameObject> Walk(GameObject o){foreach(var child in o.Children){yield return child;foreach(var nested in Walk(child))yield return nested;}}
}
