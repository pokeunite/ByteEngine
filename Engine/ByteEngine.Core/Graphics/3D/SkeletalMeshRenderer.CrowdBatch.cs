using System.Numerics;
namespace ByteEngine.Core.Graphics.ThreeD;
public sealed partial class SkeletalMeshRenderer
{
 CrowdDraw[] BuildCrowdDraws(Mesh[] meshes,Matrix4x4[] placement)
 {
  if(placement.Any(m=>m.GetDeterminant()<=.000001f))return meshes.Select((m,i)=>new CrowdDraw(m,placement[i],i)).ToArray();
  var groups=new List<List<int>>();
  for(int i=0;i<meshes.Length;i++)
  {
   var material=_runtimeMeshes[i].Material;
   var group=material.BlendMode==BlendMode3D.Opaque?groups.FirstOrDefault(g=>_runtimeMeshes[g[0]].Material.BlendMode==BlendMode3D.Opaque&&MaterialRenderStateComparer.Instance.Equals(_runtimeMeshes[g[0]].Material,material)):null;
   if(group==null){group=[];groups.Add(group);}group.Add(i);
  }
  var draws=new List<CrowdDraw>();
  foreach(var group in groups)
  {
   if(group.Count==1){int i=group[0];draws.Add(new(meshes[i],placement[i],i));continue;}
   draws.Add(new(MergeCrowdGeometry(meshes,placement,group),Matrix4x4.Identity,group[0]));
  }
  return draws.ToArray();
 }
 Mesh MergeCrowdGeometry(Mesh[] meshes,Matrix4x4[] placement,IReadOnlyList<int> group)
 {
   var vertices=new List<float>();var indices=new List<uint>();
   foreach(int i in group)
   {
    var source=meshes[i].VertexData.Span;uint offset=(uint)(vertices.Count/8);
    Matrix4x4.Invert(placement[i],out var inverse);var normalMatrix=Matrix4x4.Transpose(inverse);
    for(int v=0;v<source.Length;v+=8)
    {
     var position=Vector3.Transform(new(source[v],source[v+1],source[v+2]),placement[i]);
     var normal=Vector3.TransformNormal(new(source[v+3],source[v+4],source[v+5]),normalMatrix);if(normal.LengthSquared()>1e-12f)normal=Vector3.Normalize(normal);
     vertices.AddRange([position.X,position.Y,position.Z,normal.X,normal.Y,normal.Z,source[v+6],source[v+7]]);
    }
    foreach(uint index in meshes[i].IndexData.Span)indices.Add(index+offset);
   }
   return new Mesh(vertices.ToArray(),indices.ToArray(),true);
 }
 bool SubmitCrowdBatches(ByteEngine.Core.Graphics.RenderContext context,Matrix4x4 world,Material? modelOverride)
 {
  if(context.FrameSink!=null||modelOverride?.HeightFieldTexture!=null||modelOverride is {BlendMode:not BlendMode3D.Opaque}||!BatchCrowdSubmeshes||!CrowdEligible||crowdDraws==null)return false;
  foreach(var asset in MaterialAssetSlots)if(asset is {IsEmpty:false})return false;
  foreach(var state in _materialOverrides.Values)if(!state.IsEmpty)return false;
  bool first=true;
  foreach(var draw in crowdDraws)
  {
   var material=modelOverride??_runtimeMeshes[draw.Slot].Material;
   context.RenderWorld.SubmitWithShadow(draw.Mesh,material,draw.Placement*world,ResolveRenderQueue(material),true,crowdShadow==null||first,true,shadowMesh:first?crowdShadow:null,shadowModelMatrix:world);first=false;
  }
  return true;
 }
}
