using System.Text.Json;
using System.Text.Json.Serialization;

namespace ByteEngine.Core.Assets;

public sealed class AssetDatabase : IDisposable
{
    private static readonly JsonSerializerOptions MetaJson = CreateJsonOptions();
    private readonly string _projectRoot;
    private readonly string[] _contentRoots;
    private readonly Action<string>? _warningSink;
    private readonly Action<string>? _errorSink;
    private readonly Dictionary<Guid, AssetRecord> _byGuid = new();
    private readonly Dictionary<string, AssetRecord> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly object _eventLock = new();
    private DateTime _lastFileEventUtc;
    private bool _scanPending;
    private bool _disposed;

    public string ProjectRoot => _projectRoot;
    public IReadOnlyCollection<AssetRecord> Assets => _byGuid.Values;
    public event Action? DatabaseChanged;

    public AssetDatabase(
        string projectRoot,
        IEnumerable<string> contentDirectories,
        Action<string>? warningSink = null,
        Action<string>? errorSink = null)
    {
        _projectRoot = Path.GetFullPath(projectRoot);
        _contentRoots = contentDirectories
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(ResolveProjectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _warningSink = warningSink;
        _errorSink = errorSink;

        foreach (string root in _contentRoots)
        {
            Directory.CreateDirectory(root);
        }

        Scan();
        StartWatchers();
    }

    public void Update()
    {
        bool scan;
        lock (_eventLock)
        {
            scan = _scanPending && DateTime.UtcNow - _lastFileEventUtc >= TimeSpan.FromMilliseconds(250);
            if (scan)
            {
                _scanPending = false;
            }
        }

        if (scan)
        {
            Scan();
        }
    }

    public void RequestRefresh()
    {
        lock (_eventLock)
        {
            _scanPending = true;
            _lastFileEventUtc = DateTime.MinValue;
        }
    }

    public void Scan()
    {
        var newByGuid = new Dictionary<Guid, AssetRecord>();
        var newByPath = new Dictionary<string, AssetRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in _contentRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToArray();
            }
            catch (Exception exception)
            {
                _errorSink?.Invoke($"Could not scan asset directory '{root}': {exception.Message}");
                continue;
            }

            foreach (string file in files)
            {
                if (IsSidecarOrTemporary(file))
                {
                    continue;
                }

                try
                {
                    string projectPath = ToProjectPath(file);
                    AssetMetadata metadata = ReadOrCreateMetadata(file, DetectType(file));

                    if (newByGuid.TryGetValue(metadata.Guid, out AssetRecord? collision))
                    {
                        Guid duplicate = metadata.Guid;
                        if (_byGuid.TryGetValue(duplicate, out AssetRecord? previous) &&
                            string.Equals(previous.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase))
                        {
                            Guid replacementGuid = Guid.NewGuid();
                            collision.Metadata.Guid = replacementGuid;
                            WriteMetadata(collision.MetaPath, collision.Metadata);
                            var replacement = new AssetRecord(
                                replacementGuid,
                                collision.Type,
                                collision.ProjectPath,
                                collision.FullPath,
                                collision.MetaPath,
                                collision.Metadata);
                            newByGuid.Remove(duplicate);
                            newByGuid[replacementGuid] = replacement;
                            newByPath[replacement.ProjectPath] = replacement;
                            _warningSink?.Invoke($"Duplicate asset GUID {duplicate} detected for '{collision.ProjectPath}'. A new GUID was assigned while preserving the existing asset identity.");
                        }
                        else
                        {
                            metadata.Guid = Guid.NewGuid();
                            WriteMetadata(file + ".meta", metadata);
                            _warningSink?.Invoke($"Duplicate asset GUID {duplicate} detected for '{projectPath}'. A new GUID was assigned.");
                        }
                    }

                    var record = new AssetRecord(
                        metadata.Guid,
                        metadata.Type,
                        projectPath,
                        Path.GetFullPath(file),
                        Path.GetFullPath(file + ".meta"),
                        metadata);

                    newByGuid[record.Guid] = record;
                    newByPath[record.ProjectPath] = record;
                }
                catch (Exception exception)
                {
                    _errorSink?.Invoke($"Could not import asset '{file}': {exception.Message}");
                }
            }
        }

