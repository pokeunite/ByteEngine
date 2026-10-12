using System.Security.Cryptography;
using System.Text.Json;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Runtime;
namespace ByteEngine.Tests;
internal static partial class BrowserChunkTests
{
 static void CheckPluginFreshness(string root)
 {
  string dir=Path.Combine(root,"plugin-freshness");Directory.CreateDirectory(dir);
  string manifest=Path.Combine(dir,"plugin.json"),assembly=Path.Combine(dir,"Test.dll"),builds=Path.Combine(dir,"browser-plugin-builds.json");
  File.WriteAllText(manifest,"{\"schemaVersion\":1,\"id\":\"test.plugin\",\"name\":\"Test\",\"version\":\"1.0.0\",\"assembly\":\"Test.dll\",\"entryPoint\":\"Test.Entry\",\"runtime\":true}");
  File.WriteAllText(assembly,"current plugin");
  var plugin=new InstalledByteEnginePlugin("test.plugin","Test","1.0.0",dir,manifest,true);
  void Reject(){try{WebGamePackageExporter.ValidatePluginBuilds(dir,[plugin]);}catch(InvalidOperationException){return;}throw new Exception("Stale browser plugin was accepted");}
  Reject();
  File.WriteAllText(builds,JsonSerializer.Serialize(new Dictionary<string,object>{{plugin.Id,new{version=plugin.Version,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly)))}}}));
  WebGamePackageExporter.ValidatePluginBuilds(dir,[plugin]);
  plugin=plugin with { Version="1.0.1" };Reject();
  Console.WriteLine("PASS Browser plugin freshness: missing metadata rejected, matching code accepted, newer project plugin rejected");
 }
}
