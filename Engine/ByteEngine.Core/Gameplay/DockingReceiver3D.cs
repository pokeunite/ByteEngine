using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Physics;
namespace ByteEngine.Core.Gameplay;

/// <summary>Opt-in cargo receiver. Capture requires both carrier and cargo in the receiving zone.
/// The carrier settles at a mooring before a visible unload arc completes. Events own sound/UI.</summary>
public sealed class DockingReceiver3D:Component
{
 public string CarrierName {get;set;}="";
 public string CargoName {get;set;}="";
 public Vector3 CaptureCenter {get;set;}
 public Vector3 CaptureSize {get;set;}=new(2,4,3);
 public Vector3 MooringPosition {get;set;}
 public Vector3 CargoDestination {get;set;}
 public float SettleSeconds {get;set;}=.8f;
 public float UnloadSeconds {get;set;}=1.3f;
 public float LiftHeight {get;set;}=.55f;
 public bool Captured {get;private set;}
 public bool Delivered {get;private set;}
 public float Progress {get;private set;}
 GameObject? carrier,cargo;Vector3 start,cargoStart;float elapsed;
 protected override void OnStart(){carrier=GameObject.Scene?.FindGameObject(CarrierName);cargo=GameObject.Scene?.FindGameObject(CargoName);}
 bool Inside(Vector3 p){var d=Vector3.Abs(p-CaptureCenter);return d.X<=CaptureSize.X*.5f&&d.Y<=CaptureSize.Y*.5f&&d.Z<=CaptureSize.Z*.5f;}
 protected override void OnFixedUpdate(){
  if(carrier==null||cargo==null||Delivered)return;
  if(!Captured){if(!Inside(carrier.Transform.WorldPosition)||!Inside(cargo.Transform.WorldPosition))return;Captured=true;start=carrier.Transform.WorldPosition;cargoStart=cargo.Transform.WorldPosition;Freeze(carrier);Freeze(cargo);}
  elapsed+=(float)Time.FixedDeltaTime;
  float settle=Math.Clamp(elapsed/Math.Max(.01f,SettleSeconds),0,1);settle=settle*settle*(3-2*settle);
  carrier.Transform.WorldPosition=Vector3.Lerp(start,MooringPosition,settle);
  Vector3 settledCargo=cargoStart+MooringPosition-start;
  if(elapsed<=SettleSeconds)cargo.Transform.WorldPosition=Vector3.Lerp(cargoStart,settledCargo,settle);
  else{float t=Math.Clamp((elapsed-SettleSeconds)/Math.Max(.01f,UnloadSeconds),0,1);float ease=t*t*(3-2*t);cargo.Transform.WorldPosition=Vector3.Lerp(settledCargo,CargoDestination,ease)+Vector3.UnitY*(MathF.Sin(t*MathF.PI)*LiftHeight);Delivered=t>=1;}
  Progress=Math.Clamp(elapsed/Math.Max(.01f,SettleSeconds+UnloadSeconds),0,1);
 }
 static void Freeze(GameObject obj){if(obj.GetComponent<Rigidbody3D>() is {} b){b.Velocity=Vector3.Zero;b.BodyType=RigidbodyBodyType3D.Kinematic;}}
}
