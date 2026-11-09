using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningGameFeel3D
{
 readonly Dictionary<int,AudioSource3D> feedbackVoices=[];
 readonly HashSet<Guid> winding=[];
 void PlayFeedback(AssetReference clip,float volume,int slot)
 {
  if(clip.IsEmpty||GameObject.Scene is not {} scene)return;
  if(!feedbackVoices.TryGetValue(slot,out var source)){var o=scene.CreateGameObject("Combat confirmation voice");o.SetParent(GameObject,false);source=o.AddComponent(new AudioSource3D{Spatial=false,Bus=AudioBus.Sfx,Priority=15});feedbackVoices[slot]=source;}
  source.Volume=Math.Clamp(volume,0,1);if(source.ClipReference!=clip||!source.ClipLoaded){source.Stop();if(!AudioRuntimeAssets.TryLoadClip(source,clip))return;}source.Stop();source.Play();
 }
 void TickThreats(float dt,MiningOperation3D op)
 {
  for(int i=threats.Count-1;i>=0;i--){var t=threats[i];t.Age+=dt;if(t.Age>.55f)threats.RemoveAt(i);else threats[i]=t;}
  if(!RigAttackWarning||GameObject.Scene?.FindGameObject(op.DrillObjectId) is not {} rig)return;
  foreach(var enemy in enemies)
  {
   if(enemy.Scene!=GameObject.Scene||enemy.GetComponent<HealthComponent>()?.IsDead!=false||enemy.GetComponent<MiningRaiderPriorities3D>()?.TargetingDrill!=true)continue;
   var brain=enemy.GetComponent<AIBehavior3D>();var combat=enemy.GetComponent<SimpleEnemyAI3D>();bool signalled=brain!=null&&signals.GetValueOrDefault(enemy.Id)!=brain.SignalSequence&&brain.LastSignal.Contains("windup",StringComparison.OrdinalIgnoreCase);if(brain!=null)signals[enemy.Id]=brain.SignalSequence;
   bool active=combat?.AttackPhase is "Windup" or "Telegraph";
   bool entering=active?winding.Add(enemy.Id):false;if(!active)winding.Remove(enemy.Id);
   if((entering||signalled)&&MiningCampGeometry.SurfaceDistance(rig,enemy.Transform.WorldPosition)<3){if(threats.Count==4)threats.RemoveAt(0);threats.Add((enemy.Transform.WorldPosition,0));duckHold=Math.Max(duckHold,.2f);if(rigSoundClock<=0){PlayFeedback(RigWarningSound,RigWarningVolume,2);rigSoundClock=.9f;}}
  }
 }
 protected override void OnLateUpdate()
 {
  var camera=GameObject.Scene?.ActiveCamera;
  if(presentationCamera!=null&&presentationCamera!=camera)presentationCamera.PresentationRotationOffset=Quaternion.Identity;
  presentationCamera=camera;if(camera==null)return;
  bool modal=Player?.GetComponent<MiningCampLayout3D>()?.IsEditing==true||Interface is {Screen:not MiningScreen.None};
  float amount=CameraImpulses&&!modal?kick*Math.Clamp(ShakeIntensity,0,1):0;
  camera.PresentationRotationOffset=Quaternion.CreateFromYawPitchRoll(MathF.Sin(kickAge*45)*amount*.22f*MathF.PI/180,-amount*MathF.PI/180,MathF.Sin(kickAge*32)*amount*.12f*MathF.PI/180);
 }
 protected override void OnRender(RenderContext c)
 {
  if(GameObject.Scene?.GetComponentsSnapshot<MiningAuxiliaryRefinery3D>().Any(r=>r.IsOpen)==true||GameObject.GetComponent<MiningResearchStation3D>()?.IsOpen==true||Player?.GetComponent<MiningCampLayout3D>()?.IsEditing==true||Interface is {Screen:not MiningScreen.None})return;
  float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),w=c.TargetWidth/s,h=c.TargetHeight/s;var gold=new Vector4(1,.79f,.2f,1);var white=new Vector4(.97f,.96f,.9f,1);
  void Q(float x,float y,float width,float height,Vector4 color)=>c.DrawUiQuad(new(x*s,y*s),new(width*s,height*s),color);
  void T(string text,float x,float y,int size,Vector4 color,UiAnchor anchor=UiAnchor.TopLeft)=>c.DrawUiText(text,Font,(int)(size*s),new(x*s,y*s),color,anchor);
  var centre=new Vector2(w/2,h/2);
  if(Time.TotalTime<defenceLossUntil){Q(20,290,360,56,new(.15f,.035f,.02f,.95f));T(defenceLossTitle,32,296,18,new(1,.6f,.25f,1));T(defenceLossDetail,32,323,15,white);}
  if((HitConfirmation&&hitClock>0)||(KillConfirmation&&killClock>0))
  {
   bool kill=KillConfirmation&&killClock>0;var color=kill?gold:white;float r=kill?11:8;
   foreach(var d in new[]{new Vector2(1,1),new Vector2(1,-1),new Vector2(-1,1),new Vector2(-1,-1)})c.DrawUiLine((centre+d*r)*s,(centre+d*(r+5))*s,2*s,color);
   if(kill)T("KILL",w/2,h/2+24,12,gold,UiAnchor.TopCenter);
  }
  if(DirectionalHurt&&hurtClock>0)
  {
   float alpha=Math.Clamp(hurtClock/.55f,0,1)*.55f;var red=new Vector4(1,.13f,.08f,alpha);
   if(!float.IsFinite(hurtDirection.LengthSquared())||hurtDirection.LengthSquared()<.001f){Q(0,0,w,6,red);Q(0,h-6,w,6,red);Q(0,0,6,h,red);Q(w-6,0,6,h,red);}
   else if(GameObject.Scene?.ActiveCamera is {} camera){var direction=Vector3.Normalize(hurtDirection);float x=Vector3.Dot(direction,camera.Transform.Right),forward=Vector3.Dot(direction,camera.Transform.Forward);var v=new Vector2(x,-forward);if(v.LengthSquared()>.001f)v=Vector2.Normalize(v);var p=centre+v*105;var side=new Vector2(-v.Y,v.X);c.DrawUiLine((p-v*14-side*9)*s,p*s,5*s,red);c.DrawUiLine(p*s,(p-v*14+side*9)*s,5*s,red);}
  }
  if(playerHealth is {IsDead:false,HealthPercent:<.25f}){Q(20,h-27,280,4,new(1,.2f,.08f,.5f+.2f*MathF.Sin((float)Time.TotalTime*6)));}
  if(RigAttackWarning&&threats.Count>0){Q(20,h-163,325,26,new(.13f,.06f,.02f,.95f));T("INCOMING RIG STRIKE",32,h-159,16,gold);}
  if(c.Has3DCamera&&RigAttackWarning)foreach(var threat in threats)
  {
   var p=Vector4.Transform(new Vector4(threat.Position+Vector3.UnitY*1.2f,1),c.GetViewMatrix3D()*c.GetProjectionMatrix3D());if(p.W<=0)continue;float x=(p.X/p.W+1)*w/2,y=(1-p.Y/p.W)*h/2;
   T("!",x,y,24,new(1,.4f,.1f,1),UiAnchor.TopCenter);
  }
  if(AmmoGainPopup&&ammoClock>0){Q(20,h-205,285,30,new(.04f,.05f,.06f,.94f));T($"+{AmmoGain} RESERVE AMMO",32,h-199,17,gold);}
  if(CreditGainPopup&&creditClock>0){Q(20,250,280,30,new(.04f,.05f,.06f,.94f));T($"+{CreditGain:N0} CREDITS RECEIVED",30,256,17,gold);}
  if(RoundResults&&Player?.GetComponent<MiningDefensePlanner3D>()?.BuildMode!=true&&resultClock>0&&Op?.OverclockAvailable!=true&&Op?.IsOverclockActive!=true){Q(w/2-270,h-159,540,50,new(.04f,.05f,.06f,.95f));T(RoundResult,w/2,h-153,16,gold,UiAnchor.TopCenter);T("Refine your ore or return to the company tent",w/2,h-130,15,white,UiAnchor.TopCenter);}
 }
}
