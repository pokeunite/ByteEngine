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
 AssetReference OreImpactClip()=>ChooseClip(RefineryOreImpact1,RefineryOreImpact2,RefineryOreImpact3);
 readonly Dictionary<Guid,long> auxiliaryLandings=[];
 void TickAuxiliaryRefineryAudio(MiningOperation3D op,float dt){foreach(var extra in GameObject.Scene!.GetComponentsSnapshot<MiningAuxiliaryRefinery3D>()){
  if(extra.OperationObjectId!=op.GameObject.Id)continue;var output=extra.GameObject.GetComponent<MiningRefineryOutput3D>();bool running=op.Phase!=MiningPhase.Failed&&extra.LoadedRaw>0;
  SetLoop("Additional refinery "+extra.GameObject.Id,RefineryLoop,extra.GameObject,running,MachineVolume*RefineryVolumeMultiplier,dt);
  SetLoop("Additional conveyor "+extra.GameObject.Id,RefineryConveyorLoop,extra.GameObject,running||output?.HasDeliveryWork==true,RefineryConveyorVolume,dt);
  if(output!=null){if(output.LandedIngotSequence>auxiliaryLandings.GetValueOrDefault(extra.GameObject.Id))PlayCue(MiningSoundCue.RefineryOreImpact,output.LastLandingPosition);auxiliaryLandings[extra.GameObject.Id]=output.LandedIngotSequence;}
 }}
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
