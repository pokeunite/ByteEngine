using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Physics;

/// <summary>Shared tension-only cable law, also usable by external physics adapters.</summary>
public static class RopeWinchDynamics
{
 public static float Reel(float length,float input,float speed,float delta,float minimum,float maximum)
  =>Math.Clamp(length-Math.Clamp(input,-1,1)*Math.Max(0,speed)*Math.Max(0,delta),Math.Max(.01f,minimum),Math.Max(minimum,maximum));
 public static float Tension(float distance,float length,float separatingSpeed,float stiffness,float damping,float maximum)
 {
  if(!float.IsFinite(distance+length+separatingSpeed+stiffness+damping+maximum)||distance<=length)return 0;
  return Math.Clamp((distance-length)*Math.Max(0,stiffness)+separatingSpeed*Math.Max(0,damping),0,Math.Max(0,maximum));
 }
}
/// <summary>Reelable tension-only rope between rigid bodies or a world anchor. Applies torque at its mounting points.</summary>
public sealed class RopeWinch3D:Component,IPhysicsConstraint3D
{
 public Guid ConnectedObject {get;set;}
 public Vector3 LocalAnchor {get;set;}
 public Vector3 ConnectedAnchor {get;set;}
 public bool Connected {get;set;}=true;
 public float Length {get;set;}=5;
 public float MinimumLength {get;set;}=.7f;
 public float MaximumLength {get;set;}=24;
 public float ReelSpeed {get;set;}=1;
 public float ReelInput {get;set;}
 public float Stiffness {get;set;}=14000;
 public float Damping {get;set;}=1800;
 public float MaximumPull {get;set;}=12000;
 public float BreakForce {get;set;}=float.MaxValue;
 public float HookSpeed {get;set;}=35;
 public bool RenderCable {get;set;}=true;
 public float CableRadius {get;set;}=.025f;
 public float LastForce {get;private set;}
 public bool Broken {get;private set;}
 public bool HookInFlight=>_flight>0;
 float _flight,_flightTime;
 CableRenderer3D? _visual;
 public void Disconnect(){Connected=false;LastForce=0;ReelInput=0;}
 public void Reconnect(){Connected=true;Broken=false;LastForce=0;_flight=_flightTime=0;}
 public void Launch(){Reconnect();if(Endpoints(out var a,out var b,out _,out _)){Length=Vector3.Distance(a,b);_flight=_flightTime=Length/Math.Max(.1f,HookSpeed);}}
 bool Endpoints(out Vector3 pa,out Vector3 pb,out Rigidbody3D? a,out Rigidbody3D? b)
 {
  pa=pb=Vector3.Zero;a=b=null;if(GameObject.Scene is not {} scene)return false;
  a=GameObject.GetComponent<Rigidbody3D>();if(a is not {Enabled:true})return false;
  var target=ConnectedObject==Guid.Empty?null:scene.FindGameObject(ConnectedObject);
  if(ConnectedObject!=Guid.Empty&&(target==null||!target.ActiveInHierarchy||target==GameObject))return false;
  b=target?.GetComponent<Rigidbody3D>();if(b is {Enabled:false})b=null;
  pa=Vector3.Transform(LocalAnchor,Transform.WorldMatrix);pb=target==null?ConnectedAnchor:Vector3.Transform(ConnectedAnchor,target.Transform.WorldMatrix);return true;
 }
 public void Solve(float delta)
 {
  LastForce=0;if(!Connected||Broken||!float.IsFinite(delta)||delta<=0||!Endpoints(out var pa,out var pb,out var a,out var b))return;
  float dt=delta/Math.Clamp(GameObject.Scene!.Physics.SolverIterations,1,32);
  if(_flight>0){_flight=Math.Max(0,_flight-dt);return;}
  Length=RopeWinchDynamics.Reel(Length,ReelInput,ReelSpeed,dt,MinimumLength,MaximumLength);
  var direction=pb-pa;float distance=direction.Length();if(distance<.00001f||!float.IsFinite(distance))return;direction/=distance;
  float separating=Vector3.Dot((b?.VelocityAt(pb)??Vector3.Zero)-a!.VelocityAt(pa),direction);
  LastForce=RopeWinchDynamics.Tension(distance,Length,separating,Stiffness,Damping,MaximumPull);
  if(LastForce>BreakForce){Broken=true;Connected=false;LastForce=0;return;}
  var impulse=direction*(LastForce*dt);a.AddImpulseAtPosition(impulse,pa);b?.AddImpulseAtPosition(-impulse,pb);
 }
 protected override void OnUpdate()
 {
  if(RenderCable&&_visual==null){_visual=GameObject.GetComponent<CableRenderer3D>()??GameObject.AddComponent(new CableRenderer3D());_visual.VisibilityGuard=()=>Enabled&&Connected&&!Broken;}
  if(_visual==null)return;_visual.Enabled=RenderCable&&Connected&&!Broken&&Endpoints(out _,out _,out _,out _);
  if(_visual.Enabled&&Endpoints(out var a,out var b,out _,out _)){_visual.Start=a;_visual.End=HookInFlight?Vector3.Lerp(a,b,1-_flight/Math.Max(.001f,_flightTime)):b;_visual.Length=Length;_visual.Radius=CableRadius;}
 }
 protected override void OnDestroy(){if(_visual!=null){_visual.Enabled=false;_visual.VisibilityGuard=null;}}
 protected override void OnStop(){if(_visual!=null)_visual.Enabled=false;}
}
