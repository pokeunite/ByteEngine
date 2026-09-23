using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Normalizes retargeted Humanoid translation so locomotion lives on the target
/// skeleton root exactly once.
///
/// The pose retargeter works in semantic model space. That is correct for body
/// placement, but Root and Hips are both semantic translation carriers. When a
/// source has authored root motion, the Hips model-space delta already contains
/// the Root delta; copying both produces doubled travel. When a source has no
/// explicit Root track (common in animation-only/Mixamo sources), locomotion can
/// live entirely on Hips, while ByteEngine's runtime root-motion extractor reads
/// the target skeleton root.
///
/// This pass mirrors the established engine practice of treating root motion as
/// a separate trajectory:
/// - explicit target-root travel wins when present;
/// - otherwise meaningful Hips horizontal travel is promoted to the root;
/// - Hips is re-solved relative to that root so travel is never applied twice;
/// - small in-place Hips sway is left untouched.
/// </summary>
public static class HumanoidRetargetRootMotion
{
    private const float ExplicitRootTravelThreshold =
        0.005f;

    private const float PelvisPromotionThreshold =
        0.02f;

    public static ImportedAnimation Normalize(
        ModelAsset sourceModel,
        ModelAsset targetModel,
        ImportedAnimation animation)
    {
        ArgumentNullException.ThrowIfNull(
            sourceModel);

        return Normalize(
            sourceModel.RootMotionSource,
            targetModel,
            animation);
    }

