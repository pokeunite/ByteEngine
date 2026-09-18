using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Runtime bridge for Humanoid animation reuse.
///
/// C9M supplies both source and target ImportedNode hierarchies to the clip
/// builder so FBX helper/pre-rotation nodes are included in retargeting.
/// </summary>
public static class HumanoidRetargetRuntime
{
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

        if (cached != null)
        {
            return cached;
        }

        string runtimeName =
            ResolveRuntimeName(
                sourceModel,
                sourceAnimation,
                targetModel);

        ImportedAnimation generated =
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

        if (model.Skeleton == null ||
            model.ReferenceHumanoidPose == null ||
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

        if (diagnostics.Errors.Count > 0)
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
