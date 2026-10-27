using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public enum MiningDefenseKind{Sentry,Repeater,Mortar,Mine,SupplyCache}
public sealed partial class MiningDefense3D:Component
{
 [SceneObjectReference] public Guid OperationObjectId{get;set;}
 [SceneObjectReference] public Guid WorkshopObjectId{get;set;}
 public MiningDefenseKind Kind{get;set;}
 public float BaseDamage{get;set;}=12;
 public float BaseInterval{get;set;}=.8f;
 public float Range{get;set;}=18;
 public float BlastRadius{get;set;}=3;
 public float TriggerRadius{get;set;}=1.8f;
 public bool Spent{get;private set;}
 public int ShotsFired{get;private set;}
 float cooldown,flight=-1,flash,cacheHeight=8;int salvo;Vector3 impact,start;GameObject? head,beam,shell;
 MiningTrade3D? Trade=>GameObject.Scene?.FindGameObject(WorkshopObjectId)?.GetComponent<MiningTrade3D>();
 MiningOperation3D? Op=>GameObject.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();
 GameObject Part(string name,Vector3 p,Vector3 size,Vector4 color,PrimitiveMeshType shape=PrimitiveMeshType.Cube)
 {var o=GameObject.Scene!.CreateGameObject(name);o.SetParent(GameObject,false);o.Transform.LocalPosition=p;o.Transform.LocalScale=size;o.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=shape,Material=new(){BaseColor=color,Roughness=.55f}});return o;}
 protected override void OnStart()
 {
  if(TryStartVisual()){CreateShotEffects();return;}
  var gold=new Vector4(.65f,.42f,.08f,1);var dark=new Vector4(.1f,.15f,.17f,1);
  if(Kind==MiningDefenseKind.SupplyCache){head=Part("Incoming supply crate",new(0,8,0),new(.8f),gold);Part("Landing marker",new(0,.02f,0),new(1.2f,.025f,1.2f),gold,PrimitiveMeshType.Cylinder);return;}
  Part("Defence foundation",new(0,.12f,0),Kind==MiningDefenseKind.Mine?new(.7f,.15f,.7f):new(1,.24f,1),dark,PrimitiveMeshType.Cylinder);
  if(Kind==MiningDefenseKind.Mine){Part("Proximity charge",new(0,.23f,0),new(.48f,.1f,.48f),gold,PrimitiveMeshType.Cylinder);Part("Armed indicator",new(0,.31f,0),new(.12f,.06f,.12f),new(1,.12f,.02f,1),PrimitiveMeshType.Sphere);}
  else{Part("Mount",new(0,.6f,0),new(.25f,.9f,.25f),gold);head=Part("Tracking receiver",new(0,1.1f,0),new(.65f,.4f,.6f),gold);var barrel=Part("Barrel",new(0,1.1f,-.65f),new(Kind==MiningDefenseKind.Repeater?.25f:.12f,.13f,.85f),dark);barrel.SetParent(head,true);}
  beam=Part("Shot trace",Vector3.Zero,new(.025f),new(1,.8f,.25f,1));beam.Active=false;
  shell=Part("Mortar round",Vector3.Zero,new(.2f),gold,PrimitiveMeshType.Sphere);shell.Active=false;
 }
 public void Tick(float dt)
 {
  if(dt<=0||!float.IsFinite(dt))return;TickVisual(dt);
  if(Spent||Op is not {} op||Trade is not {} trade)return;
  if(Kind==MiningDefenseKind.SupplyCache){cacheHeight=Math.Max(.6f,cacheHeight-dt*5);if(head!=null)head.Transform.LocalPosition=new(0,cacheHeight,0);if(visualRoot!=null)visualRoot.Transform.LocalPosition=new(0,cacheHeight-.6f,0);var player=GameObject.Scene!.FindGameObject(op.PlayerObjectId);var weapon=player?.GetComponent<FpsGunLab3D>()?.SelectedWeapon;var health=player?.GetComponent<HealthComponent>();if(cacheHeight<=.6f&&player!=null&&Vector3.Distance(player.Transform.WorldPosition,Transform.WorldPosition)<1.6f&&(weapon?.ReserveAmmo<weapon?.Definition?.ReserveCapacity||health?.HealthPercent<1)){if(weapon?.Definition is {} d)weapon.SetAmmo(weapon.CurrentAmmo,Math.Min(d.ReserveCapacity,weapon.ReserveAmmo+24));health?.Heal(15);if(!CollectVisual()){Spent=true;GameObject.Scene.DestroyGameObject(GameObject);}}return;}
  cooldown=Math.Max(0,cooldown-dt);flash=Math.Max(0,flash-dt);if(beam!=null)beam.Active=flash>0;
  if(flight>=0){flight+=dt;float t=Math.Clamp(flight/.9f,0,1);if(shell!=null)shell.Transform.WorldPosition=Vector3.Lerp(start,impact,t)+Vector3.UnitY*MathF.Sin(t*MathF.PI)*6;if(t>=1){flight=-1;mortarSmoke?.Stop(true);if(shell!=null)shell.Active=false;Blast(impact,BlastRadius,BaseDamage+trade.Stat("MortarStrikeDamageFlat"));if(salvo>0){salvo--;flight=0;impact+=new Vector3(MathF.Sin(salvo*2.4f),0,MathF.Cos(salvo*2.4f));if(shell!=null){shell.Transform.WorldPosition=start;shell.Active=true;}ShotsFired++;PlayShotVisual();ShowShotEffects(start,impact,true);MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Cannon,start);}}return;}
  if(op.Phase!=MiningPhase.Drilling&&op.RoamingEnemiesAlive==0)return;
  var targets=op.SpawnedEnemies.Where(o=>o.Scene==GameObject.Scene&&o.GetComponent<HealthComponent>() is {IsDead:false}).OrderBy(o=>Vector3.DistanceSquared(o.Transform.WorldPosition,Transform.WorldPosition)).ToArray();
  var enemy=targets.FirstOrDefault(o=>Vector3.DistanceSquared(o.Transform.WorldPosition,Transform.WorldPosition)<=Range*Range);if(enemy==null)return;
  if(Kind==MiningDefenseKind.Mine)
  {if(Vector3.DistanceSquared(enemy.Transform.WorldPosition,Transform.WorldPosition)<=TriggerRadius*TriggerRadius){Blast(Transform.WorldPosition,BlastRadius+trade.Stat("LandMineBlastRadius"),BaseDamage+trade.Stat("LandMineDamageFlat"));DetonateVisual();}return;}
  TrackVisualTarget(enemy.Transform.WorldPosition+Vector3.UnitY*.7f);if(VisualReady)TickVisual(0);if(cooldown>0)return;
  Vector3 origin=ShotOrigin(),target=enemy.Transform.WorldPosition+Vector3.UnitY*.7f,ray=target-origin;
  if(Kind==MiningDefenseKind.Mortar){impact=target;impact.Y=enemy.Transform.WorldPosition.Y;start=origin;flight=0;salvo=Math.Clamp((int)trade.Stat("MortarShells"),0,7);if(shell!=null){shell.Transform.WorldPosition=start;shell.Active=true;}cooldown=Math.Max(.8f,BaseInterval+trade.Stat("MortarStrikeInterval"));ShotsFired++;PlayShotVisual();MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Cannon,origin);ShowShotEffects(origin,target,true);return;}
  if(GameplayQuery3D.Raycast(GameObject.Scene!,origin,ray,out var hit,ray.Length(),GameObject,includeTriggers:false)&&hit.GameObject!=enemy&&!hit.GameObject.IsDescendantOf(enemy))return;
  string family=Kind==MiningDefenseKind.Sentry?"Autogun":"MachineGun";float damage=BaseDamage+trade.Stat(family+"DamageFlat");cooldown=1/Math.Max(.1f,1/Math.Max(.05f,BaseInterval)+trade.Stat(family+"FireRateFlat"));
  int penetration=Math.Clamp((int)trade.Stat(family+"Pierce"),0,8);var direction=Vector3.Normalize(ray);
  if(!LaunchDefenceProjectile(origin,direction,damage,penetration,Range,GameObject.Id))return;
  if(head!=null)head.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(-direction.X,-direction.Z));
  if(beam!=null){beam.Transform.WorldPosition=(origin+target)/2;beam.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(-direction.X,-direction.Z));beam.Transform.LocalScale=new(.025f,.025f,ray.Length());beam.Active=true;flash=.08f;}ShotsFired++;PlayShotVisual();MiningAudio3D.Find(GameObject)?.PlayCue(Kind==MiningDefenseKind.Repeater?MiningSoundCue.RepeaterShot:MiningSoundCue.DefenceShot,origin);ShowShotEffects(origin,target);
 }
 void Blast(Vector3 point,float radius,float damage)
 {
  if(Op is not {} op)return;
  MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Explosion,point);
  var effects=MiningEffects3D.Find(GameObject);effects?.Emit(Kind==MiningDefenseKind.Mine?MiningEffectCue.MineExplosion:MiningEffectCue.MortarExplosion,point+Vector3.UnitY*.25f,size:Kind==MiningDefenseKind.Mine?effects.MineExplosionScale:effects.MortarExplosionScale);
  foreach(var victim in op.SpawnedEnemies.ToArray())if(victim.Scene==GameObject.Scene&&victim.GetComponent<HealthComponent>() is {IsDead:false} health&&Vector3.Distance(victim.Transform.WorldPosition,point)<=radius){var away=victim.Transform.WorldPosition-point;float falloff=.4f+.6f*(1-Math.Clamp(away.Length()/Math.Max(.1f,radius),0,1));if(effects?.ExplosionKnockdowns==true)victim.GetComponent<MiningSpiderAnimation3D>()?.ApplyExplosionImpact(away,effects.ExplosionLaunchSpeed*falloff,effects.ExplosionLift);health.Damage(damage,away);}
 }
 protected override void OnUpdate()=>Tick((float)Time.DeltaTime);
}
