using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneMainMenu3D
{
 AudioSource3D? _menuClick;
 internal static void ApplyAudioPreferences(Preferences p){AudioMixer.SetVolume(AudioBus.Master,Math.Clamp(p.Volume,0,1));AudioMixer.SetVolume(AudioBus.Music,Math.Clamp(p.Music,0,1));AudioMixer.SetVolume(AudioBus.Sfx,Math.Clamp(p.Sfx,0,1));AudioMixer.SetVolume(AudioBus.Ui,Math.Clamp(p.Ui,0,1));}
 void UpdateMenuAudio(){if(GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).OfType<UiWidget>().Any(w=>w.WasClicked)){if(_menuClick==null){var reference="Assets/Audio/Feedback/menu-click.wav";if(!AudioClip.TryLoadWave(Path.Combine(ProjectRoot,reference),out var clip))return;var obj=GameObject.Scene.CreateGameObject("Main menu click");_menuClick=obj.AddComponent(new AudioSource3D{Spatial=false,Bus=AudioBus.Ui,Volume=.45f,ClipReference=new(reference)});_menuClick.SetClip(clip);}_menuClick.Play();}}
}
public sealed partial class DuneWorkshop3D
{
 static void ApplyAudioPreferences(DuneMainMenu3D.Preferences p)=>DuneMainMenu3D.ApplyAudioPreferences(p);
}
