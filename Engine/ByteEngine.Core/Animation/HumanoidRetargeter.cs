using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Animation;

/// <summary>
/// One sampled Humanoid retarget result for a target skeleton.
/// </summary>
public sealed class HumanoidRetargetPose
{
    public IReadOnlyList<Matrix4x4> LocalMatrices { get; }

    public IReadOnlyList<Matrix4x4> ModelMatrices { get; }

    public float SourceTime { get; }

    public float TranslationScale { get; }

    internal HumanoidRetargetPose(
        IReadOnlyList<Matrix4x4> localMatrices,
        IReadOnlyList<Matrix4x4> modelMatrices,
        float sourceTime,
        float translationScale)
    {
        LocalMatrices = localMatrices;
        ModelMatrices = modelMatrices;
        SourceTime = sourceTime;
        TranslationScale = translationScale;
    }
}

/// <summary>
/// ByteEngine Humanoid source -> target pose conversion core.
///
/// Retargeting is performed in semantic Humanoid/model space rather than by
/// copying local bone rotations. This keeps animation reusable across rigs with
/// different bone names, proportions, helper nodes and coordinate frames.
///
/// Rest-pose differences need more than a rotation-delta copy. For articulated
/// limbs this implementation derives an anatomical frame from the animated
/// source chain (limb direction + bend-plane normal), remaps that frame through
/// the source/target Humanoid body frames, and solves the target limb from its
/// own reference pose. This prevents incompatible source/target local bone-axis
/// twist from corkscrewing elbows, wrists, knees and feet.
/// </summary>
public static class HumanoidRetargeter
{
    public static HumanoidRetargetPose Retarget(
        ModelAsset sourceModel,
        ImportedAnimation sourceAnimation,
        float time,
        ModelAsset targetModel,
        bool loop = true)
    {
        ArgumentNullException.ThrowIfNull(sourceModel);
        ArgumentNullException.ThrowIfNull(sourceAnimation);
        ArgumentNullException.ThrowIfNull(targetModel);

        if (sourceModel.RigType != AnimationRigType.Humanoid)
        {
            throw new InvalidOperationException(
                $"Source model '{sourceModel.Name}' is not a Humanoid rig.");
        }

        if (targetModel.RigType != AnimationRigType.Humanoid)
        {
            throw new InvalidOperationException(
                $"Target model '{targetModel.Name}' is not a Humanoid rig.");
        }

        if (sourceModel.Skeleton == null ||
            sourceModel.ReferenceHumanoidPose == null)
        {
            throw new InvalidOperationException(
                $"Source model '{sourceModel.Name}' does not have a valid Humanoid skeleton/reference pose.");
        }

        if (targetModel.Skeleton == null ||
            targetModel.ReferenceHumanoidPose == null)
        {
            throw new InvalidOperationException(
                $"Target model '{targetModel.Name}' does not have a valid Humanoid skeleton/reference pose.");
        }

        return Retarget(
            sourceModel.Skeleton,
            sourceModel.HumanoidMapping,
            sourceModel.ReferenceHumanoidPose,
            sourceAnimation,
            sourceModel.Nodes,
            sourceModel.Meshes,
            time,
            targetModel.Skeleton,
            targetModel.HumanoidMapping,
            targetModel.ReferenceHumanoidPose,
            loop);
    }

    /// <summary>
    /// Importer-neutral compatibility overload used by existing synthetic tests.
    /// </summary>
    public static HumanoidRetargetPose Retarget(
        SkeletonAsset sourceSkeleton,
        HumanoidBoneMap sourceMapping,
        HumanoidReferencePose sourceReferencePose,
        ImportedAnimation sourceAnimation,
        float time,
        SkeletonAsset targetSkeleton,
        HumanoidBoneMap targetMapping,
        HumanoidReferencePose targetReferencePose,
        bool loop = true)
    {
        ArgumentNullException.ThrowIfNull(sourceSkeleton);
        ArgumentNullException.ThrowIfNull(sourceMapping);
        ArgumentNullException.ThrowIfNull(sourceReferencePose);
        ArgumentNullException.ThrowIfNull(sourceAnimation);
        ArgumentNullException.ThrowIfNull(targetSkeleton);
        ArgumentNullException.ThrowIfNull(targetMapping);
        ArgumentNullException.ThrowIfNull(targetReferencePose);

        float sampleTime =
            ResolveSampleTime(
                sourceAnimation.Duration,
                time,
                loop);

        Matrix4x4[] sourceReferenceModels =
            BuildReferenceModelMatrices(
                sourceSkeleton);

        Matrix4x4[] sourceReferenceLocals =
            BuildReferenceLocalMatrices(
                sourceSkeleton);

        Matrix4x4[] sourceCurrentLocals =
            SampleSourceSkeletonLocals(
                sourceSkeleton,
                sourceAnimation,
                sampleTime,
                sourceReferenceLocals);

        Matrix4x4[] sourceCurrentModels =
            BuildModelMatrices(
                sourceSkeleton,
                sourceCurrentLocals);

        return RetargetFromSourceModels(
            sourceSkeleton,
            sourceMapping,
            sourceReferencePose,
            sourceReferenceModels,
            sourceCurrentModels,
            sampleTime,
            targetSkeleton,
            targetMapping,
            targetReferencePose);
    }

