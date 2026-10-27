using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
/// <summary>Bounded refinery delivery and a single batched pile backed by actual refined cargo.</summary>
public sealed class MiningRefineryOutput3D:ByteEngine.Core.Scene.Component
{
 [SceneObjectReference] public Guid OperationObjectId{get;set;}
 public string OutputStartMarker{get;set;}="OutputStart";
 public string OutputEndMarker{get;set;}="OutputEnd";
 public string BinMarker{get;set;}="BucketFloor";
 public int MaximumVisibleIngots{get;set;}=96;
 public int UnitsPerIngot{get;set;}=4;
 public int MaximumTravellingIngots{get;set;}=8;
 public float ConveyorSpeed{get;set;}=.75f;
 public float DropSeconds{get;set;}=.55f;
 public float DeliveryInterval{get;set;}=.18f;
 public Vector2 PileFootprint{get;set;}=new(2.15f,1.65f);
 public float LayerHeight{get;set;}=.17f;
 public Vector3 IngotSize{get;set;}=new(.38f,.15f,.23f);
 public Vector4 IngotColor{get;set;}=new(.49f,.52f,.55f,1);
 [Browsable(false)] public bool ModelAnchorsResolved{get;private set;}
 [Browsable(false)] public int VisiblePileIngots{get;private set;}
 [Browsable(false)] public int TravellingIngots=>flights.Count;
 [Browsable(false)] public bool HasDeliveryWork=>pending>0||flights.Count>0;
 [Browsable(false)] public long LandedIngotSequence{get;private set;}
 [Browsable(false)] public Vector3 LastLandingPosition{get;private set;}
 [Browsable(false)] public Vector3 ConveyorSoundPosition=>(start+end)*.5f;
 const int Limit=96;readonly List<float> flights=[];readonly Matrix4x4[] poses=new Matrix4x4[Limit];
 int previous,pending,poseCount;bool initialized;float emit;
 Vector3 start,end,bin;
 Mesh? mesh;float[]? vertices;readonly Material material=new(){Metallic=.7f,Roughness=.32f};
 static readonly Vector3[] Template=BuildTemplate();
 protected override void OnUpdate()
 {
  var op=GameObject.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();var renderer=GameObject.GetComponent<SkeletalMeshRenderer>();
  if(op==null||renderer==null)return;
  bool Anchor(string name,out Vector3 p){p=default;if(!renderer.TryGetBoneWorldMatrix(name,out var m))return false;p=m.Translation;return true;}
  ModelAnchorsResolved=Anchor(OutputStartMarker,out start)&Anchor(OutputEndMarker,out end)&Anchor(BinMarker,out bin);
  if(!ModelAnchorsResolved){poseCount=0;return;}
  float dt=Math.Clamp((float)Time.DeltaTime,0,1),duration=Vector3.Distance(start,end)/Math.Max(.1f,ConveyorSpeed)+Math.Max(.1f,DropSeconds);
  if(initialized&&op.RefinedOre<previous){pending=0;flights.Clear();}
  for(int i=flights.Count-1;i>=0;i--){flights[i]+=dt;if(flights[i]>=duration){LastLandingPosition=Pile(VisiblePileIngots+i);LandedIngotSequence++;flights.RemoveAt(i);}}
  float unit=Math.Max(Math.Max(1,UnitsPerIngot),op.CargoCapacity/(float)Math.Clamp(MaximumVisibleIngots,1,Limit));
  int total=Math.Clamp((int)Math.Ceiling(op.RefinedOre/unit),0,Math.Clamp(MaximumVisibleIngots,0,Limit));
  if(initialized){if(op.RefinedOre<previous){pending=0;flights.Clear();}else if(op.RefinedOre>previous)pending+=Math.Max(0,total-(int)Math.Ceiling(previous/unit));}
  initialized=true;previous=op.RefinedOre;pending=Math.Min(pending,Math.Max(0,total-flights.Count));
  emit-=dt;
  if(pending>0&&emit<=0&&flights.Count<Math.Clamp(MaximumTravellingIngots,1,12)){flights.Add(0);pending--;emit=Math.Max(.05f,DeliveryInterval);}
  while(flights.Count>total)flights.RemoveAt(flights.Count-1);
  VisiblePileIngots=Math.Max(0,total-flights.Count-pending);poseCount=0;
  for(int i=0;i<VisiblePileIngots;i++)poses[poseCount++]=Pose(Pile(i),i*.91f,.08f*MathF.Sin(i*11.7f));
  float travel=Vector3.Distance(start,end)/Math.Max(.1f,ConveyorSpeed);
  for(int i=0;i<flights.Count;i++)
  {
   float age=flights[i],t=Math.Clamp((age-travel)/Math.Max(.1f,DropSeconds),0,1);
   Vector3 p=age<travel?Vector3.Lerp(start,end,age/Math.Max(.01f,travel)):Vector3.Lerp(end,Pile(VisiblePileIngots+i),t)+Vector3.UnitY*MathF.Sin(t*MathF.PI)*.15f;
   poses[poseCount++]=Pose(p,age<travel?0:(VisiblePileIngots+i)*.91f,0);
  }
 }
 Vector3 Pile(int i)
 {
  // Anchor translations inherit model transforms; orient the grid using the conveyor.
  var axis=end-start;axis.Y=0;axis=axis.LengthSquared()>.001f?Vector3.Normalize(axis):Vector3.UnitX;var across=Vector3.Cross(axis,Vector3.UnitY);
  return bin+axis*((i%6-2.5f)/6*PileFootprint.X+MathF.Sin(i*8.1f)*.025f)+across*((i/6%4-1.5f)/4*PileFootprint.Y+MathF.Cos(i*4.7f)*.025f)+Vector3.UnitY*(.09f+i/24*Math.Clamp(LayerHeight,.10f,.23f));
 }
 Matrix4x4 Pose(Vector3 p,float yaw,float roll)=>Matrix4x4.CreateScale(IngotSize)*Matrix4x4.CreateRotationZ(roll)*Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateTranslation(p);
 protected override void OnRender(RenderContext c)
 {
  if(poseCount<=0)return;
  if(mesh==null){vertices=new float[Limit*Template.Length*8];var indices=Enumerable.Range(0,Limit*Template.Length).Select(i=>(uint)i).ToArray();mesh=new(vertices,indices,true);}
  int at=0;
  for(int i=0;i<poseCount;i++)for(int face=0;face<Template.Length;face+=3)
  {
   var normal=Vector3.Normalize(Vector3.Cross(Template[face+1]-Template[face],Template[face+2]-Template[face]));normal=Vector3.Normalize(Vector3.TransformNormal(normal,poses[i]));
   for(int v=0;v<3;v++){var p=Vector3.Transform(Template[face+v],poses[i]);vertices![at++]=p.X;vertices[at++]=p.Y;vertices[at++]=p.Z;vertices[at++]=normal.X;vertices[at++]=normal.Y;vertices[at++]=normal.Z;vertices[at++]=0;vertices[at++]=0;}
  }
  mesh.UpdateVertices(vertices!);mesh.SetDrawIndexCount(poseCount*Template.Length);material.BaseColor=IngotColor;c.RenderWorld.Submit(mesh,material,Matrix4x4.Identity,RenderQueue3D.Opaque,true,true,true);
 }
 static Vector3[] BuildTemplate()
 {
  var rings=new List<Vector3>();foreach(var (y,s) in new[]{(-.5f,.82f),(-.3f,1f),(.3f,1f),(.5f,.82f)})foreach(var (x,z) in new[]{(-.5f,-.5f),(-.5f,.5f),(.5f,.5f),(.5f,-.5f)})rings.Add(new(x*s,y,z*s));
  var result=new List<Vector3>();void Tri(int a,int b,int c){result.Add(rings[a]);result.Add(rings[b]);result.Add(rings[c]);}
  for(int layer=0;layer<3;layer++)for(int i=0;i<4;i++){int a=layer*4+i,b=layer*4+(i+1)%4;Tri(a,b,b+4);Tri(a,b+4,a+4);}
  Tri(0,2,1);Tri(0,3,2);Tri(12,13,14);Tri(12,14,15);return result.ToArray();
 }
 protected override void OnDestroy(){mesh?.Dispose();mesh=null;flights.Clear();}
}
