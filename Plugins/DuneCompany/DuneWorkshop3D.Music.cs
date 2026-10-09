using ByteEngine.Core.Audio;
using ByteEngine.Core.Scene;
namespace DuneCompany;
internal static class GarageMusic
{
 internal static AudioSource3D? Create(Scene scene,string root,string file,float volume){string reference="Assets/Audio/Music/"+file+".wav";if(!AudioClip.TryLoadWave(Path.Combine(root,reference),out var clip))return null;var obj=scene.CreateGameObject("Menu music - "+file);var source=obj.AddComponent(new AudioSource3D{ClipReference=new(reference),Spatial=false,Bus=AudioBus.Music,Loop=true,PlayOnStart=false,Volume=volume});source.SetClip(clip);source.Play();return source;}
}
public sealed partial class DuneWorkshop3D
{
 AudioSource3D? _garageMusic;float _garageMusicVolume;
 void StartGarageMusic(){_garageMusicVolume=.32f;_garageMusic=GarageMusic.Create(GameObject.Scene!,ProjectRoot,"garage",0);}
 void UpdateGarageMusic(float dt){if(_garageMusic==null)return;float target=Building?_garageMusicVolume:0;_garageMusic.Volume+=Math.Clamp(target-_garageMusic.Volume,-dt*.5f,dt*.5f);}
 void StopGarageMusic(){if(_garageMusic?.GameObject.Scene is {} scene){_garageMusic.Stop();scene.DestroyGameObject(_garageMusic.GameObject);}_garageMusic=null;}
}
