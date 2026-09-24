using System.Numerics;

using Assimp;

using NumericsMatrix4x4 =
    System.Numerics.Matrix4x4;

using NumericsQuaternion =
    System.Numerics.Quaternion;

using NumericsVector3 =
    System.Numerics.Vector3;

namespace ByteEngine.Core.Assets.Importers;

/// <summary>
/// Compatibility layer for animation-source files.
///
/// Normal model imports still go through the existing importer untouched.
/// When an FBX contains a valid hierarchy + animation but deliberately contains
/// no render mesh (for example a Mixamo "Without Skin" download), this importer
/// keeps the hierarchy/animation and derives a skeleton reference pose directly
/// from the imported nodes.
///
/// glTF animation-only sources are handled similarly when the source contains
/// animated nodes but no Skin object.
///
/// The resulting asset is still a normal ModelAsset so the existing Humanoid
/// mapper, GUID metadata and animation pickers keep one code path.
/// </summary>
internal sealed class AnimationSourceCompatibleImporter
    : ModelImporter
{
    private readonly ModelImporter _inner;

    public AnimationSourceCompatibleImporter(
        ModelImporter inner)
    {
        ArgumentNullException.ThrowIfNull(
            inner);

        _inner =
            inner;
    }

    public override IReadOnlyCollection<string> Extensions =>
        _inner.Extensions;

    public override ImportedModel Import(
        AssetRecord source,
        ModelImporterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            settings);

        settings.Normalize();

        ImportedModel imported;

        try
        {
            imported =
                _inner.Import(
                    source,
                    settings);
        }
        catch (InvalidDataException exception)
            when (IsFbx(source) &&
                  exception.Message.Contains(
                      "no mesh geometry",
                      StringComparison.OrdinalIgnoreCase))
        {
            imported =
                ImportAnimationOnlyFbx(
                    source, settings);
        }

        /*
         * Mesh-backed files normally already provide a skin-derived Skeleton.
         * Animation-only sources often do not, so derive the reference skeleton
         * from their imported node hierarchy before optional animation stripping.
         */
        if (imported.Skeleton ==
                null &&
            imported.Nodes.Count >
                0 &&
            HasUsableAnimation(
                imported.Animations))
        {
            SkeletonAsset? derived =
                DeriveSkeletonFromNodes(
                    source,
                    imported.Nodes);

            if (derived !=
                null)
            {
                imported =
                    CopyWith(
                        imported,
                        skeleton:
                            derived);
            }
        }

        if (!settings.ImportAnimations &&
            imported.Animations.Count >
                0)
        {
            imported =
                CopyWith(
                    imported,
                    animations:
                        new List<ImportedAnimation>());
        }

        return imported;
    }

    private static ImportedModel CopyWith(
        ImportedModel source,
        SkeletonAsset? skeleton = null,
        List<ImportedAnimation>? animations = null)
    {
        return
            new ImportedModel
            {
                Guid =
                    source.Guid,

                SourceAssetGuid =
                    source.SourceAssetGuid,

                Name =
                    source.Name,

                Nodes =
                    source.Nodes,

                Meshes =
                    source.Meshes,

                Materials =
                    source.Materials,

                Skeleton =
                    skeleton ??
                    source.Skeleton,

                Animations =
                    animations ??
                    source.Animations
            };
    }

    private static bool IsFbx(
        AssetRecord source) =>
        string.Equals(
            Path.GetExtension(
                source.FullPath),
            ".fbx",
            StringComparison.OrdinalIgnoreCase);

    private static bool HasUsableAnimation(
        IReadOnlyList<ImportedAnimation> animations) =>
        animations.Any(
            animation =>
                animation.Channels.Any(
                    channel =>
                        channel.HasKeys));

    private static ImportedModel ImportAnimationOnlyFbx(
        AssetRecord source, ModelImporterSettings settings)
    {
        using var context =
            new AssimpContext();

        Assimp.Scene scene =
            context.ImportFile(
                source.FullPath,
                (PostProcessSteps)0)
            ?? throw new InvalidDataException(
                $"Assimp returned no scene for '{source.ProjectPath}'.");

        if (scene.RootNode ==
            null)
        {
            throw new InvalidDataException(
                $"FBX '{source.ProjectPath}' contains no root node.");
        }

        List<ImportedAnimation> animations =
            ReadAnimations(
                source,
                scene);

        if (!HasUsableAnimation(
                animations))
        {
            throw new InvalidDataException(
                $"FBX '{source.ProjectPath}' contains no mesh geometry and no usable animation tracks.");
        }

        var nodes =
            new List<ImportedNode>();

        int nodeIndex =
            0;

        ReadNodeRecursive(
            scene.RootNode,
            null,
            nodes,
            ref nodeIndex);

        SkeletonAsset? skeleton =
            DeriveSkeletonFromNodes(
                source,
                nodes);

        if (skeleton ==
            null)
        {
            throw new InvalidDataException(
                $"FBX '{source.ProjectPath}' contains animation tracks but no usable node hierarchy.");
        }

        return ImportedModelSpace.Apply(new ImportedModel
            {
                Guid =
                    source.Guid,

                SourceAssetGuid =
                    source.Guid,

                Name =
                    Path.GetFileNameWithoutExtension(
                        source.ProjectPath),

                Nodes =
                    nodes,

                Meshes =
                    new List<ImportedMesh>(),

                Materials =
                    new List<ImportedMaterial>(),

                Skeleton =
                    skeleton,

                Animations =
                    animations
            }, ImportedModelSpace.FbxCorrection(scene, settings.ImportScale));
    }

    private static void ReadNodeRecursive(
        Node node,
        string? parentKey,
        ICollection<ImportedNode> output,
        ref int nodeIndex)
    {
        int currentIndex =
            nodeIndex++;

        string name =
            NameOr(
                node.Name,
                "Node",
                currentIndex);

        string key =
            $"node:{currentIndex}:{name}";

        output.Add(
            new ImportedNode
            {
                Key =
                    key,

                Name =
                    name,

                ParentKey =
                    parentKey,

                LocalTransform =
                    ToNumerics(
                        node.Transform),

                MeshKeys =
                    new List<string>()
            });

        foreach (Node child
                 in node.Children)
        {
            ReadNodeRecursive(
                child,
                key,
                output,
                ref nodeIndex);
        }
    }

    /// <summary>
    /// Builds inverse-bind matrices from the imported reference hierarchy.
    ///
    /// Mesh-backed assets get bind data from their Skin/bone weights in the
    /// normal importers. Animation-only sources have no such data, so the
    /// hierarchy's reference transforms are the only authoritative bind pose.
    /// </summary>
    internal static SkeletonAsset? DeriveSkeletonFromNodes(
        AssetRecord source,
        IReadOnlyList<ImportedNode> nodes)
    {
        if (nodes.Count ==
            0)
        {
            return null;
        }

        Dictionary<string, int> nodeByKey =
            nodes
                .Select(
                    (node, index) =>
                        new
                        {
                            node.Key,
                            Index =
                                index
                        })
                .Where(
                    item =>
                        !string.IsNullOrWhiteSpace(
                            item.Key))
                .GroupBy(
                    item =>
                        item.Key,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First().Index,
                    StringComparer.Ordinal);

        int[] parentIndices =
            new int[
                nodes.Count];

        Array.Fill(
            parentIndices,
            -1);

        for (int index = 0;
             index < nodes.Count;
             index++)
        {
            string? parentKey =
                nodes[index]
                    .ParentKey;

            if (!string.IsNullOrWhiteSpace(
                    parentKey) &&
                nodeByKey.TryGetValue(
                    parentKey,
                    out int parentIndex))
            {
                parentIndices[index] =
                    parentIndex;
            }
        }

        NumericsMatrix4x4[] globals =
            BuildReferenceGlobals(
                nodes,
                parentIndices);

        var includedNodeIndices =
            nodes
                .Select(
                    (node, index) =>
                        new
                        {
                            node.Name,
                            Index =
                                index
                        })
                .Where(
                    item =>
                        !string.IsNullOrWhiteSpace(
                            item.Name))
                .Where(item => nodes[item.Index].Key != ImportedModelSpace.CorrectionNodeKey)
                .GroupBy(
                    item =>
                        item.Name,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        group.First().Index)
                .OrderBy(
                    index =>
                        index)
                .ToArray();

        if (includedNodeIndices.Length ==
            0)
        {
            return null;
        }

        Dictionary<int, int> boneIndexByNode =
            includedNodeIndices
                .Select(
                    (nodeIndex, boneIndex) =>
                        new
                        {
                            NodeIndex =
                                nodeIndex,
                            BoneIndex =
                                boneIndex
                        })
                .ToDictionary(
                    item =>
                        item.NodeIndex,
                    item =>
                        item.BoneIndex);

        var bones =
            new List<Bone>(
                includedNodeIndices.Length);

        foreach (int nodeIndex
                 in includedNodeIndices)
        {
            int parentNode =
                parentIndices[
                    nodeIndex];

            int parentBone =
                -1;

            var visited =
                new HashSet<int>();

            while (parentNode >=
                       0 &&
                   parentNode <
                       nodes.Count &&
                   visited.Add(
                       parentNode))
            {
                if (boneIndexByNode.TryGetValue(
                        parentNode,
                        out parentBone))
                {
                    break;
                }

                parentNode =
                    parentIndices[
                        parentNode];
            }

            NumericsMatrix4x4 bindPose =
                NumericsMatrix4x4.Invert(
                    globals[
                        nodeIndex],
                    out NumericsMatrix4x4 inverse) &&
                IsFinite(
                    inverse)
                    ? inverse
                    : NumericsMatrix4x4.Identity;

            bones.Add(
                new Bone
                {
                    Name =
                        nodes[
                            nodeIndex].Name,

                    ParentIndex =
                        parentBone,

                    BindPose =
                        bindPose
                });
        }

        return
            new SkeletonAsset
            {
                Key =
                    $"{source.Guid:N}:skeleton:{Path.GetFileNameWithoutExtension(source.ProjectPath)}:0",

                Name =
                    Path.GetFileNameWithoutExtension(
                        source.ProjectPath),

                Bones =
                    bones
            };
    }

    private static NumericsMatrix4x4[] BuildReferenceGlobals(
        IReadOnlyList<ImportedNode> nodes,
        IReadOnlyList<int> parentIndices)
    {
        var globals =
            new NumericsMatrix4x4[
                nodes.Count];

        var state =
            new byte[
                nodes.Count];

        for (int index = 0;
             index < nodes.Count;
             index++)
        {
            Resolve(
                index);
        }

        return globals;

        NumericsMatrix4x4 Resolve(
            int index)
        {
            if (state[index] ==
                2)
            {
                return globals[
                    index];
            }

            if (state[index] ==
                1)
            {
                globals[index] =
                    nodes[index]
                        .LocalTransform;

                state[index] =
                    2;

                return globals[
                    index];
            }

            state[index] =
                1;

            int parent =
                index <
                    parentIndices.Count
                    ? parentIndices[
                        index]
                    : -1;

            globals[index] =
                parent >=
                    0 &&
                parent <
                    nodes.Count
                    ? nodes[index]
                        .LocalTransform *
                      Resolve(
                          parent)
                    : nodes[index]
                        .LocalTransform;

            state[index] =
                2;

            return globals[
                index];
        }
    }

    private static List<ImportedAnimation> ReadAnimations(
        AssetRecord source,
        Assimp.Scene scene)
    {
        var result =
            new List<ImportedAnimation>();

        for (int index = 0;
             index < scene.AnimationCount;
             index++)
        {
            Assimp.Animation animation =
                scene.Animations[
                    index];

            string name =
                NameOr(
                    animation.Name,
                    "Animation",
                    index);

            double ticksPerSecond =
                animation.TicksPerSecond >
                    0.000001
                    ? animation.TicksPerSecond
                    : 25.0;

            float duration =
                animation.DurationInTicks >
                    0.0
                    ? (float)(
                        animation.DurationInTicks /
                        ticksPerSecond)
                    : 0.0f;

            var channels =
                new List<ImportedAnimationChannel>();

            foreach (NodeAnimationChannel channel
                     in animation.NodeAnimationChannels)
            {
                ImportedVectorTrack? translation =
                    ReadVectorTrack(
                        channel.PositionKeys,
                        ticksPerSecond);

                ImportedQuaternionTrack? rotation =
                    ReadQuaternionTrack(
                        channel.RotationKeys,
                        ticksPerSecond);

                ImportedVectorTrack? scale =
                    ReadVectorTrack(
                        channel.ScalingKeys,
                        ticksPerSecond);

                var importedChannel =
                    new ImportedAnimationChannel
                    {
                        NodeName =
                            channel.NodeName,

                        Translation =
                            translation,

                        Rotation =
                            rotation,

                        Scale =
                            scale
                    };

                if (!importedChannel.HasKeys)
                {
                    continue;
                }

                channels.Add(
                    importedChannel);

                duration =
                    Math.Max(
                        duration,
                        MaxChannelTime(
                            importedChannel));
            }

            if (channels.Count ==
                0)
            {
                continue;
            }

            result.Add(
                new ImportedAnimation
                {
                    Key =
                        $"{source.Guid:N}:animation:{name}:{index}",

                    Name =
                        name,

                    Duration =
                        duration,

                    Channels =
                        channels
                });
        }

        return result;
    }

    private static ImportedVectorTrack? ReadVectorTrack(
        IReadOnlyList<VectorKey> keys,
        double ticksPerSecond)
    {
        if (keys.Count ==
            0)
        {
            return null;
        }

        var track =
            new ImportedVectorTrack
            {
                Interpolation =
                    MapInterpolation(
                        keys[0].Interpolation)
            };

        foreach (VectorKey key
                 in keys)
        {
            NumericsVector3 value =
                key.Value;

            track.Keys.Add(
                new ImportedVectorKey(
                    (float)(
                        key.Time /
                        ticksPerSecond),
                    value,
                    NumericsVector3.Zero,
                    NumericsVector3.Zero));
        }

        return track;
    }

    private static ImportedQuaternionTrack? ReadQuaternionTrack(
        IReadOnlyList<QuaternionKey> keys,
        double ticksPerSecond)
    {
        if (keys.Count ==
            0)
        {
            return null;
        }

        var track =
            new ImportedQuaternionTrack
            {
                Interpolation =
                    MapInterpolation(
                        keys[0].Interpolation)
            };

        foreach (QuaternionKey key
                 in keys)
        {
            NumericsQuaternion value =
                key.Value;

            track.Keys.Add(
                new ImportedQuaternionKey(
                    (float)(
                        key.Time /
                        ticksPerSecond),
                    value.LengthSquared() >
                        0.000001f
                        ? NumericsQuaternion.Normalize(
                            value)
                        : NumericsQuaternion.Identity,
                    default,
                    default));
        }

        return track;
    }

    private static ImportedAnimationInterpolation MapInterpolation(
        AnimationInterpolation interpolation)
    {
        return interpolation switch
        {
            AnimationInterpolation.Step =>
                ImportedAnimationInterpolation.Step,

            _ =>
                ImportedAnimationInterpolation.Linear
        };
    }

    private static float MaxChannelTime(
        ImportedAnimationChannel channel)
    {
        float result =
            0.0f;

        if (channel.Translation?.Keys.Count >
            0)
        {
            result =
                Math.Max(
                    result,
                    channel.Translation.Keys[^1].Time);
        }

        if (channel.Rotation?.Keys.Count >
            0)
        {
            result =
                Math.Max(
                    result,
                    channel.Rotation.Keys[^1].Time);
        }

        if (channel.Scale?.Keys.Count >
            0)
        {
            result =
                Math.Max(
                    result,
                    channel.Scale.Keys[^1].Time);
        }

        return result;
    }

    private static NumericsMatrix4x4 ToNumerics(
        NumericsMatrix4x4 matrix)
    {
        return
            NumericsMatrix4x4.Transpose(
                matrix);
    }

    private static string NameOr(
        string? name,
        string fallback,
        int index) =>
        string.IsNullOrWhiteSpace(
            name)
            ? $"{fallback} {index}"
            : name;

    private static bool IsFinite(
        NumericsMatrix4x4 value) =>
        float.IsFinite(value.M11) &&
        float.IsFinite(value.M12) &&
        float.IsFinite(value.M13) &&
        float.IsFinite(value.M14) &&
        float.IsFinite(value.M21) &&
        float.IsFinite(value.M22) &&
        float.IsFinite(value.M23) &&
        float.IsFinite(value.M24) &&
        float.IsFinite(value.M31) &&
        float.IsFinite(value.M32) &&
        float.IsFinite(value.M33) &&
        float.IsFinite(value.M34) &&
        float.IsFinite(value.M41) &&
        float.IsFinite(value.M42) &&
        float.IsFinite(value.M43) &&
        float.IsFinite(value.M44);
}
