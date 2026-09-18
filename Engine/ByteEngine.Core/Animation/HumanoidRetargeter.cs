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
/// C9L transfers animation orientation in MODEL SPACE rather than copying local
/// rotation deltas directly between unrelated skeletons. This is important for
/// Humanoid retargeting because two valid rigs can use completely different
/// local bone axes/roll while representing the same anatomical pose.
///
/// Target bone lengths, local offsets and scale remain target-authored.
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
            time,
            targetModel.Skeleton,
            targetModel.HumanoidMapping,
            targetModel.ReferenceHumanoidPose,
            loop);
    }

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
            BuildReferenceLocalMatrices(sourceSkeleton);

        Matrix4x4[] sourceReferenceModels =
            BuildReferenceModelMatrices(sourceSkeleton);

        Matrix4x4[] sourceCurrentLocals =
            SampleSourceLocalMatrices(
                sourceSkeleton,
                sourceAnimation,
                sampleTime,
                sourceReferenceLocals);

        Matrix4x4[] sourceCurrentModels =
            BuildModelMatrices(
                sourceSkeleton,
                sourceCurrentLocals);

        Matrix4x4[] targetReferenceLocals =
            BuildReferenceLocalMatrices(targetSkeleton);

        Matrix4x4[] targetReferenceModels =
            BuildReferenceModelMatrices(targetSkeleton);

        Dictionary<string, int> sourceIndices =
            BuildBoneIndex(sourceSkeleton);

        Dictionary<string, int> targetIndices =
            BuildBoneIndex(targetSkeleton);

        Dictionary<int, HumanoidBone> targetSemantics =
            BuildTargetSemanticIndex(
                targetMapping,
                targetIndices);

        float translationScale =
            CalculateTranslationScale(
                sourceReferencePose,
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

        /*
         * Build semantic target orientations in MODEL SPACE.
         *
         * Old C9D behaviour copied a LOCAL rotation delta from one rig onto the
         * target rig. That only works when source/target bones share the same
         * local axes and roll. Mixamo vs another Humanoid commonly does not.
         *
         * Model-space delta:
         *   sourceDelta = inverse(sourceReferenceModelRot)
         *               * sourceCurrentModelRot
         *
         * Then:
         *   targetDesiredModelRot = targetReferenceModelRot * sourceDelta
         *
         * Later we solve that desired model orientation back into each target
         * bone's own local space using the CURRENT target parent orientation.
         */
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

            desiredModelRotations[targetIndex] =
                ApplyRotationDelta(
                    targetReferenceModelRotation,
                    modelDelta);

            if (semanticBone == HumanoidBone.Root ||
                semanticBone == HumanoidBone.Hips)
            {
                desiredModelTranslationDeltas[targetIndex] =
                    (sourceCurrentModelTranslation -
                     sourceReferenceModelTranslation) *
                    translationScale;

                hasTranslationDelta[targetIndex] =
                    true;
            }
        }

        Matrix4x4[] targetLocals =
            targetReferenceLocals.ToArray();

        Matrix4x4[] targetModels =
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

            Matrix4x4 parentModel =
                Matrix4x4.Identity;

            bool hasParent =
                parentIndex >= 0 &&
                parentIndex <
                    targetSkeleton.Bones.Count;

            if (hasParent)
            {
                parentModel =
                    ResolveTargetPose(parentIndex);
            }

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

    public static float CalculateTranslationScale(
        HumanoidReferencePose source,
        HumanoidReferencePose target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        float sourceMeasure =
            MeasureBody(source);

        float targetMeasure =
            MeasureBody(target);

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

            /*
             * System.Numerics uses row-vector composition:
             * model = local * parentModel.
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
                NormalizeSafe(reference));

        Matrix4x4 currentMatrix =
            Matrix4x4.CreateFromQuaternion(
                NormalizeSafe(current));

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

        return NormalizeSafe(rotation);
    }

    private static Quaternion ApplyRotationDelta(
        Quaternion targetReference,
        Quaternion delta)
    {
        Matrix4x4 targetReferenceMatrix =
            Matrix4x4.CreateFromQuaternion(
                NormalizeSafe(targetReference));

        Matrix4x4 deltaMatrix =
            Matrix4x4.CreateFromQuaternion(
                NormalizeSafe(delta));

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

        return NormalizeSafe(rotation);
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

        return NormalizeSafe(rotation);
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

        AddDistance(HumanoidBone.Head);
        AddDistance(HumanoidBone.LeftFoot);
        AddDistance(HumanoidBone.RightFoot);

        return distances.Count == 0
            ? 0.0f
            : distances.Average();

        void AddDistance(
            HumanoidBone bone)
        {
            HumanoidReferenceBonePose? target =
                pose.GetBone(bone);

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
                distances.Add(distance);
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
            wrapped += duration;
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
            NormalizeSafe(rotation);

        return true;
    }

    private static Matrix4x4 Compose(
        Vector3 scale,
        Quaternion rotation,
        Vector3 translation)
    {
        return
            Matrix4x4.CreateScale(scale) *
            Matrix4x4.CreateFromQuaternion(
                NormalizeSafe(rotation)) *
            Matrix4x4.CreateTranslation(
                translation);
    }

    private static Quaternion NormalizeSafe(
        Quaternion value) =>
        value.LengthSquared() >
            0.000001f
            ? Quaternion.Normalize(value)
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
