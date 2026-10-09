using System.Text.Json;
namespace ByteEngine.Core.Runtime;
public enum RuntimePlatform { Windows, Browser }
public readonly record struct CapabilityIssue(string Component,string Message,bool BlocksExport);
/// <summary>Shared authoring/export capability policy. Unknown plugin support is validated separately against the runtime manifest.</summary>
public static class PlatformCapabilities
{
 public static IReadOnlyList<CapabilityIssue> Inspect(string path,RuntimePlatform platform)
 {
  return InspectJson(File.ReadAllText(path),platform);
 }
 public static IReadOnlyList<CapabilityIssue> InspectJson(string json,RuntimePlatform platform)
 {
  if(platform==RuntimePlatform.Windows)return Array.Empty<CapabilityIssue>();
  using var document=JsonDocument.Parse(json);var issues=new List<CapabilityIssue>();Visit(document.RootElement);return issues.Distinct().ToArray();
  void Visit(JsonElement node){if(node.ValueKind==JsonValueKind.Array){foreach(var child in node.EnumerateArray())Visit(child);return;}if(node.ValueKind!=JsonValueKind.Object)return;
   foreach(var property in node.EnumerateObject()){if(property.Name.Equals("type",StringComparison.OrdinalIgnoreCase)&&property.Value.ValueKind==JsonValueKind.String){string type=property.Value.GetString()!;if(type is "SpriteRenderer" or "ArenaGameManager")issues.Add(new(type,"Browser renderer does not support "+type,true));if(type is "PointLight")issues.Add(new(type,"Browser point lights have no shadow maps",false));}Visit(property.Value);}
  }
 }
}
