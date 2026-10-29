using System.Reflection;
using System.Numerics;
using System.Security.Cryptography;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningPauseAudioCheck
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS PAUSE/AUDIO "+text);}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var original=SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));var host=new SceneManager();host.LoadScene(scene);
  void Frame(int n=1){for(int i=0;i<n;i++){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}}
  Frame(4);var audio=op.GameObject.GetComponent<MiningAudio3D>()!;var menu=op.GameObject.GetComponent<MiningPauseMenu3D>()!;menu.PersistSettings=false;
  Check(audio.BattleMusic.ProjectPath.EndsWith("RailJet.wav"),"selected RailJet track is assigned");
  Check(audio.RefineryVolumeMultiplier==4&&audio.RefineryConveyorVolume>=.58f&&audio.RefineryOreVolume>=.8f,"refinery motor, conveyor and ore impacts are louder");
  Check(audio.PlayCue(MiningSoundCue.Footstep),"clean sand-footstep recording plays through native audio");
  var step=scene.GameObjects.Last(o=>o.Name=="Mining audio voice - Footstep").GetComponent<AudioSource3D>()!;Check(step.Bus==AudioBus.Sfx&&step.Pitch>=.94f&&step.Pitch<=1.06f,"footsteps use SFX routing and varied pitch");
  Check(audio.PlayCue(MiningSoundCue.AmmoPickup),"ammo collection reload sound plays");var pickup=scene.GameObjects.Last(o=>o.Name=="Mining audio voice - AmmoPickup").GetComponent<AudioSource3D>()!;
  Check(pickup.ClipReference.ProjectPath.EndsWith("pistol-reload.wav")&&Math.Abs(pickup.Pitch-1.15f)<.001f,"ammo pickup reuses the reload sound sped up fifteen percent");
  Check(op.StartOperation(),"drilling operation starts");Frame(40);var battle=scene.GameObjects.Single(o=>o.Name=="Mining combat music").GetComponent<AudioSource3D>()!;
  Check(battle.IsPlaying&&battle.Loop&&!battle.Spatial&&battle.Bus==AudioBus.Music&&battle.Volume>.25f,"RailJet fades in as non-spatial looping combat music");
  menu.Open();double time=scene.SimulationTime;int alive=op.SpawnedEnemies.Count;var player=scene.FindGameObject(op.PlayerObjectId)!;var before=player.Transform.WorldPosition;Frame(40);
  Check(menu.IsOpen&&scene.SimulationTime==time&&op.SpawnedEnemies.Count==alive&&player.Transform.WorldPosition==before,"pause freezes the active wave, player and simulation clock");
  Check(Input.IsUiPointerRequested&&Input.HasGameplayEscapeHandler,"pause owns Escape and keeps the cursor available");
  menu.ShowPage(MiningPausePage.Sound);var raw=new RawInputSnapshot();raw.MouseButtonsDown.Add(MouseButton.Left);
  typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[raw,true]);Input.SetGameViewPointer(new(805f/1280,226f/720),new(1280,720),true);Frame();
  Check(Math.Abs(AudioMixer.GetVolume(AudioBus.Master)-.5f)<.02f,"sound slider responds to actual pointer input while paused");AudioMixer.SetVolume(AudioBus.Master,1);
  typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[new RawInputSnapshot(),true]);
  menu.ShowPage(MiningPausePage.Controls);var wheel=new RawInputSnapshot{MouseWheel=-100};typeof(Input).GetMethod("UpdatePortable",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[wheel,true]);Frame();Check((float)typeof(MiningPauseMenu3D).GetField("controlsScroll",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(menu)!>7,"mouse wheel scrolls to the lower defence and mouse bindings");Check(menu.Rebind(Key.E,Key.F),"controls accept a new interaction key");menu.Resume();
  Check(!menu.IsOpen&&!Input.IsUiPointerRequested&&scene.SimulationTimeScale==1&&Input.IsGameInputCaptured,"Resume restores captured gameplay without clicking");Frame(4);Check(scene.SimulationTime>time,"simulation resumes after closing pause");
  host.UnloadScene();Check(!Input.HasGameplayEscapeHandler&&!battle.IsPlaying,"scene shutdown releases pause ownership and music");Check(original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"))),"verification leaves authored scene and progress untouched");
 }
}
