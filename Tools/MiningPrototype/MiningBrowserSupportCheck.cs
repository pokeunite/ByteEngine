using System.Reflection;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Runtime;
internal static class MiningBrowserSupportCheck
{
 public static void Run(GameProjectRuntime project)
 {
  string runtime="C:/Users/codex/ByteEngine/Dist/ByteEngine/BrowserRuntime";var supported=JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(runtime,"browser-plugins.json")))!;var plugins=ByteEnginePluginPackageManager.ListInstalled(project.Root).Where(p=>p.Enabled&&ByteEnginePluginPackageManager.ReadManifest(File.ReadAllText(p.ManifestPath)).Runtime).ToArray();
  foreach(var plugin in plugins)if(!supported.Contains(plugin.Id,StringComparer.OrdinalIgnoreCase))throw new Exception("Missing linked plugin: "+plugin.Id);
  typeof(WebGamePackageExporter).GetMethod("ValidatePluginBuilds",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[runtime,plugins]);Console.WriteLine("PASS WEB exporter compatibility guard accepts every enabled project plugin");
  using var builds=JsonDocument.Parse(File.ReadAllText(Path.Combine(runtime,"browser-plugin-builds.json")));
  foreach(var plugin in plugins){var manifest=ByteEnginePluginPackageManager.ReadManifest(File.ReadAllText(plugin.ManifestPath));using var zip=ZipFile.OpenRead(plugin.PackagePath!);using var data=zip.GetEntry(manifest.Assembly)!.Open();string hash=Convert.ToHexString(SHA256.HashData(data));if(builds.RootElement.GetProperty(plugin.Id).GetProperty("sha256").GetString()!=hash)throw new Exception("Compiled plugin fingerprint differs: "+plugin.Id);Console.WriteLine("PASS WEB compiled DLL fingerprint matches "+plugin.Id+" "+plugin.Version);}
  var records=project.Database.Assets.Where(a=>a.Type is AssetType.Scene or AssetType.Blueprint).ToArray();foreach(var record in records){var issues=PlatformCapabilities.Inspect(record.FullPath,RuntimePlatform.Browser).Where(i=>i.BlocksExport).ToArray();if(issues.Length>0)throw new Exception(record.ProjectPath+": "+string.Join("; ",issues.Select(i=>i.Message)));}Console.WriteLine("PASS WEB "+records.Length+" scene/blueprint assets pass browser capability checks; no game exported");
 }
}
