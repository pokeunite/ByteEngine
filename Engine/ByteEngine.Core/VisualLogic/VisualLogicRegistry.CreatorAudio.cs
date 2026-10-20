using ByteEngine.Core.Audio;
using ByteEngine.Core.Variables;
namespace ByteEngine.Core.VisualLogic;
public sealed partial class VisualLogicRegistry
{
    private static void RegisterCreatorAudio(VisualLogicRegistry r)
    {
        AudioSource3D? Source(VisualInstruction i,EventExecutionContext c)=>ResolveObjectTarget(i,c)?.GetComponent<AudioSource3D>();
        AudioBus Bus(VisualInstruction i,EventExecutionContext c)=>Enum.TryParse<AudioBus>(S(i,c,"bus","Sfx"),true,out var bus)&&Enum.IsDefined(bus)?bus:throw new ArgumentException("Choose Master, Music, Sfx or Ui.");
        var busArg=TextArg("bus","Audio bus (Master, Music, Sfx, Ui)","Sfx");
        Add(r,"audio.setBus","Audio / Mixer","Route audio source to bus",(i,c)=>{if(Source(i,c) is {} source)source.Bus=Bus(i,c);},ObjectArg(),busArg);
        Add(r,"audio.setBusVolume","Audio / Mixer","Set audio bus volume",(i,c)=>AudioMixer.SetVolume(Bus(i,c),(float)N(i,c,"volume",1)),busArg,NumberArg("volume","Volume (0 to 1)",1));
        Add(r,"audio.readBusVolume","Audio / Mixer","Read audio bus volume",(i,c)=>Output(i,c,VariableValue.FromNumber(AudioMixer.GetVolume(Bus(i,c)))),busArg,TextArg("scope","Variable scope","Self"),TextArg("output","Result variable","BusVolume"));
        Add(r,"audio.duckBus","Audio / Mixer","Temporarily lower audio bus volume",(i,c)=>AudioMixer.Duck(Bus(i,c),(float)N(i,c,"factor",.35),(float)N(i,c,"hold",1),(float)N(i,c,"release",.25)),busArg,NumberArg("factor","Volume multiplier",.35),NumberArg("hold","Hold seconds",1),NumberArg("release","Release seconds",.25));
        Add(r,"audio.stopBus","Audio / Mixer","Stop all scene sources on audio bus",(i,c)=>{var bus=Bus(i,c);foreach(var source in c.Scene.GameObjects.SelectMany(o=>o.Components).OfType<AudioSource3D>())if(bus==AudioBus.Master||source.Bus==bus)source.Stop();},busArg);
        Add(r,"audio.seek","Audio / Playback","Seek audio playback",(i,c)=>Source(i,c)?.Seek(N(i,c,"seconds")),ObjectArg(),NumberArg("seconds","Playback seconds"));
        Test(r,"audio.isPaused","Audio / Playback","Audio is paused",(i,c)=>Source(i,c)?.IsPaused==true,ObjectArg());
        Test(r,"audio.isStopped","Audio / Playback","Audio is stopped",(i,c)=>Source(i,c)?.IsStopped==true,ObjectArg());
        Test(r,"audio.hasFinished","Audio / Playback","Audio completed naturally",(i,c)=>Source(i,c)?.HasFinished==true,ObjectArg());
        Add(r,"audio.readPosition","Audio / Playback","Read audio playback position",(i,c)=>Output(i,c,VariableValue.FromNumber(Source(i,c)?.PlaybackPosition??0)),ObjectArg(),TextArg("scope","Variable scope","Self"),TextArg("output","Result variable","AudioPosition"));
        Add(r,"audio.readDuration","Audio / Playback","Read audio duration",(i,c)=>Output(i,c,VariableValue.FromNumber(Source(i,c)?.DurationSeconds??0)),ObjectArg(),TextArg("scope","Variable scope","Self"),TextArg("output","Result variable","AudioDuration"));
    }
}
