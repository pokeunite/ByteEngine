using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Real basin-height wet shading on selected renderers, including merged architecture.</summary>
public sealed class WetSurfaceContact3D : Component
{
 public string BasinNames {get;set;}="";
 public string RendererRoots {get;set;}="";
 public float Margin {get;set;}=.65f;
 public float Strength {get;set;}=.55f;
 public float SeaLevel {get;set;}=-10000;
 WaterBasin3D?[] basins=[];
 readonly List<(MeshRenderer r,Vector4? a,Vector4? b,Vector4? c,Vector4? levels)> renderers=[];
 protected override void OnStart(){
  var s=GameObject.Scene!;basins=BasinNames.Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Take(3).Select(n=>s.FindGameObject(n)?.GetComponent<WaterBasin3D>()).ToArray();
  var roots=RendererRoots.Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(s.FindGameObject).Where(o=>o!=null).ToArray();
  foreach(var r in s.GameObjects.SelectMany(o=>o.Components).OfType<MeshRenderer>()){
   bool included=false;for(GameObject? p=r.GameObject;p!=null;p=p.Parent)if(roots.Contains(p)){included=true;break;}if(!included)continue;
   var m=r.MaterialOverrides;renderers.Add((r,m.WetContact0,m.WetContact1,m.WetContact2,m.WetLevels));
  }
 }
 protected override void OnLateUpdate(){
  Vector4 Rect(int i)=>i<basins.Length&&basins[i] is {} b?new(b.MinX-Margin,b.MaxX+Margin,b.CenterZ-b.Depth/2-Margin,b.CenterZ+b.Depth/2+Margin):new(-10000,-9999,-10000,-9999);
  float Level(int i)=>i<basins.Length&&basins[i] is {} b&&b.Level-b.Floor>.004f?b.Level:-10000;
  var a=Rect(0);var levels=new Vector4(Level(0),Level(1),Level(2),Strength);
  if(basins.Length==0){a=new(-1000,1000,-1000,1000);levels.X=SeaLevel;}
  foreach(var (r,_,_,_,_) in renderers){var m=r.MaterialOverrides;m.WetContact0=a;m.WetContact1=Rect(1);m.WetContact2=Rect(2);m.WetLevels=levels;}
 }
 protected override void OnStop(){foreach(var(r,a,b,c,l)in renderers){var m=r.MaterialOverrides;m.WetContact0=a;m.WetContact1=b;m.WetContact2=c;m.WetLevels=l;}renderers.Clear();}
}
