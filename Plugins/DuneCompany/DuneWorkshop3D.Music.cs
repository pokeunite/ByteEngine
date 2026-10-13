using ByteEngine.Core.Audio;
using ByteEngine.Core.Scene;
namespace DuneCompany;
internal static class GarageMusic
{
 internal static AudioSource3D? Create(Scene scene,string root,string file,float volume){string reference="Assets/Audio/Music/"+file+".wav";if(!AudioClip.TryLoadWave(Path.Combine(root,reference),out var clip))return null;var obj=scene.CreateGameObject("Menu music - "+file);var source=obj.AddComponent(new AudioSource3D{ClipReference=new(reference),Spatial=false,Bus=AudioBus.Music,Loop=true,PlayOnStart=false,Volume=volume});source.SetClip(clip);source.Play();return source;}
}
public sealed partial class DuneWorkshop3D
{
 AudioSource3D? _garageMusic,_desertWind;float _garageMusicVolume;
 void StartGarageMusic(){_garageMusicVolume=.28f;_garageMusic=GarageMusic.Create(GameObject.Scene!,ProjectRoot,"garage",0);if(AudioClip.TryLoadWave(Path.Combine(ProjectRoot,"Assets/Audio/Ambience/desert-wind.wav"),out var clip)){var obj=GameObject.Scene!.CreateGameObject("Desert wind ambience");_desertWind=obj.AddComponent(new AudioSource3D{ClipReference=new("Assets/Audio/Ambience/desert-wind.wav"),Spatial=false,Bus=AudioBus.Sfx,Loop=true,PlayOnStart=false,Volume=0});_desertWind.SetClip(clip);_desertWind.Play();}}
 void UpdateGarageMusic(float dt){if(_garageMusic!=null){float target=Building?_garageMusicVolume:0;_garageMusic.Volume+=Math.Clamp(target-_garageMusic.Volume,-dt*.5f,dt*.5f);}if(_desertWind!=null){float target=(Building?.045f:.13f)*VehicleSfxVolume;_desertWind.Volume+=Math.Clamp(target-_desertWind.Volume,-dt*.12f,dt*.12f);}}
 void StopGarageMusic(){foreach(var sound in new[]{_garageMusic,_desertWind})if(sound?.GameObject.Scene is {} scene){sound.Stop();scene.DestroyGameObject(sound.GameObject);}_garageMusic=_desertWind=null;}
}
