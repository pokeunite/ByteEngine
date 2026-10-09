using System.Text.Json;
namespace ByteEngine.Core.Assets.Importers;
public static class ModelImportDependencies
{
 public static IReadOnlyList<string> Discover(string source)
 {
  var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);string directory=Path.GetDirectoryName(Path.GetFullPath(source))!;
  void Add(string uri){if(uri.StartsWith("data:",StringComparison.OrdinalIgnoreCase))return;string path=Path.GetFullPath(Path.Combine(directory,Uri.UnescapeDataString(uri)));if(File.Exists(path))files.Add(path);}
  if(Path.GetExtension(source).Equals(".gltf",StringComparison.OrdinalIgnoreCase))
  {using var json=JsonDocument.Parse(File.ReadAllText(source));foreach(string group in new[]{"buffers","images"})if(json.RootElement.TryGetProperty(group,out var array))foreach(var item in array.EnumerateArray())if(item.TryGetProperty("uri",out var uri))Add(uri.GetString()??"");}
  else if(Path.GetExtension(source).Equals(".obj",StringComparison.OrdinalIgnoreCase))
  {foreach(string line in File.ReadLines(source))if(line.StartsWith("mtllib "))Add(line[7..].Trim());foreach(string mtl in files.ToArray())foreach(string line in File.ReadLines(mtl))if(line.StartsWith("map_")&&line.IndexOf(' ') is int i&&i>0){string path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(mtl)!,line[(i+1)..].Trim()));if(File.Exists(path))files.Add(path);}}
  return files.Order(StringComparer.OrdinalIgnoreCase).ToArray();
 }
}
