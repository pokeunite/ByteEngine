using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Animation;

/// <summary>
/// C9E runtime bridge for Unity-style Humanoid animation reuse.
///
/// A source model can contain the animation while a different target Humanoid
/// model owns the visible mesh. The source clip is baked once into a target
/// runtime clip, cached on the target ModelAsset, and then played through the
/// existing SkeletalMeshRenderer. That keeps cross-fades, sockets, skinning and
/// C7 root-motion behavior on the already-proven renderer path.
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

        /*
         * Same model means no retarget is required. Returning the imported clip
         * keeps the call convenient for authoring/runtime code that does not
         * need to special-case source == target.
         */
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
            HumanoidRetargetClipBuilder.Build(
                sourceModel.Skeleton!,
                sourceModel.HumanoidMapping,
                sourceModel.ReferenceHumanoidPose!,
                sourceAnimation,
                targetModel.Skeleton!,
                targetModel.HumanoidMapping,
                targetModel.ReferenceHumanoidPose!,
                targetModel.Nodes,
                sourceModel.Guid,
                targetModel.Guid,
                runtimeName,
                samplesPerSecond);

        return
            targetModel.RegisterRuntimeAnimation(
                generated);
    }

    /// <summary>
    /// Retargets if needed and immediately plays the resulting clip through the
    /// target renderer. This is the first C9 API where a Humanoid animation from
    /// one model can actually drive another Humanoid character at runtime.
    /// </summary>
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

        return
            targetRenderer.Play(
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
