using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 public float VehicleSfxVolume{get;set;}=.8f;public float DustAmount{get;set;}=1;public float ExhaustAmount{get;set;}=1;
 GameObject? _feedbackRoot;readonly Dictionary<string,AudioSource3D> _vehicleAudio=[];readonly Dictionary<int,(VfxPlayer Player,PlacedBlock Block,int Side)> _exhaust=[];
 int _audioGear=1;float _feedbackThrottle,_engineLoad,_feedbackSlip,_feedbackGroundSpeed;bool _feedbackBrake,_feedbackDriving;
 public string VehicleFeedbackDebug=>$"audio={_vehicleAudio.Count} drift={_physics?.DriftBlend:0.00} load={_engineLoad:0.00} speed={_feedbackGroundSpeed:0.0} slip={_feedbackSlip:0.0} dust={_tyreDust.Count} exhaust={_exhaust.Count}";
 AudioSource3D AddVehicleSound(string name,bool loop){var o=GameObject.Scene!.CreateGameObject("Vehicle audio - "+name);o.SetParent(_feedbackRoot!,false);var source=o.AddComponent(new AudioSource3D{ClipReference=new("Assets/Audio/Vehicle/"+name+".wav"),Loop=loop,PlayOnStart=false,Spatial=true,MinDistance=16,MaxDistance=100,Volume=0});if(AudioClip.TryLoadWave(Path.Combine(ProjectRoot,"Assets/Audio/Vehicle/"+name+".wav"),out var clip))source.SetClip(clip);_vehicleAudio[name]=source;return source;}
 void EnsureVehicleFeedback(){if(_feedbackRoot!=null)return;_feedbackRoot=GameObject.Scene!.CreateGameObject("Vehicle feedback");if(_camera!=null&&!GameObject.Scene.GameObjects.Any(o=>o.GetComponent<AudioListener3D>()!=null))_camera.GameObject.AddComponent(new AudioListener3D());foreach(var name in new[]{"engine-idle","engine-drive","rolling","gravel-brake","tyre-skid"})AddVehicleSound(name,true);AddVehicleSound("engine-start",false);}
 static float FeedbackSmooth(float current,float target,float dt,float rate)=>current+(target-current)*(1-MathF.Exp(-Math.Clamp(dt,0,.1f)*rate));
 void MixSound(string name,float volume,float pitch,float dt){if(!_vehicleAudio.TryGetValue(name,out var s))return;s.Volume=FeedbackSmooth(s.Volume,Math.Clamp(volume*VehicleSfxVolume,0,1.2f),dt,8);s.Pitch=FeedbackSmooth(s.Pitch,Math.Clamp(pitch,.6f,2.2f),dt,6);if(s.ClipLoaded&&s.Volume>.004f){if(!s.IsPlaying)s.Play();}else if(s.Volume<.003f)s.Stop();}
 VfxPlayer VehicleEffect(string name,string file,VfxPreset fallback){var obj=GameObject.Scene!.CreateGameObject(name);obj.SetParent(_feedbackRoot!,false);var p=obj.AddComponent(new VfxPlayer{Effect=new("Assets/VFX/"+file+".bvfx"),Preset=fallback,PlayOnStart=false,ViewDistance=100,QualityDistance=30,Seed=name.GetHashCode()&int.MaxValue});string path=Path.Combine(ProjectRoot,"Assets/VFX/"+file+".bvfx");if(File.Exists(path))p.SetDefinition(VfxEffectSerializer.Load(path));return p;}
 void UpdateVehicleFeedback(float dt){if(Building||_physics==null)return;EnsureVehicleFeedback();var engines=_blocks.Where(b=>(b.Type is 23 or 24)&&b.Power>0).ToArray();var enginePosition=engines.Length>0?engines.Select(b=>_visuals[b.Id].Transform.WorldMatrix.Translation).Aggregate(Vector3.Zero,(a,b)=>a+b)/engines.Length:Transform.WorldPosition;
  foreach(var s in _vehicleAudio.Values)s.Transform.WorldPosition=enginePosition;
  float speed=0,slip=0,rpm=0;int contacts=0,wheels=0;
  foreach(var b in _blocks.Where(b=>_catalog[b.Type].Wheel).Take(8)){var info=_physics.WheelFeedback(b.Id);wheels++;rpm+=Math.Abs(info.Rpm);if(info.Grounded){contacts++;speed+=info.Speed;slip+=Math.Clamp(info.Slip,0,12);}if(!_tyreDust.TryGetValue(b.Id,out var plume)){plume=VehicleEffect("Tyre sand plume "+b.Id,"tyre-sand",VfxPreset.Dust);_tyreDust[b.Id]=plume;}plume.Transform.WorldPosition=info.Contact+info.Normal*.08f;var direction=info.Velocity.LengthSquared()>.25f?Vector3.Normalize(-Vector3.Normalize(info.Velocity)*.7f+info.Normal*.9f):info.Normal;plume.Transform.WorldRotation=FromTo(Vector3.UnitY,direction);plume.Intensity=info.Grounded?Math.Clamp((info.Speed*.075f+info.Slip*.12f)*DustAmount,0,2):0;if(plume.Intensity>.025f){if(!plume.IsEmitting)plume.Play(false);}else plume.Stop();}
  _feedbackGroundSpeed=contacts>0?speed/contacts:0;_feedbackSlip=contacts>0?slip/contacts:0;float speedLoad=Math.Clamp(_feedbackGroundSpeed/Math.Max(8,MaximumSpeed),0,1);
  // Audio-only gear bands with hysteresis; they never impose forces or alter the drivetrain.
  if(_feedbackGroundSpeed>_audioGear*8.5f+1&&_audioGear<4)_audioGear++;
  else if(_feedbackGroundSpeed<(_audioGear-1)*8.5f-1&&_audioGear>1)_audioGear--;
  float gearRev=Math.Clamp((_feedbackGroundSpeed-(_audioGear-1)*8.5f)/10,0,1);
  float target=Math.Clamp(Math.Abs(_feedbackThrottle)*.55f+speedLoad*.25f+Math.Clamp(_feedbackSlip/15,0,.2f),0,1);_engineLoad=FeedbackSmooth(_engineLoad,target,dt,target>_engineLoad?3:2);
  if(!_feedbackDriving&&engines.Length>0&&_vehicleAudio.TryGetValue("engine-start",out var ignition)){ignition.Volume=VehicleSfxVolume*.38f;ignition.Play();}_feedbackDriving=true;
  float rev=.86f+gearRev*.30f+_engineLoad*.12f;
  MixSound("engine-idle",engines.Length>0?.60f*(1-_engineLoad*.55f):0,.78f+_engineLoad*.12f,dt);
  MixSound("engine-drive",engines.Length>0?(.22f+1.15f*_engineLoad):0,rev,dt);
  MixSound("rolling",Math.Clamp(_feedbackGroundSpeed/35,0,.085f),.94f,dt);
  // Sand is a gritty slip hiss, not a sustained concrete tyre squeal.
  float sliding=Math.Clamp((_feedbackSlip-1.2f)*.035f,0,.18f);
  MixSound("gravel-brake",_feedbackGroundSpeed>1?Math.Max(sliding,_feedbackBrake?Math.Clamp(_feedbackGroundSpeed*.012f,0,.22f):0):0,.92f,dt);
  MixSound("tyre-skid",0,1,dt);
  foreach(var b in engines.Take(4)){foreach(int side in b.Type==24?new[]{-1,1}:new[]{0}){int key=b.Id*3+side+1;if(!_exhaust.TryGetValue(key,out var e)){var p=VehicleEffect("Engine exhaust "+b.Id+" / "+side,"engine-exhaust",VfxPreset.Smoke);e=(p,b,side);_exhaust[key]=e;}var matrix=_visuals[b.Id].Transform.WorldMatrix;var local=b.Type==23?new Vector3(-.38f,1.22f,.34f):new Vector3(side*.45f,.20f,.47f);var dir=b.Type==23?Vector3.UnitY:Vector3.Normalize(new Vector3(side*.08f,-.04f,.15f));e.Player.Transform.WorldPosition=Vector3.Transform(local,matrix);e.Player.Transform.WorldRotation=FromTo(Vector3.UnitY,Vector3.Normalize(Vector3.TransformNormal(dir,matrix)));e.Player.Intensity=(.22f+_engineLoad*.6f)*Math.Clamp(ExhaustAmount,0,2);if(e.Player.Intensity>.01f&&!e.Player.IsEmitting)e.Player.Play(false);else if(e.Player.Intensity<=.01f)e.Player.Stop();}}
 }
 void StopVehicleFeedback(){_feedbackDriving=false;_engineLoad=0;_audioGear=1;foreach(var s in _vehicleAudio.Values)s.Stop();foreach(var e in _exhaust.Values)e.Player.Stop(true);foreach(var d in _tyreDust.Values)d.Stop(true);}
 void ClearVehicleFeedback(){StopVehicleFeedback();if(_feedbackRoot?.Scene is {} scene)scene.DestroyGameObject(_feedbackRoot);_feedbackRoot=null;_vehicleAudio.Clear();_exhaust.Clear();_tyreDust.Clear();}
}
