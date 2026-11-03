using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
public enum MiningSoundCue{DryTrigger,AmmoClink,AlienGrowl,MetalHit,BodyHit,PlayerHurt,Confirm,Notification,Alert,Error,Credits,Explosion,Cannon,DefenceShot,RefineryOpen,PanelClose,RefineryOreImpact,PressOpen,PressStroke,PressBucketImpact,RepeaterShot,TerminalOpen,TerminalSelect,TerminalPurchase,Footstep,AmmoPickup,SpiderHit,SpiderDeath,InterfaceHover,DrillDamage}
/// <summary>Authored audio bindings and bounded voices for mining state transitions. Reload audio stays in the existing weapon Event Sheet.</summary>
public sealed partial class MiningAudio3D:Component
{
 [SceneObjectReference] public Guid OperationObjectId{get;set;}
 [SceneObjectReference] public Guid WorkshopObjectId{get;set;}
 [SceneObjectReference] public Guid PressObjectId{get;set;}
 [SceneObjectReference] public Guid RefineryObjectId{get;set;}
 [Category("Weapon sounds")] public AssetReference PistolShot{get;set;}=AssetReference.Empty;
 public AssetReference DryTrigger{get;set;}=AssetReference.Empty;
 public AssetReference AmmoClink{get;set;}=AssetReference.Empty;
 [Category("Creature sounds")] public AssetReference AlienGrowl{get;set;}=AssetReference.Empty;
 public AssetReference SpiderHitSound{get;set;}=AssetReference.Empty;
 public AssetReference SpiderDeathSound{get;set;}=AssetReference.Empty;
 public float SpiderHitVolume{get;set;}=.65f;
 public float SpiderDeathVolume{get;set;}=.85f;
 public AssetReference BodyHit{get;set;}=AssetReference.Empty;
 public AssetReference BodyHit2{get;set;}=AssetReference.Empty;
 public AssetReference BodyHit3{get;set;}=AssetReference.Empty;
 public AssetReference MetalHit{get;set;}=AssetReference.Empty;
 public AssetReference PlayerHurt{get;set;}=AssetReference.Empty;
 [Category("Machine loops")] public AssetReference DrillLoop{get;set;}=AssetReference.Empty;
 public AssetReference RefineryLoop{get;set;}=AssetReference.Empty;
 public AssetReference PressLoop{get;set;}=AssetReference.Empty;
 public AssetReference CriticalAlarm{get;set;}=AssetReference.Empty;
 [Category("Interface sounds")] public AssetReference Confirm{get;set;}=AssetReference.Empty;
 public AssetReference Notification{get;set;}=AssetReference.Empty;
 public AssetReference Alert{get;set;}=AssetReference.Empty;
 public AssetReference Error{get;set;}=AssetReference.Empty;
 public AssetReference Credits{get;set;}=AssetReference.Empty;
 [Category("Defence sounds")] public AssetReference Explosion{get;set;}=AssetReference.Empty;
 public AssetReference Cannon{get;set;}=AssetReference.Empty;
 [Category("Credits")] public string RequiredAttribution{get;set;}="SFX_Impact_Bullet_Hit_Body_1\nMarcus Dellicompagni (PoundSoundUK)\nSource: freesound.org/people/PoundSoundUK/sounds/719198/\nLicence: CC BY 4.0 - creativecommons.org/licenses/by/4.0/\nEdited: shortened impact variations, mono conversion and gain adjustment.";
 [Category("Mix")] public float CombatVolume{get;set;}=.55f;
 public float MachineVolume{get;set;}=.18f;
 public float DrillVolumeMultiplier{get;set;}=2.5f;
 public float PressVolumeMultiplier{get;set;}=3f;
 public float RefineryVolumeMultiplier{get;set;}=3f;
 public float MachineHearingDistance{get;set;}=8;
 public float MachineRolloff{get;set;}=.7f;
 public float InterfaceVolume{get;set;}=.58f;
 public float HoverVolume{get;set;}=.32f;
 public AssetReference DrillDamageSound{get;set;}=AssetReference.Empty;
 public float DrillDamageVolume{get;set;}=.9f;
 public float CreatureHearingDistance{get;set;}=8;
 public float CreatureRolloff{get;set;}=.55f;
 double feedbackFocusUntil;
 public float FeedbackMachineGain=>Math.Min(GameObject.GetComponent<MiningGameFeel3D>()?.MachineGain??1,Time.TotalTime<feedbackFocusUntil?.5f:1);
 public float FeedbackMusicGain=>Math.Min(GameObject.GetComponent<MiningGameFeel3D>()?.MusicGain??1,Time.TotalTime<feedbackFocusUntil?.78f:1);
 public float AlarmVolume{get;set;}=.28f;
 public float CriticalDrillFraction{get;set;}=.3f;
 public int MaximumOneShotVoices{get;set;}=16;
 public bool AutomaticCues{get;set;}=true;
 public int PlayedCues{get;private set;}
 public int PooledVoices=>voices.Count;
 public int LoopCount=>loops.Count;
 public string LastCue{get;private set;}="";
 public string LastError{get;private set;}="";
 public float LastCueVolume{get;private set;}
 public int LastCuePriority{get;private set;}
 public AudioBus LastCueBus{get;private set;}
 readonly List<(MiningSoundCue Cue,AudioSource3D Source)> voices=[];
 readonly List<GameObject> staleObserved=[];
 readonly List<AudioSource3D> staleGrowls=[];
 readonly Dictionary<string,AudioSource3D> loops=[];
 readonly Dictionary<MiningSoundCue,double> deadlines=[];
 readonly Dictionary<GameObject,(HealthComponent Health,Action<HealthComponent,float> Handler)> observed=[];
 readonly Dictionary<GameObject,string> phases=[];
 readonly Dictionary<WeaponInstance3D,Action<RaycastHit3D>> weaponHits=[];
 readonly Dictionary<AudioSource3D,GameObject> growlOwners=[];
 bool stateReady;
 readonly Random random=new(1597);
 MiningPhase? previousPhase;bool refining,shipping;float previousAmmo,ejectCredit;long collectionSequence;bool playerCritical;
 MiningOperation3D? Op=>GameObject.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();
 MiningTrade3D? Trade=>GameObject.Scene?.FindGameObject(WorkshopObjectId)?.GetComponent<MiningTrade3D>();
 public static MiningAudio3D? Find(GameObject? context){if(context?.Scene is not {} scene)return null;for(int i=0;i<scene.GameObjects.Count;i++){var o=scene.GameObjects[i];if(!o.ActiveInHierarchy)continue;for(int j=0;j<o.Components.Count;j++)if(o.Components[j] is MiningAudio3D {Enabled:true} a)return a;}return null;}
 AssetReference ChooseClip(AssetReference a,AssetReference b,AssetReference c,AssetReference? d=null,int sequence=-1)
 {
  int count=(!a.IsEmpty?1:0)+(!b.IsEmpty?1:0)+(!c.IsEmpty?1:0)+(d is {IsEmpty:false}?1:0);if(count==0)return AssetReference.Empty;
  int index=sequence<0?random.Next(count):(sequence&int.MaxValue)%count;
  if(!a.IsEmpty&&index--==0)return a;if(!b.IsEmpty&&index--==0)return b;if(!c.IsEmpty&&index--==0)return c;return d??AssetReference.Empty;
 }
 AssetReference Clip(MiningSoundCue cue)=>cue switch
 {
  MiningSoundCue.InterfaceHover=>TerminalSelect,MiningSoundCue.DrillDamage=>DrillDamageSound.IsEmpty?MetalHit:DrillDamageSound,MiningSoundCue.Footstep=>StepClip(),MiningSoundCue.AmmoPickup=>AmmoPickupSound,MiningSoundCue.DryTrigger=>DryTrigger,MiningSoundCue.AmmoClink=>AmmoClink,MiningSoundCue.AlienGrowl=>AlienGrowl,MiningSoundCue.MetalHit=>MetalHit,
  MiningSoundCue.SpiderHit=>SpiderHitSound.IsEmpty?BodyHit:SpiderHitSound,MiningSoundCue.SpiderDeath=>SpiderDeathSound.IsEmpty?AlienGrowl:SpiderDeathSound,
  MiningSoundCue.BodyHit=>ChooseClip(BodyHit,BodyHit2,BodyHit3),
  MiningSoundCue.PlayerHurt=>PlayerHurt,MiningSoundCue.Confirm=>Confirm,MiningSoundCue.Notification=>Notification,MiningSoundCue.Alert=>Alert,MiningSoundCue.Error=>Error,MiningSoundCue.Credits=>Credits,MiningSoundCue.Explosion=>Explosion,MiningSoundCue.Cannon=>Cannon,
  MiningSoundCue.RefineryOpen=>RefineryMenuOpen,MiningSoundCue.PanelClose=>MenuClose,MiningSoundCue.RefineryOreImpact=>OreImpactClip(),MiningSoundCue.PressOpen=>PressMenuOpen,MiningSoundCue.PressStroke=>PressStroke,MiningSoundCue.PressBucketImpact=>PressImpactClip(),MiningSoundCue.DefenceShot=>TurretShot.IsEmpty?PistolShot:TurretShot,MiningSoundCue.RepeaterShot=>RepeaterShot.IsEmpty?PistolShot:RepeaterShot,MiningSoundCue.TerminalOpen=>TerminalOpen,MiningSoundCue.TerminalSelect=>TerminalSelect,MiningSoundCue.TerminalPurchase=>TerminalPurchase,_=>PistolShot
 };
 public bool PlayCue(MiningSoundCue cue,Vector3? position=null,GameObject? owner=null)=>PlayCueWithGain(cue,position,owner,1);
 public bool PlayHoverCue(float gain=1)=>PlayCueWithGain(MiningSoundCue.InterfaceHover,null,null,gain);
 bool PlayCueWithGain(MiningSoundCue cue,Vector3? position,GameObject? owner,float gain)
 {
  if(!Enum.IsDefined(cue)||AttachedGameObject?.Scene==null)return false;
  double now=Time.TotalTime;if(deadlines.TryGetValue(cue,out var until)&&now<until)return false;
  var clip=Clip(cue);if(clip.IsEmpty)return false;
  float gap=cue switch{MiningSoundCue.InterfaceHover=>.055f,MiningSoundCue.DrillDamage=>.2f,MiningSoundCue.SpiderHit=>.08f,MiningSoundCue.SpiderDeath=>.08f,MiningSoundCue.Footstep=>.15f,MiningSoundCue.AmmoPickup=>.8f,MiningSoundCue.AlienGrowl=>.8f,MiningSoundCue.PlayerHurt=>.3f,MiningSoundCue.DryTrigger=>.25f,MiningSoundCue.AmmoClink=>.18f,MiningSoundCue.MetalHit=>.12f,MiningSoundCue.BodyHit=>.065f,MiningSoundCue.DefenceShot or MiningSoundCue.RepeaterShot=>.045f,_=>.10f};
  bool spatial=cue is MiningSoundCue.DrillDamage or MiningSoundCue.SpiderHit or MiningSoundCue.SpiderDeath or MiningSoundCue.AlienGrowl or MiningSoundCue.MetalHit or MiningSoundCue.BodyHit or MiningSoundCue.Explosion or MiningSoundCue.Cannon or MiningSoundCue.AmmoClink or MiningSoundCue.DefenceShot or MiningSoundCue.RepeaterShot or MiningSoundCue.RefineryOreImpact or MiningSoundCue.PressStroke or MiningSoundCue.PressBucketImpact;
  int priority=cue is MiningSoundCue.PlayerHurt or MiningSoundCue.Alert?15:cue==MiningSoundCue.DrillDamage?14:cue==MiningSoundCue.SpiderDeath?10:cue==MiningSoundCue.SpiderHit?8:cue==MiningSoundCue.InterfaceHover?9:spatial?3:14;
  int sameCount=0;AudioSource3D? source=null;for(int i=0;i<voices.Count;i++){var v=voices[i];if(v.Cue!=cue)continue;sameCount++;if(source==null&&!v.Source.IsPlaying)source=v.Source;}
  if(source==null&&sameCount>=(cue is MiningSoundCue.SpiderHit?6:cue is MiningSoundCue.SpiderDeath?4:cue is MiningSoundCue.BodyHit or MiningSoundCue.DefenceShot or MiningSoundCue.RepeaterShot?4:cue is MiningSoundCue.MetalHit or MiningSoundCue.RefineryOreImpact or MiningSoundCue.PressBucketImpact?3:2))return false;
  if(source==null)
  {
   if(voices.Count>=Math.Clamp(MaximumOneShotVoices,4,32))
   {
    int index=voices.FindIndex(v=>!v.Source.IsPlaying);if(index<0)index=voices.FindIndex(v=>v.Source.Priority<priority);if(index<0)return false;
    source=voices[index].Source;source.Stop();voices[index]=(cue,source);
   }
   else{var o=GameObject.Scene!.CreateGameObject("Mining audio voice - "+cue);o.SetParent(GameObject,false);source=o.AddComponent(new AudioSource3D());voices.Add((cue,source));}
  }
  growlOwners.Remove(source);if(cue==MiningSoundCue.AlienGrowl&&owner!=null)growlOwners[source]=owner;
  source.Spatial=spatial;source.Bus=spatial||cue is MiningSoundCue.PlayerHurt or MiningSoundCue.DryTrigger?AudioBus.Sfx:AudioBus.Ui;source.Priority=priority;source.MinDistance=3;source.MaxDistance=32;source.RolloffFactor=1.2f;
  source.Volume=(spatial||cue is MiningSoundCue.PlayerHurt or MiningSoundCue.DryTrigger?CombatVolume:InterfaceVolume)*(cue==MiningSoundCue.AmmoClink?.22f:cue==MiningSoundCue.AlienGrowl?.65f:1);
  if(cue==MiningSoundCue.RefineryOreImpact){source.Volume=Math.Clamp(RefineryOreVolume,0,1);source.MinDistance=Math.Clamp(MachineHearingDistance,1,20);source.RolloffFactor=Math.Clamp(MachineRolloff,.1f,3);}
  if(cue is MiningSoundCue.PressStroke or MiningSoundCue.PressBucketImpact){source.Volume=Math.Clamp(cue==MiningSoundCue.PressStroke?PressStrokeVolume:PressBucketVolume,0,1);source.MinDistance=Math.Clamp(MachineHearingDistance,1,20);source.RolloffFactor=Math.Clamp(MachineRolloff,.1f,3);}
  if(cue is MiningSoundCue.DefenceShot or MiningSoundCue.RepeaterShot or MiningSoundCue.Explosion or MiningSoundCue.Cannon){source.Volume=Math.Clamp(cue==MiningSoundCue.Explosion?ExplosionVolume:cue==MiningSoundCue.Cannon?MortarLaunchVolume:DefenceVolume,0,1);source.MinDistance=Math.Clamp(DefenceHearingDistance,3,30);source.MaxDistance=90;source.RolloffFactor=.65f;source.Priority=cue==MiningSoundCue.Explosion?12:6;}
  if(cue is MiningSoundCue.SpiderHit or MiningSoundCue.SpiderDeath){source.Volume=Math.Clamp(cue==MiningSoundCue.SpiderHit?SpiderHitVolume:SpiderDeathVolume,0,1);source.MinDistance=Math.Clamp(CreatureHearingDistance,1,16);source.MaxDistance=50;source.RolloffFactor=Math.Clamp(CreatureRolloff,.1f,2);source.Priority=cue==MiningSoundCue.SpiderDeath?10:8;}
  if(cue==MiningSoundCue.InterfaceHover)source.Volume=Math.Clamp(HoverVolume,0,1);
  if(cue==MiningSoundCue.DrillDamage){source.Volume=Math.Clamp(DrillDamageVolume,0,1);source.MinDistance=8;source.MaxDistance=60;source.RolloffFactor=.55f;source.Priority=14;}
  source.Pitch=cue is MiningSoundCue.BodyHit or MiningSoundCue.AmmoClink or MiningSoundCue.AlienGrowl?.96f+(float)random.NextDouble()*.08f:1;
  if(cue==MiningSoundCue.Footstep){source.Volume=FootstepVolume;source.Bus=AudioBus.Sfx;source.Pitch=.94f+(float)random.NextDouble()*.12f;}
  if(cue==MiningSoundCue.AmmoPickup){source.Volume=AmmoPickupVolume;source.Bus=AudioBus.Sfx;source.Pitch=AmmoPickupPitch;}
  if(cue==MiningSoundCue.RefineryOreImpact)source.Pitch=.94f+(float)random.NextDouble()*.12f;
  if(cue==MiningSoundCue.PressBucketImpact)source.Pitch=.94f+(float)random.NextDouble()*.12f;
  source.GameObject.Transform.WorldPosition=position??Transform.WorldPosition;
  if(source.ClipReference!=clip||!source.ClipLoaded){source.Stop();if(!AudioRuntimeAssets.TryLoadClip(source,clip,m=>LastError=m))return false;}
  if(cue is MiningSoundCue.RefineryOreImpact or MiningSoundCue.PressStroke or MiningSoundCue.PressBucketImpact)source.Volume*=FeedbackMachineGain;
  source.Volume*=Math.Clamp(float.IsFinite(gain)?gain:1,0,1);
  source.Play();if(!source.IsPlaying){LastError=source.BackendStatus;return false;}
  if(cue is MiningSoundCue.SpiderHit or MiningSoundCue.SpiderDeath or MiningSoundCue.DrillDamage)feedbackFocusUntil=Time.TotalTime+.3;
  deadlines[cue]=now+gap;PlayedCues++;LastCue=cue.ToString();LastCueVolume=source.Volume;LastCuePriority=source.Priority;LastCueBus=source.Bus;LastError="";return true;
 }
 void SetLoop(string name,AssetReference clip,GameObject? owner,bool running,float volume,float dt,bool spatial=true,Vector3? position=null)
 {
  if(clip.IsEmpty||owner?.Scene==null){if(loops.TryGetValue(name,out var silent))silent.Stop();return;}
  if(!loops.TryGetValue(name,out var source))
  {
   var o=GameObject.Scene!.CreateGameObject("Mining loop - "+name);o.SetParent(GameObject,false);source=o.AddComponent(new AudioSource3D{ClipReference=clip,Loop=true,Spatial=spatial,Volume=0,MinDistance=4,MaxDistance=28,RolloffFactor=1.2f,Priority=spatial?1:20,Bus=AudioBus.Sfx});loops[name]=source;
   if(!AudioRuntimeAssets.TryLoadClip(source,clip,m=>LastError=m))return;
  }
  if(source.ClipReference!=clip){source.Stop();if(!AudioRuntimeAssets.TryLoadClip(source,clip,m=>LastError=m))return;}
  source.MinDistance=Math.Clamp(MachineHearingDistance,1,20);source.RolloffFactor=Math.Clamp(MachineRolloff,.1f,3);
  source.GameObject.Transform.WorldPosition=position??owner.Transform.WorldPosition+Vector3.UnitY;float target=running?volume*(name=="Critical drill alarm"?1:FeedbackMachineGain):0;source.Volume+=(target-source.Volume)*Math.Clamp(dt*8,0,1);
  if(running&&!source.IsPlaying)source.Play();else if(!running&&source.Volume<.002f)source.Stop();
 }
 void Observe(GameObject? owner,MiningSoundCue cue)
 {
  if(owner?.GetComponent<HealthComponent>() is not {} hp||observed.ContainsKey(owner))return;
  Action<HealthComponent,float> handler=(h,amount)=>{if(amount>0)PlayCue(cue,owner.Transform.WorldPosition+Vector3.UnitY*.8f);};hp.Damaged+=handler;observed[owner]=(hp,handler);
 }
 protected override void OnUpdate()=>Tick((float)Time.DeltaTime);
 public void Tick(float dt)
 {
  TickMusic((float)Time.UnscaledDeltaTime);
  if(!AutomaticCues){StopAutomatic();return;}
  if(!float.IsFinite(dt)||dt<=0||Op is not {} op||Trade is not {} trade)return;
  if(!stateReady){stateReady=true;previousAmmo=trade.StoredAmmo;previousPhase=op.Phase;refining=op.RefiningAmount>0;shipping=trade.SaleInProgress;}
  staleGrowls.Clear();foreach(var p in growlOwners)if(p.Value.Scene!=GameObject.Scene||p.Value.GetComponent<HealthComponent>()?.IsDead==true)staleGrowls.Add(p.Key);foreach(var source in staleGrowls){source.Stop();growlOwners.Remove(source);}
  var scene=GameObject.Scene!;var player=scene.FindGameObject(op.PlayerObjectId);var drill=scene.FindGameObject(op.DrillObjectId);var press=scene.FindGameObject(PressObjectId);var refinery=scene.FindGameObject(RefineryObjectId);
  TickDrillLoop(drill,player,op.Phase==MiningPhase.Drilling,dt);
  SetLoop("Refinery",RefineryLoop,refinery,op.RefiningAmount>0,MachineVolume*RefineryVolumeMultiplier,dt);
  TickRefineryLayers(refinery,op,dt);
  SetLoop("Press",PressLoop,press,op.Phase!=MiningPhase.Failed&&trade.StoredAmmo<trade.Capacity,MachineVolume*PressVolumeMultiplier,dt);
  TickPressLayers(press,op,trade,dt);
  SetLoop("Critical drill alarm",CriticalAlarm,drill,op.Phase==MiningPhase.Drilling&&op.DrillHealthFraction>0&&op.DrillHealthFraction<=CriticalDrillFraction,AlarmVolume,dt,false);
  if(previousPhase.HasValue&&previousPhase!=op.Phase)PlayCue(op.Phase==MiningPhase.Preparation?MiningSoundCue.Notification:MiningSoundCue.Alert);
  previousPhase=op.Phase;
  if(refining&&op.RefiningAmount==0&&op.Phase==MiningPhase.Preparation)PlayCue(MiningSoundCue.Notification);refining=op.RefiningAmount>0;
  if(shipping&&!trade.SaleInProgress)PlayCue(MiningSoundCue.Credits);shipping=trade.SaleInProgress;
  ejectCredit+=Math.Max(0,trade.StoredAmmo-previousAmmo);previousAmmo=trade.StoredAmmo;
  if(ejectCredit>=1){if(PressBucketImpact1.IsEmpty&&PlayCue(MiningSoundCue.AmmoClink,press?.Transform.WorldPosition))ejectCredit-=MathF.Floor(ejectCredit);else if(!PressBucketImpact1.IsEmpty)ejectCredit=0;ejectCredit=Math.Min(ejectCredit,2);}
  if(press?.GetComponent<MiningAmmoBucket3D>() is {} bucket&&bucket.CollectionSequence!=collectionSequence){collectionSequence=bucket.CollectionSequence;PlayCue(MiningSoundCue.AmmoPickup);}
  if(player?.GetComponent<FpsGunLab3D>()?.SelectedWeapon is {CurrentAmmo:<=0,Reloading:false}&&Input.IsGameInputCaptured&&!Input.IsUiPointerRequested&&Input.IsMouseButtonPressed(MouseButton.Left)&&player.GetComponent<MiningDefensePlanner3D>()?.SuppressWeaponFire!=true)PlayCue(MiningSoundCue.DryTrigger);
  if(player?.GetComponent<FpsGunLab3D>()?.SelectedWeapon is {} weapon&&!weaponHits.ContainsKey(weapon))
  {
   Action<RaycastHit3D> hit=h=>{GameObject? target=h.GameObject;while(target!=null){if(target.GetComponent<HealthComponent>()!=null)return;target=target.Parent;}PlayCue(MiningSoundCue.MetalHit,h.Point);};weapon.Hit+=hit;weaponHits[weapon]=hit;
  }
  TickFootsteps(player,dt);
  Observe(player,MiningSoundCue.PlayerHurt);Observe(drill,MiningSoundCue.DrillDamage);
  bool critical=player?.GetComponent<HealthComponent>() is {IsDead:false,HealthPercent:<.25f};if(critical&&!playerCritical)PlayCue(MiningSoundCue.Alert);playerCritical=critical;
  foreach(var enemy in op.SpawnedEnemies)
  {
   if(enemy.Scene!=scene||enemy.GetComponent<HealthComponent>() is not {IsDead:false})continue;
   if(enemy.GetComponent<MiningSpiderAnimation3D>()==null)Observe(enemy,MiningSoundCue.BodyHit);string phase=enemy.GetComponent<SimpleEnemyAI3D>()?.AttackPhase??"";if(phase=="Windup"&&phases.GetValueOrDefault(enemy)!="Windup")PlayCue(MiningSoundCue.AlienGrowl,enemy.Transform.WorldPosition,enemy);phases[enemy]=phase;
  }
  staleObserved.Clear();foreach(var item in observed)if(item.Key.Scene!=scene)staleObserved.Add(item.Key);foreach(var key in staleObserved){var item=observed[key];item.Health.Damaged-=item.Handler;observed.Remove(key);phases.Remove(key);}
 }
 void StopAutomatic()
 {
  pressWasRunning=false;
  foreach(var loop in loops.Values)loop.Stop();
  foreach(var pair in observed.Values)pair.Health.Damaged-=pair.Handler;observed.Clear();phases.Clear();
  foreach(var p in weaponHits)p.Key.Hit-=p.Value;weaponHits.Clear();stateReady=false;
 }
 void StopAll()
 {
  battleMusicSource?.Stop();
  campMusicSource?.Stop();
  StopAutomatic();deadlines.Clear();
  foreach(var pair in observed.Values)pair.Health.Damaged-=pair.Handler;observed.Clear();phases.Clear();
  foreach(var p in weaponHits)p.Key.Hit-=p.Value;weaponHits.Clear();growlOwners.Clear();
  foreach(var voice in voices)voice.Source.Stop();foreach(var loop in loops.Values)loop.Stop();
 }
 protected override void OnStop()=>StopAll();
 protected override void OnDestroy()=>StopAll();
}
