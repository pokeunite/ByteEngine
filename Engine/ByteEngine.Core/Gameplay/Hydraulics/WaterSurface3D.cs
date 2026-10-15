using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Opt-in visual water. The basin solver remains authoritative; waves never change volume or buoyancy.</summary>
public sealed class WaterSurface3D:Component
{
 public string RendererName {get;set;}="";
 public string BasinName {get;set;}="";
 public string FlowGate {get;set;}="";
 public bool Vertical {get;set;}
 public float WaveHeight {get;set;}=.045f;
 public float FoamStrength {get;set;}=.7f;
 public Vector4 Color {get;set;}=new(.025f,.29f,.34f,.83f);
 public int Segments {get;set;}=48;
 MeshRenderer? renderer;Mesh? ownedMesh,ownedVolume;WaterBasin3D? basin;WaterGate3D? gate;
 protected override void OnStart()
 {
  var s=GameObject.Scene!;renderer=s.FindGameObject(RendererName)?.GetComponent<MeshRenderer>();
  basin=s.FindGameObject(BasinName)?.GetComponent<WaterBasin3D>();gate=s.FindGameObject(FlowGate)?.GetComponent<WaterGate3D>();if(renderer==null)return;
  var size=renderer.Transform.LocalScale;int nx=Math.Clamp(Segments,4,96),nz=Math.Clamp((int)(nx*(Vertical?size.Y:size.Z)/Math.Max(.1f,size.X)),4,48);
  float[] vertices=new float[(nx+1)*(nz+1)*8];uint[] indices=new uint[nx*nz*6];int v=0,k=0;
  for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){
   float u=(float)x/nx,w=(float)z/nz;vertices[v++]=(u-.5f)*size.X;vertices[v++]=Vertical?(w-.5f)*size.Y:0;vertices[v++]=Vertical?0:(w-.5f)*size.Z;
   vertices[v++]=0;vertices[v++]=Vertical?0:1;vertices[v++]=Vertical?1:0;vertices[v++]=u;vertices[v++]=w;
  }
  for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){uint a=(uint)(z*(nx+1)+x),b=a+1,c=a+(uint)nx+1,d=c+1;indices[k++]=a;indices[k++]=c;indices[k++]=b;indices[k++]=b;indices[k++]=c;indices[k++]=d;}
  renderer.MeshReference=null;renderer.MaterialReference=null;renderer.UsePrimitive=false;renderer.Mesh=ownedMesh=new Mesh(vertices,indices);renderer.Transform.LocalScale=Vector3.One;renderer.CastShadows=false;
  renderer.Material=new Material{WaterSurface=true,WaterSize=new(size.X,Vertical?size.Y:size.Z),BaseColor=Color,Roughness=.13f,Metallic=.12f,BlendMode=BlendMode3D.AlphaBlend,CullMode=CullMode3D.None};
  if(basin!=null&&s.FindGameObject(basin.VolumeVisualName+" Mesh")?.GetComponent<MeshRenderer>() is {} volume){
   // Keep a transparent cutaway volume, but remove its duplicate top face to avoid surface z-fighting.
   var data=new List<float>();var tris=new List<uint>();
   void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d){uint start=(uint)(data.Count/8);var n=Vector3.Normalize(Vector3.Cross(b-a,c-a));foreach(var p in new[]{a,b,c,d})data.AddRange([p.X,p.Y,p.Z,n.X,n.Y,n.Z,p.X+.5f,p.Y+.5f]);tris.AddRange([start,start+1,start+2,start,start+2,start+3]);}
   Face(new(-.5f,-.5f,.5f),new(.5f,-.5f,.5f),new(.5f,.5f,.5f),new(-.5f,.5f,.5f));
   Face(new(.5f,-.5f,-.5f),new(-.5f,-.5f,-.5f),new(-.5f,.5f,-.5f),new(.5f,.5f,-.5f));
   Face(new(-.5f,-.5f,-.5f),new(-.5f,-.5f,.5f),new(-.5f,.5f,.5f),new(-.5f,.5f,-.5f));
   Face(new(.5f,-.5f,.5f),new(.5f,-.5f,-.5f),new(.5f,.5f,-.5f),new(.5f,.5f,.5f));
   volume.MeshReference=null;volume.UsePrimitive=false;volume.Mesh=ownedVolume=new Mesh(data.ToArray(),tris.ToArray());
  }
 }
 protected override void OnLateUpdate()
 {
  if(renderer==null)return;float flow=gate?.Flow??basin?.Current??0;
  renderer.Material.WaterMotion=new((float)GameObject.Scene!.SimulationTime,flow,WaveHeight,FoamStrength);
  renderer.Material.WaterWake=Vector4.Zero;
  if(basin!=null){
   var body=GameObject.Scene.GameObjects.SelectMany(o=>o.Components).OfType<BuoyantBody3D>().Where(b=>b.Transform.WorldPosition.X>basin.MinX&&b.Transform.WorldPosition.X<basin.MaxX&&Math.Abs(b.Transform.WorldPosition.Z-basin.CenterZ)<basin.Depth/2).OrderByDescending(b=>b.Size.X).FirstOrDefault();
   if(body!=null){var p=body.Transform.WorldPosition;renderer.Material.WaterWake=new(p.X,p.Z,body.Size.X,Math.Clamp(Math.Abs(flow)*.15f,0,1));}
  }
 }
 protected override void OnDestroy(){ownedMesh?.Dispose();ownedMesh=null;ownedVolume?.Dispose();ownedVolume=null;}
}

/// <summary>Flow-driven shaft animation for native machinery; no object-specific gameplay logic.</summary>
public sealed class FlowRotor3D:Component
{
 public string FlowGate {get;set;}="";public string SecondaryGate {get;set;}="";public string DrivenBody {get;set;}="";
 public bool UseOpening {get;set;} public float DegreesPerUnit {get;set;}=120;public float DegreesPerCubicMetre {get;set;}=18;
 public Vector3 Axis {get;set;}=new(0,0,1);
 WaterGate3D? gate,secondary;GameObject? body;float lastY,angle;Vector3 rest;
 protected override void OnStart(){var scene=GameObject.Scene!;gate=scene.FindGameObject(FlowGate)?.GetComponent<WaterGate3D>();secondary=scene.FindGameObject(SecondaryGate)?.GetComponent<WaterGate3D>();body=scene.FindGameObject(DrivenBody);lastY=body?.Transform.WorldPosition.Y??0;rest=Transform.EulerAngles;}
 protected override void OnLateUpdate(){
  if(body!=null){float y=body.Transform.WorldPosition.Y;Transform.EulerAngles+=Axis*((y-lastY)*DegreesPerUnit);lastY=y;return;}
  if(gate==null)return;
  if(UseOpening){float target=Math.Max(gate.Opening,secondary?.Opening??0)*DegreesPerUnit;angle+=(target-angle)*Math.Clamp((float)Time.DeltaTime*8,0,1);Transform.EulerAngles=rest+Axis*angle;}
  else Transform.EulerAngles+=Axis*(gate.Flow*DegreesPerCubicMetre*(float)Time.DeltaTime);
 }
}
