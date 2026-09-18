using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Animation;

/// <summary>
/// One sampled Humanoid retarget result for a target skeleton.
///
/// LocalMatrices follow the target skeleton hierarchy and ModelMatrices contain
/// the resolved model-space transforms. The renderer/runtime integration added
/// after C9D can consume these matrices without rebuilding retarget math.
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
        LocalMatrices =
            localMatrices;

        ModelMatrices =
            modelMatrices;

        SourceTime =
            sourceTime;

        TranslationScale =
            translationScale;
    }
}

/// <summary>
/// ByteEngine Humanoid source -> target pose conversion core.
///
/// C9D performs the actual retarget calculation but deliberately does not wire
/// it into SkeletalMeshRenderer yet. It samples the source animation, measures
/// rotation changes relative to the source bind/reference pose, applies those
/// changes to the target reference pose, preserves target bone lengths/scales,
/// and proportionally transfers Root/Hips translation.
/// </summary>
public static class HumanoidRetargeter
{
    /// <summary>
    /// Convenience overload for imported ModelAsset instances.
    /// Both models must already be classified Humanoid and have valid reference
    /// poses from C9C.
    /// </summary>
    public static HumanoidRetargetPose Retarget(
        ModelAsset sourceModel,
        ImportedAnimation sourceAnimation,
        float time,
        ModelAsset targetModel,
        bool loop = true)
    {
        ArgumentNullException.ThrowIfNull(
            sourceModel);

        ArgumentNullException.ThrowIfNull(
            sourceAnimation);

        ArgumentNullException.ThrowIfNull(
            targetModel);

        if (sourceModel.RigType !=
            AnimationRigType.Humanoid)
        {
            throw new InvalidOperationException(
                $"Source model '{sourceModel.Name}' is not a Humanoid rig.");
        }

        if (targetModel.RigType !=
            AnimationRigType.Humanoid)
        {
            throw new InvalidOperationException(
                $"Target model '{targetModel.Name}' is not a Humanoid rig.");
        }

        if (sourceModel.Skeleton ==
                null ||
            sourceModel.ReferenceHumanoidPose ==
                null)
        {
            throw new InvalidOperationException(
                $"Source model '{sourceModel.Name}' does not have a valid Humanoid skeleton/reference pose.");
        }

        if (targetModel.Skeleton ==
                null ||
            targetModel.ReferenceHumanoidPose ==
                null)
        {
            throw new InvalidOperationException(
                $"Target model '{targetModel.Name}' does not have a valid Humanoid skeleton/reference pose.");
        }

        return
            Retarget(
                sourceModel.Skeleton,
                sourceModel.HumanoidMapping,
                sourceModel.ReferenceHumanoidPose,
                sourceAnimation,
                time,
                targetModel.Skeleton,
                targetModel.HumanoidMapping,
                targetModel.ReferenceHumanoidPose,
                loop);
    }

    /// <summary>
    /// Retarget one source clip sample into the target skeleton.
    ///
    /// This overload is importer/runtime-neutral and is also used by regression
    /// tests. It does not mutate either skeleton or animation asset.
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

        float sampleTime =
            ResolveSampleTime(
                sourceAnimation.Duration,
                time,
                loop);

        Matrix4x4[] sourceReferenceLocals =
            BuildReferenceLocalMatrices(
                sourceSkeleton);

        Matrix4x4[] sourceCurrentLocals =
            SampleSourceLocalMatrices(
                sourceSkeleton,
                sourceAnimation,
                sampleTime,
                sourceReferenceLocals);

        Matrix4x4[] targetReferenceLocals =
            BuildReferenceLocalMatrices(
                targetSkeleton);

