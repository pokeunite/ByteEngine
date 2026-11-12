using System.Numerics;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Graphics.ThreeD;
public sealed partial class SkeletalMeshRenderer
{
 /// <summary>Opt-in quantized pose/geometry sharing for repeated simple animated actors.
 /// Controllers with layers, transitions, IK, reactions or physical poses keep their private path.</summary>
 bool shareCrowdPoses;
 public bool ShareCrowdPoses{get=>shareCrowdPoses;set{if(shareCrowdPoses!=value){shareCrowdPoses=value;_poseDirty=true;}}}
 public int CrowdPoseFramesPerSecond{get;set;}=20;
 static readonly ConditionalWeakTable<ModelAsset,CrowdPool> CrowdPools=new();
 sealed class CrowdPool{public readonly Dictionary<Mesh,Mesh> ReducedShadows=new();public int Users;public long Hits,Misses,Refused;public readonly Dictionary<(ImportedAnimation Clip,int Frame,int Rate),CrowdPose> Poses=[];public readonly Dictionary<string,Material> FlatMaterials=[];}
 internal object CaptureCrowdState()=>new {model=_model?.Name,users=crowdPool?.Users,poses=crowdPool?.Poses.Count,hits=crowdPool?.Hits,misses=crowdPool?.Misses,refused=crowdPool?.Refused,clips=crowdPool?.Poses.Keys.GroupBy(k=>k.Clip).Select(g=>new{g.Key.Name,g.Key.Duration,frames=g.Count()}).ToArray()};
 sealed record CrowdPose(Matrix4x4[] Globals,Matrix4x4 Root,Mesh[] Meshes,Matrix4x4[] Placement,CrowdDraw[] Draws,Mesh? Shadow);
 sealed record CrowdDraw(Mesh Mesh,Matrix4x4 Placement,int Slot,Material? PaletteMaterial=null)
 {
  private Mesh? distantMesh;
  public Mesh DistantMesh => distantMesh ??= MeshSimplifier.Simplify(Mesh,.02f);
  public void DisposeDistantMesh() => distantMesh?.Dispose();
 }
 CrowdDraw[]? crowdDraws;Mesh? crowdShadow;
 public bool BatchCrowdSubmeshes{get;set;}=true;
 CrowdPool? crowdPool;ModelAsset? crowdModel;
 bool CrowdEligible=>ShareCrowdPoses&&_hiddenMeshKeys.Count==0&&_model!=null&&_currentAnimation!=null&&!HasAdvancedPose&&_previousAnimation==null&&!_lightRagdollActive&&_recoveryDuration<=0&&_hitReaction<=.001f&&_physicalBoneDeformations.Count==0;
 bool TryCrowdPose(out (ImportedAnimation Clip,int Frame,int Rate) key,out float sampleTime)
 {
  crowdShadow=null;crowdDraws=null;key=default;sampleTime=_currentTime;foreach(var mesh in _runtimeMeshes)mesh.SharedCrowdMesh=null;
  if(!CrowdEligible)return false;
  if(crowdPool==null){crowdModel=_model;crowdPool=CrowdPools.GetValue(_model!,_=>new());crowdPool.Users++;}
  int rate=Math.Clamp(CrowdPoseFramesPerSecond,8,30);int frame=(int)MathF.Floor(_currentTime*rate);key=(_currentAnimation!,frame,rate);sampleTime=frame/(float)rate;
  if(!crowdPool.Poses.TryGetValue(key,out var pose)){crowdPool.Misses++;return false;}crowdPool.Hits++;
  crowdShadow=pose.Shadow;crowdDraws=pose.Draws;_currentPoseGlobals=pose.Globals;_currentRootMotionCorrection=pose.Root;
  for(int i=0;i<_runtimeMeshes.Count;i++){_runtimeMeshes[i].SharedCrowdMesh=pose.Meshes[i];_runtimeMeshes[i].MeshToModelMatrix=pose.Placement[i];}
  SkeletalAttachmentService.UpdateForRenderer(this);return true;
 }
 void StoreCrowdPose((ImportedAnimation Clip,int Frame,int Rate) key)
 {
  if(!CrowdEligible||crowdPool==null)return;
  if(crowdPool.Poses.Count>=256){crowdPool.Refused++;return;}
  var meshes=new Mesh[_runtimeMeshes.Count];var placements=new Matrix4x4[meshes.Length];
  // Geometry is immutable per cached pose, but these remain animated casters.
  // Mark them dynamic so moving crowds never invalidate cached scenery shadows.
  for(int i=0;i<meshes.Length;i++){var runtime=_runtimeMeshes[i];meshes[i]=new Mesh(runtime.DeformedVertices.ToArray(),runtime.Source.Indices,true);placements[i]=runtime.MeshToModelMatrix;runtime.SharedCrowdMesh=meshes[i];}
  crowdDraws=BuildCrowdDraws(meshes,placements);
  crowdShadow=!OperatingSystem.IsBrowser()&&_runtimeMeshes.All(m=>m.Material.BlendMode==BlendMode3D.Opaque&&m.Material.HeightFieldTexture==null)&&placements.All(m=>m.GetDeterminant()>.000001f)?MergeCrowdGeometry(meshes,placements,Enumerable.Range(0,meshes.Length).ToArray()):null;
  crowdPool.Poses[key]=new(_currentPoseGlobals.ToArray(),_currentRootMotionCorrection,meshes,placements,crowdDraws,crowdShadow);
 }
 void ReleaseCrowdPool()
 {
  if(crowdPool!=null&&--crowdPool.Users==0){foreach(var pose in crowdPool.Poses.Values){foreach(var mesh in pose.Meshes)mesh.Dispose();foreach(var draw in pose.Draws){draw.DisposeDistantMesh();if(!pose.Meshes.Contains(draw.Mesh))draw.Mesh.Dispose();}pose.Shadow?.Dispose();}foreach(var reduced in crowdPool.ReducedShadows.Values) reduced.Dispose();crowdPool.ReducedShadows.Clear();crowdPool.Poses.Clear();if(crowdModel!=null)CrowdPools.Remove(crowdModel);}
  crowdShadow=null;crowdDraws=null;crowdPool=null;crowdModel=null;
 }
}
