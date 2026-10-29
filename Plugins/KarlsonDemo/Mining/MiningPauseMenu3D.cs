using System.ComponentModel;
using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;

public enum MiningPausePage { Pause, Settings, Sound, Controls, Graphics, Quit, NewCompany }
/// <summary>Unscaled pause UI with persistent, real mixer, keyboard and rendering settings.</summary>
public sealed partial class MiningPauseMenu3D:Component
{
 public AssetReference HeadingFont{get;set;}=AssetReference.Empty;
 public AssetReference BodyFont{get;set;}=AssetReference.Empty;
 public bool PersistSettings{get;set;}=true;
 public string SaveSlot{get;set;}="mining-player-settings.json";
 public bool ShowMainMenuOnStart{get;set;}
 public string CompanyTitle{get;set;}="HAZARD PAY";
 [Browsable(false)] public bool MainMenuMode{get;private set;}
 bool startupHandled;
 static string? skipNextStartupProject;
 public float MouseSensitivity{get;set;}=.12f;
 [Browsable(false)] public bool IsOpen{get;private set;}
 [Browsable(false)] public MiningPausePage Page{get;private set;}
 [Browsable(false)] public string Status{get;private set;}="";
 public override int UpdateOrder=>-10000;
 public override int? RenderOrder=>5000;
 internal static double IgnoreEscapeAt=-1;
 readonly List<Component> suspended=[];
 readonly List<AudioSource3D> pausedAudio=[];
 float priorScale=1;int focus;float controlsScroll;Key? listening;MouseButton? listeningMouse;double listenAt;
 bool windowDropdownOpen;
 public float HoverSoundGain{get;set;}=.22f;
 string? hoveredMenuItem;
 GraphicsSettings3D? graphics;GameWindowMode selectedWindowMode=GameWindowMode.Fullscreen;
 readonly record struct Item(string Label,float X,float Y,float W,float H,Action Click,bool Selected=false,bool Overlay=false);
 readonly List<Item> items=[];
 Vector2 pointer;bool click,down;float width=1280,height=720;
 readonly (string Label,Key Key)[] controls=[("Forward",Key.W),("Back",Key.S),("Left",Key.A),("Right",Key.D),("Sprint",Key.LeftShift),("Interact",Key.E),("Reload",Key.R),("Build defence / cycle",Key.B),("Arrange camp",Key.Tab),("Rotate camp left",Key.Q),("Sentry",Key.F1),("Rotary gun",Key.F2),("Mortar",Key.F3),("Mine",Key.F4),("Supply drop",Key.F5),("Weapon 1",Key.D1),("Weapon 2",Key.D2),("Weapon 3",Key.D3)];
 readonly Vector4 text=new(.94f,.93f,.88f,1),muted=new(.57f,.59f,.61f,1),accent=new(.91f,.71f,.15f,1),surface=new(.055f,.063f,.073f,.96f);
 string FilePath=>Path.Combine(GameSaveStorage.GetDirectory(GameObject.Scene!.ProjectRoot!),Path.GetFileName(SaveSlot));
 record Preferences(float Master,float Music,float Sfx,float Ui,float Sensitivity,Dictionary<Key,Key> Keys,Dictionary<MouseButton,MouseButton>? Mouse=null,GameWindowMode WindowMode=GameWindowMode.Fullscreen);
 protected override void OnStart()
 {
  Input.RegisterGameplayEscapeHandler(this,true);
  graphics=GameObject.GetComponent<GraphicsSettings3D>()??GameObject.AddComponent(new GraphicsSettings3D{SettingsKey="palclone",PreservePostEffects=true});
  if(PersistSettings)try{if(File.Exists(FilePath)&&JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) is {} p){AudioMixer.SetVolume(AudioBus.Master,p.Master);AudioMixer.SetVolume(AudioBus.Music,p.Music);AudioMixer.SetVolume(AudioBus.Sfx,p.Sfx);AudioMixer.SetVolume(AudioBus.Ui,p.Ui);MouseSensitivity=Math.Clamp(p.Sensitivity,.02f,.4f);if(Enum.IsDefined(p.WindowMode)&&!OperatingSystem.IsBrowser())GameWindowDisplay.Request(p.WindowMode);selectedWindowMode=GameWindowDisplay.Mode;foreach(var key in p.Keys)if(key.Key!=Key.Escape&&key.Value!=Key.Escape&&Enum.IsDefined(key.Key)&&Enum.IsDefined(key.Value))Input.BindGameplayKey(key.Key,key.Value);if(p.Mouse!=null)foreach(var b in p.Mouse)if(Enum.IsDefined(b.Key)&&Enum.IsDefined(b.Value))Input.BindGameplayMouseButton(b.Key,b.Value);}}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException){Status=e.Message;}
  ApplySensitivity();
 }
 void ApplySensitivity(){foreach(var camera in GameObject.Scene!.GameObjects.SelectMany(o=>o.Components.OfType<MiningThirdPerson3D>())){camera.NormalSensitivity=MouseSensitivity;camera.AimSensitivity=MouseSensitivity*.625f;}}
 public static string KeyLabel(Key logical){string key=Input.GameplayKey(logical).ToString();return key.Length==2&&key[0]=='D'&&char.IsDigit(key[1])?key[1..]:key.Replace("LeftShift","Shift").Replace("LeftAlt","Alt");}
 public void SaveSettings(){if(!PersistSettings||GameObject.Scene?.ProjectRoot==null)return;try{Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);File.WriteAllText(FilePath+".tmp",JsonSerializer.Serialize(new Preferences(AudioMixer.GetVolume(AudioBus.Master),AudioMixer.GetVolume(AudioBus.Music),AudioMixer.GetVolume(AudioBus.Sfx),AudioMixer.GetVolume(AudioBus.Ui),MouseSensitivity,controls.ToDictionary(c=>c.Key,c=>Input.GameplayKey(c.Key)),Enum.GetValues<MouseButton>().ToDictionary(b=>b,b=>Input.GameplayMouseButton(b)),GameWindowDisplay.Mode)));File.Move(FilePath+".tmp",FilePath,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Status=e.Message;}}
 public void Open()
 {
  if(IsOpen)return;var scene=GameObject.Scene!;
  foreach(var ui in scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningBusinessUi3D>()))ui.Close();
  priorScale=scene.SimulationTimeScale;scene.SimulationTimeScale=0;IsOpen=true;Page=MiningPausePage.Pause;focus=0;
  foreach(var c in scene.GameObjects.SelectMany(o=>o.Components).ToArray())
   if(c.Enabled&&c!=this&&(c is EventModuleComponent or PlayerController3D or CameraBoom3D or AIBehavior3D or SimpleEnemyAI3D or NavigationAgent3D or Projectile3D||c.GetType().Namespace==typeof(MiningPauseMenu3D).Namespace&&c is not MiningAudio3D)) {suspended.Add(c);c.Enabled=false;}
  foreach(var a in scene.GameObjects.SelectMany(o=>o.Components.OfType<AudioSource3D>()).Where(a=>a.IsPlaying&&a.Bus==AudioBus.Sfx)){a.Pause();pausedAudio.Add(a);}
  Input.RequestUiPointer(this,true);Input.ConsumeGameplayPointer();UiNavigation.Focus(null);
 }
 public void Resume()
 {
  if(!IsOpen)return;EndMainPresentation();IsOpen=false;MainMenuMode=false;listening=null;listeningMouse=null;GameObject.Scene!.SimulationTimeScale=priorScale;
  var live=GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).ToHashSet();
  foreach(var c in suspended)if(live.Contains(c))c.Enabled=true;suspended.Clear();
  foreach(var a in pausedAudio)if(live.Contains(a)&&a.IsPaused)a.Play();pausedAudio.Clear();
  Input.ConsumeGameplayPointer();Input.RequestUiPointer(this,false);SaveSettings();
 }
 public void ShowPage(MiningPausePage page){if(page==MiningPausePage.Graphics)selectedWindowMode=GameWindowDisplay.Mode;windowDropdownOpen=false;Page=page;focus=page==MiningPausePage.Controls?1:page==MiningPausePage.Graphics?2:0;listening=null;listeningMouse=null;Status="";controlsScroll=0;}
 public bool Rebind(Key logical,Key physical)
 {
  if(physical is Key.Escape or Key.Enter or Key.Up or Key.Down or Key.Left or Key.Right){Status="That key is reserved for menus.";return false;}
  var conflict=controls.FirstOrDefault(c=>c.Key!=logical&&Input.GameplayKey(c.Key)==physical);
  if(conflict.Label!=null){Status="Already used by "+conflict.Label;return false;}
  Input.BindGameplayKey(logical,physical);SaveSettings();Status="Binding saved.";return true;
 }
 protected override void OnUpdate()
 {
  if(!startupHandled){startupHandled=true;if(ShowMainMenuOnStart){if(skipNextStartupProject!=null&&skipNextStartupProject==GameObject.Scene?.ProjectRoot)skipNextStartupProject=null;else{Open();MainMenuMode=true;BeginMainPresentation();return;}}}
  if(Input.IsKeyPressed(Key.Escape)&&(Input.IsGameInputCaptured||IsOpen)&&IgnoreEscapeAt!=Time.TotalTime)
  {
   var ui=GameObject.Scene!.GameObjects.SelectMany(o=>o.Components.OfType<MiningBusinessUi3D>()).FirstOrDefault(u=>u.Screen!=MiningScreen.None);
   if(!IsOpen&&ui!=null){ui.Close();return;}
   if(windowDropdownOpen){windowDropdownOpen=false;return;}
   if(listening.HasValue||listeningMouse.HasValue){listening=null;listeningMouse=null;return;}
   if(!IsOpen)Open();else if(Page==MiningPausePage.Pause){if(!MainMenuMode)Resume();}else ShowPage(MiningPausePage.Pause);
   return;
  }
  if(!IsOpen)return;Input.RequestUiPointer(this,true);Input.ConsumeGameplayPointer();
  if(listeningMouse.HasValue){if(Time.TotalTime>listenAt)foreach(var button in Enum.GetValues<MouseButton>())if(Input.IsMouseButtonPressedForUi(button)){var logical=listeningMouse.Value;var old=Input.GameplayMouseButton(logical);var other=Enum.GetValues<MouseButton>().FirstOrDefault(b=>b!=logical&&Input.GameplayMouseButton(b)==button);if(other!=logical&&Input.GameplayMouseButton(other)==button)Input.BindGameplayMouseButton(other,old);Input.BindGameplayMouseButton(logical,button);listeningMouse=null;SaveSettings();break;}return;}
  if(listening.HasValue){if(Time.TotalTime>listenAt)foreach(var key in Enum.GetValues<Key>())if(Input.IsKeyPressed(key)){if(Rebind(listening.Value,key))listening=null;listeningMouse=null;break;}return;}
  width=Input.GameViewSize.X;height=Input.GameViewSize.Y;float scale=Math.Max(.1f,Math.Min(width/1280,height/720));pointer=Input.GameViewMousePosition/scale;click=Input.IsMouseButtonPressedForUi(MouseButton.Left);down=Input.IsMouseButtonDownForUi(MouseButton.Left);if(Page==MiningPausePage.Controls)controlsScroll=Math.Clamp(controlsScroll-Input.Snapshot.MouseWheel*2,0,Math.Max(0,controls.Length+2-7));Build(width/scale,height/scale,true);
  string? hovered=items.FirstOrDefault(i=>Inside(i.X,i.Y,i.W,i.H)).Label;
  if(hovered!=hoveredMenuItem){if(hovered!=null)MiningAudio3D.Find(GameObject)?.PlayHoverCue(HoverSoundGain);hoveredMenuItem=hovered;}
  if(Input.IsKeyPressed(Key.Down))focus=(focus+1)%Math.Max(1,items.Count);if(Input.IsKeyPressed(Key.Up))focus=(focus+items.Count-1)%Math.Max(1,items.Count);
  if(Input.IsKeyPressed(Key.Enter)&&items.Count>focus)items[focus].Click();
  if(click){var hit=items.FirstOrDefault(i=>Inside(i.X,i.Y,i.W,i.H));if(hit.Click==null)windowDropdownOpen=false;hit.Click?.Invoke();}
 }
 bool Inside(float x,float y,float w,float h)=>pointer.X>=x&&pointer.X<x+w&&pointer.Y>=y&&pointer.Y<y+h;
 void Build(float w,float h,bool interactive)=>BuildIndustrial(w,h,interactive);
 readonly List<(string Label,float X,float Y,float W,float Value)> sliderDraw=[];
 void Slider(string label,float x,float y,float w,float value,Action<float> change,bool interactive)
 {if(interactive&&down&&Inside(x,y-8,w,34)){value=Math.Clamp((pointer.X-x)/w,0,1);change(value);}if(!interactive)sliderDraw.Add((label,x,y,w,value));}
 protected override void OnRender(RenderContext c)=>RenderIndustrial(c);
 void Cleanup(){Resume();Input.RequestUiPointer(this,false);Input.RegisterGameplayEscapeHandler(this,false);Input.ClearGameplayKeyBindings();}
 protected override void OnStop()=>Cleanup();
 protected override void OnDestroy()=>Cleanup();
}
