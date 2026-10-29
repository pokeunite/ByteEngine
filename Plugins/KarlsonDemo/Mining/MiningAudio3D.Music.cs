using System.ComponentModel;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;

namespace KarlsonDemo.Plugin;
public sealed partial class MiningAudio3D
{
 [Category("Music")] public AssetReference CampMusic {get;set;}=AssetReference.Empty;
 [Category("Music")] public AssetReference BattleMusic {get;set;}=AssetReference.Empty;
 [Category("Music")] public float BattleMusicVolume {get;set;}=.3f;
 public float CampMusicVolume {get;set;}=.38f;
 [Category("Music")] public float MusicFadeSeconds {get;set;}=1.5f;
 [Category("Music")] public bool MusicEnabled {get;set;}=true;
 AudioSource3D? campMusicSource,battleMusicSource;
 void TickMusic(float dt)
 {
  if(!float.IsFinite(dt)||dt<=0||AttachedGameObject?.Scene==null)return;
  if(!MusicEnabled){battleMusicSource?.Stop();}
  if(CampMusic.IsEmpty||!MusicEnabled){campMusicSource?.Stop();if(campMusicSource!=null)campMusicSource.Volume=0;return;}
  if(campMusicSource==null){var o=GameObject.Scene!.CreateGameObject("Mining camp music");o.SetParent(GameObject,false);campMusicSource=o.AddComponent(new AudioSource3D{Loop=true,Spatial=false,Bus=AudioBus.Music,Volume=0,Priority=20});}
  if(campMusicSource.ClipReference!=CampMusic||!campMusicSource.ClipLoaded){campMusicSource.Stop();if(!AudioRuntimeAssets.TryLoadClip(campMusicSource,CampMusic,m=>LastError=m))return;}
  bool mainMenu=GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).OfType<MiningPauseMenu3D>().Any(m=>m.MainMenuMode);
  bool calm=mainMenu||Op is {Phase:MiningPhase.Preparation,RoamingEnemiesAlive:0};
  float target=calm?Math.Clamp(CampMusicVolume,0,1):0;
  float step=dt/Math.Clamp(MusicFadeSeconds,.1f,10);
  campMusicSource.Volume+=Math.Clamp(target-campMusicSource.Volume,-step,step);
  if(calm&&!campMusicSource.IsPlaying)campMusicSource.Play();
  else if(!calm&&campMusicSource.Volume<=.001f)campMusicSource.Stop();
  bool battle=!mainMenu&&Op is {} operation&&(operation.Phase==MiningPhase.Drilling||operation.RoamingEnemiesAlive>0);
  if(!BattleMusic.IsEmpty){
   if(battleMusicSource==null){var o=GameObject.Scene!.CreateGameObject("Mining combat music");o.SetParent(GameObject,false);battleMusicSource=o.AddComponent(new AudioSource3D{Loop=true,Spatial=false,Bus=AudioBus.Music,Volume=0,Priority=20});}
   if(battleMusicSource.ClipReference!=BattleMusic||!battleMusicSource.ClipLoaded){battleMusicSource.Stop();if(!AudioRuntimeAssets.TryLoadClip(battleMusicSource,BattleMusic,m=>LastError=m))return;}
   float battleTarget=battle?Math.Clamp(BattleMusicVolume,0,1):0;
   battleMusicSource.Volume+=Math.Clamp(battleTarget-battleMusicSource.Volume,-step,step);
   if(battle&&!battleMusicSource.IsPlaying)battleMusicSource.Play();else if(!battle&&battleMusicSource.Volume<=.001f)battleMusicSource.Stop();
  }
 }
}
