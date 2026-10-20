using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Scene;
namespace Downhill;

/// <summary>Optional presentation adapter. Never changes the physical board.</summary>
public sealed class BoardFeedback3D:Component
{
 public string BoardObject {get;set;}="Board";
 public string RollingAudioObject {get;set;}="Wheel audio";
 public string WindAudioObject {get;set;}="Wind audio";
 public float WheelRadius {get;set;}=.037f;
 public float RollingVolume {get;set;}=.22f;
 public float WindVolume {get;set;}=.18f;
 BoardController3D? board;AudioSource3D? rolling,wind;float angle;
 readonly List<(GameObject Object,Quaternion Bind)> wheels=[];
 protected override void OnStart(){var scene=GameObject.Scene!;board=scene.FindGameObject(BoardObject)?.GetComponent<BoardController3D>();rolling=scene.FindGameObject(RollingAudioObject)?.GetComponent<AudioSource3D>();wind=scene.FindGameObject(WindAudioObject)?.GetComponent<AudioSource3D>();foreach(var obj in scene.GameObjects)if(obj.Name.StartsWith("Wheel ",StringComparison.Ordinal)&&obj.GetComponent<ByteEngine.Core.Graphics.ThreeD.MeshRenderer>()!=null){for(var parent=obj.Parent;parent!=null;parent=parent.Parent)if(parent==board?.GameObject){wheels.Add((obj,obj.Transform.LocalRotation));break;}}}
 protected override void OnUpdate(){if(board==null)return;float speed=board.ControlsEnabled&&GameObject.Scene!.SimulationTimeScale>0?board.Speed:0;angle=(angle+speed/Math.Max(.01f,WheelRadius)*(float)Time.DeltaTime)%(MathF.PI*2);foreach(var wheel in wheels)wheel.Object.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,angle)*wheel.Bind;if(rolling!=null){rolling.Volume=board.Grounded?RollingVolume*Math.Clamp(speed/12,0,1):0;rolling.Pitch=.65f+Math.Clamp(speed/15,0,1.5f);}if(wind!=null)wind.Volume=WindVolume*Math.Clamp(speed*speed/400,0,1);}
}
