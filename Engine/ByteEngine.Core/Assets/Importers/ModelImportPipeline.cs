using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Assets.Importers;
/// <summary>CPU-only post-import processing; generated static LODs share source materials and never change collision.</summary>
public static class ModelImportPipeline
{
 public static ImportedModel Import(AssetRecord record,ModelImporterSettings settings)
 {
  var model=ModelImporter.ForPath(record.FullPath).Import(record,settings);
  if(settings.MaxTextureSize>0)
  {
   var textures=new Dictionary<ImportedTexture,ImportedTexture>();
   ImportedTexture? T(ImportedTexture? t){if(t==null||t.EncodedData.Length==0)return t;if(textures.TryGetValue(t,out var cached))return cached;var result=new ImportedTexture{Key=t.Key,Name=t.Name,SourcePath=t.SourcePath,EncodedData=WebTextureCooker.Optimize(t.EncodedData,settings.MaxTextureSize)};textures[t]=result;return result;}
   for(int i=0;i<model.Materials.Count;i++){var m=model.Materials[i];model.Materials[i]=new ImportedMaterial{Key=m.Key,Name=m.Name,BaseColor=m.BaseColor,Metallic=m.Metallic,Roughness=m.Roughness,SurfaceType=m.SurfaceType,AlphaCutoff=m.AlphaCutoff,DoubleSided=m.DoubleSided,Unlit=m.Unlit,AmbientOcclusionStrength=m.AmbientOcclusionStrength,EmissionColor=m.EmissionColor,BaseColorTexture=T(m.BaseColorTexture),NormalTexture=T(m.NormalTexture),MetallicRoughnessTexture=T(m.MetallicRoughnessTexture),MetallicTexture=T(m.MetallicTexture),RoughnessTexture=T(m.RoughnessTexture),AmbientOcclusionTexture=T(m.AmbientOcclusionTexture),EmissionTexture=T(m.EmissionTexture)};}
  }
  if(!settings.GenerateLods)return model;
  foreach(var source in model.Meshes.ToArray())
  {
   if(source.JointWeights.Length!=0||source.Vertices.Length<96)continue;
   using var mesh=new Mesh(source.Vertices,source.Indices);
   for(int i=1;i<3;i++){using var reduced=MeshSimplifier.Simplify(mesh,i==1?.5f:.2f);model.Meshes.Add(new ImportedMesh{Key=source.Key+".lod"+i,Name=source.Name+" LOD "+i,MaterialKey=source.MaterialKey,Vertices=reduced.VertexData.ToArray(),Indices=reduced.IndexData.ToArray()});}
  }
  return model;
 }
}
