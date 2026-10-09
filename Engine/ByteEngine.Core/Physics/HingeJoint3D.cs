using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Physics;
/// <summary>Local-anchor hinge with axis alignment, angular limits and a bounded motor. Bodies remain separate.</summary>
public sealed class HingeJoint3D : Component,IPhysicsConstraint3D
{
 public Guid ConnectedObject {get;set;}
 public Vector3 LocalAnchor {get;set;}
 public Vector3 ConnectedAnchor {get;set;}
 public Vector3 LocalAxis {get;set;}=Vector3.UnitY;
 public Vector3 ConnectedAxis {get;set;}=Vector3.UnitY;
 public bool LimitsEnabled {get;set;}
 public float MinimumAngle {get;set;}=-90;
 public float MaximumAngle {get;set;}=90;
 public bool MotorEnabled {get;set;}
 public float MotorSpeed {get;set;}
 public float MotorMaximumTorque {get;set;}=20;
 public float CurrentAngle {get;private set;}
 public float BreakForce {get;set;}=float.MaxValue;
 public bool Broken {get;private set;}
 private bool _initialized;
 private Vector3 _referenceA,_referenceB;
 public void Reconnect(){Broken=false;_initialized=false;}
 private static Vector3 Axis(Vector3 value)=>value.LengthSquared()<.0001f?Vector3.UnitY:Vector3.Normalize(value);
 public void Solve(float delta)
 {
  if(Broken||delta<=0||GameObject.Scene is not {} scene||GameObject.GetComponent<Rigidbody3D>() is not {Enabled:true} a)return;
  var target=ConnectedObject==Guid.Empty?null:scene.FindGameObject(ConnectedObject);if(ConnectedObject!=Guid.Empty&&(target==null||!target.ActiveInHierarchy||ReferenceEquals(target,GameObject)))return;
  var b=target?.GetComponent<Rigidbody3D>();if(b is {Enabled:false})b=null;
  Vector3 pa=Vector3.Transform(LocalAnchor,Transform.WorldMatrix),pb=target==null?ConnectedAnchor:Vector3.Transform(ConnectedAnchor,target.Transform.WorldMatrix);
  Vector3 error=pb-pa,relative=(b?.VelocityAt(pb)??Vector3.Zero)-a.VelocityAt(pa);
  foreach(var row in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})
  {
   float denominator=a.InverseMass+(b?.InverseMass??0)+a.AngularImpulseDenominator(pa,row)+(b?.AngularImpulseDenominator(pb,row)??0);if(denominator<.000001f)continue;
   float impulse=(Vector3.Dot(relative,row)+.15f*Vector3.Dot(error,row)/delta)/denominator;if(Math.Abs(impulse)/delta>BreakForce){Broken=true;return;}a.AddImpulseAtPosition(row*impulse,pa);b?.AddImpulseAtPosition(-row*impulse,pb);
  }
  float total=a.InverseMass+(b?.InverseMass??0);if(total>0){a.ApplyPositionCorrection(error*(.15f*a.InverseMass/total));b?.ApplyPositionCorrection(-error*(.15f*b.InverseMass/total));}
  Vector3 axisA=Axis(Vector3.Transform(Axis(LocalAxis),Transform.WorldRotation)),axisB=target==null?Axis(ConnectedAxis):Axis(Vector3.Transform(Axis(ConnectedAxis),target.Transform.WorldRotation));
  if(!_initialized)
  {
   Vector3 reference=Axis(Vector3.Cross(axisA,Math.Abs(axisA.X)<.8f?Vector3.UnitX:Vector3.UnitZ));_referenceA=Vector3.Transform(reference,Quaternion.Inverse(Transform.WorldRotation));_referenceB=target==null?reference:Vector3.Transform(reference,Quaternion.Inverse(target.Transform.WorldRotation));_initialized=true;
  }
  Vector3 alignment=Vector3.Cross(axisA,axisB);
  Vector3 angular=a.AngularVelocity-(b?.AngularVelocity??Vector3.Zero);
  var tangent=Axis(Vector3.Cross(axisA,Math.Abs(axisA.X)<.8f?Vector3.UnitX:Vector3.UnitZ));var bitangent=Vector3.Cross(axisA,tangent);
  AngularRow(tangent,Vector3.Dot(alignment,tangent)*.15f/delta-Vector3.Dot(angular,tangent),float.MaxValue);
  AngularRow(bitangent,Vector3.Dot(alignment,bitangent)*.15f/delta-Vector3.Dot(angular,bitangent),float.MaxValue);
  Vector3 ra=Vector3.Transform(_referenceA,Transform.WorldRotation),rb=target==null?_referenceB:Vector3.Transform(_referenceB,target.Transform.WorldRotation);ra=Axis(ra-axisA*Vector3.Dot(ra,axisA));rb=Axis(rb-axisA*Vector3.Dot(rb,axisA));float angle=MathF.Atan2(Vector3.Dot(axisA,Vector3.Cross(rb,ra)),Vector3.Dot(rb,ra));CurrentAngle=angle*180/MathF.PI;
  if(LimitsEnabled){float low=Math.Min(MinimumAngle,MaximumAngle)*MathF.PI/180,high=Math.Max(MinimumAngle,MaximumAngle)*MathF.PI/180;float clamped=Math.Clamp(angle,low,high);if(clamped!=angle)AngularRow(axisA,(clamped-angle)*.15f/delta-Vector3.Dot(angular,axisA),float.MaxValue);}
  if(MotorEnabled)AngularRow(axisA,MotorSpeed*MathF.PI/180-Vector3.Dot(angular,axisA),Math.Max(0,MotorMaximumTorque)*delta/Math.Clamp(GameObject.Scene?.Physics.SolverIterations??8,1,32));
  void AngularRow(Vector3 axis,float change,float maximum)
  {
   float inverse=(a.SimulateRotation?Vector3.Dot(axis,a.ApplyInverseInertia(axis)):0)+(b is {SimulateRotation:true}?Vector3.Dot(axis,b.ApplyInverseInertia(axis)):0);if(inverse<.000001f)return;float impulse=Math.Clamp(change/inverse,-maximum,maximum);a.AddTorqueImpulse(axis*impulse);b?.AddTorqueImpulse(-axis*impulse);
  }
 }
}
