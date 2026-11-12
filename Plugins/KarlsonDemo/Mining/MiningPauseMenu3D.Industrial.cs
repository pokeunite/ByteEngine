using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Runtime;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningPauseMenu3D
{
 void BuildIndustrial(float w,float h,bool interactive)
 {
  items.Clear();if(MainMenuMode&&Page==MiningPausePage.Pause){BuildMain(w,h);return;}
  if(Page is MiningPausePage.Pause or MiningPausePage.Quit or MiningPausePage.NewCompany)
  {
   bool pause=Page==MiningPausePage.Pause;float sw=pause?280:420,sh=pause?330:270,x=(w-sw)/2,y=(h-sh)/2;
   void B(string t,float yy,Action run)=>items.Add(new(t,x+18,y+yy,sw-36,36,run));
   if(pause){B("Resume",74,Resume);B("Settings",118,()=>ShowPage(MiningPausePage.Sound));B("Screenshot [F12]",162,TakeScreenshot);B("Quit",206,()=>ShowPage(MiningPausePage.Quit));}
   else{B(Page==MiningPausePage.Quit?"Quit to desktop":"New game",146,()=>{if(Page==MiningPausePage.NewCompany)StartNewCompany();else{SaveSettings();if(!MainMenuMode)foreach(var op in GameObject.Scene!.GetComponentsSnapshot<MiningOperation3D>())op.SaveProgress();GameObject.Scene!.RequestQuit();}});B("Cancel",194,()=>ShowPage(MiningPausePage.Pause));}return;
  }
  BuildSettings(w,h,interactive);

 }
 void ApplyDisplaySettings(){if(!OperatingSystem.IsBrowser())GameWindowDisplay.Request(selectedWindowMode);SaveSettings();Status="Settings applied.";}
 void RenderIndustrial(RenderContext c)
 {
  if(!IsOpen)return;if(MainMenuMode&&Page==MiningPausePage.Pause){RenderMain(c);return;}
  float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),w=c.TargetWidth/s,h=c.TargetHeight/s;
  void T(string t,float x,float y,int size=17,bool bold=false,Vector4? color=null)=>SupportUiArt.T(c,s,t,x,y,size,bold,color);
  void L(float x,float y,float xx,float yy)=>SupportUiArt.L(c,s,x,y,xx,yy);
  void Panel(float x,float y,float ww,float hh)=>SupportUiArt.Panel(c,s,x,y,ww,hh,accent:true);
  SupportUiArt.Q(c,s,0,0,w,h,new(0,0,0,.36f));
  if(Page is MiningPausePage.Pause or MiningPausePage.Quit or MiningPausePage.NewCompany)
  {
   bool pause=Page==MiningPausePage.Pause;float sw=pause?280:420,sh=pause?330:270,x=(w-sw)/2,y=(h-sh)/2;Panel(x,y,sw,sh);T(pause?"Paused":Page==MiningPausePage.Quit?"Quit game?":"New game?",x+22,y+17,27,true);L(x+12,y+58,x+sw-12,y+58);
   if(pause){L(x+12,y+263,x+sw-12,y+263);T("[Esc] Resume",x+67,y+284,16);}else T(Page==MiningPausePage.Quit?"Progress is saved automatically.":"Your previous save will be backed up.",x+22,y+94,16);
  }
  else RenderSettingsHeading(c,s,w,h);
  sliderDraw.Clear();BuildIndustrial(w,h,false);bool anyHover=items.Any(a=>Inside(a.X,a.Y,a.W,a.H));
  for(int i=0;i<items.Count;i++)
  {
   var a=items[i];if(a.Overlay)continue;bool active=a.Selected||Inside(a.X,a.Y,a.W,a.H)||!anyHover&&focus==i;var parts=a.Label.Split('|');
   if(parts.Length>1)
   {
    bool controlsPage=Page==MiningPausePage.Controls;
    if(active)SupportUiArt.Panel(c,s,a.X,a.Y,a.W,a.H,new(.12f,.12f,.09f,1),SupportUiArt.Gold);
    else L(a.X,a.Y+a.H,a.X+a.W,a.Y+a.H);
    T(parts[0],a.X+6,a.Y+10,18);
    if(controlsPage){bool recording=parts[1].StartsWith("Press");SupportUiArt.Panel(c,s,a.X+a.W-164,a.Y+3,154,30,recording?new(.23f,.18f,.05f,1):new(.13f,.14f,.13f,1),recording?SupportUiArt.Gold:SupportUiArt.Edge);SupportUiArt.T(c,s,parts[1],a.X+a.W-87,a.Y+9,15,true,recording?SupportUiArt.Gold:SupportUiArt.White,UiAnchor.TopCenter);}
    else{SupportUiArt.Panel(c,s,a.X+a.W-256,a.Y,256,a.H,new(.12f,.13f,.12f,1));T(parts[1],a.X+a.W-242,a.Y+11,17);T("v",a.X+a.W-22,a.Y+11,17);}
   }
   else if(IsSettingsTab(a)){T(a.Label,a.X+18,a.Y+8,18,true,active?SupportUiArt.White:SupportUiArt.Muted);if(a.Selected)SupportUiArt.Q(c,s,a.X,a.Y+a.H-3,a.W,3,SupportUiArt.Gold);}
   else{SupportUiArt.Panel(c,s,a.X,a.Y,a.W,a.H,active?SupportUiArt.Gold:new(.09f,.10f,.095f,1),active?SupportUiArt.Gold:SupportUiArt.Edge);T(a.Label,a.X+16,a.Y+9,a.H<=32?14:18,true,active?new(.045f,.05f,.045f,1):SupportUiArt.White);}
  }
  foreach(var a in sliderDraw){float labelX=(w-620)/2+28;T(a.Label,labelX,a.Y,17,true);SupportUiArt.Q(c,s,a.X,a.Y+10,a.W,5,new(.3f,.31f,.3f,1));SupportUiArt.Q(c,s,a.X,a.Y+10,a.W*a.Value,5,SupportUiArt.Gold);SupportUiArt.Image(c,s,"support-knob",a.X+a.W*a.Value-7,a.Y+5,15,15);T(Math.Round(a.Label=="Render scale"?50+a.Value*50:a.Value*100)+"%",a.X+a.W+13,a.Y,15);}
  if(Page==MiningPausePage.Controls){float x=(w-620)/2,y=(h-600)/2;SupportUiArt.Q(c,s,x+597,y+176,4,304,new(.25f,.26f,.25f,1));SupportUiArt.Q(c,s,x+596,y+176+controlsScroll/Math.Max(1,controls.Length+2-ControlVisibleRows)*244,6,60,SupportUiArt.White);}
  foreach(var a in items.Where(a=>a.Overlay)){bool active=a.Selected||Inside(a.X,a.Y,a.W,a.H);SupportUiArt.Panel(c,s,a.X,a.Y,a.W,a.H,active?SupportUiArt.Gold:new(.10f,.11f,.10f,1));T(a.Label,a.X+14,a.Y+9,17,true,active?new(.05f,.05f,.04f,1):SupportUiArt.White);}
  if(Status.Length>0&&Page!=MiningPausePage.Controls)T(Status,w/2-230,h-35,15,true,SupportUiArt.Gold);
 }
}
