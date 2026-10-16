using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Physics;
namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Reusable local surface dynamics for a native basin: wall reflections, ambient wind ripples, jets and body wakes. No level names or puzzle logic.</summary>
public sealed class WaterDynamics3D:Component
{
 public int Columns {get;set;}=48;
 public int Rows {get;set;}=32;
 public float AmbientAmplitude {get;set;}=.012f;
 public float Damping {get;set;}=.45f;
 public float MaximumWaveHeight {get;set;}=.12f;
 public float BuoyancyInfluence {get;set;}=.25f;
 public string ImpactFlowPath {get;set;}="";
 public string ImpactGate {get;set;}="";
 public Vector3 ImpactPosition {get;set;}
 public float ImpactStrength {get;set;}=.02f;
 public float FlowSpeedScale {get;set;}=1;
 public float MaximumCurrent {get;set;}=1.3f;
 public float TimeSeconds {get;private set;}
 public float WaveEnergy=>Grid?.Energy??0;
 public float MaximumDisplacement=>Grid?.MaximumDisplacement??0;
 public float MeanOffset=>Grid?.MeanOffset??0;
 public ShallowWaterGrid? Grid {get;private set;}
 WaterBasin3D? basin;WaterNetwork3D? network;WaterGate3D? jet;WaterFlowPath3D? stream;
 protected override void OnStart(){basin=GameObject.GetComponent<WaterBasin3D>();if(basin==null)return;basin.Dynamics=this;Grid=new(Columns,Rows,basin.MaxX-basin.MinX,basin.Depth);Grid.Seed(AmbientAmplitude);var s=GameObject.Scene!;network=s.GameObjects.SelectMany(o=>o.Components).OfType<WaterNetwork3D>().FirstOrDefault();jet=s.FindGameObject(ImpactGate)?.GetComponent<WaterGate3D>();stream=s.FindGameObject(ImpactFlowPath)?.GetComponent<WaterFlowPath3D>();}
 public float Sample(float x,float z)=>Grid!=null&&basin!=null?Grid.Sample(x-basin.MinX,z-(basin.CenterZ-basin.Depth/2)):0;
 protected override void OnFixedUpdate()
 {
  if(basin==null||Grid==null)return;float dt=(float)Time.FixedDeltaTime;TimeSeconds+=dt;float depth=basin.Level-basin.Floor;if(depth<.004f){Grid.Clear();return;}
  float wind=AmbientAmplitude*.7f*MathF.Sin(TimeSeconds*2.2f)*dt;Grid.Impulse(Grid.Width*.27f,Grid.Length*.32f,.7f,wind);Grid.Impulse(Grid.Width*.73f,Grid.Length*.68f,.7f,-wind);
  float q=stream?.Outflow??jet?.Flow??0;if(q>.001f)Grid.Impulse(ImpactPosition.X-basin.MinX,ImpactPosition.Z-(basin.CenterZ-basin.Depth/2),.42f,q*ImpactStrength*dt*(.6f+MathF.Sin(TimeSeconds*11)*.4f));
  if(network!=null)foreach(var body in network.Bodies){var p=body.Transform.WorldPosition;var rb=body.GameObject.GetComponent<Rigidbody3D>();if(rb==null||p.X<basin.MinX||p.X>basin.MaxX||Math.Abs(p.Z-basin.CenterZ)>basin.Depth/2||body.Bottom>basin.Level+.1f)continue;var v=new Vector2(rb.Velocity.X,rb.Velocity.Z);float speed=v.Length();if(speed<.02f)continue;var dir=v/speed;float radius=Math.Clamp(body.Size.Z*.3f,.15f,.5f);Grid.Impulse(p.X-basin.MinX+dir.X*body.Size.X*.5f,p.Z-(basin.CenterZ-basin.Depth/2)+dir.Y*body.Size.X*.5f,radius,speed*dt*.025f);Grid.Impulse(p.X-basin.MinX-dir.X*body.Size.X*.5f,p.Z-(basin.CenterZ-basin.Depth/2)-dir.Y*body.Size.X*.5f,radius,-speed*dt*.025f);}
  Grid.Step(dt,depth,new(Math.Clamp(basin.Current*FlowSpeedScale,-MaximumCurrent,MaximumCurrent),0),Damping,Math.Min(MaximumWaveHeight,depth*.25f));
 }
}
