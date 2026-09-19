using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Runtime/editor bridge for Humanoid animation reuse.
///
/// C9.5 separates BUILD from REGISTER:
/// - BuildClip creates a temporary target-skeleton clip for preview/approval.
/// - EnsureClip keeps the legacy runtime-retarget cache path working.
/// - ModelOwnedAnimationStore persists an approved clip onto the target model.
///
/// This allows editor retarget previews to remain non-destructive until the
/// user explicitly chooses Bake To Character.
/// </summary>
public static class HumanoidRetargetRuntime
{
    /// <summary>
    /// Builds a target-skeleton animation without registering it on the target
    /// ModelAsset. This is the safe path for preview/validation before baking.
    /// </summary>
    public static ImportedAnimation BuildClip(
        AssetManager assets,
        AssetReference sourceModelReference,
        string sourceClipName,
        AssetReference targetModelReference,
        string? outputName = null,
        float samplesPerSecond =
            HumanoidRetargetClipBuilder.DefaultSamplesPerSecond)
    {
        ArgumentNullException.ThrowIfNull(
            assets);

        ArgumentNullException.ThrowIfNull(
            sourceModelReference);

        ArgumentNullException.ThrowIfNull(
            targetModelReference);

        if (string.IsNullOrWhiteSpace(
                sourceClipName))
        {
            throw new ArgumentException(
                "Source Humanoid animation clip name cannot be empty.",
                nameof(sourceClipName));
        }

        ModelAsset sourceModel =
            assets.LoadModel(
                sourceModelReference);

        ModelAsset targetModel =
            assets.LoadModel(
                targetModelReference);

        ImportedAnimation sourceAnimation =
            FindAnimation(
                sourceModel,
                sourceClipName);

        string requestedName =
            string.IsNullOrWhiteSpace(
                outputName)
                ? ResolveRuntimeName(
                    sourceModel,
                    sourceAnimation,
                    targetModel)
                : outputName.Trim();

        if (sourceModel.Guid ==
            targetModel.Guid)
        {
            /*
             * The editor normally filters the target from the source list, but
             * keep this path deterministic for API callers. It is a copy only;
             * no retarget mapping is necessary.
             */
            return
                new ImportedAnimation
                {
                    Key =
                        sourceAnimation.Key,

                    Name =
                        requestedName,

                    Duration =
                        sourceAnimation.Duration,

                    Channels =
                        sourceAnimation.Channels.ToList()
                };
        }

        ValidateHumanoid(
            sourceModel,
            "source");

        ValidateHumanoid(
            targetModel,
            "target");

        return
            HumanoidRetargetClipBuilder.Build(
                sourceModel.Skeleton!,
                sourceModel.HumanoidMapping,
                sourceModel.ReferenceHumanoidPose!,
                sourceAnimation,
                sourceModel.Nodes,
                sourceModel.Meshes,
                targetModel.Skeleton!,
                targetModel.HumanoidMapping,
                targetModel.ReferenceHumanoidPose!,
                targetModel.Nodes,
                targetModel.Meshes,
                sourceModel.Guid,
                targetModel.Guid,
                requestedName,
                samplesPerSecond);
    }

    public static ImportedAnimation EnsureClip(
        AssetManager assets,
        AssetReference sourceModelReference,
        string sourceClipName,
        AssetReference targetModelReference,
        float samplesPerSecond =
            HumanoidRetargetClipBuilder.DefaultSamplesPerSecond)
    {
        ArgumentNullException.ThrowIfNull(
            assets);

        ArgumentNullException.ThrowIfNull(
            sourceModelReference);

        ArgumentNullException.ThrowIfNull(
            targetModelReference);

        if (string.IsNullOrWhiteSpace(
                sourceClipName))
        {
            throw new ArgumentException(
                "Source Humanoid animation clip name cannot be empty.",
                nameof(sourceClipName));
        }

        ModelAsset sourceModel =
            assets.LoadModel(
                sourceModelReference);

        ModelAsset targetModel =
            assets.LoadModel(
                targetModelReference);

        ImportedAnimation sourceAnimation =
            FindAnimation(
                sourceModel,
                sourceClipName);

        if (sourceModel.Guid ==
            targetModel.Guid)
        {
            return sourceAnimation;
        }

        ValidateHumanoid(
            sourceModel,
            "source");

        ValidateHumanoid(
            targetModel,
            "target");

        string runtimeKey =
            $"humanoid-retarget:{sourceModel.Guid:N}:{sourceAnimation.Key}:{targetModel.Guid:N}";

        ImportedAnimation? cached =
            targetModel.Animations.FirstOrDefault(
                animation =>
                    string.Equals(
                        animation.Key,
                        runtimeKey,
                        StringComparison.Ordinal));

        if (cached !=
            null)
        {
            return cached;
        }

        string runtimeName =
            ResolveRuntimeName(
                sourceModel,
                sourceAnimation,
                targetModel);

        ImportedAnimation generated =
            BuildClip(
                assets,
                sourceModelReference,
                sourceClipName,
                targetModelReference,
                runtimeName,
                samplesPerSecond);

        return targetModel.RegisterRuntimeAnimation(
            generated);
    }

