using System.Numerics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
/// <summary>Scene-authored settings for the single recovery prototype.</summary>
public sealed class DuneRecoveryContract3D:Component
{
 public bool WinchMission{get;set;}
 public bool RunawayMission{get;set;}
 public bool RelayMission{get;set;}
 public bool AnchorMission{get;set;}
 public Vector3 ResetPosition{get;set;}=new(12,0,52);
 public string Title{get;set;}="Stranded in the sand";
 public int Payment{get;set;}=250;
 public float DepressionDepth{get;set;}=.24f;
 public float DepressionRadius{get;set;}=5;
 public float DeliveryHalfWidth{get;set;}=4.5f;
}
