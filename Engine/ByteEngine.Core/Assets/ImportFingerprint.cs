using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace ByteEngine.Core.Assets;
/// <summary>Versioned cache identity includes content, importer settings and dependencies.</summary>
public static class ImportFingerprint
{
 public const int Version=2;
 public static string Create(string source,object settings,IEnumerable<string>? dependencies=null)
 {
  using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  hash.AppendData(Encoding.UTF8.GetBytes("ByteEngine-import-v"+Version+JsonSerializer.Serialize(settings)));
  foreach(string path in new[]{source}.Concat(dependencies??Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
  {hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).Replace('\\','/')+"\0"));if(!File.Exists(path)){hash.AppendData("missing\0"u8);continue;}using var stream=File.OpenRead(path);hash.AppendData(BitConverter.GetBytes(stream.Length));byte[] bytes=new byte[65536];int count;while((count=stream.Read(bytes))>0)hash.AppendData(bytes.AsSpan(0,count));}
  return Convert.ToHexString(hash.GetHashAndReset());
 }
}
