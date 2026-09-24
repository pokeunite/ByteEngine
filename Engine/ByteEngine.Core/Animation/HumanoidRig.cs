namespace ByteEngine.Core.Animation;

/// <summary>
/// Canonical semantic bones used by ByteEngine's Humanoid rig.
///
/// These names describe meaning, not source-skeleton naming. A source model may
/// call its hips "Hips", "mixamorig:Hips", "pelvis", etc.; the Humanoid bone
/// map connects that source bone to the semantic role below.
/// </summary>
public enum HumanoidBone
{
    Root,

    Hips,
    Spine,
    Chest,
    UpperChest,
    Neck,
    Head,
    Jaw,
    LeftEye,
    RightEye,

    LeftShoulder,
    LeftUpperArm,
    LeftLowerArm,
    LeftHand,

    RightShoulder,
    RightUpperArm,
    RightLowerArm,
    RightHand,

    LeftUpperLeg,
    LeftLowerLeg,
    LeftFoot,
    LeftToes,

    RightUpperLeg,
    RightLowerLeg,
    RightFoot,
    RightToes,

    LeftThumbProximal,
    LeftThumbIntermediate,
    LeftThumbDistal,
    LeftIndexProximal,
    LeftIndexIntermediate,
    LeftIndexDistal,
    LeftMiddleProximal,
    LeftMiddleIntermediate,
    LeftMiddleDistal,
    LeftRingProximal,
    LeftRingIntermediate,
    LeftRingDistal,
    LeftLittleProximal,
    LeftLittleIntermediate,
    LeftLittleDistal,

    RightThumbProximal,
    RightThumbIntermediate,
    RightThumbDistal,
    RightIndexProximal,
    RightIndexIntermediate,
    RightIndexDistal,
    RightMiddleProximal,
    RightMiddleIntermediate,
    RightMiddleDistal,
    RightRingProximal,
    RightRingIntermediate,
    RightRingDistal,
    RightLittleProximal,
    RightLittleIntermediate,
    RightLittleDistal
}

/// <summary>
/// Shared Humanoid bone requirements.
///
/// C9A intentionally defines only the canonical contract. Automatic mapping,
/// validation UI and reference-pose diagnostics are layered onto this
/// contract in later C9 slices.
/// </summary>
public static class HumanoidBoneCatalog
{
    private static readonly HumanoidBone[] RequiredBones =
    {
        HumanoidBone.Hips,
        HumanoidBone.Spine,
        HumanoidBone.Head,

        HumanoidBone.LeftUpperArm,
        HumanoidBone.LeftLowerArm,
        HumanoidBone.LeftHand,

        HumanoidBone.RightUpperArm,
        HumanoidBone.RightLowerArm,
        HumanoidBone.RightHand,

        HumanoidBone.LeftUpperLeg,
        HumanoidBone.LeftLowerLeg,
        HumanoidBone.LeftFoot,

        HumanoidBone.RightUpperLeg,
        HumanoidBone.RightLowerLeg,
        HumanoidBone.RightFoot
    };

    public static IReadOnlyList<HumanoidBone> Required =>
        RequiredBones;

    public static bool IsRequired(
        HumanoidBone bone) =>
        Array.IndexOf(
            RequiredBones,
            bone) >= 0;
}

/// <summary>
/// Source-skeleton bone names mapped to ByteEngine's canonical Humanoid roles.
///
/// The mapping deliberately stores source names rather than bone indices. Bone
/// indices are importer/runtime details and can change when a source model is
/// reimported, while source bone names are the stable authoring identity.
/// </summary>
public sealed class HumanoidBoneMap
{
    public Dictionary<HumanoidBone, string> Bones { get; set; } =
        new();

    public int MappedCount =>
        Bones?.Count ?? 0;

    public bool IsMapped(
        HumanoidBone bone) =>
        Bones != null &&
        Bones.TryGetValue(
            bone,
            out string? value) &&
        !string.IsNullOrWhiteSpace(
            value);

    public string? GetBoneName(
        HumanoidBone bone)
    {
        if (Bones == null ||
            !Bones.TryGetValue(
                bone,
                out string? value) ||
            string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        return value;
    }

    public bool TryGetBoneName(
        HumanoidBone bone,
        out string boneName)
    {
        boneName =
            GetBoneName(
                bone) ??
            string.Empty;

        return boneName.Length >
            0;
    }

    public void SetBone(
        HumanoidBone bone,
        string? sourceBoneName)
    {
        Bones ??=
            new Dictionary<HumanoidBone, string>();

        if (string.IsNullOrWhiteSpace(
                sourceBoneName))
        {
            Bones.Remove(
                bone);

            return;
        }

        Bones[bone] =
            sourceBoneName.Trim();
    }

    public void ClearBone(
        HumanoidBone bone)
    {
        Bones?.Remove(
            bone);
    }

    public IReadOnlyList<HumanoidBone> MissingRequiredBones()
    {
        return
            HumanoidBoneCatalog.Required
                .Where(
                    bone =>
                        !IsMapped(
                            bone))
                .ToArray();
    }

    /// <summary>
    /// Repairs old/null JSON data and removes empty source-bone names.
    /// </summary>
    public void Normalize()
    {
        Bones ??=
            new Dictionary<HumanoidBone, string>();

        foreach (HumanoidBone bone
                 in Bones.Keys.ToArray())
        {
            string? sourceName =
                Bones[bone];

            if (string.IsNullOrWhiteSpace(
                    sourceName))
            {
                Bones.Remove(
                    bone);

                continue;
            }

            Bones[bone] =
                sourceName.Trim();
        }
    }

    public HumanoidBoneMap Clone()
    {
        Normalize();

        return
            new HumanoidBoneMap
            {
                Bones =
                    Bones.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value)
            };
    }
}
