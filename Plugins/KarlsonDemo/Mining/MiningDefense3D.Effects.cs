using System.Numerics;
using ByteEngine.Core.Vfx;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningDefense3D
{
 VfxPlayer? mortarSmoke;
 void ShowShotEffects(Vector3 origin,Vector3 target,bool mortar=false)
 {
  var effects=MiningEffects3D.Find(GameObject);if(effects==null)return;
  effects.Emit(MiningEffectCue.TurretMuzzle,origin,target-origin,mortar?effects.TurretFlashScale*1.5f:effects.TurretFlashScale);
  if(!mortar){flash=0;if(beam!=null)beam.Active=false;}
  else if(shell!=null&&!effects.MortarTrail.IsEmpty){mortarSmoke??=shell.AddComponent(new VfxPlayer{Effect=effects.MortarTrail,PlayOnStart=false,Size=.5f,ViewDistance=90});mortarSmoke.Prewarm();mortarSmoke.Play();}
 }
 public void DetonateAt(Vector3 point)=>Blast(point,BlastRadius,BaseDamage);
}
