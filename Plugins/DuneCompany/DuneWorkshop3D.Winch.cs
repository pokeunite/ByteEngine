using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 public bool WinchMission=>RecoveryJob?.WinchMission==true;
 public bool WinchConnected=>_physics?.WinchConnected==true;
 public float WinchCableLength=>_physics?.CableLength??0;
 public float WinchTension=>_physics?.CableTension??0;
 Vector3 WinchDelivery=>DeliveryCenter;
 bool _winchExtracted,_winchWorkedAtPit;
 readonly List<ByteEngine.Core.Scene.GameObject> _winchArena=[];int _activeWinch=-1;
 public bool AnchorMission=>RecoveryJob?.AnchorMission==true;
 public bool RelayMission=>RecoveryJob?.RelayMission==true;
 public bool NoBrakesMission=>RecoveryJob?.RunawayMission==true;
 int CurrentContract=>DriftMission?5:SprintMission?Math.Clamp(Sprint!.ContractIndex,4,7):NoBrakesMission?3:RelayMission?3:AnchorMission?2:WinchMission?1:0;
 int? _selectedContract;
 int SelectedContract=>_selectedContract??CurrentContract;
 bool SelectedContractWinch=>SelectedContract is >0 and <4;
 static int? _deployContractAfterSwitch;
 static string ContractTitle(int id)=>id switch {7=>"Red Rock Rally",6=>"Dead Straight",5=>"Dust Bowl Drift",4=>"Scrapyard Sprint",3=>"No Brakes",2=>"Down With the Patient",1=>"The Well Ate My Truck",_=>"Stranded in the sand"};
 static int ContractReward(int id)=>id switch {7=>1500,6=>1200,5=>900,4=>1000,3=>1800,2=>850,1=>500,_=>250};
 static string ContractScene(int id)=>id switch {7=>"Scenes/RedRockRally.bytescene",6=>"Scenes/DeadStraight.bytescene",5=>"Scenes/DustBowlDrift.bytescene",4=>"Scenes/ScrapyardSprint.bytescene",3=>"Scenes/AnchorRelay.bytescene",2=>"Scenes/AnchorRescue.bytescene",1=>"Scenes/WinchRescue.bytescene",_=>"Scenes/Workshop.bytescene"};
 public void SelectRecoveryContract(bool winch)=>SelectRecoveryContract(winch?1:0);
 public void SelectRecoveryContract(int id){if(!Building||id<0||id>7)return;_selectedContract=id;_contractPage=id/4;OpenGarageOverlay("Contracts overlay");_contractExpanded=true;RefreshContractBrowser();RefreshGarageState();}
 void BindWinchMissionSwitch(){}
 void ResumeContractDeployment(){if(_deployContractAfterSwitch is not {} id)return;_deployContractAfterSwitch=null;if(id!=CurrentContract){_message="Contract scene did not match the requested job.";return;}_selectedContract=id;DeployGarageContract();}

 void PrepareWinchArena(){
  if(AnchorMission)return;
  for(int i=0;i<8;i++){float angle=i*MathF.PI/4;var point=RecoverySpawn+new Vector3(MathF.Sin(angle)*6,0,MathF.Cos(angle)*6);if(_sand?.Sample(point,out var ground,out _,out _,out _)==true)point.Y=ground.Y;
   var post=RecoveryPrimitive("Pit warning post "+i,point+Vector3.UnitY*.65f,new(.12f,1.3f,.12f),new(.8f,.43f,.1f,1));_winchArena.Add(post);
  }
  foreach(float x in new[]{-5f,5f}){var point=RecoverySpawn+new Vector3(x,0,0);if(_sand?.Sample(point,out var ground,out _,out _,out _)==true)point.Y=ground.Y;var rock=RecoveryPrimitive("Pit rim rock",point+Vector3.UnitY*.35f,new(1,.7f,2),new(.36f,.31f,.24f,1));rock.GetComponent<MeshRenderer>()!.Visible=false;rock.AddComponent(new DuneWorldObstacle3D{Size=Vector3.One});Model(GameObject.Scene!,Assets!,"Assets/ContractLevels/rock_07.obj",rock,"Scanned pit rim stone");_winchArena.Add(rock);}
 }
 void ClearWinchArena(){foreach(var obj in _winchArena)if(obj.Scene!=null)obj.Scene.DestroyGameObject(obj);_winchArena.Clear();}
 public bool ConnectWinch(){if(Building||!_contractRun||!WinchMission||_contractComplete||_physics==null)return false;int id=_blocks.Where(b=>IsWinch(b.Type)).OrderBy(b=>Vector3.DistanceSquared(_physics.WinchPoint(b.Id),_physics.TargetHitch)).Select(b=>b.Id).DefaultIfEmpty(-1).First();if(id<0)return false;bool connected=_physics.ConnectWinch(id);if(connected)_activeWinch=id;return connected;}
 public void DisconnectWinch()=>_physics?.DisconnectWinch();
 void UpdateWinchMission(){
  var physics=_physics!;int id=_blocks.Where(b=>IsWinch(b.Type)).OrderBy(b=>Vector3.DistanceSquared(physics.WinchPoint(b.Id),physics.TargetHitch)).Select(b=>b.Id).DefaultIfEmpty(-1).First();
  if(_truckHitchMarker!=null){_truckHitchMarker.Active=id>=0;if(id>=0)_truckHitchMarker.Transform.WorldPosition=physics.WinchPoint(id);}
  float distance=id>=0?Vector3.Distance(physics.WinchPoint(id),physics.TargetHitch):float.PositiveInfinity;
  UpdateAllWinches();if(NoBrakesMission){UpdateNoBrakes();return;}
  var difference=RecoveryPosition-WinchDelivery;var frame=physics.RecoveryFrame();bool upright=Vector3.Transform(Vector3.UnitY,frame.Rotation).Y>.55f;
  if(!_contractComplete){if(WinchConnected && Vector2.Distance(new(RecoveryPosition.X,RecoveryPosition.Z),new(RecoverySpawn.X,RecoverySpawn.Z))<7)_winchWorkedAtPit=true;if(_winchWorkedAtPit && WinchConnected && Vector2.Distance(new(RecoveryPosition.X,RecoveryPosition.Z),new(RecoverySpawn.X,RecoverySpawn.Z))>7 && upright)_winchExtracted=true;UpdateRelayProgress();bool extracted=_winchExtracted && (!RelayMission||_relayStations.Count>=3) && DeliveryReady(WinchDelivery) && upright && WinchConnected;_deliveryHold=extracted?_deliveryHold+(float)Time.DeltaTime:0;if(_deliveryHold>1){_contractComplete=true;if(!AwardContract()){_contractComplete=false;return;}RecordContractCompletion();AtomicSave(Path.Combine(SaveRoot,"recovery-payment.json"),JsonSerializer.Serialize(_credits));DisconnectWinch();}}
  RecoveryObjective=RelayMission&&!_contractComplete?($"Recover the canyon vehicle / anchors {_relayStations.Count}/3"+(_winchExtracted?" / Return to depot":"")):AnchorMission&&!_contractComplete?(_winchExtracted?"Return the recovered vehicle to the depot.":"Recover the survey vehicle."):_contractComplete?$"PIT RESCUE COMPLETE +${JobPayment} / Balance ${_credits}":WinchConnected?(_winchExtracted?"Bring the recovered buggy back to the depot / stop both vehicles in the delivery bay":"Park on the firm apron / reel the buggy up the pit exit"):"Rescue the buggy from the pit / Connect the winch to its green socket";
  Text("Battle objective",RecoveryObjective);Text("Recovery interaction",_contractComplete?"Paid. Return to garage to restart.":RecoveryConnectionHint+" / R safe reset");GarageActive("Recovery interaction",true);var destination=WinchDelivery;if(WinchConnected&&!_winchExtracted){destination=RecoverySpawn+new Vector3(0,0,-9);if(_sand?.Sample(destination,out var exitGround,out _,out _,out _)==true)destination.Y=exitGround.Y;}if(AnchorMission&&!_winchExtracted){if(Vector2.Distance(new(Transform.WorldPosition.X,Transform.WorldPosition.Z),new(RecoverySpawn.X,RecoverySpawn.Z))>20)ProjectRecoveryMarker(RecoverySpawn+Vector3.UnitY*9,RelayMission?"CANYON RECOVERY":"EXCAVATION");else GarageActive("Recovery marker label",false);}if(!AnchorMission||_winchExtracted)ProjectRecoveryMarker((WinchConnected?destination:physics.TargetHitch)+Vector3.UnitY,WinchConnected?(_winchExtracted?"DEPOT DELIVERY":"PIT EXIT"):"WINCH SOCKET");
 }
}
