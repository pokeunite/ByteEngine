using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
namespace ByteEngine.Tests;
internal static class ExportLifecycleTests
{
 public static void Run(string workspace)
 {
  string root=Path.Combine(workspace,".artifacts","export-lifecycle",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var project=new ProjectData{Name="Lifecycle fixture"};string file=Path.Combine(root,"fixture.byteproject");new ProjectSerializer().Save(project,file);
  var first=new ProjectSerializer().Load(file);var second=new ProjectSerializer().Load(file);GameSaveStorage.AssignExportIdentity(first);GameSaveStorage.AssignExportIdentity(second);
  if(first.ProjectId!=second.ProjectId||first.RuntimeSaveId==second.RuntimeSaveId||new ProjectSerializer().Load(file).RuntimeSaveId!=null)throw new Exception("Export identities leaked into the authoring project or reused.");
  string a=GameSaveStorage.Register(Path.Combine(root,"A"),project.ProjectId,first.RuntimeSaveId),b=GameSaveStorage.Register(Path.Combine(root,"B"),project.ProjectId,second.RuntimeSaveId);
  try{
   File.WriteAllText(Path.Combine(a,"tow-progress-v1.json"),"completed");File.WriteAllText(Path.Combine(a,"recovery-payment.json"),"750");
   if(a==b||File.Exists(Path.Combine(b,"tow-progress-v1.json")))throw new Exception("New builds inherited completion.");GameSaveStorage.Unregister(Path.Combine(root,"A"));if(GameSaveStorage.Register(Path.Combine(root,"A2"),project.ProjectId,first.RuntimeSaveId)!=a||File.ReadAllText(Path.Combine(a,"recovery-payment.json"))!="750")throw new Exception("Same build lost saves on restart.");
   Directory.CreateDirectory(Path.Combine(a,"Blueprints"));File.WriteAllText(Path.Combine(a,"Blueprints","saved.json"),"vehicle");File.WriteAllText(Path.Combine(a,"menu-settings.json"),"settings");
   typeof(DuneCompany.DuneMainMenu3D).Assembly.GetType("DuneCompany.DuneCampaignSaves")!.GetMethod("Reset",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,[a]);
   if(File.Exists(Path.Combine(a,"tow-progress-v1.json"))||File.Exists(Path.Combine(a,"recovery-payment.json"))||!File.Exists(Path.Combine(a,"Blueprints","saved.json"))||!File.Exists(Path.Combine(a,"menu-settings.json")))throw new Exception("Campaign reset did not isolate progress from designs/preferences.");
  }finally{foreach(var pair in new[]{("A",a),("A2",a),("B",b)})GameSaveStorage.Unregister(Path.Combine(root,pair.Item1));foreach(var dir in new[]{a,b}){string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ByteEngine","Games",project.ProjectId.ToString("N"));if(!dir.StartsWith(expected+Path.DirectorySeparatorChar))throw new Exception("Unexpected test save root");Directory.Delete(Path.GetDirectoryName(dir)!,true);}}
  Console.WriteLine("PASS Separate builds start fresh; same build retains saves; campaign reset keeps blueprints/preferences.");
  foreach(var value in new[]{Vector4.One,new Vector4(.12f,.34f,.56f,.78f),new Vector4(2,3,4,5)}){var text=JsonSerializer.Serialize(value,JsonSerialization.Options);var restored=JsonSerializer.Deserialize<Vector4>(text,JsonSerialization.Options);if(restored.X!=value.X||restored.Y!=value.Y||restored.Z!=value.Z||restored.W!=value.W)throw new Exception("Four-channel colour changed on serialization");}
  if(JsonSerializer.Deserialize<Vector4>("{\"X\":0.1,\"Y\":0.2,\"Z\":0.3,\"W\":0.4}",JsonSerialization.Options)!=new Vector4(.1f,.2f,.3f,.4f))throw new Exception("Legacy uppercase colour fields failed");
  Console.WriteLine("PASS Explicit four-channel colour reads/writes, including alpha and legacy field casing.");

  var texture=new ImportedTexture{Key="shared",Name="fixture",EncodedData=[1,2,3,4]};var model=new ImportedModel{Guid=Guid.NewGuid(),Name="binary fixture",Nodes=[new(){Key="root",MeshKeys=["mesh"],LocalTransform=Matrix4x4.CreateTranslation(1,2,3)}],Meshes=[new(){Key="mesh",MaterialKey="material",Vertices=[0,0,0,0,1,0,0,0, 1,0,0,0,1,0,1,0, 0,0,1,0,1,0,0,1],Indices=[0,1,2],JointIndices=[Vector4.One],JointWeights=[new(.25f)]}],Materials=[new(){Key="material",BaseColor=new(.2f,.3f,.4f,1),Metallic=.3f,Roughness=.7f,BaseColorTexture=texture,NormalTexture=texture}]};
  CookedModelStore.Save(root,model);var loaded=CookedModelStore.Load(root,model.Guid);
  if(!model.Meshes[0].Vertices.SequenceEqual(loaded.Meshes[0].Vertices)||!model.Meshes[0].Indices.SequenceEqual(loaded.Meshes[0].Indices)||!model.Meshes[0].JointWeights.SequenceEqual(loaded.Meshes[0].JointWeights)||model.Nodes[0].LocalTransform!=loaded.Nodes[0].LocalTransform||model.Materials[0].BaseColor!=loaded.Materials[0].BaseColor||loaded.Materials[0].BaseColorTexture?.CookedContentHash!=loaded.Materials[0].NormalTexture?.CookedContentHash||Directory.GetFiles(Path.Combine(root,".byteengine","WebTextures")).Length!=1)throw new Exception("Binary model roundtrip or shared textures failed.");
  var path=CookedModelStore.PathFor(root,model.Guid);File.WriteAllBytes(path,[1,2]);try{CookedModelStore.Load(root,model.Guid);throw new Exception("Corruption accepted");}catch(InvalidDataException){}File.Delete(path);File.WriteAllText(Path.ChangeExtension(path,"json"),JsonSerializer.Serialize(model,JsonSerialization.Options));if(CookedModelStore.Load(root,model.Guid).Meshes[0].Indices.Length!=3)throw new Exception("Legacy model fallback failed.");Console.WriteLine("PASS Binary geometry/materials/skinning/transforms; shared texture dedup; corruption rejection; legacy JSON loading.");
 }
}
