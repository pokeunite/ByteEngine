using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Gameplay;
using KarlsonDemo.Plugin;
internal static class MiningAudioMixCheck
{
 public static void Run(MiningOperation3D op,Action<bool,string> check)
 {
  var audio=MiningAudio3D.Find(op.GameObject)!;
  var deadlines=(System.Collections.IDictionary)typeof(MiningAudio3D).GetField("deadlines",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(audio)!;deadlines.Clear();
  var gains=Enum.GetValues<AudioBus>().Select(AudioMixer.GetVolume).ToArray();
  check(audio.PlayHoverCue(1)&&audio.LastCue=="InterfaceHover"&&audio.LastCueVolume>=.3f,"hover has an audible independent UI cue");
  check(audio.PlayCue(MiningSoundCue.TerminalSelect)&&audio.LastCue=="TerminalSelect"&&audio.LastCueVolume>=.5f&&audio.LastCueBus==AudioBus.Ui,"immediate click after hover is not swallowed by the hover cooldown");
  audio.Tick(.01f);deadlines.Remove(MiningSoundCue.DrillDamage);var hp=op.GameObject.Scene!.FindGameObject(op.DrillObjectId)!.GetComponent<HealthComponent>()!;float health=hp.CurrentHealth;hp.Damage(1);check(audio.LastCue=="DrillDamage"&&audio.LastCuePriority>=14&&audio.LastCueBus==AudioBus.Sfx,"actual drill damage has its own priority SFX cue");hp.CurrentHealth=health;
  check(audio.FeedbackMachineGain<1&&audio.FeedbackMusicGain<1,"damage feedback temporarily makes room in machine/music mix");
  check(Enum.GetValues<AudioBus>().Select(AudioMixer.GetVolume).SequenceEqual(gains),"mix focus preserves saved master, music, SFX and UI levels");
  typeof(MiningAudio3D).GetField("feedbackFocusUntil",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(audio,Time.TotalTime-1);
  check(audio.FeedbackMachineGain>=.99f,"temporary feedback mix recovers after its hold");deadlines.Clear();
 }
}
