using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Gameplay.Hydraulics;
/// <summary>Flux-driven impact foam and short ballistic spray, bounded to a receiving basin.</summary>
public sealed class WaterImpact3D:Component
{
 public string FlowGate {get;set;}="";
 public string FlowPathName {get;set;}="";
 public string BasinName {get;set;}="";
 public Vector3 Position {get;set;}
 public Vector2 Drift {get;set;}=new(.35f,0);
 public float Radius {get;set;}=.8f;
 public float Strength {get;set;}=1;
 public float Activity {get;private set;}
 WaterFlowPath3D? stream;WaterGate3D? gate;WaterBasin3D? basin;Mesh? mesh;float[]? vertices;
 const int Count=80,Sides=7;
 readonly Material material=new(){BaseColor=new(.78f,.91f,.88f,.62f),BlendMode=BlendMode3D.AlphaBlend,CullMode=CullMode3D.None,Roughness=.8f};
 protected override void OnStart(){var s=GameObject.Scene!;gate=s.FindGameObject(FlowGate)?.GetComponent<WaterGate3D>();stream=s.FindGameObject(FlowPathName)?.GetComponent<WaterFlowPath3D>();basin=s.FindGameObject(BasinName)?.GetComponent<WaterBasin3D>();}
 protected override void OnFixedUpdate(){float target=Math.Clamp(Math.Abs(stream?.Outflow??gate?.Flow??0)*Strength,0,1);Activity+=(target-Activity)*(1-MathF.Exp(-(float)Time.FixedDeltaTime*(target>Activity?5:1.5f)));}
 protected override void OnRender(RenderContext context){if(!context.Has3DCamera||Activity<.005f||basin==null)return;vertices??=new float[Count*Sides*3*8];if(mesh==null){uint[] ix=Enumerable.Range(0,Count*Sides*3).Select(i=>(uint)i).ToArray();mesh=new(vertices,ix,true);}float time=(float)GameObject.Scene!.SimulationTime;int k=0;
  for(int i=0;i<Count;i++){float seed=i*.61803399f,angle=i*2.399963f,t=(time*(i<58?.35f:1.2f)+seed)%1,spread=MathF.Sqrt((i*.3721f)%1)*Radius;var center=Position;center.X+=MathF.Cos(angle)*spread*(.25f+t)+Drift.X*t;center.Z+=MathF.Sin(angle)*spread*(.25f+t)+Drift.Y*t;center.Y=basin.Level+.025f;
   bool spray=i>=58;if(spray){center.Y+=4*t*(1-t)*.40f;center.X+=MathF.Cos(angle)*t*.3f;center.Z+=MathF.Sin(angle)*t*.3f;}
   center.X=Math.Clamp(center.X,basin.MinX+.05f,basin.MaxX-.05f);center.Z=Math.Clamp(center.Z,basin.CenterZ-basin.Depth/2+.05f,basin.CenterZ+basin.Depth/2-.05f);
   float radius=(spray?.018f:.045f+.08f*t)*Activity*MathF.Sin(MathF.PI*t);for(int j=0;j<Sides;j++){float a=j*MathF.Tau/Sides,b=(j+1)*MathF.Tau/Sides;Write(center);Write(center+new Vector3(MathF.Cos(a)*radius,spray?MathF.Sin(a)*radius:0,spray?0:MathF.Sin(a)*radius));Write(center+new Vector3(MathF.Cos(b)*radius,spray?MathF.Sin(b)*radius:0,spray?0:MathF.Sin(b)*radius));}}
  // Write directly into the reusable stream instead of allocating one triangle array per spray face.
  void Write(Vector3 p){vertices![k++]=p.X;vertices[k++]=p.Y;vertices[k++]=p.Z;vertices[k++]=0;vertices[k++]=1;vertices[k++]=0;vertices[k++]=0;vertices[k++]=0;}
  mesh.UpdateVertices(vertices);context.RenderWorld.Submit(mesh,material,Matrix4x4.Identity,RenderQueue3D.Transparent,false,true,true);
 }
 protected override void OnDestroy(){mesh?.Dispose();mesh=null;}
}