    /// <summary>
    /// Explicit-policy overload used by importer authoring so a not-yet-saved
    /// Root Motion Source selection can be previewed/baked deterministically.
    /// </summary>
    public static ImportedAnimation Normalize(
        AnimationRootMotionSource rootMotionSource,
        ModelAsset targetModel,
        ImportedAnimation animation)
    {
        ArgumentNullException.ThrowIfNull(
            targetModel);

        ArgumentNullException.ThrowIfNull(
            animation);

        SkeletonAsset? skeleton =
            targetModel.Skeleton;

        if (skeleton ==
                null ||
            targetModel.Nodes.Count ==
                0 ||
            animation.Channels.Count ==
                0)
        {
            return animation;
        }

        string? hipsName =
            targetModel.HumanoidMapping.GetBoneName(
                HumanoidBone.Hips);

        if (string.IsNullOrWhiteSpace(
                hipsName))
        {
            return animation;
        }

        Dictionary<string, int> nodeByName =
            targetModel.Nodes
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
                .GroupBy(
                    item =>
                        item.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First().Index,
                    StringComparer.OrdinalIgnoreCase);

        if (!nodeByName.TryGetValue(
                hipsName,
                out int hipsNodeIndex))
        {
            return animation;
        }

        string? rootName =
            ResolveTargetRootName(
                targetModel,
                skeleton,
                nodeByName);

        if (string.IsNullOrWhiteSpace(
                rootName) ||
            !nodeByName.TryGetValue(
                rootName,
                out int rootNodeIndex) ||
            rootNodeIndex ==
                hipsNodeIndex)
        {
            return animation;
        }

        int[] parentIndices =
            BuildParentIndices(
                targetModel.Nodes);

        if (!IsAncestor(
                rootNodeIndex,
                hipsNodeIndex,
                parentIndices))
        {
            return animation;
        }

        float[] sampleTimes =
            ResolveSampleTimes(
                animation,
                hipsName,
                rootName);

        if (sampleTimes.Length <
            2)
        {
            return animation;
        }

        Matrix4x4[] firstGlobals =
            BuildGlobals(
                targetModel.Nodes,
                parentIndices,
                animation,
                sampleTimes[0]);

        Matrix4x4[] lastGlobals =
            BuildGlobals(
                targetModel.Nodes,
                parentIndices,
                animation,
                sampleTimes[^1]);

        Vector3 rootStart =
            firstGlobals[
                rootNodeIndex].Translation;

        Vector3 rootEnd =
            lastGlobals[
                rootNodeIndex].Translation;

        Vector3 hipsStart =
            firstGlobals[
                hipsNodeIndex].Translation;

        Vector3 hipsEnd =
            lastGlobals[
                hipsNodeIndex].Translation;

        Vector3 rootNetHorizontal =
            Horizontal(
                rootEnd -
                rootStart);

        Vector3 hipsNetHorizontal =
            Horizontal(
                hipsEnd -
                hipsStart);

        bool rootHasMeaningfulTravel =
            rootNetHorizontal.LengthSquared() >=
            ExplicitRootTravelThreshold *
            ExplicitRootTravelThreshold;

        bool hipsHasMeaningfulTravel =
            hipsNetHorizontal.LengthSquared() >=
            PelvisPromotionThreshold *
            PelvisPromotionThreshold;

        bool explicitRootMotion;
        bool generateFromPelvis;

        switch (rootMotionSource)
        {
            case AnimationRootMotionSource.SkeletonRoot:
                explicitRootMotion =
                    rootHasMeaningfulTravel;

                generateFromPelvis =
                    false;

                break;

            case AnimationRootMotionSource.Hips:
                explicitRootMotion =
                    false;

                generateFromPelvis =
                    hipsHasMeaningfulTravel;

                break;

            default:
                explicitRootMotion =
                    rootHasMeaningfulTravel;

                generateFromPelvis =
                    !explicitRootMotion &&
                    hipsHasMeaningfulTravel;

                break;
        }

        if (!explicitRootMotion &&
            !generateFromPelvis)
        {
            return animation;
        }

        var rootTranslationKeys =
            new List<ImportedVectorKey>(
                sampleTimes.Length);

        var hipsTranslationKeys =
            new List<ImportedVectorKey>(
                sampleTimes.Length);

        foreach (float sampleTime
                 in sampleTimes)
        {
            Matrix4x4[] originalGlobals =
                BuildGlobals(
                    targetModel.Nodes,
                    parentIndices,
                    animation,
                    sampleTime);

            Matrix4x4 originalRootGlobal =
                originalGlobals[
                    rootNodeIndex];

            Matrix4x4 originalHipsGlobal =
                originalGlobals[
                    hipsNodeIndex];

            Vector3 rootDelta =
                originalRootGlobal.Translation -
                rootStart;

            Vector3 pelvisDelta =
                originalHipsGlobal.Translation -
                hipsStart;

            Matrix4x4 rootLocalOverride;

            if (generateFromPelvis)
            {
                Vector3 promotedTravel =
                    Horizontal(
                        pelvisDelta);

                Vector3 desiredRootPosition =
                    originalRootGlobal.Translation;

                desiredRootPosition.X =
                    rootStart.X +
                    promotedTravel.X;

                desiredRootPosition.Z =
                    rootStart.Z +
                    promotedTravel.Z;

                Matrix4x4 desiredRootGlobal =
                    WithTranslation(
                        originalRootGlobal,
                        desiredRootPosition);

                Matrix4x4 rootParentGlobal =
                    ParentGlobal(
                        originalGlobals,
                        parentIndices,
                        rootNodeIndex);

                rootLocalOverride =
                    ToLocal(
                        desiredRootGlobal,
                        rootParentGlobal);

                if (!TryTranslation(
                        rootLocalOverride,
                        out Vector3 rootLocalTranslation))
                {
                    return animation;
                }

                rootTranslationKeys.Add(
                    new ImportedVectorKey(
                        sampleTime,
                        rootLocalTranslation,
                        Vector3.Zero,
                        Vector3.Zero));
            }
            else
            {
                Matrix4x4 originalRootLocal =
                    SampleNodeLocal(
                        targetModel.Nodes[
                            rootNodeIndex],
                        animation,
                        sampleTime);

                rootLocalOverride =
                    originalRootLocal;
            }

            Matrix4x4[] adjustedGlobals =
                generateFromPelvis
                    ? BuildGlobals(
                        targetModel.Nodes,
                        parentIndices,
                        animation,
                        sampleTime,
                        rootNodeIndex,
                        rootLocalOverride)
                    : originalGlobals;

            Matrix4x4 desiredHipsGlobal =
                originalHipsGlobal;

            if (explicitRootMotion)
            {
                /*
                 * Hips was retargeted from a model-space delta, so it already
                 * contains the source Root displacement. Its target parent Root
                 * contains the same displacement. Remove that duplicate once.
                 *
                 * Use the complete root translation delta here, including Y,
                 * because ByteEngine intentionally keeps vertical root movement
                 * in the rendered pose even though world root motion is
                 * horizontal-only.
                 */
                Vector3 correctedHipsPosition =
                    originalHipsGlobal.Translation -
                    rootDelta;

                desiredHipsGlobal =
                    WithTranslation(
                        originalHipsGlobal,
                        correctedHipsPosition);
            }

            Matrix4x4 hipsParentGlobal =
                ParentGlobal(
                    adjustedGlobals,
                    parentIndices,
                    hipsNodeIndex);

            Matrix4x4 desiredHipsLocal =
                ToLocal(
                    desiredHipsGlobal,
                    hipsParentGlobal);

            if (!TryTranslation(
                    desiredHipsLocal,
                    out Vector3 hipsLocalTranslation))
            {
                return animation;
            }

            hipsTranslationKeys.Add(
                new ImportedVectorKey(
                    sampleTime,
                    hipsLocalTranslation,
                    Vector3.Zero,
                    Vector3.Zero));
        }

        if (generateFromPelvis)
        {
            ReplaceTranslationTrack(
                animation,
                rootName,
                new ImportedVectorTrack
                {
                    Interpolation =
                        ImportedAnimationInterpolation.Linear
                },
                rootTranslationKeys);
        }

        ReplaceTranslationTrack(
            animation,
            hipsName,
            new ImportedVectorTrack
            {
                Interpolation =
                    ImportedAnimationInterpolation.Linear
            },
            hipsTranslationKeys);

        return animation;
    }

