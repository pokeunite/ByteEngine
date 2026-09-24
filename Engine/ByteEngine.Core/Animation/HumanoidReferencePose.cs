using System.Numerics;

using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Animation;

/// <summary>
/// One semantic Humanoid bone captured from the source skeleton's bind pose.
///
/// Bone.BindPose is the inverse bind matrix supplied by the source asset.
/// ModelSpaceMatrix is its inverse: the source bone's model-space transform in
/// the reference/bind pose.
/// </summary>
public sealed class HumanoidReferenceBonePose
{
    public HumanoidBone Bone { get; init; }

    public string SourceBoneName { get; init; } =
        string.Empty;

    public int SourceBoneIndex { get; init; } =
        -1;

    public int SourceParentIndex { get; init; } =
        -1;

    public Matrix4x4 ModelSpaceMatrix { get; init; } =
        Matrix4x4.Identity;

    public Vector3 Position { get; init; }

    public Quaternion Rotation { get; init; } =
        Quaternion.Identity;

    public Vector3 Scale { get; init; } =
        Vector3.One;
}

/// <summary>
/// Derived Humanoid reference pose used by mapping diagnostics.
///
/// The pose is intentionally derived from imported inverse-bind matrices rather
/// than persisted separately. This keeps the model's source bind pose as the
/// single source of truth and avoids stale duplicated pose data in .meta files.
/// </summary>
public sealed class HumanoidReferencePose
{
    private readonly IReadOnlyDictionary<
        HumanoidBone,
        HumanoidReferenceBonePose> _bones;

    public IReadOnlyDictionary<
        HumanoidBone,
        HumanoidReferenceBonePose> Bones =>
        _bones;

    public IReadOnlyList<HumanoidBone> MissingRequiredBones { get; }

    public IReadOnlyList<HumanoidBone> InvalidMappedBones { get; }

    public int CapturedBoneCount =>
        _bones.Count;

    public bool IsReady =>
        MissingRequiredBones.Count ==
            0 &&
        InvalidMappedBones.Count ==
            0;

    private HumanoidReferencePose(
        IReadOnlyDictionary<
            HumanoidBone,
            HumanoidReferenceBonePose> bones,
        IReadOnlyList<HumanoidBone> missingRequiredBones,
        IReadOnlyList<HumanoidBone> invalidMappedBones)
    {
        _bones =
            bones;

        MissingRequiredBones =
            missingRequiredBones;

        InvalidMappedBones =
            invalidMappedBones;
    }

    public bool TryGetBone(
        HumanoidBone bone,
        out HumanoidReferenceBonePose? pose) =>
        _bones.TryGetValue(
            bone,
            out pose);

    public HumanoidReferenceBonePose? GetBone(
        HumanoidBone bone) =>
        _bones.TryGetValue(
            bone,
            out HumanoidReferenceBonePose? pose)
            ? pose
            : null;

    public static HumanoidReferencePose Capture(
        SkeletonAsset? skeleton,
        HumanoidBoneMap? mapping)
    {
        mapping ??=
            new HumanoidBoneMap();

        mapping.Normalize();

        if (skeleton?.Bones == null ||
            skeleton.Bones.Count ==
                0)
        {
            return
                new HumanoidReferencePose(
                    new Dictionary<
                        HumanoidBone,
                        HumanoidReferenceBonePose>(),
                    HumanoidBoneCatalog.Required
                        .ToArray(),
                    Array.Empty<HumanoidBone>());
        }

        var indices =
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

        var captured =
            new Dictionary<
                HumanoidBone,
                HumanoidReferenceBonePose>();

        var invalid =
            new List<HumanoidBone>();

        foreach ((HumanoidBone semanticBone, string sourceBoneName)
                 in mapping.Bones)
        {
            if (!indices.TryGetValue(
                    sourceBoneName,
                    out int sourceIndex))
            {
                invalid.Add(
                    semanticBone);

                continue;
            }

            Bone sourceBone =
                skeleton.Bones[sourceIndex];

            if (!Matrix4x4.Invert(
                    sourceBone.BindPose,
                    out Matrix4x4 modelSpace) ||
                !IsFinite(
                    modelSpace))
            {
                invalid.Add(
                    semanticBone);

                continue;
            }

            if (!Matrix4x4.Decompose(
                    modelSpace,
                    out Vector3 scale,
                    out Quaternion rotation,
                    out Vector3 position) ||
                !IsFinite(
                    position) ||
                !IsFinite(
                    scale) ||
                !IsFinite(
                    rotation))
            {
                invalid.Add(
                    semanticBone);

                continue;
            }

            rotation =
                rotation.LengthSquared() >
                    0.000001f
                    ? Quaternion.Normalize(
                        rotation)
                    : Quaternion.Identity;

            captured[semanticBone] =
                new HumanoidReferenceBonePose
                {
                    Bone =
                        semanticBone,

                    SourceBoneName =
                        sourceBone.Name,

                    SourceBoneIndex =
                        sourceIndex,

                    SourceParentIndex =
                        sourceBone.ParentIndex,

                    ModelSpaceMatrix =
                        modelSpace,

                    Position =
                        position,

                    Rotation =
                        rotation,

                    Scale =
                        scale
                };
        }

        HumanoidBone[] missing =
            HumanoidBoneCatalog.Required
                .Where(
                    bone =>
                        !captured.ContainsKey(
                            bone))
                .ToArray();

        return
            new HumanoidReferencePose(
                captured,
                missing,
                invalid
                    .Distinct()
                    .ToArray());
    }

    private static bool IsFinite(
        Vector3 value) =>
        float.IsFinite(
            value.X) &&
        float.IsFinite(
            value.Y) &&
        float.IsFinite(
            value.Z);

    private static bool IsFinite(
        Quaternion value) =>
        float.IsFinite(
            value.X) &&
        float.IsFinite(
            value.Y) &&
        float.IsFinite(
            value.Z) &&
        float.IsFinite(
            value.W);

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
