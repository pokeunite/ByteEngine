using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Finite-speed screw sluice. Event sheets command a scene boolean; aperture and wheel motion share measured travel.</summary>
public sealed class SluiceActuator3D:Component
{
 public string CommandVariable {get;set;}="";
 public bool InvertCommand {get;set;}
 public float ClosedHeight {get;set;}
 public float Travel {get;set;}=1;
 public float Speed {get;set;}=.45f;
 public float InitialOpening {get;set;}
 public string WheelName {get;set;}="";
 public Vector3 WheelAxis {get;set;}=Vector3.UnitX;
 public float TurnsPerMetre {get;set;}=1;
 public float Opening {get;private set;}
 GameObject? wheel;Vector3 wheelRest;
 protected override void OnStart(){Opening=Math.Clamp(InitialOpening,0,1);wheel=GameObject.Scene!.FindGameObject(WheelName);wheelRest=wheel?.Transform.EulerAngles??Vector3.Zero;Apply();}
 protected override void OnFixedUpdate(){bool command=GameObject.Scene!.Variables[CommandVariable].Boolean;float target=(command!=InvertCommand)?1:0;Opening+=Math.Clamp(target-Opening,-Speed*(float)Time.FixedDeltaTime/Math.Max(.01f,Travel),Speed*(float)Time.FixedDeltaTime/Math.Max(.01f,Travel));Apply();}
 void Apply(){var p=Transform.WorldPosition;p.Y=ClosedHeight+Opening*Travel;Transform.WorldPosition=p;if(wheel!=null)wheel.Transform.EulerAngles=wheelRest+WheelAxis*(Opening-InitialOpening)*Travel*TurnsPerMetre*360;}
}

/// <summary>Rate-limited rotary valve, with a readable transition state for event logic.</summary>
public sealed class RotaryValve3D:Component
{
 public string CommandVariable {get;set;}="";
 public float TrueAngle {get;set;}
 public float FalseAngle {get;set;}=-90;
 public float DegreesPerSecond {get;set;}=45;
 public float InitialAngle {get;set;}
 public float Angle {get;private set;}
 public bool AtTarget {get;private set;}
 protected override void OnStart(){Angle=InitialAngle;}
 protected override void OnFixedUpdate(){float target=GameObject.Scene!.Variables[CommandVariable].Boolean?TrueAngle:FalseAngle;Angle+=Math.Clamp(target-Angle,-DegreesPerSecond*(float)Time.FixedDeltaTime,DegreesPerSecond*(float)Time.FixedDeltaTime);AtTarget=Math.Abs(target-Angle)<.1f;Transform.EulerAngles=new(0,Angle,0);}
}
