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
   var group=material.BlendMode==BlendMode3D.Opaque?groups.FirstOrDefault(g=>g.Count<16&&_runtimeMeshes[g[0]].Material.BlendMode==BlendMode3D.Opaque&&(MaterialRenderStateComparer.Instance.Equals(_runtimeMeshes[g[0]].Material,material)||CompatibleFlatColors(_runtimeMeshes[g[0]].Material,material))):null;
   if(group==null){group=[];groups.Add(group);}group.Add(i);
  }
  var draws=new List<CrowdDraw>();
  foreach(var group in groups)
  {
   if(group.Count==1){int i=group[0];draws.Add(new(meshes[i],placement[i],i));continue;}
   bool palette=group.Any(i=>_runtimeMeshes[i].Material.BaseColor!=_runtimeMeshes[group[0]].Material.BaseColor);
   Material? paletteMaterial=null;
   if(palette){
    string key=string.Join(',',group);
    if(crowdPool==null||!crowdPool.FlatMaterials.TryGetValue(key,out paletteMaterial)){
     paletteMaterial=_runtimeMeshes[group[0]].Material.Clone();paletteMaterial.BaseColor=Vector4.One;
     paletteMaterial.SolidColorPalette=group.Select(i=>_runtimeMeshes[i].Material.BaseColor).ToArray();
     if(crowdPool!=null)crowdPool.FlatMaterials[key]=paletteMaterial;
    }
   }
   draws.Add(new(MergeCrowdGeometryCore(meshes,placement,group,palette),Matrix4x4.Identity,group[0],paletteMaterial));
  }
  return draws.ToArray();
 }
 Mesh MergeCrowdGeometry(Mesh[] meshes,Matrix4x4[] placement,IReadOnlyList<int> group)
  =>MergeCrowdGeometryCore(meshes,placement,group,false);
 static bool CompatibleFlatColors(Material a,Material b)
 {
  static bool Flat(Material m)=>m.SolidColorPalette==null&&m.MainTexture==null&&m.NormalTexture==null&&m.MetallicTexture==null&&m.RoughnessTexture==null&&m.AmbientOcclusionTexture==null&&m.PackedPbrTexture==null&&m.EmissionTexture==null&&m.HeightFieldTexture==null&&m.SandImprintTexture==null&&!m.WaterSurface&&!m.WaterVolume&&!m.SandSurface;
  if(!Flat(a)||!Flat(b))return false;
  var left=a.Clone();var right=b.Clone();left.BaseColor=right.BaseColor=Vector4.One;
  return MaterialRenderStateComparer.Instance.Equals(left,right);
 }
 Mesh MergeCrowdGeometryCore(Mesh[] meshes,Matrix4x4[] placement,IReadOnlyList<int> group,bool palette)
 {
   int floatCount=0,indexCount=0;
   foreach(int i in group){floatCount+=meshes[i].VertexData.Length;indexCount+=meshes[i].IndexData.Length;}
   var vertices=new float[floatCount];var indices=new uint[indexCount];int at=0,ix=0;
   for(int colorIndex=0;colorIndex<group.Count;colorIndex++)
   {
    int i=group[colorIndex];
    var source=meshes[i].VertexData.Span;uint offset=(uint)(at/8);
    Matrix4x4.Invert(placement[i],out var inverse);var normalMatrix=Matrix4x4.Transpose(inverse);
    for(int v=0;v<source.Length;v+=8)
    {
     var position=Vector3.Transform(new(source[v],source[v+1],source[v+2]),placement[i]);
     var normal=Vector3.TransformNormal(new(source[v+3],source[v+4],source[v+5]),normalMatrix);if(normal.LengthSquared()>1e-12f)normal=Vector3.Normalize(normal);
     vertices[at++]=position.X;vertices[at++]=position.Y;vertices[at++]=position.Z;
     vertices[at++]=normal.X;vertices[at++]=normal.Y;vertices[at++]=normal.Z;vertices[at++]=palette?colorIndex:source[v+6];vertices[at++]=palette?0:source[v+7];
    }
    foreach(uint index in meshes[i].IndexData.Span)indices[ix++]=index+offset;
   }
   return new Mesh(vertices,indices,true);
 }

 bool SubmitCrowdBatches(ByteEngine.Core.Graphics.RenderContext context,Matrix4x4 world,Material? modelOverride)
 {
  if(modelOverride?.HeightFieldTexture!=null||modelOverride is {BlendMode:not BlendMode3D.Opaque}||!BatchCrowdSubmeshes||!CrowdEligible||crowdDraws==null)return false;
  foreach(var asset in MaterialAssetSlots)if(asset is {IsEmpty:false})return false;
  foreach(var state in _materialOverrides.Values)if(!state.IsEmpty)return false;
  if(modelOverride!=null&&crowdDraws.Any(d=>d.PaletteMaterial!=null))return false;
  bool first=true;
  foreach(var draw in crowdDraws)
  {
   var material=modelOverride??draw.PaletteMaterial??_runtimeMeshes[draw.Slot].Material;
   context.RenderWorld.SubmitWithShadow(draw.Mesh,material,draw.Placement*world,ResolveRenderQueue(material),true,context.FrameSink!=null||crowdShadow==null||first,true,shadowMesh:context.FrameSink==null&&first?crowdShadow:null,shadowModelMatrix:world);first=false;
  }
  return true;
 }
}
