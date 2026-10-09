using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneMainMenu3D:Component
{
 ByteEngine.Core.Audio.AudioSource3D? _music;
 internal string ProjectRoot="";
 string SaveRoot=>ByteEngine.Core.Runtime.GameSaveStorage.GetDirectory(ProjectRoot);
 internal static bool ContinuePending,NewGamePending;
 public float MasterVolume{get;set;}=1;
 public bool BalancedQuality{get;set;}
 public override int UpdateOrder=>100;
 public void Command(string action){var scene=GameObject.Scene!;
  switch(action){case "Continue":ContinuePending=true;NewGamePending=false;break;case "New Game":ShowNewGameConfirmation();break;
   case "Cancel New Game":CloseNewGameConfirmation();break;
   case "Confirm New Game":ConfirmNewGame();break;
   case "Settings":scene.FindGameObject("Settings panel")!.Active=true;break;case "Credits":scene.FindGameObject("Credits panel")!.Active=true;break;
   case "Close Settings":scene.FindGameObject("Settings panel")!.Active=false;break;case "Close Credits":scene.FindGameObject("Credits panel")!.Active=false;break;
   case "Volume Up":MasterVolume=Math.Min(1,MasterVolume+.1f);Save();break;case "Volume Down":MasterVolume=Math.Max(0,MasterVolume-.1f);Save();break;
   case "Quality":BalancedQuality=!BalancedQuality;Save();break;
  }
 }
 protected override void OnStop(){_music?.Stop();_music=null;_menuClick?.Stop();_menuClick=null;}
 void Save(){ApplyAudioPreferences(ReadPreferences(ProjectRoot) with {Volume=MasterVolume,Balanced=BalancedQuality});Directory.CreateDirectory(SaveRoot);File.WriteAllText(Path.Combine(SaveRoot,"menu-settings.json"),JsonSerializer.Serialize(ReadPreferences(ProjectRoot) with {Volume=MasterVolume,Balanced=BalancedQuality}));}
 internal sealed record Preferences(float Volume=1,bool Balanced=false,float Music=1,float Sfx=1,float Ui=1);
 internal static Preferences ReadPreferences(string root){try{var file=Path.Combine(ByteEngine.Core.Runtime.GameSaveStorage.GetDirectory(root),"menu-settings.json");return File.Exists(file)?JsonSerializer.Deserialize<Preferences>(File.ReadAllText(file))??new():new();}catch(Exception e)when(e is IOException or JsonException){return new();}}
 protected override void OnStart(){Input.NotifyGameViewPointerAim();EnsureNewGameConfirmation();var prefs=ReadPreferences(ProjectRoot);MasterVolume=Math.Clamp(prefs.Volume,0,1);BalancedQuality=prefs.Balanced;ApplyAudioPreferences(prefs);_music=GarageMusic.Create(GameObject.Scene!,ProjectRoot,"main-menu",.4f);var button=GameObject.Scene!.FindGameObject("Continue button")?.GetComponent<UiWidget>();if(button!=null)button.Interactable=File.Exists(Path.Combine(SaveRoot,"vehicle.json"));}
 protected override void OnUpdate(){Input.NotifyGameViewPointerAim();if(_music!=null)_music.Volume=.4f;UpdateMenuAudio();var scene=GameObject.Scene!;var options=new[]{"Continue","New Game","Settings","Credits","Quit"};bool hasSave=File.Exists(Path.Combine(SaveRoot,"vehicle.json"));bool modal=scene.FindGameObject("Settings panel")!.Active||scene.FindGameObject("Credits panel")!.Active||_newGameConfirmation;
  int selected=hasSave?0:1;int hovered=-1;for(int i=0;i<options.Length;i++){var w=scene.FindGameObject(options[i]+" button")?.GetComponent<UiWidget>();if(w==null)continue;w.Interactable=!modal&&(i!=0||hasSave);if(w.IsFocused)selected=i;if(w.IsHovered)hovered=i;}if(hovered>=0){selected=hovered;UiNavigation.Focus(scene.FindGameObject(options[selected]+" button")!.GetComponent<UiWidget>());}

  for(int i=0;i<options.Length;i++){var button=scene.FindGameObject(options[i]+" button")?.GetComponent<UiWidget>();if(button!=null)button.Color=i==selected?new(.18f,.19f,.085f,.58f):Vector4.Zero;}
  var saveStatus=scene.FindGameObject("Save status")?.GetComponent<UiText>();if(saveStatus!=null)saveStatus.Text=hasSave?"S A V E  0 1  /  S A L V A G E  C O N T R A C T":"NO SAVED VEHICLE / START A NEW GAME";
  var continueLabel=scene.FindGameObject("Continue button label")?.GetComponent<UiText>();if(continueLabel!=null)continueLabel.Color=hasSave?new(.89f,.86f,.75f,1):new(.5f,.5f,.45f,1);
  var marker=scene.FindGameObject("Menu selection stripe")?.GetComponent<UiWidget>();if(marker!=null)marker.Offset=new(65,269+selected*56);
  var label=scene.FindGameObject("Volume value")?.GetComponent<UiText>();if(label!=null)label.Text=$"MASTER VOLUME  {MasterVolume*100:0}%";
  var quality=scene.FindGameObject("Quality value")?.GetComponent<UiText>();if(quality!=null)quality.Text="GRAPHICS  "+(BalancedQuality?"BALANCED":"FAST");
  if(_newGameConfirmation){if(scene.FindGameObject("Confirm New Game button")?.GetComponent<UiWidget>()?.WasClicked==true)ConfirmNewGame();else if(scene.FindGameObject("Cancel New Game button")?.GetComponent<UiWidget>()?.WasClicked==true)CloseNewGameConfirmation();}
  if(Input.IsKeyPressed(Key.Escape)){CloseNewGameConfirmation();scene.FindGameObject("Settings panel")!.Active=false;scene.FindGameObject("Credits panel")!.Active=false;}
 }
}
