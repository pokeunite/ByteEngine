using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.AI;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningSpiderAnimation3D
{
 [Category("Explosion reactions")] public bool ExplosionRagdoll{get;set;}=true;
 [Category("Explosion reactions")] public float BlastKnockdownSeconds{get;set;}=1.1f;
 [Category("Explosion reactions")] public float BlastRecoverySeconds{get;set;}=.3f;
 [Category("Explosion reactions")] public int MaximumBlastKnockdowns{get;set;}=12;
 [Browsable(false)] public bool IsBlastKnockedDown=>blastAge>0;
 float blastAge,recoveryAge;bool recovering;Vector3 blastVelocity;Rigidbody3D? blastBody;bool createdBody;bool aiWasEnabled,navWasEnabled,combatWasEnabled;
 public bool ApplyExplosionImpact(Vector3 direction,float speed=7,float lift=3.5f)
 {
  if(!ExplosionRagdoll||dead||health?.IsDead==true||renderer==null||!float.IsFinite(speed)||speed<=0)return false;
  if(!float.IsFinite(lift))lift=3.5f;
  var scene=GameObject.Scene;if(scene==null)return false;
  if(!IsBlastKnockedDown&&scene.GameObjects.Count(o=>o.GetComponent<MiningSpiderAnimation3D>()?.IsBlastKnockedDown==true)>=Math.Clamp(MaximumBlastKnockdowns,1,24))return false;
  direction.Y=0;if(!float.IsFinite(direction.LengthSquared())||direction.LengthSquared()<.001f)direction=Vector3.UnitZ;direction=Vector3.Normalize(direction);
  if(!renderer.BeginLightRagdoll(Math.Clamp(speed/5,.4f,2)))return false;
  if(!IsBlastKnockedDown){aiWasEnabled=GameObject.GetComponent<AIBehavior3D>()?.Enabled==true;navWasEnabled=GameObject.GetComponent<NavigationAgent3D>()?.Enabled==true;combatWasEnabled=GameObject.GetComponent<SimpleEnemyAI3D>()?.Enabled==true;}
  if(GameObject.GetComponent<AIBehavior3D>() is {} ai){ai.Stop();ai.Enabled=false;}if(GameObject.GetComponent<NavigationAgent3D>() is {} nav)nav.Enabled=false;if(GameObject.GetComponent<SimpleEnemyAI3D>() is {} combat){combat.ResetAttackPattern();combat.Enabled=false;}
  if(blastBody==null){blastBody=GameObject.GetComponent<Rigidbody3D>();createdBody=blastBody==null;}blastBody??=GameObject.AddComponent(new Rigidbody3D{Mass=6,ContinuousCollision=true,Friction=.7f,Restitution=.15f});
  blastBody.BodyType=RigidbodyBodyType3D.Dynamic;blastBody.UseGravity=true;blastVelocity=direction*Math.Clamp(speed,0,14)+Vector3.UnitY*Math.Clamp(lift,0,7);blastBody.Velocity=blastVelocity;
  blastAge=Math.Max(.2f,BlastKnockdownSeconds);recoveryAge=0;recovering=false;State="Explosion knockdown";return true;
 }
 bool TickBlastKnockdown(float dt)
 {
  if(blastAge<=0)return false;previous=Transform.WorldPosition;blastAge=Math.Max(.0001f,blastAge-dt);
  if(!recovering&&blastAge<=.0001f){renderer?.EndLightRagdoll(Math.Clamp(BlastRecoverySeconds,.05f,1));recovering=true;recoveryAge=0;}
  if(recovering){recoveryAge+=dt;if(recoveryAge>=Math.Clamp(BlastRecoverySeconds,.05f,1)){
   if(blastBody!=null){blastBody.Velocity=Vector3.Zero;if(createdBody)GameObject.RemoveComponent(blastBody);else{blastBody.BodyType=RigidbodyBodyType3D.Kinematic;blastBody.UseGravity=false;}}blastBody=null;
   if(GameObject.GetComponent<AIBehavior3D>() is {} ai)ai.Enabled=aiWasEnabled;if(GameObject.GetComponent<NavigationAgent3D>() is {} nav)nav.Enabled=navWasEnabled;if(GameObject.GetComponent<SimpleEnemyAI3D>() is {} combat)combat.Enabled=combatWasEnabled;
   blastAge=0;recovering=false;State="";PlayClip(IdleClip,true);return false;
  }}return true;
 }
 bool TryBlastDeath(ByteEngine.Core.Scene.Scene scene)
 {
  if(blastAge<=0||visual==null||renderer==null)return false;
  var corpse=scene.CreateGameObject("Spider death remains");corpse.Transform.WorldPosition=visual.Transform.WorldPosition;corpse.Transform.WorldRotation=visual.Transform.WorldRotation;visual.Name="Ragdoll spider skin";visual.SetParent(corpse,true);
  corpse.AddComponent(new CapsuleCollider3D{Radius=.45f,Height=.9f,Center=new(0,.45f,0)});
  corpse.AddComponent(new Rigidbody3D{Mass=6,UseGravity=true,ContinuousCollision=true,SimulateRotation=true,AngularVelocity=new(3.2f,1.4f,4.1f),AngularDamping=.9f,LinearDamping=.16f,Velocity=blastBody?.Velocity??blastVelocity,Friction=.7f,Restitution=.28f});
  renderer.BeginLightRagdoll(1.5f);State="Explosion ragdoll";corpse.AddComponent(new LifetimeComponent{LifetimeSeconds=Math.Clamp(CorpseSeconds,1.5f,8)});return true;
 }
}
