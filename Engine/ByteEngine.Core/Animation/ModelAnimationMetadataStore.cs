using System.Text.Json;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Persistent author metadata for native and baked animations. Identity is the
/// owner model GUID plus the animation's stable key; source model binaries and
/// baked keyframe payloads remain untouched.
/// </summary>
public static class ModelAnimationMetadataStore
{
    private const int CurrentVersion = 1;

    private static readonly Dictionary<string, CachedMetadata> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string GetMetadataPath(string projectRoot, Guid modelGuid)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new ArgumentException("Project root cannot be empty.", nameof(projectRoot));
        if (modelGuid == Guid.Empty)
            throw new ArgumentException("Model GUID cannot be empty.", nameof(modelGuid));

        return Path.Combine(
            Path.GetFullPath(projectRoot),
            ".byteengine",
            "ModelAnimations",
            $"{modelGuid:N}.metadata.json");
    }

    /// <summary>
    /// Atomically saves one clip's event/window metadata. Existing malformed
    /// metadata is never replaced with an empty or default document.
    /// </summary>
    public static void Save(
        string projectRoot,
        Guid modelGuid,
        ImportedAnimation animation)
    {
        ArgumentNullException.ThrowIfNull(animation);
        string key = NormalizeKey(animation.Key);

        ModelAnimationMetadataFile file = LoadStrict(projectRoot, modelGuid);
        ModelAnimationMetadataEntry? entry = file.Animations.FirstOrDefault(
            candidate => string.Equals(candidate.AnimationKey, key, StringComparison.Ordinal));

        var replacement = new ModelAnimationMetadataEntry
        {
            AnimationKey = key,
            AnimationName = animation.Name ?? string.Empty,
            Events = CloneEvents(animation.Events),
            Windows = CloneWindows(animation.Windows)
        };

        if (entry == null)
            file.Animations.Add(replacement);
        else
            file.Animations[file.Animations.IndexOf(entry)] = replacement;

        file.Version = CurrentVersion;
        file.ModelGuid = modelGuid;

        string path = GetMetadataPath(projectRoot, modelGuid);
        JsonSerialization.WriteAtomic(path, file);
        CacheFile(path, file);
    }

    /// <summary>
    /// Removes metadata for one deleted baked clip. A corrupt file causes a
    /// strict failure before any valid content can be overwritten.
    /// </summary>
    public static bool Remove(
        string projectRoot,
        Guid modelGuid,
        string animationKey)
    {
        string key = NormalizeKey(animationKey);
        string path = GetMetadataPath(projectRoot, modelGuid);
        if (!File.Exists(path))
        {
            Cache.Remove(path);
            return false;
        }

        ModelAnimationMetadataFile file = LoadStrict(projectRoot, modelGuid);
        int removed = file.Animations.RemoveAll(
            entry => string.Equals(entry.AnimationKey, key, StringComparison.Ordinal));
        if (removed == 0) return false;

        if (file.Animations.Count == 0)
        {
            File.Delete(path);
            Cache.Remove(path);
        }
        else
        {
            JsonSerialization.WriteAtomic(path, file);
            CacheFile(path, file);
        }

        return true;
    }

    /// <summary>
    /// Applies cached metadata during model load/reimport. Malformed metadata
    /// leaves imported animations unchanged and is reported without preventing
    /// the source model itself from loading.
    /// </summary>
    internal static int MergeInto(
        string projectRoot,
        ModelAsset model,
        Action<string>? warningSink = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        ModelAnimationMetadataFile file;
        try
        {
            file = LoadStrict(projectRoot, model.Guid);
        }
        catch (Exception exception)
        {
            warningSink?.Invoke(exception.Message);
            return 0;
        }

        if (file.Animations.Count == 0) return 0;

        var byKey = new Dictionary<string, ImportedAnimation>(StringComparer.Ordinal);
        foreach (ImportedAnimation animation in model.Animations)
        {
            if (!string.IsNullOrWhiteSpace(animation.Key))
                byKey[animation.Key] = animation;
        }

        int merged = 0;
        foreach (ModelAnimationMetadataEntry entry in file.Animations)
        {
            if (string.IsNullOrWhiteSpace(entry.AnimationKey) ||
                !byKey.TryGetValue(entry.AnimationKey, out ImportedAnimation? animation))
            {
                continue;
            }

            animation.Events.Clear();
            animation.Events.AddRange(CloneEvents(entry.Events));
            animation.Windows.Clear();
            animation.Windows.AddRange(CloneWindows(entry.Windows));
            merged++;
        }

        return merged;
    }

    private static ModelAnimationMetadataFile LoadStrict(
        string projectRoot,
        Guid modelGuid)
    {
        string path = GetMetadataPath(projectRoot, modelGuid);
        if (!File.Exists(path))
        {
            Cache.Remove(path);
            return NewFile(modelGuid);
        }

        FileInfo info = new(path);
        if (Cache.TryGetValue(path, out CachedMetadata? cached) &&
            cached.Length == info.Length &&
            cached.LastWriteTimeUtc == info.LastWriteTimeUtc)
        {
            return cached.File;
        }

        try
        {
            ModelAnimationMetadataFile? file =
                JsonSerializer.Deserialize<ModelAnimationMetadataFile>(
                    File.ReadAllText(path),
                    JsonSerialization.Options);

            if (file == null || file.ModelGuid != modelGuid)
                throw new InvalidDataException("Metadata owner model GUID does not match its filename.");

            file.Animations ??= new();
            foreach (ModelAnimationMetadataEntry entry in file.Animations)
            {
                entry.AnimationKey ??= string.Empty;
                entry.AnimationName ??= string.Empty;
                entry.Events ??= new();
                entry.Windows ??= new();
            }

            Cache[path] = new CachedMetadata(info.Length, info.LastWriteTimeUtc, file);
            return file;
        }
        catch (Exception exception) when (exception is not InvalidDataException)
        {
            Cache.Remove(path);
            throw new InvalidDataException(
                $"Animation metadata for model '{modelGuid}' is corrupt. ByteEngine will not overwrite it until it is repaired or removed.",
                exception);
        }
    }

    private static ModelAnimationMetadataFile NewFile(Guid modelGuid) =>
        new() { Version = CurrentVersion, ModelGuid = modelGuid };

    private static void CacheFile(string path, ModelAnimationMetadataFile file)
    {
        FileInfo info = new(path);
        Cache[path] = new CachedMetadata(
            info.Exists ? info.Length : 0L,
            info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue,
            file);
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Animation key cannot be empty.", nameof(key));
        return key.Trim();
    }

    private static List<AnimationEventMarker> CloneEvents(
        IEnumerable<AnimationEventMarker>? events) =>
        events?.Where(item => item != null).Select(item => item.Clone()).ToList() ?? new();

    private static List<AnimationWindow> CloneWindows(
        IEnumerable<AnimationWindow>? windows) =>
        windows?.Where(item => item != null).Select(item => item.Clone()).ToList() ?? new();

    private sealed record CachedMetadata(
        long Length,
        DateTime LastWriteTimeUtc,
        ModelAnimationMetadataFile File);

    private sealed class ModelAnimationMetadataFile
    {
        public int Version { get; set; } = CurrentVersion;
        public Guid ModelGuid { get; set; }
        public List<ModelAnimationMetadataEntry> Animations { get; set; } = new();
    }

    private sealed class ModelAnimationMetadataEntry
    {
        public string AnimationKey { get; set; } = string.Empty;
        public string AnimationName { get; set; } = string.Empty;
        public List<AnimationEventMarker> Events { get; set; } = new();
        public List<AnimationWindow> Windows { get; set; } = new();
    }
}
