using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Assets;

public sealed class AssetManager : IDisposable
{
    private readonly AssetDatabase _database;
    private readonly Action<string>? _warningSink;
    private readonly Dictionary<Guid, Texture2D> _textures = new();
    private readonly Dictionary<Guid, ModelAsset> _models = new();
    private readonly Dictionary<Guid, AnimationProfile> _animationProfiles = new();
    private readonly Dictionary<(Guid Model, string Key), Mesh> _modelMeshes = new();
    private readonly Dictionary<(Guid Model, string Key), Material> _modelMaterials = new();
    private readonly Dictionary<(Guid Model, string Key), Texture2D> _modelTextures = new();
    private readonly HashSet<string> _reportedMissing = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextureImporter _textureImporter = new();
    private readonly AnimationProfileImporter _animationProfileImporter = new();
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

    public ModelAsset LoadModel(AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        AssetRecord? asset = _database.Resolve(reference);
        if (asset == null || asset.Type != AssetType.Model3D)
            throw new FileNotFoundException($"Model asset '{reference}' could not be resolved.");
        if (_models.TryGetValue(asset.Guid, out ModelAsset? cached)) return cached;

        ImportedModel imported = ModelImporter.ForPath(asset.FullPath).Import(asset, asset.Metadata.ModelImporter);
        var model = new ModelAsset(imported);
        _models[asset.Guid] = model;
        return model;
    }

    /// <summary>
    /// Loads a unified .byteanim Animation Profile through the same GUID-backed
    /// asset system used by models and textures.
    /// </summary>
    public AnimationProfile LoadAnimationProfile(AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        AssetRecord? asset = _database.Resolve(reference);
        if (asset == null || asset.Type != AssetType.AnimationProfile)
            throw new FileNotFoundException($"Animation Profile asset '{reference}' could not be resolved.");

        if (_animationProfiles.TryGetValue(asset.Guid, out AnimationProfile? cached))
            return cached;

        AnimationProfile profile = _animationProfileImporter.Import(asset);
        _animationProfiles[asset.Guid] = profile;
        return profile;
    }

    public ModelAsset ReimportModel(Guid guid)
    {
        if (!_database.TryGetAsset(guid, out AssetRecord? record) || record?.Type != AssetType.Model3D)
            throw new FileNotFoundException($"Model asset '{guid}' could not be resolved.");
        return RefreshModel(record);
    }

    public Mesh GetModelMesh(AssetReference modelReference, string subAssetKey)
    {
        ModelAsset model = LoadModel(modelReference);
        var cacheKey = (model.Guid, subAssetKey);
        if (_modelMeshes.TryGetValue(cacheKey, out Mesh? cached)) return cached;
        ImportedMesh source = model.Meshes.FirstOrDefault(mesh => mesh.Key == subAssetKey)
            ?? throw new KeyNotFoundException($"Model mesh '{subAssetKey}' was not found in '{model.Name}'.");
        var mesh = new Mesh(source.Vertices, source.Indices);
        _modelMeshes[cacheKey] = mesh;
        return mesh;
    }

    public Material GetModelMaterial(AssetReference modelReference, string subAssetKey)
    {
        ModelAsset model = LoadModel(modelReference);
        var cacheKey = (model.Guid, subAssetKey);
        if (_modelMaterials.TryGetValue(cacheKey, out Material? cached)) return cached;
        ImportedMaterial source = model.Materials.FirstOrDefault(material => material.Key == subAssetKey)
            ?? throw new KeyNotFoundException($"Model material '{subAssetKey}' was not found in '{model.Name}'.");
        var material = new Material();
        ApplyMaterial(model.Guid, source, material);
        _modelMaterials[cacheKey] = material;
        return material;
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

        foreach ((Guid guid, ModelAsset _) in _models.ToArray())
        {
            if (!_database.TryGetAsset(guid, out AssetRecord? record) || record?.Type != AssetType.Model3D)
                continue;
            try
            {
                RefreshModel(record);
            }
            catch (Exception exception)
            {
                _warningSink?.Invoke($"Could not reimport model '{record.ProjectPath}': {exception.Message}");
            }
        }

        /*
         * Preserve AnimationProfile object identity during hot reload. Runtime
         * controllers/editor panels can safely hold the loaded profile while
         * the asset file is edited or regenerated.
         */
        foreach ((Guid guid, AnimationProfile profile) in _animationProfiles.ToArray())
        {
            if (!_database.TryGetAsset(guid, out AssetRecord? record) ||
                record?.Type != AssetType.AnimationProfile)
            {
                _animationProfiles.Remove(guid);
                continue;
            }

            try
            {
                AnimationProfile refreshed = _animationProfileImporter.Import(record);
                CopyAnimationProfile(refreshed, profile);
            }
            catch (Exception exception)
            {
                _warningSink?.Invoke($"Could not reload Animation Profile '{record.ProjectPath}': {exception.Message}");
            }
        }
    }

    private ModelAsset RefreshModel(AssetRecord record)
    {
        Guid guid = record.Guid;
        var refreshed = new ModelAsset(
            ModelImporter.ForPath(record.FullPath).Import(record, record.Metadata.ModelImporter));
        _models[guid] = refreshed;
        foreach (ImportedMesh source in refreshed.Meshes)
            if (_modelMeshes.TryGetValue((guid, source.Key), out Mesh? mesh))
                mesh.ReplaceData(source.Vertices, source.Indices);
        foreach (ImportedMaterial source in refreshed.Materials)
            if (_modelMaterials.TryGetValue((guid, source.Key), out Material? material))
                ApplyMaterial(guid, source, material);
        return refreshed;
    }

    private static void CopyAnimationProfile(
        AnimationProfile source,
        AnimationProfile target)
    {
        target.Version = source.Version;
        target.Name = source.Name;
        target.Rig = source.Rig;
        target.Locomotion = source.Locomotion;
        target.Actions = source.Actions;
        target.Procedural = source.Procedural;
        target.Normalize();
    }

    private void ApplyMaterial(Guid modelGuid, ImportedMaterial source, Material target)
    {
        target.BaseColor = source.BaseColor;
        target.Metallic = source.Metallic;
        target.Roughness = source.Roughness;
        target.MainTexture = GetModelTexture(modelGuid, source.BaseColorTexture);
        target.NormalTexture = GetModelTexture(modelGuid, source.NormalTexture);
    }

    private Texture2D? GetModelTexture(Guid modelGuid, ImportedTexture? source)
    {
        if (source == null || source.EncodedData.Length == 0) return null;
        var key = (modelGuid, source.Key);
        if (_modelTextures.TryGetValue(key, out Texture2D? cached))
        {
            cached.ReloadEncoded(source.EncodedData);
            return cached;
        }

        Texture2D texture = Texture2D.FromEncodedBytes(source.EncodedData);
        _modelTextures[key] = texture;
        return texture;
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
        foreach (Mesh mesh in _modelMeshes.Values.Distinct()) mesh.Dispose();
        _modelMeshes.Clear();
        foreach (Texture2D texture in _modelTextures.Values.Distinct()) texture.Dispose();
        _modelTextures.Clear();
        _modelMaterials.Clear();
        _models.Clear();
        _animationProfiles.Clear();
        _missingTexture?.Dispose();
        _missingTexture = null;
    }
}
