using System.Numerics;

using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Bakes a source Humanoid animation into target-skeleton node tracks.
///
/// ByteEngine's existing SkeletalMeshRenderer already knows how to blend,
/// loop, skin, expose sockets and extract root motion from ImportedAnimation.
/// C9E therefore retargets once into a runtime clip cache instead of adding a
/// second skeletal-animation player.
/// </summary>
public static class HumanoidRetargetClipBuilder
{
    public const float DefaultSamplesPerSecond =
        60.0f;

    public static ImportedAnimation Build(
        SkeletonAsset sourceSkeleton,
        HumanoidBoneMap sourceMapping,
        HumanoidReferencePose sourceReferencePose,
        ImportedAnimation sourceAnimation,
        SkeletonAsset targetSkeleton,
        HumanoidBoneMap targetMapping,
        HumanoidReferencePose targetReferencePose,
        IReadOnlyList<ImportedNode> targetNodes,
        Guid sourceModelGuid,
        Guid targetModelGuid,
        string runtimeClipName,
        float samplesPerSecond = DefaultSamplesPerSecond)
    {
        return
            Build(
                sourceSkeleton,
                sourceMapping,
                sourceReferencePose,
                sourceAnimation,
                targetSkeleton,
                targetMapping,
                targetReferencePose,
                targetNodes,
                Array.Empty<ImportedMesh>(),
                sourceModelGuid,
                targetModelGuid,
                runtimeClipName,
                samplesPerSecond);
    }

