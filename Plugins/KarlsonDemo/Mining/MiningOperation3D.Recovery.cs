using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;

public sealed partial class MiningOperation3D
{
 [Category("Defeat recovery")] public bool AutomaticDefeatRecovery{get;set;}=true;
 [Category("Defeat recovery")] public float DefeatFadeSeconds{get;set;}=.65f;
 [Category("Defeat recovery")] public float DefeatBlackSeconds{get;set;}=.4f;
 [Category("Defeat recovery")] public bool UseCustomRecoverySpawn{get;set;}
 [Category("Defeat recovery")] public Vector3 RecoverySpawnPosition{get;set;}
 [Browsable(false)] public float RecoveryOpacity{get;private set;}
 [Browsable(false)] public bool Recovering=>recoveryStage!=0;
 Vector3 initialSpawn;Quaternion initialRotation;bool recoveryMovement,recoveryCamera;
 int recoveryStage;float recoveryClock;
 public override int? RenderOrder=>1200;
 void CaptureRecoverySpawn(){if(Player is {} player){initialSpawn=player.Transform.WorldPosition;initialRotation=player.Transform.WorldRotation;}}
 void ReturnToRecoverySpawn()
 {
  if(Player is not {} player)return;
  player.Transform.WorldPosition=UseCustomRecoverySpawn?RecoverySpawnPosition:initialSpawn;
  player.Transform.WorldRotation=initialRotation;
  if(player.GetComponent<CharacterController3D>() is {} motor){motor.CancelBurstMotion();motor.SetVelocity(Vector3.Zero);motor.Move(Vector3.Zero);}
 }
 void BeginRecovery()
 {
  if(!AutomaticDefeatRecovery||Recovering)return;
  recoveryStage=1;recoveryClock=0;RecoveryOpacity=0;
  if(Player?.GetComponent<PlayerController3D>() is {} control){recoveryMovement=control.Enabled;control.Enabled=false;}
  if(Player?.GetComponent<MiningThirdPerson3D>() is {} camera){recoveryCamera=camera.Enabled;camera.Enabled=false;}
  if(Player?.GetComponent<CharacterController3D>() is {} motor){motor.CancelBurstMotion();motor.SetVelocity(Vector3.Zero);motor.Move(Vector3.Zero);}
 }
 public void TickRecovery(float delta)
 {
  if(!Recovering||!float.IsFinite(delta)||delta<=0)return;
  recoveryClock+=delta;float duration=Math.Max(.05f,DefeatFadeSeconds);
  if(recoveryStage==1){RecoveryOpacity=Math.Clamp(recoveryClock/duration,0,1);if(recoveryClock>=duration){Retry();recoveryStage=2;recoveryClock=0;}}
  else if(recoveryStage==2){RecoveryOpacity=1;if(recoveryClock>=Math.Max(0,DefeatBlackSeconds)){recoveryStage=3;recoveryClock=0;}}
  else{RecoveryOpacity=1-Math.Clamp(recoveryClock/duration,0,1);if(recoveryClock>=duration){recoveryStage=0;RecoveryOpacity=0;if(Player?.GetComponent<PlayerController3D>() is {} control)control.Enabled=recoveryMovement;if(Player?.GetComponent<MiningThirdPerson3D>() is {} camera)camera.Enabled=recoveryCamera;}}
 }
 protected override void OnRender(RenderContext context)
 {
  if(RecoveryOpacity>0)context.DrawUiQuad(Vector2.Zero,new(context.TargetWidth,context.TargetHeight),new(0,0,0,RecoveryOpacity));
 }
}
