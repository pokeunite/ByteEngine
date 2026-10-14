using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed class DuneSprintContract3D:Component
{
 public string OpponentBlueprint{get;set;}="Assets/RecoveryBlueprints/Recovery-demo.json";
 public float OpponentSpeed{get;set;}=22;
 public int ContractIndex{get;set;}=4;
 public string RaceKind{get;set;}="Sprint";
 public bool DragLaneAssist{get;set;}=true;
 public Guid DrivingRouteObject{get;set;}
}
