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
  if(obj.GetComponent<MiningDefense3D>() is {} d)return d.Kind switch{MiningDefenseKind.Mine=>"support-mine",MiningDefenseKind.Mortar=>"support-mortar",MiningDefenseKind.Sentry or MiningDefenseKind.Repeater=>"support-sentry",_=>"crate"};
  string name=(obj.GetComponent<MiningCampObject3D>()?.DisplayName??obj.Name).ToLowerInvariant();
  return name.Contains("barricade")?"support-barricade":name.Contains("refinery")?"support-refinery":name.Contains("ammo")?"ammo":name.Contains("tent")||name.Contains("research")||name.Contains("r&d")?"support-station":name.Contains("drill")?"drill":"gear";
 }
 bool PickCampIcon()
 {
  if(Selected!=null||!Input.IsMouseButtonPressedForUi(MouseButton.Left))return false;
  float s=Math.Min(Input.GameViewSize.X/1280,Input.GameViewSize.Y/720);if(s<=0)return false;var point=Input.GameViewMousePosition/s;
  foreach(var hit in campIconHits)if(Math.Abs(point.X-hit.Centre.X)<=59&&Math.Abs(point.Y-hit.Centre.Y)<=16)return BeginMove(hit.Id);
  return false;
 }
 void DrawCampIcons(RenderContext c,Camera3D camera,float s,float width,float height)
 {
  campIconHits.Clear();if(!AnimationRuntimeAssets.TryGet(out var assets)||assets==null)return;
  var vp=camera.GetRenderViewMatrix()*camera.GetProjectionMatrix(c.TargetWidth/(float)c.TargetHeight);
  var hovered=PointerGround(out var ground)?Pick(ground)?.Id:Guid.Empty;
  foreach(var obj in Objects.Where(o=>o.Id==SelectedObjectId||o.Id==hovered)){
   var clip=Vector4.Transform(new Vector4((obj.Id==SelectedObjectId?PreviewPosition:obj.Transform.WorldPosition)+Vector3.UnitY*.2f,1),vp);if(clip.W<=0)continue;
   var origin=new Vector2((clip.X/clip.W+1)*width*.5f,(1-clip.Y/clip.W)*height*.5f);if(origin.X<65||origin.X>width-65||origin.Y<85||origin.Y>height-160)continue;
   var p=Vector2.Clamp(origin+new Vector2(65,-42),new(65,85),new(width-65,height-160));bool Clear(Vector2 candidate)=>campIconHits.All(h=>(Math.Abs(h.Centre.X-candidate.X)>124||Math.Abs(h.Centre.Y-candidate.Y)>38));
   for(int ring=1;ring<=5&&!Clear(p);ring++)for(int i=0;i<8;i++){var candidate=origin+new Vector2(MathF.Cos(i*MathF.PI/4),MathF.Sin(i*MathF.PI/4))*ring*30;if(candidate.X<65||candidate.X>width-65||candidate.Y<85||candidate.Y>height-160||!Clear(candidate))continue;p=candidate;break;}
   if(!Clear(p))continue;campIconHits.Add((obj.Id,p));
   var color=obj.Id==SelectedObjectId?new Vector4(1,.79f,.12f,1):new Vector4(.94f,.95f,.91f,1);
   if(Vector2.DistanceSquared(p,origin)>10)c.DrawUiLine(origin*s,p*s,s,new(.45f,.65f,.75f,.65f));
   SupportUiArt.Panel(c,s,p.X-59,p.Y-16,118,32,SupportUiArt.Dark,color);
   SupportUiArt.Image(c,s,CampIcon(obj),p.X-51,p.Y-12,24,24);
   string label=CampIcon(obj) switch{"support-mine"=>"Mine","support-mortar"=>"Mortar","support-sentry"=>"Sentry","support-barricade"=>"Barricade","support-refinery"=>"Refinery","support-station"=>"Station","ammo"=>"Ammo press","drill"=>"Drill",_=>"Support"};
   SupportUiArt.T(c,s,label,p.X-20,p.Y-7,13,true);
  }
 }
}
