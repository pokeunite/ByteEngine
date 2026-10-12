using System.Numerics;
using ByteEngine.Core.AI;
namespace DuneCompany;
public sealed partial class LandVehiclePhysics
{
 sealed class DrivenAssembly{public required Part[] Parts;public required Part[] Wheels;public required PlacedBlock[] Engines;public float Mass,AxleMid;public AiDriveCommand Input;public float MaximumSpeed=32;}
 public float ConfiguredMaximumSpeed{get{float speed=0;foreach(var engine in _playerRig.Engines)speed=Math.Max(speed,engine.SpeedLimit);return speed;}}
 DrivenAssembly _playerRig=null!;
 DrivenAssembly DescribeDrive(Part[] parts){var wheels=parts.Where(p=>_catalog[p.Block.Type].Wheel).ToArray();return new(){Parts=parts,Wheels=wheels,Engines=parts.Select(p=>p.Block).Where(b=>_catalog[b.Type].Power>0).ToArray(),Mass=parts.Sum(p=>_catalog[p.Block.Type].Mass),AxleMid=wheels.Length>0?(wheels.Min(p=>p.Block.P.Z)+wheels.Max(p=>p.Block.P.Z))*.5f:0};}
 readonly Dictionary<int,DrivenAssembly> _drivenAssemblies=[];
 /// <summary>Adds an independently motorised rig to this simulation. All rigs collide through the same solver.</summary>
 public int AddDrivenAssembly(IReadOnlyList<PlacedBlock> blocks,IReadOnlyDictionary<int,VehiclePartPhysics> definitions,Vector3 position,Quaternion rotation)
 {
  int root=20000+_drivenAssemblies.Count*10000;
  if(blocks.Count==0||blocks.All(b=>b.Id!=0)||blocks.Any(b=>b.Id<0||b.Id>=10000))throw new ArgumentException("Assembly requires root zero and local IDs below 10000.");
  var copies=blocks.Select(b=>System.Text.Json.JsonSerializer.Deserialize<PlacedBlock>(System.Text.Json.JsonSerializer.Serialize(b,BuildStore.Json),BuildStore.Json)!).ToArray();
  foreach(var b in copies){b.Id+=root;if(b.Parent>=0)b.Parent+=root;}
  AddAssembly(copies,definitions,position,rotation);if(copies.Any(b=>b.Type==32))SetSolverProfile(SolverIterations,SolverSubsteps);_drivenAssemblies.Add(root,DescribeDrive(copies.Select(b=>_parts[b.Id]).ToArray()));return root;
 }
 public void SetAssemblyInput(int root,AiDriveCommand input,float maximumSpeed=32){var rig=_drivenAssemblies[root];rig.Input=new(float.IsFinite(input.Throttle)?Math.Clamp(input.Throttle,-1,1):0,float.IsFinite(input.Steering)?Math.Clamp(input.Steering,-1,1):0,input.Brake);rig.MaximumSpeed=float.IsFinite(maximumSpeed)?Math.Clamp(maximumSpeed,1,50):32;}
 public (Vector3 Position,Quaternion Rotation) AssemblyFrame(int root,bool rendered=false){var g=_parts[root].Root;var p=rendered?RenderPose(g):_simulation.Bodies[g.Body].Pose;return(p.Position-Vector3.Transform(g.Center,p.Orientation),p.Orientation);}
 public Vector3 AssemblyVelocity(int root)=>_simulation.Bodies[_parts[root].Root.Body].Velocity.Linear;
 public bool AssemblyGrounded(int root)=>_drivenAssemblies[root].Parts.Any(p=>p.Output!=null&&_catalog[p.Block.Type].Wheel&&WheelFeedback(p.Block.Id).Grounded);
 public void ResetDrivenAssembly(int root,Vector3 position,Quaternion rotation)=>ResetAssembly(_drivenAssemblies[root].Parts,position,rotation);
}