    public static ImportedAnimation Build(
        SkeletonAsset sourceSkeleton,
        HumanoidBoneMap sourceMapping,
        HumanoidReferencePose sourceReferencePose,
        ImportedAnimation sourceAnimation,
        SkeletonAsset targetSkeleton,
        HumanoidBoneMap targetMapping,
        HumanoidReferencePose targetReferencePose,
        IReadOnlyList<ImportedNode> targetNodes,
        IReadOnlyList<ImportedMesh> targetMeshes,
        Guid sourceModelGuid,
        Guid targetModelGuid,
        string runtimeClipName,
        float samplesPerSecond = DefaultSamplesPerSecond)
    {
        ArgumentNullException.ThrowIfNull(
            sourceSkeleton);

        ArgumentNullException.ThrowIfNull(
            sourceMapping);

        ArgumentNullException.ThrowIfNull(
            sourceReferencePose);

        ArgumentNullException.ThrowIfNull(
            sourceAnimation);

        ArgumentNullException.ThrowIfNull(
            targetSkeleton);

        ArgumentNullException.ThrowIfNull(
            targetMapping);

        ArgumentNullException.ThrowIfNull(
            targetReferencePose);

        ArgumentNullException.ThrowIfNull(
            targetNodes);

        ArgumentNullException.ThrowIfNull(
            targetMeshes);

        if (!sourceReferencePose.IsReady)
        {
            throw new InvalidOperationException(
                "Source Humanoid reference pose is not ready.");
        }

        if (!targetReferencePose.IsReady)
        {
            throw new InvalidOperationException(
                "Target Humanoid reference pose is not ready.");
        }

        if (targetNodes.Count ==
            0)
        {
            throw new InvalidOperationException(
                "Target model has no imported node hierarchy.");
        }

        samplesPerSecond =
            float.IsFinite(
                samplesPerSecond)
                ? Math.Clamp(
                    samplesPerSecond,
                    1.0f,
                    240.0f)
                : DefaultSamplesPerSecond;

        string safeName =
            string.IsNullOrWhiteSpace(
                runtimeClipName)
                ? sourceAnimation.Name
                : runtimeClipName.Trim();

        string runtimeKey =
            $"humanoid-retarget:{sourceModelGuid:N}:{sourceAnimation.Key}:{targetModelGuid:N}";

        NodeRuntime targetRuntime =
            BuildNodeRuntime(
                targetSkeleton,
                targetNodes,
                targetMeshes);

        int[] trackedBoneIndices =
            targetMapping.Bones
                .Values
                .Where(
                    name =>
                        !string.IsNullOrWhiteSpace(
                            name))
                .Select(
                    name =>
                        targetRuntime.BoneIndexByName.TryGetValue(
                            name,
                            out int index)
                            ? index
                            : -1)
                .Where(
                    index =>
                        index >=
                        0)
                .Distinct()
                .OrderBy(
                    index =>
                        index)
                .ToArray();

        if (trackedBoneIndices.Length ==
            0)
        {
            throw new InvalidOperationException(
                "Target Humanoid mapping contains no bones present in the target skeleton.");
        }

        float duration =
            Math.Max(
                sourceAnimation.Duration,
                0.0f);

        float[] sampleTimes =
            BuildSampleTimes(
                duration,
                samplesPerSecond);

        var channels =
            trackedBoneIndices
                .ToDictionary(
                    boneIndex =>
                        boneIndex,
                    boneIndex =>
                        new MutableChannel(
                            targetSkeleton.Bones[
                                boneIndex].Name));

        foreach (float sampleTime
                 in sampleTimes)
        {
            HumanoidRetargetPose retargetPose =
                HumanoidRetargeter.Retarget(
                    sourceSkeleton,
                    sourceMapping,
                    sourceReferencePose,
                    sourceAnimation,
                    sampleTime,
                    targetSkeleton,
                    targetMapping,
                    targetReferencePose,
                    loop:
                        false);

            Matrix4x4[] nodeGlobals =
                BuildNodeGlobals(
                    targetRuntime,
                    retargetPose.ModelMatrices);

            foreach (int boneIndex
                     in trackedBoneIndices)
            {
                int nodeIndex =
                    targetRuntime.BoneNodeIndices[
                        boneIndex];

                if (nodeIndex <
                        0 ||
                    nodeIndex >=
                        targetNodes.Count)
                {
                    continue;
                }

                Matrix4x4 local =
                    ToNodeLocal(
                        targetRuntime,
                        nodeGlobals,
                        nodeIndex);

                if (!Matrix4x4.Decompose(
                        local,
                        out Vector3 scale,
                        out Quaternion rotation,
                        out Vector3 translation))
                {
                    continue;
                }

                rotation =
                    NormalizeSafe(
                        rotation);

                MutableChannel channel =
                    channels[boneIndex];

                channel.Translation.Keys.Add(
                    new ImportedVectorKey(
                        sampleTime,
                        translation,
                        Vector3.Zero,
                        Vector3.Zero));

                channel.Rotation.Keys.Add(
                    new ImportedQuaternionKey(
                        sampleTime,
                        rotation,
                        default,
                        default));

                channel.Scale.Keys.Add(
                    new ImportedVectorKey(
                        sampleTime,
                        scale,
                        Vector3.Zero,
                        Vector3.Zero));
            }
        }

        return
            new ImportedAnimation
            {
                Key =
                    runtimeKey,

                Name =
                    safeName,

                Duration =
                    duration,

                Channels =
                    channels
                        .Values
                        .Where(
                            channel =>
                                channel.Translation.Keys.Count >
                                0)
                        .Select(
                            channel =>
                                channel.ToImported())
                        .ToList()
            };
    }

