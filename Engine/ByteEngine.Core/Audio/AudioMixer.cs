namespace ByteEngine.Core.Audio;
public enum AudioBus {Master,Music,Sfx,Ui}
/// <summary>Shared native/browser gain routing, transient ducking and priority-based voice budgets.</summary>
public static class AudioMixer
{
    static readonly float[] Gains=[1,1,1,1];
    static readonly Dictionary<AudioBus,(float Factor,double End,float Release)> Ducks=new();
    static readonly List<WeakReference<AudioSource3D>> Voices=new();
    public static int MaximumVoices {get;set;}=64;
    public static int ActiveVoices {get{Clean();return Voices.Count;}}
    public static float GetVolume(AudioBus bus){if(!Enum.IsDefined(bus))throw new ArgumentOutOfRangeException(nameof(bus));return Gains[(int)bus];}
    public static void SetVolume(AudioBus bus,float value){if(!Enum.IsDefined(bus))throw new ArgumentOutOfRangeException(nameof(bus));if(!float.IsFinite(value))throw new ArgumentOutOfRangeException(nameof(value));Gains[(int)bus]=Math.Clamp(value,0,1);}
    public static void Duck(AudioBus bus,float factor,float holdSeconds,float releaseSeconds=.25f)
    {
        GetVolume(bus);if(!float.IsFinite(factor+holdSeconds+releaseSeconds))throw new ArgumentOutOfRangeException(nameof(factor));
        Ducks[bus]=(Math.Clamp(factor,0,1),Time.TotalTime+Math.Max(0,holdSeconds),Math.Max(.001f,releaseSeconds));
    }
    static float DuckGain(AudioBus bus)
    {
        if(!Ducks.TryGetValue(bus,out var duck))return 1;
        double elapsed=Time.TotalTime-duck.End;
        if(elapsed>=duck.Release){Ducks.Remove(bus);return 1;}
        return elapsed<=0?duck.Factor:duck.Factor+(1-duck.Factor)*(float)(elapsed/duck.Release);
    }
    public static float EffectiveVolume(AudioBus bus,float sourceVolume)=>sourceVolume*GetVolume(AudioBus.Master)*DuckGain(AudioBus.Master)*(bus==AudioBus.Master?1:GetVolume(bus)*DuckGain(bus));
    static void Clean()=>Voices.RemoveAll(v=>!v.TryGetTarget(out var source)||!source.IsPlaying);
    internal static bool AcquireVoice(AudioSource3D source)
    {
        Clean();if(Voices.Any(v=>v.TryGetTarget(out var current)&&ReferenceEquals(current,source)))return true;
        int limit=Math.Clamp(MaximumVoices,1,256);
        if(Voices.Count>=limit)
        {
            var victim=Voices.Select(v=>v.TryGetTarget(out var s)?s:null).Where(s=>s!=null).OrderBy(s=>s!.Priority).FirstOrDefault();
            if(victim==null||victim.Priority>source.Priority)return false;victim.Stop();
        }
        Voices.Add(new(source));return true;
    }
    internal static void ReleaseVoice(AudioSource3D source)=>Voices.RemoveAll(v=>!v.TryGetTarget(out var current)||ReferenceEquals(current,source));
    public static void Reset(){foreach(var reference in Voices.ToArray())if(reference.TryGetTarget(out var source))source.Stop();Array.Fill(Gains,1);Ducks.Clear();Voices.Clear();MaximumVoices=64;}
}
