using System.Text.Json;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Audio;

/// <summary>Optional per-title persisted mixer controls, shared by menu and gameplay scenes.</summary>
public sealed class AudioSettings:Component
{
 public string SettingsKey {get;set;}="";
 public float Music {get;set;}=.4f;
 public float Sfx {get;set;}=.8f;
 public string LastError {get;private set;}="";
 string? path;float lastMusic=-1,lastSfx=-1;
 record Preferences(float Music,float Sfx);
 protected override void OnStart(){
  string key=new(SettingsKey.Where(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_').ToArray());
  if(key.Length>0){path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ByteEngine","Audio",key+".json");
   try{if(File.Exists(path)&&JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) is {} p){Music=p.Music;Sfx=p.Sfx;}}
   catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){LastError=e.Message;}}
  Apply(false);
 }
 protected override void OnUpdate(){if(Music!=lastMusic||Sfx!=lastSfx)Apply(true);}
 void Apply(bool save){
  Music=Math.Clamp(float.IsFinite(Music)?Music:.4f,0,1);Sfx=Math.Clamp(float.IsFinite(Sfx)?Sfx:.8f,0,1);
  AudioMixer.SetVolume(AudioBus.Music,Music);AudioMixer.SetVolume(AudioBus.Sfx,Sfx);AudioMixer.SetVolume(AudioBus.Ui,Sfx);
  lastMusic=Music;lastSfx=Sfx;
  if(save&&path!=null)try{Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(new Preferences(Music,Sfx)));File.Move(path+".tmp",path,true);LastError="";}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException){LastError=e.Message;}
 }
}
