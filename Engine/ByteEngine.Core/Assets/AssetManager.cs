using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Assets;

public sealed class AssetManager : IDisposable
{
    private readonly AssetDatabase _database;
    private readonly Action<string>? _warningSink;
    private readonly Dictionary<Guid, Texture2D> _textures = new();
    private readonly HashSet<string> _reportedMissing = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextureImporter _textureImporter = new();
    private Texture2D? _missingTexture;

    public string ProjectRoot => _database.ProjectRoot;

    public AssetManager(AssetDatabase database, Action<string>? warningSink = null)
    {
        _database = database;
        _warningSink = warningSink;
        _database.DatabaseChanged += ReloadChangedResources;
    }

    public Texture2D LoadTexture(AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.IsEmpty)
        {
            // Intentionally unassigned is not a broken asset reference.
            return GetMissingTexture();
        }

        AssetRecord? asset = _database.Resolve(reference);
        if (asset == null || asset.Type != AssetType.Texture2D)
        {
            ReportMissing(reference);
            return GetMissingTexture();
        }

        if (_textures.TryGetValue(asset.Guid, out Texture2D? existing))
        {
            return existing;
        }

        try
        {
            Texture2D texture = _textureImporter.Import(asset);
            _textures[asset.Guid] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            _warningSink?.Invoke($"Could not load texture '{asset.ProjectPath}': {exception.Message}. Using the missing-texture placeholder.");
            return GetMissingTexture();
        }
    }

    public string ResolveProjectPath(string projectPath) => _database.ResolveProjectPath(projectPath);

    public Texture2D GetMissingTexture() => _missingTexture ??= Texture2D.CreateMissingTexture();

    private void ReloadChangedResources()
    {
        foreach ((Guid guid, Texture2D texture) in _textures.ToArray())
        {
            if (!_database.TryGetAsset(guid, out AssetRecord? record) || record == null || record.Type != AssetType.Texture2D)
            {
                texture.ReplaceWithMissing();
                continue;
            }

            try
            {
                texture.Reload(record.FullPath, record.Metadata.Importer.Filter);
            }
            catch (Exception exception)
            {
                texture.ReplaceWithMissing();
                _warningSink?.Invoke($"Could not reload texture '{record.ProjectPath}': {exception.Message}");
            }
        }
    }

    private void ReportMissing(AssetReference reference)
    {
        string identity = reference.Guid != Guid.Empty ? reference.Guid.ToString() : reference.CachedProjectPath ?? "(empty)";
        if (_reportedMissing.Add(identity))
        {
            _warningSink?.Invoke($"Missing texture asset '{identity}'. The reference was preserved and a checkerboard is shown.");
        }
    }

    public void Dispose()
    {
        _database.DatabaseChanged -= ReloadChangedResources;
        foreach (Texture2D texture in _textures.Values.Distinct()) texture.Dispose();
        _textures.Clear();
        _missingTexture?.Dispose();
        _missingTexture = null;
    }
}
