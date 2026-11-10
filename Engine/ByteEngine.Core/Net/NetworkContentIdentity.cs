using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ByteEngine.Core.Plugins;
namespace ByteEngine.Core.Net;
/// <summary>Actual enabled runtime-plugin content identity; web exports carry the validated native manifest.</summary>
public static class NetworkContentIdentity
{
    public const string BrowserManifest=".byteengine/network-plugins.json";
    public static string Plugins(string? root)
    {
        if(string.IsNullOrEmpty(root))return "";
        if(OperatingSystem.IsBrowser()){
            string manifest=Path.Combine(root,BrowserManifest);
            return File.Exists(manifest)?JsonSerializer.Deserialize<string>(File.ReadAllText(manifest))??"":"";
        }
        var text=new StringBuilder();
        foreach(var plugin in ByteEnginePluginPackageManager.ListInstalled(root).Where(p=>p.Enabled).OrderBy(p=>p.Id,StringComparer.Ordinal)){
            var manifest=ByteEnginePluginPackageManager.ReadManifest(File.ReadAllText(plugin.ManifestPath));if(!manifest.Runtime)continue;
            string assembly=Runtime.GamePackageExporter.ResolveInside(Path.GetDirectoryName(plugin.ManifestPath)!,manifest.Assembly);
            text.Append(plugin.Id).Append('\n').Append(plugin.Version).Append('\n').Append(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly)))).Append('\n');
        }
        return text.ToString();
    }
}
