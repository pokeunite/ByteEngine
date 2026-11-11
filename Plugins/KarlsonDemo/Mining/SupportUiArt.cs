using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
internal static class SupportUiArt
{
 internal static readonly AssetReference Font=new("Assets/UI/Mining/FieldHud/Fonts/Inter-SemiBold.ttf"),Bold=new("Assets/UI/Mining/FieldHud/Fonts/Inter-Bold.ttf"),Logo=new("Assets/UI/Mining/FieldHud/Fonts/Inter-BoldItalic.ttf");
 internal static readonly Vector4 White=new(.96f,.95f,.91f,1),Gold=new(1,.79f,.12f,1),Muted=new(.64f,.66f,.65f,1),Dark=new(.07f,.075f,.075f,.96f),Edge=new(.72f,.71f,.65f,1);
 internal static void Q(RenderContext c,float s,float x,float y,float w,float h,Vector4 color)=>c.DrawUiQuad(new(x*s,y*s),new(w*s,h*s),color);
 internal static void L(RenderContext c,float s,float x,float y,float xx,float yy,Vector4? color=null)=>c.DrawUiLine(new(x*s,y*s),new(xx*s,yy*s),s,color??Edge);
 internal static void T(RenderContext c,float s,string t,float x,float y,int size=17,bool bold=false,Vector4? color=null,UiAnchor anchor=UiAnchor.TopLeft)=>c.DrawUiText(t,bold?Bold:Font,Math.Max(8,(int)(size*s)),new(x*s,y*s),color??White,anchor);
 internal static void Image(RenderContext c,float s,string icon,float x,float y,float w,float h){if(AnimationRuntimeAssets.TryGet(out var a)&&a!=null)c.DrawUiImage(a.LoadTexture(new AssetReference("Assets/UI/Mining/FieldHud/"+icon+".png")),new(x*s,y*s),new(w*s,h*s),Vector4.One);}
 internal static void Panel(RenderContext c,float s,float x,float y,float w,float h,Vector4? fill=null,Vector4? edge=null,bool accent=false)
 {
  Q(c,s,x,y+7,w,h-14,fill??Dark);for(int i=0;i<7;i++){Q(c,s,x+7-i,y+i,w-14+2*i,1,fill??Dark);Q(c,s,x+7-i,y+h-1-i,w-14+2*i,1,fill??Dark);}
  L(c,s,x+7,y,x+w-7,y,edge);L(c,s,x+w-7,y,x+w,y+7,edge);L(c,s,x+w,y+7,x+w,y+h-7,edge);L(c,s,x+w,y+h-7,x+w-7,y+h,edge);L(c,s,x+w-7,y+h,x+7,y+h,edge);L(c,s,x+7,y+h,x,y+h-7,edge);L(c,s,x,y+h-7,x,y+7,edge);L(c,s,x,y+7,x+7,y,edge);
  if(accent)for(int i=0;i<10;i++)Q(c,s,x+w-13+i,y+4+i,10-i,1,Gold);
 }
 internal static string KindIcon(MiningDefenseKind k)=>k switch{MiningDefenseKind.Mine=>"support-mine",MiningDefenseKind.Barricade=>"support-barricade",MiningDefenseKind.Sentry or MiningDefenseKind.Repeater=>"support-sentry",MiningDefenseKind.Mortar=>"support-mortar",MiningDefenseKind.ExtraRefinery=>"support-refinery",MiningDefenseKind.ResearchStation=>"support-station",_=>"cargo"};
 internal static string KindLabel(MiningDefenseKind k)=>k switch{MiningDefenseKind.Mine=>"Mine",MiningDefenseKind.Barricade=>"Barricade",MiningDefenseKind.Sentry=>"Sentry",MiningDefenseKind.Repeater=>"Rotary",MiningDefenseKind.Mortar=>"Mortar",MiningDefenseKind.ExtraRefinery=>"Refinery",MiningDefenseKind.ResearchStation=>"Station",_=>"Supply"};
}
