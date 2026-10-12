using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 bool _runawayCaught,_runawayFailed;
 public bool RunawayFailed=>_runawayFailed;
 public float RunawayLoadMass=>_physics?.RecoveryMass??0;
 void ResetNoBrakes()
 {
  if(_physics==null)return;
  Vector3 Ground(Vector3 p){if(_sand?.Sample(p,out var g,out _,out _,out _)==true)p.Y=g.Y+.85f;return p;}
  var start=Ground(RecoveryJob?.ResetPosition??new(3,0,114));var target=Ground(RecoverySpawn);
  _physics.ResetTruck(start,Quaternion.Identity);var cargoRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,-.15f);_physics.ResetRecovery(target,cargoRotation);
  _physics.MaximumHookSpeed=30;_physics.MaximumHookRelativeSpeed=9;_physics.HookReach=12;
  _physics.SetTruckVelocity(new(0,-1.5f,-10));_physics.SetRecoveryVelocity(new(1.65f,-1.5f,-10.9f));
  SetRenderedWorldPose(GameObject,Pose(start,Quaternion.Identity));_cameraReady=false;
  _runawayCaught=_runawayFailed=false;_deliveryHold=0;_impactReady=false;_recoveryTracks.Clear();
  _message="Catch the truck / match its speed / R restarts the chase";
 }
 void UpdateNoBrakes()
 {
  var physics=_physics!;var frame=physics.RecoveryFrame();
  float up=Vector3.Transform(Vector3.UnitY,frame.Rotation).Y;
  bool upright=up>.55f;
  // A secured roll or hanging fall is recoverable. Upright is required at delivery.
  if(!_contractComplete&&!_runawayFailed&&((RecoveryPosition.Y< -2.5f&&!WinchConnected)||RecoveryPosition.Z< -14||(RecoveryPosition.Z<46&&!_runawayCaught)))
  {_runawayFailed=true;DisconnectWinch();JobNotice("LOAD LOST / R TO RESTART THE CHASE");JobSound("vehicle-impact",.9f);}
  if(WinchConnected)_runawayCaught=true;
  bool delivered=!_runawayFailed&&_runawayCaught&&WinchConnected&&upright&&DeliveryReady(DeliveryCenter);
  _deliveryHold=delivered?_deliveryHold+(float)Time.DeltaTime:0;
  if(!_contractComplete&&_deliveryHold>1.2f){_contractComplete=true;if(!AwardContract()){_contractComplete=false;return;}RecordContractCompletion();AtomicSave(Path.Combine(SaveRoot,"recovery-payment.json"),JsonSerializer.Serialize(_credits));DisconnectWinch();}
  RecoveryObjective=_runawayFailed?"LOAD LOST / Press R to restart":_contractComplete?"TRUCK SECURED":!_runawayCaught?"Catch the runaway truck!":RecoveryPosition.Z>47?"Broken bridge ahead / control the load!":RecoveryPosition.Z>35?"Keep it upright!":"Stop both vehicles in the recovery yard.";
  Text("Battle objective",RecoveryObjective);
  string danger=RecoveryPosition.Z>47?$"BROKEN BRIDGE  {Math.Max(0,RecoveryPosition.Z-46):0} m":WinchTension>16000?"CABLE UNDER HEAVY LOAD":"";
  Text("Recovery interaction",(_runawayFailed?"R restarts both vehicles at speed":danger+"\n"+RecoveryConnectionHint+" / Space brake / R restart"));GarageActive("Recovery interaction",true);
  if(_runawayFailed||_contractComplete)GarageActive("Recovery marker label",false);
  else if(!_runawayCaught)ProjectRecoveryMarker(physics.TargetHitch+Vector3.UnitY,"RUNAWAY TRUCK");
  else ProjectRecoveryMarker(DeliveryCenter+Vector3.UnitY,"RECOVERY YARD");
 }
}
