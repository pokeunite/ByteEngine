using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Opt-in visual water. The basin solver remains authoritative; waves never change volume or buoyancy.</summary>
public sealed class WaterSurface3D:Component
{
 public Vector3 Absorption {get;set;}=new(.53f,.115f,.065f);
 public Vector3 Scattering {get;set;}=new(.012f,.17f,.19f);
 public float ScatteringAnisotropy {get;set;}=.25f;
 public float CausticStrength {get;set;}=.65f;
 public float Roughness {get;set;}=.13f;
 public float NormalStrength {get;set;}=1;
 public float ShoreFoamWidth {get;set;}=.16f;
 public bool ScreenSpaceReflections {get;set;}=true;
 public string RendererName {get;set;}="";
 public string BasinName {get;set;}="";
 public string FlowGate {get;set;}="";
 public string FlowPathName {get;set;}="";
 public float DryDepth {get;set;}=.004f;
 public bool IsWet {get;private set;}
 public bool Vertical {get;set;}
 public float WaveHeight {get;set;}=.045f;
 public float FoamStrength {get;set;}=.7f;
 public Vector4 Color {get;set;}=new(.025f,.29f,.34f,.83f);
 public int Segments {get;set;}=80;
 public bool MatchSimulationResolution {get;set;}=true;
 public Vector2 SurfaceSize {get;set;}
 public Vector2 FlowDirection {get;set;}=Vector2.UnitX;
 public float FlowSpeedScale {get;set;}=1;
 public float MaximumFlowSpeed {get;set;}=1.6f;
 public Vector3 ImpactPosition {get;set;}
 public float ImpactStrength {get;set;}
 public float ImpactRadius {get;set;}=1.2f;
 public float VolumeOpacity {get;set;}=.78f;
 public Vector4 VolumeTint {get;set;}=new(.01f,.21f,.27f,1);
 float[]? dynamicVertices;int gridX,gridZ;Vector3 gridSize;WaterDynamics3D? dynamics;WaterFlowPath3D? arrival;
 MeshRenderer? volumeRenderer;MeshRenderer? renderer;Mesh? ownedMesh,ownedVolume;WaterBasin3D? basin;WaterGate3D? gate;
 protected override void OnStart()
 {
  var s=GameObject.Scene!;renderer=s.FindGameObject(RendererName)?.GetComponent<MeshRenderer>();
  basin=s.FindGameObject(BasinName)?.GetComponent<WaterBasin3D>();dynamics=basin?.GameObject.GetComponent<WaterDynamics3D>();arrival=s.FindGameObject(FlowPathName)?.GetComponent<WaterFlowPath3D>();gate=s.FindGameObject(FlowGate)?.GetComponent<WaterGate3D>();if(renderer==null)return;
  var size=renderer.Transform.LocalScale;if(SurfaceSize.LengthSquared()>.0001f)size=Vertical?new(SurfaceSize.X,SurfaceSize.Y,.02f):new(SurfaceSize.X,.02f,SurfaceSize.Y);else SurfaceSize=new(size.X,Vertical?size.Y:size.Z);int nx=Math.Clamp(Segments,4,96),nz=Math.Clamp((int)(nx*(Vertical?size.Y:size.Z)/Math.Max(.1f,size.X)),4,48);
  if(!Vertical&&MatchSimulationResolution&&dynamics!=null){nx=Math.Min(nx,Math.Clamp(dynamics.Columns,4,96));nz=Math.Min(nz,Math.Clamp(dynamics.Rows,4,48));}
  float[] vertices=new float[(nx+1)*(nz+1)*8];uint[] indices=new uint[nx*nz*6];int v=0,k=0;
  for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){
   float u=(float)x/nx,w=(float)z/nz;vertices[v++]=(u-.5f)*size.X;vertices[v++]=Vertical?(w-.5f)*size.Y:0;vertices[v++]=Vertical?0:(w-.5f)*size.Z;
   vertices[v++]=0;vertices[v++]=Vertical?0:1;vertices[v++]=Vertical?1:0;vertices[v++]=u;vertices[v++]=w;
  }
  for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){uint a=(uint)(z*(nx+1)+x),b=a+1,c=a+(uint)nx+1,d=c+1;indices[k++]=a;indices[k++]=c;indices[k++]=b;indices[k++]=b;indices[k++]=c;indices[k++]=d;}
  renderer.MeshReference=null;renderer.MaterialReference=null;renderer.UsePrimitive=false;dynamicVertices=vertices;gridX=nx;gridZ=nz;gridSize=size;renderer.Mesh=ownedMesh=new Mesh(vertices,indices,true);renderer.Transform.LocalScale=Vector3.One;renderer.CastShadows=false;
  renderer.Material=new Material{WaterSurface=true,WaterSize=new(size.X,Vertical?size.Y:size.Z),BaseColor=Color,Roughness=.13f,Metallic=.12f,BlendMode=BlendMode3D.AlphaBlend,CullMode=CullMode3D.None};
  if(basin!=null&&s.FindGameObject(basin.VolumeVisualName+" Mesh")?.GetComponent<MeshRenderer>() is {} volume){
   // Keep a transparent cutaway volume, but remove its duplicate top face to avoid surface z-fighting.
   var data=new List<float>();var tris=new List<uint>();
   void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d){uint start=(uint)(data.Count/8);var n=Vector3.Normalize(Vector3.Cross(b-a,c-a));foreach(var p in new[]{a,b,c,d})data.AddRange([p.X,p.Y,p.Z,n.X,n.Y,n.Z,p.X+.5f,p.Y+.5f]);tris.AddRange([start,start+1,start+2,start,start+2,start+3]);}
   Face(new(-.5f,-.5f,.5f),new(.5f,-.5f,.5f),new(.5f,.5f,.5f),new(-.5f,.5f,.5f));
   Face(new(.5f,-.5f,-.5f),new(-.5f,-.5f,-.5f),new(-.5f,.5f,-.5f),new(.5f,.5f,-.5f));
   Face(new(-.5f,-.5f,-.5f),new(-.5f,-.5f,.5f),new(-.5f,.5f,.5f),new(-.5f,.5f,-.5f));
   Face(new(.5f,-.5f,.5f),new(.5f,-.5f,-.5f),new(.5f,.5f,-.5f),new(.5f,.5f,.5f));
   Face(new(-.5f,-.5f,-.5f),new(.5f,-.5f,-.5f),new(.5f,-.5f,.5f),new(-.5f,-.5f,.5f));
   volumeRenderer=volume;volume.MeshReference=null;volume.MaterialReference=null;volume.MaterialAssetReference=ByteEngine.Core.Assets.AssetReference.Empty;volume.MaterialOverrides.Reset();volume.Visible=true;volume.Enabled=true;volume.CastShadows=false;volume.UsePrimitive=false;volume.Mesh=ownedVolume=new Mesh(data.ToArray(),tris.ToArray());
   volume.Material=new Material{WaterVolume=true,BaseColor=VolumeTint,BlendMode=BlendMode3D.AlphaBlend,CullMode=CullMode3D.None,Roughness=.22f};
  }
  UpdateWetVisibility();
 }
 protected override void OnLateUpdate()
 {
  if(renderer==null)return;UpdateWetVisibility();if(!IsWet)return;UpdateSpatialSurface();float flow=arrival?.Outflow??gate?.Flow??basin?.Current??0;
  float depth=basin!=null?Math.Max(0,basin.Level-basin.Floor):1;
  renderer.Material.WaterMotion=new((float)GameObject.Scene!.SimulationTime,flow,dynamics?.Grid!=null?0:Math.Min(WaveHeight,depth*.15f),FoamStrength);
  renderer.Material.WaterFlow=new(FlowDirection,Math.Min(MaximumFlowSpeed,Math.Abs(flow)*FlowSpeedScale/Math.Max(.3f,(basin?.Depth??2)*depth)),0);
  renderer.Material.WaterImpact=new(ImpactPosition.X,ImpactPosition.Z,ImpactRadius,Math.Clamp(Math.Abs(flow)*.4f,0,1)*ImpactStrength);
  renderer.Material.WaterColumn=new(basin?.Level??1,basin?.Floor??0,.8f,0);
  renderer.Material.WaterAbsorption=new(Absorption,CausticStrength);renderer.Material.WaterScattering=new(Scattering,ScatteringAnisotropy);renderer.Material.WaterStyle=new(NormalStrength,Roughness,ShoreFoamWidth,0);renderer.Material.WaterReflections=ScreenSpaceReflections;
  renderer.Material.WaterWake=Vector4.Zero;
  if(basin!=null&&volumeRenderer!=null)volumeRenderer.Material.WaterColumn=new(basin.Level,basin.Floor,.8f,Math.Clamp(VolumeOpacity,0,1));
  if(basin!=null){
   var body=GameObject.Scene.GameObjects.SelectMany(o=>o.Components).OfType<BuoyantBody3D>().Where(b=>b.Transform.WorldPosition.X>basin.MinX&&b.Transform.WorldPosition.X<basin.MaxX&&Math.Abs(b.Transform.WorldPosition.Z-basin.CenterZ)<basin.Depth/2).OrderByDescending(b=>b.Size.X).FirstOrDefault();
   if(body!=null){var p=body.Transform.WorldPosition;renderer.Material.WaterWake=new(p.X,p.Z,body.Size.X,Math.Clamp((body.GameObject.GetComponent<Rigidbody3D>()?.Velocity.Length()??0)*.7f,0,1));}
  }
 }
 void UpdateWetVisibility(){IsWet=basin==null||basin.Level-basin.Floor>DryDepth;if(renderer!=null)renderer.Visible=IsWet;if(volumeRenderer!=null)volumeRenderer.Visible=IsWet;}
 void UpdateSpatialSurface()
 {
  if(Vertical||dynamics?.Grid==null||dynamicVertices==null||ownedMesh==null)return;var center=Transform.WorldPosition;
  // Sample the solver once per vertex. Derive normals from that same visible
  // surface, instead of four additional bilinear solver lookups per vertex.
  for(int z=0;z<=gridZ;z++)for(int x=0;x<=gridX;x++){int k=(z*(gridX+1)+x)*8;dynamicVertices[k+1]=dynamics.Sample(dynamicVertices[k]+center.X,dynamicVertices[k+2]+center.Z);}
  for(int z=0;z<=gridZ;z++)for(int x=0;x<=gridX;x++){
   int k=(z*(gridX+1)+x)*8,l=(z*(gridX+1)+Math.Max(0,x-1))*8,r=(z*(gridX+1)+Math.Min(gridX,x+1))*8,b=(Math.Max(0,z-1)*(gridX+1)+x)*8,f=(Math.Min(gridZ,z+1)*(gridX+1)+x)*8;
   float gx=(dynamicVertices[r+1]-dynamicVertices[l+1])/Math.Max(.001f,dynamicVertices[r]-dynamicVertices[l]),gz=(dynamicVertices[f+1]-dynamicVertices[b+1])/Math.Max(.001f,dynamicVertices[f+2]-dynamicVertices[b+2]);var normal=Vector3.Normalize(new Vector3(-gx,1,-gz));dynamicVertices[k+3]=normal.X;dynamicVertices[k+4]=normal.Y;dynamicVertices[k+5]=normal.Z;
  }
  ownedMesh.UpdateVertices(dynamicVertices);
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