    private static string? ResolveTargetRootName(
        ModelAsset targetModel,
        SkeletonAsset skeleton,
        IReadOnlyDictionary<string, int> nodeByName)
    {
        string? mappedRoot =
            targetModel.HumanoidMapping.GetBoneName(
                HumanoidBone.Root);

        if (!string.IsNullOrWhiteSpace(
                mappedRoot) &&
            nodeByName.ContainsKey(
                mappedRoot))
        {
            return mappedRoot;
        }

        foreach (Bone bone
                 in skeleton.Bones)
        {
            if (bone.ParentIndex <
                    0 &&
                nodeByName.ContainsKey(
                    bone.Name))
            {
                return bone.Name;
            }
        }

        return null;
    }

    private static float[] ResolveSampleTimes(
        ImportedAnimation animation,
        string hipsName,
        string rootName)
    {
        var times =
            new SortedSet<float>();

        AddTimes(
            FindChannel(
                animation,
                hipsName)?.Translation,
            times);

        AddTimes(
            FindChannel(
                animation,
                rootName)?.Translation,
            times);

        if (times.Count ==
            0)
        {
            foreach (ImportedAnimationChannel channel
                     in animation.Channels)
            {
                AddTimes(
                    channel.Translation,
                    times);
            }
        }

        times.Add(
            0.0f);

        if (animation.Duration >
            0.000001f)
        {
            times.Add(
                animation.Duration);
        }

        return times
            .Where(
                time =>
                    float.IsFinite(
                        time))
            .Select(
                time =>
                    Math.Clamp(
                        time,
                        0.0f,
                        Math.Max(
                            animation.Duration,
                            0.0f)))
            .Distinct()
            .OrderBy(
                time =>
                    time)
            .ToArray();
    }

    private static void AddTimes(
        ImportedVectorTrack? track,
        ISet<float> times)
    {
        if (track ==
            null)
        {
            return;
        }

        foreach (ImportedVectorKey key
                 in track.Keys)
        {
            if (float.IsFinite(
                    key.Time))
            {
                times.Add(
                    key.Time);
            }
        }
    }

    private static ImportedAnimationChannel? FindChannel(
        ImportedAnimation animation,
        string nodeName) =>
        animation.Channels.FirstOrDefault(
            channel =>
                string.Equals(
                    channel.NodeName,
                    nodeName,
                    StringComparison.Ordinal))
        ?? animation.Channels.FirstOrDefault(
            channel =>
                string.Equals(
                    channel.NodeName,
                    nodeName,
                    StringComparison.OrdinalIgnoreCase));

    private static void ReplaceTranslationTrack(
        ImportedAnimation animation,
        string nodeName,
        ImportedVectorTrack replacement,
        IReadOnlyList<ImportedVectorKey> keys)
    {
        replacement.Keys.AddRange(
            keys);

        int index =
            animation.Channels.FindIndex(
                channel =>
                    string.Equals(
                        channel.NodeName,
                        nodeName,
                        StringComparison.OrdinalIgnoreCase));

        if (index >=
            0)
        {
            ImportedAnimationChannel existing =
                animation.Channels[
                    index];

            animation.Channels[index] =
                new ImportedAnimationChannel
                {
                    NodeName =
                        existing.NodeName,

                    Translation =
                        replacement,

                    Rotation =
                        existing.Rotation,

                    Scale =
                        existing.Scale
                };

            return;
        }

        animation.Channels.Add(
            new ImportedAnimationChannel
            {
                NodeName =
                    nodeName,

                Translation =
                    replacement
            });
    }

