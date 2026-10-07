using ByteEngine.Core.Scene;
namespace DuneCompany;
/// <summary>Native inspector settings; scene transforms are used as the authored build pose.</summary>
public sealed class DuneBlock3D:Component
{
 public int BlockId{get;set;}public int PartType{get;set;}=1;public int ParentBlock{get;set;}=-1;
 public bool MechanicalTuningAuthored{get;set;}
 public float Paint{get;set;}=0;public float SpringRate{get;set;}=4;public float Damping{get;set;}=1;public float Preload{get;set;}=0;public float SpeedLimit{get;set;}=32;public float BrakeStrength{get;set;}=1;
 public float Travel{get;set;}=.17f;public float Angle{get;set;}=35;public float Stroke{get;set;}=.42f;
 public float DriveMultiplier{get;set;}=1;public float SteeringAngle{get;set;}=30;public float TyreGrip{get;set;}=1;
}
