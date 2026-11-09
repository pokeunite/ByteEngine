using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
/// <summary>Finite-use flying support. No repairs, recharging or friendly explosion damage.</summary>
public sealed partial class MiningSecurityDrone3D:Component
{
 [SceneObjectReference,Category("Bindings")] public Guid OperationObjectId{get;set;}
 [Category("Visual")] public AssetReference Model{get;set;}=AssetReference.Empty;
 [Category("Visual")] public float ModelScale{get;set;}=1;
 [Category("Battery")] public float BatterySeconds{get;set;}=45;
 [Category("Battery")] public bool DrainOnlyDuringCombat{get;set;}=true;
 [Category("Battery")] public float WarningSeconds{get;set;}=5;
 [Category("Movement")] public float FollowSpeed{get;set;}=7;
 [Category("Movement")] public float HoverHeight{get;set;}=1.7f;
 [Category("Movement")] public float FollowDistance{get;set;}=2;
 [Category("Tactics")] public float ThreatSearchRadius{get;set;}=28;
 [Category("Tactics")] public float FiringDistance{get;set;}=5;
 [Category("Tactics")] public float MaximumLeashDistance{get;set;}=32;
 [Category("Battery")] public bool ShowBatteryBar{get;set;}=true;
 [Category("Weapon")] public float AttackRange{get;set;}=12;
 [Category("Weapon")] public float ShotDamage{get;set;}=8;
 [Category("Weapon")] public float ShotInterval{get;set;}=.5f;
 [Category("Weapon")] public float ProjectileSpeed{get;set;}=45;
 [Category("Explosion")] public float ExplosionRadius{get;set;}=3;
 [Category("Explosion")] public float ExplosionDamage{get;set;}=50;
 [Category("Explosion")] public float ExplosionVisualSize{get;set;}=1.3f;
 [Category("Explosion")] public bool RemoveWhenDepleted{get;set;}=true;
 [Category("Live state")] public float BatteryRemaining{get;private set;}
 [Category("Live state")] public float BatteryFraction=>Math.Clamp(BatteryRemaining/Safe(BatterySeconds,60,.1f,3600),0,1);
 [Category("Live state")] public bool BatteryLow=>!Detonated&&BatteryRemaining<=Safe(WarningSeconds,5,0,60);
 [Category("Live state")] public bool Detonated{get;private set;}
 [Category("Live state")] public int ShotsFired{get;private set;}
 [Category("Live state")] public int ExplosionVictims{get;private set;}
 [Category("Live state")] public bool VisualReady=>visual?.ModelLoaded==true;
 bool initialized,warningPlayed;float shotClock,targetClock;GameObject? target,visualRoot;SkeletalMeshRenderer? visual;
 readonly List<GameObject> bullets=[];
 MiningOperation3D? Op=>AttachedGameObject?.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();
 static float Safe(float v,float fallback,float min,float max)=>Math.Clamp(float.IsFinite(v)?v:fallback,min,max);
 void Initialize(){if(initialized)return;initialized=true;BatteryRemaining=Safe(BatterySeconds,60,.1f,3600);}
 public void RestoreBattery(float seconds){Initialize();BatteryRemaining=Safe(seconds,0,0,Safe(BatterySeconds,60,.1f,3600));}
 protected override void OnStart()
 {
  Initialize();if(Model.IsEmpty)return;
  visualRoot=GameObject.Scene!.CreateGameObject("Security drone animated model");visualRoot.SetParent(GameObject,false);visualRoot.Transform.LocalPosition=new(0,-.53f*ModelScale,0);visualRoot.Transform.LocalScale=new(Safe(ModelScale,1,.05f,5));
  visual=visualRoot.AddComponent(new SkeletalMeshRenderer{Model=Model,PlayOnStart=true,DefaultAnimation="SecurityDrone_Hover",Loop=true});
 }
 protected override void OnUpdate()=>Tick((float)Time.DeltaTime);
 public void Tick(float delta)
 {
  if(!float.IsFinite(delta)||delta<=0||AttachedGameObject?.Scene is not {} scene||Detonated)return;Initialize();
  var op=Op;if(op==null)return;
  bool combat=op.Phase==MiningPhase.Drilling||op.RoamingEnemiesAlive>0;
  if(combat||!DrainOnlyDuringCombat)BatteryRemaining=Math.Max(0,BatteryRemaining-delta);
  if(BatteryRemaining<=0){Detonate();return;}
  if(combat&&BatteryFraction<=SelfDestructBatteryFraction){DiveAtNearest(op,delta);if(!Detonated)UpdateCameraClearance(op);return;}
  if(BatteryLow&&!warningPlayed){warningPlayed=true;visual?.Play("SecurityDrone_Overload",true,0);MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Alert,Transform.WorldPosition);}
  if(!combat){target=null;Patrol(op,delta);UpdateCameraClearance(op);return;}
  shotClock=Math.Max(0,shotClock-delta);targetClock-=delta;
  if(targetClock<=0||target?.Scene!=scene||target.GetComponent<HealthComponent>()?.IsDead!=false)
  {
   targetClock=.4f;ChooseBattlefieldTarget(op);
  }
  if(target==null){Patrol(op,delta);UpdateCameraClearance(op);return;}
  var away=Transform.WorldPosition-target.Transform.WorldPosition;away.Y=0;if(away.LengthSquared()<.01f)away=Vector3.UnitX;away=Vector3.Normalize(away);
  var side=new Vector3(-away.Z,0,away.X)*MathF.Sin((float)Time.TotalTime*.8f+GameObject.Id.GetHashCode()%10)*1.5f;
  var desired=target.Transform.WorldPosition+away*(BatteryLow?1.6f:Safe(FiringDistance,5,2,12))+side+Vector3.UnitY*Safe(HoverHeight,1.7f,.5f,5);MoveTo(ClearPlayerView(op,desired),delta);UpdateCameraClearance(op);
  if(shotClock>0)return;var direction=target.Transform.WorldPosition+Vector3.UnitY*.5f-Transform.WorldPosition;
  if(direction.LengthSquared()<.0001f||direction.LengthSquared()>AttackRange*AttackRange)return;
  if(GameplayQuery3D.Raycast(scene,Transform.WorldPosition,direction,out var obstacle,direction.Length(),GameObject,includeTriggers:false)&&obstacle.GameObject!=target&&!obstacle.GameObject.IsDescendantOf(target))return;
  Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(-direction.X,-direction.Z));
  if(Fire(Vector3.Normalize(direction))){shotClock=Safe(ShotInterval,.5f,.1f,30);ShotsFired++;}
 }
 protected override void OnRender(RenderContext c){if(!ShowBatteryBar||HiddenForCamera||Detonated||!c.Has3DCamera||Input.IsUiPointerRequested)return;var clip=Vector4.Transform(new Vector4(Transform.WorldPosition+Vector3.UnitY*.65f,1),c.GetViewMatrix3D()*c.GetProjectionMatrix3D());if(clip.W<=0)return;float x=(clip.X/clip.W*.5f+.5f)*c.TargetWidth,y=(.5f-clip.Y/clip.W*.5f)*c.TargetHeight;if(x<0||x>c.TargetWidth||y<0||y>c.TargetHeight)return;float scale=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f);c.DrawUiQuad(new(x-34*scale,y-4*scale),new(68*scale,8*scale),new(.02f,.04f,.07f,.95f));c.DrawUiQuad(new(x-32*scale,y-2*scale),new(64*scale*BatteryFraction,4*scale),new(.12f,.60f,1,1));}
 bool Fire(Vector3 direction)
 {
  var scene=GameObject.Scene!;bullets.RemoveAll(o=>o.Scene!=scene);if(bullets.Count>=16)return false;
  var origin=Transform.WorldPosition+Vector3.UnitY*(-.20f)+direction*.30f;var bullet=scene.CreateGameObject("Security drone projectile");bullets.Add(bullet);bullet.Transform.WorldPosition=origin;bullet.Transform.LocalScale=new(.04f);
  var mesh=bullet.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,CastShadows=false,Material=new(){Shading=MaterialShadingMode.Unlit,BaseColor=new(1,.9f,.4f,1)}});
  var flight=bullet.AddComponent(new Projectile3D{OwnerId=GameObject.Id,Velocity=direction*Safe(ProjectileSpeed,45,1,200),Damage=0,Radius=.03f,DestroyOnHit=false});
  var trail=bullet.AddComponent(new BulletTrail3D{Length=1,Width=.025f});var lifetime=bullet.AddComponent(new LifetimeComponent{LifetimeSeconds=Safe(AttackRange,12,.5f,60)/Safe(ProjectileSpeed,45,1,200)});
  flight.Hit+=hit=>{
   for(var victim=hit.GameObject;victim!=null;victim=victim.Parent)if(Op?.SpawnedEnemies.Contains(victim)==true){victim.GetComponent<HealthComponent>()?.Damage(Safe(ShotDamage,8,0,10000),direction);break;}
   trail.Finish(direction,.06f);mesh.Enabled=false;lifetime.Restart(.06f);
  };
  MiningEffects3D.Find(GameObject)?.Emit(MiningEffectCue.TurretMuzzle,origin,direction,size:.4f);MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.DefenceShot,origin);return true;
 }
 public void Detonate()
 {
  if(Detonated||AttachedGameObject?.Scene is not {} scene)return;Detonated=true;BatteryRemaining=0;var point=Transform.WorldPosition;Op?.GameObject.GetComponent<MiningGameFeel3D>()?.NotifyDroneDestroyed();MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Alert);
  MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Explosion,point);MiningEffects3D.Find(GameObject)?.Emit(MiningEffectCue.MineExplosion,point,size:Safe(ExplosionVisualSize,1.3f,.1f,8));
  if(Op is {} op)foreach(var enemy in op.SpawnedEnemies.ToArray())
  {
   if(enemy.Scene!=scene||enemy.GetComponent<HealthComponent>() is not {IsDead:false} health)continue;
   var away=enemy.Transform.WorldPosition+Vector3.UnitY*.5f-point;if(away.LengthSquared()>MathF.Pow(Safe(ExplosionRadius,3,.1f,20),2))continue;
   // Solid walls protect enemies from the blast. Player, rig and friendly structures are never victims.
   if(GameplayQuery3D.Raycast(scene,point,away,out var hit,away.Length(),GameObject,includeTriggers:false)&&hit.GameObject!=enemy&&!hit.GameObject.IsDescendantOf(enemy))continue;
   health.Damage(Safe(ExplosionDamage,50,0,10000),away);enemy.GetComponent<MiningSpiderAnimation3D>()?.ApplyExplosionImpact(away,5,2);ExplosionVictims++;
  }
  if(RemoveWhenDepleted)scene.DestroyGameObject(GameObject);
 }
 protected override void OnStop(){foreach(var bullet in bullets.ToArray())if(bullet.Scene is {} scene)scene.DestroyGameObject(bullet);if(visualRoot?.Scene is {} owner)owner.DestroyGameObject(visualRoot);bullets.Clear();}
}
