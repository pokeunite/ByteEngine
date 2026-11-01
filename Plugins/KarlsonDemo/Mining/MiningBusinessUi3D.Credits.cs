using System.Numerics;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 void RenderAudioCredits(RenderContext c,float width,float height,float scale)
 {
  var b=CompactBounds(width,height);
  void Box(float x,float y,float w,float h,Vector4 color)=>c.DrawUiQuad(new((b.X+x)*scale,(b.Y+y)*scale),new(w*scale,h*scale),color);
  void Text(string t,float x,float y,int size=21,Vector4? color=null)=>c.DrawUiText(t,BodyFont,Math.Max(12,(int)(size*scale)),new((b.X+x)*scale,(b.Y+y)*scale),color??TextColor,UiAnchor.TopLeft);
  Box(0,0,b.Width,b.Height,Surface);Text("ASSET CREDITS",24,20,30,Accent);Text("X",697,13,25);
  Text("JUMPSUIT Low Poly — DanlyVostok (@1799danly)",24,67,20,Accent);
  Text("Sketchfab model: ddbd830d325b40ceb3829149de146d63",24,93,17);
  Text("CC BY 4.0 — creativecommons.org/licenses/by/4.0/",24,116,17);
  Text("Modified: fitted skeleton, skin weights and gameplay animations.",24,139,17);
  float y=180;foreach(string line in (MiningAudio3D.Find(GameObject)?.RequiredAttribution??"").Split('\n')){Text(line,24,y,16);y+=22;}
  Text("Other selected recordings: CC0 / Freesound. Full sources: project credits.",24,328,16);
  Text("Thanks to the recording contributors listed with the game assets.",24,351,16);
  Box(570,385,140,40,PurchaseColor);Text("CLOSE",588,393,22,new(.045f,.05f,.06f,1));
 }
}
