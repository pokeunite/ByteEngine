using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningAudioVerify
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS AUDIO "+message);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var host=new SceneManager();host.LoadScene(scene);var director=op.GameObject.GetComponent<MiningAudio3D>()!;Check(director!=null,"editable mining audio component is installed");
  void Frame(float dt=.05f){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[(double)dt]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}
  Frame();Check(director.Confirm==director.Notification&&director.Credits==director.Notification,"rejected purchase and coin cues use the approved notification instead");Check(director.PistolShot.ProjectPath=="Assets/Audio/Weapons/First Hunt/pistol-shot.wav","original pistol recording restored");Check(director.DrillVolumeMultiplier>=2.5f&&director.PressVolumeMultiplier>=3&&director.MachineHearingDistance>=8,"machine gain and hearing distance increased");
  foreach(var p in typeof(MiningAudio3D).GetProperties().Where(p=>p.PropertyType==typeof(ByteEngine.Core.Assets.AssetReference)))
  {
   var asset=(ByteEngine.Core.Assets.AssetReference)p.GetValue(director)!;Check(!asset.IsEmpty,"assigned clip "+p.Name);Check(AudioClip.TryLoadWave(Program.Root+"/"+asset.ProjectPath,out var clip),"native decoder accepts "+p.Name);Check((clip!.Channels==1||(p.Name=="CampMusic"||p.Name=="BattleMusic")&&clip.Channels==2)&&clip.BitsPerSample==16&&clip.SampleRate==48000,"mono PCM spatial-ready "+p.Name);clip.Dispose();
  }
  Check(director.LoopCount==6,"machine, conveyor and alarm loop count stays bounded");var loops=scene.GameObjects.Where(o=>o.Name.StartsWith("Mining loop - ")).ToArray();Check(loops.All(o=>o.GetComponent<AudioSource3D>()!.ClipLoaded),"all six loop clips load into native audio");Check(loops.Single(o=>o.Name=="Mining loop - Press").GetComponent<AudioSource3D>()!.IsPlaying,"press operating loop actually plays");
  director.AutomaticCues=false;Frame();Check(loops.All(o=>!o.GetComponent<AudioSource3D>()!.IsPlaying),"disabling automatic cues stops automatic machine loops");
  foreach(var cue in Enum.GetValues<MiningSoundCue>()){Frame(.9f);Check(director.PlayCue(cue),"native playback "+cue);Check(!director.PlayCue(cue),"same-frame cue spam suppressed "+cue);foreach(var o in scene.GameObjects.Where(o=>o.Name.StartsWith("Mining audio voice - ")))o.GetComponent<AudioSource3D>()!.Stop();}
  Check(director.PooledVoices<=director.MaximumOneShotVoices,"one-shot source count respects its limit");
  director.AutomaticCues=true;Frame();
  var equipped=scene.FindGameObject(op.PlayerObjectId)!.GetComponent<FpsGunLab3D>()!.SelectedWeapon!;int ammo=equipped.CurrentAmmo,reserve=equipped.ReserveAmmo;equipped.SetAmmo(0,0);Frame(1);
  var raw=new RawInputSnapshot();raw.MouseButtonsDown.Add(MouseButton.Left);typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[raw,true]);typeof(Input).GetMethod("SetGameInputCaptured",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[true]);Frame();Check(director.LastCue==MiningSoundCue.DryTrigger.ToString(),"empty trigger plays through actual mouse input even when Can Fire blocks weapon events");typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[new RawInputSnapshot(),true]);equipped.SetAmmo(ammo,reserve);Frame(1);
  int before=director.PlayedCues;scene.FindGameObject(op.PlayerObjectId)!.GetComponent<HealthComponent>()!.Damage(2);Check(director.PlayedCues==before+1&&director.LastCue==MiningSoundCue.PlayerHurt.ToString(),"actual player damage triggers chosen hurt sound");
  Frame(1);scene.FindGameObject(op.DrillObjectId)!.GetComponent<HealthComponent>()!.Damage(2);Check(director.LastCue==MiningSoundCue.MetalHit.ToString(),"actual drill damage triggers selected metal impact");
  Check(op.StartOperation(),"audio check launches an operation");Frame(1);var drill=scene.FindGameObject(op.DrillObjectId)!.GetComponent<HealthComponent>()!;drill.Damage(drill.MaxHealth*.75f);Frame(1);Check(loops.Single(o=>o.Name=="Mining loop - Critical drill alarm").GetComponent<AudioSource3D>()!.IsPlaying,"critical rig health starts alarm");drill.Kill();Frame(1);Frame(1);Check(loops.All(o=>!o.GetComponent<AudioSource3D>()!.IsPlaying),"failure stops operating loops and the alarm");
  var sources=scene.GameObjects.SelectMany(o=>o.Components).OfType<AudioSource3D>().ToArray();host.UnloadScene();Check(sources.All(s=>!s.IsPlaying),"scene shutdown stops every audio source");Console.WriteLine("MINING AUDIO NATIVE VERIFICATION COMPLETE");
 }
}
