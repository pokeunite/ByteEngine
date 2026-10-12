using System.Numerics;
namespace DuneCompany;
public sealed partial class LandVehiclePhysics
{
 float CableMaximum(int type)=>type==35?40:MaximumCableLength;
 sealed class Cable {public Vector3? Anchor;public float Length,Input,Tension,Flight,FlightTime;}
 readonly Dictionary<int,Cable> _cables=[];
 public float MaximumCableLength{get;set;}=24;
 public float MaximumHookSpeed{get;set;}=2;
 public float MaximumHookRelativeSpeed{get;set;}=float.MaxValue;
 public float HookReach{get;set;}=12;
 public bool WinchConnected=>_cables.Values.Any(c=>c.Anchor==null&&c.Flight<=0);
 public bool AnchorConnected=>_cables.Values.Any(c=>c.Anchor!=null);
 public float CableLength=>_cables.Values.FirstOrDefault(c=>c.Anchor==null)?.Length??0;
 public float CableTension=>_cables.Values.Sum(c=>c.Tension);
 public Vector3 WinchPoint(int id){var p=PartPose(id);return p.Position+Vector3.Transform(new Vector3(0,.32f,0),p.Rotation);}
 public bool IsWinchConnected(int id)=>_cables.ContainsKey(id);
 public bool IsAnchorCable(int id)=>_cables.TryGetValue(id,out var c)&&c.Anchor!=null;
 public float WinchLength(int id)=>_cables.TryGetValue(id,out var c)?c.Length:0;
 public float WinchForce(int id)=>_cables.TryGetValue(id,out var c)?c.Tension:0;
 public Vector3 WinchVisualTarget(int id){var end=WinchTarget(id);return _cables.TryGetValue(id,out var c)&&c.FlightTime>0?Vector3.Lerp(WinchPoint(id),end,1-c.Flight/c.FlightTime):end;}
 public Vector3 WinchTarget(int id)=>_cables.TryGetValue(id,out var c)?c.Anchor??TargetHitch:TargetHitch;
 public void SetWinchInput(float input){foreach(var c in _cables.Values.Where(c=>c.Anchor==null))c.Input=Math.Clamp(input,-1,1);}
 public void SetWinchInput(int id,float input){if(_cables.TryGetValue(id,out var c))c.Input=Math.Clamp(input,-1,1);}
 public bool CanConnectWinch(int id)=>CanConnectCable(id,null);
 public bool CanConnectCable(int id,Vector3? anchor){
  if(_cables.ContainsKey(id)||!HasRecovery||!_parts.TryGetValue(id,out var part)||part.Block.Type is not (33 or 35 or 36)||id>=RecoveryRootId)return false;
  var a=WinchPoint(id);var b=anchor??TargetHitch;float distance=Vector3.Distance(a,b);
  if(!float.IsFinite(distance)||distance<.5f||distance>Math.Min(part.Block.Type==35?22:HookReach,CableMaximum(part.Block.Type))||Velocity.Length()>MaximumHookSpeed||(anchor==null&&(Velocity-RecoveryVelocity).Length()>MaximumHookRelativeSpeed))return false;
  for(float t=.04f;t<.94f;t+=.025f){var p=Vector3.Lerp(a,b,t);if(_terrain?.TrySampleWorld(p,out var ground,out _)==true&&p.Y<ground.Y+.015f)return false;}
  return true;
 }
 public bool ConnectWinch(int id)=>ConnectCable(id,null);
 public bool ConnectCable(int id,Vector3? anchor){if(!CanConnectCable(id,anchor))return false;_cables[id]=new(){Anchor=anchor,Length=Vector3.Distance(WinchPoint(id),anchor??TargetHitch),Flight=Vector3.Distance(WinchPoint(id),anchor??TargetHitch)/35,FlightTime=Vector3.Distance(WinchPoint(id),anchor??TargetHitch)/35};return true;}
 public void DisconnectWinch()=>_cables.Clear();
 public void DisconnectWinch(int id)=>_cables.Remove(id);
 void ApplyWinch(float dt){
  foreach(var (id,c) in _cables.ToArray()){
   if(!_parts.TryGetValue(id,out var part)){_cables.Remove(id);continue;}
   var truck=_simulation.Bodies[part.Root.Body];var target=_simulation.Bodies[_parts[RecoveryRootId].Root.Body];
   var a=WinchPoint(id);var b=c.Anchor??TargetHitch;var delta=b-a;float distance=delta.Length();
   if(!float.IsFinite(distance)||distance>CableMaximum(part.Block.Type)+3){_cables.Remove(id);continue;}
   if(c.Flight>0){c.Flight=Math.Max(0,c.Flight-dt);c.Tension=0;continue;}
   c.Length=Math.Clamp(c.Length-c.Input*Math.Clamp(part.Block.WinchRate,.25f,1.5f)*dt,.7f,CableMaximum(part.Block.Type));
   if(distance<=c.Length){c.Tension=0;continue;}
   var n=delta/Math.Max(.001f,distance);var ra=a-truck.Pose.Position;var rb=b-target.Pose.Position;
   var va=truck.Velocity.Linear+Vector3.Cross(truck.Velocity.Angular,ra);var vb=c.Anchor!=null?Vector3.Zero:target.Velocity.Linear+Vector3.Cross(target.Velocity.Angular,rb);
   c.Tension=ByteEngine.Core.Physics.RopeWinchDynamics.Tension(distance,c.Length,Vector3.Dot(vb-va,n),14000,1800,(part.Block.Type==36?20000:12000)*Math.Clamp(part.Block.Power,.25f,1.5f));
   var impulse=n*(c.Tension*dt);_simulation.Awakener.AwakenBody(part.Root.Body);truck.ApplyImpulse(impulse,ra);
   if(c.Anchor==null){_simulation.Awakener.AwakenBody(_parts[RecoveryRootId].Root.Body);target.ApplyImpulse(-impulse,rb);}
  }
 }
}
