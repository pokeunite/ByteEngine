using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 public AssetReference FieldHudFont{get;set;}=new("Assets/UI/Mining/FieldHud/Fonts/Inter-SemiBold.ttf");
 public AssetReference FieldHudBoldFont{get;set;}=new("Assets/UI/Mining/FieldHud/Fonts/Inter-Bold.ttf");
 internal void FieldImage(RenderContext c,string name,float x,float y,float w,float h,float scale)
 {
  if(AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null)c.DrawUiImage(assets.LoadTexture(new AssetReference("Assets/UI/Mining/FieldHud/"+name+".png")),new(x*scale,y*scale),new(w*scale,h*scale),Vector4.One);
 }
 internal void FieldText(RenderContext c,string text,float x,float y,int size,float scale,bool bold=false,UiAnchor anchor=UiAnchor.TopLeft,Vector4? color=null)=>c.DrawUiText(text,bold?FieldHudBoldFont:FieldHudFont,(int)(size*scale),new(x*scale,y*scale),color??new Vector4(.96f,.95f,.91f,1),anchor);
 internal void FieldHint(RenderContext c,string text,float x,float y,float width,float scale){FieldImage(c,"hint-border",x,y,width,30,scale);FieldText(c,text,x+width/2,y+6,16,scale,true,UiAnchor.TopCenter);}
}
