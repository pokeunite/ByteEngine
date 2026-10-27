using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningDefense3D
{
 [Category("Model and animation")] public AssetReference VisualModel{get;set;}=AssetReference.Empty;
 [Category("Model and animation")] public float VisualScale{get;set;}=.65f;
 [Category("Model and animation")] public string AnimationPrefix{get;set;}="";
 [Category("Model and animation")] public string DeployClip{get;set;}="Deploy";
 [Category("Model and animation")] public string IdleClip{get;set;}="Idle";
 [Category("Model and animation")] public string FireClip{get;set;}="Fire";
 [Category("Model and animation")] public string ArmClip{get;set;}="Arm";
 [Category("Model and animation")] public string TriggerClip{get;set;}="Trigger";
 [Category("Model and animation")] public string OpenClip{get;set;}="Open";
 [Category("Model and animation")] public string YawBone{get;set;}="Yaw";
 [Category("Model and animation")] public string PitchBone{get;set;}="Pitch";
 [Category("Model and animation")] public string MuzzleBone{get;set;}="Muzzle";
 [Category("Live visual state")] public bool VisualReady{get;private set;}
 [Category("Live visual state")] public string VisualError{get;private set;}="";
 [Category("Live visual state")] public string VisualState{get;private set;}="";
 SkeletalMeshRenderer? visual;GameObject? visualRoot;Vector3? visualTarget;float visualClock;bool visualCollecting;
 string Prefix=>string.IsNullOrWhiteSpace(AnimationPrefix)?Kind.ToString():AnimationPrefix;
 bool TryStartVisual()
 {
  if(!AnimationRuntimeAssets.IsConfigured)return false;
  try
  {
   var asset=VisualModel.IsEmpty?new AssetReference("Assets/Models/Mining/Defences/"+Kind+".glb"):VisualModel;
   visualRoot=GameObject.Scene!.CreateGameObject("Animated "+Kind+" model");visualRoot.SetParent(GameObject,false);visualRoot.Transform.LocalScale=new(Math.Clamp(VisualScale,.05f,10));
   visual=visualRoot.AddComponent(new SkeletalMeshRenderer{Model=asset,PlayOnStart=false,AnimationUpdateInterval=1f/30,TransitionDuration=.06f});
   if(visual.ResolvedModel==null)throw new InvalidDataException("Defence model could not load: "+asset.ProjectPath);
   // The inactive layer enables native procedural bone offsets, preserving recoil and child pivots.
   visual.ConfigurePoseProfile(new(){Name="Mechanical defence aiming",Layers=[new(){Name="Runtime pivot control",Enabled=false,Weight=0}]});
   VisualReady=true;PlayVisual(DeployClip,false);
   if(Kind==MiningDefenseKind.SupplyCache){cacheHeight=8;visualRoot.Transform.LocalPosition=new(0,cacheHeight,0);}
   return true;
  }
  catch(Exception e){VisualError=e.Message;if(visualRoot!=null)GameObject.Scene!.DestroyGameObject(visualRoot);visual=null;visualRoot=null;return false;}
 }
 void CreateShotEffects(){if(Kind is MiningDefenseKind.Mine or MiningDefenseKind.SupplyCache)return;beam=Part("Shot trace",Vector3.Zero,new(.025f),new(1,.8f,.25f,1));beam.Active=false;shell=Part("Mortar round",Vector3.Zero,new(.2f),new(.65f,.42f,.08f,1),PrimitiveMeshType.Sphere);shell.Active=false;}
 public bool PlayVisual(string clip,bool loop=false)
 {
  if(visual==null)return false;string name=clip.Contains('_')?clip:Prefix+"_"+clip;
  if(!visual.Play(name,loop,.04f)){VisualError="Missing defence clip: "+name;return false;}
  VisualState=clip;visualClock=Math.Max(.03f,visual.Duration);VisualError="";return true;
 }
 void TickVisual(float dt)
 {
  if(!VisualReady||visual==null)return;visualClock=Math.Max(0,visualClock-dt);
  if(visualCollecting){if(visualClock<=0){Spent=true;GameObject.Scene!.DestroyGameObject(GameObject);}return;}
  if(Spent){if(visualClock<=0)GameObject.Scene!.DestroyGameObject(GameObject);return;}
  if(VisualState==DeployClip&&visualClock<=0){if(Kind==MiningDefenseKind.Mine)PlayVisual(ArmClip);else PlayVisual(IdleClip,true);}
  else if(VisualState==ArmClip&&visualClock<=0)PlayVisual(IdleClip,true);
  else if(VisualState==FireClip&&visualClock<=0)PlayVisual(IdleClip,true);
  if(visualTarget is {} target&&Kind is MiningDefenseKind.Sentry or MiningDefenseKind.Repeater or MiningDefenseKind.Mortar)
  {
   if(!Matrix4x4.Invert(visualRoot!.Transform.WorldMatrix,out var inverse))return;var local=Vector3.Transform(target,inverse);float authored=VisualState==IdleClip&&visual.Duration>0?MathF.Sin(visual.PlaybackTime/visual.Duration*MathF.Tau)*.24f:0;
   visual.SetBoneModelRotationOffset(YawBone,Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(-local.X,-local.Z)-authored));
   if(Kind!=MiningDefenseKind.Mortar){float pitch=Math.Clamp(MathF.Atan2(local.Y-1.6f,new Vector2(local.X,local.Z).Length()),-.35f,.35f);visual.SetBoneModelRotationOffset(PitchBone,Quaternion.CreateFromAxisAngle(Vector3.UnitX,pitch));}
  }
 }
 void TrackVisualTarget(Vector3 point){visualTarget=point;}
 Vector3 ShotOrigin()
 {
  if(visual!=null){string bone=Kind==MiningDefenseKind.Repeater?(ShotsFired%2==0?"MuzzleLeft":"MuzzleRight"):MuzzleBone;if(visual.TryGetBoneWorldMatrix(bone,out var matrix))return matrix.Translation;}
  return Transform.WorldPosition+Vector3.UnitY*1.1f;
 }
 void PlayShotVisual(){if(VisualReady)PlayVisual(FireClip);}
 void DetonateVisual(){Spent=true;if(VisualReady)PlayVisual(TriggerClip);else GameObject.Scene!.DestroyGameObject(GameObject);}
 bool CollectVisual(){if(!VisualReady)return false;Spent=true;visualCollecting=true;PlayVisual(OpenClip);return true;}
}
