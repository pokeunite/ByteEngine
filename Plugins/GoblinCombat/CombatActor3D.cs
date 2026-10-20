using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
namespace GoblinCombat;

public sealed class CombatActor3D:Component
{
 public bool Player {get;set;}
 public bool Brute {get;set;}
 public bool ReadPlayerInput {get;set;}=true;
 public float MoveSpeed {get;set;}=4.6f;
 public float AttackRange {get;set;}=1.8f;
 public float TurnResponse {get;set;}=18;
 public CombatTiming Timing {get;}=new();
 public int HitsLanded {get;private set;}
 public int DamageTaken {get;private set;}
 public string EnemyState {get;private set;}="WATCHING";
 public Vector3 MoveIntent {get;set;}
 public override int UpdateOrder=>-300;
 HealthComponent? health;CharacterController3D? motor;CombatActor3D? hero;
 GameObject? rightArm,leftLeg,rightLeg,visual,ring;Vector3 visualPosition;
 float hurt,pause,aiTime,flash,walk;bool aiSwing,aiHit;int swingSeen=-1;readonly HashSet<Guid> struck=[];Vector3 dodgeDirection=-Vector3.UnitZ;Quaternion attackFacing=Quaternion.Identity;
 public bool HitStopped=>pause>0;
 public HealthComponent Health=>health??GameObject.GetComponent<HealthComponent>()!;
 public bool RequestAttack(){if(Health.IsDead||hurt>0)return false;bool started=Timing.PressAttack();if(started)AimAttack();return started;}
 public bool RequestDodge(){if(Health.IsDead||hurt>0)return false;if(!Timing.PressDodge())return false;dodgeDirection=MoveIntent.LengthSquared()>.01f?Vector3.Normalize(MoveIntent):Transform.Forward;return true;}
 protected override void OnStart(){health=GameObject.GetComponent<HealthComponent>();motor=GameObject.GetComponent<CharacterController3D>();hero=GameObject.Scene!.GameObjects.Select(o=>o.GetComponent<CombatActor3D>()).FirstOrDefault(c=>c?.Player==true);
  GameObject? Find(GameObject o,string name){if(o.Name==name)return o;foreach(var child in o.Children){var match=Find(child,name);if(match!=null)return match;}return null;}
  visual=Find(GameObject,"Visual");rightArm=Find(GameObject,"RightArm");leftLeg=Find(GameObject,"LeftLeg");rightLeg=Find(GameObject,"RightLeg");ring=Find(GameObject,"Telegraph");if(visual!=null)visualPosition=visual.Transform.LocalPosition;
 }
 void AimAttack(){if(!Player)return;Vector3 desired=MoveIntent;var target=GameObject.Scene!.GameObjects.Select(o=>o.GetComponent<CombatActor3D>()).Where(a=>a!=null&&!a.Player&&!a.Health.IsDead).OrderBy(a=>Vector3.DistanceSquared(a!.Transform.WorldPosition,Transform.WorldPosition)).FirstOrDefault();
  if(desired.LengthSquared()<.01f&&target!=null){var to=target.Transform.WorldPosition-Transform.WorldPosition;to.Y=0;if(to.LengthSquared()<6.25f&&Vector3.Dot(Vector3.Normalize(to),Transform.Forward)>.2f)desired=to;}
  if(desired.LengthSquared()>.01f)Face(desired,1);attackFacing=Transform.WorldRotation;
 }
 void Face(Vector3 direction,float amount){direction.Y=0;if(direction.LengthSquared()<.001f)return;float yaw=MathF.Atan2(-direction.X,-direction.Z);Transform.WorldRotation=Quaternion.Slerp(Transform.WorldRotation,Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw),amount);}
 protected override void OnUpdate(){if(health==null||motor==null)return;float dt=(float)Time.DeltaTime;if(dt<=0)return;
  flash=Math.Max(0,flash-dt);if(Health.IsDead){motor.Move(Vector3.Zero);if(visual!=null)visual.Transform.LocalRotation=Quaternion.Slerp(visual.Transform.LocalRotation,Quaternion.CreateFromAxisAngle(Vector3.UnitZ,1.35f),1-MathF.Exp(-10*dt));return;}
  if(pause>0){pause-=dt;motor.Move(Vector3.Zero);return;}
  hurt=Math.Max(0,hurt-dt);
  if(Player){
   if(ReadPlayerInput){var camera=GameObject.Scene!.FindGameObject("Camera");Vector3 f=camera?.Transform.Forward??-Vector3.UnitZ;f.Y=0;f=Vector3.Normalize(f);var r=Vector3.Cross(f,Vector3.UnitY);MoveIntent=f*((Input.IsKeyDown(Key.W)?1:0)-(Input.IsKeyDown(Key.S)?1:0))+r*((Input.IsKeyDown(Key.D)?1:0)-(Input.IsKeyDown(Key.A)?1:0));if(MoveIntent.LengthSquared()>1)MoveIntent=Vector3.Normalize(MoveIntent);
    if(Input.IsMouseButtonPressed(MouseButton.Left)||Input.IsKeyPressed(Key.J))RequestAttack();if(Input.IsKeyPressed(Key.Space))RequestDodge();}
   int before=Timing.Swing;Timing.Tick(dt);if(before!=Timing.Swing)AimAttack();
   
   if(hurt>0)motor.Move(Vector3.Zero);
   else if(Timing.Dodging){motor.MoveSpeed=8;motor.Move(dodgeDirection);}
   else if(Timing.Attacking){Transform.WorldRotation=attackFacing;motor.MoveSpeed=1.4f;motor.Move(Vector3.Zero);}
   else{motor.MoveSpeed=MoveSpeed;motor.Move(MoveIntent);Face(MoveIntent,1-MathF.Exp(-TurnResponse*dt));}
   if(hurt<=0&&!Timing.Dodging&&Timing.ContactStageThisTick>=0){if(swingSeen!=Timing.ContactSwingThisTick){swingSeen=Timing.ContactSwingThisTick;struck.Clear();}int contact=Timing.ContactStageThisTick;ResolveHits(AttackRange,contact==2?36:22,contact==2?.28f:.16f);}
  }else Enemy(dt);
  Animate(dt);
 }
 void Enemy(float dt){if(hero==null||hero.Health.IsDead){motor!.Move(Vector3.Zero);return;}var delta=hero.Transform.WorldPosition-Transform.WorldPosition;delta.Y=0;float distance=delta.Length();if(hurt>0){EnemyState="STAGGERED";motor!.Move(Vector3.Zero);return;}
  if(aiSwing){aiTime+=dt;float windup=Brute?.85f:.55f;EnemyState=aiTime<windup?"WINDUP":aiTime<windup+.13f?"STRIKE":"RECOVERY";motor!.MoveSpeed=1.2f;motor.Move(aiTime>=windup&&aiTime<windup+.13f?Transform.Forward:Vector3.Zero);
   if(!aiHit&&aiTime>=windup){aiHit=true;ResolveHits(Brute?2.2f:1.8f,Brute?28:17,.2f);}if(aiTime>windup+(Brute?.9f:.65f)){aiSwing=false;aiTime=0;}return;}
  if(distance<1.65f+(Brute?.25f:0)){Face(delta,1);aiSwing=true;aiTime=0;aiHit=false;struck.Clear();EnemyState="WINDUP";motor!.Move(Vector3.Zero);}else if(distance<8){EnemyState="APPROACH";motor!.MoveSpeed=Brute?1.3f:2.0f;motor.Move(Vector3.Normalize(delta));Face(delta,1-MathF.Exp(-8*dt));}else{EnemyState="WATCHING";motor!.Move(Vector3.Zero);}
 }
 void ResolveHits(float range,float damage,float stun){foreach(var obj in GameObject.Scene!.GameObjects){var target=obj.GetComponent<CombatActor3D>();if(target==null||target.Player==Player||target.Health.IsDead||struck.Contains(obj.Id))continue;var offset=target.Transform.WorldPosition-Transform.WorldPosition;offset.Y=0;float distance=offset.Length();if(distance>range+.28f||distance<.001f||Vector3.Dot(offset/distance,Transform.Forward)<.25f)continue;
   if(GameplayQuery3D.Raycast(GameObject.Scene!,Transform.WorldPosition+Vector3.UnitY*.8f,offset/distance,out var hit,Math.Max(.01f,distance-.45f),GameObject,includeTriggers:false)&&hit.GameObject!=target.GameObject)continue;
   struck.Add(obj.Id);if(target.ReceiveHit(damage,stun)){HitsLanded++;pause=.035f;GameObject.Scene.FindGameObject("Hit SFX")?.GetComponent<ByteEngine.Core.Audio.AudioSource3D>()?.Play();}}
 }
 public bool ReceiveHit(float damage,float stun){if(Health.IsDead||Player&&Timing.Invincible)return false;Health.Damage(damage);DamageTaken++;hurt=stun;pause=.035f;flash=.14f;Timing.Interrupt();if(!Player){aiSwing=false;aiTime=0;}return true;}
 void Animate(float dt){if(Player)return;walk+=dt*(motor?.Speed??0)*5;
  if(leftLeg!=null)leftLeg.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,Timing.Attacking?0:MathF.Sin(walk)*.35f);
  if(rightLeg!=null)rightLeg.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,Timing.Attacking?0:-MathF.Sin(walk)*.35f);
  float elapsed=Player?Timing.Elapsed:aiTime;bool attack=Player?Timing.Attacking:aiSwing;float wind=Player&&Timing.Stage>=0?CombatTiming.Windup[Timing.Stage]:Brute?.85f:.55f;
  float swing=attack?(elapsed<wind?float.Lerp(0,-1.5f,elapsed/wind):float.Lerp(-1.5f,1.6f,Math.Clamp((elapsed-wind)/.13f,0,1))):0;if(rightArm!=null)rightArm.Transform.LocalRotation=Quaternion.Slerp(rightArm.Transform.LocalRotation,Quaternion.CreateFromYawPitchRoll(swing,attack?-.2f:0,attack?-.65f:0),1-MathF.Exp(-32*dt));
  if(ring!=null)ring.Active=!Player&&aiSwing&&aiTime<wind;
  if(visual!=null){visual.Transform.LocalPosition=visualPosition+Vector3.UnitY*(Timing.Dodging?-.3f:0);visual.Transform.LocalScale=Vector3.One*(flash>0?1.06f:1);}
 }
}



