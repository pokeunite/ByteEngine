using System.Collections.Concurrent;
namespace ByteEngine.Core.Runtime;
/// <summary>Per-game writable saves, separate from temporary packaged content. Editor projects retain local Saves.</summary>
public static class GameSaveStorage
{
 static readonly ConcurrentDictionary<string,string> Roots=new(StringComparer.OrdinalIgnoreCase);
 public static string GetDirectory(string projectRoot)=>Roots.TryGetValue(Path.GetFullPath(projectRoot),out var path)?path:Path.Combine(projectRoot,"Saves");
 public static bool IsRuntimeProject(string projectRoot)=>Roots.ContainsKey(Path.GetFullPath(projectRoot));
 internal static string Register(string root,Guid projectId){if(projectId==Guid.Empty)throw new InvalidDataException("A runtime game requires a stable project ID for saves.");string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ByteEngine","Games",projectId.ToString("N"),"Saves");Directory.CreateDirectory(path);Roots[Path.GetFullPath(root)]=path;return path;}
 internal static void Unregister(string root)=>Roots.TryRemove(Path.GetFullPath(root),out _);
}
