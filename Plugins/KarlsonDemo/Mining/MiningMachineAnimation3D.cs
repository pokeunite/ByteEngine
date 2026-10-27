using ByteEngine.Core;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public enum MiningMachineKind{Drill,AmmoPress,Refinery,Tent}
public sealed class MiningMachineAnimation3D:Component
{
 [SceneObjectReference] public Guid OperationObjectId{get;set;}
 [SceneObjectReference] public Guid WorkshopObjectId{get;set;}
 public MiningMachineKind Machine{get;set;}
 public string OperatingClip{get;set;}="Operate";
 public float PlaybackSpeed{get;set;}=1;
 public bool Running{get;private set;}
 public string LastError{get;private set;}="";
 bool? previous;float retry;
 protected override void OnUpdate()
 {
  var op=GameObject.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();var trade=GameObject.Scene?.FindGameObject(WorkshopObjectId)?.GetComponent<MiningTrade3D>();var renderer=GameObject.GetComponent<SkeletalMeshRenderer>();if(renderer==null)return;
  Running=Machine switch{MiningMachineKind.Drill=>op?.Phase==MiningPhase.Drilling,MiningMachineKind.AmmoPress=>trade!=null&&trade.StoredAmmo<trade.Capacity&&op?.Phase!=MiningPhase.Failed,MiningMachineKind.Refinery=>op?.RefiningAmount>0||GameObject.GetComponent<MiningRefineryOutput3D>()?.HasDeliveryWork==true,_=>true};
  renderer.Speed=PlaybackSpeed;
  retry-=(float)Time.DeltaTime;if(previous==Running)return;if(Running){if(retry>0)return;if(renderer.Play(OperatingClip,true,0)){previous=true;LastError="";}else{LastError="Operating clip is not available: "+OperatingClip;retry=.5f;}}
  else{renderer.Pause();previous=false;}
 }
}
