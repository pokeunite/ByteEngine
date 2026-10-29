using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
/// <summary>Stock-backed conveyor delivery and bounded batched pile. No per-round physics or scene objects.</summary>
public sealed class MiningAmmoBucket3D:ByteEngine.Core.Scene.Component
{
 [SceneObjectReference] public Guid WorkshopObjectId{get;set;}
 [Category("Model anchors")] public string BeltStartMarker{get;set;}="BeltStart";
 public string BeltEndMarker{get;set;}="BeltEnd";
 public string BucketMarker{get;set;}="BucketFloor";
 public Vector3 BucketOffset{get;set;}=new(.5f,.5f,-1.5f);
 public Vector3 EjectionOffset{get;set;}=new(.5f,.9f,-.8f);
 [Category("Collection")] public float PickupRadius{get;set;}=1.65f;
 public bool AutoPickup{get;set;}=true;
 [Category("Collection")] public float PickupFlightSeconds{get;set;}=.28f;
 [Category("Collection")] public int MaximumPickupTrails{get;set;}=12;
 [Category("Visible ammunition")] public int MaximumVisibleRounds{get;set;}=72;
 public int RoundsPerVisual{get;set;}=2;
 public int MaximumTravellingRounds{get;set;}=12;
 public float ConveyorSpeed{get;set;}=1.58f;
 public float DropSeconds{get;set;}=.4f;
 public Vector2 PileFootprint{get;set;}=new(1.08f,.70f);
 public float MaximumPileHeight{get;set;}=.30f;
 public float RoundRadius{get;set;}=.035f;
 public float RoundLength{get;set;}=.20f;
 public Vector4 CaseColor{get;set;}=new(.68f,.43f,.10f,1);
 public Vector4 TipColor{get;set;}=new(.58f,.25f,.10f,1);
 [Browsable(false)] public int LastCollected{get;private set;}
 [Browsable(false)] public long CollectionSequence{get;private set;}
 [Browsable(false)] public int VisiblePileRounds{get;private set;}
 [Browsable(false)] public int TravellingRounds=>flights.Count;
 [Browsable(false)] public bool ModelAnchorsResolved{get;private set;}
 [Browsable(false)] public long LandedRoundSequence{get;private set;}
 [Browsable(false)] public Vector3 LastLandingPosition{get;private set;}
 [Browsable(false)] public Vector3 ConveyorSoundPosition=>(start+end)*.5f;
 const int Limit=96;
 readonly record struct Flight(float Age,int Seed);
 readonly List<Flight> flights=[];int deliverySequence;
 readonly record struct PickupFlight(Vector3 Start,Guid Player,float Age,int Seed);
 readonly List<PickupFlight> pickups=[];
 [Browsable(false)] public int CollectingRounds=>pickups.Count;
 readonly Matrix4x4[] poses=new Matrix4x4[Limit];
 float previous,credit;bool initialized;int poseCount;
 Vector3 start,end,bucket,forward=Vector3.UnitZ,side=Vector3.UnitX;
 Mesh? cases,tips;float[]? caseVertices,tipVertices;
 readonly Material caseMaterial=new(){Metallic=.65f,Roughness=.38f};
 readonly Material tipMaterial=new(){Metallic=.5f,Roughness=.4f};
 protected override void OnUpdate()
 {
  if(GameObject.Scene?.FindGameObject(WorkshopObjectId)?.GetComponent<MiningTrade3D>() is not {} trade)return;
  ResolveAnchors();float dt=Math.Clamp((float)Time.DeltaTime,0,1);
  for(int i=pickups.Count-1;i>=0;i--){pickups[i]=pickups[i] with{Age=pickups[i].Age+dt};if(pickups[i].Age>=Math.Max(.05f,PickupFlightSeconds)||GameObject.Scene.FindGameObject(pickups[i].Player)==null)pickups.RemoveAt(i);}
  float travel=Vector3.Distance(start,end)/Math.Max(.1f,ConveyorSpeed),drop=Math.Max(.1f,DropSeconds);
  if(initialized&&trade.StoredAmmo<previous-.01f){flights.Clear();credit=0;}
  for(int i=flights.Count-1;i>=0;i--){flights[i]=flights[i] with {Age=flights[i].Age+dt};if(flights[i].Age>=travel+drop){LastLandingPosition=PilePosition(VisiblePileRounds+i);LandedRoundSequence++;flights.RemoveAt(i);}}
  if(initialized)
  {
   if(trade.StoredAmmo<previous-.01f){flights.Clear();credit=0;}
   else credit+=Math.Max(0,trade.StoredAmmo-previous);
   float unit=VisualUnits(trade.Capacity);int slots=Math.Clamp(MaximumTravellingRounds,0,24);
   while(credit>=unit){credit-=unit;if(flights.Count<slots)flights.Add(new(0,deliverySequence++));}
  }
  previous=trade.StoredAmmo;initialized=true;
  int total=Math.Clamp((int)MathF.Ceiling(trade.StoredAmmo/VisualUnits(trade.Capacity)),0,Math.Clamp(MaximumVisibleRounds,0,72));
  while(flights.Count>total)flights.RemoveAt(flights.Count-1);
  VisiblePileRounds=total-flights.Count;poseCount=0;
  for(int i=0;i<VisiblePileRounds;i++)poses[poseCount++]=RoundPose(PilePosition(i),i*2.39996f,.10f*MathF.Sin(i*7.3f));
  for(int i=0;i<flights.Count;i++)
  {
   float age=flights[i].Age;int seed=flights[i].Seed;Vector3 target=PilePosition(VisiblePileRounds+i),p;
   if(age<travel)p=Vector3.Lerp(start,end,Math.Clamp(age/travel,0,1))+side*((seed%3)-1)*.14f;
   else{float t=Math.Clamp((age-travel)/drop,0,1);p=Vector3.Lerp(end,target,t)+Vector3.UnitY*MathF.Sin(t*MathF.PI)*.12f;}
   poses[poseCount++]=RoundPose(p,age<travel?MathF.PI/2:seed*2.39996f,age<travel?0:(age-travel)*5);
  }
  for(int i=0;i<pickups.Count&&poseCount<Limit;i++)
  {
   var pickup=pickups[i];var target=GameObject.Scene.FindGameObject(pickup.Player)!.Transform.WorldPosition+Vector3.UnitY*.95f;
   float t=Math.Clamp(pickup.Age/Math.Max(.05f,PickupFlightSeconds),0,1);float ease=t*t;
   var position=Vector3.Lerp(pickup.Start,target,ease)+Vector3.UnitY*(MathF.Sin(t*MathF.PI)*.2f);
   poses[poseCount++]=RoundPose(position,pickup.Seed*.91f,t*8);
  }
  if(AutoPickup&&trade.Operation is {} op&&GameObject.Scene.FindGameObject(op.PlayerObjectId) is {} player)
  {
   var delta=player.Transform.WorldPosition-bucket;delta.Y=0;
   if(delta.LengthSquared()<=MathF.Pow(Math.Max(.1f,PickupRadius),2)&&trade.CollectibleAmmo>0)
   {LastCollected=trade.Collect();if(LastCollected>0){CollectionSequence++;int count=Math.Min(Math.Clamp(MaximumPickupTrails,0,16)-pickups.Count,Math.Max(1,(int)Math.Ceiling(LastCollected/VisualUnits(trade.Capacity))));for(int i=0;i<count;i++)pickups.Add(new(PilePosition(i),player.Id,0,i));flights.Clear();credit=0;previous=trade.StoredAmmo;}}
  }
 }
 float VisualUnits(int capacity)=>Math.Max(Math.Max(1,RoundsPerVisual),capacity/(float)Math.Clamp(MaximumVisibleRounds,1,72));
 void ResolveAnchors()
 {
  var renderer=GameObject.GetComponent<SkeletalMeshRenderer>();
  bool Anchor(string name,out Vector3 p){p=default;if(renderer?.TryGetBoneWorldMatrix(name,out var m)!=true)return false;p=m.Translation;return true;}
  ModelAnchorsResolved=Anchor(BeltStartMarker,out start)&Anchor(BeltEndMarker,out end)&Anchor(BucketMarker,out bucket);
  if(!ModelAnchorsResolved){start=Vector3.Transform(EjectionOffset,Transform.WorldMatrix);bucket=Vector3.Transform(BucketOffset,Transform.WorldMatrix);end=Vector3.Lerp(start,bucket,.7f)+Vector3.UnitY*.2f;}
  var direction=end-start;direction.Y=0;forward=direction.LengthSquared()>.001f?Vector3.Normalize(direction):Vector3.UnitZ;side=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,forward));
 }
 Vector3 PilePosition(int i)
 {
  int column=i%6,row=i/6%4,layer=i/24;
  float x=(column-2.5f)/6*Math.Max(.1f,PileFootprint.X)+MathF.Sin(i*17.1f)*.025f;
  float z=(row-1.5f)/4*Math.Max(.1f,PileFootprint.Y)+MathF.Cos(i*11.7f)*.025f;
  float h=.04f+layer*Math.Min(.10f,Math.Max(.02f,MaximumPileHeight)/3)+MathF.Sin(i*9.1f)*.012f;
  return bucket+side*x+forward*z+Vector3.UnitY*h;
 }
 Matrix4x4 RoundPose(Vector3 p,float yaw,float roll)
 {
  float heading=MathF.Atan2(forward.X,forward.Z)+yaw;
  return Matrix4x4.CreateRotationZ(roll)*Matrix4x4.CreateRotationY(heading)*Matrix4x4.CreateTranslation(p);
 }
 protected override void OnRender(RenderContext context)
 {
  if(poseCount==0)return;
  cases??=CreateBatch(out caseVertices);tips??=CreateBatch(out tipVertices);
  Fill(caseVertices!,false);Fill(tipVertices!,true);
  cases.UpdateVertices(caseVertices!);tips.UpdateVertices(tipVertices!);cases.SetDrawIndexCount(poseCount*8*6);tips.SetDrawIndexCount(poseCount*8*6);
  caseMaterial.BaseColor=CaseColor;tipMaterial.BaseColor=TipColor;
  context.RenderWorld.Submit(cases,caseMaterial,Matrix4x4.Identity,RenderQueue3D.Opaque,true,true,true);
  context.RenderWorld.Submit(tips,tipMaterial,Matrix4x4.Identity,RenderQueue3D.Opaque,true,true,true);
 }
 static Mesh CreateBatch(out float[] vertices)
 {
  vertices=new float[Limit*8*4*8];var indices=new uint[Limit*8*6];
  for(int q=0;q<Limit*8;q++){uint v=(uint)(q*4);int j=q*6;indices[j]=v;indices[j+1]=v+1;indices[j+2]=v+2;indices[j+3]=v;indices[j+4]=v+2;indices[j+5]=v+3;}
  return new Mesh(vertices,indices,true);
 }
 void Fill(float[] vertices,bool tip)
 {
  float radius=Math.Clamp(RoundRadius,.01f,.08f),length=Math.Clamp(RoundLength,.06f,.35f);int at=0;
  for(int i=0;i<poseCount;i++)for(int segment=0;segment<8;segment++)for(int v=0;v<4;v++)
  {
   float a=(segment+(v is 1 or 2?1:0))*MathF.Tau/8;bool high=v>=2;
   float z=tip?(high?length*.55f:length*.20f):(high?length*.20f:-length*.45f);
   float r=tip&&high?.001f:radius;
   var p=Vector3.Transform(new Vector3(MathF.Cos(a)*r,MathF.Sin(a)*r,z),poses[i]);
   var n=Vector3.Normalize(Vector3.TransformNormal(new Vector3(MathF.Cos(a),MathF.Sin(a),tip?.4f:0),poses[i]));
   vertices[at++]=p.X;vertices[at++]=p.Y;vertices[at++]=p.Z;vertices[at++]=n.X;vertices[at++]=n.Y;vertices[at++]=n.Z;vertices[at++]=0;vertices[at++]=0;
  }
 }
 protected override void OnDestroy(){cases?.Dispose();tips?.Dispose();cases=tips=null;flights.Clear();pickups.Clear();}
}
