using System.Numerics;
using System.ComponentModel;
using ByteEngine.Core.Gameplay;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningSpiderAnimation3D
{
 [Category("Hit response")]public bool HitResponseEnabled{get;set;}=true;
 public float HitFlashSeconds{get;set;}=.07f;
 public float BodyRecoilDistance{get;set;}=.035f;
 public Vector3 HitFlashColor{get;set;}=new(1,.74f,.28f);
 float hitFlash;Vector3 hitBase,hitRecoil;
 (Vector3? Color,float? Intensity)[] hitMaterials=[];
 [Browsable(false)]public bool HitResponseActive=>hitFlash>0;
 void OnHitFeedback(HealthComponent hp,float amount)
 {
  if(amount>0&&!hp.IsDead&&!dead)spiderAudio?.PlayCue(MiningSoundCue.SpiderHit,Transform.WorldPosition);
  if(!HitResponseEnabled||amount<=0||renderer==null||visual==null||dead)return;
  if(hitFlash<=0){hitBase=visual.Transform.LocalPosition;hitMaterials=new (Vector3?,float?)[renderer.SkinnedMeshCount];for(int i=0;i<hitMaterials.Length;i++){var m=renderer.GetMaterialOverrides(i);hitMaterials[i]=(m.EmissionColor,m.EmissionIntensity);}}
  hitFlash=Math.Clamp(HitFlashSeconds,.03f,.2f);var d=hp.LastDamageDirection;d.Y=0;hitRecoil=d.LengthSquared()>.001f?Vector3.Transform(Vector3.Normalize(d),Quaternion.Inverse(Transform.WorldRotation))*Math.Clamp(BodyRecoilDistance,0,.08f):Vector3.Zero;
 }
 void TickHitFeedback(float dt)
 {
  if(hitFlash<=0)return;hitFlash=Math.Max(0,hitFlash-dt);
  if(hitFlash<=0){RestoreHitFeedback();return;}
  if(renderer!=null)for(int i=0;i<hitMaterials.Length;i++){var m=renderer.GetMaterialOverrides(i);m.EmissionColor=HitFlashColor;m.EmissionIntensity=.9f*(hitFlash/Math.Max(.03f,HitFlashSeconds));}
  if(visual!=null)visual.Transform.LocalPosition=hitBase+hitRecoil*MathF.Sin(Math.Clamp(1-hitFlash/Math.Max(.03f,HitFlashSeconds),0,1)*MathF.PI);
 }
 void RestoreHitFeedback()
 {
  if(renderer!=null)for(int i=0;i<hitMaterials.Length;i++){var m=renderer.GetMaterialOverrides(i);m.EmissionColor=hitMaterials[i].Color;m.EmissionIntensity=hitMaterials[i].Intensity;}
  if(visual!=null&&hitMaterials.Length>0)visual.Transform.LocalPosition=hitBase;hitMaterials=[];hitFlash=0;
 }
}
