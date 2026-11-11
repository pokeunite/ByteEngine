using System.ComponentModel;
using ByteEngine.Core.Gameplay;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningOperation3D
{
 [Category("Expeditions")] public bool ExpeditionsEnabled{get;set;}
 [Category("Expeditions")] public int WavesPerExpedition{get;set;}=6;
 [Category("Expeditions")] public float PreparationSeconds{get;set;}=45;
 [Category("Expeditions")] public float EarlyStartMaximumBonus{get;set;}=.15f;
 [Category("Expeditions")] public float PressureGrowthPerWave{get;set;}=.12f;
 [Category("Expeditions")] public int DisruptorsFromWave{get;set;}=3;
 [Category("Expeditions")] public float DisruptorFraction{get;set;}=.12f;
 [Category("Live state")] public bool ExpeditionActive{get;private set;}
 [Category("Live state")] public int ExpeditionWave{get;private set;}
 [Category("Live state")] public float PreparationRemaining{get;private set;}
 [Category("Live state")] public int PendingEarlyBonus=>ExpeditionActive&&Phase==MiningPhase.Preparation?(int)Math.Floor((expeditionBase?.Ore??0)*Math.Clamp(EarlyStartMaximumBonus,0,.25f)*Math.Clamp(PreparationRemaining/Math.Max(1,PreparationSeconds),0,1)):0;
 [Category("Live state")] public bool CanUseIndustry=>Phase!=MiningPhase.Failed&&!TargetWon&&!TargetExpired;
 [Category("Live state")] public bool Downtime=>Phase==MiningPhase.Preparation&&!ExpeditionActive;
 public string ExpeditionName=>expeditionBase?.Name??"";
 MiningWavePreview? expeditionBase;float expeditionSaveClock;ExpeditionState? pendingExpeditionRestore;int pendingRestoreFrames;
 public bool StartOperation()=>ExpeditionsEnabled?ExpeditionActive?StartWaveEarly():BeginExpedition():StartLegacyOperation();
 bool BeginExpedition()
 {
  Initialize();if(!Downtime)return Reject("Finish the current expedition first.");var wave=SelectedContract;
  if(wave==null)return Reject("Select a drill site.");if(!CanUseIndustry)return false;
  if((long)CargoUsed+wave.Ore>CargoCapacity)return Reject("Make room for the first wave's ore before committing.");
  if(Player?.GetComponent<HealthComponent>()==null||Drill==null||SpawnPoints().Length==0||RaiderBlueprint.IsEmpty||HunterBlueprint.IsEmpty)return Reject("Expedition setup is incomplete.");
  CleanupEnemies();RecoverActors();expeditionBase=wave;ExpeditionActive=true;ExpeditionWave=0;PreparationRemaining=Math.Clamp(PreparationSeconds,5,180);TargetStarted=true;ClearOverclockOffer(false);Message="Contract accepted. Prepare your camp; waves start automatically.";SaveProgress();return true;
 }
 public MiningWavePreview? NextExpeditionWave
 {
  get{if(expeditionBase is not {} b)return null;int number=ExpeditionWave+1,total=Math.Clamp((int)Math.Ceiling(b.Total*(1+Math.Max(0,PressureGrowthPerWave)*(number-1))),1,Math.Clamp(MaximumEnemiesPerRound,1,500));int hunters=(int)Math.Round(total*b.Hunters/(double)Math.Max(1,b.Total));int disruptors=number>=Math.Max(1,DisruptorsFromWave)?Math.Clamp((int)Math.Ceiling((total-hunters)*Math.Clamp(DisruptorFraction,0,.5f)),0,total-hunters):0;return b with{Raiders=total-hunters,Hunters=hunters,Disruptors=disruptors};}
 }
 public bool StartWaveEarly(){if(!ExpeditionActive||Phase!=MiningPhase.Preparation)return false;return LaunchExpeditionWave(PendingEarlyBonus);}
 bool LaunchExpeditionWave(int bonus)
 {
  if(NextExpeditionWave is not {} wave)return false;
  if(SpawnPoints().Length==0){Fail("No spawn points available.");return false;}
  CleanupEnemies();CloseCombatBlockingMenus();ExpeditionWave++;PreparationRemaining=0;ActiveWave=wave with{Ore=Add(wave.Ore,Math.Max(0,bonus))};RoundQuota=ActiveWave.Ore;EnemiesScheduled=wave.Total;EnemiesSpawned=0;ExtractionProgress=0;_spawnClock=0;ResetDirector();Phase=MiningPhase.Drilling;GameObject.GetComponent<MiningTrade3D>()?.ResetReviewedOperation();Message=$"Wave {ExpeditionWave}/{WavesPerExpedition}"+(bonus>0?$" / early start +{bonus} ore":"")+". Machines remain usable.";SaveProgress();return true;
 }
 void CloseCombatBlockingMenus(){foreach(var ui in GameObject.Scene!.GetComponentsSnapshot<MiningBusinessUi3D>())if(ui.Screen is MiningScreen.Drill or MiningScreen.Research or MiningScreen.Account)ui.Close();foreach(var camp in GameObject.Scene.GetComponentsSnapshot<MiningCampLayout3D>())if(camp.IsEditing)camp.ExitEdit();}
 void TickExpedition(float dt)
 {
  if(pendingExpeditionRestore is {} r&&--pendingRestoreFrames<=0){pendingExpeditionRestore=null;Player?.GetComponent<HealthComponent>()?.SetHealthForExpedition(r.PlayerHealth);if(Drill is {} hp)hp.CurrentHealth=Math.Clamp(r.RigHealth,.1f,hp.MaxHealth);foreach(var ammo in r.Ammo??[])GameObject.Scene?.FindGameObject(ammo.Id)?.GetComponent<WeaponInstance3D>()?.SetAmmo(ammo.Magazine,ammo.Reserve);}
  if(!ExpeditionActive)return;if(!CanUseIndustry){ExpeditionActive=false;return;}
  if(Player?.GetComponent<HealthComponent>()?.IsDead==true||Drill?.IsDead==true){Fail("Camp lost during preparation.");return;}
  if(Phase==MiningPhase.Preparation){PreparationRemaining=Math.Max(0,PreparationRemaining-dt);if(PreparationRemaining<=0)LaunchExpeditionWave(0);}
  if((expeditionSaveClock+=dt)>=5){expeditionSaveClock=0;SaveProgress();}
 }
 void FinishExpeditionWave()
 {
  int secured=Math.Min(RoundQuota,Math.Max(0,CargoCapacity-CargoUsed)),lost=RoundQuota-secured;RawOre=Add(RawOre,secured);CompletedRounds++;CleanupEnemies();Phase=MiningPhase.Preparation;
  bool done=ExpeditionWave>=Math.Clamp(WavesPerExpedition,1,12);if(done){ExpeditionActive=false;PreparationRemaining=0;HealAfterOperation();Message=$"Contract complete: {ExpeditionName}. Safe camp / sell, research and rearrange.";}else{PreparationRemaining=Math.Clamp(PreparationSeconds,5,180);Message=$"Wave cleared: +{secured} ore. Next wave in {PreparationRemaining:0}s.";}
  if(lost>0)Message+=$" Cargo overflow: {lost} ore lost.";SaveProgress();
 }
 public sealed class ExpeditionAmmo{public Guid Id{get;set;}public int Magazine{get;set;}public int Reserve{get;set;}}
 public sealed class ExpeditionState{public bool Active{get;set;}public int Wave{get;set;}public bool Combat{get;set;}public float Seconds{get;set;}public MiningWavePreview? Site{get;set;}public float PlayerHealth{get;set;}public float RigHealth{get;set;}public List<ExpeditionAmmo> Ammo{get;set;}=[];}
 ExpeditionState? CaptureExpedition()=>!ExpeditionActive?null:new(){Active=true,Wave=ExpeditionWave,Combat=Phase==MiningPhase.Drilling,Seconds=PreparationRemaining,Site=expeditionBase,PlayerHealth=Player?.GetComponent<HealthComponent>()?.CurrentHealth??1,RigHealth=Drill?.CurrentHealth??1,Ammo=Player?.GetComponent<FpsGunLab3D>()?.CarriedWeapons.Select(w=>new ExpeditionAmmo{Id=w.GameObject.Id,Magazine=w.CurrentAmmo,Reserve=w.ReserveAmmo}).ToList()??[]};
 void RestoreExpedition(ExpeditionState? state){if(!ExpeditionsEnabled||state is not {Active:true,Site:not null}||state.Site.Total<=0||state.Site.Total>500||state.Site.Ore<1||!float.IsFinite(state.Seconds)||!float.IsFinite(state.PlayerHealth)||!float.IsFinite(state.RigHealth))return;expeditionBase=state.Site;ExpeditionActive=true;ExpeditionWave=Math.Clamp(state.Wave-(state.Combat?1:0),0,Math.Clamp(WavesPerExpedition,1,12)-1);PreparationRemaining=state.Combat?5:Math.Clamp(state.Seconds,0,180);pendingExpeditionRestore=state;pendingRestoreFrames=2;Message=state.Combat?"Resuming contract / unpaid wave restarts in 5 seconds.":"Resuming timed preparation.";}
}
internal static class ExpeditionHealthRestore{internal static void SetHealthForExpedition(this HealthComponent h,float amount)=>h.CurrentHealth=Math.Clamp(amount,.1f,h.MaxHealth);}