        _byGuid.Clear();
        _byPath.Clear();
        foreach (var pair in newByGuid) _byGuid[pair.Key] = pair.Value;
        foreach (var pair in newByPath) _byPath[pair.Key] = pair.Value;
        DatabaseChanged?.Invoke();
    }

    public bool TryGetAsset(Guid guid, out AssetRecord? asset) => _byGuid.TryGetValue(guid, out asset);

    public bool TryGetAsset(string projectPath, out AssetRecord? asset) =>
        _byPath.TryGetValue(NormalizeProjectPath(projectPath), out asset);

    public AssetRecord? Resolve(AssetReference reference)
    {
        if (reference.Guid != Guid.Empty && TryGetAsset(reference.Guid, out AssetRecord? byGuid))
        {
            return byGuid;
        }

        if (reference.Guid == Guid.Empty && reference.CachedProjectPath != null &&
            TryGetAsset(reference.CachedProjectPath, out AssetRecord? byPath))
        {
            return byPath;
        }

        return null;
    }

    public AssetReference ResolveReference(string legacyProjectPath)
    {
        if (TryGetAsset(legacyProjectPath, out AssetRecord? asset) && asset != null)
        {
            return new AssetReference(asset.Guid, asset.ProjectPath);
        }

        return new AssetReference(legacyProjectPath);
    }

    public void SetTextureFilter(Guid guid, Graphics.TextureFilter filter)
    {
        if (!TryGetAsset(guid, out AssetRecord? record) || record == null || record.Type != AssetType.Texture2D)
        {
            return;
        }

        if (record.Metadata.Importer.Filter == filter)
        {
            return;
        }

        record.Metadata.Importer.Filter = filter;
        WriteMetadata(record.MetaPath, record.Metadata);
        Scan();
    }

    public string ResolveProjectPath(string projectPath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(_projectRoot, projectPath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = _projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, _projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Project-relative path escapes the project directory: {projectPath}");
        }

        return fullPath;
    }

    private AssetMetadata ReadOrCreateMetadata(string assetPath, AssetType detectedType)
    {
        string metaPath = assetPath + ".meta";
        AssetMetadata? metadata = null;

        if (File.Exists(metaPath))
        {
            try
            {
                metadata = JsonSerializer.Deserialize<AssetMetadata>(File.ReadAllText(metaPath), MetaJson);
            }
            catch (Exception exception)
            {
                _warningSink?.Invoke($"Corrupt metadata '{ToProjectPath(metaPath)}' was regenerated: {exception.Message}");
            }
        }

        if (metadata == null || metadata.Guid == Guid.Empty)
        {
            metadata = new AssetMetadata { Guid = Guid.NewGuid(), Type = detectedType };
            WriteMetadata(metaPath, metadata);
        }
        else if (metadata.Type != detectedType)
        {
            metadata.Type = detectedType;
            WriteMetadata(metaPath, metadata);
        }

        return metadata;
    }

    private static void WriteMetadata(string path, AssetMetadata metadata)
    {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(metadata, MetaJson));
        File.Move(temporary, path, true);
    }

    private void StartWatchers()
    {
        foreach (string root in _contentRoots)
        {
            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            watcher.Created += OnFileEvent;
            watcher.Changed += OnFileEvent;
            watcher.Deleted += OnFileEvent;
            watcher.Renamed += OnRenamed;
            watcher.Error += (_, args) =>
            {
                _warningSink?.Invoke($"Asset watcher reported an error: {args.GetException().Message}");
                QueueScan();
            };
            _watchers.Add(watcher);
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs args) => QueueScan();

    private void OnRenamed(object sender, RenamedEventArgs args)
    {
        try
        {
            string oldMeta = args.OldFullPath + ".meta";
            string newMeta = args.FullPath + ".meta";
            if (File.Exists(oldMeta) && !File.Exists(newMeta) && !IsSidecarOrTemporary(args.FullPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newMeta)!);
                File.Move(oldMeta, newMeta);
            }
        }
        catch (Exception exception)
        {
            _warningSink?.Invoke($"Could not preserve asset metadata during rename: {exception.Message}");
        }

        QueueScan();
    }

    private void QueueScan()
    {
        lock (_eventLock)
        {
            _scanPending = true;
            _lastFileEventUtc = DateTime.UtcNow;
        }
    }

    private string ToProjectPath(string fullPath) => NormalizeProjectPath(Path.GetRelativePath(_projectRoot, fullPath));

    private static string NormalizeProjectPath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static bool IsSidecarOrTemporary(string path) =>
        path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);

    private static AssetType DetectType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => AssetType.Texture2D,
        ".bytescene" => AssetType.Scene,
        ".fbx" => AssetType.Model3D,
        ".obj" => AssetType.Model3D,
        ".gltf" => AssetType.Model3D,
        ".glb" => AssetType.Model3D,
        ".byteevents" => AssetType.EventModule,
        ".byteblueprint" => AssetType.Blueprint,
        ".byteanimevents" => AssetType.AnimationEvents,
        _ => AssetType.Unknown
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (FileSystemWatcher watcher in _watchers) watcher.Dispose();
        _watchers.Clear();
    }
}
