using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Runtime;

public sealed record BrowserQualityProfile(int ShadowResolution,int ReflectionSteps,int WorldWidth,int WorldHeight,int Anisotropy);
/// <summary>Versioned platform contract. Export, browser bootstrap, shader generation and regression fixtures consume the same definition.</summary>
public static class BrowserRenderContract
{
 public const int Version=2;
 // Every public material property requires an explicit backend decision. Adding a property must not silently change web output.
 public const string MaterialProperties="BaseColor WetContact0 WetContact1 WetContact2 WetLevels MainTexture NormalTexture HeightFieldTexture HeightFieldUvTransform HeightFieldRange HeightFieldBias HeightFieldWorldSize WaterDepthTexture WaterDepthRect WaterDepthSettings WaterOceanWaves WaterAbsorption WaterScattering WaterStyle WaterReflections WaterSurface WaterVolume WaterColumn WaterMotion WaterWake WaterFlow WaterImpact WaterSize SandSurface SandImprintTexture SandImprintOrigin NormalStrength DirectXNormalMap MetallicTexture RoughnessTexture AmbientOcclusionTexture AmbientOcclusionStrength PackedPbrTexture PbrMapMode PackedAoChannel PackedRoughnessChannel PackedMetallicChannel EmissionEnabled EmissionColor EmissionIntensity EmissionTexture UvTiling UvOffset Shading DecodeColorTexturesSrgb Metallic Roughness BlendMode AlphaCutoff DepthTest DepthWriteMode CullMode FrontFace PolygonMode";
 public static readonly IReadOnlyDictionary<string,BrowserQualityProfile> Profiles=new Dictionary<string,BrowserQualityProfile>{["Fast"]=new(0,0,1280,720,1),["Balanced"]=new(1024,10,1920,1080,4),["High"]=new(2048,20,2560,1440,8)};
 public static BrowserQualityProfile Profile(GraphicsQuality quality)=>Profiles.GetValueOrDefault(quality.ToString(),Profiles["Balanced"]);
 public static void ValidateCoverage(){var known=MaterialProperties.Split(' ').ToHashSet(StringComparer.Ordinal);var actual=typeof(Material).GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.CanWrite).Select(p=>p.Name).ToHashSet(StringComparer.Ordinal);if(!known.SetEquals(actual))throw new InvalidOperationException("Browser material contract requires review: "+string.Join(", ",actual.Except(known).Select(n=>"unclassified "+n).Concat(known.Except(actual).Select(n=>"removed "+n))));}
 public static string Hash {get;}=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Version+"\n"+MaterialProperties+"\n"+PortableWaterShader.Source+"\n"+JsonSerializer.Serialize(Profiles)))).ToLowerInvariant();
 public static void PrepareRuntime(string root){
  ValidateCoverage();Directory.CreateDirectory(root);
  WriteSharedSource(root);
  var files=new[]{"shared-render.js","renderer.js","water.js","graphics.js","main.js","content.js","audio.js","frame-wire.js","performance.js","_framework/blazor.boot.json"}.ToDictionary(n=>n,n=>FileHash(Path.Combine(root,n)));
  File.WriteAllText(Path.Combine(root,"browser-render-contract.json"),JsonSerializer.Serialize(new{version=Version,contractHash=Hash,coreBuild=SourceFingerprint,files},new JsonSerializerOptions{WriteIndented=true}));
 }
 public static void WriteSharedSource(string root){ValidateCoverage();Directory.CreateDirectory(root);
  File.WriteAllText(Path.Combine(root,"shared-render.js"),"// Generated from ByteEngine.Core. Edit the shared engine definition, then publish.\nexport const renderContract="+JsonSerializer.Serialize(Hash)+";\nexport const waterMath="+JsonSerializer.Serialize(PortableWaterShader.Source)+";\nexport const qualityProfiles="+JsonSerializer.Serialize(Profiles)+";\n");
 }
 public static void ValidateRuntime(string root){
  ValidateCoverage();string file=Path.Combine(root,"browser-render-contract.json");if(!File.Exists(file))throw new InvalidOperationException("Browser runtime has no render contract. Publish the browser runtime before exporting.");
  using var doc=JsonDocument.Parse(File.ReadAllText(file));var e=doc.RootElement;if(e.GetProperty("version").GetInt32()!=Version||e.GetProperty("contractHash").GetString()!=Hash)throw new InvalidOperationException("Browser renderer and editor use different material/shader contracts. Refresh the browser runtime before exporting.");
  if(!e.TryGetProperty("coreBuild",out var core)||core.GetString()!=SourceFingerprint)throw new InvalidOperationException("Browser runtime was built from a different engine source revision. Publish the matching browser runtime before exporting.");
  foreach(var entry in e.GetProperty("files").EnumerateObject()){string path=GamePackageExporter.ResolveInside(root,entry.Name);if(!File.Exists(path)||FileHash(path)!=entry.Value.GetString())throw new InvalidOperationException("Browser runtime is mixed or stale: "+entry.Name+". Publish the complete runtime again.");}
 }
 public static string SourceFingerprint=>typeof(BrowserRenderContract).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="ByteEngineSourceFingerprint").Value!;
 static string FileHash(string file)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
}


