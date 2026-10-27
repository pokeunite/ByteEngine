using System.ComponentModel;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningAudio3D
{
 [Category("Ammo press layers")] public AssetReference PressConveyorLoop{get;set;}=AssetReference.Empty;
 [Category("Ammo press layers")] public AssetReference PressStroke{get;set;}=AssetReference.Empty;
 [Category("Ammo press layers")] public AssetReference PressBucketImpact1{get;set;}=AssetReference.Empty;
 [Category("Ammo press layers")] public AssetReference PressBucketImpact2{get;set;}=AssetReference.Empty;
 [Category("Ammo press layers")] public AssetReference PressBucketImpact3{get;set;}=AssetReference.Empty;
 [Category("Ammo press layers")] public float PressConveyorVolume{get;set;}=.48f;
 [Category("Ammo press layers")] public float PressStrokeVolume{get;set;}=.48f;
 [Category("Ammo press layers")] public float PressBucketVolume{get;set;}=.58f;
 [Category("Ammo press layers")] public int PressStrokesPerClip{get;set;}=3;
 [Category("Ammo press layers")] public float PressStrokeContactPhase{get;set;}=.5f;
 [Category("Ammo press layers")] public string PressRamBone{get;set;}="Ram";
 [Category("Interface sounds")] public AssetReference PressMenuOpen{get;set;}=AssetReference.Empty;
 [Browsable(false)] public long PressImpactCues{get;private set;}
 [Browsable(false)] public long PressStrokeCues{get;private set;}
 long pressLandings;float pressPreviousTime;int pressPreviousContact;bool pressWasRunning;
 AssetReference PressImpactClip()
 {
  var clips=new[]{PressBucketImpact1,PressBucketImpact2,PressBucketImpact3}.Where(a=>!a.IsEmpty).ToArray();return clips.Length>0?clips[random.Next(clips.Length)]:AssetReference.Empty;
 }
 void TickPressLayers(GameObject? press,MiningOperation3D op,MiningTrade3D trade,float dt)
 {
  var bucket=press?.GetComponent<MiningAmmoBucket3D>();bool running=op.Phase!=MiningPhase.Failed&&trade.StoredAmmo<trade.Capacity;
  SetLoop("Press conveyor",PressConveyorLoop,press,op.Phase!=MiningPhase.Failed&&(running||bucket?.TravellingRounds>0),Math.Clamp(PressConveyorVolume,0,1),dt,position:bucket?.ConveyorSoundPosition);
  if(bucket!=null){long count=bucket.LandedRoundSequence;if(count>pressLandings&&op.Phase!=MiningPhase.Failed&&PlayCue(MiningSoundCue.PressBucketImpact,bucket.LastLandingPosition))PressImpactCues++;pressLandings=count;}
  var renderer=press?.GetComponent<SkeletalMeshRenderer>();
  if(running&&renderer is {IsPlaying:true,Duration:>0})
  {
   int cycles=Math.Clamp(PressStrokesPerClip,1,32);float t=renderer.PlaybackTime;int contact=(int)MathF.Floor(t/renderer.Duration*cycles-Math.Clamp(PressStrokeContactPhase,0,.99f));
   int crossed=contact-pressPreviousContact+(t<pressPreviousTime?cycles:0);
   if(pressWasRunning&&crossed>0){var position=renderer.TryGetBoneWorldMatrix(PressRamBone,out var ram)?ram.Translation:press!.Transform.WorldPosition;if(PlayCue(MiningSoundCue.PressStroke,position))PressStrokeCues++;}
   pressPreviousTime=t;pressPreviousContact=contact;pressWasRunning=true;
  }
  else pressWasRunning=false;
 }
}
