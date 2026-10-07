using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ByteEngine.Core.Runtime;

/// <summary>Versioned, compressed game content. Packaging hides loose source files; it is not DRM.</summary>
public static class ByteAssetPackage
{
    public const string FileName = "Game.bytepak";
    private static readonly byte[] Magic = "BYTEPAK2"u8.ToArray();
    private const long MaxFileBytes = 1024L * 1024 * 1024;
    private const long MaxTotalBytes = 4L * 1024 * 1024 * 1024;

    public static void Create(string sourceDirectory, string packageFile)
    {
        string[] files = GamePackageExporter.WalkFiles(sourceDirectory).Order(StringComparer.Ordinal).ToArray();
        if (files.Length > 100000) throw new InvalidDataException("Too many package entries.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        using var stream = File.Create(packageFile);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(Magic);
        writer.Write(files.Length);
        foreach (string file in files)
        {
            string path = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
            ValidatePath(path);
            if (!paths.Add(path)) throw new InvalidDataException("Case-colliding package paths.");
            long length = new FileInfo(file).Length;
            if (length > MaxFileBytes || (total += length) > MaxTotalBytes)
                throw new InvalidDataException("Game content exceeds package size limits.");
            using var input = File.OpenRead(file);
            byte[] hash = SHA256.HashData(input);
            input.Position = 0;
            using var compressed = new MemoryStream();
            using (var compressor = new ZLibStream(compressed, CompressionLevel.Optimal, true))
                input.CopyTo(compressor);
            writer.Write(path);
            writer.Write(length);
            writer.Write(hash);
            writer.Write(compressed.Length);
            compressed.Position = 0;
            compressed.CopyTo(stream);
        }
    }

    public static IReadOnlyList<string> Extract(string packageFile, string destination)
    {
        using var stream = File.OpenRead(packageFile);
        return Extract(stream, destination);
    }

    public static IReadOnlyList<string> Extract(Stream stream, string destination)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Unsupported ByteEngine asset package.");
        int count = reader.ReadInt32();
        if (count < 1 || count > 100000) throw new InvalidDataException("Invalid package entry count.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<string>();
        long total = 0;
        for (int index = 0; index < count; index++)
        {
            string path = reader.ReadString();
            ValidatePath(path);
            if (!paths.Add(path)) throw new InvalidDataException("Duplicate package path.");
            long length = reader.ReadInt64();
            byte[] expectedHash = reader.ReadBytes(32);
            long packedLength = reader.ReadInt64();
            if (length < 0 || length > MaxFileBytes || (total += length) > MaxTotalBytes ||
                expectedHash.Length != 32 || packedLength < 0 || packedLength > MaxFileBytes ||
                packedLength > stream.Length - stream.Position)
                throw new InvalidDataException("Invalid package entry size.");
            byte[] packed = reader.ReadBytes(checked((int)packedLength));
            string target = GamePackageExporter.ResolveInside(destination, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var compressed = new MemoryStream(packed, false);
            using var decompressor = new ZLibStream(compressed, CompressionMode.Decompress);
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[65536];
            long written = 0;
            int read;
            while ((read = decompressor.Read(buffer)) != 0)
            {
                if ((written += read) > length) throw new InvalidDataException("Package entry exceeds its declared size.");
                output.Write(buffer, 0, read);
                hash.AppendData(buffer, 0, read);
            }
            if (written != length || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), expectedHash))
                throw new InvalidDataException("Asset package is corrupted: " + path);
            entries.Add(path);
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected package trailing data.");
        return entries;
    }

    private static void ValidatePath(string path)
    {
        if (path.Length > 1024 || path.Contains('\\') || path.Contains(':') ||
            path.Split('/').Any(part => part.Length == 0 || part is "." or ".."))
            throw new InvalidDataException("Unsafe asset package path.");
    }
}

/// <summary>Private session storage supports native importers that require file paths.</summary>
public sealed class GameContentSession : IDisposable
{
    private readonly string? _temporaryRoot;
    public string ProjectFile { get; }

    public GameContentSession(string gameDirectory)
    {
        string package = Path.Combine(gameDirectory, ByteAssetPackage.FileName);
        if (!File.Exists(package))
        {
            // Previous exported games remain playable.
            ProjectFile = Path.Combine(gameDirectory, "Content", "Game.byteproject");
            return;
        }
        _temporaryRoot = Path.Combine(Path.GetTempPath(), "ByteEngineGameContent", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryRoot);
        ProjectFile = Path.Combine(_temporaryRoot, "Game.byteproject");
        try
        {
            ByteAssetPackage.Extract(package, _temporaryRoot);
            if (!File.Exists(ProjectFile)) throw new InvalidDataException("Asset package has no game project.");
            // Packages store files, so procedural examples may have no asset-folder entries.
            var project = new ByteEngine.Core.Serialization.ProjectSerializer().Load(ProjectFile);
            Directory.CreateDirectory(GamePackageExporter.ResolveInside(_temporaryRoot, project.AssetDirectory));
            Directory.CreateDirectory(GamePackageExporter.ResolveInside(_temporaryRoot, project.SceneDirectory));
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        if (_temporaryRoot == null) return;
        // Non-collectible V1 plugin assemblies can remain mapped by Windows until exit.
        // Remove every unlocked asset even when a private plugin-cache DLL is locked.
        if (!Directory.Exists(_temporaryRoot)) return;
        foreach (string file in Directory.EnumerateFiles(_temporaryRoot, "*", SearchOption.AllDirectories))
            try { File.Delete(file); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        foreach (string directory in Directory.EnumerateDirectories(_temporaryRoot, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
            try { Directory.Delete(directory); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        try { Directory.Delete(_temporaryRoot); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}
