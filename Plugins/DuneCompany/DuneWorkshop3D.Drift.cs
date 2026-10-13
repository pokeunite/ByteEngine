using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 DuneDriftContract3D? _driftContract;bool _driftResolved;
 DuneDriftContract3D? Drift{get{if(!_driftResolved){_driftResolved=true;_driftContract=GameObject.Scene?.GameObjects.SelectMany(o=>o.Components).OfType<DuneDriftContract3D>().FirstOrDefault();}return _driftContract;}}
 bool DriftMission=>Drift!=null;
 DriftScore3D? _driftScore;float _driftRemaining,_driftCountdown;bool _driftFailed;Vector3 _driftVelocity;
 void StartDrift(){if(!DriftMission||!_contractRun)return;_driftScore=GameObject.GetComponent<DriftScore3D>()??GameObject.AddComponent(new DriftScore3D());_driftScore.Reset();_driftRemaining=Math.Clamp(Drift!.Duration,30,300);_driftCountdown=3;_driftFailed=false;_driftVelocity=Vector3.Zero;_raceAudioCount=-1;}
 void DriftControls(ref float throttle,ref float steer,ref bool brake,float dt){if(!DriftMission||!_contractRun)return;if(_driftCountdown>0){int count=(int)Math.Ceiling(_driftCountdown);if(count!=_raceAudioCount){JobSound("race-countdown",.65f);_raceAudioCount=count;}_driftCountdown=Math.Max(0,_driftCountdown-dt);if(_driftCountdown<=0)JobSound("race-go",.8f);}if(_driftCountdown>0||_driftFailed||_contractComplete){throttle=steer=0;brake=true;}}
 void UpdateDrift(){
  if(Building||!_contractRun||_physics==null||_driftScore==null)return;float dt=(float)Time.DeltaTime;
  if(_driftCountdown<=0&&!_driftFailed&&!_contractComplete){
   var velocity=_physics.Velocity;bool grounded=_blocks.Where(b=>_catalog[b.Type].Wheel).Any(b=>_physics.WheelFeedback(b.Id).Grounded);
   var prior=new Vector2(_driftVelocity.X,_driftVelocity.Z);var current=new Vector2(velocity.X,velocity.Z);
   bool collision=_physics.HasPhysicalContact&&prior.Length()-current.Length()>2&&Vector3.Distance(_driftVelocity,velocity)>3;
   float before=_driftScore.BankedScore;_driftScore.Sample(velocity,_physics.Frame.Rotation,grounded,collision,dt);_driftVelocity=velocity;
   if(collision)JobNotice("CHAIN LOST / KEEP GOING");
   if(_driftScore.BankedScore>before){JobSound("race-checkpoint",.55f);JobNotice($"BANKED +{_driftScore.BankedScore-before:0}");}
   _driftRemaining=Math.Max(0,_driftRemaining-dt);
   if(_driftRemaining<=0){_driftScore.Bank();_driftFailed=_driftScore.BankedScore<Drift!.TargetScore;}
   if(_driftScore.BankedScore>=Drift!.TargetScore){_contractComplete=true;if(AwardContract())RecordContractCompletion();else _contractComplete=false;}
  }
  RecoveryObjective=_driftCountdown>0?$"START IN {Math.Ceiling(_driftCountdown)}":_contractComplete?"DRIFT TARGET CLEARED":_driftFailed?"TIME UP / RETRY ["+DriveKey("Recover")+"]":$"DRIFT {_driftScore.BankedScore:0} / {Drift!.TargetScore:0}  /  {_driftRemaining:0}s";
  Text("Battle objective",RecoveryObjective);GarageActive("Recovery interaction",true);GarageActive("Recovery marker label",false);
  Text("Recovery interaction",$"CHAIN +{_driftScore.ChainScore:0}  /  x{_driftScore.Multiplier}  /  FILL {_driftScore.Fill*100:0}%\n{DriveKey("Drift")} handbrake / straighten to bank / {DriveKey("Recover")} retry");
 }
}