    public static bool Play(
        AssetManager assets,
        SkeletalMeshRenderer targetRenderer,
        AssetReference sourceModelReference,
        string sourceClipName,
        bool? loop = null,
        float? transitionDuration = null,
        float samplesPerSecond =
            HumanoidRetargetClipBuilder.DefaultSamplesPerSecond)
    {
        ArgumentNullException.ThrowIfNull(
            targetRenderer);

        AssetReference targetReference =
            targetRenderer.Model;

        if (targetReference.IsEmpty)
        {
            return false;
        }

        /*
         * C9.5 ownership rule: an explicitly BAKED target-owned clip wins over
         * the legacy AnimationSourceModel path. Do not use HasAnimation alone
         * here: a target FBX may already contain an imported clip with the same
         * name, and old profiles must keep their existing runtime-retarget
         * behaviour until that source clip has actually been baked.
         */
        try
        {
            ModelAsset targetModel =
                assets.LoadModel(
                    targetReference);

            if (ModelOwnedAnimationStore.IsBakedAnimation(
                    assets.ProjectRoot,
                    targetModel,
                    sourceClipName))
            {
                return targetRenderer.Play(
                    sourceClipName,
                    loop,
                    transitionDuration);
            }
        }
        catch
        {
            // Fall through to the legacy source-retarget path below.
        }

        ImportedAnimation runtimeClip;

        try
        {
            runtimeClip =
                EnsureClip(
                    assets,
                    sourceModelReference,
                    sourceClipName,
                    targetReference,
                    samplesPerSecond);
        }
        catch
        {
            return false;
        }

        return targetRenderer.Play(
            runtimeClip.Name,
            loop,
            transitionDuration);
    }

    private static ImportedAnimation FindAnimation(
        ModelAsset sourceModel,
        string sourceClipName)
    {
        return
            sourceModel.Animations.FirstOrDefault(
                animation =>
                    string.Equals(
                        animation.Name,
                        sourceClipName,
                        StringComparison.Ordinal))
            ?? sourceModel.Animations.FirstOrDefault(
                animation =>
                    string.Equals(
                        animation.Name,
                        sourceClipName,
                        StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException(
                $"Animation '{sourceClipName}' was not found in source model '{sourceModel.Name}'.");
    }

    private static void ValidateHumanoid(
        ModelAsset model,
        string role)
    {
        if (model.RigType !=
            AnimationRigType.Humanoid)
        {
            throw new InvalidOperationException(
                $"The {role} model '{model.Name}' is not classified as Humanoid.");
        }

        if (model.Skeleton ==
                null ||
            model.ReferenceHumanoidPose ==
                null ||
            !model.ReferenceHumanoidPose.IsReady)
        {
            throw new InvalidOperationException(
                $"The {role} model '{model.Name}' does not have a ready Humanoid reference pose.");
        }

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                model.Skeleton,
                model.HumanoidMapping);

        if (!validation.IsReady)
        {
            throw new InvalidOperationException(
                $"The {role} model '{model.Name}' has an incomplete Humanoid mapping.");
        }

        HumanoidRigDiagnosticReport diagnostics =
            HumanoidRigDiagnostics.Analyze(
                model.Skeleton,
                model.HumanoidMapping);

        if (diagnostics.Errors.Count >
            0)
        {
            throw new InvalidOperationException(
                $"The {role} model '{model.Name}' has an invalid Humanoid hierarchy: {string.Join(" | ", diagnostics.Errors)}");
        }
    }

    private static string ResolveRuntimeName(
        ModelAsset sourceModel,
        ImportedAnimation sourceAnimation,
        ModelAsset targetModel)
    {
        bool nameTaken =
            targetModel.Animations.Any(
                animation =>
                    string.Equals(
                        animation.Name,
                        sourceAnimation.Name,
                        StringComparison.OrdinalIgnoreCase));

        if (!nameTaken)
        {
            return sourceAnimation.Name;
        }

        string baseName =
            $"{sourceAnimation.Name} [{sourceModel.Name}]";

        string candidate =
            baseName;

        int suffix =
            2;

        while (targetModel.Animations.Any(
                   animation =>
                       string.Equals(
                           animation.Name,
                           candidate,
                           StringComparison.OrdinalIgnoreCase)))
        {
            candidate =
                $"{baseName} {suffix}";

            suffix++;
        }

        return candidate;
    }
}