        Matrix4x4[] targetLocals =
            targetReferenceLocals
                .ToArray();

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
                    out int sourceIndex))
            {
                continue;
            }

            if (!TryDecompose(
                    sourceReferenceLocals[sourceIndex],
                    out Vector3 sourceReferenceScale,
                    out Quaternion sourceReferenceRotation,
                    out Vector3 sourceReferenceTranslation) ||
                !TryDecompose(
                    sourceCurrentLocals[sourceIndex],
                    out _,
                    out Quaternion sourceCurrentRotation,
                    out Vector3 sourceCurrentTranslation) ||
                !TryDecompose(
                    targetReferenceLocals[targetIndex],
                    out Vector3 targetReferenceScale,
                    out Quaternion targetReferenceRotation,
                    out Vector3 targetReferenceTranslation))
            {
                continue;
            }

            Quaternion rotationDelta =
                RelativeRotation(
                    sourceReferenceRotation,
                    sourceCurrentRotation);

            Quaternion targetRotation =
                ApplyRotationDelta(
                    targetReferenceRotation,
                    rotationDelta);

            Vector3 targetTranslation =
                targetReferenceTranslation;

            if (semanticBone ==
                    HumanoidBone.Root ||
                semanticBone ==
                    HumanoidBone.Hips)
            {
                Vector3 sourceTranslationDelta =
                    sourceCurrentTranslation -
                    sourceReferenceTranslation;

                targetTranslation +=
                    sourceTranslationDelta *
                    translationScale;
            }

            /*
             * Keep target reference scale. Applying source animation scale is
             * intentionally deferred because it can distort characters with
             * different proportions. Rotation + Root/Hips translation is the
             * stable Humanoid baseline.
             */
            targetLocals[targetIndex] =
                Compose(
                    targetReferenceScale,
                    targetRotation,
                    targetTranslation);

            _ =
                sourceReferenceScale;
        }

        Matrix4x4[] targetModels =
            BuildModelMatrices(
                targetSkeleton,
                targetLocals);

        return
            new HumanoidRetargetPose(
                targetLocals,
                targetModels,
                sampleTime,
                translationScale);
    }

    /// <summary>
    /// Estimates target/source body-size ratio from mapped reference positions.
    /// Used only for Root/Hips translation so differently sized Humanoids do
    /// not inherit raw source displacement in the wrong scale.
    /// </summary>
    public static float CalculateTranslationScale(
        HumanoidReferencePose source,
        HumanoidReferencePose target)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            target);

        float sourceMeasure =
            MeasureBody(
                source);

        float targetMeasure =
            MeasureBody(
                target);

        if (!float.IsFinite(
                sourceMeasure) ||
            !float.IsFinite(
                targetMeasure) ||
            sourceMeasure <=
                0.0001f ||
            targetMeasure <=
                0.0001f)
        {
            return 1.0f;
        }

        return Math.Clamp(
            targetMeasure /
            sourceMeasure,
            0.01f,
            100.0f);
    }

    private static Matrix4x4[] SampleSourceLocalMatrices(
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

            if (channel ==
                null)
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

            if (parent <
                    0 ||
                parent >=
                    skeleton.Bones.Count)
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

            /*
             * System.Numerics uses row-vector composition:
             * model = local * parentModel.
             * Therefore local = model * inverse(parentModel).
             */
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
                    skeleton.Bones[index].BindPose,
                    out Matrix4x4 model) ||
                !IsFinite(
                    model))
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
            /*
             * Malformed cyclic hierarchy: fail safely at this bone instead of
             * recursing forever.
             */
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
            parent >=
                0 &&
            parent <
                skeleton.Bones.Count
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
        return
            skeleton.Bones
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
    }

    private static Dictionary<int, HumanoidBone> BuildTargetSemanticIndex(
        HumanoidBoneMap mapping,
        IReadOnlyDictionary<string, int> targetIndices)
    {
        var result =
            new Dictionary<
                int,
                HumanoidBone>();

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

    private static float MeasureBody(
        HumanoidReferencePose pose)
    {
        HumanoidReferenceBonePose? hips =
            pose.GetBone(
                HumanoidBone.Hips);

        if (hips ==
            null)
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

        if (distances.Count ==
            0)
        {
            return 0.0f;
        }

        return distances.Average();

        void AddDistance(
            HumanoidBone bone)
        {
            HumanoidReferenceBonePose? target =
                pose.GetBone(
                    bone);

            if (target ==
                null)
            {
                return;
            }

            float distance =
                Vector3.Distance(
                    hips.Position,
                    target.Position);

            if (float.IsFinite(
                    distance) &&
                distance >
                    0.0001f)
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
        if (!float.IsFinite(
                time) ||
            !float.IsFinite(
                duration) ||
            duration <=
                0.000001f)
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

        if (wrapped <
            0.0f)
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
            !IsFinite(
                scale) ||
            !IsFinite(
                rotation) ||
            !IsFinite(
                translation))
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
        value.LengthSquared() >
            0.000001f
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
