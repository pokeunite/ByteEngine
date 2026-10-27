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
  Box(0,0,b.Width,b.Height,Surface);Text("AUDIO CREDITS",24,20,30,Accent);Text("X",697,13,25);
  float y=77;foreach(string line in (MiningAudio3D.Find(GameObject)?.RequiredAttribution??"").Split('\n')){Text(line,24,y,21);y+=31;}
  Text("Other selected recordings are released as CC0 on Freesound.",24,268,20);
  Text("Thanks to unfa, Nanashi, BlondPanda, pointparkcinema, Dominik_W,",24,302,18);
  Text("kkenny101, AslakHostaker, RyanKingArt, mrickey13, Rob_Marion,",24,327,18);
  Text("plasterbrain, CogFireStudios, SamsterBirdies and the alarm contributor.",24,352,18);
  Box(570,385,140,40,PurchaseColor);Text("CLOSE",588,393,22,new(.045f,.05f,.06f,1));
 }
}
