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
    /// Effective rig classification for this import. When Auto Detect is active,
    /// a source that passes ByteEngine's complete Humanoid auto-map/validation
    /// is promoted to Humanoid before editor/runtime consumers see it.
    /// </summary>
    public AnimationRigType RigType { get; }

    /// <summary>
    /// True when this load selected Humanoid through automatic rig detection.
    /// </summary>
    public bool HumanoidRigWasAutoDetected { get; }

    /// <summary>
    /// Whether automatic Humanoid detection is active in importer metadata.
    /// </summary>
    public bool AutoDetectHumanoidRig { get; }

    /// <summary>
    /// Whether embedded animation tracks were enabled by the importer.
    /// </summary>
    public bool ImportAnimations { get; }

    /// <summary>
    /// Source trajectory policy used by Humanoid retargeting.
    /// </summary>
    public AnimationRootMotionSource RootMotionSource { get; }

    /// <summary>
    /// Default target-bake sampling frequency when this asset is the source.
    /// </summary>
    public float RetargetSamplesPerSecond { get; }

    /// <summary>
    /// Optional external Humanoid rig that supplies bind/reference data when
    /// this asset carries animation tracks but no usable skeleton of its own.
    /// </summary>
    public AssetReference AnimationSourceRigModel { get; }

    /// <summary>
    /// Importer/editor convenience target used to prefill retarget authoring.
    /// </summary>
    public AssetReference DefaultRetargetTargetModel { get; }

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

        HumanoidRigWasAutoDetected =
            TryApplyAutomaticHumanoidRig(
                settings,
                imported);

        AutoDetectHumanoidRig =
            settings.AutoDetectHumanoidRig;

        ImportAnimations =
            settings.ImportAnimations;

        RootMotionSource =
            settings.RootMotionSource;

        RetargetSamplesPerSecond =
            settings.RetargetSamplesPerSecond;

        AnimationSourceRigModel =
            settings.AnimationSourceRigModel;

        DefaultRetargetTargetModel =
            settings.DefaultRetargetTargetModel;

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
    /// Auto is deliberately conservative: only assets with a skeleton, a
    /// complete recognized Humanoid map and a valid hierarchy are promoted.
    /// Animation clips are NOT required, because a character may legitimately
    /// be imported as a target-only model. Props, animals and incomplete
    /// skeletons remain Generic.
    ///
    /// Settings are updated in memory as well as the ModelAsset so every editor
    /// surface sees the detected result immediately. The Import Settings Apply
    /// button persists that result to the .meta sidecar.
    /// </summary>
    private static bool TryApplyAutomaticHumanoidRig(
        ModelImporterSettings settings,
        ImportedModel imported)
    {
        if (!settings.AutoDetectHumanoidRig ||
            imported.Skeleton ==
                null)
        {
            return false;
        }

        HumanoidBoneMap detected =
            HumanoidRigMapper.AutoMap(
                imported.Skeleton);

        /*
         * Preserve legacy/manual Humanoid work. Old metadata predates the Auto
         * flag, so a custom valid mapping may deserialize with Auto enabled by
         * the new default. If that mapping differs from what Auto Map would
         * produce, treat it as an explicit/manual rig and leave it untouched.
         */
        if (settings.RigType ==
                AnimationRigType.Humanoid &&
            settings.HumanoidMapping.MappedCount >
                0 &&
            !MappingsEquivalent(
                settings.HumanoidMapping,
                detected))
        {
            HumanoidRigValidationResult existingValidation =
                HumanoidRigMapper.Validate(
                    imported.Skeleton,
                    settings.HumanoidMapping);

            HumanoidRigDiagnosticReport existingDiagnostics =
                HumanoidRigDiagnostics.Analyze(
                    imported.Skeleton,
                    settings.HumanoidMapping);

            if (existingValidation.IsReady &&
                existingDiagnostics.Errors.Count ==
                    0)
            {
                settings.AutoDetectHumanoidRig =
                    false;

                return false;
            }
        }

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                imported.Skeleton,
                detected);

        if (!validation.IsReady)
        {
            settings.RigType =
                AnimationRigType.Generic;

            return false;
        }

        HumanoidRigDiagnosticReport diagnostics =
            HumanoidRigDiagnostics.Analyze(
                imported.Skeleton,
                detected);

        if (diagnostics.Errors.Count >
            0)
        {
            settings.RigType =
                AnimationRigType.Generic;

            return false;
        }

        settings.RigType =
            AnimationRigType.Humanoid;

        settings.HumanoidMapping =
            detected;

        return true;
    }

    private static bool MappingsEquivalent(
        HumanoidBoneMap left,
        HumanoidBoneMap right)
    {
        left.Normalize();
        right.Normalize();

        foreach (HumanoidBone bone
                 in Enum.GetValues<HumanoidBone>())
        {
            if (!string.Equals(
                    left.GetBoneName(
                        bone),
                    right.GetBoneName(
                        bone),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
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
