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
 bool? _selectedContractWinch;
 bool SelectedContractWinch=>_selectedContractWinch??WinchMission;
 static bool? _deployContractAfterSwitch;
 public void SelectRecoveryContract(bool winch){if(!Building)return;_selectedContractWinch=winch;OpenGarageOverlay("Contracts overlay");_contractExpanded=true;RefreshContractBrowser();RefreshGarageState();}
 void BindWinchMissionSwitch(){}
 void ResumeContractDeployment(){if(_deployContractAfterSwitch is not {} winch)return;_deployContractAfterSwitch=null;if(winch!=WinchMission){_message="Contract scene did not match the requested job.";return;}_selectedContractWinch=winch;DeployGarageContract();}

 void PrepareWinchArena(){
  for(int i=0;i<8;i++){float angle=i*MathF.PI/4;var point=RecoverySpawn+new Vector3(MathF.Sin(angle)*6,0,MathF.Cos(angle)*6);if(_sand?.Sample(point,out var ground,out _,out _,out _)==true)point.Y=ground.Y;
   var post=RecoveryPrimitive("Pit warning post "+i,point+Vector3.UnitY*.65f,new(.12f,1.3f,.12f),new(.8f,.43f,.1f,1));_winchArena.Add(post);
  }
  foreach(float x in new[]{-5f,5f}){var point=RecoverySpawn+new Vector3(x,0,0);if(_sand?.Sample(point,out var ground,out _,out _,out _)==true)point.Y=ground.Y;var rock=RecoveryPrimitive("Pit rim rock",point+Vector3.UnitY*.35f,new(1,.7f,2),new(.36f,.31f,.24f,1));rock.AddComponent(new DuneWorldObstacle3D{Size=Vector3.One});_winchArena.Add(rock);}
 }
 void ClearWinchArena(){foreach(var obj in _winchArena)if(obj.Scene!=null)obj.Scene.DestroyGameObject(obj);_winchArena.Clear();}
 public bool ConnectWinch(){if(Building||!_contractRun||!WinchMission||_contractComplete||_physics==null)return false;int id=_blocks.Where(b=>b.Type==33).OrderBy(b=>Vector3.DistanceSquared(_physics.WinchPoint(b.Id),_physics.TargetHitch)).Select(b=>b.Id).DefaultIfEmpty(-1).First();if(id<0)return false;bool connected=_physics.ConnectWinch(id);if(connected)_activeWinch=id;return connected;}
 public void DisconnectWinch()=>_physics?.DisconnectWinch();
 void UpdateWinchMission(){
  var physics=_physics!;int id=_blocks.Where(b=>b.Type==33).OrderBy(b=>Vector3.DistanceSquared(physics.WinchPoint(b.Id),physics.TargetHitch)).Select(b=>b.Id).DefaultIfEmpty(-1).First();
  if(_truckHitchMarker!=null){_truckHitchMarker.Active=id>=0;if(id>=0)_truckHitchMarker.Transform.WorldPosition=physics.WinchPoint(id);}
  float distance=id>=0?Vector3.Distance(physics.WinchPoint(id),physics.TargetHitch):float.PositiveInfinity;
  if(!_contractComplete&&Input.IsKeyPressed(Key.H)){if(WinchConnected)DisconnectWinch();else if(!ConnectWinch())_message="Cannot attach: stop within 12 m, with cable clear of sand.";}
  physics.SetWinchInput(!_contractComplete&&WinchConnected?(Input.IsKeyDown(Key.E)?1:Input.IsKeyDown(Key.Q)?-1:0):0);
  RecoveryConnectionHint=WinchConnected?$"H detach / Hold E reel in / Q pay out / {WinchCableLength:0.0} m / {WinchTension/1000:0.0} kN":id<0?"Fit a powered winch in the garage":distance>12?"Bring the winch within 12 m of the green socket":physics.CanConnectWinch(id)?"H attach cable / green socket ready":"Stop and reposition: cable must clear the sand";
  if(_towLink!=null){_towLink.Active=WinchConnected;if(WinchConnected){var a=physics.WinchPoint(_activeWinch);var b=physics.TargetHitch;var delta=b-a;_towLink.Transform.WorldPosition=(a+b)*.5f;_towLink.Transform.WorldRotation=FromTo(Vector3.UnitZ,Vector3.Normalize(delta));_towLink.Transform.LocalScale=new(.035f,.035f,delta.Length());}}
  var difference=RecoveryPosition-WinchDelivery;var frame=physics.RecoveryFrame();bool upright=Vector3.Transform(Vector3.UnitY,frame.Rotation).Y>.55f;
  if(!_contractComplete){if(WinchConnected && Vector2.Distance(new(RecoveryPosition.X,RecoveryPosition.Z),new(RecoverySpawn.X,RecoverySpawn.Z))<7)_winchWorkedAtPit=true;if(_winchWorkedAtPit && WinchConnected && Vector2.Distance(new(RecoveryPosition.X,RecoveryPosition.Z),new(RecoverySpawn.X,RecoverySpawn.Z))>7 && upright)_winchExtracted=true;bool extracted=_winchExtracted && DeliveryReady(WinchDelivery) && upright && WinchConnected;_deliveryHold=extracted?_deliveryHold+(float)Time.DeltaTime:0;if(_deliveryHold>1){_contractComplete=true;RecordContractCompletion();_credits+=JobPayment;AtomicSave(Path.Combine(SaveRoot,"recovery-payment.json"),JsonSerializer.Serialize(_credits));DisconnectWinch();}}
  RecoveryObjective=_contractComplete?$"PIT RESCUE COMPLETE +${JobPayment} / Balance ${_credits}":WinchConnected?(_winchExtracted?"Bring the recovered buggy back to the depot / stop both vehicles in the delivery bay":"Park on the firm apron / reel the buggy up the pit exit"):"Rescue the buggy from the pit / Connect the winch to its green socket";
  Text("Battle objective",RecoveryObjective);Text("Recovery interaction",_contractComplete?"Paid. Return to garage to restart.":RecoveryConnectionHint+" / R safe reset");GarageActive("Recovery interaction",true);var destination=WinchDelivery;if(WinchConnected&&!_winchExtracted){destination=RecoverySpawn+new Vector3(0,0,-9);if(_sand?.Sample(destination,out var exitGround,out _,out _,out _)==true)destination.Y=exitGround.Y;}ProjectRecoveryMarker((WinchConnected?destination:physics.TargetHitch)+Vector3.UnitY,WinchConnected?(_winchExtracted?"DEPOT DELIVERY":"PIT EXIT"):"WINCH SOCKET");
 }
}
