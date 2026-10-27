using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningAudio3D
{
 [Category("Footsteps")] public AssetReference Footstep1{get;set;}=AssetReference.Empty;
 public AssetReference Footstep2{get;set;}=AssetReference.Empty;
 public AssetReference Footstep3{get;set;}=AssetReference.Empty;
 public AssetReference Footstep4{get;set;}=AssetReference.Empty;
 public float FootstepVolume{get;set;}=.4f;
 public float WalkStrideDistance{get;set;}=1.2f;
 public float SprintStrideDistance{get;set;}=1.6f;
 [Category("Ammo pickup")] public AssetReference AmmoPickupSound{get;set;}=AssetReference.Empty;
 public float AmmoPickupPitch{get;set;}=1.15f;
 public float AmmoPickupVolume{get;set;}=.5f;
 Vector3 previousStepPosition;bool stepReady;float stepDistance;int stepIndex;
 AssetReference StepClip(){var clips=new[]{Footstep1,Footstep2,Footstep3,Footstep4}.Where(c=>!c.IsEmpty).ToArray();return clips.Length==0?AssetReference.Empty:clips[(stepIndex++)%clips.Length];}
 void TickFootsteps(GameObject? player,float dt)
 {
  if(player==null){stepReady=false;return;}var position=player.Transform.WorldPosition;
  if(!stepReady){previousStepPosition=position;stepReady=true;return;}
  float distance=new Vector2(position.X-previousStepPosition.X,position.Z-previousStepPosition.Z).Length();previousStepPosition=position;
  if(dt<=0||distance>3||player.GetComponent<CharacterController3D>() is not {IsGrounded:true}||ByteEngine.Core.Input.IsUiPointerRequested){stepDistance=0;return;}
  if(distance<.005f)return;stepDistance+=distance;float stride=distance/Math.Max(dt,.001f)>4.5f?SprintStrideDistance:WalkStrideDistance;
  if(stepDistance>=Math.Clamp(stride,.3f,3)){stepDistance=0;PlayCue(MiningSoundCue.Footstep);}
 }
}
