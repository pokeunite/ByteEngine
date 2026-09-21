using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Core.Animation;

public static class ModelSocketMetadataStore
{
    private const int CurrentVersion = 1;
    private static readonly Dictionary<string, CachedFile> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string GetMetadataPath(string projectRoot, Guid modelGuid) =>
        Path.Combine(Path.GetFullPath(projectRoot), ".byteengine", "ModelSockets", $"{modelGuid:N}.sockets.json");

    public static IReadOnlyList<SkeletalSocketDefinition> Load(string projectRoot, Guid modelGuid)
    {
        string path = GetMetadataPath(projectRoot, modelGuid);
        if (!File.Exists(path)) { Cache.Remove(path); return Array.Empty<SkeletalSocketDefinition>(); }
        FileInfo info = new(path);
        if (Cache.TryGetValue(path, out CachedFile? cached) && cached.Length == info.Length && cached.WriteTime == info.LastWriteTimeUtc)
            return Clone(cached.File.Sockets);
        try
        {
            SocketFile? file = JsonSerializer.Deserialize<SocketFile>(File.ReadAllText(path), JsonSerialization.Options);
            if (file == null || file.ModelGuid != modelGuid) throw new InvalidDataException("Socket metadata owner model GUID is invalid.");
            file.Sockets ??= new();
            Normalize(file.Sockets);
            ValidateUniqueNames(file.Sockets);
            Cache[path] = new CachedFile(info.Length, info.LastWriteTimeUtc, file);
            return Clone(file.Sockets);
        }
        catch (Exception exception) when (exception is not InvalidDataException)
        {
            Cache.Remove(path);
            throw new InvalidDataException($"Socket metadata for model '{modelGuid}' is corrupt.", exception);
        }
    }

    public static void Save(string projectRoot, Guid modelGuid, IEnumerable<SkeletalSocketDefinition> sockets)
    {
        List<SkeletalSocketDefinition> copy = Clone(sockets);
        Normalize(copy);
        ValidateUniqueNames(copy);
        SocketFile file = new() { Version = CurrentVersion, ModelGuid = modelGuid, Sockets = copy };
        string path = GetMetadataPath(projectRoot, modelGuid);
        JsonSerialization.WriteAtomic(path, file);
        FileInfo info = new(path);
        Cache[path] = new CachedFile(info.Length, info.LastWriteTimeUtc, file);
    }

    internal static void MergeInto(string projectRoot, ModelAsset model, Action<string>? warningSink = null)
    {
        try { model.ReplaceSockets(Load(projectRoot, model.Guid)); }
        catch (Exception exception) { warningSink?.Invoke(exception.Message); }
    }

    public static void ValidateUniqueNames(IEnumerable<SkeletalSocketDefinition> sockets)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (SkeletalSocketDefinition socket in sockets)
        {
            if (string.IsNullOrWhiteSpace(socket.Name)) throw new InvalidDataException("Socket names cannot be empty.");
            if (!names.Add(socket.Name.Trim())) throw new InvalidDataException($"Duplicate socket name '{socket.Name}'.");
        }
    }

    private static void Normalize(List<SkeletalSocketDefinition> sockets)
    {
        foreach (SkeletalSocketDefinition socket in sockets)
        {
            if (socket.Id == Guid.Empty) socket.Id = Guid.NewGuid();
            socket.Name = socket.Name?.Trim() ?? string.Empty;
            socket.BoneName = socket.BoneName?.Trim() ?? string.Empty;
            socket.Scale = new(MathF.Max(MathF.Abs(socket.Scale.X), .0001f), MathF.Max(MathF.Abs(socket.Scale.Y), .0001f), MathF.Max(MathF.Abs(socket.Scale.Z), .0001f));
        }
    }

    private static List<SkeletalSocketDefinition> Clone(IEnumerable<SkeletalSocketDefinition>? sockets) =>
        sockets?.Where(item => item != null).Select(item => item.Clone()).ToList() ?? new();

    private sealed record CachedFile(long Length, DateTime WriteTime, SocketFile File);
    private sealed class SocketFile
    {
        public int Version { get; set; } = CurrentVersion;
        public Guid ModelGuid { get; set; }
        public List<SkeletalSocketDefinition> Sockets { get; set; } = new();
    }
}