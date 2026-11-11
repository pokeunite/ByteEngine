using System.ComponentModel;
using ByteEngine.Core.Gameplay;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningOperation3D
{
 [Category("Overclock")]public bool OverclockEnabled{get;set;}=true;
 [Category("Overclock")]public float OverclockOfferSeconds{get;set;}=12;
 [Category("Overclock")]public float OverclockHoldSeconds{get;set;}=.8f;
 [Category("Overclock")]public float OverclockBonusFraction{get;set;}=.5f;
 [Category("Overclock")]public float OverclockEnemyFraction{get;set;}=.5f;
 [Browsable(false)]public bool IsOverclockActive{get;private set;}
 [Browsable(false)]public float OverclockSecondsLeft{get;private set;}
 [Browsable(false)]public bool OverclockAvailable=>OverclockSecondsLeft>0&&Phase==MiningPhase.Preparation&&!TargetWon&&!TargetExpired;
 [Browsable(false)]public int OverclockBonusOre{get;private set;}
 [Browsable(false)]public float OverclockHoldProgress{get;private set;}
 int securedOverclockRaw,baseOperationQuota;bool overclockHoldArmed;
 void HealAfterOperation(){if(RestoreHealthAfterOperation&&Player?.GetComponent<HealthComponent>() is {IsDead:false} h)h.Heal(h.MaxHealth);}
 void FinishOperationStage()
 {
  if(ExpeditionActive){FinishExpeditionWave();return;}
  RawOre=Add(RawOre,RoundQuota);Phase=MiningPhase.Preparation;CleanupEnemies();
  if(IsOverclockActive){IsOverclockActive=false;RoundQuota=baseOperationQuota;HealAfterOperation();Message=$"Overclock cleared: +{OverclockBonusOre} bonus ore.";}
  else{
   CompletedRounds++;baseOperationQuota=RoundQuota;
   OverclockBonusOre=Math.Max(1,(int)Math.Ceiling(RoundQuota*Math.Clamp(OverclockBonusFraction,.1f,2)));
   bool room=(long)CargoUsed+OverclockBonusOre<=CargoCapacity;
   OverclockSecondsLeft=OverclockEnabled&&!TutorialActive&&room?Math.Clamp(OverclockOfferSeconds,3,60):0;
   OverclockHoldProgress=0;overclockHoldArmed=false;
   if(!OverclockAvailable)HealAfterOperation();
   Message=OverclockAvailable?$"Haul secured. Optional overclock at drill: +{OverclockBonusOre} ore.":$"Operation complete: +{RoundQuota} ore.";
  }
  SaveProgress();
 }
 void TickOverclockOffer(float delta)
 {
  if(!OverclockAvailable)return;
  OverclockSecondsLeft=Math.Max(0,OverclockSecondsLeft-delta);
  if(OverclockSecondsLeft<=0){ClearOverclockOffer();Message="Haul secured. Ready for the next operation.";}
 }
 public void ClearOverclockOffer(bool heal=true){OverclockSecondsLeft=0;OverclockHoldProgress=0;overclockHoldArmed=false;if(heal)HealAfterOperation();}
 public void HoldOverclock(bool held,float delta)
 {
  if(!OverclockAvailable)return;
  if(!held){OverclockHoldProgress=0;overclockHoldArmed=true;return;}
  // Require a fresh release after the wave; a held interaction cannot opt in.
  if(!overclockHoldArmed||!float.IsFinite(delta))return;
  OverclockHoldProgress=Math.Clamp(OverclockHoldProgress+Math.Max(0,delta)/Math.Max(.2f,OverclockHoldSeconds),0,1);
  if(OverclockHoldProgress>=1)StartOverclock();
 }
 public bool StartOverclock()
 {
  if(!OverclockAvailable||ActiveWave is not {} wave||RefiningAmount>0||CargoUsed+OverclockBonusOre>CargoCapacity)return false;
  int count=Math.Clamp((int)Math.Ceiling(wave.Total*Math.Clamp(OverclockEnemyFraction,.1f,2)),1,Math.Clamp(MaximumEnemiesPerRound,1,200));
  int hunters=Math.Clamp((int)Math.Round(count*wave.Hunters/(double)Math.Max(1,wave.Total)),0,count);
  ActiveWave=wave with{Hunters=hunters,Raiders=count-hunters,Ore=OverclockBonusOre,SpawnSeconds=Math.Max(.1f,wave.SpawnSeconds*.8f)};
  securedOverclockRaw=RawOre;IsOverclockActive=true;ClearOverclockOffer(false);
  RoundQuota=OverclockBonusOre;EnemiesScheduled=count;EnemiesSpawned=0;ResetDirector();ExtractionProgress=0;_spawnClock=0;Phase=MiningPhase.Drilling;
  Message=$"OVERCLOCK — +{OverclockBonusOre} ore. Current rig health and ammunition retained.";SaveProgress();return true;
 }
}