    private static NodeRuntime BuildNodeRuntime(
        SkeletonAsset targetSkeleton,
        IReadOnlyList<ImportedNode> targetNodes,
        IReadOnlyList<ImportedMesh> targetMeshes)
    {
        var byKey =
            targetNodes
                .Select(
                    (node, index) =>
                        new
                        {
                            node.Key,
                            Index =
                                index
                        })
                .ToDictionary(
                    item =>
                        item.Key,
                    item =>
                        item.Index,
                    StringComparer.Ordinal);

        var parentIndices =
            new int[
                targetNodes.Count];

        Array.Fill(
            parentIndices,
            -1);

        for (int index = 0;
             index < targetNodes.Count;
             index++)
        {
            string? parentKey =
                targetNodes[index]
                    .ParentKey;

            if (parentKey !=
                    null &&
                byKey.TryGetValue(
                    parentKey,
                    out int parent))
            {
                parentIndices[index] =
                    parent;
            }
        }

        var nodeByName =
            targetNodes
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

        var boneIndexByName =
            targetSkeleton.Bones
                .Select(
                    (bone, index) =>
                        new
                        {
                            bone.Name,
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

        var boneNodeIndices =
            new int[
                targetSkeleton.Bones.Count];

        Array.Fill(
            boneNodeIndices,
            -1);

        var nodeBoneIndices =
            new int[
                targetNodes.Count];

        Array.Fill(
            nodeBoneIndices,
            -1);

        for (int boneIndex = 0;
             boneIndex <
                targetSkeleton.Bones.Count;
             boneIndex++)
        {
            string boneName =
                targetSkeleton.Bones[
                    boneIndex].Name;

            if (!nodeByName.TryGetValue(
                    boneName,
                    out int nodeIndex))
            {
                continue;
            }

            boneNodeIndices[
                boneIndex] =
                nodeIndex;

            nodeBoneIndices[
                nodeIndex] =
                boneIndex;
        }

        Matrix4x4[] referenceNodeGlobals =
            BuildReferenceNodeGlobals(
                targetNodes,
                parentIndices);

        Matrix4x4[] boneMeshFrames =
            ResolveBoneMeshFrames(
                targetSkeleton,
                targetNodes,
                targetMeshes,
                referenceNodeGlobals);

        var bindToNodeCorrections =
            new Matrix4x4[
                targetSkeleton.Bones.Count];

        Array.Fill(
            bindToNodeCorrections,
            Matrix4x4.Identity);

        for (int boneIndex = 0;
             boneIndex <
                targetSkeleton.Bones.Count;
             boneIndex++)
        {
            if (boneIndex <
                    boneMeshFrames.Length)
            {
                bindToNodeCorrections[
                    boneIndex] =
                    boneMeshFrames[
                        boneIndex];

                continue;
            }

            int nodeIndex =
                boneNodeIndices[
                    boneIndex];

            if (nodeIndex <
                    0 ||
                nodeIndex >=
                    referenceNodeGlobals.Length)
            {
                continue;
            }

            bindToNodeCorrections[
                boneIndex] =
                targetSkeleton.Bones[
                    boneIndex].BindPose *
                referenceNodeGlobals[
                    nodeIndex];
        }

        return
            new NodeRuntime(
                targetNodes,
                parentIndices,
                boneNodeIndices,
                nodeBoneIndices,
                boneIndexByName,
                bindToNodeCorrections);
    }

    private static Matrix4x4[] ResolveBoneMeshFrames(
        SkeletonAsset skeleton,
        IReadOnlyList<ImportedNode> nodes,
        IReadOnlyList<ImportedMesh> meshes,
        IReadOnlyList<Matrix4x4> nodeGlobals)
    {
        if (meshes.Count ==
                0 ||
            nodes.Count ==
                0 ||
            nodeGlobals.Count !=
                nodes.Count)
        {
            return
                Array.Empty<Matrix4x4>();
        }

        var meshFrames =
            new Dictionary<string, Matrix4x4>(
                StringComparer.Ordinal);

        for (int nodeIndex = 0;
             nodeIndex <
                nodes.Count;
             nodeIndex++)
        {
            foreach (string meshKey
                     in nodes[nodeIndex]
                         .MeshKeys)
            {
                meshFrames.TryAdd(
                    meshKey,
                    nodeGlobals[nodeIndex]);
            }
        }

        var frames =
            new Matrix4x4[
                skeleton.Bones.Count];

        Array.Fill(
            frames,
            Matrix4x4.Identity);

        var resolved =
            new bool[
                skeleton.Bones.Count];

        Matrix4x4 fallback =
            Matrix4x4.Identity;

        bool hasFallback =
            false;

        foreach (ImportedMesh mesh
                 in meshes)
        {
            if (!meshFrames.TryGetValue(
                    mesh.Key,
                    out Matrix4x4 meshFrame))
            {
                continue;
            }

            int vertexCount =
                mesh.Vertices.Length /
                8;

            if (vertexCount <=
                    0 ||
                mesh.JointIndices.Length !=
                    vertexCount ||
                mesh.JointWeights.Length !=
                    vertexCount)
            {
                continue;
            }

            if (!hasFallback)
            {
                fallback =
                    meshFrame;

                hasFallback =
                    true;
            }

            for (int vertexIndex = 0;
                 vertexIndex <
                    vertexCount;
                 vertexIndex++)
            {
                Vector4 joints =
                    mesh.JointIndices[
                        vertexIndex];

                Vector4 weights =
                    mesh.JointWeights[
                        vertexIndex];

                Capture(
                    joints.X,
                    weights.X);

                Capture(
                    joints.Y,
                    weights.Y);

                Capture(
                    joints.Z,
                    weights.Z);

                Capture(
                    joints.W,
                    weights.W);
            }

            void Capture(
                float sourceIndex,
                float weight)
            {
                if (!float.IsFinite(
                        sourceIndex) ||
                    !float.IsFinite(
                        weight) ||
                    weight <=
                        0.00001f)
                {
                    return;
                }

                int boneIndex =
                    (int)MathF.Round(
                        sourceIndex);

                if (boneIndex <
                        0 ||
                    boneIndex >=
                        frames.Length ||
                    resolved[boneIndex])
                {
                    return;
                }

                frames[boneIndex] =
                    meshFrame;

                resolved[boneIndex] =
                    true;
            }
        }

        if (!hasFallback)
        {
            return
                Array.Empty<Matrix4x4>();
        }

        for (int boneIndex = 0;
             boneIndex <
                skeleton.Bones.Count;
             boneIndex++)
        {
            if (!resolved[boneIndex])
            {
                continue;
            }

            int parent =
                skeleton.Bones[boneIndex]
                    .ParentIndex;

            var visited =
                new HashSet<int>();

            while (parent >=
                       0 &&
                   parent <
                       skeleton.Bones.Count &&
                   visited.Add(
                       parent))
            {
                if (!resolved[parent])
                {
                    frames[parent] =
                        frames[boneIndex];

                    resolved[parent] =
                        true;
                }

                parent =
                    skeleton.Bones[parent]
                        .ParentIndex;
            }
        }

        for (int boneIndex = 0;
             boneIndex <
                frames.Length;
             boneIndex++)
        {
            if (!resolved[boneIndex])
            {
                frames[boneIndex] =
                    fallback;
            }
        }

        return frames;
    }

    private static Matrix4x4[] BuildReferenceNodeGlobals(
        IReadOnlyList<ImportedNode> nodes,
        IReadOnlyList<int> parentIndices)
    {
        var globals =
            new Matrix4x4[
                nodes.Count];

        var state =
            new byte[
                nodes.Count];

        for (int index = 0;
             index <
                nodes.Count;
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
                return
                    globals[index];
            }

            if (state[index] ==
                1)
            {
                globals[index] =
                    nodes[index]
                        .LocalTransform;

                state[index] =
                    2;

                return
                    globals[index];
            }

            state[index] =
                1;

            int parent =
                parentIndices[
                    index];

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

            return
                globals[index];
        }
    }

    private static Matrix4x4[] BuildNodeGlobals(
        NodeRuntime runtime,
        IReadOnlyList<Matrix4x4> retargetedBoneModels)
    {
        var globals =
            new Matrix4x4[
                runtime.Nodes.Count];

        var state =
            new byte[
                runtime.Nodes.Count];

        for (int index = 0;
             index < runtime.Nodes.Count;
             index++)
        {
            ResolveGlobal(
                runtime,
                retargetedBoneModels,
                index,
                globals,
                state);
        }

        return globals;
    }

    private static Matrix4x4 ResolveGlobal(
        NodeRuntime runtime,
        IReadOnlyList<Matrix4x4> retargetedBoneModels,
        int index,
        Matrix4x4[] globals,
        byte[] state)
    {
        if (state[index] ==
            2)
        {
            return globals[index];
        }

        if (state[index] ==
            1)
        {
            globals[index] =
                runtime.Nodes[index]
                    .LocalTransform;

            state[index] =
                2;

            return globals[index];
        }

        state[index] =
            1;

        int boneIndex =
            runtime.NodeBoneIndices[
                index];

        if (boneIndex >=
                0 &&
            boneIndex <
                retargetedBoneModels.Count)
        {
            Matrix4x4 correction =
                boneIndex <
                    runtime.BindToNodeCorrections.Length
                    ? runtime.BindToNodeCorrections[
                        boneIndex]
                    : Matrix4x4.Identity;

            globals[index] =
                retargetedBoneModels[
                    boneIndex] *
                correction;

            state[index] =
                2;

            return globals[index];
        }

        int parent =
            runtime.ParentIndices[
                index];

        globals[index] =
            parent >=
                0 &&
            parent <
                runtime.Nodes.Count
                ? runtime.Nodes[index]
                    .LocalTransform *
                    ResolveGlobal(
                        runtime,
                        retargetedBoneModels,
                        parent,
                        globals,
                        state)
                : runtime.Nodes[index]
                    .LocalTransform;

        state[index] =
            2;

        return globals[index];
    }

    private static Matrix4x4 ToNodeLocal(
        NodeRuntime runtime,
        IReadOnlyList<Matrix4x4> globals,
        int nodeIndex)
    {
        int parent =
            runtime.ParentIndices[
                nodeIndex];

        if (parent <
                0 ||
            parent >=
                globals.Count)
        {
            return globals[
                nodeIndex];
        }

        if (!Matrix4x4.Invert(
                globals[parent],
                out Matrix4x4 inverseParent))
        {
            return globals[
                nodeIndex];
        }

        return
            globals[nodeIndex] *
            inverseParent;
    }

    private static float[] BuildSampleTimes(
        float duration,
        float samplesPerSecond)
    {
        if (duration <=
            0.000001f)
        {
            return
                new[]
                {
                    0.0f
                };
        }

        int intervals =
            Math.Max(
                1,
                (int)MathF.Ceiling(
                    duration *
                    samplesPerSecond));

        var result =
            new float[
                intervals +
                1];

        for (int index = 0;
             index <=
                intervals;
             index++)
        {
            result[index] =
                index ==
                    intervals
                    ? duration
                    : duration *
                        index /
                        intervals;
        }

        return result;
    }

    private static Quaternion NormalizeSafe(
        Quaternion value) =>
        value.LengthSquared() >
            0.000001f
            ? Quaternion.Normalize(
                value)
            : Quaternion.Identity;

    private sealed class MutableChannel
    {
        public string NodeName { get; }

        public ImportedVectorTrack Translation { get; } =
            new()
            {
                Interpolation =
                    ImportedAnimationInterpolation.Linear
            };

        public ImportedQuaternionTrack Rotation { get; } =
            new()
            {
                Interpolation =
                    ImportedAnimationInterpolation.Linear
            };

        public ImportedVectorTrack Scale { get; } =
            new()
            {
                Interpolation =
                    ImportedAnimationInterpolation.Linear
            };

        public MutableChannel(
            string nodeName)
        {
            NodeName =
                nodeName;
        }

        public ImportedAnimationChannel ToImported() =>
            new()
            {
                NodeName =
                    NodeName,

                Translation =
                    Translation,

                Rotation =
                    Rotation,

                Scale =
                    Scale
            };
    }

    private sealed record NodeRuntime(
        IReadOnlyList<ImportedNode> Nodes,
        int[] ParentIndices,
        int[] BoneNodeIndices,
        int[] NodeBoneIndices,
        IReadOnlyDictionary<string, int> BoneIndexByName,
        Matrix4x4[] BindToNodeCorrections);
}
