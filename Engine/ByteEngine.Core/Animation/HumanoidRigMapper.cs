using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Result of validating a source skeleton against ByteEngine's canonical
/// Humanoid semantic-bone contract.
/// </summary>
public sealed class HumanoidRigValidationResult
{
    public bool HasSkeleton { get; init; }

    public int MappedBoneCount { get; init; }

    public int RequiredMappedCount { get; init; }

    public IReadOnlyList<HumanoidBone> MissingRequiredBones { get; init; } =
        Array.Empty<HumanoidBone>();

    public IReadOnlyList<HumanoidBone> InvalidMappedBones { get; init; } =
        Array.Empty<HumanoidBone>();

    public IReadOnlyList<string> DuplicateSourceBones { get; init; } =
        Array.Empty<string>();

    public bool IsReady =>
        HasSkeleton &&
        MissingRequiredBones.Count == 0 &&
        InvalidMappedBones.Count == 0 &&
        DuplicateSourceBones.Count == 0;
}

/// <summary>
/// Name-based first-pass Humanoid mapper.
///
/// C9B intentionally keeps this deterministic and transparent. It recognizes
/// common Mixamo, Unreal-style, Blender/DEF and conventional human bone names,
/// then leaves any unresolved role visible for manual correction in the editor.
/// Retargeting/reference-pose math is not performed here.
/// </summary>
public static class HumanoidRigMapper
{
    private static readonly IReadOnlyDictionary<HumanoidBone, string[]> Aliases =
        BuildAliases();

    private static readonly HumanoidBone[] MappingOrder =
    {
        HumanoidBone.Root,
        HumanoidBone.Hips,
        HumanoidBone.Spine,
        HumanoidBone.Chest,
        HumanoidBone.UpperChest,
        HumanoidBone.Neck,
        HumanoidBone.Head,
        HumanoidBone.Jaw,
        HumanoidBone.LeftEye,
        HumanoidBone.RightEye,

        HumanoidBone.LeftShoulder,
        HumanoidBone.LeftUpperArm,
        HumanoidBone.LeftLowerArm,
        HumanoidBone.LeftHand,

        HumanoidBone.RightShoulder,
        HumanoidBone.RightUpperArm,
        HumanoidBone.RightLowerArm,
        HumanoidBone.RightHand,

        HumanoidBone.LeftUpperLeg,
        HumanoidBone.LeftLowerLeg,
        HumanoidBone.LeftFoot,
        HumanoidBone.LeftToes,

        HumanoidBone.RightUpperLeg,
        HumanoidBone.RightLowerLeg,
        HumanoidBone.RightFoot,
        HumanoidBone.RightToes,

        HumanoidBone.LeftThumbProximal,
        HumanoidBone.LeftThumbIntermediate,
        HumanoidBone.LeftThumbDistal,
        HumanoidBone.LeftIndexProximal,
        HumanoidBone.LeftIndexIntermediate,
        HumanoidBone.LeftIndexDistal,
        HumanoidBone.LeftMiddleProximal,
        HumanoidBone.LeftMiddleIntermediate,
        HumanoidBone.LeftMiddleDistal,
        HumanoidBone.LeftRingProximal,
        HumanoidBone.LeftRingIntermediate,
        HumanoidBone.LeftRingDistal,
        HumanoidBone.LeftLittleProximal,
        HumanoidBone.LeftLittleIntermediate,
        HumanoidBone.LeftLittleDistal,

        HumanoidBone.RightThumbProximal,
        HumanoidBone.RightThumbIntermediate,
        HumanoidBone.RightThumbDistal,
        HumanoidBone.RightIndexProximal,
        HumanoidBone.RightIndexIntermediate,
        HumanoidBone.RightIndexDistal,
        HumanoidBone.RightMiddleProximal,
        HumanoidBone.RightMiddleIntermediate,
        HumanoidBone.RightMiddleDistal,
        HumanoidBone.RightRingProximal,
        HumanoidBone.RightRingIntermediate,
        HumanoidBone.RightRingDistal,
        HumanoidBone.RightLittleProximal,
        HumanoidBone.RightLittleIntermediate,
        HumanoidBone.RightLittleDistal
    };

