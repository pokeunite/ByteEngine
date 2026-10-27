using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
public enum MiningEffectCue{TurretMuzzle,Tracer,MineExplosion,MortarExplosion,MachineStamp,MaterialLanding}
/// <summary>Editable VFX assets with a scene-owned warm, bounded pool. No per-particle objects.</summary>
public sealed class MiningEffects3D:Component
{
 public AssetReference TurretMuzzle{get;set;}=AssetReference.Empty;
 public AssetReference Tracer{get;set;}=AssetReference.Empty;
 public AssetReference MineExplosion{get;set;}=AssetReference.Empty;
 public AssetReference MortarExplosion{get;set;}=AssetReference.Empty;
 public AssetReference MachineStamp{get;set;}=AssetReference.Empty;
 public AssetReference MaterialLanding{get;set;}=AssetReference.Empty;
 public AssetReference MortarTrail{get;set;}=AssetReference.Empty;
 public int MaximumTransientEffects{get;set;}=40;
 public float TurretFlashScale{get;set;}=.75f;
 public float MineExplosionScale{get;set;}=1.8f;
 public float MortarExplosionScale{get;set;}=2.3f;
 public float ExplosionLaunchSpeed{get;set;}=7;
 public float ExplosionLift{get;set;}=3.5f;
 public bool ExplosionKnockdowns{get;set;}=true;
 [Browsable(false)] public int ActiveEffects=>pool.Count(p=>p.Player.GameObject.Active);
 [Browsable(false)] public int PooledEffects=>pool.Count;
 [Browsable(false)] public int EmittedEffects{get;private set;}
 [Browsable(false)] public int DroppedEffects{get;private set;}
 [Browsable(false)] public string LastError{get;private set;}="";
 readonly List<(MiningEffectCue Cue,VfxPlayer Player)> pool=[];
 public static MiningEffects3D? Find(GameObject? context)=>context?.Scene?.GameObjects.SelectMany(o=>o.Components.OfType<MiningEffects3D>()).FirstOrDefault(c=>c.Enabled);
 AssetReference Asset(MiningEffectCue cue)=>cue switch{MiningEffectCue.TurretMuzzle=>TurretMuzzle,MiningEffectCue.Tracer=>Tracer,MiningEffectCue.MineExplosion=>MineExplosion,MiningEffectCue.MortarExplosion=>MortarExplosion,MiningEffectCue.MachineStamp=>MachineStamp,_=>MaterialLanding};
 VfxPlayer? Create(MiningEffectCue cue)
 {
  var asset=Asset(cue);if(asset.IsEmpty||GameObject.Scene==null||pool.Count>=Math.Clamp(MaximumTransientEffects,8,64))return null;
  var o=GameObject.Scene.CreateGameObject("Mining VFX - "+cue);var p=o.AddComponent(new VfxPlayer{Effect=asset,PlayOnStart=false,ViewDistance=100,QualityDistance=30});
  p.Prewarm();if(p.ParticleCapacity==0){LastError=p.Status;GameObject.Scene.DestroyGameObject(o);return null;}o.Active=false;pool.Add((cue,p));return p;
 }
 protected override void OnStart()
 {
  foreach(var cue in Enum.GetValues<MiningEffectCue>())for(int i=0;i<(cue is MiningEffectCue.TurretMuzzle or MiningEffectCue.Tracer?8:cue is MiningEffectCue.MineExplosion or MiningEffectCue.MortarExplosion?3:2);i++)Create(cue);
 }
 public VfxPlayer? Emit(MiningEffectCue cue,Vector3 position,Vector3? direction=null,float size=1,Vector3? target=null)
 {
  if(!Enum.IsDefined(cue))return null;var p=pool.FirstOrDefault(x=>x.Cue==cue&&!x.Player.GameObject.Active).Player??Create(cue);
  if(p==null){DroppedEffects++;return null;}
  p.GameObject.Active=true;p.Transform.WorldPosition=position;p.Transform.WorldRotation=Align(direction??Vector3.UnitY);p.Size=Math.Clamp(float.IsFinite(size)?size:1,.05f,12);p.ClearBeamTarget();if(target.HasValue)p.SetBeamTarget(target.Value);p.Play();EmittedEffects++;return p;
 }
 static Quaternion Align(Vector3 axis){if(axis.LengthSquared()<.0001f)return Quaternion.Identity;axis=Vector3.Normalize(axis);float d=Vector3.Dot(Vector3.UnitY,axis);return d<-.999f?Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI):Quaternion.Normalize(new Quaternion(Vector3.Cross(Vector3.UnitY,axis),1+d));}
 protected override void OnUpdate(){foreach(var slot in pool)if(slot.Player.GameObject.Active&&slot.Player.HasPlayed&&!slot.Player.IsPlaying){slot.Player.Stop(true);slot.Player.GameObject.Active=false;}}
 protected override void OnStop(){foreach(var p in pool)p.Player.Stop(true);}
}
