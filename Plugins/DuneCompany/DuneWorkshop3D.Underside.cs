using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 readonly Dictionary<MeshRenderer,(Mesh Original,Mesh? Cutaway,bool Floor,float Level,bool Visible,bool Lod)> _garageSurfaces=[];
 readonly Dictionary<Component,bool> _hiddenTerrain=[];
 readonly HashSet<MeshRenderer> _nonCutawaySurfaces=[];
 void RestoreGroundVisibility(){
  foreach(var (renderer,surface) in _garageSurfaces){renderer.Mesh=surface.Original;renderer.Visible=surface.Visible;renderer.AutomaticModelLod=surface.Lod;}
  foreach(var (component,visible) in _hiddenTerrain)SetTerrainVisible(component,visible);
  _hiddenTerrain.Clear();
 }
 static bool TerrainVisible(Component c)=>c is InteractiveSand3D sand?sand.Visible:(bool?)(c.GetType().GetProperty("Visible")?.GetValue(c))??true;
 static void SetTerrainVisible(Component c,bool visible){if(c is InteractiveSand3D sand)sand.Visible=visible;else c.GetType().GetProperty("Visible")?.SetValue(c,visible);}
 void ReleaseGarageCutaways(){RestoreGroundVisibility();foreach(var surface in _garageSurfaces.Values)surface.Cutaway?.Dispose();_garageSurfaces.Clear();_nonCutawaySurfaces.Clear();}
 static string SurfaceName(MeshRenderer renderer){string name=renderer.MaterialReference?.SubAssetKey??"";for(var obj=renderer.GameObject;obj!=null;obj=obj.Parent)name+=" "+obj.Name;return name.ToLowerInvariant();}
 void UpdateUndersideVisibility(Vector3 camera){
  RestoreGroundVisibility();
  var scene=GameObject.Scene!;
  if(_sand!=null&&_sand.Sample(camera,out var ground,out _,out _,out _)&&camera.Y<ground.Y+.12f){
   foreach(var c in scene.GameObjects.SelectMany(o=>o.Components).Where(c=>c is InteractiveSand3D||c.GetType().FullName=="DesertTerrain.DesertTerrain3D")){_hiddenTerrain[c]=TerrainVisible(c);SetTerrainVisible(c,false);}
  }
  foreach(var renderer in scene.GameObjects.SelectMany(o=>o.Components).OfType<MeshRenderer>()){
   if(_garageSurfaces.ContainsKey(renderer)||_nonCutawaySurfaces.Contains(renderer)||renderer.Mesh==null)continue;
   string name=SurfaceName(renderer);bool floor=name.Contains("concrete floor")||name.Contains("garage floor");
   bool roof=name.Contains("roof")||name.Contains("ceiling")||name.Contains("corrugated walls")||name.Contains("structural frame")||name.Contains("workshop storage shelving");
   if(!floor&&!roof&&!name.Contains("garage environment")){_nonCutawaySurfaces.Add(renderer);continue;}
   var mesh=renderer.Mesh;var vertices=mesh.VertexData.Span;float level=float.MinValue,baseY=float.MaxValue;
   for(int i=0;i<vertices.Length;i+=8){float y=Vector3.Transform(new(vertices[i],vertices[i+1],vertices[i+2]),renderer.Transform.WorldMatrix).Y;level=Math.Max(level,y);baseY=Math.Min(baseY,y);}
   if(!floor&&!roof&&level-baseY<.5f&&mesh.LocalBounds.Size.Length()>3){roof=baseY>3;floor=level<1;}
   if(!floor&&!roof){_nonCutawaySurfaces.Add(renderer);continue;}
   Mesh? cutaway=null;
   if(roof){
    // The garage GLB combines its roof and walls. Keep the lower walls visible.
    var indices=mesh.IndexData.Span;var kept=new List<uint>(indices.Length);
    for(int i=0;i<indices.Length;i+=3){float y=0;for(int k=0;k<3;k++){int v=(int)indices[i+k]*8;y+=Vector3.Transform(new(vertices[v],vertices[v+1],vertices[v+2]),renderer.Transform.WorldMatrix).Y;}if(y/3<level-.75f){kept.Add(indices[i]);kept.Add(indices[i+1]);kept.Add(indices[i+2]);}}
    if(kept.Count!=indices.Length)cutaway=new Mesh(mesh.VertexData.ToArray(),kept.ToArray());
   }
   _garageSurfaces[renderer]=(mesh,cutaway,floor,level,renderer.Visible,renderer.AutomaticModelLod);
  }
  foreach(var (renderer,surface) in _garageSurfaces){
   if(surface.Floor&&camera.Y<surface.Level+.12f)renderer.Visible=false;
   else if(!surface.Floor&&camera.Y>=surface.Level-.45f){if(surface.Cutaway!=null){renderer.Mesh=surface.Cutaway;renderer.AutomaticModelLod=false;}else renderer.Visible=false;}
  }
 }
}
