using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
namespace Downhill;

/// <summary>Independent heading and momentum, load-based static/kinetic grip,
/// separate foot braking and weight-shift sliding. Native rigid-body integration.</summary>
public sealed class BoardController3D:Component
{
 public Key LeftKey {get;set;}=Key.A;
 public Key RightKey {get;set;}=Key.D;
 public Key BrakeKey {get;set;}=Key.S;
 public Key SlideKey {get;set;}=Key.Space;
 public Key HandDragKey {get;set;}=Key.C;
 public bool HandDragInput {get;set;}
 public Key TuckKey {get;set;}=Key.LeftShift;
 public Key PushKey {get;set;}=Key.W;
 public bool ReadPlayerInput {get;set;}=true;
 public float Wheelbase {get;set;}=.76f;
 public float TrackWidth {get;set;}=.31f;
 // Legacy tuning fields remain readable in existing scenes.
 public float LowSpeedSteeringDegrees {get;set;}=15;
 public float HighSpeedSteeringDegrees {get;set;}=2.4f;
 public float LowSpeedTurnRate {get;set;}=72;
 public float HighSpeedTurnRate {get;set;}=40;
 public float SlideTurnMultiplier {get;set;}=1.5f;
 public float MaximumSlideAngle {get;set;}=55;
 public float SlideHeadingResponse {get;set;}=5;
 public float SlideRecoveryResponse {get;set;}=5;
 public float BrakeTurnMultiplier {get;set;}=.55f;
 public float SteeringResponse {get;set;}=7;
 public float StaticGripCoefficient {get;set;}=1.1f;
 public float KineticGripCoefficient {get;set;}=.7f;
 public float SlideWeightTransfer {get;set;}=.5f;
 public float GripRecoverySideSpeed {get;set;}=.45f;
 public float GripAcceleration {get;set;}=10.5f;
 public float SlideGripAcceleration {get;set;}=2.9f;
 public float GripResponse {get;set;}=11;
 public float RollingDeceleration {get;set;}=.12f;
 public float AirDrag {get;set;}=.0045f;
 public float TuckedAirDrag {get;set;}=.0019f;
 public float BrakeDeceleration {get;set;}=5;
 public float SlideDeceleration {get;set;}=.6f;
 public float PushAcceleration {get;set;}=2.7f;
 public float PushCycleSeconds {get;set;}=1.15f;
 public float PushSpeedLimit {get;set;}=6;
 public float MaximumLeanDegrees {get;set;}=20;
 public float GroundProbeHeight {get;set;}=.65f;
 public float GroundProbeLength {get;set;}=1.3f;
 public float RideHeight {get;set;}=.12f;
 public float GroundSnapDistance {get;set;}=.18f;
 public bool ControlsEnabled {get;set;}=true;
 public float SteerInput {get;set;}
 public bool BrakeInput {get;set;}
 public bool SlideInput {get;set;}
 public bool TuckInput {get;set;}
 public bool PushInput {get;set;}
 public float Speed {get;private set;}
 public float ForwardSpeed {get;private set;}
 public float SideSpeed {get;private set;}
 public float SlipAngle {get;private set;}
 public float Lean {get;private set;}
 public float SmoothedSteer {get;private set;}
 public float PushPhase {get;private set;}
 public bool IsSliding {get;private set;}
 public bool Grounded {get;private set;}
 public int WheelsOnGround {get;private set;}
 public Vector3 GroundNormal {get;private set;}=Vector3.UnitY;
 public bool Crashed {get;private set;}
 public Rigidbody3D? Body {get;private set;}
 public override int UpdateOrder=>-100;
 float pushTime;bool wasPushing;Vector3 previousVelocity;
 protected override void OnStart()=>Body=GameObject.GetComponent<Rigidbody3D>();
 protected override void OnUpdate(){if(!ReadPlayerInput)return;SteerInput=Math.Clamp((Input.IsKeyDown(RightKey)?1:0)-(Input.IsKeyDown(LeftKey)?1:0)+Input.Snapshot.Gamepad.LeftStick.X,-1,1);BrakeInput=Input.IsKeyDown(BrakeKey);HandDragInput=Input.IsKeyDown(HandDragKey);SlideInput=Input.IsKeyDown(SlideKey)||HandDragInput;TuckInput=Input.IsKeyDown(TuckKey);PushInput=Input.IsKeyDown(PushKey);}
 public float TurnRateAt(float speed)=>float.Lerp(LowSpeedTurnRate,HighSpeedTurnRate,Math.Clamp(speed/25,0,1))*Math.Clamp(speed/2.5f,0,1);
 protected override void OnFixedUpdate(){
  if(Body==null)return;float dt=(float)Time.FixedDeltaTime;Speed=Body.Velocity.Length();if(!ControlsEnabled||Crashed)return;
  SmoothedSteer+=(Math.Clamp(SteerInput,-1,1)-SmoothedSteer)*(1-MathF.Exp(-SteeringResponse*dt));
  var scene=GameObject.Scene!;Vector3 normal=Vector3.Zero;float height=0;WheelsOnGround=0;
  for(int a=0;a<2;a++)for(int w=0;w<2;w++){float axle=a-.5f,wheel=w-.5f;
   Vector3 origin=Transform.WorldPosition+Transform.Forward*axle*Wheelbase+Transform.Right*wheel*TrackWidth+Vector3.UnitY*GroundProbeHeight;
   if(GameplayQuery3D.Raycast(scene,origin,-Vector3.UnitY,out var hit,GroundProbeLength,GameObject,includeTriggers:false)&&hit.Normal.Y>.3f){normal+=hit.Normal;height+=hit.Point.Y;WheelsOnGround++;}
  }
  Grounded=WheelsOnGround>0;if(Grounded)GroundNormal=Vector3.Normalize(normal);
  Vector3 up=GroundNormal,forward=Vector3.Normalize(Transform.Forward-up*Vector3.Dot(Transform.Forward,up));
  if(Grounded){
   float supported=height/WheelsOnGround+RideHeight/Math.Max(.3f,up.Y),gap=Transform.WorldPosition.Y-supported;
   if(gap<.035f&&gap>-GroundSnapDistance){if(gap<0)Transform.WorldPosition+=Vector3.UnitY*(-gap);float into=Vector3.Dot(Body.Velocity,up);if(into<0)Body.Velocity-=up*into;Body.AddForce(up*Math.Max(0,-Vector3.Dot(scene.Physics.Gravity,up))*Body.Mass);}
   Vector3 v=Body.Velocity-up*Vector3.Dot(Body.Velocity,up);float speed=v.Length();
   Vector3 oldRight=Vector3.Normalize(Vector3.Cross(forward,up));
   float travelAngle=MathF.Atan2(Vector3.Dot(v,oldRight),Vector3.Dot(v,forward));
   bool slideHeld=SlideInput&&!BrakeInput&&speed>2.5f;
   float yawRate;
   if(slideHeld){float desired=SmoothedSteer*Math.Clamp(MaximumSlideAngle,10,70)*MathF.PI/180;yawRate=Math.Clamp((travelAngle+desired)*SlideHeadingResponse*180/MathF.PI,-150,150);}
   else if(IsSliding){yawRate=Math.Clamp(travelAngle*SlideRecoveryResponse*180/MathF.PI,-120,120);}
   else{float loadAcceleration=Math.Max(1,-Vector3.Dot(scene.Physics.Gravity,up));float gripTurn=loadAcceleration*StaticGripCoefficient*.9f/Math.Max(1,speed)*180/MathF.PI;float turn=Math.Min(TurnRateAt(speed),gripTurn);yawRate=SmoothedSteer*turn*(BrakeInput?BrakeTurnMultiplier:1);if(Vector3.Dot(v,forward)<-.5f)yawRate=-yawRate;}
   forward=Vector3.Transform(forward,Quaternion.CreateFromAxisAngle(up,-yawRate*MathF.PI/180*dt));Vector3 right=Vector3.Normalize(Vector3.Cross(forward,up));
   Transform.WorldRotation=Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(new(right.X,right.Y,right.Z,0,up.X,up.Y,up.Z,0,-forward.X,-forward.Y,-forward.Z,0,0,0,0,1)));
   ForwardSpeed=Vector3.Dot(v,forward);SideSpeed=Vector3.Dot(v,right);SlipAngle=MathF.Atan2(SideSpeed,Math.Max(.2f,Math.Abs(ForwardSpeed)))*180/MathF.PI;
   float load=Math.Max(1,-Vector3.Dot(scene.Physics.Gravity,up))*Math.Clamp(WheelsOnGround/4f,.5f,1);float demand=SideSpeed/dt+Vector3.Dot(scene.Physics.Gravity,right);
   bool requestingSlide=slideHeld&&Math.Abs(SmoothedSteer)>.05f;float staticLimit=load*StaticGripCoefficient*(requestingSlide?1-SlideWeightTransfer:1);
   if(!IsSliding&&(Math.Abs(demand)>staticLimit||requestingSlide))IsSliding=true;
   if(IsSliding&&!requestingSlide&&Math.Abs(SideSpeed)<GripRecoverySideSpeed&&Math.Abs(demand)<staticLimit)IsSliding=false;
   float limit=load*KineticGripCoefficient*(requestingSlide?Math.Max(.8f,1-SlideWeightTransfer):1);float lateral=IsSliding?Math.Clamp(demand,-limit,limit):demand;Body.AddForce(-right*lateral*Body.Mass);
   if(speed>.02f){float drag=RollingDeceleration+(TuckInput?TuckedAirDrag:AirDrag)*speed*speed+(BrakeInput?BrakeDeceleration:0)+(IsSliding?SlideDeceleration*Math.Clamp(Math.Abs(SlipAngle)/40,0,1):0);Body.AddForce(-v/speed*Math.Min(drag,speed/dt)*Body.Mass);}
   if(BrakeInput&&speed<.25f){Body.AddForce(-(scene.Physics.Gravity-up*Vector3.Dot(scene.Physics.Gravity,up))*Body.Mass);Body.Velocity-=v;}
   bool pushing=PushInput&&!TuckInput&&!BrakeInput&&!IsSliding&&speed<PushSpeedLimit;if(pushing&&!wasPushing)pushTime=0;
   if(pushing){pushTime+=dt;PushPhase=pushTime%Math.Max(.3f,PushCycleSeconds)/Math.Max(.3f,PushCycleSeconds);float stroke=Math.Clamp((PushPhase-.12f)/.42f,0,1);float pulse=PushPhase>.12f&&PushPhase<.54f?MathF.Sin(stroke*MathF.PI):0;Body.AddForce(forward*PushAcceleration*Body.Mass*pulse);}wasPushing=pushing;
   Lean=-SmoothedSteer*MaximumLeanDegrees*Math.Clamp(speed/7,0,1);
  }else{IsSliding=false;SlipAngle*=MathF.Exp(-dt*2);Lean*=MathF.Exp(-dt*3);}
  if(previousVelocity.Length()>4&&previousVelocity.Length()-Speed>Math.Max(5,previousVelocity.Length()*.48f))Crashed=true;previousVelocity=Body.Velocity;
 }
 public void ResetAt(Vector3 position,Quaternion rotation){Transform.WorldPosition=position;Transform.WorldRotation=rotation;if(Body!=null){Body.Velocity=Vector3.Zero;Body.AngularVelocity=Vector3.Zero;Body.UseGravity=true;Body.WakeUp();}Crashed=false;previousVelocity=Vector3.Zero;SmoothedSteer=0;IsSliding=false;Speed=0;pushTime=0;wasPushing=false;}
}


