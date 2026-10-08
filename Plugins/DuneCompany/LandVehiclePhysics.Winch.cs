using System.Numerics;
namespace DuneCompany;
public sealed partial class LandVehiclePhysics
{
 int _winch=-1;float _cableLength,_winchInput;public bool WinchConnected=>_winch>=0;
 public float CableLength=>_cableLength;public float CableTension{get;private set;}
 public Vector3 WinchPoint(int id){var p=PartPose(id);return p.Position+Vector3.Transform(new Vector3(0,.32f,0),p.Rotation);}
 public void SetWinchInput(float input)=>_winchInput=Math.Clamp(input,-1,1);
 public bool CanConnectWinch(int id){if(WinchConnected||RecoveryConnected||!HasRecovery||!_parts.TryGetValue(id,out var part)||part.Block.Type!=33||id>=RecoveryRootId)return false;float distance=Vector3.Distance(WinchPoint(id),TargetHitch);if(distance<.5f||distance>12||Velocity.Length()>2)return false;
  // Cable must clear ground; ignore the final socket neighbourhood only.
  var a=WinchPoint(id);var b=TargetHitch;for(float t=.04f;t<.94f;t+=.025f){var p=Vector3.Lerp(a,b,t);if(_terrain?.TrySampleWorld(p,out var ground,out _)==true&&p.Y<ground.Y+.015f)return false;}
  return true;
 }
 public bool ConnectWinch(int id){if(!CanConnectWinch(id))return false;_winch=id;_cableLength=Vector3.Distance(WinchPoint(id),TargetHitch);_winchInput=0;CableTension=0;return true;}
 public void DisconnectWinch(){_winch=-1;_winchInput=0;CableTension=0;}
 void ApplyWinch(float dt){if(!WinchConnected)return;var part=_parts[_winch];var truck=_simulation.Bodies[part.Root.Body];var target=_simulation.Bodies[_parts[RecoveryRootId].Root.Body];var a=WinchPoint(_winch);var b=TargetHitch;var delta=b-a;float distance=delta.Length();if(!float.IsFinite(distance)||distance>15){DisconnectWinch();return;}
  _cableLength=Math.Clamp(_cableLength-_winchInput*Math.Clamp(part.Block.WinchRate,.25f,1.5f)*dt,.7f,12);if(distance<=_cableLength){CableTension=0;return;}var n=delta/Math.Max(.001f,distance);var ra=a-truck.Pose.Position;var rb=b-target.Pose.Position;var va=truck.Velocity.Linear+Vector3.Cross(truck.Velocity.Angular,ra);var vb=target.Velocity.Linear+Vector3.Cross(target.Velocity.Angular,rb);
  CableTension=Math.Clamp((distance-_cableLength)*14000+Vector3.Dot(vb-va,n)*1800,0,12000*Math.Clamp(part.Block.Power,.25f,1.5f));var impulse=n*(CableTension*dt);_simulation.Awakener.AwakenBody(part.Root.Body);_simulation.Awakener.AwakenBody(_parts[RecoveryRootId].Root.Body);truck.ApplyImpulse(impulse,ra);target.ApplyImpulse(-impulse,rb);
 }
}
