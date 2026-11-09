using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
/// <summary>Bounded presentation feedback. Never changes damage, hitboxes, AI timing or simulation speed.</summary>
public sealed partial class MiningGameFeel3D:Component
{
 [SceneObjectReference]public Guid PlayerObjectId{get;set;}
 [SceneObjectReference]public Guid AmmoBucketObjectId{get;set;}
 [SceneObjectReference]public Guid InterfaceObjectId{get;set;}
 [Category("Feedback")]public bool HitConfirmation{get;set;}=true;
 public bool KillConfirmation{get;set;}=true;
 public bool DirectionalHurt{get;set;}=true;
 public bool RigAttackWarning{get;set;}=true;
 public bool CombatMix{get;set;}=true;
 public bool AmmoGainPopup{get;set;}=true;
 public bool CreditGainPopup{get;set;}=true;
 public bool RoundResults{get;set;}=true;
 [Category("Camera")]public bool CameraImpulses{get;set;}=true;
 public float ShakeIntensity{get;set;}=1;
 public float PistolKickDegrees{get;set;}=.18f;
 public float ShotgunKickDegrees{get;set;}=.45f;
 public float ExplosionKickDegrees{get;set;}=.7f;
 public float MaximumKickDegrees{get;set;}=1;
 [Category("Timing")]public float HitMarkerSeconds{get;set;}=.12f;
 public float KillMarkerSeconds{get;set;}=.23f;
 public float PopupSeconds{get;set;}=2;
 public float RoundResultSeconds{get;set;}=4;
 [Category("Mix")]public float MachineDuckedGain{get;set;}=.55f;
 public float MusicDuckedGain{get;set;}=.72f;
 [Category("Sounds")]public AssetReference HitConfirmSound{get;set;}=AssetReference.Empty;
 public AssetReference KillConfirmSound{get;set;}=AssetReference.Empty;
 public AssetReference RigWarningSound{get;set;}=AssetReference.Empty;
 public float HitConfirmVolume{get;set;}=.10f;
 public float KillConfirmVolume{get;set;}=.16f;
 public float RigWarningVolume{get;set;}=.18f;
 public AssetReference Font{get;set;}=AssetReference.Empty;
 [Browsable(false)]public int ConfirmedHits{get;private set;}
 [Browsable(false)]public int ConfirmedKills{get;private set;}
 [Browsable(false)]public int DisplayedCredits{get;private set;}
 [Browsable(false)]public float MachineGain=>CombatMix?1+(Math.Clamp(MachineDuckedGain,.1f,1)-1)*duck:1;
 [Browsable(false)]public float MusicGain=>CombatMix?1+(Math.Clamp(MusicDuckedGain,.1f,1)-1)*duck:1;
 [Browsable(false)]public int AmmoGain{get;private set;}
 [Browsable(false)]public int CreditGain{get;private set;}
 [Browsable(false)]public string RoundResult{get;private set;}="";
 [Browsable(false)]public bool HitMarkerVisible=>hitClock>0;
 [Browsable(false)]public bool KillMarkerVisible=>killClock>0;
 [Browsable(false)]public bool HurtVisible=>hurtClock>0;
 [Browsable(false)]public float CurrentKickDegrees=>kick;
 [Browsable(false)]public int VisibleRigWarnings=>threats.Count;
 public override int UpdateOrder=>500;
 public string DefenceLossMessage{get;private set;}="";double defenceLossUntil;string defenceLossTitle="",defenceLossDetail="";
 public void NotifyDroneDestroyed(){defenceLossTitle="SECURITY DRONE DESTROYED";defenceLossDetail="BATTERY DEPLETED / REORDER AT FIELD R&D";DefenceLossMessage=defenceLossTitle+" / "+defenceLossDetail;defenceLossUntil=Time.TotalTime+6;}
 public void NotifyDefenceDestroyed(string name,int cost){defenceLossTitle=name.ToUpperInvariant()+" DESTROYED";defenceLossDetail=$"REPLACE: {cost} REFINED ORE";DefenceLossMessage=defenceLossTitle+" / "+defenceLossDetail;defenceLossUntil=Time.TotalTime+5;}
 public override int? RenderOrder=>950;
 MiningOperation3D? Op=>GameObject.GetComponent<MiningOperation3D>();
 MiningBusinessUi3D? Interface=>GameObject.Scene?.FindGameObject(InterfaceObjectId)?.GetComponent<MiningBusinessUi3D>();
 GameObject? Player=>GameObject.Scene?.FindGameObject(PlayerObjectId);
 readonly Dictionary<WeaponInstance3D,int> weapons=[];
 readonly Dictionary<Guid,long> signals=[];
 readonly List<(Vector3 Position,float Age)> threats=[];
 GameObject[] enemies=[];HealthComponent? playerHealth;Camera3D? presentationCamera;
 float scanClock,hitClock,killClock,hurtClock,ammoClock,creditClock,resultClock,duck,duckHold,kick,kickAge,soundClock,rigSoundClock;
 Vector3 hurtDirection;int previousCredits,previousRounds;long collection;float creditDisplay;bool initialized;
 protected override void OnStart()=>Tick(0);
 protected override void OnUpdate()=>Tick(Math.Max(0,(float)Time.DeltaTime));
 public void Tick(float dt)
 {
  if(!float.IsFinite(dt)||dt<0||AttachedGameObject?.Scene is not {} scene||Op is not {} op)return;
  if(!initialized){initialized=true;previousCredits=DisplayedCredits=op.Credits;creditDisplay=op.Credits;previousRounds=op.CompletedRounds;collection=scene.FindGameObject(AmmoBucketObjectId)?.GetComponent<MiningAmmoBucket3D>()?.CollectionSequence??0;}
  hitClock=Math.Max(0,hitClock-dt);killClock=Math.Max(0,killClock-dt);hurtClock=Math.Max(0,hurtClock-dt);ammoClock=Math.Max(0,ammoClock-dt);creditClock=Math.Max(0,creditClock-dt);resultClock=Math.Max(0,resultClock-dt);soundClock-=dt;rigSoundClock-=dt;duckHold=Math.Max(0,duckHold-dt);duck+=( (duckHold>0?1:0)-duck)*(1-MathF.Exp(-dt*(duckHold>0?30:5)));
  kickAge+=dt;kick*=MathF.Exp(-dt*17);if(kick<.001f)kick=0;
  if(Player?.GetComponent<HealthComponent>() is {} hp&&hp!=playerHealth){if(playerHealth!=null)playerHealth.Damaged-=PlayerHurt;playerHealth=hp;hp.Damaged+=PlayerHurt;}
  if(Player?.GetComponent<FpsGunLab3D>() is {} gun)foreach(var weapon in gun.CarriedWeapons)
  {
   if(!weapons.TryGetValue(weapon,out var shots)){weapons[weapon]=weapon.ShotsFired;weapon.DamageConfirmed+=ConfirmDamage;}
   else if(shots!=weapon.ShotsFired){weapons[weapon]=weapon.ShotsFired;AddCameraImpulse(weapon.Definition?.PelletsPerShot>1?ShotgunKickDegrees:PistolKickDegrees);duckHold=Math.Max(duckHold,.09f);}
  }
  if(scene.FindGameObject(AmmoBucketObjectId)?.GetComponent<MiningAmmoBucket3D>() is {} bucket&&bucket.CollectionSequence!=collection){collection=bucket.CollectionSequence;if(bucket.LastCollected>0){AmmoGain=ammoClock>0?AmmoGain+bucket.LastCollected:bucket.LastCollected;ammoClock=PopupSeconds;}}
  if(op.Credits>previousCredits){CreditGain=creditClock>0?CreditGain+op.Credits-previousCredits:op.Credits-previousCredits;creditClock=PopupSeconds;}else if(op.Credits<previousCredits)creditDisplay=op.Credits;
  previousCredits=op.Credits;creditDisplay+=(op.Credits-creditDisplay)*(1-MathF.Exp(-dt*9));DisplayedCredits=creditClock>0?(int)MathF.Round(creditDisplay):op.Credits;
  if(op.CompletedRounds>previousRounds){RoundResult=$"OPERATION {op.CompletedRounds:00} COMPLETE  /  +{op.RoundQuota} RAW ORE  /  RIG {op.DrillHealthFraction:P0}";resultClock=RoundResultSeconds;}previousRounds=op.CompletedRounds;
  scanClock-=dt;if(scanClock<=0){scanClock=.25f;enemies=scene.GameObjects.Where(o=>o.ActiveInHierarchy&&o.GetComponent<MiningRaiderPriorities3D>()!=null&&o.GetComponent<HealthComponent>() is {IsDead:false}).ToArray();}
  TickThreats(dt,op);
 }
 public void ConfirmDamage(CombatDamageReceipt receipt)
 {
  if(!receipt.Enemy||receipt.Amount<=0||!float.IsFinite(receipt.Amount))return;
  ConfirmedHits++;if(HitConfirmation)hitClock=Math.Clamp(HitMarkerSeconds,.04f,.4f);
  if(receipt.Killed){ConfirmedKills++;if(KillConfirmation)killClock=Math.Clamp(KillMarkerSeconds,.06f,.6f);}
  duckHold=Math.Max(duckHold,.13f);
  if(receipt.Killed&&KillConfirmation){PlayFeedback(KillConfirmSound,KillConfirmVolume,1);soundClock=.05f;}
  else if(HitConfirmation&&soundClock<=0){PlayFeedback(HitConfirmSound,HitConfirmVolume,0);soundClock=.065f;}
 }
 void PlayerHurt(HealthComponent health,float amount){if(amount<=0)return;hurtClock=.55f;hurtDirection=-health.LastDamageDirection;duckHold=Math.Max(duckHold,.35f);}
 public void AddCameraImpulse(float degrees)
 {if(!CameraImpulses||!float.IsFinite(degrees)||degrees<=0)return;kick=Math.Min(Math.Clamp(MaximumKickDegrees,0,3),kick+degrees);kickAge=0;}
 public static MiningGameFeel3D? Find(GameObject? context)=>context?.Scene?.GameObjects.SelectMany(o=>o.Components.OfType<MiningGameFeel3D>()).FirstOrDefault(f=>f.Enabled);
 protected override void OnStop()=>Release();
 protected override void OnDestroy()=>Release();
 void Release(){foreach(var weapon in weapons.Keys)weapon.DamageConfirmed-=ConfirmDamage;weapons.Clear();if(playerHealth!=null)playerHealth.Damaged-=PlayerHurt;playerHealth=null;if(presentationCamera!=null)presentationCamera.PresentationRotationOffset=Quaternion.Identity;foreach(var source in feedbackVoices.Values)source.Stop();}
}
