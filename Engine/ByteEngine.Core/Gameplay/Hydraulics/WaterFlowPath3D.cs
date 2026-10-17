using System.Globalization;
using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Gravity-driven stream transport. Each reach stores real water volume; its free surface, delayed outflow and drain-down come from the same profile.</summary>
public sealed class WaterFlowPath3D:Component
{
 public Vector3 Start {get;set;}
 public Vector3 Bend {get;set;}
 public Vector3 End {get;set;}
 public bool HasBend {get;set;}
 public float Width {get;set;}=2;
 /// <summary>Walled channels spread the transported cross section across their bed, rather than rendering a narrowing free jet.</summary>
 public bool ConfinedChannel {get;set;}
 public string AppearanceSurface {get;set;}="";
 public float FoamStrength {get;set;}=.35f;
 public float Speed {get;set;}=2.8f;
 public string FlowGate {get;set;}="";
 public string EndBasin {get;set;}="";
 public string BasinPrefix {get;set;}="";
 public int BasinCount {get;set;}
 public float BedHeight {get;set;}=3.4f;
 public float MaximumDepth {get;set;}=.35f;
 public float ImpactRadius {get;set;}=1;
 public float InitialWetLength {get;set;}
 public string InitialState {get;set;}="";
 public bool ConserveVolume {get;set;}
 public float WetLength {get;private set;}
 public float VisibleFlow {get;private set;}
 public float Outflow {get;private set;}
 public double StoredVolume=>pendingInflow+pendingDelivery+volumes.Sum();
 public double InFlightVolume=>pendingInflow+volumes.Sum();
 const int Along=96,Across=8;
 readonly double[] volumes=new double[Along+1];
 readonly float[] speeds=new float[Along+1];
 double pendingInflow,pendingDelivery;float length,dx;bool initialized;
 Mesh? mesh;float[]? vertices;
 WaterGate3D? gate;WaterBasin3D? destination;WaterBasin3D[] cells=[];WaterSurface3D? appearance;
 readonly Material material=new(){WaterSurface=true,BaseColor=new(.015f,.23f,.26f,.8f),BlendMode=BlendMode3D.AlphaBlend,CullMode=CullMode3D.None,Roughness=.15f};
 internal void EnsureHydraulicsInitialized()
 {
  if(initialized)return;initialized=true;var scene=GameObject.Scene!;gate=scene.FindGameObject(FlowGate)?.GetComponent<WaterGate3D>();destination=scene.FindGameObject(EndBasin)?.GetComponent<WaterBasin3D>();cells=Enumerable.Range(0,Math.Clamp(BasinCount,0,64)).Select(i=>scene.FindGameObject(BasinPrefix+i)?.GetComponent<WaterBasin3D>()).OfType<WaterBasin3D>().ToArray();length=Math.Max(.01f,HasBend?Vector3.Distance(Start,Bend)+Vector3.Distance(Bend,End):Vector3.Distance(Start,End));dx=length/(Along+1);
  for(int i=0;i<=Along;i++)speeds[i]=MathF.Sqrt(Math.Max(.04f,Speed*Speed)+19.62f*Math.Max(0,Start.Y-Point((i+.5f)/(Along+1)).Y));
  var saved=InitialState.Split(',');if(saved.Length==Along+3){pendingInflow=Parse(saved[0]);pendingDelivery=Parse(saved[1]);for(int i=0;i<=Along;i++)volumes[i]=Parse(saved[i+2]);}
 }
 static double Parse(string s)=>double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out var v)&&double.IsFinite(v)?Math.Max(0,v):0;
 public string ExportState()=>string.Join(",",new[]{pendingInflow,pendingDelivery}.Concat(volumes).Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));
 internal void Receive(double volume){EnsureHydraulicsInitialized();pendingInflow+=Math.Max(0,volume);}
 internal double Deliver(double room){double v=Math.Min(Math.Max(0,room),pendingDelivery);pendingDelivery-=v;return v;}
 protected override void OnStart()=>EnsureHydraulicsInitialized();
 protected override void OnFixedUpdate()
 {
  EnsureHydraulicsInitialized();float dt=(float)Time.FixedDeltaTime;VisibleFlow=Math.Max(0,gate?.Flow??0);
  if(cells.Length>0){Outflow=VisibleFlow;return;}
  var end=End;if(destination!=null)end.Y=Math.Max(end.Y,destination.Level+.015f);length=Math.Max(.01f,HasBend?Vector3.Distance(Start,Bend)+Vector3.Distance(Bend,end):Vector3.Distance(Start,end));dx=length/(Along+1);for(int i=0;i<=Along;i++)speeds[i]=MathF.Sqrt(Math.Max(.04f,Speed*Speed)+19.62f*Math.Max(0,Start.Y-Point((i+.5f)/(Along+1)).Y));
  if(!ConserveVolume)pendingInflow+=VisibleFlow*dt;
  int steps=Math.Max(1,(int)MathF.Ceiling(dt*speeds.Max()/dx/.7f));double h=dt/steps,inputRate=pendingInflow/dt,exited=0;
  for(int step=0;step<steps;step++){
   for(int i=Along;i>=0;i--){double amount=Math.Min(volumes[i],volumes[i]/dx*speeds[i]*h);volumes[i]-=amount;if(i==Along)exited+=amount;else volumes[i+1]+=amount;}
   double input=Math.Min(pendingInflow,inputRate*h);pendingInflow-=input;volumes[0]+=input;
  }
  if(ConserveVolume)pendingDelivery+=exited;else pendingDelivery=0;
  Outflow=(float)(exited/dt);WetLength=0;for(int i=0;i<=Along;i++)if(volumes[i]>1e-7)WetLength=(i+1)*dx;
 }
 Vector3 Point(float t){var end=End;if(destination!=null)end.Y=Math.Max(End.Y,destination.Level+.015f);if(!HasBend)return Vector3.Lerp(Start,end,t);float split=Vector3.Distance(Start,Bend)/Math.Max(.01f,length);return t<split?Vector3.Lerp(Start,Bend,t/Math.Max(.001f,split)):Vector3.Lerp(Bend,end,(t-split)/Math.Max(.001f,1-split));}
 protected override void OnRender(RenderContext context)
 {
  if(!context.Has3DCamera)return;
  appearance??=GameObject.Scene?.FindGameObject(AppearanceSurface)?.GetComponent<WaterSurface3D>();
  if(appearance is {} look){material.BaseColor=look.Color;material.WaterAbsorption=new(look.Absorption,look.CausticStrength);material.WaterScattering=new(look.Scattering,look.ScatteringAnisotropy);material.WaterStyle=new(look.NormalStrength,look.Roughness,look.ShoreFoamWidth,0);material.WaterReflections=look.ScreenSpaceReflections;}
  if(cells.Length==0&&WetLength<=.001f)return;
  if(cells.Length>0&&cells.All(b=>b.Level-b.Floor<.004f))return;
  vertices??=new float[(Along+1)*(Across+3)*8];if(mesh==null){var ix=new uint[Along*(Across+2)*6];int k=0;for(int j=0;j<Along;j++)for(int i=0;i<Across+2;i++){uint a=(uint)(j*(Across+3)+i),b=a+1,c=a+Across+3,d=c+1;foreach(uint v in new[]{a,c,b,b,c,d})ix[k++]=v;}mesh=new(vertices,ix,true);}
  int vtx=0;float meanDepth=0;
  for(int j=0;j<=Along;j++){
   float t=(float)j/Along;Vector3 p=Point(t),prev=Point(Math.Max(0,t-.005f)),next=Point(Math.Min(1,t+.005f));var tangent=Vector3.Normalize(next-prev);var side=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,tangent));var normal=Vector3.Normalize(Vector3.Cross(tangent,side));float area=(float)(volumes[j]/dx),q=area*speeds[j],width=ConfinedChannel?Width:Width*Math.Clamp(MathF.Sqrt(q/1.2f),.10f,1),depth=Math.Min(MaximumDepth,area/Math.Max(.01f,width));bool dry=depth<.0015f;
   float localBed=BedHeight;
   if(cells.Length>0){float f=Math.Clamp(t*cells.Length-.5f,0,cells.Length-1);int a=(int)f,b=Math.Min(a+1,cells.Length-1);p.Y=float.Lerp(cells[a].Level,cells[b].Level,f-a);localBed=float.Lerp(cells[a].Floor,cells[b].Floor,f-a);depth=Math.Max(0,p.Y-localBed);width=Width;dry=depth<.004f;}
   meanDepth+=depth;
   for(int i=0;i<=Across+2;i++){float x=Math.Clamp((float)(i-1)/Across,0,1);var position=p+side*((x-.5f)*width);if(cells.Length==0)position.Y+=depth;if(i==0||i==Across+2)position.Y=cells.Length>0?localBed+.001f:p.Y;if(dry)position.Y=(cells.Length>0?localBed:p.Y)-.065f;int k=vtx++*8;vertices[k]=position.X;vertices[k+1]=position.Y;vertices[k+2]=position.Z;vertices[k+3]=normal.X;vertices[k+4]=normal.Y;vertices[k+5]=normal.Z;vertices[k+6]=x;vertices[k+7]=t;}
  }
  mesh.UpdateVertices(vertices);material.WaterSize=new(Width,length);float advection=cells.Length>0?Math.Clamp(cells.Average(b=>Math.Abs(b.Current)),0,3.2f):speeds[Along/2];var direction=Vector2.Normalize(new Vector2(End.X-Start.X,End.Z-Start.Z));material.WaterFlow=cells.Length>0?new(direction,advection,0):new(direction,advection,1);material.WaterMotion=new((float)GameObject.Scene!.SimulationTime,Math.Max(VisibleFlow,Outflow),.008f,FoamStrength);material.WaterColumn=new(meanDepth/(Along+1),0,0,0);context.RenderWorld.Submit(mesh,material,Matrix4x4.Identity,RenderQueue3D.Transparent,false,true,true);
 }
 protected override void OnDestroy(){mesh?.Dispose();mesh=null;}
}