    public static HumanoidBoneMap AutoMap(
        SkeletonAsset? skeleton)
    {
        var result =
            new HumanoidBoneMap();

        if (skeleton?.Bones == null ||
            skeleton.Bones.Count == 0)
        {
            return result;
        }

        BoneCandidate[] candidates =
            skeleton.Bones
                .Select(
                    (bone, index) =>
                        new BoneCandidate(
                            index,
                            bone.Name,
                            NormalizeBoneName(
                                bone.Name)))
                .Where(
                    candidate =>
                        candidate.Normalized.Length > 0)
                .ToArray();

        var usedSourceBones =
            new HashSet<int>();

        foreach (HumanoidBone semanticBone
                 in MappingOrder)
        {
            if (!Aliases.TryGetValue(
                    semanticBone,
                    out string[]? aliases))
            {
                continue;
            }

            int bestIndex =
                -1;

            int bestScore =
                0;

            foreach (BoneCandidate candidate
                     in candidates)
            {
                if (usedSourceBones.Contains(
                        candidate.Index))
                {
                    continue;
                }

                int score =
                    aliases
                        .Select(
                            alias =>
                                Score(
                                    candidate.Normalized,
                                    alias))
                        .DefaultIfEmpty(0)
                        .Max();

                if (score <=
                    bestScore)
                {
                    continue;
                }

                bestScore =
                    score;

                bestIndex =
                    candidate.Index;
            }

            if (bestIndex <
                    0 ||
                bestScore <
                    600)
            {
                continue;
            }

            BoneCandidate selected =
                candidates.First(
                    candidate =>
                        candidate.Index ==
                        bestIndex);

            result.SetBone(
                semanticBone,
                selected.SourceName);

            usedSourceBones.Add(
                bestIndex);
        }

        result.Normalize();

        return result;
    }

