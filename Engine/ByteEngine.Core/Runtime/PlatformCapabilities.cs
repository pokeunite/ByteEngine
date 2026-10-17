using System.Text.Json;
namespace ByteEngine.Core.Runtime;
public enum RuntimePlatform { Windows, Browser }
public readonly record struct CapabilityIssue(string Component,string Message,bool BlocksExport);
/// <summary>Explicit browser feature policy. Quality profiles may reduce cost, never silently omit required scene features.</summary>
public static class PlatformCapabilities
{
 public static IReadOnlyList<CapabilityIssue> Inspect(string path,RuntimePlatform platform)=>InspectJson(File.ReadAllText(path),platform);
 public static IReadOnlyList<CapabilityIssue> InspectJson(string json,RuntimePlatform platform){
  if(platform==RuntimePlatform.Windows)return [];
  using var document=JsonDocument.Parse(json);var issues=new List<CapabilityIssue>();Visit(document.RootElement);return issues.Distinct().ToArray();
  void Add(string type,string message,bool blocked=false)=>issues.Add(new(type,message,blocked));
  void Visit(JsonElement node){
   if(node.ValueKind==JsonValueKind.Array){foreach(var item in node.EnumerateArray())Visit(item);return;}if(node.ValueKind!=JsonValueKind.Object)return;
   if(node.TryGetProperty("type",out var t)&&t.ValueKind==JsonValueKind.String){string type=t.GetString()!;var p=node.TryGetProperty("properties",out var props)?props:node;
    bool Flag(string key,bool fallback=false)=>p.TryGetProperty(key,out var v)&&v.ValueKind is JsonValueKind.True or JsonValueKind.False?v.GetBoolean():fallback;
    string Text(string key)=>p.TryGetProperty(key,out var v)?v.ToString():"";
    if(type is "SpriteRenderer" or "ArenaGameManager")Add(type,"Browser renderer does not support "+type+".",true);
    if(type=="PointLight"&&Flag("castShadows"))Add(type,"Point-light illumination is supported; point-light shadow maps are unavailable.");
    if(type=="SkyEnvironment"){
     if(Flag("environmentLightingEnabled"))Add(type,"Environment-map image lighting differs from desktop cubemap IBL; compare the browser preview.");
     if(p.TryGetProperty("graphicsLook",out var look)){foreach(string field in new[]{"ao","bloom"})if(look.TryGetProperty(field,out var value)&&value.TryGetDouble(out double strength)&&strength>0)Add(type,field=="ao"?"Screen-space ambient occlusion is unavailable; material AO textures remain supported.":"Bloom is unavailable; emissive materials remain visible.");}
    }
    if(type is "MeshRenderer" or "SkeletalMeshRenderer"){
     if(Text("polygonMode") is "Line" or "Point" or "Wireframe")Add(type,"Wireframe/point polygon rendering has no browser implementation.",true);
    }
   }
   if(node.TryGetProperty("sandSurface",out var sand)&&sand.ValueKind==JsonValueKind.True)Add("Material","Procedural desktop sand shading is unavailable; sand imprint deformation alone is supported.",true);
   foreach(var property in node.EnumerateObject())Visit(property.Value);
  }
 }
}
public sealed record WebCompatibilityIssue(string Asset,string Component,string Message,bool BlocksExport);
