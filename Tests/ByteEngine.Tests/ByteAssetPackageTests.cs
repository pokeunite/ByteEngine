using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ByteEngine.Core.Runtime;

namespace ByteEngine.Tests;

internal static class ByteAssetPackageTests
{
    public static void Run(string root)
    {
        string source = Path.Combine(root, "Package Source");
        Directory.CreateDirectory(Path.Combine(source, "Assets", "nested"));
        byte[] payload = Encoding.UTF8.GetBytes("Texture/model/audio payload " + new string('x', 5000));
        File.WriteAllBytes(Path.Combine(source, "Assets", "nested", "test.bin"), payload);
        File.WriteAllText(Path.Combine(source, "Game.byteproject"), "{}");
        string package = Path.Combine(root, "test.bytepak");
        ByteAssetPackage.Create(source, package);
        string output = Path.Combine(root, "Extracted");
        var paths = ByteAssetPackage.Extract(package, output);
        if (paths.Count != 2 || !File.ReadAllBytes(Path.Combine(output, "Assets", "nested", "test.bin")).SequenceEqual(payload))
            throw new Exception("Packed content must round-trip byte-for-byte.");
        bool repeatedRejected = false;
        try { ByteAssetPackage.Extract(package, output); } catch (IOException) { repeatedRejected = true; }
        if (!repeatedRejected) throw new Exception("Extraction must not overwrite existing files.");
        using var malicious = new MemoryStream();
        using (var writer = new BinaryWriter(malicious, Encoding.UTF8, true))
        {
            writer.Write("BYTEPAK2"u8); writer.Write(1); writer.Write("../escape.bin");
        }
        malicious.Position = 0;
        ExpectInvalid(() => ByteAssetPackage.Extract(malicious, Path.Combine(root, "Unsafe")));
        using var badHash = new MemoryStream();
        using (var writer = new BinaryWriter(badHash, Encoding.UTF8, true))
        {
            using var compressed = new MemoryStream();
            using (var compressor = new ZLibStream(compressed, CompressionLevel.Optimal, true)) compressor.Write(payload);
            writer.Write("BYTEPAK2"u8); writer.Write(1); writer.Write("bad.bin"); writer.Write((long)payload.Length);
            writer.Write(new byte[32]); writer.Write(compressed.Length); writer.Write(compressed.ToArray());
        }
        badHash.Position = 0;
        ExpectInvalid(() => ByteAssetPackage.Extract(badHash, Path.Combine(root, "Corrupt")));
        Console.WriteLine("Byte asset package round-trip, overwrite protection, path traversal and integrity checks passed.");
    }

    private static void ExpectInvalid(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("Invalid package should be rejected.");
    }
}
