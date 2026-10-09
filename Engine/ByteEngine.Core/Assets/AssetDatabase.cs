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
    private bool _fullScanPending;
    private readonly HashSet<string> _changedPaths = new(StringComparer.OrdinalIgnoreCase);
    public int LastRefreshFileCount { get; private set; }
    public bool LastRefreshWasFullScan { get; private set; }
    public event Action<IReadOnlyCollection<Guid>>? AssetsChanged;
    public event Action? OwnerThreadUpdate;
    public AssetDependencyGraph Dependencies { get; } = new();
    private bool _disposed;
    private readonly bool _readOnly;

    public string ProjectRoot => _projectRoot;
    public IReadOnlyCollection<AssetRecord> Assets => _byGuid.Values;
    public event Action? DatabaseChanged;

    public AssetDatabase(
        string projectRoot,
        IEnumerable<string> contentDirectories,
        Action<string>? warningSink = null,
        Action<string>? errorSink = null,
        bool readOnly = false)
    {
        _readOnly = readOnly;
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
            if (!_readOnly) Directory.CreateDirectory(root);
            else if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        }

        Scan();
        if (!_readOnly) StartWatchers();
    }

    public void Update()
    {
        OwnerThreadUpdate?.Invoke();
        string[] paths;
        bool full;
        lock (_eventLock)
        {
            if (!_scanPending || DateTime.UtcNow - _lastFileEventUtc < TimeSpan.FromMilliseconds(250)) return;
            _scanPending = false;
            full = _fullScanPending;
            _fullScanPending = false;
            paths = _changedPaths.ToArray();
            _changedPaths.Clear();
        }
        if (full) Scan();
        else RefreshPaths(paths);
    }

    /// <summary>Refreshes changed files on the owner thread. Directories require a recovery scan.</summary>
    public void RefreshPaths(IEnumerable<string> paths)
    {
        var changed = new HashSet<Guid>();
        LastRefreshFileCount = 0;
        LastRefreshWasFullScan = false;
        foreach (string requested in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string fullPath = Path.IsPathRooted(requested) ? Path.GetFullPath(requested) : ResolveProjectPath(requested);
            if (fullPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) fullPath = fullPath[..^5];
            if (IsSidecarOrTemporary(fullPath)) continue;
            if (Directory.Exists(fullPath)) { Scan(); return; }
            string projectPath = ToProjectPath(fullPath);
            // Deleted directory notifications must also remove every indexed descendant.
            var descendants = _byPath.Keys.Where(p => p.StartsWith(projectPath + "/", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (descendants.Length > 0) { Scan(); return; }
            try
            {
                LastRefreshFileCount++;
                _byPath.TryGetValue(projectPath, out var previous);
                if (!File.Exists(fullPath))
                {
                    if (previous != null) { _byPath.Remove(projectPath); _byGuid.Remove(previous.Guid); changed.Add(previous.Guid); }
                    continue;
                }
                var metadata = ReadOrCreateMetadata(fullPath, DetectType(fullPath));
                if (_byGuid.TryGetValue(metadata.Guid, out var other) &&
                    !string.Equals(other.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Reuse the full-scan identity recovery for a duplicate or rename collision.
                    Scan(); return;
                }
                if (previous != null && previous.Guid != metadata.Guid) { _byGuid.Remove(previous.Guid); changed.Add(previous.Guid); }
                var record = new AssetRecord(metadata.Guid, metadata.Type, projectPath, fullPath, fullPath + ".meta", metadata);
                _byPath[projectPath] = record; _byGuid[metadata.Guid] = record;
                changed.Add(metadata.Guid);
            }
            catch (Exception exception)
            {
                if (_readOnly) throw;
                _errorSink?.Invoke($"Could not refresh asset '{projectPath}': {exception.Message}");
            }
        }
        if (changed.Count > 0) { AssetsChanged?.Invoke(Dependencies.Affected(changed)); DatabaseChanged?.Invoke(); }
    }

    public void RequestRefresh()
    {
        lock (_eventLock)
        {
            _scanPending = true;
            _fullScanPending = true;
            _lastFileEventUtc = DateTime.MinValue;
        }
    }

    public void Scan()
    {
        LastRefreshFileCount = 0;
        LastRefreshWasFullScan = true;
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
                if (_readOnly) throw;
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
                    LastRefreshFileCount++;
                    string projectPath = ToProjectPath(file);
                    AssetMetadata metadata = ReadOrCreateMetadata(file, DetectType(file));

                    if (newByGuid.TryGetValue(metadata.Guid, out AssetRecord? collision))
                    {
                        if (_readOnly) throw new InvalidDataException($"Duplicate packaged asset GUID: {metadata.Guid}");
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
                    if (_readOnly) throw;
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
        RefreshPaths(new[] { record.FullPath });
    }

    public void SetModelImporterSettings(
        Guid guid,
        float importScale,
        bool generateNormals,
        bool preferEmbeddedMaterials)
    {
        if (!TryGetAsset(guid, out AssetRecord? record) || record?.Type != AssetType.Model3D) return;
        record.Metadata.ModelImporter.ImportScale = Math.Max(importScale, .0001f);
        record.Metadata.ModelImporter.GenerateNormals = generateNormals;
        record.Metadata.ModelImporter.PreferEmbeddedMaterials = preferEmbeddedMaterials;
        WriteMetadata(record.MetaPath, record.Metadata);
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
        if (_readOnly)
        {
            AssetMetadata? packaged = JsonSerializer.Deserialize<AssetMetadata>(File.ReadAllText(metaPath), MetaJson);
            if (packaged == null || packaged.Guid == Guid.Empty || packaged.Type != detectedType)
                throw new InvalidDataException($"Invalid packaged asset metadata: {metaPath}");
            return packaged;
        }
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

    private void OnFileEvent(object sender, FileSystemEventArgs args) => QueuePath(args.FullPath);

    private void QueuePath(string path)
    {
        if (path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return;
        lock (_eventLock)
        {
            _changedPaths.Add(path);
            _scanPending = true;
            _lastFileEventUtc = DateTime.UtcNow;
        }
    }

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

        // Process old path first to preserve the existing GUID on a rename.
        QueuePath(args.OldFullPath);
        QueuePath(args.FullPath);
    }

    private void QueueScan()
    {
        lock (_eventLock)
        {
            _fullScanPending = true;
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
        ".jpg" => AssetType.Texture2D,
        ".jpeg" => AssetType.Texture2D,
        ".tga" => AssetType.Texture2D,
        ".bmp" => AssetType.Texture2D,
        ".hdr" => AssetType.Texture2D,
        ".wav" => AssetType.AudioClip,
        ".ogg" => AssetType.AudioClip,
        ".bytescene" => AssetType.Scene,
        ".fbx" => AssetType.Model3D,
        ".obj" => AssetType.Model3D,
        ".gltf" => AssetType.Model3D,
        ".glb" => AssetType.Model3D,
        ".byteevents" => AssetType.EventModule,
        ".byteblueprint" => AssetType.Blueprint,
        ".byteanimevents" => AssetType.AnimationEvents,
        ".byteanim" => AssetType.AnimationProfile,
        ".bmat" => AssetType.Material,
        ".bvfx" => AssetType.VfxEffect,
        ".uitheme" => AssetType.UiTheme,
        ".ttf" => AssetType.Font,
        ".otf" => AssetType.Font,
        ".fnt" => AssetType.Font,
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
