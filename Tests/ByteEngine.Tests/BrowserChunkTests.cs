using ByteEngine.Core.Runtime;
namespace ByteEngine.Tests;
internal static class BrowserChunkTests{
 public static void Run(string root){var file=Path.Combine(root,"test.bytepak");var bytes=new byte[17*1024*1024+29];new Random(123).NextBytes(bytes);File.WriteAllBytes(file,bytes);var parts=WebGamePackageExporter.SplitBrowserPackage(file);if(parts.Length!=3||File.Exists(file))throw new Exception("Chunk count or original removal wrong");int at=0;foreach(var part in parts){var data=File.ReadAllBytes(part);if(data.Length>8*1024*1024||!data.AsSpan().SequenceEqual(bytes.AsSpan(at,data.Length)))throw new Exception("Chunk bytes corrupt");at+=data.Length;}if(at!=bytes.Length)throw new Exception("Chunk boundary loss");Console.WriteLine("PASS Browser package chunk boundaries, reassembly equality and original removal");}
}
