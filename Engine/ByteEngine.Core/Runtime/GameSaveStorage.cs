using System.Collections.Concurrent;
namespace ByteEngine.Core.Runtime;
/// <summary>Writable saves scoped to a project and, for new exports, one build identity.</summary>
public static class GameSaveStorage
{
 internal static void AssignExportIdentity(ByteEngine.Core.Serialization.SerializationModels.ProjectData project)=>project.RuntimeSaveId=Guid.NewGuid();
 static readonly ConcurrentDictionary<string,string> Roots=new(StringComparer.OrdinalIgnoreCase);
 public static string GetDirectory(string projectRoot)=>Roots.TryGetValue(Path.GetFullPath(projectRoot),out var path)?path:Path.Combine(projectRoot,"Saves");
 public static bool IsRuntimeProject(string projectRoot)=>Roots.ContainsKey(Path.GetFullPath(projectRoot));
 internal static string Register(string root,Guid projectId,Guid? runtimeSaveId=null)
 {
  if(projectId==Guid.Empty)throw new InvalidDataException("A runtime game requires a stable project ID for saves.");
  string gameRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ByteEngine","Games",projectId.ToString("N"));
  if(runtimeSaveId is {} id && id!=Guid.Empty)gameRoot=Path.Combine(gameRoot,"Builds",id.ToString("N"));
  string path=Path.Combine(gameRoot,"Saves");Directory.CreateDirectory(path);Roots[Path.GetFullPath(root)]=path;return path;
 }
 internal static void Unregister(string root)=>Roots.TryRemove(Path.GetFullPath(root),out _);
}
