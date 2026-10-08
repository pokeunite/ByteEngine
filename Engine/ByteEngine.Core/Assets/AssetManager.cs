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
    private readonly Dictionary<Guid, Material> _runtimeMaterials = new();
    private readonly Dictionary<Guid, MaterialAsset> _materialAssets = new();
    private readonly Dictionary<Guid, ByteEngine.Core.Vfx.VfxEffect> _vfxEffects = new();
    private readonly Dictionary<Guid, AssetRevision> _vfxRevisions = new();
    public int VfxRevision { get; private set; }

    public ByteEngine.Core.Vfx.VfxEffect LoadVfxEffect(AssetReference reference)
    {
        var asset = _database.Resolve(reference);
        if (asset?.Type != AssetType.VfxEffect)
            throw new FileNotFoundException($"VFX asset '{reference}' could not be resolved.");
        if (!_vfxEffects.TryGetValue(asset.Guid, out var effect))
        {
            _vfxEffects[asset.Guid] = effect = ByteEngine.Core.Vfx.VfxEffectSerializer.Load(asset.FullPath);
            _vfxRevisions[asset.Guid] = CaptureRevision(asset);
        }
        return effect;
    }

    public void ReloadVfxEffects() { _vfxEffects.Clear(); _vfxRevisions.Clear(); VfxRevision++; }
    private readonly Dictionary<Guid, AssetRevision> _materialRevisions = new();
    private readonly Dictionary<Guid, AnimationProfile> _animationProfiles = new();
    private readonly Dictionary<Guid, AssetRevision> _textureRevisions = new();
    private readonly Dictionary<Guid, AssetRevision> _modelRevisions = new();
    private readonly Dictionary<Guid, AssetRevision> _animationProfileRevisions = new();
    private readonly Dictionary<(Guid Model, string Key), Mesh> _modelMeshes = new();
    private readonly Dictionary<(Guid Model, string Key), Material> _modelMaterials = new();
    private readonly Dictionary<(Guid Model, string Key), Texture2D> _modelTextures = new();
    private readonly Dictionary<(Guid Model,string Key),byte[]> _modelTextureSources=new();
    private readonly HashSet<string> _reportedMissing = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextureImporter _textureImporter = new();
    private readonly AnimationProfileImporter _animationProfileImporter = new();
    private Texture2D? _missingTexture;

    public string ProjectRoot => _database.ProjectRoot;

    public AssetManager(AssetDatabase database, Action<string>? warningSink = null)
    {
        _database = database;
        Renderer2D.FontProjectRoot = database.ProjectRoot;
        FontRuntime.Configure(database);
        _warningSink = warningSink;
        _database.DatabaseChanged += ReloadChangedResources;
    }

    private readonly Dictionary<string,Texture2D> _runtimeTextures = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Registers a cached user-generated UI image without rescanning the project asset database.
    /// The manager owns the texture and disposes it when the project closes. Use content-addressed keys.</summary>
    public void RegisterRuntimeTexture(string projectPath,byte[] encodedImage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        string key=projectPath.Replace('\\','/');
        if (!_runtimeTextures.ContainsKey(key)) _runtimeTextures[key]=Texture2D.FromEncodedBytes(encodedImage);
    }

    public Texture2D LoadTexture(AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.IsEmpty)
        {
            // Intentionally unassigned is not a broken asset reference.
            return GetMissingTexture();
        }

        if (reference.CachedProjectPath is {} runtimePath && _runtimeTextures.TryGetValue(runtimePath.Replace('\\','/'),out var runtimeTexture)) return runtimeTexture;
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
            _textureRevisions[asset.Guid] = CaptureRevision(asset);
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

        asset.Metadata.ModelImporter.Normalize();

        ImportedModel imported =
            OperatingSystem.IsBrowser()
                ? CookedModelStore.Load(ProjectRoot, asset.Guid)
                : ModelImporter.ForPath(asset.FullPath).Import(asset, asset.Metadata.ModelImporter);

        var model =
            new ModelAsset(
                imported,
                asset.Metadata.ModelImporter);

        /*
         * Merge historical model-owned animations into the target
         * model before it enters the cache. The existing Asset Browser model
         * expand-arrow and all animation pickers already read ModelAsset.Animations,
         * so no parallel animation asset hierarchy is required.
         */
        ModelOwnedAnimationStore.MergeInto(
            ProjectRoot,
            model);

        ModelAnimationMetadataStore.MergeInto(
            ProjectRoot,
            model,
            _warningSink);

        ModelSocketMetadataStore.MergeInto(ProjectRoot, model, _warningSink);

        _models[asset.Guid] = model;
        _modelRevisions[asset.Guid] = CaptureRevision(asset);
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
        _animationProfileRevisions[asset.Guid] = CaptureRevision(asset);
        return profile;
    }

    /// <summary>
    /// Reloads one Animation Profile without forcing a full AssetDatabase scan.
    /// This is used by the profile editor after Save so editing a .byteanim file
    /// does not reimport every cached model and texture in the project.
    /// </summary>
    public AnimationProfile ReloadAnimationProfile(AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        AssetRecord? asset = _database.Resolve(reference);
        if (asset == null || asset.Type != AssetType.AnimationProfile)
            throw new FileNotFoundException($"Animation Profile asset '{reference}' could not be resolved.");

        AnimationProfile refreshed = _animationProfileImporter.Import(asset);

        if (_animationProfiles.TryGetValue(asset.Guid, out AnimationProfile? existing))
        {
            CopyAnimationProfile(refreshed, existing);
            _animationProfileRevisions[asset.Guid] = CaptureRevision(asset);
            return existing;
        }

        _animationProfiles[asset.Guid] = refreshed;
        _animationProfileRevisions[asset.Guid] = CaptureRevision(asset);
        return refreshed;
    }

    public MaterialAsset LoadMaterialAsset(AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        AssetRecord? asset = _database.Resolve(reference);
        if (asset == null || asset.Type != AssetType.Material)
            throw new FileNotFoundException($"Material asset '{reference}' could not be resolved.");
        if (_materialAssets.TryGetValue(asset.Guid, out MaterialAsset? cached))
            return cached;
        MaterialAsset material = MaterialAssetSerializer.Load(asset.FullPath);
        _materialAssets[asset.Guid] = material;
        _materialRevisions[asset.Guid] = CaptureRevision(asset);
        return material;
    }

    public Material LoadMaterial(AssetReference reference)
    {
        AssetRecord? record = _database.Resolve(reference);
        if (record == null || record.Type != AssetType.Material)
            throw new FileNotFoundException($"Material asset '{reference}' could not be resolved.");
        if (_runtimeMaterials.TryGetValue(record.Guid, out Material? cached))
            return cached;
        var material = new Material();
        ApplyMaterialParameters(ResolveMaterialParameters(reference), material);
        _runtimeMaterials[record.Guid] = material;
        return material;
    }

    public MaterialAsset ReloadMaterialAsset(AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        AssetRecord? record = _database.Resolve(reference);
        if (record == null || record.Type != AssetType.Material)
            throw new FileNotFoundException($"Material asset '{reference}' could not be resolved.");
        MaterialAsset refreshed = MaterialAssetSerializer.Load(record.FullPath);
        _materialAssets[record.Guid] = refreshed;
        _materialRevisions[record.Guid] = CaptureRevision(record);
        RefreshRuntimeMaterials();
        return refreshed;
    }

    public MaterialParameters ResolveMaterialParameters(AssetReference reference) =>
        MaterialAssetSerializer.Resolve(reference, LoadMaterialAsset);

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
        foreach (var guid in _vfxEffects.Keys.ToArray())
        {
            if (_database.TryGetAsset(guid, out var effectRecord) && effectRecord?.Type == AssetType.VfxEffect &&
                _vfxRevisions.TryGetValue(guid, out var previousVfx) && previousVfx == CaptureRevision(effectRecord))
                continue;
            _vfxEffects.Remove(guid); _vfxRevisions.Remove(guid); VfxRevision++;
        }
        /*
         * AssetDatabase.Scan() reports that the database changed even when the
         * contents of already-loaded assets did not. Reimporting every cached
         * FBX/GLTF/texture on each scan made project open, profile save and
         * ordinary browser refreshes increasingly expensive.
         *
         * Keep hot reload, but only touch a loaded resource when its source or
         * .meta sidecar revision actually changed.
         */
        foreach ((Guid guid, Texture2D texture) in _textures.ToArray())
        {
            if (!_database.TryGetAsset(guid, out AssetRecord? record) ||
                record == null ||
                record.Type != AssetType.Texture2D)
            {
                texture.ReplaceWithMissing();
                _textureRevisions.Remove(guid);
                continue;
            }

            AssetRevision revision = CaptureRevision(record);
            if (_textureRevisions.TryGetValue(guid, out AssetRevision previous) &&
                previous == revision)
            {
                continue;
            }

            try
            {
                texture.Reload(record.FullPath, record.Metadata.Importer.Filter);
                _textureRevisions[guid] = revision;
            }
            catch (Exception exception)
            {
                texture.ReplaceWithMissing();
                _warningSink?.Invoke($"Could not reload texture '{record.ProjectPath}': {exception.Message}");
            }
        }

        foreach ((Guid guid, ModelAsset _) in _models.ToArray())
        {
            if (!_database.TryGetAsset(guid, out AssetRecord? record) ||
                record?.Type != AssetType.Model3D)
            {
                _modelRevisions.Remove(guid);
                continue;
            }

            AssetRevision revision = CaptureRevision(record);
            if (_modelRevisions.TryGetValue(guid, out AssetRevision previous) &&
                previous == revision)
            {
                continue;
            }

            try
            {
                RefreshModel(record);
            }
            catch (Exception exception)
            {
                _warningSink?.Invoke($"Could not reimport model '{record.ProjectPath}': {exception.Message}");
            }
        }

        foreach ((Guid guid, AnimationProfile profile) in _animationProfiles.ToArray())
        {
            if (!_database.TryGetAsset(guid, out AssetRecord? record) ||
                record?.Type != AssetType.AnimationProfile)
            {
                _animationProfiles.Remove(guid);
                _animationProfileRevisions.Remove(guid);
                continue;
            }

            AssetRevision revision = CaptureRevision(record);
            if (_animationProfileRevisions.TryGetValue(guid, out AssetRevision previous) &&
                previous == revision)
            {
                continue;
            }

            try
            {
                AnimationProfile refreshed = _animationProfileImporter.Import(record);
                CopyAnimationProfile(refreshed, profile);
                _animationProfileRevisions[guid] = revision;
            }
            catch (Exception exception)
            {
                _warningSink?.Invoke($"Could not reload Animation Profile '{record.ProjectPath}': {exception.Message}");
            }
        }
        ReloadChangedMaterialAssets();
        RefreshRuntimeMaterials();
    }

    private void RefreshRuntimeMaterials()
    {
        foreach ((Guid guid, Material material) in _runtimeMaterials.ToArray())
        {
            if (!_database.TryGetAsset(guid, out AssetRecord? record) ||
                record?.Type != AssetType.Material)
            {
                _runtimeMaterials.Remove(guid);
                continue;
            }
            try
            {
                ApplyMaterialParameters(ResolveMaterialParameters(
                    new AssetReference(guid, record.ProjectPath)), material);
            }
            catch (Exception exception)
            {
                _warningSink?.Invoke($"Could not refresh material '{record.ProjectPath}': {exception.Message}");
            }
        }
    }

    public void ApplyMaterialParameters(MaterialParameters source, Material target)
    {
        source.Normalize();
        target.BaseColor = source.BaseColor;
        target.Metallic = source.Metallic;
        target.Roughness = source.Roughness;
        target.MainTexture = LoadOptionalTexture(source.BaseColorTexture);
        target.NormalTexture = LoadOptionalTexture(source.NormalTexture);
        target.NormalStrength = source.NormalStrength;
        target.DirectXNormalMap = source.NormalConvention == MaterialNormalConvention.DirectX ||
            source.NormalConvention == MaterialNormalConvention.Auto &&
            (source.NormalTexture.CachedProjectPath ?? string.Empty)
                .Contains("_NormalDX", StringComparison.OrdinalIgnoreCase);
        target.MetallicTexture = LoadOptionalTexture(source.MetallicTexture);
        target.RoughnessTexture = LoadOptionalTexture(source.RoughnessTexture);
        target.AmbientOcclusionTexture = LoadOptionalTexture(source.AmbientOcclusionTexture);
        target.AmbientOcclusionStrength = source.AmbientOcclusionStrength;
        target.PbrMapMode = source.PbrMapMode;
        target.PackedPbrTexture = LoadOptionalTexture(source.PackedPbrTexture);
        target.PackedAoChannel = source.PackedAoChannel;
        target.PackedRoughnessChannel = source.PackedRoughnessChannel;
        target.PackedMetallicChannel = source.PackedMetallicChannel;
        target.EmissionEnabled = source.EmissionEnabled;
        target.EmissionColor = source.EmissionColor;
        target.EmissionTexture = LoadOptionalTexture(source.EmissionTexture);
        target.EmissionIntensity = source.EmissionIntensity;
        target.UvTiling = source.UvTiling;
        target.UvOffset = source.UvOffset;
        target.Shading = source.Shading;
        target.DecodeColorTexturesSrgb = true;
        target.BlendMode = source.SurfaceType switch
        {
            MaterialSurfaceType.Cutout => BlendMode3D.Cutout,
            MaterialSurfaceType.Transparent => BlendMode3D.AlphaBlend,
            MaterialSurfaceType.Additive => BlendMode3D.Additive,
            _ => BlendMode3D.Opaque
        };
        target.AlphaCutoff = source.AlphaCutoff;
        target.DepthTest = source.DepthTest;
        target.DepthWriteMode = source.DepthWriteMode;
        target.CullMode = source.DoubleSided ? CullMode3D.None :
            source.CullMode == CullMode3D.None ? CullMode3D.Back : source.CullMode;
        target.FrontFace = source.FrontFace;
        target.PolygonMode = source.PolygonMode;
    }

    private Texture2D? LoadOptionalTexture(AssetReference reference) =>
        reference.IsEmpty ? null : LoadTexture(reference);

    private void ReloadChangedMaterialAssets()
    {
        foreach ((Guid guid, MaterialAsset _) in _materialAssets.ToArray())
        {
            if (!_database.TryGetAsset(guid, out AssetRecord? record) ||
                record?.Type != AssetType.Material)
            {
                _materialAssets.Remove(guid);
                _materialRevisions.Remove(guid);
                continue;
            }

            AssetRevision revision = CaptureRevision(record);
            if (_materialRevisions.TryGetValue(guid, out AssetRevision previous) &&
                previous == revision)
                continue;
            try
            {
                _materialAssets[guid] = MaterialAssetSerializer.Load(record.FullPath);
                _materialRevisions[guid] = revision;
            }
            catch (Exception exception)
            {
                _warningSink?.Invoke($"Could not reload material '{record.ProjectPath}': {exception.Message}");
            }
        }
    }

    private static AssetRevision CaptureRevision(AssetRecord record)
    {
        FileInfo asset = new(record.FullPath);
        FileInfo meta = new(record.MetaPath);

        return new AssetRevision(
            asset.Exists ? asset.LastWriteTimeUtc.Ticks : 0L,
            asset.Exists ? asset.Length : 0L,
            meta.Exists ? meta.LastWriteTimeUtc.Ticks : 0L,
            meta.Exists ? meta.Length : 0L);
    }

    private readonly record struct AssetRevision(
        long AssetWriteTicks,
        long AssetLength,
        long MetaWriteTicks,
        long MetaLength);

    private ModelAsset RefreshModel(AssetRecord record)
    {
        Guid guid = record.Guid;
        record.Metadata.ModelImporter.Normalize();

        var refreshed =
            new ModelAsset(
                ModelImporter.ForPath(record.FullPath)
                    .Import(
                        record,
                        record.Metadata.ModelImporter),
                record.Metadata.ModelImporter);

        /*
         * Baked model-owned animations survive FBX/GLTF reimport because they
         * live in project-internal GUID-keyed data rather than in the source
         * model binary.
         */
        ModelOwnedAnimationStore.MergeInto(
            ProjectRoot,
            refreshed);

        ModelAnimationMetadataStore.MergeInto(
            ProjectRoot,
            refreshed,
            _warningSink);

        ModelSocketMetadataStore.MergeInto(ProjectRoot, refreshed, _warningSink);

        _models[guid] = refreshed;
        _modelRevisions[guid] = CaptureRevision(record);
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
        target.BlendSpaces = source.BlendSpaces;
        target.Layers = source.Layers;
        target.SyncGroups = source.SyncGroups;
        target.StateGraph = source.StateGraph;
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
        target.DecodeColorTexturesSrgb = true;
        target.BlendMode = source.SurfaceType switch
        {
            MaterialSurfaceType.Cutout => BlendMode3D.Cutout,
            MaterialSurfaceType.Transparent => BlendMode3D.AlphaBlend,
            MaterialSurfaceType.Additive => BlendMode3D.Additive,
            _ => BlendMode3D.Opaque
        };
        target.AlphaCutoff = source.AlphaCutoff;
        target.CullMode = source.DoubleSided ? CullMode3D.None : CullMode3D.Back;
        target.Shading = source.Unlit ? MaterialShadingMode.Unlit : MaterialShadingMode.Lit;
        target.PackedPbrTexture = GetModelTexture(modelGuid, source.MetallicRoughnessTexture);
        target.MetallicTexture = GetModelTexture(modelGuid, source.MetallicTexture);
        target.RoughnessTexture = GetModelTexture(modelGuid, source.RoughnessTexture);
        target.PbrMapMode = source.MetallicRoughnessTexture == null ? MaterialPbrMapMode.Separate : MaterialPbrMapMode.Packed;
        target.PackedAoChannel = MaterialMapChannel.None;
        target.AmbientOcclusionTexture = GetModelTexture(modelGuid, source.AmbientOcclusionTexture);
        target.AmbientOcclusionStrength = source.AmbientOcclusionStrength;
        target.EmissionColor = source.EmissionColor;
        target.EmissionEnabled = source.EmissionColor.LengthSquared() > 0f || source.EmissionTexture != null;
        target.EmissionTexture = GetModelTexture(modelGuid, source.EmissionTexture);
    }

    private Texture2D? GetModelTexture(Guid modelGuid, ImportedTexture? source)
    {
        if (source == null || source.EncodedData.Length == 0) return null;
        var key = (modelGuid, source.Key);
        if (_modelTextures.TryGetValue(key, out Texture2D? cached))
        {
            if(!_modelTextureSources.TryGetValue(key,out var bytes)||!ReferenceEquals(bytes,source.EncodedData)){cached.ReloadEncoded(source.EncodedData);_modelTextureSources[key]=source.EncodedData;cached.EnableWorldSampling();}
            return cached;
        }

        Texture2D texture = Texture2D.FromEncodedBytes(source.EncodedData);
        texture.EnableWorldSampling();
        _modelTextures[key] = texture;
        _modelTextureSources[key]=source.EncodedData;
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
        foreach (var texture in _runtimeTextures.Values) texture.Dispose();
        _runtimeTextures.Clear();
        foreach (Mesh mesh in _modelMeshes.Values.Distinct()) mesh.Dispose();
        _modelMeshes.Clear();
        foreach (Texture2D texture in _modelTextures.Values.Distinct()) texture.Dispose();
        _modelTextures.Clear();
        _modelTextureSources.Clear();
        _modelMaterials.Clear();
        _models.Clear();
        _textureRevisions.Clear();
        _modelRevisions.Clear();
        _animationProfiles.Clear();
        _animationProfileRevisions.Clear();
        _missingTexture?.Dispose();
        _missingTexture = null;
    }
}