    /// <summary>
    /// FBX/runtime-aware overload. The complete ImportedNode hierarchy is
    /// evaluated first so FBX helper/pre-rotation nodes participate in the
    /// source pose before conversion into SkeletonAsset bind space.
    /// </summary>
    public static HumanoidRetargetPose Retarget(
        SkeletonAsset sourceSkeleton,
        HumanoidBoneMap sourceMapping,
        HumanoidReferencePose sourceReferencePose,
        ImportedAnimation sourceAnimation,
        IReadOnlyList<ImportedNode> sourceNodes,
        IReadOnlyList<ImportedMesh> sourceMeshes,
        float time,
        SkeletonAsset targetSkeleton,
        HumanoidBoneMap targetMapping,
        HumanoidReferencePose targetReferencePose,
        bool loop = true)
    {
        ArgumentNullException.ThrowIfNull(sourceSkeleton);
        ArgumentNullException.ThrowIfNull(sourceMapping);
        ArgumentNullException.ThrowIfNull(sourceReferencePose);
        ArgumentNullException.ThrowIfNull(sourceAnimation);
        ArgumentNullException.ThrowIfNull(sourceNodes);
        ArgumentNullException.ThrowIfNull(sourceMeshes);
        ArgumentNullException.ThrowIfNull(targetSkeleton);
        ArgumentNullException.ThrowIfNull(targetMapping);
        ArgumentNullException.ThrowIfNull(targetReferencePose);

        float sampleTime =
            ResolveSampleTime(
                sourceAnimation.Duration,
                time,
                loop);

        Matrix4x4[] sourceReferenceModels =
            BuildReferenceModelMatrices(
                sourceSkeleton);

        Matrix4x4[] sourceCurrentModels =
            sourceNodes.Count > 0
                ? SampleSourceNodeModelsInSkeletonSpace(
                    sourceSkeleton,
                    sourceAnimation,
                    sourceNodes,
                    sourceMeshes,
                    sampleTime,
                    sourceReferenceModels)
                : BuildModelMatrices(
                    sourceSkeleton,
                    SampleSourceSkeletonLocals(
                        sourceSkeleton,
                        sourceAnimation,
                        sampleTime,
                        BuildReferenceLocalMatrices(
                            sourceSkeleton)));

        return RetargetFromSourceModels(
            sourceSkeleton,
            sourceMapping,
            sourceReferencePose,
            sourceReferenceModels,
            sourceCurrentModels,
            sampleTime,
            targetSkeleton,
            targetMapping,
            targetReferencePose);
    }

    private static HumanoidRetargetPose RetargetFromSourceModels(
        SkeletonAsset sourceSkeleton,
        HumanoidBoneMap sourceMapping,
        HumanoidReferencePose sourceReferencePose,
        IReadOnlyList<Matrix4x4> sourceReferenceModels,
        IReadOnlyList<Matrix4x4> sourceCurrentModels,
        float sampleTime,
        SkeletonAsset targetSkeleton,
        HumanoidBoneMap targetMapping,
        HumanoidReferencePose targetReferencePose)
    {
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

        Matrix4x4[] targetReferenceLocals =
            BuildReferenceLocalMatrices(
                targetSkeleton);

        Matrix4x4[] targetReferenceModels =
            BuildReferenceModelMatrices(
                targetSkeleton);

        Dictionary<string, int> sourceIndices =
            BuildBoneIndex(
                sourceSkeleton);

        Dictionary<string, int> targetIndices =
            BuildBoneIndex(
                targetSkeleton);

        Dictionary<int, HumanoidBone> targetSemantics =
            BuildTargetSemanticIndex(
                targetMapping,
                targetIndices);

        float translationScale =
            CalculateTranslationScale(
                sourceReferencePose,
                targetReferencePose);

        HumanoidBodyFrame sourceBodyFrame =
            BuildBodyFrame(
                sourceReferencePose);

        HumanoidBodyFrame targetBodyFrame =
            BuildBodyFrame(
                targetReferencePose);

        var desiredModelRotations =
            new Quaternion?[
                targetSkeleton.Bones.Count];

        var desiredModelTranslationDeltas =
            new Vector3[
                targetSkeleton.Bones.Count];

        var hasTranslationDelta =
            new bool[
                targetSkeleton.Bones.Count];

        for (int targetIndex = 0;
             targetIndex < targetSkeleton.Bones.Count;
             targetIndex++)
        {
            if (!targetSemantics.TryGetValue(
                    targetIndex,
                    out HumanoidBone semanticBone))
            {
                continue;
            }

            if (!sourceMapping.TryGetBoneName(
                    semanticBone,
                    out string sourceBoneName) ||
                !sourceIndices.TryGetValue(
                    sourceBoneName,
                    out int sourceIndex) ||
                sourceIndex < 0 ||
                sourceIndex >= sourceReferenceModels.Count ||
                sourceIndex >= sourceCurrentModels.Count)
            {
                continue;
            }

            if (!TryDecompose(
                    sourceReferenceModels[sourceIndex],
                    out _,
                    out Quaternion sourceReferenceModelRotation,
                    out Vector3 sourceReferenceModelTranslation) ||
                !TryDecompose(
                    sourceCurrentModels[sourceIndex],
                    out _,
                    out Quaternion sourceCurrentModelRotation,
                    out Vector3 sourceCurrentModelTranslation) ||
                !TryDecompose(
                    targetReferenceModels[targetIndex],
                    out _,
                    out Quaternion targetReferenceModelRotation,
                    out _))
            {
                continue;
            }

            Quaternion modelDelta =
                RelativeRotation(
                    sourceReferenceModelRotation,
                    sourceCurrentModelRotation);

            Quaternion targetModelDelta =
                RemapRotationDelta(
                    modelDelta,
                    sourceBodyFrame,
                    targetBodyFrame);

            Quaternion candidateModelRotation =
                ApplyRotationDelta(
                    targetReferenceModelRotation,
                    targetModelDelta);

            /*
             * A source/target rest-pose mismatch is not only a swing problem.
             * Rigs such as SYNTY and Manny can encode different axial twist in
             * their local bone frames. Copying the quaternion twist after merely
             * aiming the limb direction produces corkscrewed elbows/wrists and
             * splayed knees/feet.
             *
             * Solve articulated limbs from two geometric signals instead:
             *  1) outgoing joint direction = swing
             *  2) bend-plane normal       = anatomical twist reference
             *
             * Both are remapped through canonical Humanoid body space, so this
             * is independent of local source/target bone-axis conventions.
             */
            desiredModelRotations[targetIndex] =
                SolveAnimatedLimbFrame(
                    semanticBone,
                    sourceIndex,
                    targetIndex,
                    candidateModelRotation,
                    targetReferenceModelRotation,
                    sourceMapping,
                    sourceIndices,
                    sourceCurrentModels,
                    targetMapping,
                    targetIndices,
                    targetReferenceModels,
                    sourceBodyFrame,
                    targetBodyFrame);

            if (semanticBone == HumanoidBone.Root ||
                semanticBone == HumanoidBone.Hips)
            {
                Vector3 sourceTranslationDelta =
                    sourceCurrentModelTranslation -
                    sourceReferenceModelTranslation;

                desiredModelTranslationDeltas[targetIndex] =
                    RemapVector(
                        sourceTranslationDelta,
                        sourceBodyFrame,
                        targetBodyFrame) *
                    translationScale;

                hasTranslationDelta[targetIndex] =
                    true;
            }
        }

        Matrix4x4[] targetLocals =
            targetReferenceLocals.ToArray();

        var targetModels =
            new Matrix4x4[
                targetSkeleton.Bones.Count];

        var resolved =
            new bool[
                targetSkeleton.Bones.Count];

        var resolving =
            new bool[
                targetSkeleton.Bones.Count];

        for (int targetIndex = 0;
             targetIndex < targetSkeleton.Bones.Count;
             targetIndex++)
        {
            ResolveTargetPose(
                targetIndex);
        }

        return new HumanoidRetargetPose(
            targetLocals,
            targetModels,
            sampleTime,
            translationScale);

        Matrix4x4 ResolveTargetPose(
            int targetIndex)
        {
            if (resolved[targetIndex])
            {
                return targetModels[targetIndex];
            }

            if (resolving[targetIndex])
            {
                targetModels[targetIndex] =
                    targetLocals[targetIndex];

                resolved[targetIndex] =
                    true;

                return targetModels[targetIndex];
            }

            resolving[targetIndex] =
                true;

            int parentIndex =
                targetSkeleton.Bones[targetIndex]
                    .ParentIndex;

            bool hasParent =
                parentIndex >= 0 &&
                parentIndex < targetSkeleton.Bones.Count;

            Matrix4x4 parentModel =
                hasParent
                    ? ResolveTargetPose(
                        parentIndex)
                    : Matrix4x4.Identity;

            if (!TryDecompose(
                    targetReferenceLocals[targetIndex],
                    out Vector3 targetScale,
                    out Quaternion targetReferenceLocalRotation,
                    out Vector3 targetTranslation))
            {
                targetLocals[targetIndex] =
                    targetReferenceLocals[targetIndex];

                targetModels[targetIndex] =
                    hasParent
                        ? targetLocals[targetIndex] *
                          parentModel
                        : targetLocals[targetIndex];

                resolving[targetIndex] =
                    false;

                resolved[targetIndex] =
                    true;

                return targetModels[targetIndex];
            }

            Quaternion targetLocalRotation =
                targetReferenceLocalRotation;

            Quaternion? desiredModelRotation =
                desiredModelRotations[targetIndex];

            if (desiredModelRotation.HasValue)
            {
                if (hasParent &&
                    TryDecompose(
                        parentModel,
                        out _,
                        out Quaternion parentModelRotation,
                        out _))
                {
                    targetLocalRotation =
                        ModelToLocalRotation(
                            desiredModelRotation.Value,
                            parentModelRotation);
                }
                else
                {
                    targetLocalRotation =
                        NormalizeSafe(
                            desiredModelRotation.Value);
                }
            }

            if (hasTranslationDelta[targetIndex])
            {
                Vector3 modelDelta =
                    desiredModelTranslationDeltas[targetIndex];

                if (hasParent &&
                    Matrix4x4.Invert(
                        parentModel,
                        out Matrix4x4 inverseParentModel))
                {
                    targetTranslation +=
                        Vector3.TransformNormal(
                            modelDelta,
                            inverseParentModel);
                }
                else
                {
                    targetTranslation +=
                        modelDelta;
                }
            }

            targetLocals[targetIndex] =
                Compose(
                    targetScale,
                    targetLocalRotation,
                    targetTranslation);

            targetModels[targetIndex] =
                hasParent
                    ? targetLocals[targetIndex] *
                      parentModel
                    : targetLocals[targetIndex];

            resolving[targetIndex] =
                false;

            resolved[targetIndex] =
                true;

            return targetModels[targetIndex];
        }
    }

