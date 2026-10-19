using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
namespace Downhill;

public sealed class BoardRider3D:Component
{
 public string BoardObject {get;set;}="Board";
 public AssetReference PoseProfile {get;set;}=AssetReference.Empty;
 public string RidingBlend {get;set;}="Ride";
 public string TuckBlend {get;set;}="Tuck";
 public string BrakeClip {get;set;}="Foot Brake Board";
 public string PushClip {get;set;}="Push Board";
 public string HeelSlideClip {get;set;}="Heelside Slide Board";
 public string ToeSlideClip {get;set;}="Toeside Slide Board";
 public string HeelHandDragClip {get;set;}="Hands Down Heel Board";
 public string ToeHandDragClip {get;set;}="Hands Down Toe Board";
 public string LeftFootChain {get;set;}="LeftFoot";
 public string RightFootChain {get;set;}="RightFoot";
 public Vector3 LeftFootAnchor {get;set;}=new(-.025f,.14f,-.24f);
 public Vector3 RightFootAnchor {get;set;}=new(.025f,.14f,.24f);
 public Vector3 BrakeFootAnchor {get;set;}=new(.32f,0,.2f);
 public Vector3 KneePole {get;set;}=new(1,.4f,0);
 public Vector4 LeftFootRotation {get;set;}=new(0,0,0,1);
 public Vector4 RightFootRotation {get;set;}=new(0,0,0,1);
 public Vector4 LeftForwardFootRotation {get;set;}=new(0,0,0,1);
 public Vector4 RightBrakeFootRotation {get;set;}=new(0,0,0,1);
 public Vector3 TuckLeftHand {get;set;}=new(-.1f,.64f,.35f);
 public Vector3 TuckRightHand {get;set;}=new(.1f,.64f,.35f);
 public float FootAnkleHeight {get;set;}=.12f;
 public float PushFootOutsideX {get;set;}=.38f;
 public float PushFootResponse {get;set;}=24;
 public float DeckHalfWidth {get;set;}=.16f;
 public float DeckHalfLength {get;set;}=.60f;
 public float FootLockWeight {get;set;}=1;
 public float FootReleaseBlendSeconds {get;set;}=.14f;
 public float BrakeBodyDrop {get;set;}=.05f;
 public float PushBodyDrop {get;set;}=.19f;
 public string ActivePose {get;private set;}="";
 public float FootError {get;private set;}
 public float PlantedFootError {get;private set;}
 public float FootRotationError {get;private set;}
 public float HandContactError {get;private set;}
 string? contactHand;Vector3 handContactTarget;
 public override int UpdateOrder=>-30;
 BoardController3D? board;SkeletalMeshRenderer? renderer;AnimationProfile? profile;
 Vector3 bindPosition;float rearLock=1,brakeBlend,forwardFootBlend,bodyDrop,tuckBlend,handBlend;
 Vector3 smoothRightLocal;bool smoothRightReady;
 Quaternion leftGoal=Quaternion.Identity,rightGoal=Quaternion.Identity;
 Vector3 rightTarget;float rightWeight;
 protected override void OnStart(){board=GameObject.Scene!.FindGameObject(BoardObject)?.GetComponent<BoardController3D>();renderer=GameObject.GetComponent<SkeletalMeshRenderer>();bindPosition=Transform.LocalPosition;if(renderer!=null&&!PoseProfile.IsEmpty&&AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null){profile=assets.LoadAnimationProfile(PoseProfile);renderer.ConfigurePoseProfile(profile);renderer.EvaluatePoseAfterPhysics=true;}}
 static Quaternion Rotation(Vector4 v){var q=new Quaternion(v.X,v.Y,v.Z,v.W);return q.LengthSquared()>1e-8f?Quaternion.Normalize(q):Quaternion.Identity;}
 protected override void OnUpdate(){
  if(board==null||renderer==null)return;float dt=(float)Time.DeltaTime;
  bool braking=board.BrakeInput,pushing=!braking&&!board.TuckInput&&board.PushInput&&board.Speed<board.PushSpeedLimit&&!board.IsSliding;
  bool sliding=board.IsSliding&&board.Speed>2;
  bool tucking=board.TuckInput&&!braking&&!pushing&&!sliding;
  bool toes=Math.Abs(board.SmoothedSteer)>.05f?board.SmoothedSteer>0:board.SlipAngle<0;
  bool handsDown=sliding&&board.HandDragInput;
  string? clip=braking?BrakeClip:pushing?PushClip:sliding?(handsDown?(toes?ToeHandDragClip:HeelHandDragClip):(toes?ToeSlideClip:HeelSlideClip)):null;
  if(clip!=null){renderer.SetBlendSpace(null,0,0,.25f);if(renderer.CurrentAnimation!=clip)renderer.Play(clip,true,.25f);renderer.Speed=pushing?renderer.Duration/Math.Max(.3f,board.PushCycleSeconds):1;if(pushing)renderer.AlignPlaybackPhase(board.PushPhase);ActivePose=clip;}
  else{renderer.Speed=1;string name=board.TuckInput?TuckBlend:RidingBlend;renderer.SetBlendSpace(profile?.BlendSpaces.FirstOrDefault(x=>x.Name==name),Math.Clamp(board.SmoothedSteer,-1,1),0,.25f);ActivePose=name;}
  float alpha=1-MathF.Exp(-dt/Math.Max(.02f,FootReleaseBlendSeconds));
  brakeBlend+=((braking?1:0)-brakeBlend)*alpha;forwardFootBlend+=((braking||pushing||tucking?1:0)-forwardFootBlend)*alpha;tuckBlend+=((tucking?1:0)-tuckBlend)*alpha;
  bodyDrop+=((braking?BrakeBodyDrop:pushing?PushBodyDrop:handsDown?.12f:0)-bodyDrop)*alpha;Transform.LocalPosition=bindPosition-Vector3.UnitY*bodyDrop;
  rearLock+=((pushing?0:FootLockWeight)-rearLock)*alpha;
  handBlend+=((handsDown?1:0)-handBlend)*alpha;
 }
 void UpdateFootTargets(){
  if(board==null||renderer==null)return;
  bool braking=board.BrakeInput,pushing=!braking&&!board.TuckInput&&board.PushInput&&board.Speed<board.PushSpeedLimit&&!board.IsSliding;
  Vector3 World(Vector3 p)=>Vector3.Transform(p,board.Transform.WorldMatrix);
  Quaternion WorldRotation(Quaternion q)=>Quaternion.Normalize(board.Transform.WorldRotation*q);
  float Smooth(float a,float b,float t){float x=Math.Clamp((t-a)/(b-a),0,1);return x*x*(3-2*x);}
  leftGoal=WorldRotation(Quaternion.Slerp(Rotation(LeftFootRotation),Rotation(LeftForwardFootRotation),forwardFootBlend));
  rightGoal=WorldRotation(Quaternion.Slerp(Rotation(RightFootRotation),Rotation(RightBrakeFootRotation),Math.Max(brakeBlend,tuckBlend)));
  renderer.SetIkTarget("LeftHand",World(TuckLeftHand),World(new(-.45f,.8f,.25f)),tuckBlend);
  renderer.SetIkTarget("RightHand",World(TuckRightHand),World(new(.45f,.8f,.25f)),tuckBlend);
  var forwardPole=Vector3.Lerp(KneePole,new(0,.4f,-.15f),forwardFootBlend);
  renderer.SetIkPoseTarget(LeftFootChain,World(LeftFootAnchor),World(LeftFootAnchor+forwardPole),leftGoal,FootLockWeight,forwardFootBlend<.01f);
  contactHand=null;
  renderer.SetBoneModelRotationOffset("mixamorig:Spine",Quaternion.Identity,0);
  if(handBlend>.001f&&board.HandDragInput){
   float phase=renderer.NormalizedPlaybackPhase;
   if(renderer.TrySampleAnimationBoneWorldMatrix("mixamorig:LeftHand",phase,out var lh)&&renderer.TrySampleAnimationBoneWorldMatrix("mixamorig:RightHand",phase,out var rh)){
    string side=lh.Translation.Y<rh.Translation.Y?"Left":"Right";var hand=side=="Left"?lh:rh;
    var probe=hand.Translation;
    if(Matrix4x4.Invert(board.Transform.WorldMatrix,out var invHand)){var localHand=Vector3.Transform(probe,invHand);localHand.X=MathF.CopySign(Math.Max(DeckHalfWidth+.18f,Math.Abs(localHand.X)),Math.Abs(localHand.X)>.02f?localHand.X:side=="Left"?-.34f:.34f);probe=World(localHand);}
    if(GameplayQuery3D.Raycast(GameObject.Scene!,probe+Vector3.UnitY*.5f,-Vector3.UnitY,out var contact,1.5f,board.GameObject,includeTriggers:false)){
     var target=contact.Point+contact.Normal*.035f;
     if(renderer.TrySampleAnimationBoneWorldMatrix("mixamorig:Spine",phase,out var spine)&&renderer.TrySampleAnimationBoneWorldMatrix("mixamorig:"+side+"Arm",phase,out var shoulder)&&renderer.TrySampleAnimationBoneWorldMatrix("mixamorig:"+side+"ForeArm",phase,out var arm)){
      var lean=target-spine.Translation;lean.Y=0;
      if(lean.LengthSquared()>.001f&&Matrix4x4.Invert(Transform.WorldMatrix,out var inverseRider)){
       var worldAxis=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,Vector3.Normalize(lean)));
       float reach=(Vector3.Distance(shoulder.Translation,arm.Translation)+Vector3.Distance(arm.Translation,hand.Translation))*.98f;
       float Distance(float angle)=>Vector3.Distance(spine.Translation+Vector3.Transform(shoulder.Translation-spine.Translation,Quaternion.CreateFromAxisAngle(worldAxis,angle)),target);
       float low=0,high=65*MathF.PI/180;
       if(Distance(0)<=reach)high=0;else if(Distance(high)<=reach)for(int i=0;i<12;i++){float mid=(low+high)*.5f;if(Distance(mid)>reach)low=mid;else high=mid;}
       var axis=Vector3.TransformNormal(worldAxis,inverseRider);
       renderer.SetBoneModelRotationOffset("mixamorig:Spine",Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis),high),handBlend);
      }
     }
     if(renderer.TrySampleAnimationBoneWorldMatrix("mixamorig:"+side+"ForeArm",phase,out var elbow)){renderer.SetIkTarget(side+"Hand",target,elbow.Translation+Vector3.UnitY*.15f,handBlend);contactHand=side+"Hand";handContactTarget=target;}
    }
   }
  }
  var brakeTarget=World(BrakeFootAnchor);
  if(braking&&GameplayQuery3D.Raycast(GameObject.Scene!,brakeTarget+Vector3.UnitY*.5f,-Vector3.UnitY,out var hit,1.5f,board.GameObject,includeTriggers:false))brakeTarget.Y=hit.Point.Y+FootAnkleHeight;
  rightTarget=Vector3.Lerp(World(RightFootAnchor),brakeTarget,brakeBlend);rightWeight=rearLock;
  if(pushing){
   // Sample the free authored stroke, then clear the deck before lowering the sole.
   renderer.AlignPlaybackPhase(board.PushPhase);
   if(renderer.TrySampleAnimationBoneWorldMatrix("mixamorig:RightFoot",board.PushPhase,out var foot)&&Matrix4x4.Invert(board.Transform.WorldMatrix,out var inverse)){
    var local=Vector3.Transform(foot.Translation,inverse);
    float phase=board.PushPhase;
    float outside=Smooth(.02f,.16f,phase)*(1-Smooth(.68f,.94f,phase));
    local.X=float.Lerp(local.X,Math.Max(PushFootOutsideX,local.X),outside);
    float contact=Smooth(.16f,.23f,phase)*(1-Smooth(.48f,.60f,phase));
    var goal=World(local);
    if(board.Grounded&&contact>0&&GameplayQuery3D.Raycast(GameObject.Scene!,goal+Vector3.UnitY*.5f,-Vector3.UnitY,out var ground,1.5f,board.GameObject,includeTriggers:false)){
     goal.Y=float.Lerp(goal.Y,ground.Point.Y+FootAnkleHeight,contact);
     local=Vector3.Transform(goal,inverse);
    }
    float lift=Math.Max(1-Smooth(.10f,.19f,phase),Smooth(.54f,.68f,phase));
    local.Y=float.Lerp(local.Y,Math.Max(local.Y,RightFootAnchor.Y+.06f),lift);
    rightTarget=World(local);rightWeight=0;
    rightGoal=WorldRotation(Quaternion.Slerp(Rotation(RightFootRotation),Rotation(RightBrakeFootRotation),outside));
   }
  }
  if(Matrix4x4.Invert(board.Transform.WorldMatrix,out var inverseBoard)){
   var targetLocal=Vector3.Transform(rightTarget,inverseBoard);
   if(!smoothRightReady){smoothRightLocal=RightFootAnchor;smoothRightReady=true;}
   var next=Vector3.Lerp(smoothRightLocal,targetLocal,1-MathF.Exp(-PushFootResponse*(float)Time.DeltaTime));var travel=next-smoothRightLocal;float maxStep=2.5f*(float)Time.DeltaTime;smoothRightLocal+=travel.LengthSquared()>maxStep*maxStep?Vector3.Normalize(travel)*maxStep:travel;
   if(pushing){float clearance=(1-Smooth(DeckHalfWidth+.13f,DeckHalfWidth+.19f,Math.Abs(smoothRightLocal.X)))*(1-Smooth(DeckHalfLength+.16f,DeckHalfLength+.23f,Math.Abs(smoothRightLocal.Z)));smoothRightLocal.Y=float.Lerp(smoothRightLocal.Y,Math.Max(smoothRightLocal.Y,RightFootAnchor.Y+.018f),clearance);}
   rightTarget=World(smoothRightLocal);
  }
  var rightPole=pushing?new Vector3(0,.4f,-.15f):Vector3.Lerp(KneePole,new(0,.4f,-.15f),tuckBlend);
  renderer.SetIkPoseTarget(RightFootChain,rightTarget,rightTarget+Vector3.TransformNormal(rightPole,board.Transform.WorldMatrix),rightGoal,1,!pushing);
  rightWeight=1;
 }
 protected override void OnLateUpdate(){
  UpdateFootTargets();
  if(board==null||renderer==null)return;float total=0,rot=0;int count=0;
  if(renderer.TryGetBoneWorldMatrix("mixamorig:LeftFoot",out var left)){PlantedFootError=Vector3.Distance(left.Translation,Vector3.Transform(LeftFootAnchor,board.Transform.WorldMatrix));total+=PlantedFootError;Matrix4x4.Decompose(left,out _,out var q,out _);rot=MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(Quaternion.Normalize(q),leftGoal)),0,1))*360/MathF.PI;count++;}
  if(rightWeight>.98f&&renderer.TryGetBoneWorldMatrix("mixamorig:RightFoot",out var right)){total+=Vector3.Distance(right.Translation,rightTarget);Matrix4x4.Decompose(right,out _,out var q,out _);rot=Math.Max(rot,MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(Quaternion.Normalize(q),rightGoal)),0,1))*360/MathF.PI);count++;}
  FootError=count>0?total/count:0;FootRotationError=rot;
  HandContactError=contactHand!=null&&renderer.TryGetBoneWorldMatrix("mixamorig:"+contactHand,out var hand)?Vector3.Distance(hand.Translation,handContactTarget):0;
 }
}







