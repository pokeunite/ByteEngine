using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace GoblinCombat;
/// <summary>Native skeletal playback driven by the same clock as melee contact, with a scale-independent hand socket.</summary>
public sealed class CombatAnimation3D:Component {
 public string VisualName {get;set;}="Animated fighter";
 public string SwordName {get;set;}="Equipped sword";
 public string AttackOne {get;set;}="ComboLight1";
 public string AttackTwo {get;set;}="ComboLight2";
 public string AttackThree {get;set;}="AttackTwoHanded";
 public string HandBone {get;set;}="hand.R";
 public Vector3 GripOffset {get;set;}=new(0,.06f,-.02f);
 public Vector3 GripEulerDegrees {get;set;}=new(0,0,90);
 public AssetReference Palette {get;set;}=AssetReference.Empty;
 public bool ApplyPaletteToCharacter {get;set;}
 public string CurrentClip {get;private set;}="";
 public float GripError {get;private set;}
 public override int UpdateOrder=>100;
 CombatActor3D? actor;CharacterController3D? motor;SkeletalMeshRenderer? skin;GameObject? visual,sword;int damageSeen;float reaction;float playbackClock;bool moving;
 protected override void OnStart(){actor=GameObject.GetComponent<CombatActor3D>();motor=GameObject.GetComponent<CharacterController3D>();visual=GameObject.Children.FirstOrDefault(c=>c.Name==VisualName);skin=visual?.GetComponent<SkeletalMeshRenderer>();sword=GameObject.Children.FirstOrDefault(c=>c.Name==SwordName);skin?.ResolveRuntimeResources();if(skin!=null){skin.PlayOnStart=false;skin.BlendFromCurrentPose=true;skin.Pause();}if(skin!=null&&!Palette.IsEmpty){if(ApplyPaletteToCharacter){skin.GetMaterialOverrides(0).BaseColor=Vector4.One;skin.GetMaterialOverrides(0).SetTexture(MaterialTextureSlot.BaseColor,Palette);}if(sword!=null)ApplyWeaponPalette(sword);}}
 void ApplyWeaponPalette(GameObject root){if(root.GetComponent<MeshRenderer>() is {} mesh){mesh.MaterialOverrides.BaseColor=Vector4.One;mesh.MaterialOverrides.SetTexture(MaterialTextureSlot.BaseColor,Palette);}foreach(var child in root.Children)ApplyWeaponPalette(child);}
 void Play(string clip,bool loop,float blend){if(skin==null)return;if(CurrentClip!=clip&&skin.Play(clip,loop,blend)){CurrentClip=clip;playbackClock=0;}skin.Pause();}
 // Sample once, after all gameplay and physics; the renderer never advances a second clock.
 void UpdatePlayback(){if(actor==null||skin==null||visual==null)return;float dt=(float)Time.DeltaTime;if(dt<=0)return;
  if(actor.DamageTaken!=damageSeen){damageSeen=actor.DamageTaken;reaction=.35f;}float step=actor.HitStopped?0:dt;reaction=Math.Max(0,reaction-step);
  if(actor.Health.IsDead){Play("Death",false,.08f);playbackClock+=step;skin.SamplePlayback(Math.Min(skin.Duration,playbackClock),step);return;}
  if(actor.Timing.Attacking){reaction=0;string clip=actor.Timing.Stage==0?AttackOne:actor.Timing.Stage==1?AttackTwo:AttackThree;Play(clip,false,.09f);float total=CombatTiming.Windup[actor.Timing.Stage]+CombatTiming.Contact[actor.Timing.Stage]+CombatTiming.Recovery[actor.Timing.Stage];skin.SamplePlayback(actor.Timing.Elapsed*skin.Duration/total,step);return;}
  if(reaction>0){Play("Damage",false,.06f);playbackClock+=step*2.2f;skin.SamplePlayback(Math.Min(skin.Duration,playbackClock),step);return;}
  float speed=motor?.Speed??0;
  if(!actor.HitStopped)moving=actor.Timing.Dodging||speed>(moving?.12f:.35f);
  string locomotion=moving?"Run":"GuardIdle";Play(locomotion,true,.13f);playbackClock+=step*(moving?Math.Clamp(speed/4.6f,.7f,1.35f):1);skin.SamplePlayback(skin.Duration>0?playbackClock%skin.Duration:0,step);
 }
 protected override void OnLateUpdate(){UpdatePlayback();if(skin==null||sword==null)return;if(skin.TryGetBoneWorldMatrix(HandBone,out var hand)){Matrix4x4.Decompose(hand,out _,out var boneRotation,out var bonePosition);var radians=GripEulerDegrees*(MathF.PI/180);var rotation=Quaternion.CreateFromYawPitchRoll(radians.Y,radians.X,radians.Z);sword.Transform.WorldPosition=bonePosition+Vector3.Transform(GripOffset,boneRotation);sword.Transform.WorldRotation=Quaternion.Normalize(boneRotation*rotation);sword.Transform.WorldScale=Vector3.One;GripError=Vector3.Distance(bonePosition+Vector3.Transform(GripOffset,boneRotation),sword.Transform.WorldPosition);}}
}