    private static Matrix4x4[] SampleSourceNodeModelsInSkeletonSpace(
        SkeletonAsset skeleton,
        ImportedAnimation animation,
        IReadOnlyList<ImportedNode> nodes,
        IReadOnlyList<ImportedMesh> meshes,
        float sampleTime,
        IReadOnlyList<Matrix4x4> referenceSkeletonModels)
    {
        int[] parentIndices =
            BuildNodeParentIndices(
                nodes);

        Matrix4x4[] referenceNodeGlobals =
            BuildNodeGlobals(
                nodes,
                parentIndices,
                animation:
                    null,
                sampleTime:
                    0.0f);

        Matrix4x4 skeletonBindFrame =
            ResolveSkeletonBindFrame(
                nodes,
                meshes,
                referenceNodeGlobals);

        if (!Matrix4x4.Invert(
                skeletonBindFrame,
                out Matrix4x4 inverseBindFrame))
        {
            inverseBindFrame =
                Matrix4x4.Identity;
        }

        Matrix4x4[] animatedNodeGlobals =
            BuildNodeGlobals(
                nodes,
                parentIndices,
                animation,
                sampleTime);

        Dictionary<string, int> nodeByName =
            nodes
                .Select(
                    (node, index) =>
                        new
                        {
                            node.Name,
                            Index = index
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

        var result =
            new Matrix4x4[
                skeleton.Bones.Count];

        for (int boneIndex = 0;
             boneIndex < skeleton.Bones.Count;
             boneIndex++)
        {
            Bone bone =
                skeleton.Bones[boneIndex];

            if (nodeByName.TryGetValue(
                    bone.Name,
                    out int nodeIndex) &&
                nodeIndex >= 0 &&
                nodeIndex < animatedNodeGlobals.Length)
            {
                Matrix4x4 converted =
                    animatedNodeGlobals[nodeIndex] *
                    inverseBindFrame;

                if (IsFinite(converted))
                {
                    result[boneIndex] =
                        converted;

                    continue;
                }
            }

            result[boneIndex] =
                boneIndex < referenceSkeletonModels.Count
                    ? referenceSkeletonModels[boneIndex]
                    : Matrix4x4.Identity;
        }

        return result;
    }

    private static int[] BuildNodeParentIndices(
        IReadOnlyList<ImportedNode> nodes)
    {
        Dictionary<string, int> byKey =
            nodes
                .Select(
                    (node, index) =>
                        new
                        {
                            node.Key,
                            Index = index
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

        var parentIndices =
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
                byKey.TryGetValue(
                    parentKey,
                    out int parentIndex))
            {
                parentIndices[index] =
                    parentIndex;
            }
        }

        return parentIndices;
    }

    private static Matrix4x4[] BuildNodeGlobals(
        IReadOnlyList<ImportedNode> nodes,
        IReadOnlyList<int> parentIndices,
        ImportedAnimation? animation,
        float sampleTime)
    {
        var locals =
            new Matrix4x4[
                nodes.Count];

        for (int index = 0;
             index < nodes.Count;
             index++)
        {
            locals[index] =
                SampleNodeLocal(
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
            if (state[index] == 2)
            {
                return globals[index];
            }

            if (state[index] == 1)
            {
                globals[index] =
                    locals[index];

                state[index] =
                    2;

                return globals[index];
            }

            state[index] =
                1;

            int parent =
                index < parentIndices.Count
                    ? parentIndices[index]
                    : -1;

            globals[index] =
                parent >= 0 &&
                parent < nodes.Count
                    ? locals[index] *
                      Resolve(
                          parent)
                    : locals[index];

            state[index] =
                2;

            return globals[index];
        }
    }

    private static Matrix4x4 SampleNodeLocal(
        ImportedNode node,
        ImportedAnimation? animation,
        float sampleTime)
    {
        Matrix4x4 fallback =
            node.LocalTransform;

        if (animation == null)
        {
            return fallback;
        }

        if (!TryDecompose(
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

        if (channel == null)
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

        return Compose(
            scale,
            rotation,
            translation);
    }

    private static Matrix4x4 ResolveSkeletonBindFrame(
        IReadOnlyList<ImportedNode> nodes,
        IReadOnlyList<ImportedMesh> meshes,
        IReadOnlyList<Matrix4x4> nodeGlobals)
    {
        if (meshes.Count == 0 ||
            nodes.Count == 0 ||
            nodeGlobals.Count != nodes.Count)
        {
            return Matrix4x4.Identity;
        }

        var meshFrames =
            new Dictionary<string, Matrix4x4>(
                StringComparer.Ordinal);

        for (int nodeIndex = 0;
             nodeIndex < nodes.Count;
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

            if (vertexCount <= 0 ||
                mesh.JointIndices.Length != vertexCount ||
                mesh.JointWeights.Length != vertexCount)
            {
                continue;
            }

            for (int vertexIndex = 0;
                 vertexIndex < vertexCount;
                 vertexIndex++)
            {
                Vector4 weights =
                    mesh.JointWeights[
                        vertexIndex];

                if (weights.X > 0.00001f ||
                    weights.Y > 0.00001f ||
                    weights.Z > 0.00001f ||
                    weights.W > 0.00001f)
                {
                    return meshFrame;
                }
            }
        }

        return Matrix4x4.Identity;
    }

    internal static Quaternion RemapRotationDeltaForAvatarFrames(
        HumanoidReferencePose source,
        HumanoidReferencePose target,
        Quaternion sourceModelDelta)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        return RemapRotationDelta(
            sourceModelDelta,
            BuildBodyFrame(source),
            BuildBodyFrame(target));
    }

    internal static Vector3 RemapVectorForAvatarFrames(
        HumanoidReferencePose source,
        HumanoidReferencePose target,
        Vector3 sourceModelVector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        return RemapVector(
            sourceModelVector,
            BuildBodyFrame(source),
            BuildBodyFrame(target));
    }

    private static HumanoidBodyFrame BuildBodyFrame(
        HumanoidReferencePose pose)
    {
        Vector3 hips =
            GetPosition(
                pose,
                HumanoidBone.Hips,
                Vector3.Zero);

        Vector3 head =
            GetPosition(
                pose,
                HumanoidBone.Head,
                hips + Vector3.UnitY);

        Vector3 up =
            SafeNormalize(
                head - hips,
                Vector3.UnitY);

        Vector3 leftAnchor =
            FirstAvailablePosition(
                pose,
                new[]
                {
                    HumanoidBone.LeftShoulder,
                    HumanoidBone.LeftUpperArm,
                    HumanoidBone.LeftHand,
                    HumanoidBone.LeftUpperLeg,
                    HumanoidBone.LeftFoot
                },
                hips - Vector3.UnitX);

        Vector3 rightAnchor =
            FirstAvailablePosition(
                pose,
                new[]
                {
                    HumanoidBone.RightShoulder,
                    HumanoidBone.RightUpperArm,
                    HumanoidBone.RightHand,
                    HumanoidBone.RightUpperLeg,
                    HumanoidBone.RightFoot
                },
                hips + Vector3.UnitX);

        Vector3 right =
            rightAnchor -
            leftAnchor;

        right -=
            up *
            Vector3.Dot(
                right,
                up);

        right =
            SafeNormalize(
                right,
                AnyPerpendicular(
                    up));

        Vector3 forward =
            SafeNormalize(
                Vector3.Cross(
                    right,
                    up),
                Vector3.UnitZ);

        right =
            SafeNormalize(
                Vector3.Cross(
                    up,
                    forward),
                right);

        return new HumanoidBodyFrame(
            right,
            up,
            forward);
    }

    private static Quaternion RemapRotationDelta(
        Quaternion sourceModelDelta,
        HumanoidBodyFrame sourceFrame,
        HumanoidBodyFrame targetFrame)
    {
        Quaternion normalized =
            NormalizeSafe(
                sourceModelDelta);

        if (normalized.W < 0.0f)
        {
            normalized =
                new Quaternion(
                    -normalized.X,
                    -normalized.Y,
                    -normalized.Z,
                    -normalized.W);
        }

        float w =
            Math.Clamp(
                normalized.W,
                -1.0f,
                1.0f);

        float angle =
            2.0f *
            MathF.Acos(
                w);

        if (!float.IsFinite(angle) ||
            angle <= 0.000001f)
        {
            return Quaternion.Identity;
        }

        float sinHalf =
            MathF.Sqrt(
                Math.Max(
                    1.0f -
                    w * w,
                    0.0f));

        Vector3 sourceAxis =
            sinHalf > 0.000001f
                ? new Vector3(
                    normalized.X,
                    normalized.Y,
                    normalized.Z) /
                  sinHalf
                : Vector3.UnitX;

        sourceAxis =
            SafeNormalize(
                sourceAxis,
                Vector3.UnitX);

        Vector3 targetAxis =
            RemapVector(
                sourceAxis,
                sourceFrame,
                targetFrame);

        targetAxis =
            SafeNormalize(
                targetAxis,
                Vector3.UnitX);

        return NormalizeSafe(
            Quaternion.CreateFromAxisAngle(
                targetAxis,
                angle));
    }

    private static Vector3 RemapVector(
        Vector3 sourceModelVector,
        HumanoidBodyFrame sourceFrame,
        HumanoidBodyFrame targetFrame)
    {
        if (!IsFinite(sourceModelVector))
        {
            return Vector3.Zero;
        }

        Vector3 canonical =
            new(
                Vector3.Dot(
                    sourceModelVector,
                    sourceFrame.Right),
                Vector3.Dot(
                    sourceModelVector,
                    sourceFrame.Up),
                Vector3.Dot(
                    sourceModelVector,
                    sourceFrame.Forward));

        return
            targetFrame.Right * canonical.X +
            targetFrame.Up * canonical.Y +
            targetFrame.Forward * canonical.Z;
    }

    private static Vector3 GetPosition(
        HumanoidReferencePose pose,
        HumanoidBone bone,
        Vector3 fallback)
    {
        HumanoidReferenceBonePose? mapped =
            pose.GetBone(
                bone);

        return mapped != null &&
               IsFinite(mapped.Position)
            ? mapped.Position
            : fallback;
    }

    private static Vector3 FirstAvailablePosition(
        HumanoidReferencePose pose,
        IReadOnlyList<HumanoidBone> candidates,
        Vector3 fallback)
    {
        foreach (HumanoidBone bone
                 in candidates)
        {
            HumanoidReferenceBonePose? mapped =
                pose.GetBone(
                    bone);

            if (mapped != null &&
                IsFinite(mapped.Position))
            {
                return mapped.Position;
            }
        }

        return fallback;
    }

    private static Vector3 AnyPerpendicular(
        Vector3 normal)
    {
        Vector3 candidate =
            MathF.Abs(normal.Y) < 0.9f
                ? Vector3.UnitY
                : Vector3.UnitX;

        Vector3 perpendicular =
            Vector3.Cross(
                candidate,
                normal);

        return SafeNormalize(
            perpendicular,
            Vector3.UnitX);
    }

    private static Vector3 SafeNormalize(
        Vector3 value,
        Vector3 fallback)
    {
        if (!IsFinite(value) ||
            value.LengthSquared() <= 0.0000001f)
        {
            return fallback;
        }

        return Vector3.Normalize(
            value);
    }

    private readonly record struct HumanoidBodyFrame(
        Vector3 Right,
        Vector3 Up,
        Vector3 Forward);

    public static float CalculateTranslationScale(
        HumanoidReferencePose source,
        HumanoidReferencePose target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        float sourceMeasure =
            MeasureBody(
                source);

        float targetMeasure =
            MeasureBody(
                target);

        if (!float.IsFinite(sourceMeasure) ||
            !float.IsFinite(targetMeasure) ||
            sourceMeasure <= 0.0001f ||
            targetMeasure <= 0.0001f)
        {
            return 1.0f;
        }

        return Math.Clamp(
            targetMeasure /
            sourceMeasure,
            0.01f,
            100.0f);
    }

    private static Matrix4x4[] SampleSourceSkeletonLocals(
        SkeletonAsset skeleton,
        ImportedAnimation animation,
        float sampleTime,
        IReadOnlyList<Matrix4x4> referenceLocals)
    {
        var result =
            new Matrix4x4[
                skeleton.Bones.Count];

        for (int index = 0;
             index < skeleton.Bones.Count;
             index++)
        {
            Bone bone =
                skeleton.Bones[index];

            Matrix4x4 fallback =
                referenceLocals[index];

            if (!TryDecompose(
                    fallback,
                    out Vector3 fallbackScale,
                    out Quaternion fallbackRotation,
                    out Vector3 fallbackTranslation))
            {
                result[index] =
                    fallback;

                continue;
            }

            ImportedAnimationChannel? channel =
                animation.FindChannel(
                    bone.Name);

            if (channel == null)
            {
                result[index] =
                    fallback;

                continue;
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

            result[index] =
                Compose(
                    scale,
                    rotation,
                    translation);
        }

        return result;
    }

    private static Matrix4x4[] BuildReferenceLocalMatrices(
        SkeletonAsset skeleton)
    {
        Matrix4x4[] globals =
            BuildReferenceModelMatrices(
                skeleton);

        var locals =
            new Matrix4x4[
                skeleton.Bones.Count];

        for (int index = 0;
             index < skeleton.Bones.Count;
             index++)
        {
            int parent =
                skeleton.Bones[index]
                    .ParentIndex;

            if (parent < 0 ||
                parent >= skeleton.Bones.Count)
            {
                locals[index] =
                    globals[index];

                continue;
            }

            if (!Matrix4x4.Invert(
                    globals[parent],
                    out Matrix4x4 inverseParent))
            {
                locals[index] =
                    globals[index];

                continue;
            }

            locals[index] =
                globals[index] *
                inverseParent;
        }

        return locals;
    }

    private static Matrix4x4[] BuildReferenceModelMatrices(
        SkeletonAsset skeleton)
    {
        var result =
            new Matrix4x4[
                skeleton.Bones.Count];

        for (int index = 0;
             index < skeleton.Bones.Count;
             index++)
        {
            if (!Matrix4x4.Invert(
                    skeleton.Bones[index]
                        .BindPose,
                    out Matrix4x4 model) ||
                !IsFinite(model))
            {
                model =
                    Matrix4x4.Identity;
            }

            result[index] =
                model;
        }

        return result;
    }

    private static Matrix4x4[] BuildModelMatrices(
        SkeletonAsset skeleton,
        IReadOnlyList<Matrix4x4> locals)
    {
        var result =
            new Matrix4x4[
                skeleton.Bones.Count];

        var resolved =
            new bool[
                skeleton.Bones.Count];

        var resolving =
            new bool[
                skeleton.Bones.Count];

        for (int index = 0;
             index < skeleton.Bones.Count;
             index++)
        {
            ResolveModelMatrix(
                skeleton,
                locals,
                index,
                result,
                resolved,
                resolving);
        }

        return result;
    }

    private static Matrix4x4 ResolveModelMatrix(
        SkeletonAsset skeleton,
        IReadOnlyList<Matrix4x4> locals,
        int index,
        Matrix4x4[] result,
        bool[] resolved,
        bool[] resolving)
    {
        if (resolved[index])
        {
            return result[index];
        }

        if (resolving[index])
        {
            result[index] =
                locals[index];

            resolved[index] =
                true;

            return result[index];
        }

        resolving[index] =
            true;

        int parent =
            skeleton.Bones[index]
                .ParentIndex;

        Matrix4x4 model =
            parent >= 0 &&
            parent < skeleton.Bones.Count
                ? locals[index] *
                  ResolveModelMatrix(
                      skeleton,
                      locals,
                      parent,
                      result,
                      resolved,
                      resolving)
                : locals[index];

        resolving[index] =
            false;

        resolved[index] =
            true;

        result[index] =
            model;

        return model;
    }

    private static Dictionary<string, int> BuildBoneIndex(
        SkeletonAsset skeleton)
    {
        return skeleton.Bones
            .Select(
                (bone, index) =>
                    new
                    {
                        bone.Name,
                        Index = index
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
    }

    private static Dictionary<int, HumanoidBone> BuildTargetSemanticIndex(
        HumanoidBoneMap mapping,
        IReadOnlyDictionary<string, int> targetIndices)
    {
        var result =
            new Dictionary<int, HumanoidBone>();

        foreach ((HumanoidBone semantic, string sourceName)
                 in mapping.Bones)
        {
            if (targetIndices.TryGetValue(
                    sourceName,
                    out int index))
            {
                result[index] =
                    semantic;
            }
        }

        return result;
    }

    private static Quaternion SolveAnimatedLimbFrame(
        HumanoidBone semanticBone,
        int sourceIndex,
        int targetIndex,
        Quaternion candidateModelRotation,
        Quaternion targetReferenceModelRotation,
        HumanoidBoneMap sourceMapping,
        IReadOnlyDictionary<string, int> sourceIndices,
        IReadOnlyList<Matrix4x4> sourceCurrentModels,
        HumanoidBoneMap targetMapping,
        IReadOnlyDictionary<string, int> targetIndices,
        IReadOnlyList<Matrix4x4> targetReferenceModels,
        HumanoidBodyFrame sourceBodyFrame,
        HumanoidBodyFrame targetBodyFrame)
    {
        GetLimbNeighbors(
            semanticBone,
            out HumanoidBone[] parentCandidates,
            out HumanoidBone[] childCandidates);

        if (childCandidates.Length == 0)
        {
            return candidateModelRotation;
        }

        if (!TryResolveSharedBone(
                childCandidates,
                sourceMapping,
                sourceIndices,
                targetMapping,
                targetIndices,
                out int sourceChildIndex,
                out int targetChildIndex) ||
            !ValidIndex(
                sourceIndex,
                sourceCurrentModels.Count) ||
            !ValidIndex(
                sourceChildIndex,
                sourceCurrentModels.Count) ||
            !ValidIndex(
                targetIndex,
                targetReferenceModels.Count) ||
            !ValidIndex(
                targetChildIndex,
                targetReferenceModels.Count))
        {
            return candidateModelRotation;
        }

        Vector3 sourceOutgoing =
            sourceCurrentModels[sourceChildIndex].Translation -
            sourceCurrentModels[sourceIndex].Translation;

        Vector3 targetReferenceOutgoing =
            targetReferenceModels[targetChildIndex].Translation -
            targetReferenceModels[targetIndex].Translation;

        if (!TryDirection(
                sourceOutgoing,
                out Vector3 sourcePrimary) ||
            !TryDirection(
                targetReferenceOutgoing,
                out Vector3 targetPrimary))
        {
            return candidateModelRotation;
        }

        Vector3 desiredPrimary =
            SafeNormalize(
                RemapVector(
                    sourcePrimary,
                    sourceBodyFrame,
                    targetBodyFrame),
                targetPrimary);

        /*
         * If we cannot establish a stable anatomical bend plane, retain the
         * existing rotation-delta twist but still make the segment point where
         * the source segment points. This is the safe fallback for nearly
         * straight limbs or rigs that omit the neighboring semantic joint.
         */
        if (!TryResolveSharedBone(
                parentCandidates,
                sourceMapping,
                sourceIndices,
                targetMapping,
                targetIndices,
                out int sourceParentIndex,
                out int targetParentIndex) ||
            !ValidIndex(
                sourceParentIndex,
                sourceCurrentModels.Count) ||
            !ValidIndex(
                targetParentIndex,
                targetReferenceModels.Count))
        {
            return AimCandidateDirection(
                candidateModelRotation,
                targetReferenceModelRotation,
                targetPrimary,
                desiredPrimary);
        }

        Vector3 sourceIncoming =
            sourceCurrentModels[sourceIndex].Translation -
            sourceCurrentModels[sourceParentIndex].Translation;

        Vector3 targetReferenceIncoming =
            targetReferenceModels[targetIndex].Translation -
            targetReferenceModels[targetParentIndex].Translation;

        Vector3 sourcePlane =
            Vector3.Cross(
                sourceIncoming,
                sourceOutgoing);

        Vector3 targetPlane =
            Vector3.Cross(
                targetReferenceIncoming,
                targetReferenceOutgoing);

        if (!TryDirection(
                sourcePlane,
                out Vector3 sourceNormal) ||
            !TryDirection(
                targetPlane,
                out Vector3 targetNormal))
        {
            return AimCandidateDirection(
                candidateModelRotation,
                targetReferenceModelRotation,
                targetPrimary,
                desiredPrimary);
        }

        Vector3 desiredNormal =
            RemapVector(
                sourceNormal,
                sourceBodyFrame,
                targetBodyFrame);

        desiredNormal =
            RejectFromAxis(
                desiredNormal,
                desiredPrimary);

        if (!TryDirection(
                desiredNormal,
                out desiredNormal))
        {
            return AimCandidateDirection(
                candidateModelRotation,
                targetReferenceModelRotation,
                targetPrimary,
                desiredPrimary);
        }

        /*
         * Full anatomical-frame solve. Start from the TARGET reference frame,
         * not from the raw source quaternion delta. That deliberately drops the
         * incompatible local-axis twist which caused the bad Manny preview.
         */
        Quaternion swing =
            FromToRotation(
                targetPrimary,
                desiredPrimary);

        Quaternion corrected =
            ApplyRotationDelta(
                targetReferenceModelRotation,
                swing);

        Vector3 swungTargetNormal =
            Vector3.Transform(
                targetNormal,
                swing);

        swungTargetNormal =
            RejectFromAxis(
                swungTargetNormal,
                desiredPrimary);

        if (!TryDirection(
                swungTargetNormal,
                out swungTargetNormal))
        {
            return corrected;
        }

        float twistAngle =
            SignedAngleAroundAxis(
                swungTargetNormal,
                desiredNormal,
                desiredPrimary);

        if (!float.IsFinite(twistAngle) ||
            MathF.Abs(twistAngle) <= 0.000001f)
        {
            return corrected;
        }

        Quaternion twist =
            NormalizeSafe(
                Quaternion.CreateFromAxisAngle(
                    desiredPrimary,
                    twistAngle));

        return ApplyRotationDelta(
            corrected,
            twist);
    }

    private static Quaternion AimCandidateDirection(
        Quaternion candidateModelRotation,
        Quaternion targetReferenceModelRotation,
        Vector3 targetReferenceDirection,
        Vector3 desiredDirection)
    {
        Quaternion candidateDelta =
            RelativeRotation(
                targetReferenceModelRotation,
                candidateModelRotation);

        Vector3 candidateDirection =
            SafeNormalize(
                Vector3.Transform(
                    targetReferenceDirection,
                    candidateDelta),
                targetReferenceDirection);

        Quaternion correction =
            FromToRotation(
                candidateDirection,
                desiredDirection);

        return ApplyRotationDelta(
            candidateModelRotation,
            correction);
    }

    private static void GetLimbNeighbors(
        HumanoidBone semanticBone,
        out HumanoidBone[] parentCandidates,
        out HumanoidBone[] childCandidates)
    {
        parentCandidates =
            Array.Empty<HumanoidBone>();

        childCandidates =
            Array.Empty<HumanoidBone>();

        switch (semanticBone)
        {
            case HumanoidBone.LeftShoulder:
                parentCandidates = new[]
                {
                    HumanoidBone.UpperChest,
                    HumanoidBone.Chest,
                    HumanoidBone.Spine
                };
                childCandidates = new[]
                {
                    HumanoidBone.LeftUpperArm
                };
                break;

            case HumanoidBone.LeftUpperArm:
                parentCandidates = new[]
                {
                    HumanoidBone.LeftShoulder,
                    HumanoidBone.UpperChest,
                    HumanoidBone.Chest
                };
                childCandidates = new[]
                {
                    HumanoidBone.LeftLowerArm
                };
                break;

            case HumanoidBone.LeftLowerArm:
                parentCandidates = new[]
                {
                    HumanoidBone.LeftUpperArm
                };
                childCandidates = new[]
                {
                    HumanoidBone.LeftHand
                };
                break;

            case HumanoidBone.LeftHand:
                parentCandidates = new[]
                {
                    HumanoidBone.LeftLowerArm
                };
                childCandidates = new[]
                {
                    HumanoidBone.LeftMiddleProximal,
                    HumanoidBone.LeftIndexProximal,
                    HumanoidBone.LeftRingProximal
                };
                break;

            case HumanoidBone.RightShoulder:
                parentCandidates = new[]
                {
                    HumanoidBone.UpperChest,
                    HumanoidBone.Chest,
                    HumanoidBone.Spine
                };
                childCandidates = new[]
                {
                    HumanoidBone.RightUpperArm
                };
                break;

            case HumanoidBone.RightUpperArm:
                parentCandidates = new[]
                {
                    HumanoidBone.RightShoulder,
                    HumanoidBone.UpperChest,
                    HumanoidBone.Chest
                };
                childCandidates = new[]
                {
                    HumanoidBone.RightLowerArm
                };
                break;

            case HumanoidBone.RightLowerArm:
                parentCandidates = new[]
                {
                    HumanoidBone.RightUpperArm
                };
                childCandidates = new[]
                {
                    HumanoidBone.RightHand
                };
                break;

            case HumanoidBone.RightHand:
                parentCandidates = new[]
                {
                    HumanoidBone.RightLowerArm
                };
                childCandidates = new[]
                {
                    HumanoidBone.RightMiddleProximal,
                    HumanoidBone.RightIndexProximal,
                    HumanoidBone.RightRingProximal
                };
                break;

            case HumanoidBone.LeftUpperLeg:
                parentCandidates = new[]
                {
                    HumanoidBone.Hips
                };
                childCandidates = new[]
                {
                    HumanoidBone.LeftLowerLeg
                };
                break;

            case HumanoidBone.LeftLowerLeg:
                parentCandidates = new[]
                {
                    HumanoidBone.LeftUpperLeg
                };
                childCandidates = new[]
                {
                    HumanoidBone.LeftFoot
                };
                break;

            case HumanoidBone.LeftFoot:
                parentCandidates = new[]
                {
                    HumanoidBone.LeftLowerLeg
                };
                childCandidates = new[]
                {
                    HumanoidBone.LeftToes
                };
                break;

            case HumanoidBone.RightUpperLeg:
                parentCandidates = new[]
                {
                    HumanoidBone.Hips
                };
                childCandidates = new[]
                {
                    HumanoidBone.RightLowerLeg
                };
                break;

            case HumanoidBone.RightLowerLeg:
                parentCandidates = new[]
                {
                    HumanoidBone.RightUpperLeg
                };
                childCandidates = new[]
                {
                    HumanoidBone.RightFoot
                };
                break;

            case HumanoidBone.RightFoot:
                parentCandidates = new[]
                {
                    HumanoidBone.RightLowerLeg
                };
                childCandidates = new[]
                {
                    HumanoidBone.RightToes
                };
                break;
        }
    }

    private static bool TryResolveSharedBone(
        IReadOnlyList<HumanoidBone> candidates,
        HumanoidBoneMap sourceMapping,
        IReadOnlyDictionary<string, int> sourceIndices,
        HumanoidBoneMap targetMapping,
        IReadOnlyDictionary<string, int> targetIndices,
        out int sourceIndex,
        out int targetIndex)
    {
        foreach (HumanoidBone candidate
                 in candidates)
        {
            if (sourceMapping.TryGetBoneName(
                    candidate,
                    out string sourceName) &&
                targetMapping.TryGetBoneName(
                    candidate,
                    out string targetName) &&
                sourceIndices.TryGetValue(
                    sourceName,
                    out sourceIndex) &&
                targetIndices.TryGetValue(
                    targetName,
                    out targetIndex))
            {
                return true;
            }
        }

        sourceIndex =
            -1;

        targetIndex =
            -1;

        return false;
    }

    private static bool ValidIndex(
        int index,
        int count) =>
        index >= 0 &&
        index < count;

    private static bool TryDirection(
        Vector3 value,
        out Vector3 normalized)
    {
        if (!IsFinite(value) ||
            value.LengthSquared() <= 0.0000001f)
        {
            normalized =
                Vector3.Zero;

            return false;
        }

        normalized =
            Vector3.Normalize(
                value);

        return true;
    }

    private static Vector3 RejectFromAxis(
        Vector3 value,
        Vector3 axis)
    {
        return value -
            axis *
            Vector3.Dot(
                value,
                axis);
    }

    private static Quaternion FromToRotation(
        Vector3 from,
        Vector3 to)
    {
        from =
            Vector3.Normalize(
                from);

        to =
            Vector3.Normalize(
                to);

        float dot =
            Math.Clamp(
                Vector3.Dot(
                    from,
                    to),
                -1.0f,
                1.0f);

        if (dot >= 0.999999f)
        {
            return Quaternion.Identity;
        }

        if (dot <= -0.999999f)
        {
            Vector3 axis =
                MathF.Abs(from.X) < 0.9f
                    ? Vector3.Cross(
                        from,
                        Vector3.UnitX)
                    : Vector3.Cross(
                        from,
                        Vector3.UnitY);

            axis =
                SafeNormalize(
                    axis,
                    AnyPerpendicular(
                        from));

            return Quaternion.CreateFromAxisAngle(
                axis,
                MathF.PI);
        }

        Vector3 cross =
            Vector3.Cross(
                from,
                to);

        if (!TryDirection(
                cross,
                out Vector3 rotationAxis))
        {
            return Quaternion.Identity;
        }

        return NormalizeSafe(
            Quaternion.CreateFromAxisAngle(
                rotationAxis,
                MathF.Acos(
                    dot)));
    }

    private static float SignedAngleAroundAxis(
        Vector3 from,
        Vector3 to,
        Vector3 axis)
    {
        float cosine =
            Math.Clamp(
                Vector3.Dot(
                    from,
                    to),
                -1.0f,
                1.0f);

        float sine =
            Vector3.Dot(
                Vector3.Cross(
                    from,
                    to),
                axis);

        return MathF.Atan2(
            sine,
            cosine);
    }

    private static Quaternion RelativeRotation(
        Quaternion reference,
        Quaternion current)
    {
        Matrix4x4 referenceMatrix =
            Matrix4x4.CreateFromQuaternion(
                NormalizeSafe(
                    reference));

        Matrix4x4 currentMatrix =
            Matrix4x4.CreateFromQuaternion(
                NormalizeSafe(
                    current));

        if (!Matrix4x4.Invert(
                referenceMatrix,
                out Matrix4x4 inverseReference))
        {
            return Quaternion.Identity;
        }

        Matrix4x4 delta =
            inverseReference *
            currentMatrix;

        if (!Matrix4x4.Decompose(
                delta,
                out _,
                out Quaternion rotation,
                out _))
        {
            return Quaternion.Identity;
        }

        return NormalizeSafe(
            rotation);
    }

    private static Quaternion ApplyRotationDelta(
        Quaternion targetReference,
        Quaternion delta)
    {
        Matrix4x4 targetReferenceMatrix =
            Matrix4x4.CreateFromQuaternion(
                NormalizeSafe(
                    targetReference));

        Matrix4x4 deltaMatrix =
            Matrix4x4.CreateFromQuaternion(
                NormalizeSafe(
                    delta));

        Matrix4x4 target =
            targetReferenceMatrix *
            deltaMatrix;

        if (!Matrix4x4.Decompose(
                target,
                out _,
                out Quaternion rotation,
                out _))
        {
            return NormalizeSafe(
                targetReference);
        }

        return NormalizeSafe(
            rotation);
    }

    private static Quaternion ModelToLocalRotation(
        Quaternion desiredModelRotation,
        Quaternion parentModelRotation)
    {
        Matrix4x4 model =
            Matrix4x4.CreateFromQuaternion(
                NormalizeSafe(
                    desiredModelRotation));

        Matrix4x4 parent =
            Matrix4x4.CreateFromQuaternion(
                NormalizeSafe(
                    parentModelRotation));

        if (!Matrix4x4.Invert(
                parent,
                out Matrix4x4 inverseParent))
        {
            return NormalizeSafe(
                desiredModelRotation);
        }

        Matrix4x4 local =
            model *
            inverseParent;

        if (!Matrix4x4.Decompose(
                local,
                out _,
                out Quaternion rotation,
                out _))
        {
            return Quaternion.Identity;
        }

        return NormalizeSafe(
            rotation);
    }

    private static float MeasureBody(
        HumanoidReferencePose pose)
    {
        HumanoidReferenceBonePose? hips =
            pose.GetBone(
                HumanoidBone.Hips);

        if (hips == null)
        {
            return 0.0f;
        }

        var distances =
            new List<float>();

        AddDistance(
            HumanoidBone.Head);

        AddDistance(
            HumanoidBone.LeftFoot);

        AddDistance(
            HumanoidBone.RightFoot);

        return distances.Count == 0
            ? 0.0f
            : distances.Average();

        void AddDistance(
            HumanoidBone bone)
        {
            HumanoidReferenceBonePose? target =
                pose.GetBone(
                    bone);

            if (target == null)
            {
                return;
            }

            float distance =
                Vector3.Distance(
                    hips.Position,
                    target.Position);

            if (float.IsFinite(distance) &&
                distance > 0.0001f)
            {
                distances.Add(
                    distance);
            }
        }
    }

    private static float ResolveSampleTime(
        float duration,
        float time,
        bool loop)
    {
        if (!float.IsFinite(time) ||
            !float.IsFinite(duration) ||
            duration <= 0.000001f)
        {
            return 0.0f;
        }

        if (!loop)
        {
            return Math.Clamp(
                time,
                0.0f,
                duration);
        }

        float wrapped =
            time %
            duration;

        if (wrapped < 0.0f)
        {
            wrapped +=
                duration;
        }

        return wrapped;
    }

    private static bool TryDecompose(
        Matrix4x4 matrix,
        out Vector3 scale,
        out Quaternion rotation,
        out Vector3 translation)
    {
        if (!Matrix4x4.Decompose(
                matrix,
                out scale,
                out rotation,
                out translation) ||
            !IsFinite(scale) ||
            !IsFinite(rotation) ||
            !IsFinite(translation))
        {
            scale =
                Vector3.One;

            rotation =
                Quaternion.Identity;

            translation =
                Vector3.Zero;

            return false;
        }

        rotation =
            NormalizeSafe(
                rotation);

        return true;
    }

    private static Matrix4x4 Compose(
        Vector3 scale,
        Quaternion rotation,
        Vector3 translation)
    {
        return
            Matrix4x4.CreateScale(
                scale) *
            Matrix4x4.CreateFromQuaternion(
                NormalizeSafe(
                    rotation)) *
            Matrix4x4.CreateTranslation(
                translation);
    }

    private static Quaternion NormalizeSafe(
        Quaternion value) =>
        value.LengthSquared() > 0.000001f
            ? Quaternion.Normalize(
                value)
            : Quaternion.Identity;

    private static bool IsFinite(
        Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);

    private static bool IsFinite(
        Quaternion value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) &&
        float.IsFinite(value.W);

    private static bool IsFinite(
        Matrix4x4 value) =>
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
