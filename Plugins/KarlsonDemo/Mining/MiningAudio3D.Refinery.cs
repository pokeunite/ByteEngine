using System.ComponentModel;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningAudio3D
{
 [Category("Refinery layers")] public AssetReference RefineryConveyorLoop{get;set;}=AssetReference.Empty;
 [Category("Refinery layers")] public AssetReference RefineryOreImpact1{get;set;}=AssetReference.Empty;
 [Category("Refinery layers")] public AssetReference RefineryOreImpact2{get;set;}=AssetReference.Empty;
 [Category("Refinery layers")] public AssetReference RefineryOreImpact3{get;set;}=AssetReference.Empty;
 [Category("Refinery layers")] public float RefineryConveyorVolume{get;set;}=.42f;
 [Category("Refinery layers")] public float RefineryOreVolume{get;set;}=.65f;
 [Category("Interface sounds")] public AssetReference RefineryMenuOpen{get;set;}=AssetReference.Empty;
 [Category("Interface sounds")] public AssetReference MenuClose{get;set;}=AssetReference.Empty;
 [Browsable(false)] public long RefineryImpactCues{get;private set;}
 long refineryLandings;
 AssetReference OreImpactClip()
 {
  var clips=new[]{RefineryOreImpact1,RefineryOreImpact2,RefineryOreImpact3}.Where(a=>!a.IsEmpty).ToArray();
  return clips.Length>0?clips[random.Next(clips.Length)]:AssetReference.Empty;
 }
 void TickRefineryLayers(GameObject? refinery,MiningOperation3D op,float dt)
 {
  var output=refinery?.GetComponent<MiningRefineryOutput3D>();
  bool belt=op.Phase!=MiningPhase.Failed&&(op.RefiningAmount>0||output?.HasDeliveryWork==true);
  SetLoop("Refinery conveyor",RefineryConveyorLoop,refinery,belt,Math.Clamp(RefineryConveyorVolume,0,1),dt,position:output?.ModelAnchorsResolved==true?output.ConveyorSoundPosition:null);
  if(output==null)return;
  long count=output.LandedIngotSequence;
  if(count>refineryLandings&&op.Phase!=MiningPhase.Failed&&PlayCue(MiningSoundCue.RefineryOreImpact,output.LastLandingPosition))RefineryImpactCues++;
  refineryLandings=count;
 }
}
