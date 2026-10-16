using ByteEngine.Core.Scene;
using ByteEngine.Core.Audio;
namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Reusable flux-driven sound bed. Attach an Audio Source with a looping water clip.</summary>
public sealed class WaterFlowAudio3D : Component
{
    public string FlowGate {get;set;}="";
    public string FlowPathName {get;set;}="";
    public float Gain {get;set;}=.13f;
    public float MaximumVolume {get;set;}=.5f;
    public float MinimumFlow {get;set;}=.015f;
    public float Activity {get;private set;}
    WaterGate3D? gate;WaterFlowPath3D? path;AudioSource3D? source;
    protected override void OnStart(){gate=GameObject.Scene!.FindGameObject(FlowGate)?.GetComponent<WaterGate3D>();path=GameObject.Scene.FindGameObject(FlowPathName)?.GetComponent<WaterFlowPath3D>();source=GameObject.GetComponent<AudioSource3D>();if(source!=null){source.Loop=true;source.Volume=0;}}
    protected override void OnLateUpdate(){if(source==null)return;if(Time.DeltaTime<=0){if(source.IsPlaying)source.Pause();return;}float flux=Math.Abs(path?.Outflow??gate?.Flow??0),target=flux<MinimumFlow?0:Math.Min(MaximumVolume,MathF.Sqrt(flux)*Gain);Activity+=(target-Activity)*(1-MathF.Exp(-(float)Time.DeltaTime*(target>Activity?7:3)));source.Volume=Activity;source.Pitch=.85f+Math.Min(.35f,flux*.035f);if(Activity>.005f&&!source.IsPlaying)source.Play();else if(Activity<.003f&&source.IsPlaying)source.Stop();}
}
