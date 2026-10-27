using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningPauseMenu3D
{
 void BuildIndustrial(float w,float h,bool interactive)
 {
  items.Clear();if(MainMenuMode&&Page==MiningPausePage.Pause){BuildMain(w,h);return;}
  float x=(w-1080)/2,y=(h-600)/2;
  void B(string label,float bx,float by,float bw,Action action,bool selected=false)=>items.Add(new(label,bx,by,bw,42,action,selected));
  if(Page==MiningPausePage.Pause){B("RESUME",x+56,y+175,360,Resume);B("SETTINGS",x+56,y+237,360,()=>ShowPage(MiningPausePage.Sound));B("QUIT",x+56,y+299,360,()=>ShowPage(MiningPausePage.Quit));return;}
  if(Page==MiningPausePage.Quit||Page==MiningPausePage.NewCompany){B(Page==MiningPausePage.Quit?"QUIT TO DESKTOP":"NEW GAME",x+56,y+240,460,()=>{if(Page==MiningPausePage.NewCompany)StartNewCompany();else{SaveSettings();if(!MainMenuMode)foreach(var op in GameObject.Scene!.GameObjects.SelectMany(o=>o.Components.OfType<MiningOperation3D>()))op.SaveProgress();GameObject.Scene.RequestQuit();}});B("CANCEL",x+56,y+302,460,()=>ShowPage(MiningPausePage.Pause));return;}
  if(Page==MiningPausePage.Settings)Page=MiningPausePage.Sound;
  B("AUDIO",x+28,y+135,230,()=>ShowPage(MiningPausePage.Sound),Page==MiningPausePage.Sound);
  B("CONTROLS",x+28,y+197,230,()=>ShowPage(MiningPausePage.Controls),Page==MiningPausePage.Controls);
  B("GRAPHICS",x+28,y+259,230,()=>ShowPage(MiningPausePage.Graphics),Page==MiningPausePage.Graphics);
  B("ESC    BACK",x+28,y+532,230,()=>{SaveSettings();ShowPage(MiningPausePage.Pause);});
  if(Page==MiningPausePage.Sound){for(int i=0;i<4;i++){var bus=(AudioBus)i;Slider(new[]{"MASTER","MUSIC","EFFECTS","INTERFACE"}[i],x+530,y+145+i*69,420,AudioMixer.GetVolume(bus),v=>AudioMixer.SetVolume(bus,v),interactive);}B("RESET DEFAULTS",x+760,y+532,285,()=>{foreach(var bus in Enum.GetValues<AudioBus>())AudioMixer.SetVolume(bus,1);SaveSettings();});}
  if(Page==MiningPausePage.Controls){int start=(int)controlsScroll;for(int row=0;row<7;row++){int index=start+row;if(index>=controls.Length+2)break;int item=index;string label=index<controls.Length?controls[index].Label: index==controls.Length?"Fire / place defence":"Aim / cancel placement";string value=index<controls.Length?(listening==controls[index].Key?"PRESS A KEY...":KeyLabel(controls[index].Key)):(listeningMouse==(index==controls.Length?MouseButton.Left:MouseButton.Right)?"PRESS BUTTON...":Input.GameplayMouseButton(index==controls.Length?MouseButton.Left:MouseButton.Right).ToString());B(label.ToUpperInvariant()+"|"+value,x+320,y+120+row*49,690,()=>{if(item<controls.Length)listening=controls[item].Key;else listeningMouse=item==controls.Length?MouseButton.Left:MouseButton.Right;listenAt=Time.TotalTime;});}
   B("RESET KEYS",x+760,y+532,285,()=>{Input.ClearGameplayKeyBindings();SaveSettings();});
   if(interactive&&down&&Inside(x+1032,y+120,20,343))controlsScroll=Math.Clamp((pointer.Y-y-120)/343*(controls.Length+2-7),0,controls.Length+2-7);
  }
  if(Page==MiningPausePage.Graphics){B("QUALITY|"+(graphics?.Quality.ToString().ToUpperInvariant()??"HIGH"),x+320,y+122,690,()=>{if(graphics!=null)graphics.Quality=graphics.Quality==GraphicsQuality.Fast?GraphicsQuality.Balanced:graphics.Quality==GraphicsQuality.Balanced?GraphicsQuality.High:GraphicsQuality.Fast;});Slider("RENDER SCALE",x+580,y+220,370,((graphics?.RenderScale??1)-.5f)*2,v=>{if(graphics!=null)graphics.RenderScale=.5f+v*.5f;},interactive);Slider("LOOK SENSITIVITY",x+580,y+300,370,(MouseSensitivity-.02f)/.38f,v=>{MouseSensitivity=.02f+v*.38f;ApplySensitivity();},interactive);B("APPLY",x+760,y+532,285,SaveSettings);}
 }
 void RenderIndustrial(RenderContext c)
 {
  if(!IsOpen)return;if(MainMenuMode&&Page==MiningPausePage.Pause){RenderMain(c);return;}
  float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),w=c.TargetWidth/s,h=c.TargetHeight/s,x=(w-1080)/2,y=(h-600)/2;
  void Q(float a,float b,float ww,float hh,Vector4 color)=>c.DrawUiQuad(new(a*s,b*s),new(ww*s,hh*s),color);
  void T(string t,float a,float b,int size=20,Vector4? color=null)=>c.DrawUiText(t,BodyFont,(int)(size*s),new(a*s,b*s),color??text,UiAnchor.TopLeft);
  Q(0,0,w,h,new(0,0,0,.32f));Q(x,y,1080,600,surface);
  Q(x,y,1080,1,new(.32f,.34f,.36f,1));Q(x,y+599,1080,1,new(.32f,.34f,.36f,1));
  foreach(float cx in new[]{x,x+1068})foreach(float cy in new[]{y,y+598})Q(cx,cy,12,2,accent);
  string title=Page==MiningPausePage.Pause?"PAUSED":Page==MiningPausePage.Quit?"QUIT GAME?":Page==MiningPausePage.NewCompany?"NEW GAME?":"SETTINGS / "+(Page==MiningPausePage.Sound?"AUDIO":Page==MiningPausePage.Controls?"CONTROLS":"GRAPHICS");
  c.DrawUiText(title,HeadingFont,(int)(30*s),new((x+36)*s,(y+35)*s),text,UiAnchor.TopLeft);Q(x+28,y+94,1024,1,new(.28f,.30f,.32f,1));Q(x+28,y+514,1024,1,new(.28f,.30f,.32f,1));
  bool settings=Page is MiningPausePage.Settings or MiningPausePage.Sound or MiningPausePage.Controls or MiningPausePage.Graphics;if(settings)Q(x+283,y+116,1,378,new(.28f,.30f,.32f,1));
  sliderDraw.Clear();BuildIndustrial(w,h,false);bool hovered=items.Any(a=>Inside(a.X,a.Y,a.W,a.H));
  for(int n=0;n<items.Count;n++){var a=items[n];bool selected=a.Selected||Inside(a.X,a.Y,a.W,a.H)||!hovered&&focus==n;bool binding=a.Label.Contains('|');var parts=a.Label.Split('|');
   if(binding){Q(a.X,a.Y+a.H, a.W,1,new(.20f,.22f,.24f,1));T(parts[0],a.X+4,a.Y+10,19);Q(a.X+a.W-230,a.Y,230,a.H,new(.09f,.10f,.12f,1));if(selected){Q(a.X+a.W-230,a.Y,230,2,accent);Q(a.X+a.W-230,a.Y+a.H-2,230,2,accent);}T(parts[1],a.X+a.W-210,a.Y+10,19,selected?accent:text);}
   else{Q(a.X,a.Y,a.W,a.H,selected?accent:new(.07f,.08f,.09f,.5f));Q(a.X+15,a.Y+18,6,6,selected?new(.045f,.05f,.055f,1):muted);T(a.Label,a.X+37,a.Y+10,20,selected?new(.045f,.05f,.055f,1):text);}
  }
  foreach(var a in sliderDraw){T(a.Label,x+325,a.Y-3,19);Q(a.X,a.Y+9,a.W,4,new(.24f,.25f,.27f,1));Q(a.X,a.Y+9,a.W*a.Value,4,accent);Q(a.X+a.W*a.Value-6,a.Y+2,12,18,accent);T(Math.Round(a.Label=="RENDER SCALE"?50+a.Value*50:a.Value*100)+"%",a.X+a.W+22,a.Y-2,18);}
  if(Page==MiningPausePage.Controls){Q(x+1038,y+120,4,343,new(.21f,.23f,.25f,1));Q(x+1037,y+120+controlsScroll/(controls.Length+2-7)*240,6,103,accent);T("SCROLL TO VIEW ALL  /  CLICK A KEY TO REBIND",x+320,y+478,15,muted);}
  if(Page==MiningPausePage.NewCompany)T("Start fresh. Your previous save will be backed up.",x+56,y+165,20,muted);
  if(Page==MiningPausePage.Quit)T("Progress is saved automatically.",x+56,y+165,20,muted);
  if(Status.Length>0)T(Status,x+320,y+488,15,accent);
 }
}
