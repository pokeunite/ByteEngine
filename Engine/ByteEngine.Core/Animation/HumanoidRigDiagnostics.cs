using System.Numerics;

using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Extra authoring diagnostics for a Humanoid mapping.
///
/// "Humanoid Ready" only proves that required semantic slots resolve to source
/// bones. Retarget quality also depends on hierarchy and the source reference
/// pose. These diagnostics deliberately stay separate from runtime validation so
/// unusual but intentional rigs are not rejected automatically.
/// </summary>
public sealed class HumanoidRigDiagnosticReport
{
    public IReadOnlyList<string> Errors { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } =
        Array.Empty<string>();

    public bool HierarchyLooksValid =>
        Errors.Count == 0;

    public bool ApproximatelyTPose { get; init; }

    public bool HasIssues =>
        Errors.Count > 0 ||
        Warnings.Count > 0;
}

public static class HumanoidRigDiagnostics
{
    public static HumanoidRigDiagnosticReport Analyze(
        SkeletonAsset? skeleton,
        HumanoidBoneMap? mapping)
    {
        if (skeleton?.Bones == null ||
            skeleton.Bones.Count == 0)
        {
            return
                new HumanoidRigDiagnosticReport
                {
                    Errors =
                        new[]
                        {
                            "The model has no imported skeleton."
                        }
                };
        }

        mapping ??=
            new HumanoidBoneMap();

        mapping.Normalize();

        Dictionary<string, int> indices =
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

        var errors =
            new List<string>();

        var warnings =
            new List<string>();

        CheckChain(
            HumanoidBone.Hips,
            HumanoidBone.Spine);

        CheckChain(
            HumanoidBone.Spine,
            HumanoidBone.Head);

        CheckChain(
            HumanoidBone.LeftUpperArm,
            HumanoidBone.LeftLowerArm);

        CheckChain(
            HumanoidBone.LeftLowerArm,
            HumanoidBone.LeftHand);

        CheckChain(
            HumanoidBone.RightUpperArm,
            HumanoidBone.RightLowerArm);

        CheckChain(
            HumanoidBone.RightLowerArm,
            HumanoidBone.RightHand);

        CheckChain(
            HumanoidBone.LeftUpperLeg,
            HumanoidBone.LeftLowerLeg);

        CheckChain(
            HumanoidBone.LeftLowerLeg,
            HumanoidBone.LeftFoot);

        CheckChain(
            HumanoidBone.RightUpperLeg,
            HumanoidBone.RightLowerLeg);

        CheckChain(
            HumanoidBone.RightLowerLeg,
            HumanoidBone.RightFoot);

        CheckHelperBoneAssignments();

        HumanoidReferencePose pose =
            HumanoidReferencePose.Capture(
                skeleton,
                mapping);

        bool approximatelyTPose =
            false;

        if (pose.IsReady)
        {
            approximatelyTPose =
                CheckReferencePose(
                    pose,
                    warnings);
        }

        return
            new HumanoidRigDiagnosticReport
            {
                Errors =
                    errors,

                Warnings =
                    warnings,

                ApproximatelyTPose =
                    approximatelyTPose
            };

        void CheckChain(
            HumanoidBone parentSemantic,
            HumanoidBone childSemantic)
        {
            if (!TryIndex(
                    parentSemantic,
                    out int parent) ||
                !TryIndex(
                    childSemantic,
                    out int child))
            {
                return;
            }

            if (IsDescendant(
                    skeleton,
                    child,
                    parent))
            {
                return;
            }

            errors.Add(
                $"{childSemantic} is not below {parentSemantic} in the source skeleton hierarchy.");
        }

        void CheckHelperBoneAssignments()
        {
            foreach (HumanoidBone semantic
                     in HumanoidBoneCatalog.Required)
            {
                if (!mapping.TryGetBoneName(
                        semantic,
                        out string sourceName))
                {
                    continue;
                }

                string normalized =
                    HumanoidRigMapper.NormalizeBoneName(
                        sourceName);

                bool looksLikeHelper =
                    normalized.Contains(
                        "ik",
                        StringComparison.Ordinal) ||
                    normalized.Contains(
                        "ctrl",
                        StringComparison.Ordinal) ||
                    normalized.Contains(
                        "control",
                        StringComparison.Ordinal) ||
                    normalized.Contains(
                        "target",
                        StringComparison.Ordinal) ||
                    normalized.Contains(
                        "pole",
                        StringComparison.Ordinal) ||
                    normalized.Contains(
                        "helper",
                        StringComparison.Ordinal);

                if (!looksLikeHelper)
                {
                    continue;
                }

                warnings.Add(
                    $"{semantic} is mapped to '{sourceName}', which looks like an IK/control/helper bone rather than a deform bone.");
            }
        }

        bool TryIndex(
            HumanoidBone semantic,
            out int index)
        {
            index =
                -1;

            return
                mapping.TryGetBoneName(
                    semantic,
                    out string name) &&
                indices.TryGetValue(
                    name,
                    out index);
        }
    }

