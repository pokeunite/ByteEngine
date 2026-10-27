using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
public sealed class MiningMachineVfx3D:Component
{
 [SceneObjectReference] public Guid OperationObjectId{get;set;}
 [SceneObjectReference] public Guid WorkshopObjectId{get;set;}
 public MiningMachineKind Machine{get;set;}
 public AssetReference OperatingEffect{get;set;}=AssetReference.Empty;
 public string AnchorBone{get;set;}="";
 public Vector3 AnchorOffset{get;set;}=new(0,1,0);
 public float EffectSize{get;set;}=1;
 public int StrokesPerClip{get;set;}=3;
 public float ContactPhase{get;set;}=.5f;
 public bool LandingPuffs{get;set;}=true;
 [Browsable(false)] public bool Running{get;private set;}
 [Browsable(false)] public VfxPlayer? OperatingPlayer=>operating;
 VfxPlayer? operating;long landings;float previousTime;int previousContact;bool wasRunning;
 protected override void OnStart(){if(OperatingEffect.IsEmpty)return;var o=GameObject.Scene!.CreateGameObject("Machine VFX - "+Machine);o.SetParent(GameObject,false);operating=o.AddComponent(new VfxPlayer{Effect=OperatingEffect,PlayOnStart=false,Size=EffectSize,ViewDistance=90,QualityDistance=25});operating.Prewarm();}
 protected override void OnUpdate()
 {
  var scene=GameObject.Scene;if(scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>() is not {} op)return;var trade=scene.FindGameObject(WorkshopObjectId)?.GetComponent<MiningTrade3D>();
  var renderer=GameObject.GetComponent<SkeletalMeshRenderer>();Running=op.Phase!=MiningPhase.Failed&&(Machine==MiningMachineKind.Drill?op.Phase==MiningPhase.Drilling:Machine==MiningMachineKind.Refinery?op.RefiningAmount>0:trade!=null&&trade.StoredAmmo<trade.Capacity);
  Vector3 point=Transform.WorldPosition+Vector3.TransformNormal(AnchorOffset,Transform.WorldMatrix);
  if(!string.IsNullOrEmpty(AnchorBone)&&renderer?.TryGetBoneWorldMatrix(AnchorBone,out var bone)==true)point=Vector3.Transform(AnchorOffset,bone);
  if(operating!=null){operating.Transform.WorldPosition=point;operating.Transform.WorldScale=Vector3.One;operating.Size=EffectSize;if(Running&&!operating.IsEmitting)operating.Play();else if(!Running&&operating.IsEmitting)operating.Stop();}
  var effects=MiningEffects3D.Find(GameObject);
  if(Machine==MiningMachineKind.AmmoPress&&Running&&renderer is {IsPlaying:true,Duration:>0}){
   int cycles=Math.Clamp(StrokesPerClip,1,32);float t=renderer.PlaybackTime;int contact=(int)MathF.Floor(t/renderer.Duration*cycles-Math.Clamp(ContactPhase,0,.99f));int crossed=contact-previousContact+(t<previousTime?cycles:0);
   if(wasRunning&&crossed>0)effects?.Emit(MiningEffectCue.MachineStamp,point,size:.45f);previousTime=t;previousContact=contact;wasRunning=true;
  }else wasRunning=false;
  long sequence=0;Vector3 landing=point;
  if(GameObject.GetComponent<MiningRefineryOutput3D>() is {} output){sequence=output.LandedIngotSequence;landing=output.LastLandingPosition;}
  else if(GameObject.GetComponent<MiningAmmoBucket3D>() is {} bucket){sequence=bucket.LandedRoundSequence;landing=bucket.LastLandingPosition;}
  if(LandingPuffs&&sequence>landings&&op.Phase!=MiningPhase.Failed)effects?.Emit(MiningEffectCue.MaterialLanding,landing,size:Machine==MiningMachineKind.Refinery?.5f:.16f);landings=sequence;
 }
 protected override void OnStop()=>operating?.Stop(true);
}
