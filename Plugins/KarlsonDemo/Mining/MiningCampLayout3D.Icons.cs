using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningCampLayout3D
{
 readonly List<(Guid Id,Vector2 Centre)> campIconHits=[];
 internal static string CampIcon(GameObject obj)
 {
  if(obj.GetComponent<MiningDefense3D>() is {} d)return d.Kind switch{MiningDefenseKind.Mine=>"mine",MiningDefenseKind.Mortar=>"mortar",MiningDefenseKind.Sentry or MiningDefenseKind.Repeater=>"turret",_=>"crate"};
  string name=(obj.GetComponent<MiningCampObject3D>()?.DisplayName??obj.Name).ToLowerInvariant();
  return name.Contains("barricade")?"shield":name.Contains("ammo")?"ammo":name.Contains("tent")?"company":name.Contains("drill")?"drill":"gear";
 }
 bool PickCampIcon()
 {
  if(Selected!=null||!Input.IsMouseButtonPressedForUi(MouseButton.Left))return false;
  float s=Math.Min(Input.GameViewSize.X/1280,Input.GameViewSize.Y/720);if(s<=0)return false;var point=Input.GameViewMousePosition/s;
  foreach(var hit in campIconHits)if(Math.Abs(point.X-hit.Centre.X)<=14&&Math.Abs(point.Y-hit.Centre.Y)<=14)return BeginMove(hit.Id);
  return false;
 }
 void DrawCampIcons(RenderContext c,Camera3D camera,float s,float width,float height)
 {
  campIconHits.Clear();if(!AnimationRuntimeAssets.TryGet(out var assets)||assets==null)return;
  var vp=camera.GetRenderViewMatrix()*camera.GetProjectionMatrix(c.TargetWidth/(float)c.TargetHeight);
  foreach(var obj in Objects.OrderBy(o=>o.Id==SelectedObjectId?0:1)){
   var clip=Vector4.Transform(new Vector4(obj.Transform.WorldPosition+Vector3.UnitY*.2f,1),vp);if(clip.W<=0)continue;
   var origin=new Vector2((clip.X/clip.W+1)*width*.5f,(1-clip.Y/clip.W)*height*.5f);if(origin.X<20||origin.X>width-20||origin.Y<80||origin.Y>height-110)continue;
   var p=origin;bool Clear(Vector2 candidate)=>campIconHits.All(h=>Vector2.DistanceSquared(h.Centre,candidate)>30*30);
   for(int ring=1;ring<=5&&!Clear(p);ring++)for(int i=0;i<8;i++){var candidate=origin+new Vector2(MathF.Cos(i*MathF.PI/4),MathF.Sin(i*MathF.PI/4))*ring*30;if(candidate.X<20||candidate.X>width-20||candidate.Y<80||candidate.Y>height-110||!Clear(candidate))continue;p=candidate;break;}
   if(!Clear(p))continue;campIconHits.Add((obj.Id,p));
   var color=obj.Id==SelectedObjectId?new Vector4(1,.79f,.12f,1):new Vector4(.94f,.95f,.91f,1);
   if(Vector2.DistanceSquared(p,origin)>10)c.DrawUiLine(origin*s,p*s,s,new(.45f,.65f,.75f,.65f));
   c.DrawUiQuad((p-new Vector2(14))*s,new(28*s),new(.025f,.035f,.045f,.94f));
   c.DrawUiImage(assets.LoadTexture(new AssetReference("Assets/UI/Mining/Artwork/research-"+CampIcon(obj)+".png")),(p-new Vector2(10))*s,new(20*s),color);
  }
 }
}
