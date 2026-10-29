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
  if(Page is MiningPausePage.Pause or MiningPausePage.Quit or MiningPausePage.NewCompany){
   bool pause=Page==MiningPausePage.Pause;float sw=pause?440:520,sh=pause?360:300,sx=(w-sw)/2,sy=(h-sh)/2;
   void Small(string label,float yy,Action action)=>items.Add(new(label,sx+48,sy+yy,sw-96,44,action));
   if(pause){Small("RESUME",112,Resume);Small("SETTINGS",176,()=>ShowPage(MiningPausePage.Sound));Small("QUIT",240,()=>ShowPage(MiningPausePage.Quit));}
   else{Small(Page==MiningPausePage.Quit?"QUIT TO DESKTOP":"NEW GAME",146,()=>{if(Page==MiningPausePage.NewCompany)StartNewCompany();else{SaveSettings();if(!MainMenuMode)foreach(var op in GameObject.Scene!.GameObjects.SelectMany(o=>o.Components.OfType<MiningOperation3D>()))op.SaveProgress();GameObject.Scene!.RequestQuit();}});Small("CANCEL",210,()=>ShowPage(MiningPausePage.Pause));}return;
  }
  float x=(w-940)/2,y=(h-540)/2;
  void B(string label,float bx,float by,float bw,Action action,bool selected=false)=>items.Add(new(label,bx,by,bw,38,action,selected));
  if(Page==MiningPausePage.Settings)Page=MiningPausePage.Sound;
  B("AUDIO",x+28,y+110,180,()=>ShowPage(MiningPausePage.Sound),Page==MiningPausePage.Sound);
  B("CONTROLS",x+28,y+169,180,()=>ShowPage(MiningPausePage.Controls),Page==MiningPausePage.Controls);
  B("GRAPHICS",x+28,y+228,180,()=>ShowPage(MiningPausePage.Graphics),Page==MiningPausePage.Graphics);
  B("BACK",x+28,y+480,180,()=>{SaveSettings();ShowPage(MiningPausePage.Pause);});
  if(Page==MiningPausePage.Sound){for(int i=0;i<4;i++){var bus=(AudioBus)i;Slider(new[]{"MASTER","MUSIC","EFFECTS","INTERFACE"}[i],x+460,y+130+i*66,350,AudioMixer.GetVolume(bus),v=>AudioMixer.SetVolume(bus,v),interactive);}B("RESET DEFAULTS",x+700,y+480,210,()=>{foreach(var bus in Enum.GetValues<AudioBus>())AudioMixer.SetVolume(bus,1);SaveSettings();});}
  if(Page==MiningPausePage.Controls){int start=(int)controlsScroll;for(int row=0;row<7;row++){int index=start+row;if(index>=controls.Length+2)break;int item=index;string label=index<controls.Length?controls[index].Label: index==controls.Length?"Fire / place defence":"Aim / cancel placement";string value=index<controls.Length?(listening==controls[index].Key?"PRESS A KEY...":KeyLabel(controls[index].Key)):(listeningMouse==(index==controls.Length?MouseButton.Left:MouseButton.Right)?"PRESS BUTTON...":Input.GameplayMouseButton(index==controls.Length?MouseButton.Left:MouseButton.Right).ToString());B(label.ToUpperInvariant()+"|"+value,x+250,y+110+row*43,650,()=>{if(item<controls.Length)listening=controls[item].Key;else listeningMouse=item==controls.Length?MouseButton.Left:MouseButton.Right;listenAt=Time.TotalTime;});}
   B("RESET KEYS",x+700,y+480,210,()=>{Input.ClearGameplayKeyBindings();SaveSettings();});
   if(interactive&&down&&Inside(x+908,y+110,20,300))controlsScroll=Math.Clamp((pointer.Y-y-110)/300*(controls.Length+2-7),0,controls.Length+2-7);
  }
  if(Page==MiningPausePage.Graphics){B("QUALITY|"+(graphics?.Quality.ToString().ToUpperInvariant()??"HIGH"),x+250,y+110,650,()=>{if(graphics!=null)graphics.Quality=graphics.Quality==GraphicsQuality.Fast?GraphicsQuality.Balanced:graphics.Quality==GraphicsQuality.Balanced?GraphicsQuality.High:GraphicsQuality.Fast;});B("WINDOW MODE|"+(OperatingSystem.IsBrowser()?"BROWSER":selectedWindowMode.ToString().ToUpperInvariant()),x+250,y+168,650,()=>{if(OperatingSystem.IsBrowser()){Status="Use your browser's fullscreen controls.";return;}windowDropdownOpen=!windowDropdownOpen;});Slider("RENDER SCALE",x+460,y+260,350,((graphics?.RenderScale??1)-.5f)*2,v=>{if(graphics!=null)graphics.RenderScale=.5f+v*.5f;},interactive&&!windowDropdownOpen);Slider("LOOK SENSITIVITY",x+460,y+340,350,(MouseSensitivity-.02f)/.38f,v=>{MouseSensitivity=.02f+v*.38f;ApplySensitivity();},interactive&&!windowDropdownOpen);B("APPLY",x+700,y+480,210,ApplyDisplaySettings);if(windowDropdownOpen)foreach(var mode in Enum.GetValues<GameWindowMode>()){var option=mode;items.Add(new(mode.ToString().ToUpperInvariant(),x+670,y+210+(int)mode*36,230,36,()=>{selectedWindowMode=option;windowDropdownOpen=false;},selectedWindowMode==mode,true));}}
 }
 void ApplyDisplaySettings(){if(!OperatingSystem.IsBrowser())GameWindowDisplay.Request(selectedWindowMode);SaveSettings();Status="Settings applied.";}
 void RenderIndustrial(RenderContext c)
 {
  if(!IsOpen)return;if(MainMenuMode&&Page==MiningPausePage.Pause){RenderMain(c);return;}
  float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),w=c.TargetWidth/s,h=c.TargetHeight/s,x=(w-940)/2,y=(h-540)/2;
  void Q(float a,float b,float ww,float hh,Vector4 color)=>c.DrawUiQuad(new(a*s,b*s),new(ww*s,hh*s),color);
  void T(string t,float a,float b,int size=20,Vector4? color=null)=>c.DrawUiText(t,BodyFont,(int)(size*s),new(a*s,b*s),color??text,UiAnchor.TopLeft);
  if(Page is MiningPausePage.Pause or MiningPausePage.Quit or MiningPausePage.NewCompany){bool pause=Page==MiningPausePage.Pause;float sw=pause?440:520,sh=pause?360:300,sx=(w-sw)/2,sy=(h-sh)/2;Q(0,0,w,h,new(0,0,0,.28f));Q(sx,sy,sw,sh,surface);Q(sx,sy,12,2,accent);Q(sx+sw-12,sy+sh-2,12,2,accent);T(pause?"PAUSED":Page==MiningPausePage.Quit?"QUIT GAME?":"NEW GAME?",sx+48,sy+30,28);Q(sx+48,sy+79,sw-96,1,new(.28f,.30f,.32f,1));if(!pause)T(Page==MiningPausePage.Quit?"Progress is saved automatically.":"Your previous save will be backed up.",sx+48,sy+100,16,muted);BuildIndustrial(w,h,false);bool hoverAny=items.Any(a=>Inside(a.X,a.Y,a.W,a.H));for(int i=0;i<items.Count;i++){var a=items[i];bool selected=Inside(a.X,a.Y,a.W,a.H)||!hoverAny&&focus==i;Q(a.X,a.Y,a.W,a.H,selected?accent:new(.09f,.10f,.12f,1));T(a.Label,a.X+20,a.Y+11,20,selected?new(.045f,.05f,.055f,1):text);}return;}
  Q(0,0,w,h,new(0,0,0,.32f));Q(x,y,940,540,surface);
  Q(x,y,940,1,new(.32f,.34f,.36f,1));Q(x,y+539,940,1,new(.32f,.34f,.36f,1));
  foreach(float cx in new[]{x,x+928})foreach(float cy in new[]{y,y+538})Q(cx,cy,12,2,accent);
  string title=Page==MiningPausePage.Pause?"PAUSED":Page==MiningPausePage.Quit?"QUIT GAME?":Page==MiningPausePage.NewCompany?"NEW GAME?":"SETTINGS / "+(Page==MiningPausePage.Sound?"AUDIO":Page==MiningPausePage.Controls?"CONTROLS":"GRAPHICS");
  c.DrawUiText(title,HeadingFont,(int)(30*s),new((x+36)*s,(y+35)*s),text,UiAnchor.TopLeft);Q(x+28,y+80,884,1,new(.28f,.30f,.32f,1));Q(x+28,y+460,884,1,new(.28f,.30f,.32f,1));
  bool settings=Page is MiningPausePage.Settings or MiningPausePage.Sound or MiningPausePage.Controls or MiningPausePage.Graphics;if(settings)Q(x+220,y+100,1,340,new(.28f,.30f,.32f,1));
  sliderDraw.Clear();BuildIndustrial(w,h,false);bool hovered=items.Any(a=>Inside(a.X,a.Y,a.W,a.H));
  for(int n=0;n<items.Count;n++){var a=items[n];if(a.Overlay)continue;bool selected=a.Selected||Inside(a.X,a.Y,a.W,a.H)||!hovered&&focus==n;bool binding=a.Label.Contains('|');var parts=a.Label.Split('|');
   if(binding){Q(a.X,a.Y+a.H, a.W,1,new(.20f,.22f,.24f,1));T(parts[0],a.X+4,a.Y+10,19);Q(a.X+a.W-230,a.Y,230,a.H,new(.09f,.10f,.12f,1));if(selected){Q(a.X+a.W-230,a.Y,230,2,accent);Q(a.X+a.W-230,a.Y+a.H-2,230,2,accent);}T(parts[1],a.X+a.W-210,a.Y+10,19,selected?accent:text);}
   else{Q(a.X,a.Y,a.W,a.H,selected?accent:new(.07f,.08f,.09f,.5f));Q(a.X+15,a.Y+18,6,6,selected?new(.045f,.05f,.055f,1):muted);T(a.Label,a.X+37,a.Y+10,20,selected?new(.045f,.05f,.055f,1):text);}
  }
  foreach(var a in sliderDraw){T(a.Label,x+250,a.Y-3,19);Q(a.X,a.Y+9,a.W,4,new(.24f,.25f,.27f,1));Q(a.X,a.Y+9,a.W*a.Value,4,accent);Q(a.X+a.W*a.Value-6,a.Y+2,12,18,accent);T(Math.Round(a.Label=="RENDER SCALE"?50+a.Value*50:a.Value*100)+"%",a.X+a.W+22,a.Y-2,18);}
  if(Page==MiningPausePage.Controls){Q(x+913,y+110,4,300,new(.21f,.23f,.25f,1));Q(x+912,y+110+controlsScroll/(controls.Length+2-7)*210,6,90,accent);T("SCROLL TO VIEW ALL  /  CLICK A KEY TO REBIND",x+250,y+432,15,muted);}
  if(Page==MiningPausePage.NewCompany)T("Start fresh. Your previous save will be backed up.",x+56,y+165,20,muted);
  if(Page==MiningPausePage.Quit)T("Progress is saved automatically.",x+56,y+165,20,muted);
  if(Status.Length>0)T(Status,x+250,y+445,15,accent);
  foreach(var a in items.Where(i=>i.Overlay)){bool selected=a.Selected||Inside(a.X,a.Y,a.W,a.H);Q(a.X,a.Y,a.W,a.H,selected?accent:new(.10f,.12f,.14f,1));T(a.Label,a.X+16,a.Y+9,18,selected?new(.045f,.05f,.06f,1):text);}
 }
}