    private static int[] BuildParentIndices(
        IReadOnlyList<ImportedNode> nodes)
    {
        Dictionary<string, int> byKey =
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
                .ToDictionary(
                    item =>
                        item.Key,
                    item =>
                        item.Index,
                    StringComparer.Ordinal);

        var result =
            new int[
                nodes.Count];

        Array.Fill(
            result,
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
                byKey.TryGetValue(
                    parentKey,
                    out int parentIndex))
            {
                result[index] =
                    parentIndex;
            }
        }

        return result;
    }

    private static bool IsAncestor(
        int ancestor,
        int descendant,
        IReadOnlyList<int> parentIndices)
    {
        int current =
            descendant;

        var visited =
            new HashSet<int>();

        while (current >=
                   0 &&
               current <
                   parentIndices.Count &&
               visited.Add(
                   current))
        {
            if (current ==
                ancestor)
            {
                return true;
            }

            current =
                parentIndices[
                    current];
        }

        return false;
    }

    private static Matrix4x4[] BuildGlobals(
        IReadOnlyList<ImportedNode> nodes,
        IReadOnlyList<int> parentIndices,
        ImportedAnimation animation,
        float sampleTime,
        int overrideNodeIndex = -1,
        Matrix4x4? overrideLocal = null)
    {
        var locals =
            new Matrix4x4[
                nodes.Count];

        for (int index = 0;
             index < nodes.Count;
             index++)
        {
            locals[index] =
                index ==
                    overrideNodeIndex &&
                overrideLocal.HasValue
                    ? overrideLocal.Value
                    : SampleNodeLocal(
                        nodes[index],
                        animation,
                        sampleTime);
        }

        var globals =
            new Matrix4x4[
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

        Matrix4x4 Resolve(
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
                    locals[
                        index];

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
                    ? locals[index] *
                      Resolve(
                          parent)
                    : locals[index];

            state[index] =
                2;

            return globals[
                index];
        }
    }

    private static Matrix4x4 SampleNodeLocal(
        ImportedNode node,
        ImportedAnimation animation,
        float sampleTime)
    {
        Matrix4x4 fallback =
            node.LocalTransform;

        if (!Matrix4x4.Decompose(
                fallback,
                out Vector3 fallbackScale,
                out Quaternion fallbackRotation,
                out Vector3 fallbackTranslation))
        {
            return fallback;
        }

        ImportedAnimationChannel? channel =
            animation.FindChannel(
                node.Name);

        if (channel ==
            null)
        {
            return fallback;
        }

        Vector3 translation =
            AnimationPoseSampler.Sample(
                channel.Translation,
                sampleTime,
                fallbackTranslation);

        Quaternion rotation =
            AnimationPoseSampler.Sample(
                channel.Rotation,
                sampleTime,
                fallbackRotation);

        Vector3 scale =
            AnimationPoseSampler.Sample(
                channel.Scale,
                sampleTime,
                fallbackScale);

        return
            Matrix4x4.CreateScale(
                scale) *
            Matrix4x4.CreateFromQuaternion(
                rotation) *
            Matrix4x4.CreateTranslation(
                translation);
    }

    private static Matrix4x4 ParentGlobal(
        IReadOnlyList<Matrix4x4> globals,
        IReadOnlyList<int> parentIndices,
        int nodeIndex)
    {
        int parent =
            nodeIndex <
                parentIndices.Count
                ? parentIndices[
                    nodeIndex]
                : -1;

        return
            parent >=
                0 &&
            parent <
                globals.Count
                ? globals[
                    parent]
                : Matrix4x4.Identity;
    }

    private static Matrix4x4 ToLocal(
        Matrix4x4 desiredGlobal,
        Matrix4x4 parentGlobal)
    {
        if (!Matrix4x4.Invert(
                parentGlobal,
                out Matrix4x4 inverseParent))
        {
            return desiredGlobal;
        }

        return
            desiredGlobal *
            inverseParent;
    }

    private static Matrix4x4 WithTranslation(
        Matrix4x4 matrix,
        Vector3 translation)
    {
        matrix.M41 =
            translation.X;

        matrix.M42 =
            translation.Y;

        matrix.M43 =
            translation.Z;

        return matrix;
    }

    private static bool TryTranslation(
        Matrix4x4 matrix,
        out Vector3 translation)
    {
        if (Matrix4x4.Decompose(
                matrix,
                out _,
                out _,
                out translation) &&
            IsFinite(
                translation))
        {
            return true;
        }

        translation =
            matrix.Translation;

        return
            IsFinite(
                translation);
    }

    private static Vector3 Horizontal(
        Vector3 value)
    {
        value.Y =
            0.0f;

        return value;
    }

    private static bool IsFinite(
        Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);
}
