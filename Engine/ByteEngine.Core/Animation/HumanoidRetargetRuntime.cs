using System.Text;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Runtime/editor bridge for Humanoid animation reuse.
///
/// A retarget source is intentionally split into TWO concepts:
/// - Animation asset: owns the animation tracks and importer root-motion/sample settings.
/// - Source rig: owns the Humanoid skeleton, semantic map and reference pose used to interpret those tracks.
///
/// For normal FBX/glTF characters these are the same asset. For animation-only
/// files they may be different, which lets ByteEngine consume clips that carry
/// no render mesh or no usable skeleton as long as a compatible Humanoid rig is
/// assigned in the importer.
/// </summary>
public static class HumanoidRetargetRuntime
{
    /// <summary>
    /// Backward-compatible source-model API. If the source importer has an
    /// Animation Source Rig override, it is resolved automatically.
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
        return BuildClip(
            assets,
            sourceModelReference,
            AssetReference.Empty,
            sourceClipName,
            targetModelReference,
            outputName,
            samplesPerSecond);
    }

    /// <summary>
    /// Builds a target-skeleton clip from an animation asset plus an optional
    /// external Humanoid rig provider.
    ///
    /// sourceAnimationReference owns the clip. sourceRigReference may be empty;
    /// in that case ByteEngine uses the source asset's persisted importer
    /// override, then finally falls back to the source asset itself.
    /// </summary>
    public static ImportedAnimation BuildClip(
        AssetManager assets,
        AssetReference sourceAnimationReference,
        AssetReference sourceRigReference,
        string sourceClipName,
        AssetReference targetModelReference,
        string? outputName = null,
        float samplesPerSecond =
            HumanoidRetargetClipBuilder.DefaultSamplesPerSecond,
        AnimationRootMotionSource? rootMotionSourceOverride =
            null)
    {
        ArgumentNullException.ThrowIfNull(
            assets);

        ArgumentNullException.ThrowIfNull(
            sourceAnimationReference);

        ArgumentNullException.ThrowIfNull(
            sourceRigReference);

        ArgumentNullException.ThrowIfNull(
            targetModelReference);

        if (string.IsNullOrWhiteSpace(
                sourceClipName))
        {
            throw new ArgumentException(
                "Source Humanoid animation clip name cannot be empty.",
                nameof(sourceClipName));
        }

        ModelAsset animationSource =
            assets.LoadModel(
                sourceAnimationReference);

        ImportedAnimation sourceAnimation =
            FindAnimation(
                animationSource,
                sourceClipName);

        ModelAsset sourceRig =
            ResolveSourceRig(
                assets,
                animationSource,
                sourceRigReference);

        ModelAsset targetModel =
            assets.LoadModel(
                targetModelReference);

        string requestedName =
            string.IsNullOrWhiteSpace(
                outputName)
                ? ResolveRuntimeName(
                    animationSource,
                    sourceAnimation,
                    targetModel)
                : outputName.Trim();

        if (animationSource.Guid ==
                targetModel.Guid &&
            sourceRig.Guid ==
                targetModel.Guid)
        {
            return
                CloneAnimation(
                    sourceAnimation,
                    sourceAnimation.Key,
                    requestedName);
        }

        ValidateHumanoid(
            sourceRig,
            "source rig");

        ValidateHumanoid(
            targetModel,
            "target");

        ImportedAnimation boundAnimation =
            sourceRig.Guid ==
                    animationSource.Guid
                ? sourceAnimation
                : BindAnimationToRig(
                    animationSource,
                    sourceAnimation,
                    sourceRig);

        float effectiveSamplesPerSecond =
            !float.IsFinite(
                samplesPerSecond) ||
            samplesPerSecond <=
                0.0f ||
            MathF.Abs(
                samplesPerSecond -
                HumanoidRetargetClipBuilder.DefaultSamplesPerSecond) <=
                0.0001f
                ? animationSource.RetargetSamplesPerSecond
                : samplesPerSecond;

        ImportedAnimation generated =
            HumanoidRetargetClipBuilder.Build(
                sourceRig.Skeleton!,
                sourceRig.HumanoidMapping,
                sourceRig.ReferenceHumanoidPose!,
                boundAnimation,
                sourceRig.Nodes,
                sourceRig.Meshes,
                targetModel.Skeleton!,
                targetModel.HumanoidMapping,
                targetModel.ReferenceHumanoidPose!,
                targetModel.Nodes,
                targetModel.Meshes,
                animationSource.Guid,
                targetModel.Guid,
                requestedName,
                effectiveSamplesPerSecond);

        generated =
            HumanoidRetargetRootMotion.Normalize(
                rootMotionSourceOverride ??
                animationSource.RootMotionSource,
                targetModel,
                generated);

        string runtimeKey =
            BuildRuntimeKey(
                animationSource,
                sourceAnimation,
                sourceRig,
                targetModel);

        return
            CloneAnimation(
                generated,
                runtimeKey,
                requestedName);
    }

    public static ImportedAnimation EnsureClip(
        AssetManager assets,
        AssetReference sourceModelReference,
        string sourceClipName,
        AssetReference targetModelReference,
        float samplesPerSecond =
            HumanoidRetargetClipBuilder.DefaultSamplesPerSecond)
    {
        return EnsureClip(
            assets,
            sourceModelReference,
            AssetReference.Empty,
            sourceClipName,
            targetModelReference,
            samplesPerSecond);
    }

    public static ImportedAnimation EnsureClip(
        AssetManager assets,
        AssetReference sourceAnimationReference,
        AssetReference sourceRigReference,
        string sourceClipName,
        AssetReference targetModelReference,
        float samplesPerSecond =
            HumanoidRetargetClipBuilder.DefaultSamplesPerSecond)
    {
        ArgumentNullException.ThrowIfNull(
            assets);

        ModelAsset animationSource =
            assets.LoadModel(
                sourceAnimationReference);

        ModelAsset sourceRig =
            ResolveSourceRig(
                assets,
                animationSource,
                sourceRigReference);

        ModelAsset targetModel =
            assets.LoadModel(
                targetModelReference);

        ImportedAnimation sourceAnimation =
            FindAnimation(
                animationSource,
                sourceClipName);

        if (animationSource.Guid ==
                targetModel.Guid &&
            sourceRig.Guid ==
                targetModel.Guid)
        {
            return sourceAnimation;
        }

        ValidateHumanoid(
            sourceRig,
            "source rig");

        ValidateHumanoid(
            targetModel,
            "target");

        string runtimeKey =
            BuildRuntimeKey(
                animationSource,
                sourceAnimation,
                sourceRig,
                targetModel);

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
                animationSource,
                sourceAnimation,
                targetModel);

        ImportedAnimation generated =
            BuildClip(
                assets,
                sourceAnimationReference,
                sourceRigReference,
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
            // Fall through to the runtime-retarget path below.
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

    /// <summary>
    /// Resolves the rig that interprets the source tracks. Explicit editor/API
    /// choice wins, then the source importer's persisted rig override, then the
    /// animation asset itself.
    /// </summary>
    private static ModelAsset ResolveSourceRig(
        AssetManager assets,
        ModelAsset animationSource,
        AssetReference explicitRigReference)
    {
        AssetReference reference =
            !explicitRigReference.IsEmpty
                ? explicitRigReference
                : animationSource.AnimationSourceRigModel;

        if (reference.IsEmpty)
        {
            return animationSource;
        }

        return assets.LoadModel(
            reference);
    }

    /// <summary>
    /// Rebinds animation-channel names to an assigned source rig without
    /// rewriting source files. This is the ByteEngine equivalent of supplying a
    /// compatible Avatar to a Humanoid clip before baking it in Unity.
    ///
    /// Matching order:
    /// 1. exact rig node/bone name;
    /// 2. semantic Humanoid mapping when the animation asset has one;
    /// 3. canonical leaf-name matching (namespace/prefix tolerant).
    ///
    /// A genuinely skeleton-less clip still needs SOME compatible rig/rest pose;
    /// raw keyframes alone cannot describe bone hierarchy or reference pose.
    /// </summary>
    private static ImportedAnimation BindAnimationToRig(
        ModelAsset animationSource,
        ImportedAnimation animation,
        ModelAsset sourceRig)
    {
        var rigNames =
            sourceRig.Nodes
                .Select(
                    node =>
                        node.Name)
                .Concat(
                    sourceRig.Skeleton?.Bones.Select(
                        bone =>
                            bone.Name) ??
                    Enumerable.Empty<string>())
                .Where(
                    name =>
                        !string.IsNullOrWhiteSpace(
                            name))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (rigNames.Length ==
            0)
        {
            throw new InvalidOperationException(
                $"Assigned source rig '{sourceRig.Name}' has no usable node/bone names.");
        }

        var exactNames =
            rigNames.ToDictionary(
                name =>
                    name,
                name =>
                    name,
                StringComparer.OrdinalIgnoreCase);

        var canonicalNames =
            rigNames
                .Select(
                    name =>
                        new
                        {
                            Name =
                                name,
                            Canonical =
                                CanonicalBoneName(
                                    name)
                        })
                .Where(
                    item =>
                        item.Canonical.Length >
                        0)
                .GroupBy(
                    item =>
                        item.Canonical,
                    StringComparer.Ordinal)
                .Where(
                    group =>
                        group.Count() ==
                        1)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First().Name,
                    StringComparer.Ordinal);

        var semanticBySourceName =
            new Dictionary<string, HumanoidBone>(
                StringComparer.OrdinalIgnoreCase);

        var semanticByCanonicalSourceName =
            new Dictionary<string, HumanoidBone>(
                StringComparer.Ordinal);

        foreach ((HumanoidBone semantic, string sourceName)
                 in animationSource.HumanoidMapping.Bones)
        {
            if (string.IsNullOrWhiteSpace(
                    sourceName))
            {
                continue;
            }

            semanticBySourceName[sourceName] =
                semantic;

            string canonical =
                CanonicalBoneName(
                    sourceName);

            if (canonical.Length >
                0)
            {
                semanticByCanonicalSourceName[canonical] =
                    semantic;
            }
        }

        var chosen =
            new Dictionary<string, BoundChannel>(
                StringComparer.OrdinalIgnoreCase);

        foreach (ImportedAnimationChannel channel
                 in animation.Channels)
        {
            string? targetName =
                null;

            int score =
                0;

            if (exactNames.TryGetValue(
                    channel.NodeName,
                    out string? exact))
            {
                targetName =
                    exact;

                score =
                    40;
            }
            else
            {
                HumanoidBone semantic;

                string canonical =
                    CanonicalBoneName(
                        channel.NodeName);

                bool hasSemantic =
                    semanticBySourceName.TryGetValue(
                        channel.NodeName,
                        out semantic) ||
                    canonical.Length >
                        0 &&
                    semanticByCanonicalSourceName.TryGetValue(
                        canonical,
                        out semantic);

                if (hasSemantic &&
                    sourceRig.HumanoidMapping.TryGetBoneName(
                        semantic,
                        out string semanticTarget) &&
                    exactNames.TryGetValue(
                        semanticTarget,
                        out string? resolvedSemanticTarget))
                {
                    targetName =
                        resolvedSemanticTarget;

                    score =
                        35;
                }
                else if (canonical.Length >
                             0 &&
                         canonicalNames.TryGetValue(
                             canonical,
                             out string? canonicalTarget))
                {
                    targetName =
                        canonicalTarget;

                    score =
                        25;
                }
            }

            if (string.IsNullOrWhiteSpace(
                    targetName))
            {
                continue;
            }

            if (chosen.TryGetValue(
                    targetName,
                    out BoundChannel existing) &&
                existing.Score >=
                    score)
            {
                continue;
            }

            chosen[targetName] =
                new BoundChannel(
                    score,
                    new ImportedAnimationChannel
                    {
                        NodeName =
                            targetName,

                        Translation =
                            channel.Translation,

                        Rotation =
                            channel.Rotation,

                        Scale =
                            channel.Scale
                    });
        }

        if (chosen.Count ==
            0)
        {
            throw new InvalidOperationException(
                $"Animation '{animation.Name}' could not bind any channels to source rig '{sourceRig.Name}'. Assign the model/rig the clip was authored for, or use an animation file that carries its own Humanoid skeleton.");
        }

        return
            new ImportedAnimation
            {
                Key =
                    animation.Key,

                Name =
                    animation.Name,

                Duration =
                    animation.Duration,

                Channels =
                    chosen.Values
                        .Select(
                            item =>
                                item.Channel)
                        .ToList(),

                Events =
                    animation.Events.ToList(),

                Windows =
                    animation.Windows.ToList()
            };
    }

    private static string CanonicalBoneName(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        string leaf =
            value.Trim();

        int separator =
            leaf.LastIndexOfAny(
                new[]
                {
                    '/',
                    '\\',
                    '|',
                    ':'
                });

        if (separator >=
                0 &&
            separator +
                1 <
            leaf.Length)
        {
            leaf =
                leaf[(separator +
                      1)..];
        }

        var builder =
            new StringBuilder(
                leaf.Length);

        foreach (char character
                 in leaf)
        {
            if (char.IsLetterOrDigit(
                    character))
            {
                builder.Append(
                    char.ToLowerInvariant(
                        character));
            }
        }

        string canonical =
            builder.ToString();

        string[] disposablePrefixes =
        {
            "mixamorig",
            "armature",
            "def",
            "bip001",
            "bip01"
        };

        bool removed;

        do
        {
            removed =
                false;

            foreach (string prefix
                     in disposablePrefixes)
            {
                if (!canonical.StartsWith(
                        prefix,
                        StringComparison.Ordinal) ||
                    canonical.Length <=
                        prefix.Length)
                {
                    continue;
                }

                canonical =
                    canonical[prefix.Length..];

                removed =
                    true;

                break;
            }
        }
        while (removed);

        return canonical;
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
                $"Animation '{sourceClipName}' was not found in animation asset '{sourceModel.Name}'.");
    }

    private static void ValidateHumanoid(
        ModelAsset model,
        string role)
    {
        if (model.RigType !=
            AnimationRigType.Humanoid)
        {
            throw new InvalidOperationException(
                $"The {role} '{model.Name}' is not classified as Humanoid.");
        }

        if (model.Skeleton ==
                null ||
            model.ReferenceHumanoidPose ==
                null ||
            !model.ReferenceHumanoidPose.IsReady)
        {
            throw new InvalidOperationException(
                $"The {role} '{model.Name}' does not have a ready Humanoid skeleton/reference pose.");
        }

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                model.Skeleton,
                model.HumanoidMapping);

        if (!validation.IsReady)
        {
            throw new InvalidOperationException(
                $"The {role} '{model.Name}' has an incomplete Humanoid mapping.");
        }

        HumanoidRigDiagnosticReport diagnostics =
            HumanoidRigDiagnostics.Analyze(
                model.Skeleton,
                model.HumanoidMapping);

        if (diagnostics.Errors.Count >
            0)
        {
            throw new InvalidOperationException(
                $"The {role} '{model.Name}' has an invalid Humanoid hierarchy: {string.Join(" | ", diagnostics.Errors)}");
        }
    }

    private static string BuildRuntimeKey(
        ModelAsset animationSource,
        ImportedAnimation sourceAnimation,
        ModelAsset sourceRig,
        ModelAsset targetModel) =>
        $"humanoid-retarget:{animationSource.Guid:N}:{sourceAnimation.Key}:{sourceRig.Guid:N}:{targetModel.Guid:N}";

    private static ImportedAnimation CloneAnimation(
        ImportedAnimation source,
        string key,
        string name)
    {
        return
            new ImportedAnimation
            {
                Key =
                    key,

                Name =
                    name,

                Duration =
                    source.Duration,

                Channels =
                    source.Channels.ToList(),

                Events =
                    source.Events.ToList(),

                Windows =
                    source.Windows.ToList()
            };
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

    private readonly record struct BoundChannel(
        int Score,
        ImportedAnimationChannel Channel);
}
