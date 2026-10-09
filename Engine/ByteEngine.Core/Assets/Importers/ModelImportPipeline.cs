using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Assets.Importers;
/// <summary>CPU-only post-import processing; generated static LODs share source materials and never change collision.</summary>
public static class ModelImportPipeline
{
 public static ImportedModel Import(AssetRecord record,ModelImporterSettings settings)
 {
  var model=ModelImporter.ForPath(record.FullPath).Import(record,settings);if(!settings.GenerateLods)return model;
  foreach(var source in model.Meshes.ToArray())
  {
   if(source.JointWeights.Length!=0||source.Vertices.Length<96)continue;
   using var mesh=new Mesh(source.Vertices,source.Indices);
   for(int i=1;i<3;i++){using var reduced=MeshSimplifier.Simplify(mesh,i==1?.5f:.2f);model.Meshes.Add(new ImportedMesh{Key=source.Key+".lod"+i,Name=source.Name+" LOD "+i,MaterialKey=source.MaterialKey,Vertices=reduced.VertexData.ToArray(),Indices=reduced.IndexData.ToArray()});}
  }
  return model;
 }
}
