using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Characters;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
/// <summary>Presentation only: navigation, attack timing and damage remain owned by the AI.</summary>
public sealed partial class MiningSpiderAnimation3D:Component
{
 [Category("Model")] public AssetReference Model{get;set;}=AssetReference.Empty;
 [Category("Model")] public float ModelScale{get;set;}=.55f;
 [Category("Clips")] public string ClipPrefix{get;set;}="PlayerHunter";
 [Category("Clips")] public string IdleClip{get;set;}="Idle";
 [Category("Clips")] public string WalkClip{get;set;}="Walk";
 [Category("Clips")] public string JumpClip{get;set;}="Jump";
 [Category("Clips")] public string AttackClip{get;set;}="Attack";
 [Category("Clips")] public string DeathClip{get;set;}="Death";
 [Category("Playback")] public float WalkSpeedAtOne{get;set;}=3.2f;
 [Category("Playback")] public float MinimumWalkSpeed{get;set;}=.65f;
 [Category("Playback")] public float MaximumWalkSpeed{get;set;}=1.8f;
 [Category("Playback")] public float BlendSeconds{get;set;}=.1f;
 [Category("Playback")] public float AnimationInterval{get;set;}=1f/24;
 [Category("Playback")] public bool AutomaticPlayback{get;set;}=true;
 [Category("Death")] public float CorpseSeconds{get;set;}=3;
 [Category("Death")] public int MaximumCorpses{get;set;}=12;
 [Category("Damage hitbox")] public bool BodyHitboxEnabled{get;set;}=true;
 [Category("Damage hitbox")] public Vector3 BodyHitboxSize{get;set;}=new(.9f,.85f,1.45f);
 [Category("Damage hitbox")] public Vector3 BodyHitboxCenter{get;set;}=new(0,.55f,.1f);
 public string State{get;private set;}="";
 public string LastError{get;private set;}="";
 public bool ModelReady=>renderer?.ResolvedModel!=null;
 [Browsable(false)] public SkeletalMeshRenderer? Renderer=>renderer;
 public override int UpdateOrder=>100;
 SkeletalMeshRenderer? renderer;GameObject? visual;HealthComponent? health;Vector3 previous;float held;bool dead;string phase="Ready";
 protected override void OnStart()
 {
  previous=Transform.WorldPosition;health=GameObject.GetComponent<HealthComponent>();if(health!=null)health.Died+=OnDeath;
  if(BodyHitboxEnabled){var hit=GameObject.Scene!.CreateGameObject("Spider body damage hitbox");hit.Layer=GameObject.Layer;hit.SetParent(GameObject,false);hit.AddComponent(new BoxCollider3D{IsTrigger=true,Size=BodyHitboxSize,Center=BodyHitboxCenter});hit.AddComponent(new DamageRegion3D{HealthOwnerId=GameObject.Id,IsWeakPoint=false});}
  if(!AnimationRuntimeAssets.IsConfigured){LastError="Model asset service is unavailable";return;}
  try{
   visual=GameObject.Scene!.CreateGameObject("Animated spider");visual.SetParent(GameObject,false);visual.Transform.LocalScale=new(Math.Clamp(ModelScale,.1f,3));
   renderer=visual.AddComponent(new SkeletalMeshRenderer{Model=Model,PlayOnStart=false,AnimationUpdateInterval=Math.Clamp(AnimationInterval,0,.2f),TransitionDuration=BlendSeconds});
   if(renderer.ResolvedModel==null)throw new InvalidDataException("Spider model could not load: "+Model.ProjectPath);
   PlayClip(IdleClip,true);
  }catch(Exception e){LastError=e.Message;}
 }
 public bool PlayClip(string clip,bool loop=false)
 {
  if(renderer==null||dead)return false;string name=clip.Contains('_')?clip:ClipPrefix+"_"+clip;
  renderer.Speed=1;if(!renderer.Play(name,loop,Math.Clamp(BlendSeconds,0,.4f))){LastError="Missing spider clip: "+name;return false;}
  State=clip;held=loop?0:Math.Max(.05f,renderer.Duration);LastError="";return true;
 }
 protected override void OnUpdate()
 {
  if(dead||renderer==null)return;float dt=(float)Time.DeltaTime;if(dt<=0||!float.IsFinite(dt))return;
  if(TickBlastKnockdown(dt))return;
  Vector3 now=Transform.WorldPosition,delta=now-previous;previous=now;float speed=new Vector2(delta.X,delta.Z).Length()/dt;
  var combat=GameObject.GetComponent<SimpleEnemyAI3D>();string next= combat?.AttackPhase??"Ready";held=Math.Max(0,held-dt);
  if(health?.IsDead==true){OnDeath(health);return;}
  if(!AutomaticPlayback)return;
  if(combat?.AttackFiredThisFrame==true){PlayClip(AttackClip);}
  else if(next!=phase&&next is "Leap" or "Lunge"){PlayClip(JumpClip);}
  else if(next!=phase&&next is "Charge" or "Strike"){PlayClip(AttackClip);}
  else if(held<=0){string desired=speed>.08f?WalkClip:IdleClip;if(State!=desired)PlayClip(desired,true);if(desired==WalkClip)renderer.Speed=Math.Clamp(speed/Math.Max(.1f,WalkSpeedAtOne),Math.Max(.1f,MinimumWalkSpeed),Math.Max(MinimumWalkSpeed,MaximumWalkSpeed));}
  phase=next;
 }
 void OnDeath(HealthComponent _)
 {
  if(dead)return;dead=true;State=DeathClip;if(visual==null||renderer==null||visual.Scene==null)return;
  var scene=visual.Scene;
  var corpses=scene.GameObjects.Where(o=>o.Name=="Spider death remains").ToArray();
  foreach(var old in corpses.Take(Math.Max(0,corpses.Length-Math.Clamp(MaximumCorpses,1,32)+1)))scene.DestroyGameObject(old);
  // Detach before Health destroys the gameplay root, including the final enemy of a wave.
  visual.SetParent(null,true);visual.Name="Spider death remains";renderer.Speed=1;
  if(TryBlastDeath(scene)){visual=null;return;}
  string clip=DeathClip.Contains('_')?DeathClip:ClipPrefix+"_"+DeathClip;
  if(!renderer.Play(clip,false,0))LastError="Missing spider death clip: "+clip;
  visual.AddComponent(new LifetimeComponent{LifetimeSeconds=Math.Max(renderer.Duration+.2f,Math.Clamp(CorpseSeconds,.5f,10))});
  visual=null;
 }
 protected override void OnStop(){if(health!=null)health.Died-=OnDeath;}
 protected override void OnDestroy(){if(health!=null)health.Died-=OnDeath;}
}