    private static bool CheckReferencePose(
        HumanoidReferencePose pose,
        ICollection<string> warnings)
    {
        HumanoidReferenceBonePose? hips =
            pose.GetBone(
                HumanoidBone.Hips);

        HumanoidReferenceBonePose? head =
            pose.GetBone(
                HumanoidBone.Head);

        if (hips == null ||
            head == null)
        {
            return false;
        }

        Vector3 up =
            head.Position -
            hips.Position;

        if (!TryNormalize(
                up,
                out up))
        {
            warnings.Add(
                "Hips and Head overlap in the reference pose, so body orientation cannot be determined.");

            return false;
        }

        bool leftArm =
            CheckArm(
                HumanoidBone.LeftUpperArm,
                HumanoidBone.LeftHand,
                "Left");

        bool rightArm =
            CheckArm(
                HumanoidBone.RightUpperArm,
                HumanoidBone.RightHand,
                "Right");

        bool leftLeg =
            CheckLeg(
                HumanoidBone.LeftUpperLeg,
                HumanoidBone.LeftFoot,
                "Left");

        bool rightLeg =
            CheckLeg(
                HumanoidBone.RightUpperLeg,
                HumanoidBone.RightFoot,
                "Right");

        CheckSides();

        bool tPose =
            leftArm &&
            rightArm &&
            leftLeg &&
            rightLeg;

        if (!tPose)
        {
            warnings.Add(
                "The bind/reference pose is not close to a clean T-pose. Retargeting can look twisted even when the bone names are mapped correctly.");
        }

        return tPose;

        bool CheckArm(
            HumanoidBone upperArmSemantic,
            HumanoidBone handSemantic,
            string side)
        {
            HumanoidReferenceBonePose? upperArm =
                pose.GetBone(
                    upperArmSemantic);

            HumanoidReferenceBonePose? hand =
                pose.GetBone(
                    handSemantic);

            if (upperArm == null ||
                hand == null)
            {
                return false;
            }

            Vector3 direction =
                hand.Position -
                upperArm.Position;

            if (!TryNormalize(
                    direction,
                    out direction))
            {
                warnings.Add(
                    $"{side} arm has effectively zero length in the reference pose.");

                return false;
            }

            /*
             * T-pose arms should be mostly perpendicular to the body-up axis.
             * A moderately relaxed A-pose is accepted, but a strongly downward
             * or vertical arm is flagged because C9 retargeting is sensitive to
             * reference-pose differences.
             */
            return
                MathF.Abs(
                    Vector3.Dot(
                        direction,
                        up)) <=
                0.45f;
        }

        bool CheckLeg(
            HumanoidBone upperLegSemantic,
            HumanoidBone footSemantic,
            string side)
        {
            HumanoidReferenceBonePose? upperLeg =
                pose.GetBone(
                    upperLegSemantic);

            HumanoidReferenceBonePose? foot =
                pose.GetBone(
                    footSemantic);

            if (upperLeg == null ||
                foot == null)
            {
                return false;
            }

            Vector3 direction =
                foot.Position -
                upperLeg.Position;

            if (!TryNormalize(
                    direction,
                    out direction))
            {
                warnings.Add(
                    $"{side} leg has effectively zero length in the reference pose.");

                return false;
            }

            return
                Vector3.Dot(
                    direction,
                    up) <=
                -0.55f;
        }

        void CheckSides()
        {
            HumanoidReferenceBonePose? leftUpperArm =
                pose.GetBone(
                    HumanoidBone.LeftUpperArm);

            HumanoidReferenceBonePose? rightUpperArm =
                pose.GetBone(
                    HumanoidBone.RightUpperArm);

            HumanoidReferenceBonePose? leftHand =
                pose.GetBone(
                    HumanoidBone.LeftHand);

            HumanoidReferenceBonePose? rightHand =
                pose.GetBone(
                    HumanoidBone.RightHand);

            if (leftUpperArm == null ||
                rightUpperArm == null ||
                leftHand == null ||
                rightHand == null)
            {
                return;
            }

            Vector3 leftToRight =
                rightUpperArm.Position -
                leftUpperArm.Position;

            if (!TryNormalize(
                    leftToRight,
                    out leftToRight))
            {
                return;
            }

            Vector3 center =
                (
                    leftUpperArm.Position +
                    rightUpperArm.Position
                ) *
                0.5f;

            float leftSide =
                Vector3.Dot(
                    leftHand.Position -
                    center,
                    leftToRight);

            float rightSide =
                Vector3.Dot(
                    rightHand.Position -
                    center,
                    leftToRight);

            if (leftSide >=
                    0.0f ||
                rightSide <=
                    0.0f)
            {
                warnings.Add(
                    "Left/right arm mapping appears crossed in the reference pose. Check the arm and hand assignments.");
            }
        }
    }

    private static bool IsDescendant(
        SkeletonAsset skeleton,
        int child,
        int ancestor)
    {
        if (child ==
            ancestor)
        {
            return false;
        }

        var visited =
            new HashSet<int>();

        int current =
            child;

        while (current >=
                   0 &&
               current <
                   skeleton.Bones.Count &&
               visited.Add(
                   current))
        {
            current =
                skeleton.Bones[
                    current]
                    .ParentIndex;

            if (current ==
                ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryNormalize(
        Vector3 value,
        out Vector3 normalized)
    {
        float lengthSquared =
            value.LengthSquared();

        if (!float.IsFinite(
                lengthSquared) ||
            lengthSquared <=
                0.000001f)
        {
            normalized =
                Vector3.Zero;

            return false;
        }

        normalized =
            value /
            MathF.Sqrt(
                lengthSquared);

        return true;
    }
}
