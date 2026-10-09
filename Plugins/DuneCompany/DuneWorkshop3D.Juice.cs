using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 GameObject? _recoveryNotice;UiText? _recoveryNoticeText;UiWidget? _recoveryNoticePanel;
 readonly Dictionary<string,AudioSource3D> _jobSounds=[];
 float _noticeTime;bool _wasJobConnected,_wasJobCompleted;int _lastCredits;
 void EnsureJobFeedback(){
  if(_recoveryNotice!=null)return;
  var canvas=GameObject.Scene!.GameObjects.FirstOrDefault(o=>o.GetComponent<UiCanvas>()!=null);if(canvas==null)return;
  _recoveryNotice=GameObject.Scene.CreateGameObject("Recovery feedback");_recoveryNotice.SetParent(canvas,false);
  _recoveryNoticePanel=_recoveryNotice.AddComponent(new UiWidget{Anchor=UiAnchor.BottomCenter,Offset=new(0,-90),Size=new(440,48),Color=new(.055f,.07f,.06f,.95f),Interactable=false,OrderInLayer=90});
  var label=GameObject.Scene.CreateGameObject("Recovery feedback text");label.SetParent(_recoveryNotice,false);
  _recoveryNoticeText=label.AddComponent(new UiText{Offset=new(18,10),FontSize=23,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Color=new(.95f,.78f,.40f,1),OrderInLayer=91});
  _recoveryNotice.Active=false;
 }
 void JobSound(string name,float volume=.65f,bool loop=false){
  if(!_jobSounds.TryGetValue(name,out var source)){
   string reference="Assets/Audio/Feedback/"+name+".wav";if(!AudioClip.TryLoadWave(Path.Combine(ProjectRoot,reference),out var clip))return;
   var obj=GameObject.Scene!.CreateGameObject("Job sound - "+name);source=obj.AddComponent(new AudioSource3D{Spatial=false,Bus=name.StartsWith("menu-")?AudioBus.Ui:AudioBus.Sfx,ClipReference=new(reference),PlayOnStart=false,Loop=loop});source.SetClip(clip);_jobSounds[name]=source;
  }
  source.Volume=Math.Clamp(volume*(name.StartsWith("menu-")?1:VehicleSfxVolume),0,1);if(!loop||!source.IsPlaying)source.Play();
 }
 void JobNotice(string text){EnsureJobFeedback();if(_recoveryNoticeText==null)return;_recoveryNoticeText.Text=text;_noticeTime=3;_recoveryNotice!.Active=true;}
 void ClearJobFeedback(){foreach(var sound in _jobSounds.Values){sound.Stop();if(sound.GameObject.Scene is {} scene)scene.DestroyGameObject(sound.GameObject);}_jobSounds.Clear();if(_recoveryNotice?.Scene is {} owner)owner.DestroyGameObject(_recoveryNotice);_recoveryNotice=null;_recoveryNoticeText=null;_recoveryNoticePanel=null;}
 void UpdateJobFeedback(float dt){
  bool active=!Building&&_contractRun;
  bool connected=active&&(WinchMission?WinchConnected:RecoveryConnected);
  if(active&&!_contractComplete&&connected!=_wasJobConnected){JobNotice(connected?(WinchMission?"CABLE CONNECTED / E to reel in":"HITCH LOCKED / Ready to tow"):(WinchMission?"CABLE DETACHED":"HITCH RELEASED"));JobSound(connected?"hitch-lock":"hitch-release");}
  if(active&&_contractComplete&&!_wasJobCompleted){JobNotice($"PAYMENT RECEIVED  +${Math.Max(0,_credits-_lastCredits)}");JobSound("contract-victory",.85f);_noticeTime=5;}
  if(active&&WinchConnected&&!_contractComplete&&Input.IsKeyDown(Key.E))JobSound("winch-motor",.25f+Math.Clamp(WinchTension/15000,0,.3f),true);
  else if(_jobSounds.TryGetValue("winch-motor",out var motor))motor.Stop();
  _wasJobConnected=connected;_wasJobCompleted=active&&_contractComplete;_lastCredits=_credits;
  _noticeTime=Math.Max(0,_noticeTime-dt);if(_recoveryNotice!=null){_recoveryNotice.Active=active&&_noticeTime>0&&(!WorkshopV3||!_contractComplete);if(_recoveryNoticePanel!=null)_recoveryNoticePanel.Color=new(.055f,.07f,.06f,.95f*Math.Clamp(_noticeTime/.3f,0,1));}
 }
}
