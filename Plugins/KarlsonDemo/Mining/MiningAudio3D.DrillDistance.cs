using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningAudio3D
{
 [Category("Drill distance"),Description("Full drill volume within this distance from its surface.")]
 public float DrillNearDistance{get;set;}=3;
 [Category("Drill distance"),Description("Drill fades to silence at this distance. Works with mono and stereo recordings.")]
 public float DrillFarDistance{get;set;}=65;
 public float DrillDistanceGain(float distance)
 {
  float near=Math.Max(0,float.IsFinite(DrillNearDistance)?DrillNearDistance:3),far=Math.Max(near+.1f,float.IsFinite(DrillFarDistance)?DrillFarDistance:65);
  float t=Math.Clamp((distance-near)/(far-near),0,1);return 1-t*t*(3-2*t);
 }
 void TickDrillLoop(GameObject? drill,GameObject? player,bool running,float dt)
 {
  float distance=drill!=null&&player!=null?MiningCampGeometry.SurfaceDistance(drill,player.Transform.WorldPosition):DrillFarDistance;
  // OpenAL doesn't attenuate stereo buffers. Apply one explicit player-distance
  // envelope and leave this loop non-spatial to avoid double attenuation on mono.
  SetLoop("Drill",DrillLoop,drill,running,MachineVolume*DrillVolumeMultiplier*DrillDistanceGain(distance),dt,false);
 }
}
