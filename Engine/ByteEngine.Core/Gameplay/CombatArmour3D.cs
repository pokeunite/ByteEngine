using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Gameplay;

public enum CombatHitKind { Normal, Armour, WeakPoint }
/// <summary>Reusable armour/weak-point rule. Only the linked, exposed hit region bypasses armour.</summary>
public sealed class CombatArmour3D : Component
{
 public float ArmouredDamageMultiplier {get;set;}=.04f;
 public float WeakPointDamageMultiplier {get;set;}=2;
 [SceneObjectReference] public Guid WeakPointObjectId {get;set;}
 [SceneObjectReference] public Guid StatusTextObjectId {get;set;}
 public AssetReference ArmourSound {get;set;}=AssetReference.Empty;
 public AssetReference WeakPointSound {get;set;}=AssetReference.Empty;
 public float SoundVolume {get;set;}=.35f;
 public string DisplayName {get;set;}="";
 public string ArmouredLabel {get;set;}="ARMOURED - BAIT ITS ATTACK";
 public string ExposedLabel {get;set;}="BACK CORE EXPOSED - SHOOT";
 public string EnragedLabel {get;set;}="ENRAGED - READ THE WARNINGS";
 public float HitFlashSeconds {get;set;}=.07f;
 public bool IsExposed {get;private set;}
 public float EnrageHealthFraction {get;set;}=.35f;
 public float EnrageAttackSpeed {get;set;}=1.25f;
 public bool IsEnraged=>GameObject.GetComponent<HealthComponent>() is {} h&&!h.IsDead&&h.HealthPercent<=EnrageHealthFraction;
 private HealthComponent? _health;
 private float _flash,_soundClock;
 private CombatHitKind _lastKind;
 private readonly List<(MeshRenderer Renderer,Material Original,Material Runtime)> _materials=[];
 public void SetExposed(bool exposed)
 {
  IsExposed=exposed;
  if(GameObject.Scene?.FindGameObject(WeakPointObjectId) is {} core)core.Active=exposed&&GameObject.GetComponent<HealthComponent>()?.IsDead!=true;
 }
 public float ResolveDamage(float amount,bool weakPoint,out CombatHitKind kind)
 {
  bool vulnerable=weakPoint&&IsExposed;kind=vulnerable?CombatHitKind.WeakPoint:CombatHitKind.Armour;
  float multiplier=vulnerable?WeakPointDamageMultiplier:ArmouredDamageMultiplier;
  return amount*(float.IsFinite(multiplier)?Math.Clamp(multiplier,0,10):0);
 }
 protected override void OnStart()
 {
  _health=GameObject.GetComponent<HealthComponent>();if(_health!=null){_health.Damaged+=OnHit;_health.Died+=OnDeath;}
  foreach(var o in GameObject.Scene!.GameObjects)
  {
   bool descendant=false;for(var p=o;p!=null;p=p.Parent)if(p==GameObject){descendant=true;break;}
   if(!descendant||o.Id==WeakPointObjectId)continue;
   foreach(var r in o.Components.OfType<MeshRenderer>()){var original=r.Material;var instance=original.Clone();r.Material=instance;_materials.Add((r,original,instance));}
  }
  SetExposed(false);
 }
 private void OnDeath(HealthComponent h)=>SetExposed(false);
 private void OnHit(HealthComponent h,float damage)
 {
  _lastKind=h.LastHitKind;_flash=Math.Max(.01f,HitFlashSeconds);
  if(_soundClock>0)return;_soundClock=.08f;
  var clip=_lastKind==CombatHitKind.WeakPoint?WeakPointSound:ArmourSound;
  if(clip.IsEmpty||GameObject.Scene is not {} scene)return;
  if(scene.GameObjects.Count(o=>o.Name=="Armour impact voice")>=10)return;
  var voice=scene.CreateGameObject("Armour impact voice");voice.Transform.WorldPosition=Transform.WorldPosition;
  var source=voice.AddComponent(new AudioSource3D{ClipReference=clip,Spatial=false,Volume=SoundVolume,PlayOnStart=false});
  if(AudioRuntimeAssets.TryLoadClip(source,clip)){source.Play();voice.AddComponent(new LifetimeComponent{LifetimeSeconds=Math.Max(.1f,source.DurationSeconds+.1f)});}else scene.DestroyGameObject(voice);
 }
 protected override void OnUpdate()
 {
  _flash=Math.Max(0,_flash-(float)Time.DeltaTime);_soundClock=Math.Max(0,_soundClock-(float)Time.DeltaTime);
  foreach(var (_,original,runtime) in _materials)
  {
   runtime.BaseColor=_flash>0?(_lastKind==CombatHitKind.WeakPoint?new(1,.85f,.2f,1):new(.6f,.65f,.7f,1)):IsEnraged?new(.65f,.035f,.015f,1):original.BaseColor;
  }
  if(GameObject.Scene?.FindGameObject(StatusTextObjectId)?.GetComponent<UiText>() is {} text)
   text.Text=(string.IsNullOrWhiteSpace(DisplayName)?GameObject.Name:DisplayName)+" | "+(IsExposed?ExposedLabel:IsEnraged?EnragedLabel:ArmouredLabel);
 }
 protected override void OnStop()
 {
  if(_health!=null){_health.Damaged-=OnHit;_health.Died-=OnDeath;}_health=null;
  foreach(var (renderer,original,_) in _materials)renderer.Material=original;_materials.Clear();SetExposed(false);
 }
}
public sealed class DamageRegion3D : Component
{
 [SceneObjectReference] public Guid HealthOwnerId {get;set;}
 public void ApplyDamage(float amount,Vector3 direction)
 {var owner=GameObject.Scene?.FindGameObject(HealthOwnerId)??GameObject.Parent;owner?.GetComponent<HealthComponent>()?.DamageHit(amount,direction,GameObject.ActiveInHierarchy);}
}