    public static HumanoidRigValidationResult Validate(
        SkeletonAsset? skeleton,
        HumanoidBoneMap? mapping)
    {
        if (skeleton?.Bones == null ||
            skeleton.Bones.Count == 0)
        {
            return
                new HumanoidRigValidationResult
                {
                    HasSkeleton =
                        false,

                    MissingRequiredBones =
                        HumanoidBoneCatalog.Required
                            .ToArray()
                };
        }

        mapping ??=
            new HumanoidBoneMap();

        mapping.Normalize();

        var sourceNames =
            skeleton.Bones
                .Select(
                    bone =>
                        bone.Name)
                .Where(
                    name =>
                        !string.IsNullOrWhiteSpace(
                            name))
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var invalid =
            new List<HumanoidBone>();

        var validAssignments =
            new List<(HumanoidBone Semantic, string Source)>();

        foreach ((HumanoidBone semantic, string source)
                 in mapping.Bones)
        {
            if (!sourceNames.Contains(
                    source))
            {
                invalid.Add(
                    semantic);

                continue;
            }

            validAssignments.Add(
                (semantic, source));
        }

        string[] duplicates =
            validAssignments
                .GroupBy(
                    assignment =>
                        assignment.Source,
                    StringComparer.OrdinalIgnoreCase)
                .Where(
                    group =>
                        group.Count() >
                        1)
                .Select(
                    group =>
                        group.Key)
                .OrderBy(
                    value =>
                        value,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        HumanoidBone[] missing =
            HumanoidBoneCatalog.Required
                .Where(
                    semantic =>
                        !mapping.TryGetBoneName(
                            semantic,
                            out string source) ||
                        !sourceNames.Contains(
                            source))
                .ToArray();

        int requiredMapped =
            HumanoidBoneCatalog.Required.Count -
            missing.Length;

        return
            new HumanoidRigValidationResult
            {
                HasSkeleton =
                    true,

                MappedBoneCount =
                    validAssignments.Count,

                RequiredMappedCount =
                    requiredMapped,

                MissingRequiredBones =
                    missing,

                InvalidMappedBones =
                    invalid,

                DuplicateSourceBones =
                    duplicates
            };
    }

    /// <summary>
    /// Normalizes source bone naming while retaining side/segment information.
    /// Examples:
    /// mixamorig:Hips -> hips
    /// DEF-upper_arm.L -> upperarml
    /// CC_Base_L_Upperarm -> lupperarm
    /// </summary>
    public static string NormalizeBoneName(
        string? name)
    {
        if (string.IsNullOrWhiteSpace(
                name))
        {
            return string.Empty;
        }

        string leaf =
            name.Trim();

        int separator =
            leaf.LastIndexOfAny(
                new[]
                {
                    ':',
                    '|',
                    '/',
                    '\\'
                });

        if (separator >=
                0 &&
            separator + 1 <
                leaf.Length)
        {
            leaf =
                leaf[
                    (separator + 1)..];
        }

        string normalized =
            new(
                leaf
                    .Where(
                        char.IsLetterOrDigit)
                    .Select(
                        char.ToLowerInvariant)
                    .ToArray());

        string[] prefixes =
        {
            "mixamorig",
            "ccbase",
            "bip001",
            "bip01",
            "jbip",
            "def",
            "mch"
        };

        bool removed;

        do
        {
            removed =
                false;

            foreach (string prefix
                     in prefixes)
            {
                if (!normalized.StartsWith(
                        prefix,
                        StringComparison.Ordinal) ||
                    normalized.Length <=
                        prefix.Length + 1)
                {
                    continue;
                }

                normalized =
                    normalized[
                        prefix.Length..];

                removed =
                    true;

                break;
            }
        }
        while (removed);

        return normalized;
    }

    private static int Score(
        string normalizedName,
        string alias)
    {
        string normalizedAlias =
            NormalizeBoneName(
                alias);

        if (normalizedName.Length ==
                0 ||
            normalizedAlias.Length ==
                0)
        {
            return 0;
        }

        if (string.Equals(
                normalizedName,
                normalizedAlias,
                StringComparison.Ordinal))
        {
            return 1000;
        }

        if (normalizedName.EndsWith(
                normalizedAlias,
                StringComparison.Ordinal))
        {
            return
                820 -
                Math.Min(
                    normalizedName.Length -
                    normalizedAlias.Length,
                    100);
        }

        if (normalizedAlias.Length >=
                5 &&
            normalizedName.Contains(
                normalizedAlias,
                StringComparison.Ordinal))
        {
            return
                640 -
                Math.Min(
                    normalizedName.Length -
                    normalizedAlias.Length,
                    100);
        }

        return 0;
    }

    private static IReadOnlyDictionary<HumanoidBone, string[]> BuildAliases()
    {
        var aliases =
            new Dictionary<HumanoidBone, string[]>
            {
                [HumanoidBone.Root] =
                    A(
                        "root",
                        "rootmotion",
                        "armature"),

                [HumanoidBone.Hips] =
                    A(
                        "hips",
                        "pelvis",
                        "hip"),

                [HumanoidBone.Spine] =
                    A(
                        "spine",
                        "spine01"),

                [HumanoidBone.Chest] =
                    A(
                        "chest",
                        "spine1",
                        "spine02"),

                [HumanoidBone.UpperChest] =
                    A(
                        "upperchest",
                        "spine2",
                        "spine03"),

                [HumanoidBone.Neck] =
                    A(
                        "neck",
                        "neck01"),

                [HumanoidBone.Head] =
                    A(
                        "head"),

                [HumanoidBone.Jaw] =
                    A(
                        "jaw"),

                [HumanoidBone.LeftEye] =
                    A(
                        "lefteye",
                        "eyel"),

                [HumanoidBone.RightEye] =
                    A(
                        "righteye",
                        "eyer"),

                [HumanoidBone.LeftShoulder] =
                    A(
                        "leftshoulder",
                        "shoulderl",
                        "lshoulder",
                        "claviclel",
                        "lclavicle"),

                [HumanoidBone.LeftUpperArm] =
                    A(
                        "leftupperarm",
                        "leftarm",
                        "upperarml",
                        "lupperarm",
                        "larm"),

                [HumanoidBone.LeftLowerArm] =
                    A(
                        "leftlowerarm",
                        "leftforearm",
                        "lowerarml",
                        "forearml",
                        "llowerarm",
                        "lforearm"),

                [HumanoidBone.LeftHand] =
                    A(
                        "lefthand",
                        "handl",
                        "lhand"),

                [HumanoidBone.RightShoulder] =
                    A(
                        "rightshoulder",
                        "shoulderr",
                        "rshoulder",
                        "clavicler",
                        "rclavicle"),

                [HumanoidBone.RightUpperArm] =
                    A(
                        "rightupperarm",
                        "rightarm",
                        "upperarmr",
                        "rupperarm",
                        "rarm"),

                [HumanoidBone.RightLowerArm] =
                    A(
                        "rightlowerarm",
                        "rightforearm",
                        "lowerarmr",
                        "forearmr",
                        "rlowerarm",
                        "rforearm"),

                [HumanoidBone.RightHand] =
                    A(
                        "righthand",
                        "handr",
                        "rhand"),

                [HumanoidBone.LeftUpperLeg] =
                    A(
                        "leftupperleg",
                        "leftupleg",
                        "leftthigh",
                        "upperlegl",
                        "uplegl",
                        "thighl",
                        "lthigh"),

                [HumanoidBone.LeftLowerLeg] =
                    A(
                        "leftlowerleg",
                        "leftleg",
                        "leftcalf",
                        "leftshin",
                        "lowerlegl",
                        "calfl",
                        "shinl",
                        "lcalf"),

                [HumanoidBone.LeftFoot] =
                    A(
                        "leftfoot",
                        "footl",
                        "lfoot"),

                [HumanoidBone.LeftToes] =
                    A(
                        "lefttoes",
                        "lefttoebase",
                        "lefttoe",
                        "toesl",
                        "toebasel",
                        "balll",
                        "ltoe"),

                [HumanoidBone.RightUpperLeg] =
                    A(
                        "rightupperleg",
                        "rightupleg",
                        "rightthigh",
                        "upperlegr",
                        "uplegr",
                        "thighr",
                        "rthigh"),

                [HumanoidBone.RightLowerLeg] =
                    A(
                        "rightlowerleg",
                        "rightleg",
                        "rightcalf",
                        "rightshin",
                        "lowerlegr",
                        "calfr",
                        "shinr",
                        "rcalf"),

                [HumanoidBone.RightFoot] =
                    A(
                        "rightfoot",
                        "footr",
                        "rfoot"),

                [HumanoidBone.RightToes] =
                    A(
                        "righttoes",
                        "righttoebase",
                        "righttoe",
                        "toesr",
                        "toebaser",
                        "ballr",
                        "rtoe")
            };

        AddFingerAliases(
            aliases,
            left: true);

        AddFingerAliases(
            aliases,
            left: false);

        return aliases;
    }

    private static void AddFingerAliases(
        IDictionary<HumanoidBone, string[]> aliases,
        bool left)
    {
        string sideWord =
            left
                ? "left"
                : "right";

        string sideLetter =
            left
                ? "l"
                : "r";

        void Add(
            HumanoidBone proximal,
            HumanoidBone intermediate,
            HumanoidBone distal,
            string mixamoName,
            string unrealName)
        {
            aliases[proximal] =
                A(
                    $"{sideWord}hand{mixamoName}1",
                    $"{unrealName}01{sideLetter}",
                    $"{sideWord}{unrealName}1");

            aliases[intermediate] =
                A(
                    $"{sideWord}hand{mixamoName}2",
                    $"{unrealName}02{sideLetter}",
                    $"{sideWord}{unrealName}2");

            aliases[distal] =
                A(
                    $"{sideWord}hand{mixamoName}3",
                    $"{unrealName}03{sideLetter}",
                    $"{sideWord}{unrealName}3");
        }

        Add(
            left
                ? HumanoidBone.LeftThumbProximal
                : HumanoidBone.RightThumbProximal,
            left
                ? HumanoidBone.LeftThumbIntermediate
                : HumanoidBone.RightThumbIntermediate,
            left
                ? HumanoidBone.LeftThumbDistal
                : HumanoidBone.RightThumbDistal,
            "thumb",
            "thumb");

        Add(
            left
                ? HumanoidBone.LeftIndexProximal
                : HumanoidBone.RightIndexProximal,
            left
                ? HumanoidBone.LeftIndexIntermediate
                : HumanoidBone.RightIndexIntermediate,
            left
                ? HumanoidBone.LeftIndexDistal
                : HumanoidBone.RightIndexDistal,
            "index",
            "index");

        Add(
            left
                ? HumanoidBone.LeftMiddleProximal
                : HumanoidBone.RightMiddleProximal,
            left
                ? HumanoidBone.LeftMiddleIntermediate
                : HumanoidBone.RightMiddleIntermediate,
            left
                ? HumanoidBone.LeftMiddleDistal
                : HumanoidBone.RightMiddleDistal,
            "middle",
            "middle");

        Add(
            left
                ? HumanoidBone.LeftRingProximal
                : HumanoidBone.RightRingProximal,
            left
                ? HumanoidBone.LeftRingIntermediate
                : HumanoidBone.RightRingIntermediate,
            left
                ? HumanoidBone.LeftRingDistal
                : HumanoidBone.RightRingDistal,
            "ring",
            "ring");

        Add(
            left
                ? HumanoidBone.LeftLittleProximal
                : HumanoidBone.RightLittleProximal,
            left
                ? HumanoidBone.LeftLittleIntermediate
                : HumanoidBone.RightLittleIntermediate,
            left
                ? HumanoidBone.LeftLittleDistal
                : HumanoidBone.RightLittleDistal,
            "pinky",
            "pinky");
    }

    private static string[] A(
        params string[] values) =>
        values;

    private readonly record struct BoneCandidate(
        int Index,
        string SourceName,
        string Normalized);
}
