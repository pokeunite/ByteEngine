using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Assets;

public sealed class ModelAsset
{
    private readonly List<ImportedAnimation> _animations;
    private readonly List<SkeletalSocketDefinition> _sockets = new();
    private readonly Dictionary<string, SkeletalSocketDefinition> _socketsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, SkeletalSocketDefinition> _socketsById = new();

    public Guid Guid { get; }

    public Guid SourceAssetGuid { get; }

    public string Name { get; }

    public IReadOnlyList<ImportedNode> Nodes { get; }

    public IReadOnlyList<ImportedMesh> Meshes { get; }

    public IReadOnlyList<ImportedMaterial> Materials { get; }

    public SkeletonAsset? Skeleton { get; }

    public IReadOnlyList<ImportedAnimation> Animations =>
        _animations;

    public IReadOnlyList<SkeletalSocketDefinition> Sockets => _sockets;

    public SkeletalSocketDefinition? FindSocket(string name) =>
        !string.IsNullOrWhiteSpace(name) && _socketsByName.TryGetValue(name.Trim(), out SkeletalSocketDefinition? socket) ? socket : null;

    public SkeletalSocketDefinition? FindSocket(Guid id) => _socketsById.GetValueOrDefault(id);

    public void ReplaceSockets(IEnumerable<SkeletalSocketDefinition> sockets)
    {
        _sockets.Clear(); _socketsByName.Clear(); _socketsById.Clear();
        foreach (SkeletalSocketDefinition socket in sockets.Select(item => item.Clone()))
        {
            _sockets.Add(socket); _socketsByName[socket.Name] = socket; _socketsById[socket.Id] = socket;
        }
    }

    /// <summary>
    /// Rig classification captured from this model's importer metadata.
    /// Existing assets remain Generic unless explicitly changed to Humanoid.
    /// </summary>
    public AnimationRigType RigType { get; }

    /// <summary>
    /// Snapshot of the source model's semantic Humanoid bone mapping.
    /// </summary>
    public HumanoidBoneMap HumanoidMapping { get; }

    /// <summary>
    /// Derived source bind/reference pose for Humanoid models.
    ///
    /// This is not persisted independently; it is rebuilt from the imported
    /// skeleton's inverse bind matrices whenever the model is imported.
    /// </summary>
    public HumanoidReferencePose? ReferenceHumanoidPose { get; }

    internal ModelAsset(
        ImportedModel imported)
        : this(
            imported,
            null)
    {
    }

    internal ModelAsset(
        ImportedModel imported,
        ModelImporterSettings? settings)
    {
        ArgumentNullException.ThrowIfNull(
            imported);

        settings ??=
            new ModelImporterSettings();

        settings.Normalize();

        Guid =
            imported.Guid;

        SourceAssetGuid =
            imported.SourceAssetGuid;

        Name =
            imported.Name;

        Nodes =
            imported.Nodes;

        Meshes =
            imported.Meshes;

        Materials =
            imported.Materials;

        Skeleton =
            imported.Skeleton;

        _animations =
            imported.Animations
                .ToList();

        RigType =
            settings.RigType;

        HumanoidMapping =
            settings.HumanoidMapping.Clone();

        if (RigType ==
                AnimationRigType.Humanoid &&
            Skeleton !=
                null)
        {
            ReferenceHumanoidPose =
                HumanoidReferencePose.Capture(
                    Skeleton,
                    HumanoidMapping);
        }
    }

    /// <summary>
    /// Adds or replaces a runtime/generated animation clip without changing the
    /// source model file. Retargeted model-owned clips use the same collection
    /// as native imported clips so existing pickers and Asset Browser expansion
    /// keep working without another animation asset type.
    /// </summary>
    internal ImportedAnimation RegisterRuntimeAnimation(
        ImportedAnimation animation)
    {
        ArgumentNullException.ThrowIfNull(
            animation);

        int existing =
            _animations.FindIndex(
                candidate =>
                    string.Equals(
                        candidate.Key,
                        animation.Key,
                        StringComparison.Ordinal));

        if (existing >=
            0)
        {
            _animations[existing] =
                animation;

            return animation;
        }

        _animations.Add(
            animation);

        return animation;
    }

    /// <summary>
    /// Removes one generated/model-owned animation by its stable generated key.
    /// Native FBX/GLTF clips are never removed through this path because the
    /// caller only receives keys persisted in ModelOwnedAnimationStore.
    /// </summary>
    internal bool RemoveRuntimeAnimation(
        string animationKey)
    {
        if (string.IsNullOrWhiteSpace(
                animationKey))
        {
            return false;
        }

        int index =
            _animations.FindIndex(
                candidate =>
                    string.Equals(
                        candidate.Key,
                        animationKey,
                        StringComparison.Ordinal));

        if (index <
            0)
        {
            return false;
        }

        _animations.RemoveAt(
            index);

        return true;
    }
}
