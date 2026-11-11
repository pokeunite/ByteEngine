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
  if(Page==MiningPausePage.Settings)Page=MiningPausePage.Sound;
  if(Page is MiningPausePage.Sound or MiningPausePage.Controls)
  {
   float x=(w-850)/2,y=(h-450)/2;
   for(int i=0;i<4;i++){var bus=(AudioBus)i;Slider(new[]{"Master","Music","Effects","UI"}[i],x+118,y+91+i*70,152,AudioMixer.GetVolume(bus),v=>AudioMixer.SetVolume(bus,v),interactive);}
   int start=(int)controlsScroll;
   for(int row=0;row<7;row++){
    int index=start+row;if(index>=controls.Length+2)break;int item=index;
    string label=index<controls.Length?controls[index].Label:index==controls.Length?"Fire / place":"Aim / cancel";
    string value=index<controls.Length?(listening==controls[index].Key?"Press key":KeyLabel(controls[index].Key)):(listeningMouse==(index==controls.Length?MouseButton.Left:MouseButton.Right)?"Press button":Input.GameplayMouseButton(index==controls.Length?MouseButton.Left:MouseButton.Right).ToString());
    items.Add(new(label+"|"+value,x+364,y+61+row*40,458,36,()=>{if(item<controls.Length)listening=controls[item].Key;else listeningMouse=item==controls.Length?MouseButton.Left:MouseButton.Right;listenAt=Time.TotalTime;}));
   }
   items.Add(new("Reset audio",x+20,y+350,130,30,()=>{foreach(var bus in Enum.GetValues<AudioBus>())AudioMixer.SetVolume(bus,1);SaveSettings();}));
   items.Add(new("Reset keys",x+364,y+350,130,30,()=>{Input.ClearGameplayKeyBindings();SaveSettings();}));
   items.Add(new("Graphics",x+210,y+408,200,38,()=>ShowPage(MiningPausePage.Graphics)));items.Add(new("Back",x+440,y+408,200,38,()=>{SaveSettings();ShowPage(MiningPausePage.Pause);}));
   if(interactive&&down&&Inside(x+828,y+61,18,280))controlsScroll=Math.Clamp((pointer.Y-y-61)/280*Math.Max(0,controls.Length+2-7),0,Math.Max(0,controls.Length+2-7));
   return;
  }
  if(Page==MiningPausePage.Graphics)
  {
   float x=(w-620)/2,y=(h-490)/2;
   items.Add(new("Window mode|"+(OperatingSystem.IsBrowser()?"Browser":selectedWindowMode.ToString()),x+24,y+72,572,42,()=>{if(OperatingSystem.IsBrowser()){Status="Use browser fullscreen controls.";return;}windowDropdownOpen=!windowDropdownOpen;}));
   items.Add(new("Quality|"+(graphics?.Quality.ToString()??"High"),x+24,y+130,572,42,()=>{if(graphics!=null)graphics.Quality=graphics.Quality==GraphicsQuality.Fast?GraphicsQuality.Balanced:graphics.Quality==GraphicsQuality.Balanced?GraphicsQuality.High:GraphicsQuality.Fast;}));
   Slider("Render scale",x+260,y+207,265,((graphics?.RenderScale??1)-.5f)*2,v=>{if(graphics!=null)graphics.RenderScale=.5f+v*.5f;},interactive&&!windowDropdownOpen);
   Slider("Look sensitivity",x+260,y+272,265,(MouseSensitivity-.02f)/.38f,v=>{MouseSensitivity=.02f+v*.38f;ApplySensitivity();},interactive&&!windowDropdownOpen);
   Slider("Camera shake",x+260,y+337,265,CameraShakeIntensity,v=>{CameraShakeIntensity=v;ApplyShake();},interactive&&!windowDropdownOpen);
   items.Add(new("Apply",x+92,y+418,200,40,ApplyDisplaySettings,true));items.Add(new("Back",x+310,y+418,200,40,()=>{SaveSettings();ShowPage(MiningPausePage.Sound);}));
   if(windowDropdownOpen)foreach(var mode in Enum.GetValues<GameWindowMode>()){var option=mode;items.Add(new(mode.ToString(),x+340,y+112+(int)mode*30,256,30,()=>{selectedWindowMode=option;windowDropdownOpen=false;},selectedWindowMode==mode,true));}
  }
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
  else if(Page==MiningPausePage.Graphics){float x=(w-620)/2,y=(h-490)/2;Panel(x,y,620,490);T("Graphics",x+24,y+16,27,true);L(x+12,y+56,x+608,y+56);L(x+12,y+397,x+608,y+397);}
  else{float x=(w-850)/2,y=(h-450)/2;Panel(x,y,330,390);Panel(x+346,y,504,390);T("Audio",x+20,y+15,27,true);T("Controls",x+366,y+15,27,true);L(x+12,y+55,x+318,y+55);L(x+358,y+55,x+838,y+55);}
  sliderDraw.Clear();BuildIndustrial(w,h,false);bool anyHover=items.Any(a=>Inside(a.X,a.Y,a.W,a.H));
  for(int i=0;i<items.Count;i++)
  {
   var a=items[i];if(a.Overlay)continue;bool active=a.Selected||Inside(a.X,a.Y,a.W,a.H)||!anyHover&&focus==i;var parts=a.Label.Split('|');
   if(parts.Length>1)
   {
    bool controlsPage=Page is MiningPausePage.Sound or MiningPausePage.Controls;
    if(active)SupportUiArt.Panel(c,s,a.X,a.Y,a.W,a.H,new(.12f,.12f,.09f,1),SupportUiArt.Gold);
    else L(a.X,a.Y+a.H,a.X+a.W,a.Y+a.H);
    T(parts[0],a.X+6,a.Y+10,controlsPage?15:18);
    if(controlsPage){SupportUiArt.Panel(c,s,a.X+a.W-213,a.Y+5,101,26,new(.15f,.16f,.15f,1));T(parts[1],a.X+a.W-206,a.Y+10,13,true);SupportUiArt.Panel(c,s,a.X+a.W-103,a.Y+5,96,26,new(.13f,.14f,.13f,1));T("Listen",a.X+a.W-79,a.Y+10,13);}
    else{SupportUiArt.Panel(c,s,a.X+a.W-256,a.Y,256,a.H,new(.12f,.13f,.12f,1));T(parts[1],a.X+a.W-242,a.Y+11,17);T("v",a.X+a.W-22,a.Y+11,17);}
   }
   else{SupportUiArt.Panel(c,s,a.X,a.Y,a.W,a.H,active?SupportUiArt.Gold:new(.09f,.10f,.095f,1),active?SupportUiArt.Gold:SupportUiArt.Edge);T(a.Label,a.X+16,a.Y+9,a.H<=32?14:18,true,active?new(.045f,.05f,.045f,1):SupportUiArt.White);}
  }
  foreach(var a in sliderDraw){float labelX=Page==MiningPausePage.Graphics?(w-620)/2+24:(w-850)/2+20;T(a.Label,labelX,a.Y,17,true);SupportUiArt.Q(c,s,a.X,a.Y+10,a.W,5,new(.3f,.31f,.3f,1));SupportUiArt.Q(c,s,a.X,a.Y+10,a.W*a.Value,5,SupportUiArt.Gold);SupportUiArt.Image(c,s,"support-knob",a.X+a.W*a.Value-7,a.Y+5,15,15);T(Math.Round(a.Label=="Render scale"?50+a.Value*50:a.Value*100)+"%",a.X+a.W+13,a.Y,15);}
  if(Page is MiningPausePage.Sound or MiningPausePage.Controls){float x=(w-850)/2,y=(h-450)/2;SupportUiArt.Q(c,s,x+835,y+61,4,280,new(.25f,.26f,.25f,1));SupportUiArt.Q(c,s,x+834,y+61+controlsScroll/Math.Max(1,controls.Length+2-7)*215,6,65,SupportUiArt.White);}
  foreach(var a in items.Where(a=>a.Overlay)){bool active=a.Selected||Inside(a.X,a.Y,a.W,a.H);SupportUiArt.Panel(c,s,a.X,a.Y,a.W,a.H,active?SupportUiArt.Gold:new(.10f,.11f,.10f,1));T(a.Label,a.X+14,a.Y+9,17,true,active?new(.05f,.05f,.04f,1):SupportUiArt.White);}
  if(Status.Length>0)T(Status,w/2-230,h-35,15,true,SupportUiArt.Gold);
 }
}
